namespace SIMS.Application.DTOs.Quotes;

public class RatingResultDto
{
    public Guid SnapshotId { get; set; }
    public Guid? PolicyTransactionId { get; set; }
    public decimal ManualPremium { get; set; }
    public decimal ScheduleModifier { get; set; }
    public string? ScheduleModifierReason { get; set; }
    public bool DebrisRemoval { get; set; }
    public bool RentalReimbursement { get; set; }
    public bool TowingStorageRecovery { get; set; }
    public bool NewlyAcquiredEquipment { get; set; }
    public decimal EndorsementPremium { get; set; }
    public decimal GrandTotalPremium { get; set; }
    public DateTime RatedAt { get; set; }
    public Guid RatedById { get; set; }
    public string? RatedByName { get; set; }
    public bool IsBoundSnapshot { get; set; }
    // Plan bounds (so the UI can render the schedule modifier slider/min/max)
    public decimal ScheduleMin { get; set; }
    public decimal ScheduleMax { get; set; }
    public decimal? MinimumPremium { get; set; }
    public List<RatingLineDto> Lines { get; set; } = new();
}

public class RatingLineDto
{
    public string ExposureRef { get; set; } = string.Empty;
    public decimal LinePremium { get; set; }
    public string Inputs { get; set; } = "{}";
    public string FactorsApplied { get; set; } = "{}";
}
