namespace SIMS.Application.DTOs.Quotes;

public class RatingResultDto
{
    public Guid SnapshotId { get; set; }
    public decimal ManualPremium { get; set; }
    public decimal ScheduleModifier { get; set; }
    public decimal GrandTotalPremium { get; set; }
    public List<RatingLineDto> Lines { get; set; } = new();
}

public class RatingLineDto
{
    public string ExposureRef { get; set; } = string.Empty;
    public decimal LinePremium { get; set; }
    public string FactorsApplied { get; set; } = "{}";
}
