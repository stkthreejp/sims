using SIMS.Domain.Entities;

namespace SIMS.Domain.Entities.Accounting;

public class FeeRuleVersion
{
    public long Id { get; set; }
    public long FeeDefinitionId { get; set; }

    // Scope dimensions (null = wildcard)
    public Guid? ProgramConfigurationId { get; set; }
    public Guid? CarrierId { get; set; }
    public int? CompanyId { get; set; }
    public int? ProducerId { get; set; }
    public string? LineOfBusiness { get; set; }
    public string? StateCode { get; set; }
    public string? City { get; set; }
    public string? LicenseType { get; set; }  // 'Admitted'|'Non-Admitted'

    // Effective dating
    public DateOnly EffectiveDate { get; set; }
    public DateOnly? DisabledDate { get; set; }

    // Calculation type
    public string CalcType { get; set; } = string.Empty;  // 'Flat'|'Percent'|'Stratified'
    public decimal? FlatAmount { get; set; }
    public decimal? PercentRate { get; set; }
    public bool PercentOfNet { get; set; } = false;
    public decimal? MinimumAmount { get; set; }

    // Maximums
    public decimal? MaxPercent { get; set; }
    public decimal? MaxAmount { get; set; }

    // Behavior flags
    public bool Commissionable { get; set; } = false;
    public int? OfficeId { get; set; }
    public string InstallmentBehavior { get; set; } = "PerInstallment";  // 'PerInstallment'|'DownpaymentOnly'
    public bool SplitByParticipation { get; set; } = false;
    public bool FullyEarned { get; set; } = false;
    public int? FullyEarnedDays { get; set; }
    public bool ExcludeTerrorism { get; set; } = false;
    public bool MultiplyByLocations { get; set; } = false;
    public bool MultiplyByVehicles { get; set; } = false;
    public bool SendToAccounting { get; set; } = true;
    public bool ApplyOnlyOnce { get; set; } = false;
    public bool MandatoryCharge { get; set; } = false;

    // Auto Apply
    public bool ApplyAutomatically { get; set; } = true;
    public bool ApplyWhenPackagePolicyOnly { get; set; } = false;
    public bool DoNotApplyWhenPackagePolicyOnly { get; set; } = false;
    public bool ApplyToChildLines { get; set; } = false;
    public bool OnlyAppliesToIssuanceState { get; set; } = false;
    public bool AppliesToFlatCancellations { get; set; } = false;
    public decimal? PremiumMinThreshold { get; set; }
    public decimal? PremiumMaxThreshold { get; set; }
    public string? PremiumThresholdBasis { get; set; }  // 'ByLine'|'ByPolicy'
    public int? StateCountMin { get; set; }
    public int? StateCountMax { get; set; }
    public string RoundingMode { get; set; } = "NearestCent";

    // Exclusions
    public bool ExcludeWhenNotFiling { get; set; } = false;
    public bool ExcludeOnEndorsements { get; set; } = false;
    public bool ExcludeOnRenewal { get; set; } = false;
    public bool ExcludeOnOriginalBinder { get; set; } = false;
    public bool ExcludeOnMultiCarrierPolicy { get; set; } = false;
    public bool PayHomeState { get; set; } = false;
    public string? ExcludedPolicyTransactionTypes { get; set; }

    // Payable routing
    public string PayableRouting { get; set; } = "NotPayable";  // 'NotPayable'|'Company'|'Entity'
    public long? PayablePayeeId { get; set; }
    public bool MasterPayeeWhenHomeState { get; set; } = false;

    // Audit
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? LastEditedBy { get; set; }
    public DateTime? LastEditedAt { get; set; }
    public string? Notes { get; set; }

    public FeeDefinition FeeDefinition { get; set; } = null!;
    public ProgramConfiguration? ProgramConfiguration { get; set; }
    public Carrier? Carrier { get; set; }
    public Payee? PayablePayee { get; set; }
    public ICollection<FeePremiumBracket> PremiumBrackets { get; set; } = new List<FeePremiumBracket>();
    public ICollection<FeeAuditLog> AuditLogs { get; set; } = new List<FeeAuditLog>();
}
