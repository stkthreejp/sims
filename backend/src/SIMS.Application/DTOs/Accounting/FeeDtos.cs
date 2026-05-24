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

public record LedgerAccountOptionDto(
    int Id,
    string InternalCode,
    string ExternalLabel,
    string AccountType
);

public record PayeeOptionDto(
    long Id,
    string Name,
    string PayeeType
);

public record FeeRuleVersionDto(
    long Id,
    long FeeDefinitionId,
    string FeeCode,
    string FeeDisplayName,
    Guid? ProgramConfigurationId,
    Guid? CarrierId,
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
    bool ApplyOnlyOnce,
    bool MandatoryCharge,
    bool ApplyAutomatically,
    bool ApplyWhenPackagePolicyOnly,
    bool DoNotApplyWhenPackagePolicyOnly,
    bool ApplyToChildLines,
    bool OnlyAppliesToIssuanceState,
    bool AppliesToFlatCancellations,
    decimal? PremiumMinThreshold,
    decimal? PremiumMaxThreshold,
    string? PremiumThresholdBasis,
    int? StateCountMin,
    int? StateCountMax,
    string RoundingMode,
    bool ExcludeWhenNotFiling,
    bool ExcludeOnEndorsements,
    bool ExcludeOnRenewal,
    bool ExcludeOnOriginalBinder,
    bool ExcludeOnMultiCarrierPolicy,
    bool PayHomeState,
    string? ExcludedPolicyTransactionTypes,
    string PayableRouting,
    long? PayablePayeeId,
    bool MasterPayeeWhenHomeState,
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
    Guid? ProgramConfigurationId,
    Guid? CarrierId,
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
    bool ApplyOnlyOnce,
    bool MandatoryCharge,
    bool ApplyAutomatically,
    bool ApplyWhenPackagePolicyOnly,
    bool DoNotApplyWhenPackagePolicyOnly,
    bool ApplyToChildLines,
    bool OnlyAppliesToIssuanceState,
    bool AppliesToFlatCancellations,
    decimal? PremiumMinThreshold,
    decimal? PremiumMaxThreshold,
    string? PremiumThresholdBasis,
    int? StateCountMin,
    int? StateCountMax,
    string RoundingMode,
    bool ExcludeWhenNotFiling,
    bool ExcludeOnEndorsements,
    bool ExcludeOnRenewal,
    bool ExcludeOnOriginalBinder,
    bool ExcludeOnMultiCarrierPolicy,
    bool PayHomeState,
    string? ExcludedPolicyTransactionTypes,
    string PayableRouting,
    long? PayablePayeeId,
    bool MasterPayeeWhenHomeState,
    string? Notes,
    IReadOnlyList<FeePremiumBracketDto> PremiumBrackets
);

public record SetStateTaxabilityRequest(IReadOnlyList<string> NonTaxableStateCodes);
