namespace SIMS.Application.DTOs;

public record CarrierCommissionDto(
    long Id,
    string? LineOfBusiness,
    string? LineOfBusinessLabel,
    decimal CommissionRate,
    decimal SMMRetentionRate,
    DateOnly EffectiveDate,
    DateOnly? DisabledDate,
    bool IsActive,
    DateTime CreatedAt
);

public record CreateCarrierCommissionRequest(
    string? LineOfBusiness,
    decimal CommissionRate,
    decimal SMMRetentionRate,
    DateOnly EffectiveDate
);

public record DisableCarrierCommissionRequest(
    DateOnly? DisabledDate  // defaults to today if null
);

public record CarrierCommissionRates(decimal CommissionRate, decimal SMMRetentionRate);
