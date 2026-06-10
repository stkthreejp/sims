using SIMS.Application.DTOs.Accounting;
using SIMS.Domain.Enums;

namespace SIMS.Application.DTOs.Reports;

public record TrustReconciliationDto(
    DateOnly AsOf,
    decimal TrustBalance,
    decimal UnappliedReceipts,
    decimal OpenInvoices,
    decimal ReconcilingDifference,
    IReadOnlyList<TrustTransactionLineDto> RecentActivity
);

public record TrustTransactionLineDto(
    DateTime PostedAt,
    DateOnly EffectiveDate,
    string SourceType,
    string? Memo,
    decimal Debit,
    decimal Credit,
    decimal RunningBalance
);

public record OpenReceivableDto(
    long Id,
    string InvoiceNumber,
    string AgentName,
    Guid? AgentId,
    decimal TotalAmount,
    decimal ClearedAmount,
    decimal Balance,
    DateOnly InvoiceDate,
    DateOnly DueDate,
    int DaysOutstanding,
    string Status
);

public record BrokerArRowDto(
    string AgentName,
    Guid? AgentId,
    decimal Current,
    decimal Days31to60,
    decimal Days61to90,
    decimal Over90,
    decimal Total
);

public record BrokerArAgingDto(
    AgingBucketDto Summary,
    IReadOnlyList<BrokerArRowDto> Rows,
    IReadOnlyList<OpenReceivableDto> Receivables
);

public record CommissionPeriodDto(
    int Year,
    int Month,
    decimal Earned,
    decimal AgentPaid,
    decimal NetRetained,
    decimal CashReceived,
    int InvoiceCount
);

public record CommissionSummaryDto(
    IReadOnlyList<CommissionPeriodDto> Periods,
    decimal TotalEarned,
    decimal TotalAgentPaid,
    decimal TotalNetRetained,
    decimal TotalCashReceived
);

public record InvoiceTotalsByPolicyTransactionDto(
    IReadOnlyList<InvoiceTotalsByPolicyTransactionRowDto> Rows
);

public record InvoiceTotalsByPolicyTransactionRowDto(
    Guid? PolicyTransactionId,
    string PolicyTransactionNumber,
    TransactionType? PolicyTransactionType,
    Guid? PolicyVersionId,
    int? PolicyVersionNumber,
    int InvoiceCount,
    decimal GrossPremium,
    decimal TotalFees,
    decimal TotalAmount
);

public record InvoiceTotalsByProgramDto(
    IReadOnlyList<InvoiceTotalsByProgramRowDto> Rows,
    IReadOnlyList<InvoiceTotalsByProgramOptionDto> AvailablePrograms
);

public record InvoiceTotalsByProgramOptionDto(
    Guid Id,
    string Name,
    string Code
);

public record InvoiceTotalsByProgramRowDto(
    Guid? ProgramId,
    string ProgramName,
    string? ProgramCode,
    int InvoiceCount,
    decimal GrossPremium,
    decimal TotalFees,
    decimal TotalAmount,
    decimal CommissionAmount,
    decimal AgentCommissionAmount,
    decimal NetRetained
);

public record PostBindFollowUpDto(
    IReadOnlyList<PostBindFollowUpRowDto> Rows
);

public record PostBindFollowUpRowDto(
    Guid PolicyId,
    string PolicyNumber,
    Guid BoundQuoteId,
    string InsuredName,
    string CarrierName,
    PolicyLineOfBusiness LineOfBusiness,
    Guid? ProgramId,
    string? ProgramName,
    string? ProgramCode,
    string? State,
    DateOnly BoundDate,
    DateOnly? IssuedDate,
    int DaysSinceBind,
    int? DaysSinceIssue,
    Guid? OwnerId,
    string? OwnerName,
    DateOnly DueDate,
    int DaysUntilDue,
    string SlaStatus,
    int OpenRequiredItemCount,
    IReadOnlyList<string> OpenRequiredItems
);

public record ManagerQueueDto(
    int PendingReferralCount,
    int PendingAuthorityApprovalCount,
    int PostBindFollowUpCount,
    IReadOnlyList<ManagerQueueRowDto> Rows
);

public record ManagerQueueRowDto(
    Guid Id,
    string WorkType,
    string Title,
    string Detail,
    string Priority,
    string ReferenceNumber,
    string? InsuredName,
    Guid? SubmissionId,
    Guid? QuoteId,
    Guid? PolicyId,
    Guid? OwnerId,
    string? OwnerName,
    DateTime CreatedAt,
    DateOnly? DueDate,
    int DaysOpen,
    string SlaStatus,
    string ActionUrl
);

public record UnassignedProgramCleanupDto(
    int OpenQuoteCount,
    int ActivePolicyCount,
    IReadOnlyList<UnassignedProgramCleanupRowDto> Rows
);

public record UnassignedProgramCleanupRowDto(
    Guid Id,
    string RecordType,
    string ReferenceNumber,
    string InsuredName,
    string CarrierName,
    PolicyLineOfBusiness LineOfBusiness,
    string? State,
    string Status,
    DateOnly EffectiveDate,
    DateOnly ExpirationDate,
    Guid? SubmissionId,
    Guid? QuoteId,
    Guid? PolicyId,
    string ActionUrl
);

public record AuthorityApprovalActivityDto(
    int PendingCount,
    int ApprovedCount,
    int DeclinedCount,
    int CancelledCount,
    int OverrideCount,
    int OverduePendingCount,
    decimal? AverageDecisionHours,
    IReadOnlyList<AuthorityApprovalActivityRowDto> Rows
);

public record AuthorityApprovalActivityRowDto(
    Guid Id,
    AuthorityApprovalTargetType TargetType,
    Guid TargetId,
    string ActionCode,
    string ActionLabel,
    string ApprovalType,
    bool IsOverride,
    string Reason,
    string Status,
    string ReferenceNumber,
    string? InsuredName,
    Guid? ProgramId,
    string? ProgramName,
    string? ProgramCode,
    PolicyLineOfBusiness? LineOfBusiness,
    string? State,
    Guid RequestedById,
    string? RequestedByName,
    Guid? OwnerId,
    string? OwnerName,
    Guid? DecisionById,
    string? DecisionByName,
    DateTime RequestedAt,
    DateTime? DueAt,
    DateTime? DecisionAt,
    decimal? DecisionHours,
    int? HoursUntilDue,
    string SlaStatus,
    string ActionUrl
);

public record DeclineReasonReportDto(
    int TotalDeclines,
    int WithReasonCount,
    int UnspecifiedCount,
    IReadOnlyList<DeclineReasonSummaryDto> Reasons,
    IReadOnlyList<DeclineReasonRowDto> Rows
);

public record DeclineReasonSummaryDto(
    string Reason,
    int Count,
    decimal Share
);

public record DeclineReasonRowDto(
    Guid QuoteId,
    string QuoteNumber,
    Guid SubmissionId,
    string SubmissionNumber,
    string InsuredName,
    string CarrierName,
    PolicyLineOfBusiness LineOfBusiness,
    Guid? ProgramId,
    string? ProgramName,
    string? ProgramCode,
    string? State,
    string Reason,
    DateTime DeclinedAt,
    string ActionUrl
);

public record ClearanceOverrideReportDto(
    int TotalOverrides,
    int BlockedOverrideCount,
    int WarningOverrideCount,
    IReadOnlyList<ClearanceOverrideSummaryDto> CheckTypes,
    IReadOnlyList<ClearanceOverrideRowDto> Rows
);

public record ClearanceOverrideSummaryDto(
    UnderwritingClearanceCheckType CheckType,
    int Count
);

// ── Production Reports ────────────────────────────────────────────────────────

public record RenewalsUpcomingDto(
    int DaysAhead,
    int TotalCount,
    IReadOnlyList<RenewalsUpcomingRowDto> Rows);

public record RenewalsUpcomingRowDto(
    Guid PolicyId,
    string PolicyNumber,
    string InsuredName,
    string? AgentName,
    Guid? ProgramId,
    string? ProgramCode,
    string? ProgramName,
    Guid CarrierId,
    string CarrierName,
    PolicyLineOfBusiness LineOfBusiness,
    DateOnly EffectiveDate,
    DateOnly ExpirationDate,
    int DaysUntilExpiry,
    decimal PremiumAmount,
    bool HasRenewalSubmission);

public record BoundByPeriodDto(
    DateOnly DateFrom,
    DateOnly DateTo,
    int TotalPolicies,
    decimal TotalGrossPremium,
    IReadOnlyList<BoundByPeriodPeriodRowDto> Periods,
    IReadOnlyList<BoundByPeriodBreakdownRowDto> Breakdown);

public record BoundByPeriodPeriodRowDto(
    int Year,
    int Month,
    int PolicyCount,
    decimal GrossPremium,
    decimal TotalPremium);

public record BoundByPeriodBreakdownRowDto(
    Guid? ProgramId,
    string? ProgramCode,
    string ProgramName,
    Guid CarrierId,
    string CarrierName,
    PolicyLineOfBusiness LineOfBusiness,
    int PolicyCount,
    decimal GrossPremium,
    decimal TotalPremium);

public record HitRatioByCarrierDto(
    DateOnly DateFrom,
    DateOnly DateTo,
    int TotalQuotes,
    int TotalBound,
    decimal OverallHitRatio,
    IReadOnlyList<HitRatioByCarrierRowDto> Rows);

public record HitRatioByCarrierRowDto(
    Guid CarrierId,
    string CarrierName,
    int TotalQuotes,
    int BoundCount,
    int DeclinedCount,
    int ExpiredCount,
    int OpenCount,
    decimal HitRatio);

public record ClearanceOverrideRowDto(
    Guid Id,
    Guid SubmissionId,
    string SubmissionNumber,
    string InsuredName,
    Guid? ProgramId,
    string? ProgramName,
    string? ProgramCode,
    string? State,
    PolicyLineOfBusiness? LineOfBusiness,
    UnderwritingClearanceCheckType CheckType,
    UnderwritingClearanceStatus Status,
    Guid? MatchedRecordId,
    string? MatchedRecordLabel,
    string Explanation,
    Guid? OverriddenById,
    string? OverriddenByName,
    DateTime? OverriddenAt,
    string OverrideReason,
    DateTime ReviewedAt,
    string ActionUrl
);
