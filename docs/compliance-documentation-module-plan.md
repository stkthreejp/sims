# Compliance Documentation Module Plan

## Goal

Create a SIMS module for maintaining internal compliance documents such as IT Data Policy, Business Continuity Plan, Disaster Recovery Plan, Incident Response Plan, vendor policies, and security procedures.

The module should let SMM create, edit, review, approve, search, compare, and attest to compliance documents inside SIMS.

## Core Features

### Compliance Dashboard

- Total active documents
- Drafts waiting for approval
- Reviews due soon
- Overdue reviews
- Active attestation campaigns
- Pending and overdue attestations
- Recently changed documents

### Document Register

Each document should track:

- Title
- Category, such as IT, Security, Business Continuity, Privacy, HR, Finance, Operations, or Vendor Management
- Type, such as Policy, Plan, Procedure, Standard, Checklist, or Evidence
- Owner
- Approver
- Status: Draft, Active, Under Review, Needs Update, Retired
- Effective date
- Last reviewed date
- Next review date
- Review cadence
- Tags
- Related evidence
- Current published version
- Current draft version

### In-App Document Editing

- Rich-text editor using the existing SIMS editor stack
- Headings, sections, tables, lists, and checklists
- Save draft
- Submit for review
- Approve and publish
- Retire previous versions without deleting them
- Optional Word import later using existing Mammoth/document tooling

### Version History

- Preserve every published version
- Store draft and approved versions separately
- Track version number, author, approver, published date, effective date, and notes
- Allow viewing any historical version

### Version Comparison

- Compare draft against the current approved version
- Compare any two historical versions
- Highlight added, removed, and changed text
- Show reviewers exactly what changed before approval

### Review Workflow

- Review queue for documents due soon or overdue
- Owner marks document reviewed
- Reviewer can require changes
- Approver publishes the version
- Next review date calculated from cadence
- Review comments saved to audit history

### E-Attestation

- Launch attestation campaigns for a specific document version
- Assign campaigns to users, roles, or groups
- Users open the exact document version and attest electronically
- Track accepted, declined, pending, and overdue attestations
- Store timestamp, user, version, statement, and optional comment
- Export attestation evidence reports

### Search and Indexing

- Full-text search across document title, category, type, tags, owner, approver, document body, review notes, and evidence descriptions
- Filters for status, category, owner, due date, and attestation state
- Use PostgreSQL full-text search initially to avoid adding a separate search service too early

### Evidence Tracking

Link supporting evidence to documents or reviews. Examples include:

- Backup test result
- Business continuity exercise
- Incident response tabletop
- Security training record
- Vendor review

Evidence can be notes, links, or file attachments.

### Audit Trail

Record all meaningful compliance actions:

- Document created
- Draft edited
- Submitted for review
- Approved and published
- Retired
- Review completed
- Attestation launched
- Attestation completed or declined
- Evidence added

Each audit entry should keep who, what, when, old value, new value, and comments where relevant.

## Suggested Pages

- `/compliance-documentation` - dashboard and document register
- `/compliance-documentation/:id` - document detail, editor, metadata, versions, reviews, evidence, and attestations
- `/compliance-documentation/reviews` - review queue
- `/compliance-documentation/attestations` - attestation campaigns and completion tracking
- `/compliance-documentation/search` - search/results view, if not built directly into the register

## Backend Shape

- `ComplianceDocument`
- `ComplianceDocumentVersion`
- `ComplianceDocumentReview`
- `ComplianceEvidence`
- `ComplianceAttestationCampaign`
- `ComplianceAttestationRecipient`
- `ComplianceAuditLog`

## Starter Seed Documents

- IT Data Security Policy
- Business Continuity Plan
- Disaster Recovery Plan
- Incident Response Plan
- Access Control Policy
- Acceptable Use Policy
- Vendor Management Policy
- Data Retention Policy
- Privacy Policy
- Change Management Procedure
- Backup and Recovery Procedure
- Security Awareness Training Procedure

## Implementation Order

1. Data model, migrations, and seed records.
2. API for document register, detail, create/update, and version save.
3. Frontend register and document detail/editor.
4. Review and approval workflow.
5. Version comparison.
6. Search and indexing.
7. Attestation campaigns.
8. Evidence tracking and exportable reports.
9. Dashboard metrics and reminder polish.

## Success Criteria

- SMM can maintain compliance documents directly in SIMS.
- Every document has an owner, status, cadence, and next review date.
- Drafts can be reviewed, compared, and approved.
- Old versions are preserved.
- Documents are searchable by metadata and body text.
- Staff can electronically attest to a specific document version.
- SIMS can produce evidence of reviews, approvals, changes, and attestations.
