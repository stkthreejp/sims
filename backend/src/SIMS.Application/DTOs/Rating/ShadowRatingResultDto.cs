namespace SIMS.Application.DTOs.Rating;

public class ShadowRatingResultDto
{
    public Guid Id { get; set; }
    public Guid QuoteId { get; set; }
    public string QuoteNumber { get; set; } = "";
    public string InsuredName { get; set; } = "";
    public Guid RatingPlanVersionId { get; set; }
    public string PlanName { get; set; } = "";
    public int VersionNumber { get; set; }
    public DateTime RatedAt { get; set; }
    public Guid RatedById { get; set; }
    public string RatedByName { get; set; } = "";
    public decimal ShadowPremium { get; set; }
    public decimal ActualPremium { get; set; }
    public decimal DeltaAmount { get; set; }
    public decimal DeltaPct { get; set; }
    public bool IsOutlier { get; set; }
    public decimal ScheduleModifier { get; set; }
}
