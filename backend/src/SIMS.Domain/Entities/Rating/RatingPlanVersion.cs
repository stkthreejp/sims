using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities.Rating;

public class RatingPlanVersion : BaseEntity
{
    public Guid RatingPlanId { get; set; }
    public int VersionNumber { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public DateOnly? ExpirationDate { get; set; }
    public PlanStatus Status { get; set; } = PlanStatus.Draft;
    public DateTime? PromotedAt { get; set; }
    public Guid? PromotedById { get; set; }
    public Guid? CreatedById { get; set; }
    public Guid? LastEditedById { get; set; }
    public string? Notes { get; set; }
    public decimal ScheduleMin { get; set; } = 0.50m;
    public decimal ScheduleMax { get; set; } = 1.50m;
    public decimal? MinimumPremium { get; set; }

    public RatingPlan RatingPlan { get; set; } = null!;
    public User? PromotedBy { get; set; }
    public User? CreatedBy { get; set; }
    public User? LastEditedBy { get; set; }
    public ICollection<FactorTable> FactorTables { get; set; } = new List<FactorTable>();
    public ICollection<EligibilityRule> EligibilityRules { get; set; } = new List<EligibilityRule>();
}
