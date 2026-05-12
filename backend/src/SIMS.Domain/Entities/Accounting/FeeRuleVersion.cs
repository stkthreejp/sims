namespace SIMS.Domain.Entities.Accounting;

public class FeeRuleVersion
{
    public long Id { get; set; }
    public long FeeDefinitionId { get; set; }

    // Scope dimensions (null = wildcard)
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

    // Auto Apply
    public bool ApplyAutomatically { get; set; } = true;
    public decimal? PremiumMinThreshold { get; set; }
    public decimal? PremiumMaxThreshold { get; set; }
    public string? PremiumThresholdBasis { get; set; }  // 'ByLine'|'ByPolicy'
    public string RoundingMode { get; set; } = "NearestCent";

    // Exclusions
    public bool ExcludeWhenNotFiling { get; set; } = false;
    public bool ExcludeOnEndorsements { get; set; } = false;

    // Payable routing
    public string PayableRouting { get; set; } = "NotPayable";  // 'NotPayable'|'Company'|'Entity'
    public long? PayablePayeeId { get; set; }

    // Audit
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? LastEditedBy { get; set; }
    public DateTime? LastEditedAt { get; set; }
    public string? Notes { get; set; }

    public FeeDefinition FeeDefinition { get; set; } = null!;
    public Carrier? Carrier { get; set; }
    public Payee? PayablePayee { get; set; }
    public ICollection<FeePremiumBracket> PremiumBrackets { get; set; } = new List<FeePremiumBracket>();
    public ICollection<FeeAuditLog> AuditLogs { get; set; } = new List<FeeAuditLog>();
}
