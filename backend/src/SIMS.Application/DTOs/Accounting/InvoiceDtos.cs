namespace SIMS.Application.DTOs.Accounting;

public record CreateInvoiceRequest(
    DateOnly EffectiveDate,
    decimal GrossPremium,
    string StateCode,
    bool IsEndorsement,
    bool IsFilingState,
    Guid? CarrierId,
    int? CompanyId,
    int? ProducerId,
    string? LineOfBusiness,
    string? City,
    string? LicenseType,
    int LocationCount = 1,
    int VehicleCount = 1,
    Guid? PolicyTransactionId = null
);

public record InvoiceSummaryDto(
    long Id,
    string InvoiceNumber,
    DateOnly InvoiceDate,
    DateOnly EffectiveDate,
    decimal GrossPremium,
    decimal TotalFees,
    decimal TotalAmount,
    string Status
);

public record InvoiceLineDto(
    long Id,
    string FeeCode,
    string FeeDisplayName,
    string FeeCategory,
    decimal Amount,
    bool IsTaxable,
    string AccountCode,
    string AccountLabel
);

public record LedgerEntryDto(
    long Id,
    string AccountCode,
    string AccountLabel,
    decimal Debit,
    decimal Credit,
    string? Memo
);

public record InvoiceDetailDto(
    long Id,
    string InvoiceNumber,
    DateOnly InvoiceDate,
    DateOnly EffectiveDate,
    decimal GrossPremium,
    decimal TotalFees,
    decimal TotalAmount,
    string Status,
    Guid LedgerTransactionId,
    IReadOnlyList<InvoiceLineDto> Lines,
    IReadOnlyList<LedgerEntryDto> LedgerEntries
);

public record InvoicePreviewDto(
    decimal GrossPremium,
    decimal TotalFees,
    decimal TotalAmount,
    IReadOnlyList<InvoiceLineDto> Lines
);
