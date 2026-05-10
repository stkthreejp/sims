using SIMS.Domain.Entities;

namespace SIMS.Domain.Entities.FmcsaAnalytics;

public class FmcsaAnalyticsImportBatch : BaseEntity
{
    public string SnapshotMonth { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public int RowsImported { get; set; }
    public string? ErrorMessage { get; set; }
}
