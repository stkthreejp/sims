namespace SIMS.Application.DTOs.Accounting;

// ---- Aging view ----

public record OpenPayableDto(
    long Id,
    long InvoiceId,
    string InvoiceNumber,
    string PayeeName,
    long? PayeeId,
    Guid? CarrierId,
    decimal Amount,
    decimal PaidAmount,
    decimal Balance,
    DateOnly InvoiceDate,
    DateOnly DueDate,
    int DaysOutstanding,   // positive = past due, 0 = current
    string Status
);

public record AgingBucketDto(
    decimal Current,        // 0-30 days past due
    decimal Days31to60,
    decimal Days61to90,
    decimal Over90,
    decimal Total
);

public record AgingRowDto(
    string PayeeName,
    long? PayeeId,
    Guid? CarrierId,
    decimal Current,
    decimal Days31to60,
    decimal Days61to90,
    decimal Over90,
    decimal Total
);

public record PayableAgingDto(
    AgingBucketDto Summary,
    IReadOnlyList<AgingRowDto> Rows,
    IReadOnlyList<OpenPayableDto> Payables
);

// ---- Disbursement creation ----

public record CreateDisbursementRequest(
    IReadOnlyList<DisbursementLineRequest> Lines,
    DateOnly PaymentDate,
    string PaymentMethod,   // Check|Wire|ACH
    string? Reference,
    string? Notes
);

public record DisbursementLineRequest(
    long PayableId,
    decimal Amount
);

// ---- Disbursement detail ----

public record DisbursementLineSummaryDto(
    long Id,
    long PayableId,
    string InvoiceNumber,
    string PayeeName,
    decimal Amount
);

public record DisbursementSummaryDto(
    long Id,
    string DisbursementNumber,
    string PayeeName,
    Guid? CarrierId,
    decimal TotalAmount,
    DateOnly PaymentDate,
    string PaymentMethod,
    string? Reference,
    string Status,
    DateTime CreatedAt
);

public record DisbursementDetailDto(
    long Id,
    string DisbursementNumber,
    string PayeeName,
    Guid? CarrierId,
    decimal TotalAmount,
    DateOnly PaymentDate,
    string PaymentMethod,
    string? Reference,
    string Status,
    Guid? LedgerTransactionId,
    string? Notes,
    DateTime CreatedAt,
    IReadOnlyList<DisbursementLineSummaryDto> Lines
);

// ---- Void ----

public record VoidDisbursementRequest(string? Reason);
