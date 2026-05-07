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
    public List<string> SummaryFlags { get; set; } = new();
    public List<AutoSafetyBasicDto> Basics { get; set; } = new();
    public AutoSafetyOosDto Oos { get; set; } = new();
    public List<AutoSafetyHotspotDto> GeographicHotspots { get; set; } = new();
    public List<AutoSafetyEventDto> RecentSevereEvents { get; set; } = new();
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
    public string TrendDirection { get; set; } = "Flat";
}

public class AutoSafetyOosDto
{
    public int InspectionCount { get; set; }
    public int DriverOosCount { get; set; }
    public int VehicleOosCount { get; set; }
    public decimal? DriverOosRate { get; set; }
    public decimal? VehicleOosRate { get; set; }
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
