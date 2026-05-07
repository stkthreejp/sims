using SIMS.Domain.Entities;

namespace SIMS.Domain.Entities.Fmcsa;

public class FmcsaViolation : BaseEntity
{
    public Guid FmcsaInspectionId { get; set; }
    public string UsDotNumber { get; set; } = string.Empty;
    public string ReportNumber { get; set; } = string.Empty;
    public string ViolationCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Basic { get; set; }
    public string? ViolationGroup { get; set; }
    public bool IsOutOfService { get; set; }
    public bool IsDriverDisqualifying { get; set; }
    public int SeverityWeight { get; set; } = 1;
    public decimal TimeWeight { get; set; } = 1m;
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;

    public FmcsaInspection Inspection { get; set; } = null!;
}
