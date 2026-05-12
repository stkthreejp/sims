namespace SIMS.Domain.Entities;

public class LegiScanTrackedBill : BaseEntity
{
    public int BillId { get; set; }
    public string State { get; set; } = string.Empty;
    public string BillNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ChangeHash { get; set; }
    public int? Status { get; set; }
    public DateOnly? StatusDate { get; set; }
    public string? Url { get; set; }
    public string? Stance { get; set; } = "watch";
    public bool IsActive { get; set; } = true;
    public DateTime? LastSyncedAt { get; set; }
    public string? RawBillJson { get; set; }
}
