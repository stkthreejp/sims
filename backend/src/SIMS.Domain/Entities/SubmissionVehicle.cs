using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities;

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

    // APD rating inputs
    public int? ApdVehicleClass { get; set; }     // 1=Light/Med, 2=Heavy/XHeavy, 3=TT, 4=Trailer
    public int? ApdRoadType { get; set; }          // 1=State Hwy Rural, 2=Surface Rural, 3=State Hwy Sub, 4=Surface Sub, 5=Off Road
    public int? ApdAnnualMiles { get; set; }       // Actual miles/year → maps to mileage class 10/11/12/13/20
    public int? ApdOperationCode { get; set; }     // 91=Logging, 92=Chips, 99=For Hire
    public string? ApdState { get; set; }          // 2-letter state code
    public decimal? ApdStatedValue { get; set; }
    public decimal? ApdCompDeductible { get; set; }
    public decimal? ApdCollDeductible { get; set; }
    public int? ApdDriverAgeCode { get; set; }     // 0=<21, 1=21-24, 2=25-29, 3=30-39, 4=40-49, 5=50-65, 6=66-72, 7=>72, 8=Non-Fleet Unassigned
    public int? ApdDriverPointsCode { get; set; }  // 0=0-1pts, 1=2pts, 2=3pts, 3=4pts, 4=4+pts, 5=Fleet Unassigned
    public decimal? ApdDriverExpMod { get; set; }  // 1.0=Standard, 1.15=<2yrs exp, 1.25=No exp

    public Submission Submission { get; set; } = null!;
}
