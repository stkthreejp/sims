# UW Writeup — IM Reference Implementation

Goal: replace the Word-doc UW Review Sheet with a structured, prefilled, decision-driving form that lives on each quote. IM is the reference; AL, APD, GL follow the same shape with their own per-LOB section.

## Field map (from existing IM Review Sheet)

| Field | Source | Type |
|---|---|---|
| Underwriter | current user | auto |
| Date | now() | auto |
| Assistant UW | user picker | manual |
| Agent | submission.agent | auto |
| Named Insured | insured.name | auto |
| LOB | quote.lob (= IM) | auto |
| Policy Type (New/Ren) | computed: prior bound policy on insured | auto, override |
| Effective Date | quote.effective_date | auto |
| Operation Type | insured.operation_type *(new field)* | auto, override |
| New Venture? | computed: insured.years_in_business < 1 | auto, override |
| Physical Address | insured.address (multi-location list) | auto |
| Prior Carrier | submission.prior_carrier *(new field)* | auto |
| Reason Submitted | submission.intake_notes | auto |
| **Referral triggers** (checkboxes — auto-checked) | | |
| Loss Ratio > 55% | computed from loss runs | auto |
| Pieces > $500K (TIV > $2M) | computed from equipment list | auto |
| Loss > $400K | computed from loss runs | auto |
| Other | free text | manual |
| **Losses** | | |
| # of claims (5 yr) | loss run aggregate | auto |
| Total incurred (5 yr) | loss run aggregate | auto |
| Mitigation actions | narrative | manual |
| Losses > $25K — describe | narrative (per loss row) | manual |
| **Equipment & Values** (all from equipment list) | | |
| Largest unit TIV | max(equipment.value) | auto |
| Total TIV | sum(equipment.value) | auto |
| EQ Value Checked | boolean | manual |
| # Cutter / Skidder / Loader / Dozer / Other | count by equipment_type | auto |
| **Operations & Metrics** | | |
| Years in business | insured.years_in_business *(new field)* | auto |
| New Venture addl docs OK? | Yes/No (shows only if New Venture) | manual |
| Credit Score | insured.credit_score *(new field)* | manual |
| Waterborne Exposure | Yes/No | manual |
| Last Inspection Date | quote.last_inspection_date *(new field)* | manual |
| Recommendations Outstanding | Yes/No + explain | manual |
| Website Found/Reviewed | insured.website + reviewed bool | mixed |
| Any Issues | narrative | manual |
| **Requested Terms / Pricing** | | |
| Any One Item Limit | quote.coverage.any_one_item | auto |
| Any One Loss Limit | quote.coverage.any_one_loss | auto |
| Deductible | quote.deductible | auto |
| **Narrative (guided prompts)** | | |
| Operators | prompt: employees, yrs experience, training/certs, concerns | manual |
| Equipment | prompt: age, maintenance, cool-down, mortgage +75%, deductible, usage | manual |
| Fire Suppression | prompt: type, maintenance schedule | manual |
| Other risk concerns | open | manual |
| **Recommendation** | | |
| Decision | Approve / Approve w/ conditions / Refer up / Decline | manual |
| Rationale | narrative | manual |
| Conditions | checklist + custom adds | manual |
| **Sign-off** | | |
| UW signature | digital — user + timestamp on submit | auto |
| Approver | second-line user (only if escalated) | auto |

## New fields needed on existing entities

- `Insured`: `OperationType`, `YearsInBusiness`, `CreditScore`, `Website`
- `Submission`: `PriorCarrier`, `IntakeNotes`
- `Quote`: `LastInspectionDate`

(All nullable; existing rows stay valid.)

## Data model

```
QuoteUWWriteup
  Id
  QuoteId (FK, unique — one writeup per quote)
  Status: Draft | Submitted | Approved | Declined
  Decision: Approve | ApproveWithConditions | ReferUp | Decline | null
  PayloadJson (jsonb — structured form data, schema versioned)
  SchemaVersion (int)
  SubmittedAt, SubmittedById
  ApprovedAt,  ApprovedById
  CreatedAt, UpdatedAt

QuoteUWWriteupCondition
  Id
  WriteupId (FK)
  Text
  Required (bool)
  Satisfied (bool, set at bind)
```

`PayloadJson` keeps narrative + manual fields; auto fields are recomputed on render so they always reflect current data. Submitted writeups snapshot the auto fields into the JSON to lock the audit record.

## UI / wireframe

Single page route: `/quotes/:id/writeup` (also embedded as a tab inside the quote workspace once that exists).

```
┌──────────────────────────────────────────────────────────────────┐
│ ← Back to quote     QTE-2026-0001 · IM · Precision Timber LLC    │
│ Status: Draft  ·  Eff 4/6/2026  ·  Carrier: Napco                │
├──────────────────────────────────────────────────────────────────┤
│ ► Header & Insured           [auto-filled, expand to override]   │
│ ► Referral Triggers          ✓ Loss Ratio  ✓ Piece > $500K       │
│ ► Losses                                                         │
│ ► Equipment & Values         [4 items · TIV $1.8M]               │
│ ► Operations & Metrics                                           │
│ ► Requested Terms                                                │
│ ▼ Narrative (where the UW actually types)                        │
│   Operators ▢                                                    │
│     prompt: "Insured employees, yrs experience, training…"       │
│     [textarea]                                                   │
│   Equipment ▢                                                    │
│   Fire Suppression ▢                                             │
│   Other concerns ▢                                               │
│ ► Recommendation             [decision + rationale + conditions] │
├──────────────────────────────────────────────────────────────────┤
│  [Save Draft]  [Preview PDF]  [Submit for review / Approve]      │
└──────────────────────────────────────────────────────────────────┘
```

Right rail (collapsible) shows the data-source previews so the UW knows where each auto field came from and can jump to fix bad data without leaving the page:

```
Equipment list (4)
  CAT 559C Loader · $480K
  John Deere 748L Skidder · $385K
  ...
  → Edit on quote
Loss runs (3 claims, $48K)
  → View loss runs doc
Prior writeup (renewal)
  → Open last year's
```

## Renewal mode

If quote.is_renewal: load last bound policy's writeup and render a diff view — old value left, new value right, changed fields highlighted. UW can confirm-as-is or edit.

## PDF/Word export

Server-side render (Syncfusion or QuestPDF) using a template that mirrors the existing Review Sheet layout. Triggered from "Preview PDF" and on Submit. Goes to the quote's docs tagged `LOB=IM, Type=UWWriteup`.

## Carrier-specific overlays (later)

Each carrier may have extra questions (e.g., Napco asks about cool-down procedure specifics). Implement as a per-carrier `additional_fields` JSON appended to the writeup form when that carrier is selected on the quote. Out of scope for v1.

## Build order

1. **Backend**
   - Migrations: new fields on Insured/Submission/Quote, new tables QuoteUWWriteup + QuoteUWWriteupCondition
   - DTO + service: get/save draft, submit, compute auto fields (referral triggers, loss aggregates, equipment counts/TIVs)
   - Controller: GET/PUT `/quotes/{id}/writeup`, POST `/quotes/{id}/writeup/submit`, GET `/quotes/{id}/writeup/pdf`
2. **Frontend**
   - `/quotes/:id/writeup` page with the section layout above
   - Guided-prompt narrative blocks (component reusable across LOBs)
   - Right-rail data source previews
   - Decision + conditions block
   - PDF export button
3. **Audit polish**
   - Submitted writeups snapshot auto fields into PayloadJson
   - Read-only view for approved writeups
   - Renewal diff view

## Sequencing vs. quote workspace work

This writeup feature plugs into the future `/quotes/:id` workspace page (it'll be a tab). To unblock writeup work without waiting for the workspace refactor, ship `/quotes/:id/writeup` as a standalone route first, then re-host it as a tab when the workspace lands. No throwaway code — same component, different shell.

## Quote workspace work (next, after this lands)

Tracked separately — covers:
- LOB checkboxes on submission, conditional risk-data sections
- Move drivers/vehicles/equipment to submission scope (one set, shared by all carrier quotes)
- Standalone `/quotes/:id` workspace page
- Doc model with (scope, lob) tagging; quote workspace shows quote docs + submission docs matching this LOB + 'all'
- Per-carrier writeup re-uses this same writeup module, scoped to its quote
