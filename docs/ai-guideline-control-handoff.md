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

When SIMS has a matching Program Configuration, send `programId`. SIMS will use the configured program name from that record, while company, line, and state still come from the document scope fields. Use `programId: null` only when no program exists yet.

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
- `contains` (only for supported text/list fields such as `glClassCodes`)
- `notContains` (only for supported text/list fields such as `glClassCodes`)

Current enforceable `field` values:

| Field | Admin label | Meaning | Value format | Good matches |
| --- | --- | --- | --- | --- |
| `largestSingleItemValue` | Largest single item value | Highest scheduled equipment/item value on the submission. | Number in dollars, no commas. | Single item over $500K; any one piece exceeds threshold. |
| `totalInsuredValue` | Total insured value | Sum of scheduled equipment/item values on the submission. | Number in dollars, no commas. | TIV over $2M; total scheduled value exceeds threshold. |
| `premiumAmount` | Premium amount | Base premium for the quote or policy being evaluated. | Number in dollars, no commas. | Premium threshold based on base premium. |
| `totalPremium` | Total premium | Total quote or policy premium including applicable fees/taxes where SIMS stores total premium. | Number in dollars, no commas. | Total premium exceeds authority threshold. |
| `lossRatio` | Loss ratio | Paid plus reserved losses divided by loss history premium. SIMS stores this as a decimal. | Decimal, not percent. Use `0.55` for 55%. | Loss ratio over 50%; unacceptable loss experience ratio. |
| `driverCount` | Driver count | Count of active/non-deleted drivers on the submission. | Whole number. | More than 10 drivers; fleet driver threshold. |
| `vehicleCount` | Vehicle count | Count of active/non-deleted vehicles on the submission. | Whole number. | More than 20 vehicles; fleet size threshold. |
| `isFilingState` | Filing state | Whether the quote/policy is in a filing state. | `1` for yes, `0` for no. | Filing state referrals or restrictions. |
| `glGeneralAggregate` | GL general aggregate | General liability general aggregate limit captured on the submission GL coverages. | Number in dollars, no commas. | GL aggregate over $2M; max general aggregate limit. |
| `glProductsCompletedOps` | GL products/completed ops aggregate | Products/completed operations aggregate limit captured on the submission GL coverages. | Number in dollars, no commas. | Products/completed ops limit over threshold. |
| `glEachOccurrence` | GL each occurrence limit | Each occurrence limit captured on the submission GL coverages. | Number in dollars, no commas. | Occurrence limit exceeds authority. |
| `glPersonalAndAdvertisingInjury` | GL personal & advertising injury | Personal and advertising injury limit captured on the submission GL coverages. | Number in dollars, no commas. | Personal and advertising injury limit threshold. |
| `glDamageToRentedPremises` | GL damage to rented premises | Damage to rented premises limit captured on the submission GL coverages. | Number in dollars, no commas. | Damage to premises rented to you limit threshold. |
| `glMedicalExpense` | GL medical expense limit | Medical expense limit captured on the submission GL coverages. | Number in dollars, no commas. | Medical expense limit over $10K. |
| `glTotalSubcontractorCost` | GL total subcontractor cost | Total subcontractor cost captured on the submission GL coverages. | Number in dollars, no commas. | Subcontractor cost exceeds threshold. |
| `glAdditionalInsuredCount` | GL additional insured count | Count of individual additional insured endorsements on GL. | Whole number. | More than 5 scheduled additional insureds. |
| `glBlanketAdditionalInsured` | GL blanket additional insured | Whether blanket additional insured applies on GL. | `1` for yes, `0` for no. | Blanket AI requested; blanket AI not allowed. |
| `glWaiverOfSubrogationCount` | GL waiver of subrogation count | Count of individual waiver of subrogation endorsements on GL. | Whole number. | More than 5 scheduled WOS endorsements. |
| `glBlanketWaiverOfSubrogation` | GL blanket waiver of subrogation | Whether blanket waiver of subrogation applies on GL. | `1` for yes, `0` for no. | Blanket WOS requested; blanket WOS not allowed. |
| `glPrimaryNonContributory` | GL primary & non-contributory | Whether primary and non-contributory wording applies on GL. | `1` for yes, `0` for no. | PNC requested; PNC not allowed. |
| `glIncludeTria` | GL TRIA included | Whether TRIA is included on GL. | `1` for yes, `0` for no. | TRIA included or excluded requirement. |
| `glClassificationCount` | GL classification count | Count of GL classification/exposure rows on the submission. | Whole number. | More than one GL class; missing classifications. |
| `glTotalExposure` | GL total exposure | Sum of GL classification exposure values across active GL class rows. | Number in dollars or units as captured, no commas. | Total payroll/sales/exposure threshold. |
| `glMaxClassExposure` | GL largest class exposure | Largest single GL classification exposure value. | Number in dollars or units as captured, no commas. | Any one class exposure exceeds threshold. |
| `glClassCodes` | GL class codes | GL class codes from active submission GL classification rows. | String class code with `contains` or `notContains`. | Specific eligible or prohibited class code checks. |
| `glHasUnsupportedClassCode` | GL has unsupported class code | Whether any submitted GL class code is outside the Longleaf-supported class code list. | `1` for yes, `0` for no. | Only listed GL class codes are eligible. |
| `glScheduleCreditPercent` | GL schedule credit | Schedule credit percent derived from the rater schedule modifier. A 0.80 modifier is stored as 20. | Whole percent number. | Schedule credit over 20%. |
| `glLoggingRevenuePercent` | GL logging revenue | Percent of revenue from eligible logging operations, entered on the UW review sheet. | Whole percent number. | Logging revenue below 80%. |
| `glManagementExperienceYears` | GL management experience years | Years of logging management experience, entered on the UW review sheet or sourced from years in business when applicable. | Number of years. | Management experience below 3 years. |
| `glLargestSingleLossAmount` | GL largest single loss | Largest single loss amount entered on the UW review sheet. | Number in dollars, no commas. | Single loss over $75K. |
| `glFuelStorageOverMax` | GL fuel storage over max allowable | UW review yes/no indicating fuel storage exceeds the guideline maximum. | `1` for yes, `0` for no. | Fuel storage over maximum allowable amount. |
| `glLogRoadBuildingOverAllowed` | GL log road building exceeds allowed percent | UW review yes/no indicating log road building exceeds the guideline allowance. | `1` for yes, `0` for no. | Log road building over allowed percent. |
| `glGradingExcavationOverAllowed` | GL grading/excavation exceeds allowed percent | UW review yes/no indicating grading or excavation exceeds the guideline allowance. | `1` for yes, `0` for no. | Grading/excavation over allowed percent. |
| `glAircraftOrDroneOps` | GL aircraft/drone operations | UW review yes/no indicating aircraft, helicopter, airlift, or drone operations. | `1` for yes, `0` for no. | Aircraft, helicopter, airlift, or drone operations prohibited. |
| `glExplosivesUsed` | GL explosives used | UW review yes/no indicating use of explosives. | `1` for yes, `0` for no. | Explosives prohibited. |
| `glNonMechanizedLogging` | GL non-mechanized logging | UW review yes/no indicating non-mechanized logging operations. | `1` for yes, `0` for no. | Non-mechanized logging prohibited. |
| `glBankruptcyOrReceivership` | GL bankruptcy or receivership | UW review yes/no indicating bankruptcy or receivership. | `1` for yes, `0` for no. | Bankruptcy/receivership prohibited. |
| `glHerbicidePesticideApplication` | GL herbicide/pesticide application | UW review yes/no indicating herbicide or pesticide application. | `1` for yes, `0` for no. | Herbicide/pesticide application referral. |
| `glCraneUseOutsideAllowed` | GL crane use outside allowed operations | UW review yes/no indicating crane use beyond loading/unloading trailers. | `1` for yes, `0` for no. | Crane use referral. |
| `glEquipmentRentalToOthers` | GL equipment rental/leasing to others | UW review yes/no indicating equipment rental or leasing to others. | `1` for yes, `0` for no. | Equipment rental/leasing referral. |
| `glThirdPartyEquipmentRepair` | GL third-party equipment repair/service | UW review yes/no indicating service or repair of equipment not owned by the insured. | `1` for yes, `0` for no. | Third-party equipment repair referral. |
| `glRightOfWayClearing` | GL right-of-way clearing/maintenance | UW review yes/no indicating right-of-way clearing or maintenance. | `1` for yes, `0` for no. | Right-of-way clearing referral. |

Do not use unsupported field names such as `glAggregateLimit`, `generalLiabilityAggregate`, `buildingLimit`, `propertyLimit`, `payroll`, `sales`, `classCode`, `territory`, or `yearsInBusiness` unless they are added to this catalog in the future.

Only map to an existing field when the meaning is clearly the same. Do not map similar-looking but different concepts. For example, GL aggregate limit is not total insured value.

GL class codes support these text/list operators:

- `contains`
- `notContains`

For a guideline that says only the listed GL class codes are eligible, use:

```json
{ "field": "glHasUnsupportedClassCode", "operator": "==", "value": 1 }
```

For a specific class code referral or prohibition, use:

```json
{ "field": "glClassCodes", "operator": "contains", "value": "94007" }
```

For unconditional blockers, use `conditionJson: null`.

For conditional blockers or referrals, use only the documented field/operator/value schema above. If the field needed by the guideline is not listed, do not invent one. Set `conditionJson: null` and start `description` with `Needs SIMS field: <plain English field name>.` A human can then decide whether SIMS needs a new measurable field before publishing.

Example missing measurable:

```json
{
  "itemType": "AuthorityLimit",
  "stage": "Quote",
  "severity": "ReferralRequired",
  "ruleKey": "gl-aggregate-over-2m",
  "label": "GL aggregate over $2M",
  "description": "Needs SIMS field: GL aggregate limit. Guideline requires referral when the general liability aggregate limit exceeds $2,000,000.",
  "conditionJson": null,
  "isBlocking": false,
  "overrideAllowed": true,
  "overridePermission": "underwriting.clearance.override",
  "sourceCitation": "Max GL aggregate limit is $2M.",
  "aiConfidence": 0.82,
  "sortOrder": 30
}
```

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
