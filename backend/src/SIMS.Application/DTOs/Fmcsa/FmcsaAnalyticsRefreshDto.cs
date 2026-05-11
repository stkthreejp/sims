namespace SIMS.Application.DTOs.Fmcsa;

public class FmcsaAnalyticsRefreshDto
{
    public string SnapshotMonth { get; set; } = string.Empty;
    public int CarrierCount { get; set; }
    public int BasicMeasureCount { get; set; }
    public DateTime RefreshedAt { get; set; } = DateTime.UtcNow;
}

public class FmcsaAnalyticsStatusDto
{
    public bool IsConfigured { get; set; }
    public int CarrierPeerSnapshotCount { get; set; }
    public int BasicPeerMeasureCount { get; set; }
    public bool HasRunningImport { get; set; }
    public List<FmcsaScheduledJobDto> ScheduledJobs { get; set; } = new();
    public List<FmcsaAnalyticsImportBatchDto> LatestBatches { get; set; } = new();
}

public class FmcsaScheduledJobDto
{
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string Schedule { get; set; } = string.Empty;
    public DateTime? NextRunAtUtc { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class FmcsaAnalyticsImportBatchDto
{
    public string SnapshotMonth { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int RowsImported { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
