using SIMS.Application.Common;
using SIMS.Application.DTOs.Quotes;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Entities.Fmcsa;
using SIMS.Domain.Entities.FmcsaAnalytics;
using SIMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace SIMS.Infrastructure.Services;

public class FmcsaSafetyService : IFmcsaSafetyService
{
    private const string CurrentMethodologyVersion = "SMS 3.20";
    private static readonly string[] Basics =
    [
        "Unsafe Driving",
        "Crash Indicator",
        "Hours-of-Service Compliance",
        "Vehicle Maintenance",
        "Controlled Substances/Alcohol",
        "Hazardous Materials Compliance",
        "Driver Fitness"
    ];
    private static readonly (string Label, int FromMonthsAgo, int ToMonthsAgo)[] TrendBuckets =
    [
        ("36-30", 30, 36),
        ("30-24", 24, 30),
        ("24-18", 18, 24),
        ("18-12", 12, 18),
        ("12-6", 6, 12),
        ("6-0", 0, 6),
    ];
    private static readonly Dictionary<string, (double Latitude, double Longitude)> StateCentroids = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AL"] = (32.8067, -86.7911), ["AK"] = (61.3707, -152.4044), ["AZ"] = (33.7298, -111.4312), ["AR"] = (34.9697, -92.3731),
        ["CA"] = (36.1162, -119.6816), ["CO"] = (39.0598, -105.3111), ["CT"] = (41.5978, -72.7554), ["DE"] = (39.3185, -75.5071),
        ["FL"] = (27.7663, -81.6868), ["GA"] = (33.0406, -83.6431), ["HI"] = (21.0943, -157.4983), ["ID"] = (44.2405, -114.4788),
        ["IL"] = (40.3495, -88.9861), ["IN"] = (39.8494, -86.2583), ["IA"] = (42.0115, -93.2105), ["KS"] = (38.5266, -96.7265),
        ["KY"] = (37.6681, -84.6701), ["LA"] = (31.1695, -91.8678), ["ME"] = (44.6939, -69.3819), ["MD"] = (39.0639, -76.8021),
        ["MA"] = (42.2302, -71.5301), ["MI"] = (43.3266, -84.5361), ["MN"] = (45.6945, -93.9002), ["MS"] = (32.7416, -89.6787),
        ["MO"] = (38.4561, -92.2884), ["MT"] = (46.9219, -110.4544), ["NE"] = (41.1254, -98.2681), ["NV"] = (38.3135, -117.0554),
        ["NH"] = (43.4525, -71.5639), ["NJ"] = (40.2989, -74.5210), ["NM"] = (34.8405, -106.2485), ["NY"] = (42.1657, -74.9481),
        ["NC"] = (35.6301, -79.8064), ["ND"] = (47.5289, -99.7840), ["OH"] = (40.3888, -82.7649), ["OK"] = (35.5653, -96.9289),
        ["OR"] = (44.5720, -122.0709), ["PA"] = (40.5908, -77.2098), ["RI"] = (41.6809, -71.5118), ["SC"] = (33.8569, -80.9450),
        ["SD"] = (44.2998, -99.4388), ["TN"] = (35.7478, -86.6923), ["TX"] = (31.0545, -97.5635), ["UT"] = (40.1500, -111.8624),
        ["VT"] = (44.0459, -72.7107), ["VA"] = (37.7693, -78.1700), ["WA"] = (47.4009, -121.4905), ["WV"] = (38.4912, -80.9545),
        ["WI"] = (44.2685, -89.6165), ["WY"] = (42.7560, -107.3025), ["DC"] = (38.9072, -77.0369),
    };
    private static readonly SmsBasicMapping[] SmsBasicMappings =
    [
        new("Unsafe Driving", "unsafe_driv_measure", "unsafe_driv_pct", "unsafe_driv_basic_alert", "unsafe_driv_rd_alert", "unsafe_driv_ac", "unsafe_driv_insp_w_viol"),
        new("Hours-of-Service Compliance", "hos_driv_measure", "hos_driv_pct", "hos_driv_basic_alert", "hos_driv_rd_alert", "hos_driv_ac", "hos_driv_insp_w_viol"),
        new("Driver Fitness", "driv_fit_measure", "driv_fit_pct", "driv_fit_basic_alert", "driv_fit_rd_alert", "driv_fit_ac", "driv_fit_insp_w_viol"),
        new("Controlled Substances/Alcohol", "contr_subst_measure", "contr_subst_pct", "contr_subst_basic_alert", "contr_subst_rd_alert", "contr_subst_ac", "contr_subst_insp_w_viol"),
        new("Vehicle Maintenance", "veh_maint_measure", "veh_maint_pct", "veh_maint_basic_alert", "veh_maint_rd_alert", "veh_maint_ac", "veh_maint_insp_w_viol"),
    ];
    private static readonly IReadOnlyDictionary<string, string> QcMobileBasicNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Unsafe Driving"] = "Unsafe Driving",
        ["US"] = "Unsafe Driving",
        ["Fatigued Driving"] = "Hours-of-Service Compliance",
        ["Hours-of-Service Compliance"] = "Hours-of-Service Compliance",
        ["HOS Compliance"] = "Hours-of-Service Compliance",
        ["Driver Fitness"] = "Driver Fitness",
        ["Controlled Substances/Alcohol"] = "Controlled Substances/Alcohol",
        ["Controlled Substance/Alcohol"] = "Controlled Substances/Alcohol",
        ["Drugs/Alcohol"] = "Controlled Substances/Alcohol",
        ["Vehicle Maintenance"] = "Vehicle Maintenance",
    };
    private static readonly IReadOnlyDictionary<string, string> WeatherConditions = new Dictionary<string, string>
    {
        ["1"] = "No adverse conditions",
        ["2"] = "Rain",
        ["3"] = "Sleet, hail",
        ["4"] = "Snow",
        ["5"] = "Fog, smoke, smog",
        ["6"] = "Severe crosswinds",
        ["7"] = "Other",
        ["8"] = "Not reported",
        ["9"] = "Unknown",
    };
    private static readonly IReadOnlyDictionary<string, string> RoadSurfaceConditions = new Dictionary<string, string>
    {
        ["1"] = "Dry",
        ["2"] = "Wet",
        ["3"] = "Snow",
        ["4"] = "Ice/frost",
        ["5"] = "Sand, mud, dirt, oil, gravel",
        ["6"] = "Water",
        ["7"] = "Slush",
        ["8"] = "Other",
        ["9"] = "Unknown",
    };
    private static readonly IReadOnlyDictionary<string, string> TrafficwayTypes = new Dictionary<string, string>
    {
        ["1"] = "Two-way, divided",
        ["2"] = "Two-way, not divided",
        ["3"] = "One-way trafficway",
        ["4"] = "Entrance/exit ramp",
        ["5"] = "Non-trafficway",
        ["8"] = "Other",
        ["9"] = "Unknown",
    };
    private static readonly IReadOnlyDictionary<string, string> LightConditions = new Dictionary<string, string>
    {
        ["1"] = "Daylight",
        ["2"] = "Dark, not lighted",
        ["3"] = "Dark, lighted",
        ["4"] = "Dawn",
        ["5"] = "Dusk",
        ["6"] = "Dark, unknown lighting",
        ["7"] = "Other",
        ["8"] = "Not reported",
        ["9"] = "Unknown",
    };
    private static readonly IReadOnlyDictionary<string, string> VehicleConfigurationTypes = new Dictionary<string, string>
    {
        ["1"] = "Passenger car",
        ["2"] = "Light truck",
        ["3"] = "Single-unit truck",
        ["4"] = "Truck/trailer",
        ["5"] = "Truck tractor/semi-trailer",
        ["6"] = "Truck tractor/double",
        ["7"] = "Truck tractor/triple",
        ["8"] = "Bus",
        ["9"] = "Truck tractor",
    };
    private static readonly IReadOnlyDictionary<string, string> CargoBodyTypes = new Dictionary<string, string>
    {
        ["1"] = "Van/enclosed box",
        ["2"] = "Cargo tank",
        ["3"] = "Flatbed",
        ["4"] = "Dump",
        ["5"] = "Concrete mixer",
        ["6"] = "Auto transporter",
        ["7"] = "Garbage/refuse",
        ["8"] = "Grain/chips/gravel",
        ["9"] = "Pole/logging",
        ["10"] = "Intermodal container",
        ["11"] = "Vehicle towing another vehicle",
        ["12"] = "Not applicable",
        ["13"] = "Other",
        ["14"] = "Logging",
    };
    private static readonly IReadOnlyDictionary<string, string> GvwRanges = new Dictionary<string, string>
    {
        ["1"] = "10,000 lbs or less",
        ["2"] = "10,001-26,000 lbs",
        ["3"] = "26,001 lbs or more",
    };

    private readonly ApplicationDbContext _db;
    private readonly FmcsaSocrataClient _socrata;
    private readonly ILogger<FmcsaSafetyService> _logger;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly IFmcsaSafetyAnalyticsService _analyticsService;

    public FmcsaSafetyService(ApplicationDbContext db, FmcsaSocrataClient socrata, ILogger<FmcsaSafetyService> logger, IHttpClientFactory httpFactory, IServiceProvider serviceProvider, IFmcsaSafetyAnalyticsService analyticsService)
    {
        _db = db;
        _socrata = socrata;
        _logger = logger;
        _httpFactory = httpFactory;
        _serviceProvider = serviceProvider;
        _analyticsService = analyticsService;
    }

    public async Task<Result<AutoSafetySummaryDto>> GetQuoteAutoSafetyAsync(Guid quoteId, CancellationToken ct = default)
    {
        var quote = await _db.Quotes
            .Include(q => q.Submission).ThenInclude(s => s.Insured)
            .FirstOrDefaultAsync(q => q.Id == quoteId, ct);

        if (quote == null)
            return Result<AutoSafetySummaryDto>.Failure("NOT_FOUND", "Quote not found.");

        var insured = quote.Submission?.Insured;
        var dotNumber = NormalizeDotNumber(insured?.UsDotNumber);
        if (string.IsNullOrWhiteSpace(dotNumber))
        {
            return Result<AutoSafetySummaryDto>.Success(new AutoSafetySummaryDto
            {
                Status = "MissingDot",
                Message = "Add a USDOT number to this insured to show FMCSA auto safety intelligence.",
                CarrierName = insured?.DisplayName,
                OverallRiskLevel = "Unknown",
            });
        }

        var carrier = await _db.FmcsaCarrierSnapshots
            .Where(c => c.UsDotNumber == dotNumber)
            .OrderByDescending(c => c.SnapshotMonth)
            .FirstOrDefaultAsync(ct);

        var scoringRun = await _db.FmcsaScoringRuns
            .Include(r => r.BasicScores)
            .Where(r => r.UsDotNumber == dotNumber)
            .OrderByDescending(r => r.SnapshotMonth)
            .ThenByDescending(r => r.GeneratedAt)
            .FirstOrDefaultAsync(ct);
        var analyticsScores = await GetAnalyticsBasicScoresAsync(dotNumber, ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var windowStart = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-24));
        var trendWindowStart = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-36));
        var inspections = await _db.FmcsaInspections
            .Where(i => i.UsDotNumber == dotNumber && i.InspectionDate >= windowStart)
            .ToListAsync(ct);
        var violations = await _db.FmcsaViolations
            .Where(v => v.UsDotNumber == dotNumber && v.Inspection.InspectionDate >= windowStart)
            .Include(v => v.Inspection)
            .ToListAsync(ct);
        var trendInspections = await _db.FmcsaInspections
            .Where(i => i.UsDotNumber == dotNumber && i.InspectionDate >= trendWindowStart)
            .ToListAsync(ct);
        var trendViolations = await _db.FmcsaViolations
            .Where(v => v.UsDotNumber == dotNumber && v.Inspection.InspectionDate >= trendWindowStart)
            .Include(v => v.Inspection)
            .ToListAsync(ct);
        var crashes = await _db.FmcsaCrashes
            .Where(c => c.UsDotNumber == dotNumber && c.CrashDate >= windowStart)
            .ToListAsync(ct);

        if (carrier == null && scoringRun == null && inspections.Count == 0 && violations.Count == 0 && crashes.Count == 0)
        {
            return Result<AutoSafetySummaryDto>.Success(new AutoSafetySummaryDto
            {
                Status = "NoData",
                Message = "No FMCSA data has been imported for this USDOT number yet.",
                UsDotNumber = dotNumber,
                CarrierName = insured?.DisplayName,
                OverallRiskLevel = "Unknown",
            });
        }

        var oos = BuildOos(inspections, violations);
        var accidentSummary = BuildAccidentSummary(crashes, carrier?.PowerUnits);
        var basics = BuildBasics(scoringRun, analyticsScores, violations, accidentSummary);
        var hotspots = BuildHotspots(inspections, violations);
        var radiusSummary = BuildRadiusSummary(insured, inspections, violations);
        var severeEvents = BuildSevereEvents(violations);
        var flags = BuildFlags(basics, oos, carrier, scoringRun);

        return Result<AutoSafetySummaryDto>.Success(new AutoSafetySummaryDto
        {
            Status = "Ready",
            UsDotNumber = dotNumber,
            CarrierName = carrier?.LegalName ?? insured?.DisplayName,
            SnapshotMonth = scoringRun?.SnapshotMonth ?? carrier?.SnapshotMonth,
            MethodologyVersion = scoringRun?.MethodologyVersion ?? CurrentMethodologyVersion,
            OverallRiskLevel = DetermineRiskLevel(basics, oos, severeEvents),
            PowerUnits = carrier?.PowerUnits,
            DriverCount = carrier?.DriverCount,
            DataRefreshedAt = new[] { carrier?.ImportedAt, scoringRun?.GeneratedAt, inspections.Select(i => (DateTime?)i.ImportedAt).Max(), violations.Select(v => (DateTime?)v.ImportedAt).Max(), crashes.Select(c => (DateTime?)c.ImportedAt).Max() }
                .Where(d => d.HasValue)
                .Max(),
            Iss = BuildIss(basics, carrier, inspections, violations),
            SummaryFlags = flags,
            Basics = basics,
            Oos = oos,
            AccidentSummary = accidentSummary,
            GeographicHotspots = hotspots,
            RadiusSummary = radiusSummary,
            RecentSevereEvents = severeEvents,
            InspectionTrend = BuildInspectionTrend(trendInspections, trendViolations, today),
            ViolationTrend = BuildViolationTrend(trendViolations, today),
        });
    }

    public async Task<Result<List<AutoSafetyDetailDto>>> GetQuoteAutoSafetyDetailsAsync(Guid quoteId, string kind, string? basic = null, CancellationToken ct = default)
    {
        var quote = await _db.Quotes
            .Include(q => q.Submission).ThenInclude(s => s.Insured)
            .FirstOrDefaultAsync(q => q.Id == quoteId, ct);

        if (quote == null)
            return Result<List<AutoSafetyDetailDto>>.Failure("NOT_FOUND", "Quote not found.");

        var dotNumber = NormalizeDotNumber(quote.Submission?.Insured?.UsDotNumber);
        if (string.IsNullOrWhiteSpace(dotNumber))
            return Result<List<AutoSafetyDetailDto>>.Success([]);

        var windowStart = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-24));
        var trendWindowStart = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-36));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var normalizedKind = kind?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedKind))
            return Result<List<AutoSafetyDetailDto>>.Failure("INVALID_DETAIL_KIND", "Select an Auto Safety detail type.");

        if (normalizedKind.EndsWith("crash", StringComparison.Ordinal))
        {
            var crashes = await _db.FmcsaCrashes
                .Where(c => c.UsDotNumber == dotNumber && c.CrashDate >= windowStart && (c.Fatality || c.Injury || c.TowAway))
                .ToListAsync(ct);

            return Result<List<AutoSafetyDetailDto>>.Success(BuildCrashDetails(crashes, normalizedKind));
        }

        var detailWindowStart = normalizedKind is "inspection-trend" or "violation-trend" ? trendWindowStart : windowStart;
        var violations = await _db.FmcsaViolations
            .Include(v => v.Inspection)
            .Where(v => v.UsDotNumber == dotNumber && v.Inspection.InspectionDate >= detailWindowStart)
            .ToListAsync(ct);
        var inspections = normalizedKind == "inspection-trend"
            ? await _db.FmcsaInspections
                .Where(i => i.UsDotNumber == dotNumber && i.InspectionDate >= trendWindowStart)
                .ToListAsync(ct)
            : [];
        if (normalizedKind is "inspection-trend" or "violation-trend" && !string.IsNullOrWhiteSpace(basic))
        {
            violations = violations.Where(v => IsInTrendBucket(v.Inspection.InspectionDate, basic, today)).ToList();
            inspections = inspections.Where(i => IsInTrendBucket(i.InspectionDate, basic, today)).ToList();
        }

        var details = normalizedKind switch
        {
            "overall-oos" => BuildViolationDetails(violations.Where(v => v.IsOutOfService || v.IsDriverDisqualifying), "OOS"),
            "driver-oos" => BuildViolationDetails(violations.Where(v => (v.IsOutOfService || v.IsDriverDisqualifying) && IsDriverOosViolation(v)), "Driver OOS"),
            "vehicle-oos" => BuildViolationDetails(violations.Where(v => (v.IsOutOfService || v.IsDriverDisqualifying) && IsVehicleOosViolation(v)), "Vehicle OOS"),
            "hazmat-oos" => BuildViolationDetails(violations.Where(v => (v.IsOutOfService || v.IsDriverDisqualifying) && IsHazmatOosViolation(v)), "Hazmat OOS"),
            "inspection-trend" => BuildInspectionDetails(inspections, violations),
            "violation-trend" => BuildViolationDetails(violations, "Violation"),
            "basic" when !string.IsNullOrWhiteSpace(basic) => BuildViolationDetails(violations.Where(v => v.Basic == basic), basic!),
            _ => [],
        };

        return Result<List<AutoSafetyDetailDto>>.Success(details);
    }

    public async Task<Result<AutoSafetyRefreshDto>> RefreshQuoteAutoSafetyAsync(Guid quoteId, CancellationToken ct = default)
    {
        var quote = await _db.Quotes
            .Include(q => q.Submission).ThenInclude(s => s.Insured)
            .FirstOrDefaultAsync(q => q.Id == quoteId, ct);

        if (quote == null)
            return Result<AutoSafetyRefreshDto>.Failure("NOT_FOUND", "Quote not found.");

        var dotNumber = NormalizeDotNumber(quote.Submission?.Insured?.UsDotNumber);
        if (string.IsNullOrWhiteSpace(dotNumber))
        {
            var missingDotSummary = await GetQuoteAutoSafetyAsync(quoteId, ct);
            return Result<AutoSafetyRefreshDto>.Success(new AutoSafetyRefreshDto { Summary = missingDotSummary.Value! });
        }

        List<Dictionary<string, JsonElement>> carrierRows;
        List<Dictionary<string, JsonElement>> vehicleInspectionRows;
        List<Dictionary<string, JsonElement>> inspectionRows;
        List<Dictionary<string, JsonElement>> violationRows;
        List<Dictionary<string, JsonElement>> crashRows;
        (string Source, List<Dictionary<string, JsonElement>> Rows) smsRows;
        List<Dictionary<string, JsonElement>> qcMobileBasicRows;

        try
        {
            carrierRows = await _socrata.GetCensusByDotAsync(dotNumber, ct);
            vehicleInspectionRows = await _socrata.GetVehicleInspectionFileByDotAsync(dotNumber, ct);
            inspectionRows = await _socrata.GetInspectionsByDotAsync(dotNumber, ct);
            violationRows = await _socrata.GetViolationsByInspectionIdsAsync(
                inspectionRows.Select(r => GetString(r, "unique_id", "inspection_id")).Where(id => !string.IsNullOrWhiteSpace(id))!,
                ct);
            crashRows = await _socrata.GetCrashesByDotAsync(dotNumber, ct);
            smsRows = await _socrata.GetSmsScoresByDotAsync(dotNumber, ct);
            qcMobileBasicRows = await _socrata.GetQcMobileBasicsByDotAsync(dotNumber, ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "FMCSA Socrata lookup failed for USDOT {DotNumber}", dotNumber);
            return Result<AutoSafetyRefreshDto>.Failure(
                "FMCSA_SOURCE_UNAVAILABLE",
                "FMCSA source data could not be reached for this USDOT number. Try again shortly.");
        }

        var now = DateTime.UtcNow;
        var snapshotMonth = now.ToString("yyyy-MM", CultureInfo.InvariantCulture);

        int carrierCount;
        int inspectionCount;
        int violationCount;
        int crashCount;
        try
        {
            carrierCount = await UpsertCarrierSnapshotsAsync(dotNumber, snapshotMonth, carrierRows, now, ct);
            inspectionCount = await UpsertVehicleInspectionFileAsync(dotNumber, vehicleInspectionRows, now, ct);
            inspectionCount += await UpsertInspectionsAsync(dotNumber, inspectionRows, now, ct);
            violationCount = await UpsertViolationsAsync(dotNumber, inspectionRows, violationRows, now, ct);
            crashCount = await UpsertCrashesAsync(dotNumber, crashRows, now, ct);
            await UpsertOfficialSmsScoresAsync(dotNumber, snapshotMonth, smsRows.Source, smsRows.Rows, now, ct);
            await UpsertQcMobileBasicScoresAsync(dotNumber, snapshotMonth, qcMobileBasicRows, now, ct);

            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "FMCSA data save failed for USDOT {DotNumber}", dotNumber);
            return Result<AutoSafetyRefreshDto>.Failure(
                "FMCSA_SAVE_FAILED",
                BuildSaveFailureMessage(ex));
        }

        var analyticsResult = await _analyticsService.RefreshImportedCarrierAnalyticsAsync(snapshotMonth, ct);
        if (!analyticsResult.IsSuccess && analyticsResult.ErrorCode != "SAFETY_ANALYTICS_NOT_CONFIGURED")
            _logger.LogWarning("FMCSA analytics refresh failed after USDOT {DotNumber} import: {ErrorCode} {ErrorMessage}", dotNumber, analyticsResult.ErrorCode, analyticsResult.ErrorMessage);

        var summary = await GetQuoteAutoSafetyAsync(quoteId, ct);
        if (!summary.IsSuccess)
            return Result<AutoSafetyRefreshDto>.Failure(summary.ErrorCode ?? "REFRESH_FAILED", summary.ErrorMessage ?? "Unable to refresh Auto Safety.");

        return Result<AutoSafetyRefreshDto>.Success(new AutoSafetyRefreshDto
        {
            Summary = summary.Value!,
            CarrierRowsImported = carrierCount,
            InspectionRowsImported = inspectionCount,
            ViolationRowsImported = violationCount,
            CrashRowsImported = crashCount,
            RefreshedAt = now,
        });
    }

    private async Task<int> UpsertCarrierSnapshotsAsync(string dotNumber, string snapshotMonth, List<Dictionary<string, JsonElement>> rows, DateTime now, CancellationToken ct)
    {
        var count = 0;
        foreach (var row in rows.Take(1))
        {
            var carrier = await _db.FmcsaCarrierSnapshots
                .FirstOrDefaultAsync(c => c.UsDotNumber == dotNumber && c.SnapshotMonth == snapshotMonth, ct);
            if (carrier == null)
            {
                carrier = new FmcsaCarrierSnapshot { UsDotNumber = dotNumber, SnapshotMonth = snapshotMonth };
                _db.FmcsaCarrierSnapshots.Add(carrier);
            }

            carrier.LegalName = GetString(row, "legal_name", "carrier_name", "name") ?? dotNumber;
            carrier.DbaName = GetString(row, "dba_name", "dba");
            carrier.PhysicalAddress = GetString(row, "phy_street", "physical_address", "street");
            carrier.City = GetString(row, "phy_city", "city");
            carrier.State = GetString(row, "phy_state", "state");
            carrier.ZipCode = GetString(row, "phy_zip", "zip_code", "zip");
            carrier.PowerUnits = GetInt(row, "nbr_power_unit", "power_units", "total_power_units");
            carrier.DriverCount = GetInt(row, "driver_total", "drivers", "driver_count");
            carrier.Mileage = GetInt(row, "mcs150_mileage", "mileage");
            carrier.MileageYear = GetInt(row, "mcs150_mileage_year", "mileage_year");
            carrier.OperationClassification = GetString(row, "classification", "operation_classification");
            carrier.CarrierOperation = GetString(row, "carrier_operation", "operation");
            carrier.ImportedAt = now;
            count++;
        }

        return count;
    }

    private async Task<int> UpsertInspectionsAsync(string dotNumber, List<Dictionary<string, JsonElement>> rows, DateTime now, CancellationToken ct)
    {
        var count = 0;
        foreach (var row in rows)
        {
            var reportNumber = GetString(row, "report_number", "insp_report_number", "inspection_report_number");
            if (string.IsNullOrWhiteSpace(reportNumber)) continue;
            var inspectionDate = GetDate(row, "inspection_date", "insp_date", "inspection_dt", "inspection_date_dt", "activity_date", "report_date", "date");
            if (inspectionDate == null) continue;

            var inspection = await _db.FmcsaInspections
                .FirstOrDefaultAsync(i => i.UsDotNumber == dotNumber && i.ReportNumber == reportNumber, ct);
            if (inspection == null)
            {
                inspection = new FmcsaInspection { UsDotNumber = dotNumber, ReportNumber = reportNumber };
                _db.FmcsaInspections.Add(inspection);
            }

            inspection.ExternalInspectionId ??= GetString(row, "unique_id", "inspection_id");
            inspection.InspectionDate = inspectionDate.Value;
            inspection.State = GetString(row, "state", "inspection_state", "insp_state", "report_state");
            inspection.CountyCodeState = GetString(row, "county_code_state", "county_code", "county");
            inspection.InspectionLevel = GetInt(row, "inspection_level", "insp_level", "insp_level_id", "level");
            inspection.DriverOutOfService = GetBool(row, "driver_oos", "driver_out_of_service", "drv_oos", "driver_oos_indicator", "driver_oos_flag")
                || GetInt(row, "driver_oos_total") is > 0;
            inspection.VehicleOutOfService = GetBool(row, "vehicle_oos", "vehicle_out_of_service", "veh_oos", "vehicle_oos_indicator", "vehicle_oos_flag")
                || GetInt(row, "vehicle_oos_total") is > 0;
            inspection.HazmatOutOfService = GetBool(row, "hazmat_oos", "hm_oos", "hazmat_out_of_service", "hazmat_oos_indicator", "hazmat_oos_flag")
                || GetInt(row, "hazmat_oos_total") is > 0;
            inspection.HazmatPlacardRequired = GetBool(row, "hazmat_placard_req", "hazmat_placard_required");
            inspection.DriverViolationCount = GetInt(row, "driver_violation_count", "drv_violation_count", "driver_violations", "driver_viol_total") ?? 0;
            inspection.VehicleViolationCount = GetInt(row, "vehicle_violation_count", "veh_violation_count", "vehicle_violations", "vehicle_viol_total") ?? 0;
            inspection.HazmatViolationCount = GetInt(row, "hazmat_violation_count", "hm_violation_count", "hazmat_violations", "hazmat_viol_total") ?? 0;
            inspection.UnitType = GetString(row, "unit_type_desc", "unit_type");
            inspection.UnitMake = GetString(row, "unit_make", "make");
            inspection.UnitLicense = GetString(row, "unit_license", "license");
            inspection.UnitLicenseState = GetString(row, "unit_license_state", "license_state");
            inspection.Vin = GetString(row, "vin", "vehicle_identification_number");
            inspection.UnitType2 = GetString(row, "unit_type_desc2", "unit_type_2");
            inspection.UnitMake2 = GetString(row, "unit_make2", "unit_make_2");
            inspection.UnitLicense2 = GetString(row, "unit_license2", "unit_license_2");
            inspection.UnitLicenseState2 = GetString(row, "unit_license_state2", "unit_license_state_2");
            inspection.Vin2 = GetString(row, "vin2", "vin_2");
            inspection.ImportedAt = now;
            count++;
        }

        return count;
    }

    private async Task<int> UpsertVehicleInspectionFileAsync(string dotNumber, List<Dictionary<string, JsonElement>> rows, DateTime now, CancellationToken ct)
    {
        var count = 0;
        foreach (var row in rows)
        {
            var reportNumber = GetString(row, "report_number");
            if (string.IsNullOrWhiteSpace(reportNumber)) continue;

            var inspectionDate = GetDate(row, "insp_date", "inspection_date");
            if (inspectionDate == null) continue;

            var inspection = await _db.FmcsaInspections
                .FirstOrDefaultAsync(i => i.UsDotNumber == dotNumber && i.ReportNumber == reportNumber, ct);
            if (inspection == null)
            {
                inspection = new FmcsaInspection { UsDotNumber = dotNumber, ReportNumber = reportNumber };
                _db.FmcsaInspections.Add(inspection);
            }

            inspection.ExternalInspectionId = GetString(row, "inspection_id") ?? inspection.ExternalInspectionId;
            inspection.InspectionDate = inspectionDate.Value;
            inspection.State = GetString(row, "report_state") ?? inspection.State;
            inspection.CountyCodeState = GetString(row, "county_code_state") ?? inspection.CountyCodeState;
            inspection.CountyCode = GetString(row, "county_code") ?? inspection.CountyCode;
            inspection.InspectionCounty = ResolveCountyName(inspection.CountyCodeState ?? inspection.State, inspection.CountyCode) ?? inspection.InspectionCounty;
            inspection.InspectionLocation = GetString(row, "location_desc") ?? inspection.InspectionLocation;
            inspection.InspectionFacility = FormatInspectionFacility(GetString(row, "insp_facility")) ?? inspection.InspectionFacility;
            inspection.StartTime = FormatMilitaryTime(GetString(row, "insp_start_time")) ?? inspection.StartTime;
            inspection.EndTime = FormatMilitaryTime(GetString(row, "insp_end_time")) ?? inspection.EndTime;
            inspection.PostCrash = GetNullableBool(row, "post_acc_ind") ?? inspection.PostCrash;
            inspection.HazmatPlacardRequired = GetNullableBool(row, "hazmat_placard_req") ?? inspection.HazmatPlacardRequired;
            inspection.InspectionLevel = GetInt(row, "insp_level_id") ?? inspection.InspectionLevel;
            inspection.InspectionLevelDescription = FormatInspectionLevel(inspection.InspectionLevel) ?? inspection.InspectionLevelDescription;
            inspection.DriverOutOfService = GetInt(row, "driver_oos_total") is > 0 || inspection.DriverOutOfService;
            inspection.VehicleOutOfService = GetInt(row, "vehicle_oos_total") is > 0 || inspection.VehicleOutOfService;
            inspection.HazmatOutOfService = GetInt(row, "hazmat_oos_total") is > 0 || inspection.HazmatOutOfService;
            inspection.DriverViolationCount = GetInt(row, "driver_viol_total") ?? inspection.DriverViolationCount;
            inspection.VehicleViolationCount = GetInt(row, "vehicle_viol_total") ?? inspection.VehicleViolationCount;
            inspection.HazmatViolationCount = GetInt(row, "hazmat_viol_total") ?? inspection.HazmatViolationCount;
            inspection.DetailEnrichedAt = now;
            inspection.ImportedAt = now;
            count++;
        }

        return count;
    }

    private async Task<int> UpsertViolationsAsync(string dotNumber, List<Dictionary<string, JsonElement>> inspectionRows, List<Dictionary<string, JsonElement>> rows, DateTime now, CancellationToken ct)
    {
        var count = 0;
        var inspectionsById = inspectionRows
            .Select(row => new
            {
                InspectionId = GetString(row, "unique_id", "inspection_id"),
                ReportNumber = GetString(row, "report_number", "insp_report_number", "inspection_report_number"),
                InspectionDate = GetDate(row, "inspection_date", "insp_date", "inspection_dt", "inspection_date_dt", "activity_date", "report_date", "date"),
                State = GetString(row, "state", "inspection_state", "insp_state", "report_state"),
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.InspectionId) && !string.IsNullOrWhiteSpace(x.ReportNumber))
            .ToDictionary(x => x.InspectionId!, x => x, StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var inspectionId = GetString(row, "unique_id", "inspection_id");
            if (string.IsNullOrWhiteSpace(inspectionId) || !inspectionsById.TryGetValue(inspectionId, out var inspectionRow))
                continue;

            var reportNumber = inspectionRow.ReportNumber;
            var violationCode = BuildViolationCode(row);
            var description = BuildViolationDescription(row);
            if (string.IsNullOrWhiteSpace(reportNumber) || string.IsNullOrWhiteSpace(violationCode)) continue;
            var inspectionDate = inspectionRow.InspectionDate;

            var inspection = await _db.FmcsaInspections
                .FirstOrDefaultAsync(i => i.UsDotNumber == dotNumber && i.ReportNumber == reportNumber, ct);
            if (inspection == null)
            {
                inspection = new FmcsaInspection
                {
                    UsDotNumber = dotNumber,
                    ReportNumber = reportNumber,
                    InspectionDate = inspectionDate ?? DateOnly.FromDateTime(now),
                    State = inspectionRow.State,
                    ImportedAt = now,
                };
                _db.FmcsaInspections.Add(inspection);
            }
            else if (inspectionDate.HasValue)
            {
                inspection.InspectionDate = inspectionDate.Value;
            }

            var violation = await _db.FmcsaViolations
                .FirstOrDefaultAsync(v =>
                    v.UsDotNumber == dotNumber &&
                    v.ReportNumber == reportNumber &&
                    v.ViolationCode == violationCode &&
                    v.Description == description, ct);
            if (violation == null)
            {
                violation = new FmcsaViolation
                {
                    UsDotNumber = dotNumber,
                    ReportNumber = reportNumber,
                    ViolationCode = violationCode,
                    Inspection = inspection,
                };
                _db.FmcsaViolations.Add(violation);
            }

            violation.Description = description;
            violation.Basic = NormalizeBasic(GetString(row, "basic", "basic_desc", "basic_name"));
            violation.ViolationGroup = GetString(row, "violation_group", "group_desc", "viol_group", "defect_group", "violation_category", "category", "unit_type", "insp_viol_unit", "insp_violation_category_id");
            violation.UnitNumber = GetString(row, "viol_unit", "insp_viol_unit", "unit");
            violation.OosWeight = GetDecimal(row, "oos_weight");
            violation.IsOutOfService = GetBool(row, "oos_indicator", "is_out_of_service", "out_of_service", "oos", "oos_flag", "out_of_service_indicator");
            violation.IsDriverDisqualifying = GetBool(row, "driver_disqualified", "is_driver_disqualifying");
            violation.SeverityWeight = GetInt(row, "severity_weight", "total_severity_wght") ?? (violation.IsOutOfService || violation.IsDriverDisqualifying ? 2 : 1);
            violation.TimeWeight = 1m;
            violation.ImportedAt = now;
            if (violation.IsOutOfService || violation.IsDriverDisqualifying)
                ApplyViolationOosToInspection(inspection, violation);
            count++;
        }

        return count;
    }

    private async Task<int> UpsertCrashesAsync(string dotNumber, List<Dictionary<string, JsonElement>> rows, DateTime now, CancellationToken ct)
    {
        var count = 0;
        var vinDecodes = new Dictionary<string, VinDecodeResult>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in DeduplicateBy(rows, r => GetString(r, "report_number", "crash_report_number", "crash_id")))
        {
            var reportNumber = GetString(row, "report_number", "crash_report_number", "crash_id");
            if (string.IsNullOrWhiteSpace(reportNumber)) continue;
            var crashDate = GetDate(row, "report_date", "crash_date", "accident_date", "date");
            if (crashDate == null) continue;
            if (!GetCrashFlag(row, "federal_recordable")) continue;

            var crash = await _db.FmcsaCrashes
                .FirstOrDefaultAsync(c => c.UsDotNumber == dotNumber && c.ReportNumber == reportNumber, ct);
            if (crash == null)
            {
                crash = new FmcsaCrash { UsDotNumber = dotNumber, ReportNumber = reportNumber };
                _db.FmcsaCrashes.Add(crash);
            }

            crash.CrashDate = crashDate.Value;
            crash.State = GetString(row, "state", "crash_state");
            crash.City = GetString(row, "city", "crash_city");
            crash.CountyCode = GetString(row, "county_code", "county");
            crash.Location = GetString(row, "location", "crash_location");
            crash.Agency = GetString(row, "agency", "reporting_agency");
            crash.VehiclesInAccident = GetInt(row, "vehicles_in_accident", "vehicles_in_crash");
            crash.WeatherConditionId = GetString(row, "weather_condition_id", "weather");
            crash.RoadSurfaceConditionId = GetString(row, "road_surface_condition_id", "road_surface");
            crash.TrafficwayId = GetString(row, "trafficway_id", "roadway_trafficway");
            crash.LightConditionId = GetString(row, "light_condition_id", "light_condition");
            crash.VehicleConfigurationId = GetString(row, "vehicle_configuration_id", "vehicle_configuration");
            crash.CargoBodyTypeId = GetString(row, "cargo_body_type_id", "cargo_body_type");
            crash.GvwRatingId = GetString(row, "gvw_rating_id", "gvw_rating");
            crash.VehicleIdentificationNumber = GetString(row, "vehicle_identification_number", "vin");
            var vinDecode = await DecodeVinAsync(crash.VehicleIdentificationNumber, vinDecodes, ct);
            if (vinDecode != null)
            {
                crash.VehicleYear = vinDecode.Year;
                crash.VehicleMake = vinDecode.Make;
                crash.VehicleModel = vinDecode.Model;
            }
            crash.VehicleLicenseNumber = GetString(row, "vehicle_license_number", "license_number");
            crash.VehicleLicenseState = GetString(row, "vehicle_lic_state", "vehicle_license_state");
            crash.HazmatPlacard = GetBool(row, "vehicle_hazmat_placard", "hazmat_placard");
            crash.HazmatReleased = GetBool(row, "hazmat_released");
            crash.TowAway = GetCrashFlag(row, "tow_away", "towaway", "tow_away_indicator", "towaway_indicator", "tow", "tow_away_count", "towaway_count");
            crash.Injury = GetCrashFlag(row, "injuries", "injury", "injury_indicator", "injury_crash", "injury_count", "non_fatal_injuries", "nonfatal_injuries", "number_of_injuries");
            crash.Fatality = GetCrashFlag(row, "fatalities", "fatality", "fatality_indicator", "fatal_crash", "fatal", "fatality_count", "fatal_injuries", "number_of_fatalities");
            crash.SeverityWeight = crash.Fatality ? 3m : crash.Injury ? 2m : 1m;
            crash.TimeWeight = 1m;
            crash.ImportedAt = now;
            count++;
        }

        return count;
    }

    private async Task UpsertOfficialSmsScoresAsync(string dotNumber, string snapshotMonth, string source, List<Dictionary<string, JsonElement>> rows, DateTime now, CancellationToken ct)
    {
        var row = rows.FirstOrDefault();
        if (row == null)
            return;

        var scoringRun = await _db.FmcsaScoringRuns
            .Include(r => r.BasicScores)
            .FirstOrDefaultAsync(r => r.UsDotNumber == dotNumber && r.SnapshotMonth == snapshotMonth, ct);
        if (scoringRun == null)
        {
            scoringRun = new FmcsaScoringRun { UsDotNumber = dotNumber, SnapshotMonth = snapshotMonth };
            _db.FmcsaScoringRuns.Add(scoringRun);
        }

        scoringRun.MethodologyVersion = $"{CurrentMethodologyVersion} - {source}";
        scoringRun.GeneratedAt = now;

        foreach (var smsBasic in SmsBasicMappings)
        {
            var score = scoringRun.BasicScores.FirstOrDefault(s => s.Basic == smsBasic.Basic);
            if (score == null)
            {
                score = new FmcsaBasicScore { Basic = smsBasic.Basic };
                scoringRun.BasicScores.Add(score);
            }

            score.Measure = GetDecimal(row, smsBasic.Measure);
            score.Percentile = GetDecimal(row, smsBasic.Percentile);
            score.IsPrioritized = GetBool(row, smsBasic.Alert, smsBasic.RoadsideAlert, smsBasic.AlertCode);
            score.EventCount = GetInt(row, smsBasic.EventCount) ?? 0;
            score.TrendDirection = "Flat";
        }
    }

    private async Task UpsertQcMobileBasicScoresAsync(string dotNumber, string snapshotMonth, List<Dictionary<string, JsonElement>> rows, DateTime now, CancellationToken ct)
    {
        if (rows.Count == 0)
            return;

        var scoringRun = await _db.FmcsaScoringRuns
            .Include(r => r.BasicScores)
            .FirstOrDefaultAsync(r => r.UsDotNumber == dotNumber && r.SnapshotMonth == snapshotMonth, ct);
        if (scoringRun == null)
        {
            scoringRun = new FmcsaScoringRun { UsDotNumber = dotNumber, SnapshotMonth = snapshotMonth };
            _db.FmcsaScoringRuns.Add(scoringRun);
        }

        scoringRun.MethodologyVersion = $"{CurrentMethodologyVersion} - FMCSA QCMobile BASICs";
        scoringRun.GeneratedAt = now;

        foreach (var row in rows)
        {
            var basic = NormalizeQcMobileBasic(row);
            if (string.IsNullOrWhiteSpace(basic))
                continue;

            var score = scoringRun.BasicScores.FirstOrDefault(s => s.Basic.Equals(basic, StringComparison.OrdinalIgnoreCase));
            if (score == null)
            {
                score = new FmcsaBasicScore { Basic = basic };
                scoringRun.BasicScores.Add(score);
            }

            score.Measure = GetDecimal(row, "measure", "basicMeasure", "performanceMeasure") ?? score.Measure;
            score.Percentile = GetDecimal(row, "percentile", "basicPercentile", "performancePercentile");
            score.IsPrioritized = GetBool(row, "rdDeficient", "rdsvDeficient", "svDeficient", "basicAlert", "alert") || score.IsPrioritized;
            score.EventCount = GetInt(row, "totalViolation", "totalViolations", "totalInspectionWithViolation") ?? score.EventCount;
            score.TrendDirection = "Flat";
        }
    }

    private static IEnumerable<Dictionary<string, JsonElement>> DeduplicateBy(
        IEnumerable<Dictionary<string, JsonElement>> rows,
        Func<Dictionary<string, JsonElement>, string?> keySelector)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var key = keySelector(row);
            if (string.IsNullOrWhiteSpace(key) || seen.Add(key))
                yield return row;
        }
    }

    private async Task<List<FmcsaBasicPeerMeasure>> GetAnalyticsBasicScoresAsync(string dotNumber, CancellationToken ct)
    {
        var analyticsDb = _serviceProvider.GetService<SafetyAnalyticsDbContext>();
        if (analyticsDb == null)
            return [];

        var latestSnapshot = await analyticsDb.FmcsaBasicPeerMeasures
            .Where(m => m.UsDotNumber == dotNumber)
            .OrderByDescending(m => m.SnapshotMonth)
            .Select(m => m.SnapshotMonth)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(latestSnapshot))
            return [];

        return await analyticsDb.FmcsaBasicPeerMeasures
            .AsNoTracking()
            .Where(m => m.UsDotNumber == dotNumber && m.SnapshotMonth == latestSnapshot)
            .ToListAsync(ct);
    }

    private static List<AutoSafetyBasicDto> BuildBasics(FmcsaScoringRun? scoringRun, List<FmcsaBasicPeerMeasure> analyticsScores, List<FmcsaViolation> violations, AutoSafetyAccidentSummaryDto accidentSummary)
    {
        var recentStart = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-12));
        var grouped = violations
            .Where(v => !string.IsNullOrWhiteSpace(v.Basic))
            .GroupBy(v => v.Basic!)
            .ToDictionary(g => g.Key, g => g.ToList());
        var analytics = analyticsScores.ToDictionary(s => s.Basic, StringComparer.OrdinalIgnoreCase);

        if (scoringRun?.BasicScores.Count > 0)
        {
            var officialScores = scoringRun.BasicScores.ToDictionary(s => s.Basic, StringComparer.OrdinalIgnoreCase);
            var basics = Basics.Select(b =>
                {
                    officialScores.TryGetValue(b, out var score);
                    analytics.TryGetValue(b, out var peerScore);
                    grouped.TryGetValue(b, out var events);
                    events ??= [];
                    var recentEvents = events.Where(v => v.Inspection.InspectionDate >= recentStart).ToList();
                    var peerPercentile = HasUsablePeerScore(peerScore, events)
                        ? peerScore?.SimsPercentile
                        : null;
                    var source = score?.Percentile != null
                        ? "Official SMS"
                        : peerPercentile != null
                            ? "SIMS peer percentile"
                            : score != null
                                ? "Official SMS measure"
                                : "SIMS signal";
                    return new AutoSafetyBasicDto
                    {
                        Basic = b,
                        Measure = score?.Measure ?? peerScore?.OfficialMeasure ?? peerScore?.SimsMeasure,
                        Percentile = score?.Percentile ?? peerPercentile,
                        IsPrioritized = score?.IsPrioritized ?? false,
                        EventCount = score?.EventCount > 0
                            ? score.EventCount
                            : events.Select(v => new { v.ReportNumber, Group = v.ViolationGroup ?? v.ViolationCode }).Distinct().Count(),
                        OutOfServiceCount = events.Count(v => v.IsOutOfService || v.IsDriverDisqualifying),
                        RecentEventCount = recentEvents.Select(v => new { v.ReportNumber, Group = v.ViolationGroup ?? v.ViolationCode }).Distinct().Count(),
                        RecentOutOfServiceCount = recentEvents.Count(v => v.IsOutOfService || v.IsDriverDisqualifying),
                        TrendDirection = score?.TrendDirection ?? "Flat",
                        ScoreSource = source,
                    };
                })
                .OrderByDescending(s => s.IsPrioritized)
                .ThenByDescending(s => s.Percentile ?? 0)
                .ThenBy(s => Array.IndexOf(Basics, s.Basic))
                .ToList();

            ApplyCrashIndicatorSignal(basics, accidentSummary);
            return basics;
        }

        var signalBasics = Basics.Select(b =>
        {
            analytics.TryGetValue(b, out var peerScore);
            grouped.TryGetValue(b, out var events);
            events ??= [];
            var recentEvents = events.Where(v => v.Inspection.InspectionDate >= recentStart).ToList();
            var peerPercentile = HasUsablePeerScore(peerScore, events)
                ? peerScore?.SimsPercentile
                : null;
            return new AutoSafetyBasicDto
                {
                    Basic = b,
                    Measure = peerScore?.OfficialMeasure ?? peerScore?.SimsMeasure,
                    Percentile = peerPercentile,
                    EventCount = events.Select(v => new { v.ReportNumber, Group = v.ViolationGroup ?? v.ViolationCode }).Distinct().Count(),
                    OutOfServiceCount = events.Count(v => v.IsOutOfService || v.IsDriverDisqualifying),
                    RecentEventCount = recentEvents.Select(v => new { v.ReportNumber, Group = v.ViolationGroup ?? v.ViolationCode }).Distinct().Count(),
                    RecentOutOfServiceCount = recentEvents.Count(v => v.IsOutOfService || v.IsDriverDisqualifying),
                    TrendDirection = "Flat",
                    ScoreSource = peerPercentile != null ? "SIMS peer percentile" : "SIMS signal",
                };
        }).ToList();

        ApplyCrashIndicatorSignal(signalBasics, accidentSummary);
        return signalBasics;
    }

    private static void ApplyCrashIndicatorSignal(List<AutoSafetyBasicDto> basics, AutoSafetyAccidentSummaryDto accidentSummary)
    {
        if (accidentSummary.TotalReportableCount <= 0)
            return;

        var crash = basics.FirstOrDefault(b => b.Basic == "Crash Indicator");
        if (crash == null || crash.Percentile.HasValue || crash.IsPrioritized)
            return;

        crash.EventCount = Math.Max(crash.EventCount, accidentSummary.TotalReportableCount);
        crash.RecentEventCount = Math.Max(crash.RecentEventCount, accidentSummary.TotalReportableCount);
        crash.Measure ??= accidentSummary.AccidentToPowerUnitRatio.HasValue
            ? Math.Round(accidentSummary.AccidentToPowerUnitRatio.Value / 100m, 2)
            : null;

        if (crash.ScoreSource == "SIMS signal")
            crash.ScoreSource = "SIMS crash signal";
    }

    private static bool HasUsablePeerScore(FmcsaBasicPeerMeasure? peerScore, List<FmcsaViolation> events)
    {
        if (peerScore?.SimsPercentile == null)
            return false;

        return events.Count > 0
            || peerScore.ViolationCount > 0
            || peerScore.InspectionWithViolationCount > 0
            || peerScore.OutOfServiceCount > 0
            || peerScore.WeightedViolationScore > 0
            || (peerScore.OfficialMeasure ?? 0) > 0
            || (peerScore.SimsMeasure ?? 0) > 0;
    }

    private static AutoSafetyOosDto BuildOos(List<FmcsaInspection> inspections, List<FmcsaViolation> violations)
    {
        const decimal overallNationalAverage = 20.18m;
        const decimal driverNationalAverage = 6.67m;
        const decimal vehicleNationalAverage = 22.26m;
        const decimal hazmatNationalAverage = 4.44m;
        var count = inspections.Count;
        var driverOosReports = inspections
            .Where(i => i.DriverOutOfService)
            .Select(i => i.ReportNumber)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var vehicleOosReports = inspections
            .Where(i => i.VehicleOutOfService)
            .Select(i => i.ReportNumber)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var hazmatOosReports = inspections
            .Where(i => i.HazmatOutOfService)
            .Select(i => i.ReportNumber)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var violation in violations.Where(v => v.IsOutOfService || v.IsDriverDisqualifying))
        {
            if (IsDriverOosViolation(violation))
                driverOosReports.Add(violation.ReportNumber);
            else if (IsHazmatOosViolation(violation))
                hazmatOosReports.Add(violation.ReportNumber);
            else if (IsVehicleOosViolation(violation))
                vehicleOosReports.Add(violation.ReportNumber);
        }

        var hazmatInspectionReports = violations
            .Where(IsHazmatOosViolation)
            .Select(v => v.ReportNumber)
            .Concat(inspections.Where(i => i.HazmatViolationCount > 0 || i.HazmatOutOfService).Select(i => i.ReportNumber))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var overallOos = driverOosReports
            .Concat(vehicleOosReports)
            .Concat(hazmatOosReports)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var driverInspectionCount = inspections.Count;
        var vehicleInspectionCount = inspections.Count(i => IsVehicleInspection(i));
        var hazmatInspectionCount = hazmatInspectionReports.Count;
        var driverOos = driverOosReports.Count;
        var vehicleOos = vehicleOosReports.Count;
        var hazmatOos = hazmatOosReports.Count;

        return new AutoSafetyOosDto
        {
            InspectionCount = count,
            OverallOosCount = overallOos,
            OverallOosRate = count == 0 ? null : Math.Round(overallOos * 100m / count, 2),
            DriverInspectionCount = driverInspectionCount,
            DriverOosCount = driverOos,
            VehicleInspectionCount = vehicleInspectionCount,
            VehicleOosCount = vehicleOos,
            HazmatInspectionCount = hazmatInspectionCount,
            HazmatOosCount = hazmatOos,
            DriverOosRate = driverInspectionCount == 0 ? null : Math.Round(driverOos * 100m / driverInspectionCount, 2),
            VehicleOosRate = vehicleInspectionCount == 0 ? null : Math.Round(vehicleOos * 100m / vehicleInspectionCount, 2),
            HazmatOosRate = hazmatInspectionCount == 0 ? null : Math.Round(hazmatOos * 100m / hazmatInspectionCount, 2),
            OverallNationalAverageRate = overallNationalAverage,
            DriverNationalAverageRate = driverNationalAverage,
            VehicleNationalAverageRate = vehicleNationalAverage,
            HazmatNationalAverageRate = hazmatNationalAverage,
        };
    }

    private static AutoSafetyAccidentSummaryDto BuildAccidentSummary(List<FmcsaCrash> crashes, int? powerUnits)
    {
        var reportableCrashes = crashes
            .Where(c => c.Fatality || c.Injury || c.TowAway)
            .GroupBy(c => c.ReportNumber, StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                Fatal = g.Any(c => c.Fatality),
                Injury = g.Any(c => c.Injury),
                Tow = g.Any(c => c.TowAway),
            })
            .ToList();

        var fatal = reportableCrashes.Count(c => c.Fatal);
        var injury = reportableCrashes.Count(c => !c.Fatal && c.Injury);
        var tow = reportableCrashes.Count(c => !c.Fatal && !c.Injury && c.Tow);
        var totalReportable = reportableCrashes.Count;

        return new AutoSafetyAccidentSummaryDto
        {
            FatalCount = fatal,
            InjuryCount = injury,
            TowCount = tow,
            TotalReportableCount = totalReportable,
            AccidentToPowerUnitRatio = powerUnits is > 0
                ? Math.Round(totalReportable * 100m / powerUnits.Value, 2)
                : null,
        };
    }

    private static List<AutoSafetyDetailDto> BuildCrashDetails(List<FmcsaCrash> crashes, string kind)
    {
        return crashes
            .GroupBy(c => c.ReportNumber, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var first = g.OrderByDescending(c => c.CrashDate).First();
                var fatal = g.Any(c => c.Fatality);
                var injury = g.Any(c => c.Injury);
                var tow = g.Any(c => c.TowAway);
                return new AutoSafetyDetailDto
                {
                    Category = fatal ? "Fatal crash" : injury ? "Injury crash" : "Tow-only crash",
                    Date = first.CrashDate,
                    ReportNumber = first.ReportNumber,
                    State = first.State,
                    City = first.City,
                    CountyCode = first.CountyCode,
                    Location = first.Location,
                    Agency = first.Agency,
                    Conditions = BuildCrashConditions(first),
                    VehicleInfo = BuildCrashVehicleInfo(first),
                    CrashEvents = BuildCrashEvents(first, fatal, injury, tow),
                    Description = BuildCrashDescription(first, fatal, injury, tow),
                    IsFatal = fatal,
                    IsInjury = injury,
                    IsTow = tow,
                };
            })
            .Where(d => kind switch
            {
                "fatal-crash" => d.IsFatal,
                "injury-crash" => !d.IsFatal && d.IsInjury,
                "tow-crash" => !d.IsFatal && !d.IsInjury && d.IsTow,
                _ => true,
            })
            .OrderByDescending(d => d.Date)
            .ThenBy(d => d.ReportNumber)
            .ToList();
    }

    private static List<AutoSafetyDetailDto> BuildViolationDetails(IEnumerable<FmcsaViolation> violations, string category)
    {
        return violations
            .OrderByDescending(v => v.Inspection.InspectionDate)
            .ThenBy(v => v.ReportNumber)
            .Select(v => new AutoSafetyDetailDto
            {
                Category = category,
                Date = v.Inspection.InspectionDate,
                ReportNumber = v.ReportNumber,
                State = v.Inspection.State,
                Basic = v.Basic,
                VehicleInfo = BuildInspectionUnitInfo(v.Inspection, v.UnitNumber),
                Description = v.Description ?? v.ViolationCode,
                IsOutOfService = v.IsOutOfService || v.IsDriverDisqualifying,
            })
            .ToList();
    }

    private static string? BuildViolationCode(Dictionary<string, JsonElement> row)
    {
        var code = GetString(row, "violation_code", "viol_code", "code", "viol_code");
        if (!string.IsNullOrWhiteSpace(code))
            return code;

        var part = GetString(row, "part_no");
        var section = GetString(row, "part_no_section");
        if (!string.IsNullOrWhiteSpace(part) && !string.IsNullOrWhiteSpace(section))
            return $"{part}.{section}";

        return GetString(row, "insp_violation_id", "viol_code");
    }

    private static string? BuildViolationDescription(Dictionary<string, JsonElement> row)
    {
        var description = GetString(row, "description", "violation_description", "viol_desc", "section_desc");
        if (!string.IsNullOrWhiteSpace(description))
            return description;

        var unit = GetString(row, "viol_unit", "insp_viol_unit");
        var category = GetString(row, "group_desc", "insp_violation_category_id");
        var citation = GetString(row, "citation_number");
        var parts = new[]
        {
            !string.IsNullOrWhiteSpace(unit) ? $"Unit {unit}" : null,
            !string.IsNullOrWhiteSpace(category) ? $"Category {category}" : null,
            !string.IsNullOrWhiteSpace(citation) ? $"Citation {citation}" : null,
        }.Where(p => !string.IsNullOrWhiteSpace(p));

        var summary = string.Join(" - ", parts);
        return string.IsNullOrWhiteSpace(summary) ? null : summary;
    }

    private static List<AutoSafetyDetailDto> BuildInspectionDetails(List<FmcsaInspection> inspections, List<FmcsaViolation> violations)
    {
        var violationsByReport = violations
            .GroupBy(v => v.ReportNumber, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        return inspections
            .OrderByDescending(i => i.InspectionDate)
            .ThenBy(i => i.ReportNumber)
            .Select(i =>
            {
                var reportViolations = violationsByReport.GetValueOrDefault(i.ReportNumber) ?? [];
                var oosCount = reportViolations.Count(v => v.IsOutOfService || v.IsDriverDisqualifying);
                return new AutoSafetyDetailDto
                {
                    Category = i.DriverOutOfService || i.VehicleOutOfService || i.HazmatOutOfService ? "OOS inspection" : "Inspection",
                    Date = i.InspectionDate,
                    ReportNumber = i.ReportNumber,
                    State = i.State,
                    CountyCode = i.InspectionCounty ?? i.CountyCodeState,
                    Location = i.InspectionLocation,
                    Conditions = BuildInspectionConditions(i),
                    VehicleInfo = BuildInspectionVehicleInfo(i),
                    Description = BuildInspectionDescription(i, reportViolations),
                    IsOutOfService = i.DriverOutOfService || i.VehicleOutOfService || i.HazmatOutOfService || oosCount > 0,
                    Basic = reportViolations.Count == 0 ? null : $"{reportViolations.Count} violations, {oosCount} OOS",
                };
            })
            .ToList();
    }

    private static string BuildInspectionDescription(FmcsaInspection inspection, List<FmcsaViolation> violations)
    {
        var oosTypes = new List<string>();
        if (inspection.DriverOutOfService) oosTypes.Add("driver OOS");
        if (inspection.VehicleOutOfService) oosTypes.Add("vehicle OOS");
        if (inspection.HazmatOutOfService) oosTypes.Add("hazmat OOS");

        var level = inspection.InspectionLevel.HasValue ? $"Level {inspection.InspectionLevel.Value} inspection" : "Inspection";
        var violationSummary = violations.Count == 0
            ? "no imported violations"
            : $"{violations.Count} imported violation{(violations.Count == 1 ? "" : "s")}";
        var oosSummary = oosTypes.Count == 0 ? null : $" ({string.Join(", ", oosTypes)})";

        return $"{level} with {violationSummary}{oosSummary}.";
    }

    private static string? BuildInspectionConditions(FmcsaInspection inspection)
    {
        var parts = new[]
        {
            !string.IsNullOrWhiteSpace(inspection.InspectionFacility) ? $"Facility: {inspection.InspectionFacility}" : null,
            !string.IsNullOrWhiteSpace(inspection.StartTime) || !string.IsNullOrWhiteSpace(inspection.EndTime) ? $"Time: {inspection.StartTime} - {inspection.EndTime}".Trim() : null,
            inspection.PostCrash.HasValue ? $"Post crash: {(inspection.PostCrash.Value ? "Yes" : "No")}" : null,
            inspection.HazmatPlacardRequired.HasValue ? $"HM placard required: {(inspection.HazmatPlacardRequired.Value ? "Yes" : "No")}" : null,
            !string.IsNullOrWhiteSpace(inspection.InspectionLevelDescription) ? $"Level: {inspection.InspectionLevelDescription}" : null,
        }.Where(p => !string.IsNullOrWhiteSpace(p));

        var value = string.Join(" | ", parts);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? BuildInspectionVehicleInfo(FmcsaInspection inspection)
    {
        var unit1 = BuildInspectionUnitInfo(inspection, "1");
        var unit2 = BuildInspectionUnitInfo(inspection, "2");
        return string.Join(" || ", new[] { unit1, unit2 }.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private static string? BuildInspectionUnitInfo(FmcsaInspection inspection, string? unitNumber)
    {
        var useSecondUnit = unitNumber == "2";
        var unitType = useSecondUnit ? inspection.UnitType2 : inspection.UnitType;
        var unitMake = useSecondUnit ? inspection.UnitMake2 : inspection.UnitMake;
        var unitLicense = useSecondUnit ? inspection.UnitLicense2 : inspection.UnitLicense;
        var unitLicenseState = useSecondUnit ? inspection.UnitLicenseState2 : inspection.UnitLicenseState;
        var vin = useSecondUnit ? inspection.Vin2 : inspection.Vin;

        var parts = new[]
        {
            !string.IsNullOrWhiteSpace(unitNumber) ? $"Unit: {unitNumber}" : null,
            !string.IsNullOrWhiteSpace(unitType) ? $"Type: {unitType}" : null,
            !string.IsNullOrWhiteSpace(unitMake) ? $"Make: {unitMake}" : null,
            !string.IsNullOrWhiteSpace(vin) ? $"VIN: {vin}" : null,
            !string.IsNullOrWhiteSpace(unitLicense) ? $"Plate: {unitLicenseState} {unitLicense}".Trim() : null,
        }.Where(p => !string.IsNullOrWhiteSpace(p));

        var value = string.Join(" | ", parts);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static List<AutoSafetyTrendBucketDto> BuildInspectionTrend(List<FmcsaInspection> inspections, List<FmcsaViolation> violations, DateOnly today)
    {
        var oosReports = inspections
            .Where(i => i.DriverOutOfService || i.VehicleOutOfService || i.HazmatOutOfService)
            .Select(i => i.ReportNumber)
            .Concat(violations.Where(v => v.IsOutOfService || v.IsDriverDisqualifying).Select(v => v.ReportNumber))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return TrendBuckets.Select(bucket =>
        {
            var bucketInspections = inspections
                .Where(i => IsInTrendBucket(i.InspectionDate, bucket, today))
                .ToList();
            var oosCount = bucketInspections.Count(i => oosReports.Contains(i.ReportNumber));
            return new AutoSafetyTrendBucketDto
            {
                Label = bucket.Label,
                TotalCount = bucketInspections.Count,
                OutOfServiceCount = oosCount,
                OutOfServiceRate = bucketInspections.Count == 0 ? null : Math.Round(oosCount * 100m / bucketInspections.Count, 2),
            };
        }).ToList();
    }

    private static List<AutoSafetyTrendBucketDto> BuildViolationTrend(List<FmcsaViolation> violations, DateOnly today)
    {
        return TrendBuckets.Select(bucket =>
        {
            var bucketViolations = violations
                .Where(v => IsInTrendBucket(v.Inspection.InspectionDate, bucket, today))
                .ToList();
            var oosCount = bucketViolations.Count(v => v.IsOutOfService || v.IsDriverDisqualifying);
            return new AutoSafetyTrendBucketDto
            {
                Label = bucket.Label,
                TotalCount = bucketViolations.Count,
                OutOfServiceCount = oosCount,
                OutOfServiceRate = bucketViolations.Count == 0 ? null : Math.Round(oosCount * 100m / bucketViolations.Count, 2),
            };
        }).ToList();
    }

    private static bool IsInTrendBucket(DateOnly date, string label, DateOnly today)
    {
        var bucket = TrendBuckets.FirstOrDefault(b => b.Label.Equals(label, StringComparison.OrdinalIgnoreCase));
        return bucket.Label != null && IsInTrendBucket(date, bucket, today);
    }

    private static bool IsInTrendBucket(DateOnly date, (string Label, int FromMonthsAgo, int ToMonthsAgo) bucket, DateOnly today)
    {
        var bucketStart = today.AddMonths(-bucket.ToMonthsAgo);
        var bucketEnd = today.AddMonths(-bucket.FromMonthsAgo);
        return date >= bucketStart && date < bucketEnd;
    }

    private static string BuildCrashConditions(FmcsaCrash crash)
    {
        var parts = new[]
        {
            FormatMappedCode("Light", crash.LightConditionId, LightConditions),
            FormatMappedCode("Weather", crash.WeatherConditionId, WeatherConditions),
            FormatMappedCode("Road Surface", crash.RoadSurfaceConditionId, RoadSurfaceConditions),
            FormatMappedCode("Roadway Trafficway", crash.TrafficwayId, TrafficwayTypes),
            crash.VehiclesInAccident.HasValue ? $"Vehicles: {crash.VehiclesInAccident.Value}" : null,
        }.Where(p => !string.IsNullOrWhiteSpace(p));

        return string.Join(" | ", parts);
    }

    private static string BuildCrashVehicleInfo(FmcsaCrash crash)
    {
        var decodedVehicle = string.Join(" ", new[]
        {
            crash.VehicleYear?.ToString(CultureInfo.InvariantCulture),
            crash.VehicleMake,
            crash.VehicleModel,
        }.Where(p => !string.IsNullOrWhiteSpace(p)));

        var parts = new[]
        {
            !string.IsNullOrWhiteSpace(decodedVehicle) ? decodedVehicle : null,
            FormatMappedCode("Type", crash.VehicleConfigurationId, VehicleConfigurationTypes),
            FormatMappedCode("Cargo Body", crash.CargoBodyTypeId, CargoBodyTypes),
            FormatMappedCode("GVW Range", crash.GvwRatingId, GvwRanges),
            !string.IsNullOrWhiteSpace(crash.VehicleIdentificationNumber) ? $"VIN: {crash.VehicleIdentificationNumber}" : null,
            !string.IsNullOrWhiteSpace(crash.VehicleLicenseNumber) ? $"Plate: {crash.VehicleLicenseState} {crash.VehicleLicenseNumber}".Trim() : null,
            crash.HazmatPlacard ? "HM placard: Yes" : null,
            crash.HazmatReleased ? "HM released: Yes" : null,
        }.Where(p => !string.IsNullOrWhiteSpace(p));

        return string.Join(" | ", parts);
    }

    private static string BuildCrashDescription(FmcsaCrash crash, bool fatal, bool injury, bool tow)
    {
        var severity = fatal ? "Fatal" : injury ? "Injury" : tow ? "Tow-away" : "Reportable";
        var vehicles = crash.VehiclesInAccident.HasValue ? $" involving {crash.VehiclesInAccident.Value} vehicle{(crash.VehiclesInAccident.Value == 1 ? "" : "s")}" : string.Empty;
        return $"{severity} federally recordable crash{vehicles}.";
    }

    private static string BuildCrashEvents(FmcsaCrash crash, bool fatal, bool injury, bool tow)
    {
        var events = new List<string>();
        if (fatal) events.Add("Fatality reported");
        if (injury) events.Add("Injury reported");
        if (tow) events.Add("Tow-away reported");
        if (events.Count == 0) events.Add("Federally recordable crash");
        return string.Join(" | ", events);
    }

    private static string? FormatMappedCode(string label, string? value, IReadOnlyDictionary<string, string> map)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return map.TryGetValue(value, out var mapped)
            ? $"{label}: {mapped}"
            : $"{label}: Code {value}";
    }

    private async Task<VinDecodeResult?> DecodeVinAsync(string? vin, Dictionary<string, VinDecodeResult> cache, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(vin) || vin.Length < 11)
            return null;

        if (cache.TryGetValue(vin, out var cached))
            return cached;

        try
        {
            var client = _httpFactory.CreateClient("nhtsa_vpic");
            var response = await client.GetAsync($"/api/vehicles/DecodeVinValues/{Uri.EscapeDataString(vin)}?format=json", ct);
            if (!response.IsSuccessStatusCode)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var payload = await JsonSerializer.DeserializeAsync<VpicDecodeResponse>(stream, cancellationToken: ct);
            var result = payload?.Results?.FirstOrDefault();
            if (result == null)
                return null;

            var decoded = new VinDecodeResult(
                int.TryParse(result.ModelYear, NumberStyles.Integer, CultureInfo.InvariantCulture, out var year) ? year : null,
                NullIfBlank(result.Make),
                NullIfBlank(result.Model));

            cache[vin] = decoded;
            return decoded;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogDebug(ex, "VIN decode failed for {Vin}", vin);
            return null;
        }
    }

    private sealed record VinDecodeResult(int? Year, string? Make, string? Model);

    private sealed class VpicDecodeResponse
    {
        public List<VpicDecodeResult> Results { get; set; } = [];
    }

    private sealed class VpicDecodeResult
    {
        public string? ModelYear { get; set; }
        public string? Make { get; set; }
        public string? Model { get; set; }
    }

    private sealed record SmsBasicMapping(
        string Basic,
        string Measure,
        string Percentile,
        string Alert,
        string RoadsideAlert,
        string AlertCode,
        string EventCount);

    private static void ApplyViolationOosToInspection(FmcsaInspection inspection, FmcsaViolation violation)
    {
        if (IsDriverOosViolation(violation))
        {
            inspection.DriverOutOfService = true;
            return;
        }

        if (IsHazmatOosViolation(violation))
        {
            inspection.HazmatOutOfService = true;
            return;
        }

        if (IsVehicleOosViolation(violation))
            inspection.VehicleOutOfService = true;
    }

    private static bool IsDriverOosViolation(FmcsaViolation violation)
    {
        if (violation.IsDriverDisqualifying)
            return true;

        var text = BuildViolationText(violation);
        return ContainsAny(text, "driver", "drv", "license", "medical", "hours", "hos", "log", "substance", "alcohol", "disqual")
            || IsDriverBasic(violation.Basic);
    }

    private static bool IsVehicleOosViolation(FmcsaViolation violation)
    {
        var text = BuildViolationText(violation);
        return ContainsAny(text, "vehicle", "veh", "brake", "tire", "lamp", "light", "steer", "load")
            || IsVehicleBasic(violation.Basic);
    }

    private static bool IsHazmatOosViolation(FmcsaViolation violation)
    {
        var text = BuildViolationText(violation);
        return ContainsAny(text, "hazmat", "hazardous", "hm")
            || violation.Basic == "Hazardous Materials Compliance";
    }

    private static bool IsVehicleInspection(FmcsaInspection inspection)
    {
        return inspection.VehicleOutOfService
            || inspection.VehicleViolationCount > 0
            || inspection.InspectionLevel is 1 or 2 or 5 or 6;
    }

    private static string BuildViolationText(FmcsaViolation violation)
    {
        return string.Join(' ', violation.Basic, violation.ViolationGroup, violation.ViolationCode, violation.Description)
            .ToLowerInvariant();
    }

    private static bool IsDriverBasic(string? basic)
    {
        return basic is "Hours-of-Service Compliance" or "Controlled Substances/Alcohol" or "Driver Fitness";
    }

    private static bool IsVehicleBasic(string? basic)
    {
        return basic is "Vehicle Maintenance" or "Hazardous Materials Compliance";
    }

    private static bool ContainsAny(string value, params string[] terms)
    {
        return terms.Any(value.Contains);
    }

    private static List<AutoSafetyHotspotDto> BuildHotspots(List<FmcsaInspection> inspections, List<FmcsaViolation> violations)
    {
        var violationsByLocation = violations
            .Where(v => !string.IsNullOrWhiteSpace(v.Inspection.State))
            .GroupBy(v => BuildInspectionLocationLabel(v.Inspection))
            .ToDictionary(g => g.Key, g => g.Count());

        return inspections
            .Where(i => !string.IsNullOrWhiteSpace(i.State))
            .GroupBy(BuildInspectionLocationLabel)
            .Select(g => new AutoSafetyHotspotDto
            {
                State = g.Key,
                InspectionCount = g.Count(),
                ViolationCount = violationsByLocation.GetValueOrDefault(g.Key),
                OutOfServiceCount = g.Count(i => i.DriverOutOfService || i.VehicleOutOfService || i.HazmatOutOfService),
            })
            .OrderByDescending(h => h.OutOfServiceCount)
            .ThenByDescending(h => h.ViolationCount)
            .ThenByDescending(h => h.InspectionCount)
            .Take(5)
            .ToList();
    }

    private static string BuildInspectionLocationLabel(FmcsaInspection inspection)
    {
        if (!string.IsNullOrWhiteSpace(inspection.InspectionCounty))
            return !string.IsNullOrWhiteSpace(inspection.State)
                ? $"{inspection.InspectionCounty}, {inspection.State}"
                : inspection.InspectionCounty;

        var state = inspection.State?.Trim().ToUpperInvariant();
        var countyCodeState = inspection.CountyCodeState?.Trim().ToUpperInvariant();

        if (!string.IsNullOrWhiteSpace(countyCodeState) && !string.Equals(countyCodeState, state, StringComparison.OrdinalIgnoreCase))
            return !string.IsNullOrWhiteSpace(state) ? $"{countyCodeState}, {state}" : countyCodeState;

        return !string.IsNullOrWhiteSpace(state) ? state : "Unknown";
    }

    private static string? ResolveCountyName(string? state, string? countyCode) =>
        FmcsaCountyLookup.GetCountyName(state, countyCode);

    private static string? FormatInspectionFacility(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "F" => "Fixed",
        "R" => "Roadside",
        _ => string.IsNullOrWhiteSpace(value) ? null : value.Trim(),
    };

    private static string? FormatInspectionLevel(int? value) => value switch
    {
        1 => "1 - Full",
        2 => "2 - Walk-Around",
        3 => "3 - Driver-Only",
        4 => "4 - Special Study",
        5 => "5 - Terminal",
        99 => "99 - Invalid",
        _ => value?.ToString(CultureInfo.InvariantCulture),
    };

    private static string? FormatMilitaryTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = new string(value.Where(char.IsDigit).ToArray()).PadLeft(4, '0');
        if (digits == "9999") return null;
        return digits.Length == 4 ? $"{digits[..2]}:{digits[2..]}" : value.Trim();
    }

    private static AutoSafetyRadiusSummaryDto BuildRadiusSummary(Insured? insured, List<FmcsaInspection> inspections, List<FmcsaViolation> violations)
    {
        var bands = new[]
        {
            new AutoSafetyRadiusBandDto { Label = "<50 mi" },
            new AutoSafetyRadiusBandDto { Label = "50-100 mi" },
            new AutoSafetyRadiusBandDto { Label = "100-250 mi" },
            new AutoSafetyRadiusBandDto { Label = "250+ mi" },
            new AutoSafetyRadiusBandDto { Label = "Unknown" },
        };

        var summary = new AutoSafetyRadiusSummaryDto
        {
            HasBaseCoordinate = insured?.Latitude != null && insured.Longitude != null,
            Precision = "Mixed precision",
            Note = "Distances use stored/geocoded inspection locations first. Rows without inspection coordinates fall back to low-precision state-centroid estimates.",
            Bands = bands.ToList(),
        };

        if (insured?.Latitude == null || insured.Longitude == null)
        {
            summary.Precision = "Unavailable";
            summary.Note = "Insured coordinates are not cached yet. Select or save the insured address to geocode the base location.";
            bands.Last().InspectionCount = inspections.Count;
            bands.Last().OutOfServiceCount = CountInspectionOos(inspections, violations);
            summary.PrecisionCounts = [new AutoSafetyRadiusPrecisionDto { Label = "Unknown", Count = inspections.Count }];
            return summary;
        }

        var oosReports = violations
            .Where(v => v.IsOutOfService || v.IsDriverDisqualifying)
            .Select(v => v.ReportNumber)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var inspectionGeocodeCount = 0;
        var countyEstimateCount = 0;
        var stateEstimateCount = 0;
        var unknownCount = 0;

        foreach (var inspection in inspections)
        {
            var band = bands.Last();
            if (inspection.Latitude != null && inspection.Longitude != null)
            {
                if (IsCountyEstimate(inspection.GeocodePrecision))
                    countyEstimateCount++;
                else
                    inspectionGeocodeCount++;

                var miles = HaversineMiles(
                    (double)insured.Latitude.Value,
                    (double)insured.Longitude.Value,
                    (double)inspection.Latitude.Value,
                    (double)inspection.Longitude.Value);
                band = miles switch
                {
                    < 50 => bands[0],
                    < 100 => bands[1],
                    < 250 => bands[2],
                    _ => bands[3],
                };
            }
            else if (!string.IsNullOrWhiteSpace(inspection.State) && StateCentroids.TryGetValue(inspection.State.Trim().ToUpperInvariant(), out var point))
            {
                stateEstimateCount++;
                var miles = HaversineMiles((double)insured.Latitude.Value, (double)insured.Longitude.Value, point.Latitude, point.Longitude);
                band = miles switch
                {
                    < 50 => bands[0],
                    < 100 => bands[1],
                    < 250 => bands[2],
                    _ => bands[3],
                };
            }
            else
            {
                unknownCount++;
            }

            band.InspectionCount++;
            if (inspection.DriverOutOfService || inspection.VehicleOutOfService || inspection.HazmatOutOfService || oosReports.Contains(inspection.ReportNumber))
                band.OutOfServiceCount++;
        }

        summary.PrecisionCounts =
        [
            new AutoSafetyRadiusPrecisionDto { Label = "Inspection geocode", Count = inspectionGeocodeCount },
            new AutoSafetyRadiusPrecisionDto { Label = "County estimate", Count = countyEstimateCount },
            new AutoSafetyRadiusPrecisionDto { Label = "State estimate", Count = stateEstimateCount },
            new AutoSafetyRadiusPrecisionDto { Label = "Unknown", Count = unknownCount },
        ];

        if (inspectionGeocodeCount == inspections.Count)
        {
            summary.Precision = "Inspection geocode";
            summary.Note = "Distances use stored/geocoded inspection locations.";
        }
        else if (countyEstimateCount > 0 || stateEstimateCount > 0)
        {
            summary.Precision = "Mixed precision";
            summary.Note = "Some inspections use stored/geocoded locations. Rows without inspection coordinates use county estimates when available, then low-precision state-centroid estimates.";
        }

        return summary;
    }

    private static bool IsCountyEstimate(string? precision) =>
        precision?.StartsWith("County estimate", StringComparison.OrdinalIgnoreCase) == true;

    private static int CountInspectionOos(List<FmcsaInspection> inspections, List<FmcsaViolation> violations)
    {
        var oosReports = violations
            .Where(v => v.IsOutOfService || v.IsDriverDisqualifying)
            .Select(v => v.ReportNumber)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return inspections.Count(i => i.DriverOutOfService || i.VehicleOutOfService || i.HazmatOutOfService || oosReports.Contains(i.ReportNumber));
    }

    private static double HaversineMiles(double lat1, double lon1, double lat2, double lon2)
    {
        const double radiusMiles = 3958.8;
        static double ToRadians(double degrees) => degrees * Math.PI / 180;

        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Pow(Math.Sin(dLat / 2), 2)
            + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) * Math.Pow(Math.Sin(dLon / 2), 2);
        return radiusMiles * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static List<AutoSafetyEventDto> BuildSevereEvents(List<FmcsaViolation> violations)
    {
        return violations
            .Where(v => v.IsOutOfService || v.IsDriverDisqualifying || v.SeverityWeight >= 2)
            .OrderByDescending(v => v.Inspection.InspectionDate)
            .ThenByDescending(v => v.SeverityWeight)
            .Take(20)
            .Select(v => new AutoSafetyEventDto
            {
                Date = v.Inspection.InspectionDate,
                EventType = v.IsDriverDisqualifying ? "Driver disqualifying" : v.IsOutOfService ? "Out of service" : "High severity",
                State = v.Inspection.State,
                Description = v.Description ?? v.ViolationCode,
                Basic = v.Basic,
                SeverityWeight = v.SeverityWeight,
            })
            .ToList();
    }

    private static List<string> BuildFlags(List<AutoSafetyBasicDto> basics, AutoSafetyOosDto oos, FmcsaCarrierSnapshot? carrier, FmcsaScoringRun? scoringRun)
    {
        var flags = new List<string>();
        flags.AddRange(basics.Where(b => b.IsPrioritized).Select(b => $"{b.Basic} prioritized"));
        if (oos.VehicleOosRate >= 25m) flags.Add("Vehicle OOS elevated");
        if (oos.DriverOosRate >= 15m) flags.Add("Driver OOS elevated");
        if (oos.InspectionCount == 0) flags.Add("No inspections in 24-month window");
        if (carrier == null) flags.Add("Carrier census not imported");
        if (scoringRun == null) flags.Add("Calculated score not available");
        return flags.Distinct().Take(6).ToList();
    }

    private static AutoSafetyIssDto BuildIss(List<AutoSafetyBasicDto> basics, FmcsaCarrierSnapshot? carrier, List<FmcsaInspection> inspections, List<FmcsaViolation> violations)
    {
        if (basics.Any(b => b.IsPrioritized))
        {
            return BuildIssResult(100, "Safety", "FMCSA BASIC alert or serious violation indicator present.");
        }

        var availablePercentiles = basics
            .Where(b => b.Percentile.HasValue)
            .Select(b => b.Percentile!.Value)
            .ToList();

        if (availablePercentiles.Count > 0)
        {
            var score = Math.Clamp((int)Math.Round(availablePercentiles.Max(), MidpointRounding.AwayFromZero), 1, 100);
            var source = basics.Any(b => b.ScoreSource == "Official SMS" && b.Percentile.HasValue)
                ? "the highest official FMCSA BASIC percentile available"
                : "the highest SIMS peer percentile available";
            return BuildIssResult(score, "Safety", $"Estimated from {source}.");
        }

        if (carrier?.PowerUnits is int powerUnits)
        {
            var inspectionCount = inspections.Count;
            var oosCount = violations.Count(v => v.IsOutOfService || v.IsDriverDisqualifying)
                + inspections.Count(i => i.DriverOutOfService || i.VehicleOutOfService || i.HazmatOutOfService);
            var score = 50 + Math.Min(35, powerUnits / 2) - Math.Min(25, inspectionCount * 4) + Math.Min(20, oosCount * 3);
            score = Math.Clamp(score, 1, 95);

            return BuildIssResult(
                score,
                "Insufficient Data",
                "Estimated from fleet size, inspection volume, and OOS activity because official BASIC percentiles are not available.");
        }

        return new AutoSafetyIssDto
        {
            Status = "Unknown",
            Label = "Pending",
            Basis = "Pending",
            Explanation = "Not enough FMCSA data is available to estimate an ISS recommendation.",
            Source = "Official ISS is not available from the public Socrata feed; configure FMCSA QCMobile for official BASIC percentiles.",
        };
    }

    private static AutoSafetyIssDto BuildIssResult(int score, string basis, string explanation)
    {
        var status = score >= 75 ? "Red" : score >= 50 ? "Yellow" : "Green";
        var recommendation = status switch
        {
            "Red" => "Inspect",
            "Yellow" => "Optional",
            _ => "Pass",
        };

        return new AutoSafetyIssDto
        {
            Score = score,
            Status = status,
            Label = $"{recommendation} estimate",
            Basis = basis,
            Explanation = explanation,
            Source = "SIMS estimate using FMCSA safety data; official ISS values require FMCSA ISS/Portal data.",
        };
    }

    private static string DetermineRiskLevel(List<AutoSafetyBasicDto> basics, AutoSafetyOosDto oos, List<AutoSafetyEventDto> severeEvents)
    {
        if (basics.Any(b => b.IsPrioritized || b.Percentile >= 90m) || severeEvents.Count >= 3)
            return "High";
        if (basics.Any(b => b.Percentile >= 75m) || oos.VehicleOosRate >= 25m || oos.DriverOosRate >= 15m)
            return "Watch";
        if (oos.InspectionCount == 0)
            return "Unknown";
        return "Acceptable";
    }

    private static string? NormalizeDotNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = new string(value.Where(char.IsDigit).ToArray());
        return string.IsNullOrWhiteSpace(digits) ? null : digits;
    }

    private static string BuildSaveFailureMessage(DbUpdateException ex)
    {
        var detail = ex.GetBaseException().Message;
        return string.IsNullOrWhiteSpace(detail)
            ? "FMCSA data was found but could not be saved. Check that the latest database migration is applied."
            : $"FMCSA data was found but could not be saved: {detail}";
    }

    private static string? NormalizeBasic(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        return normalized switch
        {
            "Fatigued Driving" => "Hours-of-Service Compliance",
            "HOS Compliance" => "Hours-of-Service Compliance",
            "Controlled Substance/Alcohol" => "Controlled Substances/Alcohol",
            "Controlled Substances" => "Controlled Substances/Alcohol",
            "Drugs/Alcohol" => "Controlled Substances/Alcohol",
            "HM Compliance" => "Hazardous Materials Compliance",
            _ => normalized,
        };
    }

    private static string? NormalizeQcMobileBasic(Dictionary<string, JsonElement> row)
    {
        var basicName = GetString(row, "basicDesc", "basicShortDesc", "basic", "basicName");
        var normalized = NormalizeBasic(basicName);
        if (!string.IsNullOrWhiteSpace(normalized) && QcMobileBasicNames.TryGetValue(normalized, out var match))
            return match;

        return normalized != null && QcMobileBasicNames.TryGetValue(normalized, out match) ? match : null;
    }

    private static string? GetString(Dictionary<string, JsonElement> row, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetValue(row, name, out var value)) continue;
            if (value.ValueKind == JsonValueKind.String) return NullIfBlank(value.GetString());
            if (value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False) return value.ToString();
        }

        return null;
    }

    private static int? GetInt(Dictionary<string, JsonElement> row, params string[] names)
    {
        var raw = GetString(row, names);
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static decimal? GetDecimal(Dictionary<string, JsonElement> row, params string[] names)
    {
        var raw = GetString(row, names);
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static DateOnly? GetDate(Dictionary<string, JsonElement> row, params string[] names)
    {
        var raw = GetString(row, names);
        if (DateOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return date;
        if (raw?.Length == 8
            && DateOnly.TryParseExact(raw, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var compactDate))
            return compactDate;
        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dateTime))
            return DateOnly.FromDateTime(dateTime);
        return null;
    }

    private static bool GetBool(Dictionary<string, JsonElement> row, params string[] names)
    {
        var raw = GetString(row, names);
        if (string.IsNullOrWhiteSpace(raw)) return false;
        return raw.Equals("true", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("y", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("1", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("x", StringComparison.OrdinalIgnoreCase);
    }

    private static bool? GetNullableBool(Dictionary<string, JsonElement> row, params string[] names)
    {
        var raw = GetString(row, names);
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (raw.Equals("true", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("y", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("1", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("x", StringComparison.OrdinalIgnoreCase))
            return true;
        if (raw.Equals("false", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("n", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("no", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("0", StringComparison.OrdinalIgnoreCase))
            return false;
        return null;
    }

    private static bool GetCrashFlag(Dictionary<string, JsonElement> row, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetValue(row, name, out var value)) continue;

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
                return number > 0;

            var raw = value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : value.ValueKind is JsonValueKind.True or JsonValueKind.False or JsonValueKind.Number
                    ? value.ToString()
                    : null;

            if (string.IsNullOrWhiteSpace(raw)) continue;
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                return parsed > 0;
            if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue))
                return decimalValue > 0;

            if (raw.Equals("true", StringComparison.OrdinalIgnoreCase)
                || raw.Equals("y", StringComparison.OrdinalIgnoreCase)
                || raw.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || raw.Equals("x", StringComparison.OrdinalIgnoreCase))
                return true;

            if (raw.Equals("false", StringComparison.OrdinalIgnoreCase)
                || raw.Equals("n", StringComparison.OrdinalIgnoreCase)
                || raw.Equals("no", StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return false;
    }

    private static bool TryGetValue(Dictionary<string, JsonElement> row, string name, out JsonElement value)
    {
        if (row.TryGetValue(name, out value)) return true;
        var match = row.Keys.FirstOrDefault(k => k.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (match == null)
        {
            value = default;
            return false;
        }

        value = row[match];
        return true;
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
