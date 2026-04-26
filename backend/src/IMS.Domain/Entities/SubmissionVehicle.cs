using IMS.Domain.Enums;

namespace IMS.Domain.Entities;

public class SubmissionVehicle : BaseEntity
{
    public Guid SubmissionId { get; set; }
    public int UnitNumber { get; set; }
    public int? Year { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public string? Vin { get; set; }
    public int? Gvw { get; set; }
    public VehicleClass VehicleClass { get; set; } = VehicleClass.Unknown;
    public string? GaragingZip { get; set; }
    public OperatingRadius? Radius { get; set; }

    public Submission Submission { get; set; } = null!;
}
