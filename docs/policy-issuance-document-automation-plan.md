# Policy Issuance, Document Library, and Automation Plan

## Purpose

SIMS needs four related but separate document systems:

1. Document Library and Communications
2. Policy Issuance Setup
3. Application Packet Generation
4. Event-Based Document and Email Automation

The systems should share storage, generated documents, attachments, and communication history, but they should not be collapsed into one generic document screen.

## Guiding Decisions

- Pilot policy issuance with Inland Marine first.
- Use lessons from Inland Marine schedules before building Auto.
- GL should follow once the shared framework exists because it should be simpler.
- Keep the existing Document Library for proposals, letters, notices, email templates, and generated documents.
- Build a separate Policy Issuance Setup area for carrier forms, package rules, sequencing, and issuance.
- Add an Application Packet Generation workflow to recreate ACORD forms and carrier applications from SIMS quote/submission data.
- Support both shared mailboxes and current-user mailboxes for outbound communication.
- Proposal sending should be user-triggered automatic: when the underwriter clicks Send Proposal, SIMS generates, sends, files, and logs the proposal.
- Policy issuance should start with Review & Issue, then later allow trusted packages to become more automated.

## Module 1: Document Library and Communications

The existing Document Library should become the home for reusable business templates and generated communication artifacts.

Examples:

- quote proposals
- broker letters
- subjectivity letters
- submission acknowledgment emails
- cancellation and non-renewal notices
- underwriting memos
- email templates
- generated PDFs saved to submissions, quotes, policies, carriers, agents, or insureds

Recommended additions:

- Add template kinds:
  - Document
  - Email
  - DocumentAndEmail
- Add email template support:
  - subject template
  - body HTML
  - default recipient logic
  - default sender mode
- Add outbound communication records:
  - linked entity
  - recipient
  - sender mailbox/user
  - subject
  - body HTML
  - status: Draft, Queued, Sent, Failed, Cancelled
  - sent by
  - sent at
  - attachments
  - related template
  - related system event

## Module 2: Send Proposal Workflow

Start with a clear Send Proposal button.

When an underwriter clicks Send Proposal:

1. Generate the proposal document/PDF.
2. Generate the email from the configured template.
3. Attach the proposal.
4. Send from the configured mailbox or current user.
5. Save the generated proposal to documents.
6. Log the sent email in communication history.
7. Log activity on the quote/submission.

This is not fully background automation. It is underwriter-triggered automation, which gives the UW a clear control point.

## Module 3: Policy Issuance Setup

Policy issuance setup should be separate from the generic Document Library because it has insurance-specific rules:

- carrier
- line of business
- state
- form version
- sequencing
- mandatory/conditional/ad-hoc usage
- trigger logic
- underwriter override rules
- final packet assembly

Recommended entities:

- PolicyFormTemplate
  - carrier
  - LOB
  - state applicability
  - form number
  - form name
  - edition date
  - source file blob path
  - source type: Pdf, Docx, Generated
  - processing mode
  - active/inactive
  - version or replaces-form reference

- PolicyFormFieldMapping
  - form template
  - source field name
  - SIMS data key
  - formatting rule

- PolicyPackageConfiguration
  - carrier
  - LOB
  - state
  - effective date/version
  - active/inactive

- PolicyPackageForm
  - package configuration
  - policy form template
  - sequence order
  - usage: Mandatory, Conditional, AdHoc
  - trigger condition JSON
  - allow underwriter removal
  - notes

## Supported Policy Form Processing Modes

Support mixed packets from the beginning.

### Static PDF

Use when a carrier PDF needs no data merging.

Issuance behavior:

1. Download PDF.
2. Insert at configured sequence.
3. Merge into final packet.

### Fillable PDF

Use when carrier layout must remain exact, especially dec pages and official carrier forms.

Issuance behavior:

1. Download fillable PDF.
2. Fill mapped fields.
3. Flatten the PDF.
4. Merge into final packet.

### DOCX Merge

Use for business documents, letters, notices, and forms where Word layout is safe enough.

Issuance behavior:

1. Download DOCX.
2. Fill mapped tags/fields.
3. Convert to PDF with Syncfusion.
4. Merge into final packet.

### Repeating DOCX Table

Use first for Inland Marine schedules because it matches the old system's repeatable tag approach.

The schedule template has one populated/tagged row. SIMS clones that row for each equipment item.

Issuance behavior:

1. Download DOCX schedule template.
2. Find the repeatable row.
3. Clone the row for every scheduled item.
4. Fill row-level tags.
5. Remove placeholder tags.
6. Convert to PDF.
7. Merge into final packet.

This avoids creating PDFs with large numbers of blank rows.

### Generated PDF Schedule

Use as a fallback or for complex schedules where DOCX repeat rows do not hold formatting well.

Issuance behavior:

1. Generate schedule directly from structured data.
2. Control page breaks, headers, table widths, totals, and continuation pages.
3. Merge into final packet.

## Module 4: Inland Marine Issuance Pilot

Pilot Inland Marine first.

Reasons:

- It has schedules, so it tests the hard part.
- Its schedules should be simpler than Auto.
- The patterns learned will help with Auto.
- GL can reuse the framework later with fewer schedule complications.

Pilot packet should include:

- one static PDF
- one fillable PDF if available
- one DOCX merge form if available
- one repeating DOCX table schedule
- one conditional form

The proof of concept should generate and save one final merged policy packet.

## Module 5: Application Packet Generation

SIMS should be able to recreate ACORD forms and carrier supplemental applications from the exact data used to quote. This gives agents a clean, signature-ready packet and reduces the risk of receiving signed applications that do not match the quote.

This workflow belongs with the Document Library and Communications system, not final Policy Issuance Setup. It is a pre-bind / quote workflow.

Example flow:

1. Submission or quote data is complete enough to quote.
2. Underwriter clicks Send Application Packet or Generate Application Packet.
3. SIMS fills ACORD and carrier application forms from system data.
4. SIMS leaves signature/date fields blank unless a signature workflow later requires otherwise.
5. SIMS merges required applications and schedules into one clean packet.
6. SIMS saves the generated packet to submission/quote documents.
7. SIMS can email the packet to the agent and log the communication.

Recommended processing modes:

- Fillable PDF Application
  - Best for ACORD forms and rigid carrier application layouts.
  - Fill mapped PDF fields from SIMS data.
  - Leave signature/date fields editable or blank.
  - Flatten only the completed non-signature fields if needed.

- Repeating DOCX Table Schedule
  - Useful for Inland Marine equipment schedules or simpler attached schedules.

- Generated PDF Schedule
  - Use for schedules that need controlled pagination, continuation headers, totals, or complex formatting.

Recommended setup records:

- ApplicationFormTemplate
  - carrier, if carrier-specific
  - LOB
  - state applicability
  - form name
  - form number
  - edition date
  - source file blob path
  - processing mode
  - active/inactive

- ApplicationFormFieldMapping
  - application form template
  - PDF field name or DOCX tag
  - SIMS data key
  - formatting rule

- ApplicationPacketConfiguration
  - carrier, if carrier-specific
  - LOB
  - state
  - effective version
  - active/inactive

- ApplicationPacketForm
  - packet configuration
  - application form template
  - sequence order
  - mandatory/conditional/ad-hoc usage
  - trigger condition JSON

Initial Inland Marine application packet should include:

- ACORD or common application form, if applicable
- carrier supplemental application
- Inland Marine equipment schedule
- any required state/carrier disclosure forms

Future E&O control:

After signed applications are returned, SIMS can later compare the signed/returned packet against quote data and flag mismatches:

- effective date changed
- insured name or DBA changed
- limits/deductibles changed
- equipment missing
- scheduled values changed
- coverage selections changed
- required signatures/initials missing

## Module 6: Review & Issue Workflow

Initial policy issuance should require review.

Workflow:

1. Bound policy reaches issuance step.
2. SIMS finds matching package by carrier, LOB, state, and effective version.
3. SIMS pulls mandatory forms.
4. SIMS evaluates conditional forms.
5. SIMS creates a draft packet.
6. Underwriter reviews included forms and why they were included.
7. Underwriter can add ad-hoc forms.
8. Underwriter can remove/reorder only where allowed.
9. SIMS validates missing mappings, unresolved tags, and required data.
10. Underwriter previews the final PDF.
11. Underwriter clicks Issue Policy.
12. SIMS saves the final packet to policy documents and updates policy status.

Later, trusted package configurations can move toward one-click or automatic issuance.

## Module 7: Event-Based Automation

Automation should sit above the Document Library and Communications system.

Initial events:

- SubmissionCreated
- QuoteProposalReady
- QuoteBound
- PolicyIssued
- CancellationStarted
- CancellationCompleted

Automation send modes:

- AutoSend: generate and send immediately.
- DraftForReview: generate draft and wait for approval.
- GenerateOnly: create and file document only.

Examples:

- Submission acknowledgment: AutoSend.
- Quote proposal: user-triggered Send Proposal.
- Policy issued email: DraftForReview initially.
- Cancellation notice: DraftForReview.
- Internal underwriting memo: GenerateOnly.

Safety fallback:

Even an AutoSend rule should become DraftForReview when:

- recipient email is missing
- unresolved merge tags exist
- required attachment failed
- packet generation failed
- the communication is legally sensitive
- account size/premium exceeds threshold

## Sender Strategy

Outbound communication must support both:

- shared mailboxes
- current user mailbox

This should be configurable by automation rule, template, or action. It should not be hardcoded globally.

## Implementation Order

1. Upgrade Document Library for document/email template kinds.
2. Add outbound communication draft/history records.
3. Build Send Proposal workflow.
4. Add Application Packet Generation for Inland Marine application forms and schedules.
5. Add Policy Form Library.
6. Add Policy Package Configuration and Package Builder.
7. Build Syncfusion proof of concept for mixed packet assembly.
8. Build Inland Marine repeating DOCX table schedule support.
9. Build Review & Issue workflow.
10. Add automation rules and event trigger handling.
11. Expand to GL.
12. Expand to Auto.

## Open Inputs Needed

- First Inland Marine carrier/package to pilot.
- Sample static PDF.
- Sample fillable PDF.
- Sample DOCX form.
- Sample Inland Marine schedule.
- Sample proposal.
- Sample ACORD/application forms.
- First shared mailbox to support.
- Confirmation of whether Send Proposal should allow an optional preview/edit step or send immediately after click.
