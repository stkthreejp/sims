namespace SIMS.Domain.Entities.Rating;

public class QuoteRatingLine : BaseEntity
{
    public Guid QuoteRatingSnapshotId { get; set; }
    public string ExposureRef { get; set; } = string.Empty;
    public string Inputs { get; set; } = "{}";
    public string FactorsApplied { get; set; } = "{}";
    public decimal LinePremium { get; set; }

    public QuoteRatingSnapshot Snapshot { get; set; } = null!;
}
