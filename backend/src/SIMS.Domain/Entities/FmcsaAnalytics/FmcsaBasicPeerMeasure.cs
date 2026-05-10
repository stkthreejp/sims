using SIMS.Domain.Entities;

namespace SIMS.Domain.Entities.FmcsaAnalytics;

public class FmcsaBasicPeerMeasure : BaseEntity
{
    public string SnapshotMonth { get; set; } = string.Empty;
    public string UsDotNumber { get; set; } = string.Empty;
    public string Basic { get; set; } = string.Empty;
    public decimal? OfficialMeasure { get; set; }
    public decimal? SimsMeasure { get; set; }
    public int InspectionWithViolationCount { get; set; }
    public int ViolationCount { get; set; }
    public int OutOfServiceCount { get; set; }
    public decimal WeightedViolationScore { get; set; }
    public decimal Exposure { get; set; }
    public string PeerGroupKey { get; set; } = string.Empty;
    public int? PeerRank { get; set; }
    public int? PeerPopulation { get; set; }
    public decimal? SimsPercentile { get; set; }
}
