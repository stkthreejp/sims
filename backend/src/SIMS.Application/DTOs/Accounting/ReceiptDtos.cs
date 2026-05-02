namespace SIMS.Application.DTOs.Accounting;

public record CreateReceiptRequest(
    DateOnly ReceivedDate,
    decimal Amount,
    string PayerName,
    string? Reference
);

public record ReceiptSummaryDto(
    long Id,
    string ReceiptNumber,
    DateOnly ReceivedDate,
    string PayerName,
    decimal Amount,
    decimal AppliedAmount,
    string Status
);

public record ReceiptApplicationDto(
    long Id,
    long InvoiceId,
    string InvoiceNumber,
    decimal GrossApplied,
    decimal CommissionAmount,
    decimal NetApplied,
    DateTime CreatedAt
);

public record ReceiptDetailDto(
    long Id,
    string ReceiptNumber,
    DateOnly ReceivedDate,
    string PayerName,
    decimal Amount,
    decimal AppliedAmount,
    string Status,
    Guid LedgerTransactionId,
    IReadOnlyList<ReceiptApplicationDto> Applications
);
