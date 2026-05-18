using SIMS.Domain.Enums;

namespace SIMS.Application.DTOs.Accounting;

public record ActivityEventDto(
    Guid TransactionId,
    string SourceType,          // Invoice|Receipt|CashApplication|Disbursement|Distribution
    long SourceId,
    string SourceNumber,        // INV-2026-00001, RCT-..., etc.
    string? SourceDescription,  // payerName, payeeName, etc.
    Guid? SourcePolicyTransactionId,
    string? SourcePolicyTransactionNumber,
    TransactionType? SourcePolicyTransactionType,
    Guid? SourcePolicyVersionId,
    int? SourcePolicyVersionNumber,
    DateOnly EffectiveDate,
    DateTime PostedAt,
    decimal TotalDebits,
    decimal TotalCredits,
    string PostingStatus,       // Posted|Voided|Reversal
    Guid? VoidedByTransactionId,
    Guid? ReversesTransactionId,
    string? VoidReason,
    DateTime? VoidedAt,
    bool CanVoid,
    string? VoidBlockReason,
    IReadOnlyList<ActivityLedgerLineDto> Lines
);

public record ActivityLedgerLineDto(
    long Id,
    string AccountCode,
    string AccountName,
    decimal Debit,
    decimal Credit,
    string? Memo,
    string PostingStatus
);

public record ActivityFilterRequest(
    DateOnly? FromDate,
    DateOnly? ToDate,
    string? SourceType,
    string? PostingStatus
);

public record VoidRequest(string? Reason);

public record VoidResultDto(
    bool Success,
    string? ErrorCode,
    string? ErrorMessage,
    Guid? ReversalTransactionId
);
