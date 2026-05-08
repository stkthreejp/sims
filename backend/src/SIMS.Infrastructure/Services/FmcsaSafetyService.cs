using SIMS.Application.Common;
using SIMS.Application.DTOs.Quotes;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Entities.Fmcsa;
using SIMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;

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

    private readonly ApplicationDbContext _db;
    private readonly FmcsaSocrataClient _socrata;
    private readonly ILogger<FmcsaSafetyService> _logger;

    public FmcsaSafetyService(ApplicationDbContext db, FmcsaSocrataClient socrata, ILogger<FmcsaSafetyService> logger)
    {
        _db = db;
        _socrata = socrata;
        _logger = logger;
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

        var windowStart = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-24));
        var inspections = await _db.FmcsaInspections
            .Where(i => i.UsDotNumber == dotNumber && i.InspectionDate >= windowStart)
            .ToListAsync(ct);
        var violations = await _db.FmcsaViolations
            .Where(v => v.UsDotNumber == dotNumber && v.Inspection.InspectionDate >= windowStart)
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

        var basics = BuildBasics(scoringRun, violations);
        var oos = BuildOos(inspections, violations);
        var accidentSummary = BuildAccidentSummary(crashes, carrier?.PowerUnits);
        var hotspots = BuildHotspots(inspections, violations);
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
            SummaryFlags = flags,
            Basics = basics,
            Oos = oos,
            AccidentSummary = accidentSummary,
            GeographicHotspots = hotspots,
            RecentSevereEvents = severeEvents,
        });
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
        List<Dictionary<string, JsonElement>> inspectionRows;
        List<Dictionary<string, JsonElement>> violationRows;
        List<Dictionary<string, JsonElement>> crashRows;

        try
        {
            carrierRows = await _socrata.GetCensusByDotAsync(dotNumber, ct);
            inspectionRows = await _socrata.GetInspectionsByDotAsync(dotNumber, ct);
            violationRows = await _socrata.GetViolationsByDotAsync(dotNumber, ct);
            crashRows = await _socrata.GetCrashesByDotAsync(dotNumber, ct);
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
            inspectionCount = await UpsertInspectionsAsync(dotNumber, inspectionRows, now, ct);
            violationCount = await UpsertViolationsAsync(dotNumber, violationRows, now, ct);
            crashCount = await UpsertCrashesAsync(dotNumber, crashRows, now, ct);

            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "FMCSA data save failed for USDOT {DotNumber}", dotNumber);
            return Result<AutoSafetyRefreshDto>.Failure(
                "FMCSA_SAVE_FAILED",
                BuildSaveFailureMessage(ex));
        }

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

            inspection.InspectionDate = inspectionDate.Value;
            inspection.State = GetString(row, "state", "inspection_state", "insp_state");
            inspection.InspectionLevel = GetInt(row, "inspection_level", "insp_level", "level");
            inspection.DriverOutOfService = GetBool(row, "driver_oos", "driver_out_of_service", "drv_oos", "driver_oos_indicator", "driver_oos_flag");
            inspection.VehicleOutOfService = GetBool(row, "vehicle_oos", "vehicle_out_of_service", "veh_oos", "vehicle_oos_indicator", "vehicle_oos_flag");
            inspection.HazmatOutOfService = GetBool(row, "hazmat_oos", "hm_oos", "hazmat_out_of_service", "hazmat_oos_indicator", "hazmat_oos_flag");
            inspection.DriverViolationCount = GetInt(row, "driver_violation_count", "drv_violation_count", "driver_violations") ?? 0;
            inspection.VehicleViolationCount = GetInt(row, "vehicle_violation_count", "veh_violation_count", "vehicle_violations") ?? 0;
            inspection.HazmatViolationCount = GetInt(row, "hazmat_violation_count", "hm_violation_count", "hazmat_violations") ?? 0;
            inspection.ImportedAt = now;
            count++;
        }

        return count;
    }

    private async Task<int> UpsertViolationsAsync(string dotNumber, List<Dictionary<string, JsonElement>> rows, DateTime now, CancellationToken ct)
    {
        var count = 0;
        foreach (var row in rows)
        {
            var reportNumber = GetString(row, "report_number", "insp_report_number", "inspection_report_number");
            var violationCode = GetString(row, "violation_code", "viol_code", "code");
            var description = GetString(row, "description", "violation_description", "viol_desc");
            if (string.IsNullOrWhiteSpace(reportNumber) || string.IsNullOrWhiteSpace(violationCode)) continue;
            var inspectionDate = GetDate(row, "inspection_date", "insp_date", "inspection_dt", "inspection_date_dt", "activity_date", "report_date", "date");

            var inspection = await _db.FmcsaInspections
                .FirstOrDefaultAsync(i => i.UsDotNumber == dotNumber && i.ReportNumber == reportNumber, ct);
            if (inspection == null)
            {
                inspection = new FmcsaInspection
                {
                    UsDotNumber = dotNumber,
                    ReportNumber = reportNumber,
                    InspectionDate = inspectionDate ?? DateOnly.FromDateTime(now),
                    State = GetString(row, "state", "inspection_state", "insp_state"),
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
            violation.ViolationGroup = GetString(row, "violation_group", "group_desc", "viol_group", "defect_group", "violation_category", "category", "unit_type");
            violation.IsOutOfService = GetBool(row, "oos_indicator", "is_out_of_service", "out_of_service", "oos", "oos_flag", "out_of_service_indicator");
            violation.IsDriverDisqualifying = GetBool(row, "driver_disqualified", "is_driver_disqualifying");
            violation.SeverityWeight = violation.IsOutOfService || violation.IsDriverDisqualifying ? 2 : 1;
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
        foreach (var row in DeduplicateBy(rows, r => GetString(r, "report_number", "crash_report_number", "crash_id")))
        {
            var reportNumber = GetString(row, "report_number", "crash_report_number", "crash_id");
            if (string.IsNullOrWhiteSpace(reportNumber)) continue;
            var crashDate = GetDate(row, "crash_date", "accident_date", "date");
            if (crashDate == null) continue;

            var crash = await _db.FmcsaCrashes
                .FirstOrDefaultAsync(c => c.UsDotNumber == dotNumber && c.ReportNumber == reportNumber, ct);
            if (crash == null)
            {
                crash = new FmcsaCrash { UsDotNumber = dotNumber, ReportNumber = reportNumber };
                _db.FmcsaCrashes.Add(crash);
            }

            crash.CrashDate = crashDate.Value;
            crash.State = GetString(row, "state", "crash_state");
            crash.TowAway = GetCrashFlag(row, "tow_away", "towaway", "tow_away_indicator", "towaway_indicator", "tow", "tow_away_count", "towaway_count");
            crash.Injury = GetCrashFlag(row, "injury", "injuries", "injury_indicator", "injury_crash", "injury_count", "non_fatal_injuries", "nonfatal_injuries", "number_of_injuries");
            crash.Fatality = GetCrashFlag(row, "fatality", "fatalities", "fatality_indicator", "fatal_crash", "fatal", "fatality_count", "fatal_injuries", "number_of_fatalities");
            crash.SeverityWeight = crash.Fatality ? 3m : crash.Injury ? 2m : 1m;
            crash.TimeWeight = 1m;
            crash.ImportedAt = now;
            count++;
        }

        return count;
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

    private static List<AutoSafetyBasicDto> BuildBasics(FmcsaScoringRun? scoringRun, List<FmcsaViolation> violations)
    {
        if (scoringRun?.BasicScores.Count > 0)
        {
            return scoringRun.BasicScores
                .OrderByDescending(s => s.IsPrioritized)
                .ThenByDescending(s => s.Percentile ?? 0)
                .Select(s => new AutoSafetyBasicDto
                {
                    Basic = s.Basic,
                    Measure = s.Measure,
                    Percentile = s.Percentile,
                    IsPrioritized = s.IsPrioritized,
                    EventCount = s.EventCount,
                    OutOfServiceCount = s.OutOfServiceCount,
                    TrendDirection = s.TrendDirection,
                })
                .ToList();
        }

        var grouped = violations
            .Where(v => !string.IsNullOrWhiteSpace(v.Basic))
            .GroupBy(v => v.Basic!)
            .ToDictionary(g => g.Key, g => g.ToList());

        return Basics.Select(b =>
        {
            grouped.TryGetValue(b, out var events);
            events ??= [];
            return new AutoSafetyBasicDto
            {
                Basic = b,
                EventCount = events.Select(v => new { v.ReportNumber, Group = v.ViolationGroup ?? v.ViolationCode }).Distinct().Count(),
                OutOfServiceCount = events.Count(v => v.IsOutOfService || v.IsDriverDisqualifying),
                TrendDirection = "Flat",
            };
        }).ToList();
    }

    private static AutoSafetyOosDto BuildOos(List<FmcsaInspection> inspections, List<FmcsaViolation> violations)
    {
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
        };
    }

    private static AutoSafetyAccidentSummaryDto BuildAccidentSummary(List<FmcsaCrash> crashes, int? powerUnits)
    {
        var reportableCrashes = crashes
            .Where(c => c.Fatality || c.Injury || c.TowAway)
            .GroupBy(c => new { c.CrashDate, State = c.State ?? string.Empty })
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
        var violationsByState = violations
            .Where(v => !string.IsNullOrWhiteSpace(v.Inspection.State))
            .GroupBy(v => v.Inspection.State!)
            .ToDictionary(g => g.Key, g => g.Count());

        return inspections
            .Where(i => !string.IsNullOrWhiteSpace(i.State))
            .GroupBy(i => i.State!)
            .Select(g => new AutoSafetyHotspotDto
            {
                State = g.Key,
                InspectionCount = g.Count(),
                ViolationCount = violationsByState.GetValueOrDefault(g.Key),
                OutOfServiceCount = g.Count(i => i.DriverOutOfService || i.VehicleOutOfService),
            })
            .OrderByDescending(h => h.OutOfServiceCount)
            .ThenByDescending(h => h.ViolationCount)
            .ThenByDescending(h => h.InspectionCount)
            .Take(5)
            .ToList();
    }

    private static List<AutoSafetyEventDto> BuildSevereEvents(List<FmcsaViolation> violations)
    {
        return violations
            .Where(v => v.IsOutOfService || v.IsDriverDisqualifying || v.SeverityWeight >= 2)
            .OrderByDescending(v => v.Inspection.InspectionDate)
            .ThenByDescending(v => v.SeverityWeight)
            .Take(5)
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
            "HOS Compliance" => "Hours-of-Service Compliance",
            "Controlled Substances" => "Controlled Substances/Alcohol",
            "HM Compliance" => "Hazardous Materials Compliance",
            _ => normalized,
        };
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

    private static DateOnly? GetDate(Dictionary<string, JsonElement> row, params string[] names)
    {
        var raw = GetString(row, names);
        if (DateOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return date;
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
