# AI Guideline Control Handoff

This is the contract for the AI agent that reads underwriting guidelines and submits proposed SIMS controls.

## Boundary

The AI agent should only create proposed controls. SIMS handles human review, publish, enforcement, permissions, and audit.

Published controls are live immediately. Do not publish from the AI agent.

## Scope Model

Guidelines are scoped by:

- Program id when available
- Program name
- Company/carrier id, or all companies
- Line of business
- State code, or `ALL`

Most items should use `ALL` for state. Use a specific state only when the guideline requirement is state-specific.

When SIMS has a matching Program Configuration, send `programId`. SIMS will use the configured program name, company, line, and state from that program. Use the free-text scope fields only when no program exists yet.

## Step 1: Create Guideline Document

Endpoint:

```http
POST /api/v1/admin/underwriting-guidelines/documents
```

Payload:

```json
{
  "programId": "00000000-0000-0000-0000-000000000000",
  "programName": "Longleaf",
  "carrierId": "00000000-0000-0000-0000-000000000000",
  "lineOfBusiness": "InlandMarine",
  "stateCode": "ALL",
  "title": "Longleaf Inland Marine UW Guidelines",
  "sourceFileName": "longleaf-im-guidelines.pdf",
  "sourceBlobName": "optional/blob/path.pdf",
  "notes": "Imported by AI for human review"
}
```

Use `carrierId: null` when the guideline applies to all companies.
Use `programId: null` when the guideline is still being loaded before the program has been configured.

AI helper endpoint:

```http
POST /api/v1/admin/ai-guideline-control-proposals/from-text
```

Payload:

```json
{
  "document": {
    "programId": "00000000-0000-0000-0000-000000000000",
    "programName": "Longleaf",
    "carrierId": null,
    "lineOfBusiness": "InlandMarine",
    "stateCode": "ALL",
    "title": "Longleaf Inland Marine UW Guidelines",
    "sourceFileName": "longleaf-im-guidelines.pdf",
    "sourceBlobName": "optional/blob/path.pdf",
    "notes": "Imported by AI for human review"
  },
  "guidelineText": "Extracted guideline text..."
}
```

This helper creates the guideline document and submits proposed controls in one call. It still only creates `AiSuggested` controls; it does not approve, publish, retire, or enforce anything.

AI attachment helper endpoint:

```http
POST /api/v1/admin/ai-guideline-control-proposals/from-attachment
```

Payload:

```json
{
  "attachmentId": "00000000-0000-0000-0000-000000000000",
  "document": {
    "programId": "00000000-0000-0000-0000-000000000000",
    "programName": "Longleaf",
    "carrierId": null,
    "lineOfBusiness": "InlandMarine",
    "stateCode": "ALL",
    "title": "Longleaf Inland Marine UW Guidelines",
    "sourceFileName": null,
    "sourceBlobName": null,
    "notes": "Imported by AI for human review"
  }
}
```

This endpoint reads an uploaded `UnderwritingGuidelines` attachment, extracts text from supported files, fills missing source file/blob values from the attachment, then creates the same `AiSuggested` controls as the text helper. PDF files use the configured Document AI processor. Plain-text files are decoded directly. Other file types return `UNSUPPORTED_GUIDELINE_ATTACHMENT`.

## Step 2: Submit Proposed Controls

Endpoint:

```http
POST /api/v1/admin/underwriting-guidelines/documents/{documentId}/proposed-controls
```

Payload:

```json
{
  "controls": [
    {
      "itemType": "DocumentChecklistItem",
      "stage": "Submission",
      "severity": "Warning",
      "ruleKey": "loss-runs-required",
      "label": "Five years currently valued loss runs",
      "description": "Guideline requests currently valued loss runs before underwriting review.",
      "conditionJson": null,
      "isBlocking": false,
      "overrideAllowed": true,
      "overridePermission": "underwriting.clearance.override",
      "sourceCitation": "Page 4, Loss History Requirements",
      "aiConfidence": 0.86,
      "sortOrder": 10
    }
  ]
}
```

## Allowed Values

`itemType`:

- `AppetiteRule`
- `ReferralTrigger`
- `AuthorityLimit`
- `DocumentChecklistItem`
- `AppetiteNote`

`stage`:

- `Submission`
- `Quote`
- `Bind`
- `Issue`
- `PostBind`
- `Renewal`

`severity`:

- `Informational`
- `Warning`
- `ReferralRequired`
- `HardBlock`

## Document Checklist Guidance

UW guidelines often include more documents than the team actually requires in practice. For document checklist items, default to:

- `isBlocking: false`
- `severity: "Warning"`
- `stage: "Submission"` or `stage: "Bind"` only when the guideline clearly says it is required before bind

Only propose `isBlocking: true` when the guideline clearly says the document is required before the selected stage.

Quote checklist generation currently uses published `DocumentChecklistItem` controls for stages:

- `Submission`
- `Quote`
- `Bind`

`Issue`, `PostBind`, and `Renewal` items are stored for review/publish now, but they will not appear in the bind checklist until their dedicated enforcement surfaces are added.

## Condition JSON Guidance

Use `conditionJson` when a proposed control only applies if measurable quote, submission, or policy facts meet a threshold. This is how the deterministic enforcement layer answers whether the rule is triggered.

Default to `conditionJson: null` when the control always applies within the document scope.

For simple threshold rules, use this exact shape:

```json
{
  "field": "largestSingleItemValue",
  "operator": ">",
  "value": 500000
}
```

Example for a conditional referral trigger:

```json
{
  "itemType": "ReferralTrigger",
  "stage": "Quote",
  "severity": "ReferralRequired",
  "ruleKey": "single-piece-over-500k",
  "label": "Single piece over $500K",
  "description": "Guideline requires referral review when a single piece exceeds the threshold.",
  "conditionJson": "{\"field\":\"largestSingleItemValue\",\"operator\":\">\",\"value\":500000}",
  "isBlocking": false,
  "overrideAllowed": true,
  "overridePermission": "underwriting.clearance.override",
  "sourceCitation": "Page 3, Referral Requirements",
  "aiConfidence": 0.84,
  "sortOrder": 20
}
```

Allowed `operator` values:

- `>`
- `>=`
- `<`
- `<=`
- `==`
- `!=`

Current enforceable `field` values:

- `largestSingleItemValue`
- `totalInsuredValue`
- `premiumAmount`
- `totalPremium`
- `lossRatio`
- `driverCount`
- `vehicleCount`
- `isFilingState` (`1` for true, `0` for false)

For unconditional blockers, use `conditionJson: null`.

For conditional blockers or referrals, use only the documented field/operator/value schema above. If the field needed by the guideline is not listed, do not invent one. Flag the missing measurable field in `description` or `sourceCitation` so a human can decide whether SIMS needs a new measurable field before publishing.

## Rule Key Guidance

Use stable kebab-case keys:

- `five-year-loss-runs`
- `signed-application`
- `driver-schedule`
- `coastal-property-photos`

Do not include document ids, page numbers, or dates in the `ruleKey`.

## Human Review

After AI submission, a human admin reviews in:

```text
Admin > UW Controls
```

The human can edit, approve, reject, publish, or retire controls. Published blocking controls become live immediately, with override support when `overrideAllowed` is true.
