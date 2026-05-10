using SIMS.Domain.Entities;

namespace SIMS.Domain.Entities.Fmcsa;

public class FmcsaInspection : BaseEntity
{
    public string UsDotNumber { get; set; } = string.Empty;
    public string ReportNumber { get; set; } = string.Empty;
    public DateOnly InspectionDate { get; set; }
    public string? State { get; set; }
    public string? CountyCodeState { get; set; }
    public string? InspectionCounty { get; set; }
    public string? InspectionLocation { get; set; }
    public string? InspectionFacility { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public bool? PostCrash { get; set; }
    public bool? HazmatPlacardRequired { get; set; }
    public string? InspectionLevelDescription { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? GeocodePrecision { get; set; }
    public string? DetailSourceUrl { get; set; }
    public DateTime? DetailEnrichedAt { get; set; }
    public int? InspectionLevel { get; set; }
    public bool DriverOutOfService { get; set; }
    public bool VehicleOutOfService { get; set; }
    public bool HazmatOutOfService { get; set; }
    public int DriverViolationCount { get; set; }
    public int VehicleViolationCount { get; set; }
    public int HazmatViolationCount { get; set; }
    public string? UnitType { get; set; }
    public string? UnitMake { get; set; }
    public string? UnitLicense { get; set; }
    public string? UnitLicenseState { get; set; }
    public string? Vin { get; set; }
    public string? UnitType2 { get; set; }
    public string? UnitMake2 { get; set; }
    public string? UnitLicense2 { get; set; }
    public string? UnitLicenseState2 { get; set; }
    public string? Vin2 { get; set; }
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;

    public ICollection<FmcsaViolation> Violations { get; set; } = new List<FmcsaViolation>();
}
