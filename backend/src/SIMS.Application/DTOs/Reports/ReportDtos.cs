using SIMS.Application.DTOs.Accounting;

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
