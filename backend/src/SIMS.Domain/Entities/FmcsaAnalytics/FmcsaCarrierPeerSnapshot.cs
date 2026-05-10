using SIMS.Domain.Entities;

namespace SIMS.Domain.Entities.FmcsaAnalytics;

public class FmcsaCarrierPeerSnapshot : BaseEntity
{
    public string SnapshotMonth { get; set; } = string.Empty;
    public string UsDotNumber { get; set; } = string.Empty;
    public string? LegalName { get; set; }
    public string? State { get; set; }
    public int? PowerUnits { get; set; }
    public int? DriverCount { get; set; }
    public int? Mileage { get; set; }
    public int? MileageYear { get; set; }
    public int InspectionCount { get; set; }
    public int DriverInspectionCount { get; set; }
    public int VehicleInspectionCount { get; set; }
    public int DriverOosInspectionCount { get; set; }
    public int VehicleOosInspectionCount { get; set; }
}
