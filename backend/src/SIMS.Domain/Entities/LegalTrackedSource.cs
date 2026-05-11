namespace SIMS.Domain.Entities;

public class LegalTrackedSource : BaseEntity
{
    public string State { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string? Url { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string ScanCadence { get; set; } = "Manual";
    public DateTime? LastCheckedAt { get; set; }
    public DateTime? LastChangedAt { get; set; }
    public string LastStatus { get; set; } = "NotChecked";
    public string? LastErrorMessage { get; set; }
    public string? Notes { get; set; }
}
