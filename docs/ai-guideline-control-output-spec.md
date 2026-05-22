# AI Guideline Control Output Spec

Use this document when asking Claude/Desktop or another AI tool to convert underwriting guidelines into SIMS underwriting controls.

## Goal

Read the underwriting guideline document and return proposed controls that a human can review in SIMS Admin -> UW Controls.

Do not approve, publish, or enforce anything. The output is only a proposed control list for human review.

In SIMS, fill out the Guideline Scope fields first, then use `Upload JSON` or paste the JSON and click `Create From AI JSON`. SIMS will create the guideline document and add the proposed controls for review.

## Required Output

Return JSON only. Do not include markdown fences, commentary, explanations, or text before/after the JSON.

```json
{
  "controls": [
    {
      "itemType": "DocumentChecklistItem",
      "stage": "Submission",
      "severity": "Warning",
      "ruleKey": "completed-acord-125",
      "label": "Completed ACORD 125",
      "description": "Completed ACORD 125 is required for underwriting review.",
      "conditionJson": null,
      "isBlocking": false,
      "overrideAllowed": true,
      "overridePermission": "underwriting.clearance.override",
      "sourceCitation": "Submission requirements: Completed ACORD 125 is required.",
      "aiConfidence": 0.85,
      "sortOrder": 10
    }
  ]
}
```

## Allowed Values

`itemType` must be one of:

- `AppetiteRule`
- `ReferralTrigger`
- `AuthorityLimit`
- `DocumentChecklistItem`
- `AppetiteNote`

`stage` must be one of:

- `Submission`
- `Quote`
- `Bind`
- `Issue`
- `PostBind`
- `Renewal`

`severity` must be one of:

- `Informational`
- `Warning`
- `ReferralRequired`
- `HardBlock`

## Condition Rules

Use `conditionJson: null` for unconditional controls, checklist requirements, and blockers that always apply.

For conditional referrals/blockers, use an object with exactly `field`, `operator`, and `value`:

```json
{ "field": "lossRatio", "operator": ">", "value": 0.5 }
```

Allowed condition fields are listed in the SIMS measurable field catalog below. Only use fields from that catalog.

Allowed operators:

- `>`
- `>=`
- `<`
- `<=`
- `==`
- `!=`

If the guideline needs a field SIMS does not support, do not invent a field. Set `conditionJson` to `null` and start the description with `Needs SIMS field: <plain English field name>.`

## SIMS Measurable Field Catalog

These are the only fields SIMS can currently check automatically for underwriting guideline enforcement.

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

Do not use unsupported field names such as `glAggregateLimit`, `generalLiabilityAggregate`, `buildingLimit`, `propertyLimit`, `payroll`, `sales`, `classCode`, `territory`, or `yearsInBusiness` unless they are added to this catalog in the future.

## Missing Measurable Guidance

Some guideline requirements are real underwriting rules, but SIMS does not yet store the needed value as a measurable field. Keep those controls reviewable, but do not make up `conditionJson`.

When a measurable field is missing:

- Set `conditionJson` to `null`.
- Start `description` with `Needs SIMS field: <field name>.`
- Keep the rest of `description` in plain English.
- Keep the original guideline wording in `sourceCitation`.
- Use `severity` and `isBlocking` based on what the guideline says, even though enforcement will be manual until SIMS adds the field.

Example:

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

Only map to an existing field when the meaning is clearly the same. For example, do not map GL aggregate limit to `totalInsuredValue`; those are different underwriting facts.

## Control Guidance

- Use clear, short `ruleKey` values in lowercase kebab-case.
- Make `label` human-readable and concise.
- Use `description` to explain what the guideline requires.
- Use `sourceCitation` to cite the shortest useful phrase, section name, or sentence from the source guideline.
- Set `isBlocking: true` only when the guideline clearly says the item must block bind/issue or cannot proceed.
- Set `overrideAllowed: true` unless the guideline clearly says no override is allowed.
- Use `overridePermission: "underwriting.clearance.override"` unless told otherwise.
- Use `aiConfidence` from `0` to `1`.
- Use `sortOrder` increments of `10`.

## Prompt To Use

Copy this prompt into Claude/Desktop with the guideline file attached:

```text
Convert the attached underwriting guideline into SIMS proposed underwriting controls.

Return JSON only using the exact schema and allowed values from this spec:
- itemType: AppetiteRule, ReferralTrigger, AuthorityLimit, DocumentChecklistItem, AppetiteNote
- stage: Submission, Quote, Bind, Issue, PostBind, Renewal
- severity: Informational, Warning, ReferralRequired, HardBlock

For conditionJson:
- use null for unconditional controls
- use { "field": "...", "operator": "...", "value": ... } only with documented SIMS fields
- if a needed field is missing, do not invent one; set conditionJson to null and start description with "Needs SIMS field: ..."
- do not map similar-looking but different concepts; for example, GL aggregate limit is not total insured value

Prefer clear, actionable controls. Skip vague prose that does not create a reviewable requirement, blocker, referral, authority limit, appetite rule, or checklist item.
```
