namespace SIMS.Domain.Entities;

public class LegalSourceScanRun : BaseEntity
{
    public string SourceName { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public int ResultsFound { get; set; }
    public int PossibleChanges { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? StartedById { get; set; }
    public string? StartedByName { get; set; }

    public User? StartedBy { get; set; }
    public ICollection<LegalSourceScanResult> Results { get; set; } = [];
}
