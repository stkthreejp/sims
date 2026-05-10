namespace SIMS.Domain.Entities;

public class LegalRequirementChangeLog : BaseEntity
{
    public Guid RequirementSectionId { get; set; }
    public Guid? ScanResultId { get; set; }
    public string ChangeType { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Comment { get; set; }
    public Guid? ChangedById { get; set; }
    public string ChangedByName { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    public LegalRequirementSection RequirementSection { get; set; } = null!;
    public LegalSourceScanResult? ScanResult { get; set; }
    public User? ChangedBy { get; set; }
}
