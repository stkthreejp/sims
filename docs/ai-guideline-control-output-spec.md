# AI Guideline Control Output Spec

Use this document when asking Claude/Desktop or another AI tool to convert underwriting guidelines into SIMS underwriting controls.

## Goal

Read the underwriting guideline document and return proposed controls that a human can review in SIMS Admin -> UW Controls.

Do not approve, publish, or enforce anything. The output is only a proposed control list for human review.

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
{ "field": "lossRatio", "operator": ">", "value": 50 }
```

Allowed condition fields:

- `largestSingleItemValue`
- `totalInsuredValue`
- `premiumAmount`
- `totalPremium`
- `lossRatio`
- `driverCount`
- `vehicleCount`
- `isFilingState`

Allowed operators:

- `>`
- `>=`
- `<`
- `<=`
- `==`
- `!=`

If the guideline needs a field SIMS does not support, do not invent a field. Set `conditionJson` to `null` and mention the missing field in `description` or `sourceCitation`.

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
- if a needed field is missing, do not invent one; set conditionJson to null and mention the missing field in description or sourceCitation

Prefer clear, actionable controls. Skip vague prose that does not create a reviewable requirement, blocker, referral, authority limit, appetite rule, or checklist item.
```

