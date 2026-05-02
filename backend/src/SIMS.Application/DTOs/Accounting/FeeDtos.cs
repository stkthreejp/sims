namespace SIMS.Application.DTOs.Accounting;

public record FeeDefinitionDto(
    long Id,
    string Code,
    string DisplayName,
    string FeeCategory,
    bool IsTaxable,
    int CalculationOrder,
    int LedgerAccountId
);

public record FeeRuleVersionDto(
    long Id,
    long FeeDefinitionId,
    string FeeCode,
    string FeeDisplayName,
    int? CompanyId,
    int? ProducerId,
    string? LineOfBusiness,
    string? StateCode,
    string? City,
    string? LicenseType,
    DateOnly EffectiveDate,
    DateOnly? DisabledDate,
    string CalcType,
    decimal? FlatAmount,
    decimal? PercentRate,
    bool PercentOfNet,
    decimal? MinimumAmount,
    decimal? MaxPercent,
    decimal? MaxAmount,
    bool Commissionable,
    string InstallmentBehavior,
    bool SplitByParticipation,
    bool FullyEarned,
    int? FullyEarnedDays,
    bool ExcludeTerrorism,
    bool MultiplyByLocations,
    bool MultiplyByVehicles,
    bool SendToAccounting,
    bool ApplyAutomatically,
    decimal? PremiumMinThreshold,
    decimal? PremiumMaxThreshold,
    string? PremiumThresholdBasis,
    string RoundingMode,
    bool ExcludeWhenNotFiling,
    bool ExcludeOnEndorsements,
    string PayableRouting,
    long? PayablePayeeId,
    string? Notes,
    IReadOnlyList<FeePremiumBracketDto> PremiumBrackets,
    IReadOnlyList<string> NonTaxableStates
);

public record FeePremiumBracketDto(
    long Id,
    decimal TierFrom,
    decimal? TierTo,
    decimal PercentRate
);

public record FeeAuditLogDto(
    long Id,
    Guid EditedBy,
    DateTime EditedAt,
    string ChangeType,
    string? FieldChanges,
    string? Notes
);

public record CreateFeeDefinitionRequest(
    string Code,
    string DisplayName,
    string FeeCategory,
    bool IsTaxable,
    int CalculationOrder,
    int LedgerAccountId
);

public record CreateFeeRuleVersionRequest(
    long FeeDefinitionId,
    int? CompanyId,
    int? ProducerId,
    string? LineOfBusiness,
    string? StateCode,
    string? City,
    string? LicenseType,
    DateOnly EffectiveDate,
    string CalcType,
    decimal? FlatAmount,
    decimal? PercentRate,
    bool PercentOfNet,
    decimal? MinimumAmount,
    decimal? MaxPercent,
    decimal? MaxAmount,
    bool Commissionable,
    string InstallmentBehavior,
    bool SplitByParticipation,
    bool FullyEarned,
    int? FullyEarnedDays,
    bool ExcludeTerrorism,
    bool MultiplyByLocations,
    bool MultiplyByVehicles,
    bool SendToAccounting,
    bool ApplyAutomatically,
    decimal? PremiumMinThreshold,
    decimal? PremiumMaxThreshold,
    string? PremiumThresholdBasis,
    string RoundingMode,
    bool ExcludeWhenNotFiling,
    bool ExcludeOnEndorsements,
    string PayableRouting,
    long? PayablePayeeId,
    string? Notes,
    IReadOnlyList<FeePremiumBracketDto> PremiumBrackets
);

public record SetStateTaxabilityRequest(IReadOnlyList<string> NonTaxableStateCodes);
