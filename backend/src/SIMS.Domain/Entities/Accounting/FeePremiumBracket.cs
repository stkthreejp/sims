namespace SIMS.Domain.Entities.Accounting;

public class FeePremiumBracket
{
    public long Id { get; set; }
    public long FeeRuleVersionId { get; set; }
    public decimal TierFrom { get; set; }
    public decimal? TierTo { get; set; }  // null = infinity
    public decimal PercentRate { get; set; }

    public FeeRuleVersion FeeRuleVersion { get; set; } = null!;
}
