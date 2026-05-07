using SIMS.Domain.Entities;

namespace SIMS.Domain.Entities.Fmcsa;

public class FmcsaCarrierSnapshot : BaseEntity
{
    public string UsDotNumber { get; set; } = string.Empty;
    public string SnapshotMonth { get; set; } = string.Empty;
    public string LegalName { get; set; } = string.Empty;
    public string? DbaName { get; set; }
    public string? PhysicalAddress { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public int? PowerUnits { get; set; }
    public int? DriverCount { get; set; }
    public int? Mileage { get; set; }
    public int? MileageYear { get; set; }
    public string? OperationClassification { get; set; }
    public string? CarrierOperation { get; set; }
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
}
