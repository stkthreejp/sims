namespace SIMS.Application.DTOs.Quotes;

public class RateQuoteRequest
{
    public decimal ScheduleModifier { get; set; } = 1.0m;
    public string? ScheduleModifierReason { get; set; }
    public bool? DebrisRemoval { get; set; }
    public bool? RentalReimbursement { get; set; }
    public bool? TowingStorageRecovery { get; set; }
    public bool? NewlyAcquiredEquipment { get; set; }
}
