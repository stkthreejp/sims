using Microsoft.EntityFrameworkCore;
using SIMS.Application.Common;
using SIMS.Application.DTOs.Policies;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;

namespace SIMS.Application.Services;

public class PolicyTransactionLifecycleService : IPolicyTransactionLifecycleService
{
    private static readonly IReadOnlyDictionary<PolicyTransactionStatus, PolicyTransactionStatus[]> AllowedTransitions =
        new Dictionary<PolicyTransactionStatus, PolicyTransactionStatus[]>
        {
            [PolicyTransactionStatus.Submitted] = [PolicyTransactionStatus.InReview, PolicyTransactionStatus.Referred, PolicyTransactionStatus.Approved, PolicyTransactionStatus.Quoted, PolicyTransactionStatus.Issued, PolicyTransactionStatus.Declined, PolicyTransactionStatus.Withdrawn, PolicyTransactionStatus.Voided],
            [PolicyTransactionStatus.InReview] = [PolicyTransactionStatus.Referred, PolicyTransactionStatus.Approved, PolicyTransactionStatus.Quoted, PolicyTransactionStatus.Declined, PolicyTransactionStatus.Withdrawn, PolicyTransactionStatus.Voided],
            [PolicyTransactionStatus.Referred] = [PolicyTransactionStatus.InReview, PolicyTransactionStatus.Approved, PolicyTransactionStatus.Declined, PolicyTransactionStatus.Withdrawn, PolicyTransactionStatus.Voided],
            [PolicyTransactionStatus.Approved] = [PolicyTransactionStatus.Quoted, PolicyTransactionStatus.Accepted, PolicyTransactionStatus.Issued, PolicyTransactionStatus.Declined, PolicyTransactionStatus.Voided],
            [PolicyTransactionStatus.Quoted] = [PolicyTransactionStatus.Accepted, PolicyTransactionStatus.Issued, PolicyTransactionStatus.Declined, PolicyTransactionStatus.Withdrawn, PolicyTransactionStatus.Voided],
            [PolicyTransactionStatus.Accepted] = [PolicyTransactionStatus.Bound, PolicyTransactionStatus.Issued, PolicyTransactionStatus.Withdrawn, PolicyTransactionStatus.Voided],
            [PolicyTransactionStatus.Bound] = [PolicyTransactionStatus.Issued, PolicyTransactionStatus.Voided],
            [PolicyTransactionStatus.NoticePending] = [PolicyTransactionStatus.NoticeSent, PolicyTransactionStatus.Withdrawn, PolicyTransactionStatus.Voided],
            [PolicyTransactionStatus.NoticeSent] = [PolicyTransactionStatus.PendingEffectiveDate, PolicyTransactionStatus.Issued, PolicyTransactionStatus.Completed, PolicyTransactionStatus.Voided],
            [PolicyTransactionStatus.PendingEffectiveDate] = [PolicyTransactionStatus.Issued, PolicyTransactionStatus.Completed, PolicyTransactionStatus.Voided],
            [PolicyTransactionStatus.Issued] = [PolicyTransactionStatus.Completed, PolicyTransactionStatus.Voided],
            [PolicyTransactionStatus.Completed] = [],
            [PolicyTransactionStatus.Declined] = [],
            [PolicyTransactionStatus.Withdrawn] = [],
            [PolicyTransactionStatus.Voided] = [],
        };

    private static readonly IReadOnlyDictionary<PolicyTransactionStatus, string> StatusEvents =
        new Dictionary<PolicyTransactionStatus, string>
        {
            [PolicyTransactionStatus.Submitted] = "policy.transaction.submitted",
            [PolicyTransactionStatus.InReview] = "policy.transaction.in_review",
            [PolicyTransactionStatus.Referred] = "policy.transaction.referred",
            [PolicyTransactionStatus.Approved] = "policy.transaction.approved",
            [PolicyTransactionStatus.Quoted] = "policy.transaction.quoted",
            [PolicyTransactionStatus.Accepted] = "policy.transaction.accepted",
            [PolicyTransactionStatus.Bound] = "policy.transaction.bound",
            [PolicyTransactionStatus.NoticePending] = "policy.transaction.notice_pending",
            [PolicyTransactionStatus.NoticeSent] = "policy.transaction.notice_sent",
            [PolicyTransactionStatus.PendingEffectiveDate] = "policy.transaction.pending_effective_date",
            [PolicyTransactionStatus.Issued] = "policy.transaction.issued",
            [PolicyTransactionStatus.Completed] = "policy.transaction.completed",
            [PolicyTransactionStatus.Declined] = "policy.transaction.declined",
            [PolicyTransactionStatus.Withdrawn] = "policy.transaction.withdrawn",
            [PolicyTransactionStatus.Voided] = "policy.transaction.voided",
        };

    public static readonly IReadOnlyList<PolicyTransactionStatusDefinition> StatusDefinitions =
    [
        new(PolicyTransactionStatus.Submitted, "Submitted", "Underwriting", "A transaction has been entered and is awaiting review or processing.", false),
        new(PolicyTransactionStatus.InReview, "In Review", "Underwriting", "An underwriter is actively reviewing the transaction.", false),
        new(PolicyTransactionStatus.Referred, "Referred", "Senior Underwriting", "The transaction is outside straight-through authority and needs referral approval.", false),
        new(PolicyTransactionStatus.Approved, "Approved", "Underwriting Authority", "The transaction is approved to proceed to quote, acceptance, bind, or issue.", false),
        new(PolicyTransactionStatus.Quoted, "Quoted", "Underwriting", "The financial impact has been calculated and presented.", false),
        new(PolicyTransactionStatus.Accepted, "Accepted", "Insured or Producer", "The quoted terms have been accepted but are not fully bound or issued.", false),
        new(PolicyTransactionStatus.Bound, "Bound", "Underwriting", "Coverage has been bound and downstream issuance/accounting can proceed.", false),
        new(PolicyTransactionStatus.NoticePending, "Notice Pending", "Compliance", "A required legal notice has been identified but not sent.", false),
        new(PolicyTransactionStatus.NoticeSent, "Notice Sent", "Compliance", "A required legal notice has been sent and the transaction is awaiting the effective date or final action.", false),
        new(PolicyTransactionStatus.PendingEffectiveDate, "Pending Effective Date", "Operations", "The transaction is approved or noticed and waiting for its effective date.", false),
        new(PolicyTransactionStatus.Issued, "Issued", "Operations", "The transaction has been issued and financial processing may occur.", false),
        new(PolicyTransactionStatus.Completed, "Completed", "Operations", "The transaction is fully complete with no further action expected.", true),
        new(PolicyTransactionStatus.Declined, "Declined", "Underwriting", "The transaction was declined and cannot proceed.", true),
        new(PolicyTransactionStatus.Withdrawn, "Withdrawn", "Producer or Insured", "The request was withdrawn before completion.", true),
        new(PolicyTransactionStatus.Voided, "Voided", "Operations", "The transaction was voided and should not be treated as active business.", true),
    ];

    private readonly DbContext _db;
    private readonly IWorkflowEngineService _workflow;

    public PolicyTransactionLifecycleService(DbContext db, IWorkflowEngineService workflow)
    {
        _db = db;
        _workflow = workflow;
    }

    public static bool CanTransition(PolicyTransactionStatus fromStatus, PolicyTransactionStatus toStatus)
        => fromStatus == toStatus ||
           (AllowedTransitions.TryGetValue(fromStatus, out var allowed) && allowed.Contains(toStatus));

    public async Task<Result> RecordCreatedAsync(PolicyTransaction transaction, Guid userId, string? notes = null)
    {
        await RecordHistoryAndEventAsync(transaction, null, transaction.Status, "policy.transaction.created", userId, notes);
        await RecordHistoryAndEventAsync(transaction, null, transaction.Status, GetEventName(transaction.Status), userId, notes);
        await _db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> TransitionAsync(PolicyTransaction transaction, PolicyTransactionStatus toStatus, Guid userId, string? notes = null)
    {
        var fromStatus = transaction.Status;
        if (!CanTransition(fromStatus, toStatus))
        {
            return Result.Failure(
                "INVALID_TRANSACTION_STATUS_TRANSITION",
                $"Cannot transition policy transaction {transaction.Id} from {fromStatus} to {toStatus}.");
        }

        if (fromStatus == toStatus)
            return Result.Success();

        transaction.Status = toStatus;
        transaction.UpdatedAt = DateTime.UtcNow;
        await RecordHistoryAndEventAsync(transaction, fromStatus, toStatus, GetEventName(toStatus), userId, notes);
        await _db.SaveChangesAsync();
        return Result.Success();
    }

    private static string GetEventName(PolicyTransactionStatus status) => StatusEvents[status];

    private async Task RecordHistoryAndEventAsync(
        PolicyTransaction transaction,
        PolicyTransactionStatus? fromStatus,
        PolicyTransactionStatus toStatus,
        string eventName,
        Guid userId,
        string? notes)
    {
        _db.Set<PolicyTransactionStatusHistory>().Add(new PolicyTransactionStatusHistory
        {
            PolicyTransactionId = transaction.Id,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            EventName = eventName,
            ChangedById = userId,
            ChangedAt = DateTime.UtcNow,
            Notes = notes,
        });

        await _workflow.FireEventAsync(
            eventName,
            TaskEntityType.Policy,
            transaction.Id,
            BuildContext(transaction, fromStatus, toStatus));
    }

    private static Dictionary<string, object> BuildContext(
        PolicyTransaction transaction,
        PolicyTransactionStatus? fromStatus,
        PolicyTransactionStatus toStatus)
    {
        var context = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["PolicyTransactionId"] = transaction.Id,
            ["PolicyId"] = transaction.PolicyId,
            ["TransactionType"] = transaction.TransactionType.ToString(),
            ["Status"] = toStatus.ToString(),
            ["ToStatus"] = toStatus.ToString(),
            ["EffectiveDate"] = transaction.EffectiveDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
        };

        if (fromStatus.HasValue)
            context["FromStatus"] = fromStatus.Value.ToString();

        return context;
    }
}
