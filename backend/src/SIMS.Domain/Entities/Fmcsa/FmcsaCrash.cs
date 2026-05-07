using SIMS.Domain.Entities;

namespace SIMS.Domain.Entities.Fmcsa;

public class FmcsaCrash : BaseEntity
{
    public string UsDotNumber { get; set; } = string.Empty;
    public string ReportNumber { get; set; } = string.Empty;
    public DateOnly CrashDate { get; set; }
    public string? State { get; set; }
    public bool TowAway { get; set; }
    public bool Injury { get; set; }
    public bool Fatality { get; set; }
    public decimal SeverityWeight { get; set; } = 1m;
    public decimal TimeWeight { get; set; } = 1m;
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
}
