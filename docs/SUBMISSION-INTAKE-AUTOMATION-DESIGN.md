# SIMS Submission Intake Automation — Design

> **Status:** DRAFT for review (2026-07-07). **Goal:** make SIMS take over what the local `smm-submission-intake` skill does today by hand — turn a raw broker email/PDF bundle into an organized, worked-up, monoline submission — using **deterministic tooling + Claude, no Gemini**. Architecture recommendation (see §3): an **all-.NET intake worker** (OCR via Claude vision or the Google Doc-AI path SIMS already has), not a separate Python service. This doc is the architecture + phased plan; §9 collects the decisions only Jeremiah can make.

---

## 1. What we're automating

The `smm-submission-intake` skill (at `~/ClaudeProj/UA Skill/.../smm-submission-intake/`) does this per submission:

0. **Token discipline** — one submission per subagent; get page content out as *text* (OCR) before ever using vision.
1. **Ingest & inventory** — unzip; read `EMAIL_BODY.txt` for broker context; **un-nest forwarded `.msg`** (loss runs hide here) via `extract-msg`; discard junk (signature/logo images); list real docs + page counts.
2. **Classify & split** the combined ACORD PDF by **document type AND line of business** — OCR ladder (pypdf text → ocrmypdf/Tesseract → optional Document AI → **vision only for pages OCR couldn't read**) produces a page-span→(form, LOB) map; split each span to its own PDF. **Monoline rule:** the quoting line's forms go to `APPLICATION/`; every other line is split to `OTHER_LINES_FLAGGED/` with a `_REVIEW` suffix (never silently dropped).
3. **Parse loss runs** → `Loss_Summary.xlsx` (rows by LOB × policy year, subtotals, UW-input Premium, auto Loss Ratio). Keep + prefix `OUTDATED_` superseded runs.
   - **3b (IM only):** equipment valuation xlsx (insured vs. market value, over/under-insurance flags) from the ACORD 146.
4. **Intake reports** — OFAC screen, address/property report (geocode + satellite aerial + FEMA flood + parcel), web/business report (web/social, FMCSA/USDOT, adverse media).
5. **Completeness check** (mandatory items: ACORD app for the quoting line, SMM supplemental, 5 yrs loss runs, runs valued within 90 days of effective; Auto also needs MVRs) → then the **`00_ACCOUNT_SUMMARY.md`** UW orientation (checklist at top).
6. **Present** the filed deliverable folder.

**Key SMM constraint:** SMM quotes **monoline**, so splitting a bundled package by line and getting the *quoting* line right is the whole point.

## 2. What SIMS already has

- **Inbound email** → `InboundEmailsController` → `InboundEmailService`. `GET /inbound-emails` (the Submission Inbox), `GET /{id}`, `POST /{id}/create-submission`, `POST /{id}/re-extract`.
- `CreateSubmissionFromEmailAsync` today: copies email attachments to the submission, calls the (now-legacy) `GeminiExtractionService` **inline** for LOB + field extraction, and creates the `Submission` with detected `LinesOfBusiness`. Extraction is already `try/catch`-guarded → a failure marks `extractionStatus='Failed'` but the submission is still created.
- **Extraction contract worth keeping:** `IGeminiExtractionService.ExtractFromAttachmentsAsync(attachments, lobHint)` → `List<GeminiLobExtraction>` (one per LOB), where each carries a rich `GeminiExtractionResult` (DescriptionOfOperations, Dba, EntityType, YearsInBusiness, Drivers, Vehicles, Locations, PriorCarriers, Supplemental, GLCoverages, GLClassifications, IMCoverages, Equipment) + `InferLinesOfBusiness()` fallback. **These DTO shapes are good and should be reused** — only the *engine* behind them changes (Gemini → Claude).
- `EmailAttachmentDocumentType` enum: `Unknown, Acord125, Acord126, LossRun, ScheduleOfValues, SignedApplication`.
- Blob storage for attachments; a background-worker pattern already exists (`EmailIngestionWorker`, `TaskNotificationWorker`); Anthropic model reference in the `claude-api` skill.

**Decision already made:** no Gemini. `GeminiExtractionService` + the AI-settings "DocumentExtraction" knob are legacy (see `project_submission_intake_direction` memory). LOB detection will be **Claude reading the OCR'd application data and deciding** the line(s) — replacing Gemini's detection pass.

## 3. Where the pipeline runs — an all-.NET worker is viable (recommended)

The skill is Python because of its native stack — `ocrmypdf`/Tesseract/Ghostscript (OCR), `extract-msg`, `pdf2image`, `reportlab`/`openpyxl`. But **OCR is the only piece that forced Python, and it has strong .NET-native options** — including one SIMS already ships. So the pipeline can run as an **all-.NET background worker** (same pattern as the existing `EmailIngestionWorker`), with **no Python container and no cross-runtime contract**.

**OCR / read-the-scanned-form options in .NET:**

| Option | How | Trade-off |
|---|---|---|
| **Claude vision directly** ⭐ MVP | Render PDF pages → images in .NET (PDFium via `Docnet.Core`/`PDFtoImage` NuGet — no Ghostscript), send to Claude vision → **text + LOB + fields in one call** | Collapses OCR+extraction into the Claude call we're already making; per-page vision tokens; all pages → Anthropic. Server pipeline (per-submission API call), so the skill's context-window token-discipline concern doesn't apply. |
| **Google Document AI** | **Already wired in SIMS** — `DocumentAiNormalizationService` maps `DocumentAiExtractionResult` → the extraction DTOs. Reuse/extend it; Claude reads the text for judgment/fields. | Proven here; text-first → cheaper Claude calls; PII to Google (skill flags this too). |
| **Azure AI Document Intelligence** | `Azure.AI.DocumentIntelligence` NuGet (managed, no native binaries) → Claude on the text | Production OCR; PII stays in Azure (same cloud as SIMS); cheaper Claude calls. |
| Managed Tesseract | `Tesseract` NuGet (offline) | No cloud PII, but ship tessdata/native libs + a rasterizer; most setup. |

**Rest of the Python stack → .NET equivalents:** `.msg` un-nest → **MsgReader**; PDF split by page-span → **PdfSharpCore/iText7/Docnet**; PDF→image → **Docnet.Core/PDFtoImage** (PDFium NuGet); xlsx (loss/equipment) → **ClosedXML/EPPlus**; PDF reports → **QuestPDF**; geocode/aerial + OFAC → `HttpClient`.

**Architecture options:**

| Option | Runtime | Verdict |
|---|---|---|
| **A. All-.NET intake worker** ⭐ recommended | One .NET background worker (NuGet OCR/render/xlsx/pdf); OCR = Claude vision (MVP) or the existing Google Doc AI path | Single runtime the codebase already speaks; one deploy; no Python container or callback contract; reuses `EmailIngestionWorker` pattern + `DocumentAiNormalizationService`. |
| B. Separate Python worker (queue + callback) | Python container reusing the skill's scripts verbatim | Only if we want the skill's exact scripts unchanged and accept a second runtime/deploy + cross-service auth. Fallback, not default. |
| C. .NET shells out to Python | Python+native deps bundled with .NET | Rejected — fat/fragile image, couples runtimes. |

**Recommendation: Option A — an all-.NET intake worker.** It stays async (a background worker draining an intake queue), keeps the .NET API as orchestrator/system-of-record, and eliminates the polyglot deploy. Reimplement the skill's stages in .NET (they're mostly orchestration around OCR + Claude + file generation), reusing the extraction-DTO mapping SIMS already has.

## 4. Target flow (async)

```
Inbound email (Graph)  ─▶  /inbound-emails (Inbox)  ─▶  POST /inbound-emails/{id}/create-submission
                                                              │  (fast: create Submission, copy attachments,
                                                              │   status = IntakeQueued; enqueue IntakeJob)
                                                              ▼
                                            ┌──────────  intake queue  ──────────┐
                                            ▼                                     │
                            .NET Intake Worker (drains intake queue)             │
                            1 un-nest .msg (MsgReader) / discard junk            │
                            2 render/OCR → page-span→(form,LOB) map → split PDFs │
                            2b Claude: read pages (vision) or Doc-AI text →      │
                               decide LOB(s) + quoting line + extract fields     │
                            3 parse loss runs → Loss_Summary.xlsx                │
                            3b IM: equipment valuation xlsx                      │
                            4 reports (OFAC / address+aerial / web)              │
                            5 completeness check + 00_ACCOUNT_SUMMARY.md         │
                            ▼                                                     │
              writes deliverables to Blob; POST /intake/{jobId}/result  ────────▶│ .NET
                            │                                                     │
                            ▼                                                     ▼
        Submission updated: LinesOfBusiness (quoting + flagged), extracted    UI: inbox item shows
        entities (drivers/vehicles/GL/IM/equipment via existing DTO shapes),  "Intake processing…"
        deliverables as Attachments, completeness checklist, status=Ready     then updates when done
```

**Why async:** OCR + research + report generation is slow and image-heavy. The current *synchronous* inline extraction blocks `create-submission`; moving it behind a job makes submission creation instant and lets intake take its time (and retry).

## 5. Claude's role (replacing Gemini)

Two Claude calls in the worker (Anthropic Messages API, **latest Claude model per the `claude-api` reference**, **tool-use / structured JSON output** so results validate):

1. **LOB decision** — give Claude the application content + broker `EMAIL_BODY.txt` and ask it to identify the line(s) present and **which is the quoting line** (monoline), returning `{ lines: [...], quotingLine, confidence, rationale }`. "Application content" is either the **rendered page images** (Claude-vision path) or **OCR text** (Google Doc AI / Azure path) per §3. This is exactly "Claude reads the data and makes the decision."
2. **Field extraction per LOB** — Claude extracts into the **existing `GeminiExtractionResult` shapes** (drivers, vehicles, locations, prior carriers, supplemental, GL coverages/classifications, IM coverages, equipment). Reuse those DTOs verbatim; keep `InferLinesOfBusiness()` as the fallback.

**Interface change:** rename `IGeminiExtractionService` → `IDocumentExtractionService` (keep the method shape); add a `ClaudeDocumentExtractionService` implementation used by the worker (or a thin .NET client if extraction stays server-side). The rich DTOs (`GeminiExtractionResult` etc.) get renamed `DocumentExtractionResult` but keep every field. This is a mechanical rename + one new impl; every consumer keeps working.

**Deterministic-first where it pays:** the cheap steps (page render/split, `.msg` un-nest, and text extraction via Doc AI/Azure when that path is used) run in .NET with no model; Claude is reserved for the *judgment* (which line to quote) and *field extraction*. On the Claude-vision path, the read and the extraction collapse into one call.

## 6. Data model & API additions

- **`IntakeJob`** entity: `Id, SubmissionId, Status (Queued|Running|NeedsReview|Completed|Failed), Stage, StartedAt, CompletedAt, ErrorMessage, ResultJson`. Drives the UI "processing/needs-review/ready" state.
- **Submission** gains an intake status + a `QuotingLineOfBusiness` (distinct from the flagged other lines already storable in `LinesOfBusiness`).
- **Deliverables as Attachments** — the account summary, loss summary xlsx, equipment xlsx, and the three report PDFs are stored as submission `Attachment`s with a new `DocumentType` set (`AccountSummary, LossSummary, EquipmentValuation, OfacScreen, AddressReport, WebReport`) so they show in the existing Documents section.
- **Completeness checklist** — persist the Stage-5 checklist (mandatory items + pass/fail + the stale-loss-run date math) on the submission so the UW sees missing items up front (mirrors the skill's "checklist at the top").
- **New endpoints:** `POST /intake/{jobId}/result` (worker callback, service-auth), `GET /submissions/{id}/intake` (status + checklist for the UI), `POST /submissions/{id}/reintake` (re-run).
- **`EmailAttachmentDocumentType`** expands to cover what the classifier detects (Acord127 auto, Acord146 equipment/IM schedule, SupplementalApp, Mvr, etc.).

## 7. Config, security & PII

- **Anthropic API key** (Claude) — Key Vault; the worker degrades gracefully if absent (extraction skipped, submission still created — same principle that fixed the inbox 500).
- **Google Maps** (geocode + Static Maps aerial) and **OFAC-API** keys — Key Vault; degrade to placeholders like the skill does (address report → map link; OFAC → "screen manually" note).
- **PII egress depends on the OCR path (§3):** Claude-vision sends all page images to Anthropic; Google Doc AI / Azure Document Intelligence send pages to that cloud (Azure = the same cloud SIMS already runs in); only managed-Tesseract keeps OCR fully local. Whichever path, the Claude judgment/extraction call receives application content — call the data flow out for compliance sign-off.
- Reuse the audit's lesson: **no config validation in service constructors** — validate the Anthropic/Maps/OFAC keys lazily at call time so a missing key never breaks unrelated endpoints (see `anti-pattern-ctor-config-throw`).

## 8. Phasing (ship value early)

- **Phase 1 — Intake spine + LOB + organized file set (highest value):** the .NET intake worker + intake queue; un-nest `.msg`; render/OCR + doc-type/LOB split; **Claude LOB decision + field extraction** (replaces Gemini) populating the existing extracted entities; deliverables filed as attachments; **completeness check + account summary**. This alone replaces the manual "split + work up + is it complete" grind.
- **Phase 2 — Loss & equipment:** `Loss_Summary.xlsx` (with the loss-ratio worksheet) and IM `Equipment_Valuation.xlsx`.
- **Phase 3 — Reports:** OFAC screen, address/aerial report, web/business report.
- **Phase 4 — Polish:** re-intake, outdated-loss-run detection, MVR handling for Auto, multi-submission throughput.

Each phase is independently shippable; Phase 1 is the MVP.

## 9. Decisions needed (Jeremiah)

1. **Runtime** — Option A (all-.NET intake worker, recommended) vs. B (separate Python worker reusing the skill scripts). Recommend A now that OCR has .NET-native options (Claude vision / the existing Google Doc AI path / Azure Document Intelligence). Sub-decision: which OCR path for the read step — Claude vision (MVP simplicity) vs. Doc-AI text-first (cheaper Claude, PII in a known cloud)?
2. **PII to Anthropic** — OK to send OCR'd application text (and rare page images) to the Claude API? (Tesseract-first keeps most in-house.) Compliance sign-off needed.
3. **Sync vs async** — confirm the async job model (recommended) vs. keeping a synchronous best-effort extraction on `create-submission` for small bundles.
4. **Skill logic port** — reimplement the skill's stages in .NET (fits Option A; recommended) vs. keep the Python `scripts/` verbatim (only under Option B). The main logic to port is `detect_form_boundaries.py` (the doc-type/LOB boundary map); the rest is orchestration around OCR + Claude + file generation.
5. **Deliverables destination** — attach to the submission in SIMS (recommended) and/or also drop the folder to a share (as the skill does today)?
6. **Trigger scope** — auto-run intake on every create-submission-from-email, or a manual "Run intake" button first (safer for rollout)?

## 10. Non-goals (initially)
Not replacing underwriter judgment (everything is "intake intelligence, verify before binding"); not the Intake Unpacker desktop app; not multi-tenant; not real-time (async, minutes-scale, is fine).

---

*Companion context: `smm-submission-intake` SKILL.md (source of the workflow); `docs/UI-AUDIT-2026-07-05.md` (the inbox surfaced this); memory `project-submission-intake-direction`, `anti-pattern-ctor-config-throw`.*
