using SIMS.Domain.Entities.Rating;
using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities;

public class PolicyVersion : BaseEntity
{
    public Guid PolicyId { get; set; }
    public int VersionNumber { get; set; }
    public Guid? CreatedByPolicyTransactionId { get; set; }
    public Guid? PriorPolicyVersionId { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public DateOnly ExpirationDate { get; set; }
    public PolicyStatus Status { get; set; }
    public decimal PremiumAmount { get; set; }
    public decimal TaxesAndFees { get; set; }
    public decimal TotalPremium { get; set; }
    public string CoverageSnapshotJson { get; set; } = "{}";
    public string ExposureSnapshotJson { get; set; } = "{}";
    public Guid? RatingSnapshotId { get; set; }
    public Guid CreatedById { get; set; }

    public Policy Policy { get; set; } = null!;
    public PolicyTransaction? CreatedByPolicyTransaction { get; set; }
    public PolicyVersion? PriorPolicyVersion { get; set; }
    public QuoteRatingSnapshot? RatingSnapshot { get; set; }
    public User CreatedBy { get; set; } = null!;
}
