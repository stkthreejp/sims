namespace SIMS.Application.DTOs.Quotes;

public class AutoSafetySummaryDto
{
    public string Status { get; set; } = "Ready";
    public string? Message { get; set; }
    public string? UsDotNumber { get; set; }
    public string? CarrierName { get; set; }
    public string? SnapshotMonth { get; set; }
    public string? MethodologyVersion { get; set; }
    public string OverallRiskLevel { get; set; } = "Unknown";
    public int? PowerUnits { get; set; }
    public int? DriverCount { get; set; }
    public DateTime? DataRefreshedAt { get; set; }
    public AutoSafetyIssDto Iss { get; set; } = new();
    public List<string> SummaryFlags { get; set; } = new();
    public List<AutoSafetyBasicDto> Basics { get; set; } = new();
    public AutoSafetyOosDto Oos { get; set; } = new();
    public AutoSafetyAccidentSummaryDto AccidentSummary { get; set; } = new();
    public List<AutoSafetyHotspotDto> GeographicHotspots { get; set; } = new();
    public List<AutoSafetyEventDto> RecentSevereEvents { get; set; } = new();
    public List<AutoSafetyTrendBucketDto> InspectionTrend { get; set; } = new();
    public List<AutoSafetyTrendBucketDto> ViolationTrend { get; set; } = new();
}

public class AutoSafetyRefreshDto
{
    public AutoSafetySummaryDto Summary { get; set; } = new();
    public int CarrierRowsImported { get; set; }
    public int InspectionRowsImported { get; set; }
    public int ViolationRowsImported { get; set; }
    public int CrashRowsImported { get; set; }
    public DateTime RefreshedAt { get; set; } = DateTime.UtcNow;
}

public class AutoSafetyBasicDto
{
    public string Basic { get; set; } = string.Empty;
    public decimal? Measure { get; set; }
    public decimal? Percentile { get; set; }
    public bool IsPrioritized { get; set; }
    public int EventCount { get; set; }
    public int OutOfServiceCount { get; set; }
    public int RecentEventCount { get; set; }
    public int RecentOutOfServiceCount { get; set; }
    public string TrendDirection { get; set; } = "Flat";
    public string ScoreSource { get; set; } = "SIMS signal";
}

public class AutoSafetyIssDto
{
    public int? Score { get; set; }
    public string Status { get; set; } = "Unknown";
    public string? Label { get; set; }
    public string Basis { get; set; } = "Pending";
    public string? Explanation { get; set; }
    public string Source { get; set; } = "Pending ISS source";
}

public class AutoSafetyOosDto
{
    public int InspectionCount { get; set; }
    public int OverallOosCount { get; set; }
    public decimal? OverallOosRate { get; set; }
    public int DriverInspectionCount { get; set; }
    public int DriverOosCount { get; set; }
    public int VehicleInspectionCount { get; set; }
    public int VehicleOosCount { get; set; }
    public int HazmatInspectionCount { get; set; }
    public int HazmatOosCount { get; set; }
    public decimal? DriverOosRate { get; set; }
    public decimal? VehicleOosRate { get; set; }
    public decimal? HazmatOosRate { get; set; }
    public decimal? OverallNationalAverageRate { get; set; }
    public decimal? DriverNationalAverageRate { get; set; }
    public decimal? VehicleNationalAverageRate { get; set; }
    public decimal? HazmatNationalAverageRate { get; set; }
}

public class AutoSafetyAccidentSummaryDto
{
    public int FatalCount { get; set; }
    public int InjuryCount { get; set; }
    public int TowCount { get; set; }
    public int TotalReportableCount { get; set; }
    public decimal? AccidentToPowerUnitRatio { get; set; }
}

public class AutoSafetyHotspotDto
{
    public string State { get; set; } = string.Empty;
    public int InspectionCount { get; set; }
    public int ViolationCount { get; set; }
    public int OutOfServiceCount { get; set; }
}

public class AutoSafetyEventDto
{
    public DateOnly Date { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? State { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Basic { get; set; }
    public int SeverityWeight { get; set; }
}

public class AutoSafetyDetailDto
{
    public string Category { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string ReportNumber { get; set; } = string.Empty;
    public string? State { get; set; }
    public string? City { get; set; }
    public string? CountyCode { get; set; }
    public string? Location { get; set; }
    public string? Agency { get; set; }
    public string? Conditions { get; set; }
    public string? VehicleInfo { get; set; }
    public string? CrashEvents { get; set; }
    public string? Basic { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsOutOfService { get; set; }
    public bool IsFatal { get; set; }
    public bool IsInjury { get; set; }
    public bool IsTow { get; set; }
    public string Source { get; set; } = "FMCSA/Socrata";
}

public class AutoSafetyTrendBucketDto
{
    public string Label { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public int OutOfServiceCount { get; set; }
    public decimal? OutOfServiceRate { get; set; }
}
