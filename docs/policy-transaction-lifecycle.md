# Policy Transaction Lifecycle

Policy transaction states are owned by the backend. UI code may display these states, but it should not invent new values or decide whether a transition is allowed.

## Status Definitions

| Status | Owner | Meaning | Terminal |
| --- | --- | --- | --- |
| Submitted | Underwriting | A transaction has been entered and is awaiting review or processing. | No |
| InReview | Underwriting | An underwriter is actively reviewing the transaction. | No |
| Referred | Senior Underwriting | The transaction is outside straight-through authority and needs referral approval. | No |
| Approved | Underwriting Authority | The transaction is approved to proceed to quote, acceptance, bind, or issue. | No |
| Quoted | Underwriting | The financial impact has been calculated and presented. | No |
| Accepted | Insured or Producer | The quoted terms have been accepted but are not fully bound or issued. | No |
| Bound | Underwriting | Coverage has been bound and downstream issuance/accounting can proceed. | No |
| NoticePending | Compliance | A required legal notice has been identified but not sent. | No |
| NoticeSent | Compliance | A required legal notice has been sent and the transaction is awaiting the effective date or final action. | No |
| PendingEffectiveDate | Operations | The transaction is approved or noticed and waiting for its effective date. | No |
| Issued | Operations | The transaction has been issued and financial processing may occur. | No |
| Completed | Operations | The transaction is fully complete with no further action expected. | Yes |
| Declined | Underwriting | The transaction was declined and cannot proceed. | Yes |
| Withdrawn | Producer or Insured | The request was withdrawn before completion. | Yes |
| Voided | Operations | The transaction was voided and should not be treated as active business. | Yes |

## Allowed Transitions

| From | Allowed To |
| --- | --- |
| Submitted | InReview, Referred, Approved, Quoted, Issued, Declined, Withdrawn, Voided |
| InReview | Referred, Approved, Quoted, Declined, Withdrawn, Voided |
| Referred | InReview, Approved, Declined, Withdrawn, Voided |
| Approved | Quoted, Accepted, Issued, Declined, Voided |
| Quoted | Accepted, Issued, Declined, Withdrawn, Voided |
| Accepted | Bound, Issued, Withdrawn, Voided |
| Bound | Issued, Voided |
| NoticePending | NoticeSent, Withdrawn, Voided |
| NoticeSent | PendingEffectiveDate, Issued, Completed, Voided |
| PendingEffectiveDate | Issued, Completed, Voided |
| Issued | Completed, Voided |
| Completed | None |
| Declined | None |
| Withdrawn | None |
| Voided | None |

## Workflow Events

Every created transaction records `policy.transaction.created` and a status-specific event. Later transitions record only the status-specific event.

Core normalized events:

- `policy.transaction.created`
- `policy.transaction.submitted`
- `policy.transaction.approved`
- `policy.transaction.issued`
- `policy.transaction.completed`

Additional status events follow the same naming pattern, for example `policy.transaction.referred` and `policy.transaction.notice_sent`.
