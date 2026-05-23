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
