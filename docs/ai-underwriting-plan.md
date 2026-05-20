# SIMS AI Underwriting Assistant — Implementation Plan

**Revised Architecture — May 2026**
Specialty Market Managers, LLC — Confidential

---

## Executive Summary

Sixfold is a purpose-built AI underwriting platform used by carriers like Zurich North America and Skyward Specialty. It ingests submissions and supporting documents, scores them against underwriting guidelines, auto-detects referral triggers, and generates AI narrative write-ups with source citations. Zurich reported savings of up to two hours per submission after deployment.

This plan replicates Sixfold's core value at SMM's scale using a deliberate two-vendor AI architecture:

| Vendor | Task | Replaces |
|---|---|---|
| **Google Document AI** | Document extraction | `GeminiExtractionService` — all PDF intake: ACORD forms, SOVs, loss runs, inbound email attachments, and direct uploads |
| **Configurable LLM API** | Scoring, flags, narratives | Any planned Gemini generative calls. Claude Sonnet is the recommended default because the SMM Underwriter skill already exists there; OpenAI can be added as an approved alternative through Admin model settings. |

SIMS already has a strong foundation: the existing Gemini extraction pipeline (to be replaced), structured loss history, live FMCSA data, the UW Writeup with all narrative and referral fields Sixfold would populate, and Azure Blob Storage holding every document available for re-processing.

---

## Vendor Rationale

### Google Document AI — Extraction

The current `GeminiExtractionService` sends raw PDF bytes inline to a general LLM. Google Document AI is purpose-built for document understanding with insurance/lending vertical processors pre-trained on ACORD forms, loss runs, and schedules of values.

- Insurance vertical processors handle ACORD 125, 126, SOVs, and loss runs without prompt engineering
- Returns structured entities with **confidence scores and page/bounding-box citations** — directly enables Sixfold-style source citation per field
- Handles poor scan quality and multi-document PDFs more reliably than inline LLM extraction

The cost premium is justified by reliability and citation capability, which are key trust-building features for underwriters.

### Configurable LLM API — Scoring, Flags, and Narratives

Generative tasks require judgment, not extraction. Claude Sonnet is the recommended default because the **SMM Underwriter skill already exists** and encodes SMM's appetite, programs, thresholds, and risk context. The SIMS implementation should still keep the LLM provider configurable so OpenAI or a future approved model can be tested or selected without rewriting underwriting workflows.

- SMM Underwriter skill content becomes the default system prompt for Claude calls — Claude is pre-calibrated to SMM before seeing a submission
- No additional guidelines embedding, vector database, or fine-tuning required — the skill is the guidelines layer
- Claude's reasoning quality on insurance judgment tasks is well-suited to the nuanced calls an underwriter makes
- OpenAI-compatible prompt versions can reuse the same SMM underwriting rubric, structured output schema, and evaluation set once added to the approved model registry

#### SMM Underwriter Skill — Planned Enhancements

- **Explicit 1-5 scoring rubric**: define what combination of loss ratio, TIV, driver profile, and ops type maps to each score tier for each active LOB
- **Referral trigger thresholds**: embed the exact dollar amounts, ratios, and conditions from the UW Writeup referral flags so flag detection matches what underwriters already expect
- **Program-specific risk tolerances**: Longleaf IM, Longleaf GL, AL, and APD each have different appetite profiles — scoring should reflect the specific program being quoted
- **FMCSA interpretation guidance**: define which BASIC score thresholds and safety rating values are meaningful risk signals vs. background noise for the SMM book

---

## Sixfold Feature Map

| Sixfold Feature | Vendor | SIMS Today | Plan |
|---|---|---|---|
| Submission intake from documents | Doc AI | Gemini (email only) | Replace `GeminiExtractionService`; cover inbound email and direct uploads |
| Appetite scoring (1-5) | Configurable LLM | None | Phase 2: `AiRiskScoringService` with Claude Sonnet default and Admin-selectable approved models |
| Referral trigger detection | Configurable LLM + DB | Manual checkboxes | Phase 3: math flags from DB + selected LLM for judgment-based flags |
| AI narrative generation | Configurable LLM | Blank text fields | Phase 4: pre-populate all writeup narrative fields; UW edits before submit |
| Source citations | Doc AI | None | Included in Doc AI output: page and bounding box per extracted field |
| Submission triage queue | N/A | Date-sorted list | Phase 6: swim-lane view sorted by AI risk score |
| FMCSA enrichment | Existing | Already live | No change; feeds into AI scoring context |
| Institutional intelligence | Configurable LLM | None | Phase 7: link scores to bind/decline outcomes over time |

---

## Implementation Roadmap

| # | Phase | Vendor(s) | Backend | Frontend | Prerequisites |
|---|---|---|---|---|---|
| 0 | Infrastructure Setup | GCP + Anthropic/OpenAI | ~1 day | — | None — start first |
| 0A | Admin AI Model Configuration | Configurable | 1-2 days | 1-2 days | Phase 0 complete |
| 1 | Replace Extraction (Doc AI) | Document AI | 4-5 days | 1-2 days | Phase 0 complete |
| 2 | AI Risk Scoring | Configurable LLM | 3-4 days | 1-2 days | Phase 0A complete |
| 3 | Auto Referral Flags | Configurable LLM + DB | 2-3 days | 1 day | Phase 2 complete |
| 4 | AI Narratives | Configurable LLM | 3-4 days | 2-3 days | Phase 3 complete |
| 5 | Loss Run Upload Extraction | Document AI | 2 days | 2 days | Phase 1 complete |
| 6 | Triage Queue | N/A | 2 days | 3-4 days | Phase 2 data available |
| 7 | Institutional Intelligence | Configurable LLM | 1-2 weeks | 3-5 days | 6+ months Phase 2 data |

Phase 0 is the only hard blocker. Once GCP credentials and at least one LLM provider API key are in place, Phases 0A and 1 can run in parallel. Phase 2 should wait for Phase 0A so risk scoring records are created under the same provider/model registry that Admin will use. Phase 5 can run alongside Phases 3-4 as it reuses the Phase 1 service with no additional infrastructure.

---

## Spine Hardening Alignment

This AI work should be tracked alongside spine hardening, but it should not create a second underwriting workflow beside the SIMS lifecycle. The rule is:

> AI may suggest, prefill, cite, and prioritize. SIMS users and SIMS lifecycle records still decide, approve, bind, decline, cancel, renew, and issue.

### Integration Principles

- **Attach AI output to existing lifecycle records**: extraction results attach to submissions and attachments; risk assessments attach to submissions and quotes; writeup drafts attach to UW writeups; future outcome learning attaches back to finalized lifecycle outcomes.
- **Do not let AI create final business state**: no AI call should directly bind a quote, decline a submission, issue a policy, complete a cancellation, non-renew, reinstate, or rewrite.
- **Prefer deterministic spine data over model judgment**: status, policy transaction, premium, coverage, effective dates, cancellation dates, notice periods, and authority outcomes come from SIMS data and rules first.
- **Record provenance**: store model id, prompt version, source citations, input snapshot hash, assessed timestamp, user acceptance/override, and final user id for every AI-generated assessment or draft that becomes part of the underwriting file.
- **Keep AI advisory until underwriting controls exist**: before the Underwriting Control Layer is complete, AI referral flags should prefill writeup fields only. After that layer exists, AI can create proposed referral records that require underwriter or manager approval.
- **Respect program configuration timing**: program-specific appetite scoring can start with the SMM Underwriter skill, but it should move to Program Configuration once program, authority, appetite, and forms become first-class SIMS data.

### Spine Compatibility Checkpoints

| Checkpoint | Needed Before | Success Criteria |
|---|---|---|
| Submission and quote identity | AI Phases 1-2 | Every AI result links to a stable submission, quote, attachment, or writeup id; no orphaned AI rows |
| Policy transaction spine | AI Phases 3-4 touching bound policy actions | AI does not bypass `PolicyTransaction` or type-specific detail records |
| Underwriting Control Layer | AI referral records beyond writeup prefill | AI proposals can be accepted, rejected, audited, and permissioned |
| Program Configuration | Program-specific scoring and appetite automation | Appetite thresholds live in versioned program data rather than only in prompt text |
| Async job spine | Batch scoring, triage queue, and document reprocessing | Long-running AI calls are retryable, observable, and do not block user workflows |

---

## Phase 0 — Infrastructure Setup

**Pre-conditions:** None
**Parallel to:** SIMS Improvement WP3 (Phase 5 lifecycle workflows)

### Google Cloud / Document AI

| Item | Action |
|---|---|
| GCP Project | Create or designate a GCP project for SIMS; enable Document AI API and Cloud Storage API |
| Processor | Create a processor in the lending/insurance vertical. Start with the general Form Parser; evaluate specialized lending processor for ACORD-heavy workflows |
| Service Account | Create a service account with `documentai.apiUser` role; download the JSON key |
| SIMS Config | Add `DocumentAI:ProjectId`, `DocumentAI:Location`, `DocumentAI:ProcessorId`, `DocumentAI:CredentialsJson` to appsettings / environment |
| NuGet Package | Add `Google.Cloud.DocumentAI.V1` to `SIMS.Infrastructure` |

### Anthropic / Claude API

| Item | Action |
|---|---|
| API Key | Obtain Anthropic API key. Pin `Anthropic:Model` to the current approved Sonnet-class model id for scoring and narratives; review the id during each AI release because Anthropic model ids are versioned and retired over time |
| SIMS Config | Add `Anthropic:ApiKey` and `Anthropic:Model` to appsettings / environment |
| NuGet Package | Add `Anthropic.SDK` to `SIMS.Infrastructure` |
| System Prompt | Extract SMM Underwriter skill content into `SIMS.Infrastructure/AI/SystemPrompts/SmmUnderwriterSystemPrompt.txt`. Add as EmbeddedResource. Update when skill is updated. |

### OpenAI API

| Item | Action |
|---|---|
| API Key | Optional for launch, but recommended for model comparison. Add OpenAI credentials only when SMM wants OpenAI available in Admin. |
| SIMS Config | Add `OpenAI:ApiKey` and default model settings without making OpenAI the production default. |
| Package | Add the OpenAI SDK only when implementing the OpenAI provider adapter. |
| Prompt | Reuse the same SMM underwriting rubric and structured output schema; keep provider-specific prompt versions so Claude and OpenAI can be evaluated separately. |

### SMM Underwriter Skill Enhancement

Before wiring any LLM provider into the backend, enhance the skill with the scoring rubric, referral trigger thresholds, program-specific tolerances, and FMCSA interpretation guidance. The enriched skill becomes the authoritative system prompt embedded in SIMS.

### AI Governance Baseline

Add these controls in Phase 0 so later phases do not need to retrofit auditability:

- `AiModelRegistry` or configuration record for provider, model id, active flag, approved use cases, and retirement date review.
- `AiUseCaseModelSetting` records the active model per use case: extraction, risk scoring, referral judgment, narrative drafting, and batch triage.
- Prompt versioning for the embedded SMM Underwriter prompt, including a simple changelog entry when appetite or referral thresholds change.
- Central AI call logging that stores request purpose, target record id, model id, prompt version, elapsed time, token/cost estimate, success/failure, and correlation id without storing raw sensitive prompt content in normal logs.
- Permission check: only users who can manage underwriting should trigger scoring, narrative generation, referral prefill, or batch triage actions.
- Manual-review rule: low-confidence extraction, high-risk scores, and AI-generated decline/refer recommendations must remain queued for human review.

**Effort:** GCP setup: ~1 day | Anthropic setup: ~2 hours | optional OpenAI setup: ~2 hours | Skill enhancement: ~half day | No migrations needed

---

## Phase 0A — Admin AI Model Configuration

**Pre-conditions:** Phase 0 complete
**Parallel to:** Phase 1 (no shared code)

Add an Admin settings page so SMM can choose which approved provider/model powers each AI use case. This should be a controlled selector, not a free-form model id text box.

### Backend

- Add `AiModelRegistry`: Provider, ModelId, DisplayName, Active, AllowedUseCases, DefaultUseCases, CostNotes, RetirementReviewDate, CreatedAt, UpdatedAt
- Add `AiUseCaseModelSetting`: UseCase, AiModelRegistryId, PromptVersion, UpdatedByUserId, UpdatedAt
- Seed initial approved models with Claude Sonnet as the default for risk scoring, referral judgment, narrative drafting, and batch triage
- Add Admin-only endpoints to list approved models, update active model by use case, and view model-change history
- Log every model setting change with previous value, new value, user id, timestamp, and reason

### Frontend

- Add Admin > AI Settings
- Show one row per use case: Document extraction, Risk scoring, Referral judgment, Narrative drafting, Batch triage
- Use dropdowns populated only by active approved models for that use case
- Show provider, model id, default marker, cost note, prompt version, and retirement review date
- Require a short change reason before saving a new production model

### Guardrails

- Document extraction remains tied to Document AI processors, not a general LLM selector.
- Existing AI results keep their original provider/model/prompt metadata; changing the Admin setting affects future runs only.
- If a selected model is deactivated or reaches its review date, Admin should show a warning but not silently switch production behavior.
- Claude Sonnet remains the recommended default until OpenAI is evaluated against the same SMM underwriting examples.

**Effort:** Backend: ~1-2 days | Frontend: ~1-2 days | Migration: 2 small configuration tables

---

## Phase 1 — Replace Document Extraction with Document AI

**Pre-conditions:** Phase 0 complete
**Parallel to:** Phase 0A, then Phase 2 (no shared code)

Migrate all PDF extraction — inbound email and direct uploads — from `GeminiExtractionService` to Google Document AI. This retires Gemini as the extraction engine and is the foundation every downstream phase depends on.

### Backend

- Create `IDocumentAiExtractionService` with same method signature as `IGeminiExtractionService` so `InboundEmailService` requires minimal change
- Create `DocumentAiExtractionService` — downloads blob, sends to Document AI processor, maps entity/form-field output to existing `GeminiExtractionResult` schema
- Add `SourceCitations` (JSON) — stores page number, confidence, bounding box, processor id, and attachment id per extracted field
- Preserve the raw Document AI response in blob storage or a restricted audit table so extraction mappings can be replayed when processors change
- If Doc AI returns low-confidence results (avg < 0.7), log warning and flag extraction for manual review
- In `InfrastructureServiceExtensions`, replace `IGeminiExtractionService` registration with `IDocumentAiExtractionService`

### Frontend

- Confidence indicator on Inbox extraction preview for fields with confidence below 0.85
- Citation tooltip: hover over an extracted field to see its page and section source

**Effort:** Backend: ~4-5 days | Frontend: ~1-2 days | Migration: 1 optional citation detail table

### Phase 1 Validation Notes

Initial Document AI Form Parser smoke test completed against two local sample PDFs on May 20, 2026:

| Sample | Result |
|---|---|
| ACORD submission packet | Parsed 9 pages, 127 form fields, 9 generic entities |
| Loss run packet | Parsed 2 pages, 14 form fields, 2 generic entities |

Key implementation note: the general Form Parser is strong enough for field capture and confidence scoring, but it does not yet produce underwriting-ready normalized objects. Phase 1 still needs a SIMS mapping layer that converts raw form fields into the existing submission schema, and Phase 5 should add loss-run-specific normalization before writing any loss history rows.

Normalization checkpoint added May 20, 2026: SIMS now has a preview-only mapper from raw Document AI fields into `GeminiExtractionResult` submission data and `SubmissionLossYearCreateDto` loss-year previews. It deliberately does not write submission or loss-history rows; low-confidence source fields remain marked for user review.

Preview endpoint checkpoint added May 20, 2026: SIMS now exposes a guarded submission-attachment AI preview path for PDFs. The endpoint downloads the attachment, runs Document AI, returns the normalized preview, and leaves all submission and loss-history tables unchanged.

---

## Phase 2 — AI Submission Risk Scoring

**Pre-conditions:** Phase 0A complete
**Parallel to:** Phase 1 (no shared code)

Add a configurable LLM-powered 1-5 risk score to every submission using the SMM Underwriter rubric as the system prompt. Claude Sonnet is the default model, but Admin can switch scoring to another approved model after evaluation. Gives underwriters immediate appetite signal without opening the full writeup.

### What It Does

- On-demand or post-save trigger assembles submission context: description of operations, LOBs, loss history summary, FMCSA rating, driver/vehicle/equipment profile, GL classifications, years in business
- Context sent to the selected LLM with the SMM Underwriter prompt version for that provider
- Selected LLM returns: score (1-5), one-paragraph appetite narrative, flagged risk factors with explanation and source data field
- Results stored in new `AiRiskAssessment` table; score badge appears on Submissions list and Submission Detail header

### Backend

| Item | Detail |
|---|---|
| New entity | `AiRiskAssessment`: SubmissionId, QuoteId nullable, Score (1-5), AppetiteNarrative, RiskFlags (JSON), InputSnapshotHash, AssessedAt, ModelVersion, PromptVersion, AcceptedByUserId nullable, OverriddenAt nullable |
| New interface | `IAiRiskScoringService.ScoreSubmissionAsync(Guid submissionId)` |
| New service | `AiRiskScoringService`: assembles context, resolves the active model from Admin settings, calls the selected provider adapter, persists result |
| New endpoints | `POST /api/v1/submissions/{id}/ai-score` + `GET /api/v1/submissions/{id}/ai-score` |

### Frontend

- Color-coded score badge (1=dark green through 5=red) on Submissions list and Submission Detail header
- Expandable risk flags panel with explanation and source data field per flag
- "Score this Submission" button with last-scored timestamp; stale indicator when submission data changes after scoring
- "AI advisory" label and override affordance so the underwriter can record why they disagreed with the score or a flag

**Effort:** Backend: ~3-4 days | Frontend: ~1-2 days | Migration: 1 new `AiRiskAssessment` table

---

## Phase 3 — Auto-Populate UW Writeup Referral Flags

**Pre-conditions:** Phase 2 complete
**Run before:** SIMS WP4 (Underwriting Control Layer)

Pre-check the UW Writeup referral flag checkboxes before the underwriter opens the form. Math-based flags use DB data; judgment-based flags use the selected LLM with SMM Underwriter context.

### Flag Detection Strategy

| Method | Flags | Logic |
|---|---|---|
| **DB only (no AI)** | Loss ratio >55%, piece >$500K, TIV >$2M, loss >$400K/$50K, premium >$100K, owner-operator >30%, schedule credit >20% | Pure arithmetic from loss history, TIV, driver %, and pricing already in DB. Fast, deterministic, no API cost. |
| **Selected LLM** | FMCSA Conditional, BASIC over threshold, sawmill ops, burning exposure, residential work, subcontractor controls | Requires reading FMCSA data, description of operations, and GL classifications — judgment calls that benefit from SMM Underwriter context. |

### Backend / Frontend

- New `AiWriteupPrefillService`: runs math checks from DB, calls the selected LLM for judgment flags, returns `IMWriteupPayload` patch
- Called inside `UWWriteupService.GetOrCreateAsync` when status == Draft and payload is empty
- Store AI-pre-checked flags in new `AiPrefillFlags` JSON column on `UWWriteup`
- Store the source method for each flag (`DbRule` or `Llm`) plus provider/model, source field/citation, and prompt version so deterministic flags remain distinguishable from judgment flags
- Small "AI" chip next to each pre-checked flag; chip disappears once underwriter manually saves their choice

**Effort:** Backend: ~2-3 days | Frontend: ~1 day | Migration: 1 JSON column on `UWWriteup`

---

## Phase 4 — AI UW Narrative Generation

**Pre-conditions:** Phase 3 complete

Pre-populate the UW Writeup narrative fields with selected-LLM-drafted text so the underwriter edits rather than writes from scratch.

### Narrative Fields and Source Data

| Writeup Field | AI Source Data |
|---|---|
| Loss Synopsis | Loss history: frequency, severity, open reserves, large losses, year-over-year trend, loss ratio vs. target |
| Narrative — Operators | Description of operations, DOT type, commodities hauled, supplemental flags, years in business |
| Narrative — Equipment | Equipment schedule: TIV, unit count by type, largest unit, age distribution |
| Narrative — Drivers | Driver count, MVR status, owner-operator %, age span, turnover %, date hired distribution |
| Narrative — CAB/FMCSA | FMCSA safety rating, BASIC scores, inspection count, OOS rate, recent violations |
| Narrative — Add. Interests | Additional interest list, blanket AI requests, certificate holders |
| Loss Control Analysis | Outstanding recommendations, loss mitigation actions, loss trend context |
| Decision Rationale | Phase 2 risk score + key flags + recommended path (Approve / Refer / Decline) |

### Implementation

- New `IAiNarrativeService.GenerateNarrativesAsync(Guid quoteId)` — single selected-LLM call with all source data in one context window
- New endpoint: `POST /api/v1/quotes/{id}/writeup/ai-narratives`
- Drafts stored in `AiWriteupDraft` table — not written to live writeup until underwriter explicitly accepts
- `AiWriteupDraft` stores model id, prompt version, source snapshot hash, generated timestamp, accepted fields, discarded fields, and accepting user
- "Generate AI Draft" button on `QuoteWriteupPage`; per-field accept/discard controls

**Effort:** Backend: ~3-4 days | Frontend: ~2-3 days | Migration: 1 new `AiWriteupDraft` table

---

## Phase 5 — Loss Run PDF Extraction from Direct Uploads

**Pre-conditions:** Phase 1 complete
**Parallel to:** Phases 3-4

Extend Document AI extraction to loss run PDFs uploaded directly to a submission, with an import-preview workflow before any data is written to loss history.

After Phase 1, Document AI handles inbound email attachments. This phase reuses `DocumentAiExtractionService` to also process loss runs uploaded directly to a submission — closing the gap where brokers email separate PDFs or UW uploads manually.

- New endpoint: `POST /api/v1/submissions/{id}/attachments/{attachmentId}/extract-loss-run`
- Calls `DocumentAiExtractionService`, returns structured year/claim data matching `SubmissionLossYearCreateDto`
- "Extract from Document" button on `SubmissionLossHistoryPage` when a loss run attachment is present
- Preview table with extracted years/claims and confidence scores; conflict detection if policy year already exists
- No loss history rows are written until the user accepts the preview; accepted rows keep attachment/citation references for audit support

**Effort:** Backend: ~2 days | Frontend: ~2 days | No migration needed (uses existing loss history tables)

---

## Phase 6 — AI-Sorted Submission Triage Queue

**Pre-conditions:** Phase 2 complete and at least 2 weeks of scoring data available

Replace the date-sorted submissions list with an AI-aware triage view that surfaces the highest-priority submissions first.

- `SubmissionsPage` gains a Triage view toggle alongside the existing list view
- Submissions grouped into three swim lanes: **In Appetite (1-2)**, **Review Required (3)**, **Out of Appetite (4-5)**
- Unscored submissions in a "Not Yet Assessed" section with "Score All" bulk action
- Each card: insured name, LOBs, score badge, effective date, top referral flags
- Bulk scoring endpoint: `POST /api/v1/submissions/ai-score-batch`

**Effort:** Backend: ~2 days | Frontend: ~3-4 days | Builds directly on Phase 2 scoring data

---

## Phase 7 — Institutional Intelligence (Future)

**Pre-conditions:** 6+ months of Phase 2 data. SIMS WP5 (Program Configuration) ideally complete.

After sufficient scoring data, link AI assessments to actual bind/decline outcomes to improve accuracy and surface comparable past submissions.

- **Outcome tracking**: `AiRiskAssessment` records eventual submission, quote, and policy lifecycle outcomes when spine status changes occur
- **Similar submissions panel**: 3-5 historical submissions with similar profiles shown on `SubmissionDetailPage`
- **Score calibration**: compare AI score distribution to actual bind/decline ratio; adjust SMM Underwriter skill scoring rubric accordingly (trigger: bind rate for score 1-2 below 60%, or bind rate for score 4-5 above 20%)
- **Optional**: pgvector extension on PostgreSQL for embedding-based similarity search

**Effort:** Backend: ~1-2 weeks | Frontend: ~3-5 days | Requires 6+ months of Phase 2 data to be meaningful

---

## Alignment with SIMS Improvement Roadmap

| Improvement Roadmap Window | AI Work Running Alongside |
|---|---|
| Now / WP1 | AI Phase 0 governance/setup + AI Phase 0A Admin model settings + AI Phase 1 Doc AI swap, limited to intake records and attachments |
| WP1–WP3 | AI Phase 2 scoring + AI Phase 5 loss run uploads, with advisory-only output tied to submissions/quotes |
| WP2–WP3 | AI Phase 3 referral flags as writeup prefill only; wait for WP4 before creating formal referral/approval records |
| WP3–WP4 | AI Phase 4 narratives as draft-only writeup assistance |
| WP4–WP5 | AI Phase 6 triage queue once scoring is stable and permissioned |
| WP5+ | Move program appetite thresholds from prompt text into Program Configuration; then start AI Phase 7 institutional intelligence |
| WP8 | Migrate batch scoring, large document extraction, and reprocessing to async jobs |

**Current status:** SIMS is at Phase 5 of the improvement roadmap (WP3 — Full Lifecycle Workflows). AI Phases 0, 0A, 1, 2, and 5 can start now in parallel with ongoing WP3 work, with Phase 2 starting after the Admin model registry exists. Phases 3-4 should remain advisory/writeup-only until WP4 formalizes underwriting controls. Phase 6 should wait until scoring is reliable enough that triage ordering will not create operational noise.

---

*Prepared by Claude (Cowork) for Specialty Market Managers, LLC • May 2026 • Internal use only*
