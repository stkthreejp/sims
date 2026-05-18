# Phase 5: Full Lifecycle Workflows Plan

## Goal

Complete the SIMS policy lifecycle workflow layer so every major policy action is represented by a policy transaction, type-specific detail record, policy version impact, document/compliance artifacts, and visible UI state.

## Scope

Phase 5 includes:

- New business hardening.
- Endorsement workflow hardening.
- Notice-driven cancellation workflow.
- Reinstatement workflow.
- Renewal workflow completion.
- Non-renewal workflow hardening.
- Rewrite skeleton.
- Audit skeleton.
- Type-specific policy transaction detail tables.
- Cancellation notice template wiring.
- Focused DOCX table import fix for notice templates.

Phase 5 excludes:

- Full legal tracker automation.
- Program configuration.
- Bordereaux.
- Claims.
- Broker self-service.
- Generic workflow redesign.

## Core Decisions

1. `PolicyTransaction` remains the shared lifecycle header.
2. Type-specific data must move into detail tables.
3. Existing legacy fields on `PolicyTransaction` may remain during Phase 5 for compatibility, but new workflow data should be written to detail tables.
4. Cancellation notices are manual/legal-review assisted for now. The user selects a reason, notice days, mailing days, and template.
5. Mailing days are added on top of notice requirement days.
6. The calculated cancellation date is:

   `notice mailing date + notice requirement days + mailing days`

7. Cancellation should remain pending until the cancellation effective date is reached and completed.
8. Cancellation notice generation uses the existing Document Library template system first, with `CancellationNonRenewal` as the temporary document type until a dedicated `CancellationNotice` type is added.

## Workstream 5A: Shared Transaction Detail Tables

Create type-specific detail tables:

- `PolicyEndorsementDetail`
- `PolicyCancellationDetail`
- `PolicyReinstatementDetail`
- `PolicyRenewalDetail`
- `PolicyNonRenewalDetail`
- `PolicyRewriteDetail`
- `PolicyAuditDetail`

Each detail table should have:

- `PolicyTransactionId`
- one-to-one relationship with `PolicyTransaction`
- type-specific workflow fields
- created/updated timestamps through existing entity base conventions

Acceptance:

- EF model includes all detail tables.
- Migration creates all detail tables.
- Policy transaction artifact response can expose relevant detail data.
- Existing transactions remain readable.

## Workstream 5B: Cancellation Reason Library

Add a cancellation reason library based on the supplied reason code document.

Reason groups:

- `NP` Non-payment
- `UW` New policy / underwriting period
- `FR` Fraud and material misrepresentation
- `IH` Substantial increase in hazard
- `PC` Physical or property changes
- `LR` Legal, regulatory, and license-related
- `AR` Arson and intentional loss risk
- `RE` Reinsurance / insurer solvency

Each reason needs:

- code
- label
- category
- default notice requirement days
- reason language template
- required user-input tokens parsed from bracketed placeholders
- special handling flag where applicable

Acceptance:

- UI can present grouped reasons.
- UI can render required fields for bracketed placeholders.
- Backend rejects issuing a notice when required reason fields are missing.
- Backend stores original reason language, variables, and resolved reason language.

## Workstream 5C: Cancellation Notice UI

Replace the immediate cancellation modal with an issue-notice flow.

UI fields:

- reason code dropdown
- reason detail fields generated from bracketed placeholders
- notice mailing date
- notice requirement days
- mailing days
- calculated cancellation effective date
- notice method
- notice template
- notes

UI preview:

- reason code
- reason label
- resolved reason language
- notice mailing date
- notice requirement days
- mailing days
- cancellation effective date
- selected notice template

Acceptance:

- User can see exactly what reason and cancellation date will print on the notice before issuing.
- Cancellation effective date updates when notice date, notice days, or mailing days change.
- Issue button is blocked until required reason fields and template are present.

## Workstream 5D: Cancellation Notice Backend

Create an issue-cancellation-notice endpoint.

Backend behavior:

- creates a `Cancellation` policy transaction
- creates `PolicyCancellationDetail`
- stores notice date, notice days, mailing days, calculated cancellation date
- stores reason code, label, language template, variables, and resolved language
- stores selected template id
- generates notice document from template
- attaches generated notice to the transaction
- sets transaction status to `NoticeSent` or `PendingEffectiveDate`
- leaves policy not finally cancelled

Completion behavior:

- a manual complete endpoint, and later job-ready service method, completes eligible cancellations
- sets policy status to `Cancelled`
- creates cancelled policy version
- records completion history
- adds return premium accounting hook

Acceptance:

- Issuing notice does not immediately cancel the policy.
- Generated notice is visible under the transaction artifacts.
- Final cancellation cannot complete before effective date.
- Final cancellation creates a policy version.

## Workstream 5E: Template Wiring and DOCX Table Import

Template wiring:

- use active Policy/Document templates of type `CancellationNonRenewal` initially
- later migrate to `CancellationNotice`
- pass cancellation merge data into the document generator

Template tags:

- `{{insured.name}}`
- `{{insured.mailingAddress}}`
- `{{producer.name}}`
- `{{carrier.name}}`
- `{{policy.policyNumber}}`
- `{{policy.effectiveDate}}`
- `{{policy.expirationDate}}`
- `{{policy.lineOfBusiness}}`
- `{{cancellation.noticeMailingDate}}`
- `{{cancellation.noticeRequirementDays}}`
- `{{cancellation.mailingDays}}`
- `{{cancellation.cancellationEffectiveDate}}`
- `{{cancellation.reasonCode}}`
- `{{cancellation.reasonLabel}}`
- `{{cancellation.reasonLanguageResolved}}`
- `{{cancellation.returnPremiumAmount}}`
- `{{cancellation.returnPremiumMethod}}`
- `{{legal.citations}}`
- `{{legal.requirementSummary}}`
- `{{company.name}}`
- `{{company.phone}}`
- `{{company.email}}`

DOCX import fix:

- preserve Word tables as usable HTML tables
- normalize imported table borders and cell padding
- add editor styles for TipTap table rendering

Acceptance:

- Sample cancellation notice template can be selected.
- Generated notice resolves cancellation fields.
- Imported DOCX tables remain readable in the template editor.

## Workstream 5F: Endorsement Hardening

Improve endorsement workflow:

- create `PolicyEndorsementDetail`
- store premium-bearing flag
- store changed coverage/exposure summary
- ensure invoice failures do not leave inconsistent issued endorsements
- attach endorsement document packet when available

Acceptance:

- Endorsement has before/after policy version references.
- Premium delta and invoice are traceable.
- Detail record carries endorsement-specific data.

## Workstream 5G: Renewal Completion

Complete renewal traceability:

- create `PolicyRenewalDetail`
- link prior policy, renewal quote, renewal transaction, and renewed/new policy
- when renewal binds, mark prior policy `Renewed`
- preserve prior policy version and renewal term data

Acceptance:

- Renewal workflow traces from expiring policy to renewal quote to renewed policy.
- Prior policy status updates only when renewal binds.

## Workstream 5H: Non-Renewal Hardening

Move non-renewal toward notice workflow:

- create `PolicyNonRenewalDetail`
- store reason, notice dates, proof-of-notice data, and legal snapshot
- avoid hidden immediate status changes where notice is required

Acceptance:

- Non-renewal has transaction detail and notice artifacts.
- Notice and compliance history are preserved.

## Workstream 5I: Reinstatement

Add reinstatement workflow:

- create `PolicyReinstatementDetail`
- allow reinstatement from cancelled policy
- capture reason, payment requirement, approval requirement, and restored status
- create reinstated policy version

Acceptance:

- Cancelled policies can be reinstated through a transaction.
- Reinstatement does not erase cancellation history.

## Workstream 5J: Rewrite Skeleton

Add rewrite skeleton:

- create `PolicyRewriteDetail`
- link original policy and replacement quote/policy
- capture rewrite reason
- preserve accounting traceability

Acceptance:

- Rewrite is tracked as a first-class transaction.
- Original and replacement policy/quote links are visible.

## Workstream 5K: Audit Skeleton

Add audit transaction skeleton:

- create `PolicyAuditDetail`
- capture audit basis
- capture audited exposure summary
- capture additional or return premium
- create audit invoice or credit hook

Acceptance:

- Audits are not hidden as endorsements.
- Audit transaction has its own detail record and financial trace.

## Execution Order

1. Detail tables and DTO shape.
2. Cancellation reason library.
3. Cancellation notice UI and calculation.
4. Cancellation notice backend and template generation.
5. DOCX table import fix.
6. Cancellation completion.
7. Endorsement hardening.
8. Renewal completion.
9. Non-renewal hardening.
10. Reinstatement.
11. Rewrite skeleton.
12. Audit skeleton.

## Verification

Backend:

- targeted application tests for each workflow
- lifecycle transition tests
- migration build
- `dotnet build`

Frontend:

- TypeScript check
- UI smoke test for policy cancellation notice modal
- template editor DOCX table import check

