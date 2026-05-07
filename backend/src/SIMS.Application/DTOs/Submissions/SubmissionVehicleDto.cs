using SIMS.Domain.Enums;

namespace SIMS.Application.DTOs.Submissions;

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

    // APD rating inputs
    public int? ApdVehicleClass { get; set; }
    public int? ApdRoadType { get; set; }
    public int? ApdAnnualMiles { get; set; }
    public int? ApdOperationCode { get; set; }
    public string? ApdState { get; set; }
    public decimal? ApdStatedValue { get; set; }
    public decimal? ApdCompDeductible { get; set; }
    public decimal? ApdCollDeductible { get; set; }
    public int? ApdDriverAgeCode { get; set; }
    public int? ApdDriverPointsCode { get; set; }
    public decimal? ApdDriverExpMod { get; set; }
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

    // APD rating inputs
    public int? ApdVehicleClass { get; set; }
    public int? ApdRoadType { get; set; }
    public int? ApdAnnualMiles { get; set; }
    public int? ApdOperationCode { get; set; }
    public string? ApdState { get; set; }
    public decimal? ApdStatedValue { get; set; }
    public decimal? ApdCompDeductible { get; set; }
    public decimal? ApdCollDeductible { get; set; }
    public int? ApdDriverAgeCode { get; set; }
    public int? ApdDriverPointsCode { get; set; }
    public decimal? ApdDriverExpMod { get; set; }
}

public class SubmissionVehicleUpdateDto : SubmissionVehicleCreateDto { }
