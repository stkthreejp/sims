namespace SIMS.Application.DTOs.Quotes;

public class RateQuoteRequest
{
    public decimal ScheduleModifier { get; set; } = 1.0m;
    public string? ScheduleModifierReason { get; set; }
}
