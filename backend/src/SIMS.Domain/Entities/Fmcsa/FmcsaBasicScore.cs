using SIMS.Domain.Entities;

namespace SIMS.Domain.Entities.Fmcsa;

public class FmcsaBasicScore : BaseEntity
{
    public Guid FmcsaScoringRunId { get; set; }
    public string Basic { get; set; } = string.Empty;
    public decimal? Measure { get; set; }
    public decimal? Percentile { get; set; }
    public bool IsPrioritized { get; set; }
    public int EventCount { get; set; }
    public int OutOfServiceCount { get; set; }
    public string TrendDirection { get; set; } = "Flat";

    public FmcsaScoringRun ScoringRun { get; set; } = null!;
}
