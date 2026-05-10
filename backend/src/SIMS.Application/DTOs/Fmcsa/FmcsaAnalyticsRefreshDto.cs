namespace SIMS.Application.DTOs.Fmcsa;

public class FmcsaAnalyticsRefreshDto
{
    public string SnapshotMonth { get; set; } = string.Empty;
    public int CarrierCount { get; set; }
    public int BasicMeasureCount { get; set; }
    public DateTime RefreshedAt { get; set; } = DateTime.UtcNow;
}
