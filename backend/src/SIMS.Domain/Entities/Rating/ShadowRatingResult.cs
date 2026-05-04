namespace SIMS.Domain.Entities.Rating;

public class ShadowRatingResult : BaseEntity
{
    public Guid QuoteId { get; set; }
    public Guid RatingPlanVersionId { get; set; }
    public DateTime RatedAt { get; set; }
    public Guid RatedById { get; set; }

    public decimal ShadowPremium { get; set; }
    public decimal ActualPremium { get; set; }
    public decimal DeltaAmount { get; set; }
    public decimal DeltaPct { get; set; }
    public decimal ScheduleModifier { get; set; } = 1.0m;
    public string SnapshotJson { get; set; } = "{}";

    public Quote Quote { get; set; } = null!;
    public RatingPlanVersion RatingPlanVersion { get; set; } = null!;
    public User RatedBy { get; set; } = null!;
}
