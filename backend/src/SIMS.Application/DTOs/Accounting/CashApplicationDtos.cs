namespace SIMS.Application.DTOs.Accounting;

public record ApplicationLineRequest(
    long InvoiceId,
    decimal GrossApplied,
    decimal CommissionAmount
);

public record ApplyCashRequest(
    long ReceiptId,
    IReadOnlyList<ApplicationLineRequest> Lines
);

public record OpenInvoiceDto(
    long Id,
    string InvoiceNumber,
    DateOnly InvoiceDate,
    decimal TotalAmount,
    decimal ClearedAmount,
    decimal OpenBalance,
    string Status
);

public record ApplyCashResultDto(
    long ReceiptId,
    string ReceiptNumber,
    decimal ReceiptAmount,
    decimal AppliedAmount,
    decimal RemainingAmount,
    string ReceiptStatus,
    IReadOnlyList<ReceiptApplicationDto> Applications
);
