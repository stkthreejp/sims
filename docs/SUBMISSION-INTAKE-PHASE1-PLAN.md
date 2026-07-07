# Submission Intake — Phase 1 Build Plan

> **Status:** build-ready (2026-07-07). Companion to `docs/SUBMISSION-INTAKE-AUTOMATION-DESIGN.md` (architecture + resolved §9 decisions). This doc is the concrete implementation sequence: files, signatures, migration, tests, gates. Grounded in a codebase recon (commit-time file/line refs below).
>
> **Settled decisions (design §9):** all-.NET async worker · first-party Claude API + ZDR + no-training + `inference_geo` · Claude vision reads + classifies + extracts + detects PDF boundaries in one pass · deliverables attached to the SIMS submission · auto-trigger on every email-created submission **behind a feature-flag kill-switch** · skill logic reimplemented in C#.

## Phase 1 scope (the MVP)
Inbound email → submission (existing) → **auto-enqueued intake job** → worker renders the combined PDF, asks Claude for the page-span→(form, LOB) map + quoting line + per-LOB field extraction, **splits the PDF and files each part as a submission Attachment**, runs the **mandatory-items completeness check**, writes the **`00_ACCOUNT_SUMMARY`**, and updates the submission (LinesOfBusiness + extracted entities) and job status. UI shows an intake status chip; admin has a kill-switch.

**Deferred to later phases (not in Phase 1):** loss-summary xlsx + equipment valuation (Phase 2); OFAC/address/web reports (Phase 3). The worker's stage pipeline is structured so those slot in as added stages.

## Green-light findings from recon (reduce the work)
- **Anthropic REST is already scaffolded:** a named `HttpClient("anthropic")` (BaseAddress `https://api.anthropic.com`, 90s configurable timeout) is registered in `InfrastructureServiceExtensions.cs`, and `Anthropic:BaseUrl` / `Anthropic:Model` / `Anthropic:ApiKey` config keys already exist. No official .NET SDK — we call `/v1/messages` over REST (same pattern as `GeminiExtractionService`). ZDR/no-training are contractual; `inference_geo` is a body param.
- **PDF stack already present with Linux native assets:** `SkiaSharp.NativeAssets.Linux.NoDependencies`, `HarfBuzzSharp.NativeAssets.Linux`, `Syncfusion.Pdf.Net.Core`, `QuestPDF` are all referenced. → **PDF splitting** uses Syncfusion.Pdf (already there); **report/summary PDFs** use QuestPDF (already there). Only **PDF→image rendering** needs one new package.
- **Feature-flag pattern exists:** `FmcsaScheduledJobsWorker` reads `IOptions<FmcsaJobSettings>.Enabled` and returns early if off. The kill-switch mirrors this exactly.
- **Extraction is already try/caught** in `InboundEmailService.CreateSubmissionFromEmailAsync` (submission still created if extraction fails) — the async move preserves that resilience.

## New NuGet packages (SIMS.Infrastructure)
- **`PDFtoImage`** (SkiaSharp-based, bundles PDFium, **no Ghostscript**) — render PDF pages → images for Claude vision. SkiaSharp Linux assets are already present, so this adds no new native system dep. *(Alternative if we want zero new packages: Syncfusion's rasterizer — but it's a separate Syncfusion assembly not currently referenced, so PDFtoImage is the smaller add.)*
- **`MsgReader`** — un-nest forwarded `.msg` attachments (loss runs hide there). Pure managed.
- *(Phase 2 will add ClosedXML/EPPlus for xlsx; QuestPDF already present for Phase 3 report PDFs.)*

Add via `dotnet add`; keep versions pinned. `dotnet restore` + `dotnet build` after.

---

## Step 1 — Rename the extraction contract (mechanical, zero behavior change)
Recon confirms **7 files, no test doubles**. Rename:
- `IGeminiExtractionService` → `IDocumentExtractionService`
- `GeminiExtractionService` → *(unchanged this step — see Step 2; the Gemini impl stays as a fallback impl of the renamed interface until Claude lands, then is removed)*
- `GeminiLobExtraction` → `DocumentLobExtraction`
- `GeminiExtractionResult` → `DocumentExtractionResult`
- namespace `SIMS.Application.DTOs.Gemini` → `SIMS.Application.DTOs.DocumentExtraction`

Files (in dependency order):
1. `src/SIMS.Application/DTOs/Gemini/GeminiExtractionResult.cs` → rename file to `DocumentExtractionResult.cs`; update namespace + `GeminiLobExtraction`/`GeminiExtractionResult` + keep `InferLinesOfBusiness`/`MergeInto` + all `Extracted*` child classes **verbatim** (every field preserved).
2. `src/SIMS.Application/Interfaces/Services/IGeminiExtractionService.cs` → rename file to `IDocumentExtractionService.cs`; update interface name + `using`.
3. `src/SIMS.Infrastructure/Services/GeminiExtractionService.cs` → keep class name `GeminiExtractionService` but implement `IDocumentExtractionService`; update `using` + return types (`List<DocumentLobExtraction>?`). *(Renaming the class is optional; leaving it named Gemini is fine since it's removed in Step 2.)*
4. `src/SIMS.Infrastructure/Extensions/InfrastructureServiceExtensions.cs:115` → `services.AddScoped<IDocumentExtractionService, GeminiExtractionService>();`
5. `src/SIMS.Application/Services/InboundEmailService.cs` → all 11 references (field `_gemini`→`_extractor` optional, ctor param type, `List<DocumentLobExtraction>?`, static calls `DocumentExtractionResult.InferLinesOfBusiness/.MergeInto`, `new DocumentLobExtraction(...)`, method param `DocumentExtractionResult data`).
6. `src/SIMS.Infrastructure/Services/DocumentAiNormalizationService.cs` → `using` + `MapSubmissionFields(DocumentExtractionResult target, ...)`.
7. `src/SIMS.Application/DTOs/DocumentAI/DocumentAiNormalizationPreview.cs` → `using` + `public DocumentExtractionResult SubmissionData { get; set; } = new();`
8. `tests/.../DocumentAiNormalizationServiceTests.cs` — no direct change (uses the preview DTO); recompiles.

**Gate:** `dotnet build` + `dotnet test` (532 tests) green. Commit by explicit path. **No behavior change** — this is the safe first commit.

---

## Step 2 — `IDocumentExtractionService` gains the vision method + `ClaudeDocumentExtractionService`
**Interface addition** (keep the existing `ExtractFromAttachmentsAsync` for back-compat during transition):
```csharp
// New: the one-pass vision analysis the intake worker uses.
Task<SubmissionAnalysis?> AnalyzeSubmissionAsync(
    IReadOnlyList<RenderedPage> pages,   // page index + PNG bytes (from PDFtoImage)
    string? emailBodyContext,            // broker email text — underwriting intent
    CancellationToken ct = default);
```
New DTOs (in `DTOs.DocumentExtraction`):
```csharp
public record RenderedPage(int PageNumber, byte[] PngBytes);
public record FormSpan(int StartPage, int EndPage, string Form /*Acord125…*/, string LineOfBusiness);
public class SubmissionAnalysis {
    public List<FormSpan> Boundaries { get; set; } = [];      // page-span → (form, LOB) map
    public string? QuotingLineOfBusiness { get; set; }         // monoline pick
    public List<DocumentLobExtraction> PerLob { get; set; } = [];  // reuse existing shapes
    public string? Confidence { get; set; }
    public string? Rationale { get; set; }
}
```
**`ClaudeDocumentExtractionService : IDocumentExtractionService`** (`SIMS.Infrastructure/Services/`):
- ctor injects `IHttpClientFactory` (`CreateClient("anthropic")` — already configured), `IConfiguration`, `ILogger`. **Key is nullable, validated lazily** at the call site — never in the ctor (`anti-pattern-ctor-config-throw`).
- `AnalyzeSubmissionAsync`: build a `/v1/messages` request — model = `Anthropic:Model` (default `claude-opus-4-8`; ZDR-eligible), `max_tokens` generous, **`inference_geo`** from `Anthropic:InferenceGeo` (default `"us"`), content = one `image` block per rendered page (`source:{type:"base64",media_type:"image/png",data:…}`) + a text instruction, and **structured output** (`output_config.format` = JSON schema for `SubmissionAnalysis`). Headers: `x-api-key`, `anthropic-version: 2023-06-01`. Parse the JSON into `SubmissionAnalysis`. On non-2xx / parse failure → log + return null (worker marks the job Failed, submission still exists).
- Register in DI: `services.AddScoped<IDocumentExtractionService, ClaudeDocumentExtractionService>();` (replaces the Gemini registration). Delete `GeminiExtractionService.cs` + the legacy `DocumentExtraction` AI-settings knob once nothing references it.
- Config additions (appsettings.example.json + Key Vault): `Anthropic:Model`, `Anthropic:InferenceGeo`, `Anthropic:ApiKey` (flat Key Vault name `AnthropicApiKey`, per the Xero-style override pattern).

**Gate:** `dotnet build`. (Unit-testable with a fake `HttpMessageHandler`; see Tests.)

---

## Step 3 — `IntakeJob` entity + EF config + migration + enum additions
**`IntakeJob : BaseEntity`** (`SIMS.Domain/Entities/`):
```csharp
public Guid SubmissionId { get; set; }
public IntakeJobStatus Status { get; set; } = IntakeJobStatus.Queued;  // Queued|Running|NeedsReview|Completed|Failed
public string? Stage { get; set; }            // e.g. "Rendering","Analyzing","Splitting","Completeness","Summary"
public DateTime? StartedAt { get; set; }
public DateTime? CompletedAt { get; set; }
public int AttemptCount { get; set; }
public string? ErrorMessage { get; set; }
public string? ResultJson { get; set; }       // SubmissionAnalysis + completeness checklist snapshot
public Submission Submission { get; set; } = null!;
```
- **`IntakeJobConfiguration : IEntityTypeConfiguration<IntakeJob>`** → `ToTable("intake_jobs")`, key, `HasIndex(j => new { j.Status, j.CreatedAt })` (worker queue drain), FK to submissions `OnDelete(Restrict)`, max-lengths on string cols. Follows the `AttachmentConfiguration` style (snake_case).
- **`Submission`** gains `QuotingLineOfBusiness` (string?, nullable) — distinct from the existing `LinesOfBusiness` JSON (which holds all present lines, quoting + flagged).
- **`DocumentType`** enum: add `AccountSummary` (Phase 1). *(Phase 2/3 add `LossSummary`, `EquipmentValuation`, `OfacScreen`, `AddressReport`, `WebReport`.)*
- **`EmailAttachmentDocumentType`**: add `Acord127` (auto), `Acord146` (equipment/IM schedule), `Mvr` — plus extend the `MapDocumentType` switch in `InboundEmailService`.
- **Migration:** `dotnet ef migrations add AddIntakeJob --project src/SIMS.Infrastructure --startup-project src/SIMS.API --context ApplicationDbContext` (note the `--context` — there are two DbContexts). Review `Up()/Down()`. **Apply to the test DB as part of deploy** (migrations are manual — this is the class of gap that caused the inbox 500 root-cause chase).

**Gate:** `dotnet build` + `dotnet test`.

---

## Step 4 — Async flow: feature flag → enqueue → worker pipeline
- **`IntakeSettings`** (`Enabled` bool, `PollingIntervalMinutes` int, `Model`/`InferenceGeo` optional) bound via `services.Configure<IntakeSettings>(config.GetSection("Intake"))`. This `Enabled` flag is the **kill-switch**.
- **Enqueue on auto-trigger:** in `InboundEmailService.CreateSubmissionFromEmailAsync`, after the submission is created, if `IntakeSettings.Enabled` → add an `IntakeJob { SubmissionId, Status=Queued }` and save. **Remove the inline Gemini extraction call** (extraction now happens in the worker). If the flag is off, no job is enqueued (submission still created — unchanged UX).
- **`IIntakeProcessingService` / `IntakeProcessingService`** (`SIMS.Application/Services/`): `ProcessPendingIntakesAsync(ct)` — claim the oldest `Queued` job (set Running/StartedAt/AttemptCount++), then run stages, each updating `Stage`:
  1. **Ingest normalize** — pull submission attachments; un-nest `.msg` (MsgReader) into their real PDFs; discard junk (signature/logo images by name/type).
  2. **Render** — the combined ACORD PDF → page PNGs (PDFtoImage). Cap pages/log if truncated.
  3. **Analyze** — `IDocumentExtractionService.AnalyzeSubmissionAsync(pages, emailBody)` → `SubmissionAnalysis`.
  4. **Split & file** — Syncfusion.Pdf writes each `FormSpan` to its own PDF; upload via `IBlobStorageService.UploadAsync`; create `Attachment` rows (`EntityType=Submission`, `DocumentType` mapped from form; quoting-line forms as-is, non-quoting lines flagged in `Description` `"OTHER LINE — REVIEW"`).
  5. **Completeness check** — reimplement the skill's mandatory-items + date-math (ACORD app for quoting line, SMM supplemental, 5 yrs loss runs, runs valued within 90 days of effective; Auto adds MVRs) → checklist object.
  6. **Account summary** — render `00_ACCOUNT_SUMMARY` (QuestPDF, already referenced) with checklist-at-top + snapshot + lines/split actions + document index; upload + `Attachment(DocumentType.AccountSummary)`.
  7. **Persist** — update `Submission.LinesOfBusiness` (+ `QuotingLineOfBusiness`) and extracted entities; set job `Completed` (or `NeedsReview` if low confidence / missing mandatory items), `ResultJson`, `CompletedAt`. On any stage throw → log, job `Failed` + `ErrorMessage` (submission untouched).
- **`IntakeWorker : BackgroundService`** (`SIMS.Infrastructure/Workers/`) — mirrors `EmailIngestionWorker`: `IServiceScopeFactory` loop, `IOptions<IntakeSettings>` → return early if `!Enabled` (kill-switch), `Task.Delay(PollingInterval)`, scope-per-iteration resolves `IIntakeProcessingService`, catch-and-log (don't rethrow). Register `services.AddHostedService<IntakeWorker>()` + `services.AddScoped<IIntakeProcessingService, IntakeProcessingService>()`.

**Gate:** `dotnet build` + `dotnet test`.

---

## Step 5 — API + frontend
**Backend endpoints** (`SubmissionsController`, `[Authorize(Policy = AppPermissions.UnderwritingManage)]`):
- `GET /api/v1/submissions/{id}/intake` → `{ status, stage, extractionStatus, startedAt, completedAt, errorMessage, checklist }` (latest `IntakeJob` for the submission).
- `POST /api/v1/submissions/{id}/reintake` → enqueue a fresh job (manual re-run; also `UnderwritingManage`).
- **Admin kill-switch:** `GET/PUT /api/v1/admin/intake-settings` (`[Authorize(Policy = AppPermissions.AdminSystemManage)]`) reading/writing the `Intake:Enabled` flag. *(If runtime-togglable persistence is wanted rather than a redeploy, store the flag in a small settings table/row; otherwise document that toggling `Intake:Enabled` is a config change. Recommend a DB-backed toggle so ops can pause without a deploy — decide at build time.)*

**Frontend:**
- `src/api/submissions.api.ts` → `getIntake(id)`, `reintake(id)`; new `src/api/admin.api.ts` submodule `adminIntakeSettingsApi`.
- **`SubmissionDetailPage.tsx`** — add an intake status chip/section next to the existing extraction banner (it already handles `extractionStatus` + a re-extract button at ~L180/L1340). react-query `['submissions', id, 'intake']` with `refetchInterval` while `status ∈ {Queued,Running}`; "Re-run intake" button → `reintake` mutation → invalidate.
- **`InboxPage.tsx`** — optional: show a small "intake processing" hint on rows whose submission is mid-intake.
- **`IntakeProcessingAdminPage.tsx`** (mirror `AiSettingsAdminPage.tsx`) — the kill-switch toggle; route + nav entry gated on `AdminSystemManage`.

**Gate:** `npx tsc -b` (NOT `--noEmit` — the composite build is the real gate; see the ReportsPage/Compliance lesson) + `npm run build`.

---

## Tests (service-layer, the CI gate)
- **Rename:** existing 532 pass unchanged.
- **`ClaudeDocumentExtractionService`:** fake `HttpMessageHandler` returning a canned `/v1/messages` body → asserts boundaries/quoting-line/per-LOB parse correctly; a non-2xx and a malformed body → returns null (no throw); missing key → throws only when called, not at construction.
- **`IntakeProcessingService`:** in-memory/SQLite DB + fake `IDocumentExtractionService` + fake `IBlobStorageService` → a Queued job runs end-to-end to Completed, creates the expected Attachment rows, sets `LinesOfBusiness`/`QuotingLineOfBusiness`; an extraction-returns-null path → job Failed, submission untouched; flag-off → no job dequeued.
- **Completeness check:** unit tests for the date-math (stale loss run > 90 days; missing mandatory item; Auto-needs-MVR).
- Keep test-doubles in sync with the interface additions (the CS0535 lesson — `RecordingQuoteService`-style stubs).

## Verification gates & discipline
- Backend: `dotnet build` + `dotnet test` after every step. Frontend: `npx tsc -b` + `npm run build`.
- **Commit by explicit path; never `git add -A`** — the WS5/SurplusLines workstream shares the tree. Don't touch `SurplusLinesAdminPage.tsx` / `CompanyLicensesAdminPage.tsx`.
- One commit per step (rename → Claude service → entity/migration → worker/flow → API/UI), each independently green.
- **Migration must be applied to the test DB on deploy** (manual `database update`) — otherwise the intake endpoints 500 the same way the inbox did.

## Go-live gates (from design §7)
- Enable **ZDR** on the org + sign the **DPA** (no-training + `inference_geo` region) with Anthropic before real insured data flows — contractual, compliance sign-off.
- Model stays ZDR-eligible (**Opus 4.8** default / Sonnet 5 / Haiku).
- Ship with `Intake:Enabled=false` on first deploy; flip on once watched on a few real submissions (auto-trigger + kill-switch).

## Out of scope (Phase 2/3 — pipeline stages slot in)
- Phase 2: loss-run parse → `Loss_Summary.xlsx` (ClosedXML/EPPlus); IM equipment valuation.
- Phase 3: OFAC screen, address/aerial report, web/business report (Google Maps + OFAC keys, QuestPDF).
- Phase 4: outdated-loss-run detection, multi-submission throughput tuning, manual per-stage re-run.

## Open build-time micro-decisions
1. **Kill-switch storage** — config flag (redeploy to toggle) vs. a DB-backed settings row (ops toggles live). *Recommend DB-backed* so a bad batch can be paused without a deploy.
2. **Rendering resolution** — DPI for page→PNG (accuracy vs. token cost). Start ~150 DPI; tune with `count_tokens` on real submissions.
3. **`GeminiExtractionService` removal timing** — delete in Step 2 (once Claude registered) vs. keep as a config-selectable fallback. *Recommend delete* — decision already says Gemini is legacy.
