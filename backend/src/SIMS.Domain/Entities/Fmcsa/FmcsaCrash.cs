using SIMS.Domain.Entities;

namespace SIMS.Domain.Entities.Fmcsa;

public class FmcsaCrash : BaseEntity
{
    public string UsDotNumber { get; set; } = string.Empty;
    public string ReportNumber { get; set; } = string.Empty;
    public DateOnly CrashDate { get; set; }
    public string? State { get; set; }
    public string? City { get; set; }
    public string? CountyCode { get; set; }
    public string? Location { get; set; }
    public string? Agency { get; set; }
    public int? VehiclesInAccident { get; set; }
    public string? WeatherConditionId { get; set; }
    public string? RoadSurfaceConditionId { get; set; }
    public string? TrafficwayId { get; set; }
    public string? LightConditionId { get; set; }
    public string? VehicleConfigurationId { get; set; }
    public string? CargoBodyTypeId { get; set; }
    public string? GvwRatingId { get; set; }
    public string? VehicleIdentificationNumber { get; set; }
    public string? VehicleLicenseNumber { get; set; }
    public string? VehicleLicenseState { get; set; }
    public bool HazmatPlacard { get; set; }
    public bool HazmatReleased { get; set; }
    public bool TowAway { get; set; }
    public bool Injury { get; set; }
    public bool Fatality { get; set; }
    public decimal SeverityWeight { get; set; } = 1m;
    public decimal TimeWeight { get; set; } = 1m;
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
}
