namespace SIMS.Domain.Entities.Rating;

public class RatingPlanVersionImpactPreview : BaseEntity
{
    public Guid RatingPlanVersionId { get; set; }
    public DateTime ComputedAt { get; set; }
    public Guid ComputedById { get; set; }

    public int QuoteCount { get; set; }
    public decimal TotalCurrentPremium { get; set; }
    public decimal TotalNewPremium { get; set; }
    public decimal TotalDeltaPct { get; set; }
    public int QuotesUp { get; set; }
    public int QuotesDown { get; set; }
    public int QuotesFlat { get; set; }

    public string PreviewJson { get; set; } = "{}";

    public RatingPlanVersion RatingPlanVersion { get; set; } = null!;
    public User ComputedBy { get; set; } = null!;
}
