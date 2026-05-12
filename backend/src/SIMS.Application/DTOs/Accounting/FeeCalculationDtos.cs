namespace SIMS.Application.DTOs.Accounting;

public record PolicyContext(
    DateOnly EffectiveDate,
    decimal GrossPremium,
    string StateCode,
    bool IsEndorsement,
    bool IsFilingState,       // false = non-admitted, filing not required in this state
    Guid? CarrierId,
    int? CompanyId,
    int? ProducerId,
    string? LineOfBusiness,
    string? City,
    string? LicenseType,      // 'Admitted'|'Non-Admitted'
    int LocationCount = 1,
    int VehicleCount = 1
);

public record InvoiceLine(
    long FeeRuleVersionId,
    string FeeCode,
    string FeeDisplayName,
    string FeeCategory,
    decimal Amount,
    bool IsTaxable,
    string? PayableRouting,   // 'NotPayable'|'Company'|'Entity'
    long? PayablePayeeId,
    int LedgerAccountId
);

public record FeeCalculationResult(IReadOnlyList<InvoiceLine> Lines);
