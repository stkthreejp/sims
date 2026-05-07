using SIMS.Domain.Entities;

namespace SIMS.Domain.Entities.Fmcsa;

public class FmcsaScoringRun : BaseEntity
{
    public string UsDotNumber { get; set; } = string.Empty;
    public string SnapshotMonth { get; set; } = string.Empty;
    public string MethodologyVersion { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    public ICollection<FmcsaBasicScore> BasicScores { get; set; } = new List<FmcsaBasicScore>();
}
