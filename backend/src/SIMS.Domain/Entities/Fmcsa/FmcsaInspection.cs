using SIMS.Domain.Entities;

namespace SIMS.Domain.Entities.Fmcsa;

public class FmcsaInspection : BaseEntity
{
    public string UsDotNumber { get; set; } = string.Empty;
    public string ReportNumber { get; set; } = string.Empty;
    public DateOnly InspectionDate { get; set; }
    public string? State { get; set; }
    public int? InspectionLevel { get; set; }
    public bool DriverOutOfService { get; set; }
    public bool VehicleOutOfService { get; set; }
    public bool HazmatOutOfService { get; set; }
    public int DriverViolationCount { get; set; }
    public int VehicleViolationCount { get; set; }
    public int HazmatViolationCount { get; set; }
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;

    public ICollection<FmcsaViolation> Violations { get; set; } = new List<FmcsaViolation>();
}
