namespace SIMS.Domain.Entities;

public class CarrierCommission
{
    public long Id { get; set; }
    public Guid CarrierId { get; set; }
    public string? LineOfBusiness { get; set; }  // null = applies to all LOBs (fallback)
    public decimal CommissionRate { get; set; }    // e.g. 0.125 = 12.5% — total commission from carrier
    public decimal SMMRetentionRate { get; set; }  // portion SMM keeps (e.g. 0.05 = 5%)
    public DateOnly EffectiveDate { get; set; }
    public DateOnly? DisabledDate { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Carrier Carrier { get; set; } = null!;
}
