using IMS.Domain.Enums;

namespace IMS.Application.DTOs.Submissions;

public class SubmissionVehicleDto
{
    public Guid Id { get; set; }
    public Guid SubmissionId { get; set; }
    public int UnitNumber { get; set; }
    public int? Year { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public string? Vin { get; set; }
    public int? Gvw { get; set; }
    public VehicleClass VehicleClass { get; set; }
    public string? GaragingZip { get; set; }
    public OperatingRadius? Radius { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SubmissionVehicleCreateDto
{
    public int UnitNumber { get; set; }
    public int? Year { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public string? Vin { get; set; }
    public int? Gvw { get; set; }
    public VehicleClass VehicleClass { get; set; }
    public string? GaragingZip { get; set; }
    public OperatingRadius? Radius { get; set; }
}

public class SubmissionVehicleUpdateDto : SubmissionVehicleCreateDto { }
