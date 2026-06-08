using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SIMS.Application.Configuration;
using SIMS.Application.Common;
using SIMS.Application.DTOs.Fmcsa;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities.Fmcsa;
using SIMS.Domain.Entities.FmcsaAnalytics;
using SIMS.Infrastructure.Data;
using System.Globalization;
using System.Text.Json;

namespace SIMS.Infrastructure.Services;

public class FmcsaSafetyAnalyticsService : IFmcsaSafetyAnalyticsService
{
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
    private static readonly OfficialSmsBasicMapping[] OfficialSmsMappings =
    [
        new("Unsafe Driving", ["unsafe_driv_measure"], ["unsafe_driv_pct"], ["unsafe_driv_insp_w_viol", "unsafe_driv_total_viol"], ["unsafe_driv_oos"]),
        new("Crash Indicator", ["crash_indicator_measure", "crash_measure"], ["crash_indicator_pct", "crash_pct"], ["crash_indicator_count", "crash_count"], ["crash_oos"]),
        new("Hours-of-Service Compliance", ["hos_driv_measure", "hos_measure"], ["hos_driv_pct", "hos_pct"], ["hos_driv_insp_w_viol", "hos_total_viol"], ["hos_oos"]),
        new("Vehicle Maintenance", ["veh_maint_measure"], ["veh_maint_pct"], ["veh_maint_insp_w_viol", "veh_maint_total_viol"], ["veh_maint_oos"]),
        new("Controlled Substances/Alcohol", ["contr_subst_measure"], ["contr_subst_pct"], ["contr_subst_insp_w_viol", "contr_subst_total_viol"], ["contr_subst_oos"]),
        new("Hazardous Materials Compliance", ["hm_measure", "hazmat_measure"], ["hm_pct", "hazmat_pct"], ["hm_insp_w_viol", "hazmat_insp_w_viol", "hm_total_viol"], ["hm_oos", "hazmat_oos"]),
        new("Driver Fitness", ["driv_fit_measure"], ["driv_fit_pct"], ["driv_fit_insp_w_viol", "driv_fit_total_viol"], ["driv_fit_oos"]),
    ];

    private readonly ApplicationDbContext _appDb;
    private readonly IServiceProvider _serviceProvider;
    private readonly FmcsaSocrataClient _socrata;
    private readonly FmcsaSocrataSettings _settings;
    private readonly FmcsaJobSettings _jobSettings;

    public FmcsaSafetyAnalyticsService(ApplicationDbContext appDb, IServiceProvider serviceProvider, FmcsaSocrataClient socrata, IOptions<FmcsaSocrataSettings> settings, IOptions<FmcsaJobSettings> jobSettings)
    {
        _appDb = appDb;
        _serviceProvider = serviceProvider;
        _socrata = socrata;
        _settings = settings.Value;
        _jobSettings = jobSettings.Value;
    }

    public async Task<Result<FmcsaAnalyticsStatusDto>> GetStatusAsync(CancellationToken ct = default)
    {
        var analyticsDb = _serviceProvider.GetService<SafetyAnalyticsDbContext>();
        if (analyticsDb == null)
        {
            return Result<FmcsaAnalyticsStatusDto>.Success(new FmcsaAnalyticsStatusDto
            {
                IsConfigured = false,
            });
        }

        var batches = await analyticsDb.FmcsaAnalyticsImportBatches
            .AsNoTracking()
            .OrderByDescending(b => b.StartedAt)
            .Take(10)
            .Select(b => new FmcsaAnalyticsImportBatchDto
            {
                SnapshotMonth = b.SnapshotMonth,
                SourceName = b.SourceName,
                Status = b.Status,
                RowsImported = b.RowsImported,
                StartedAt = b.StartedAt,
                CompletedAt = b.CompletedAt,
                ErrorMessage = b.ErrorMessage,
            })
            .ToListAsync(ct);

        return Result<FmcsaAnalyticsStatusDto>.Success(new FmcsaAnalyticsStatusDto
        {
            IsConfigured = true,
            CarrierPeerSnapshotCount = await analyticsDb.FmcsaCarrierPeerSnapshots.CountAsync(ct),
            BasicPeerMeasureCount = await analyticsDb.FmcsaBasicPeerMeasures.CountAsync(ct),
            HasRunningImport = batches.Any(b => b.Status == "Running" && b.StartedAt > DateTime.UtcNow.AddHours(-4)),
            ScheduledJobs = BuildScheduledJobs(),
            LatestBatches = batches,
        });
    }

    public async Task<Result<FmcsaAnalyticsRefreshDto>> RefreshImportedCarrierAnalyticsAsync(string? snapshotMonth = null, CancellationToken ct = default)
    {
        var analyticsDb = _serviceProvider.GetService<SafetyAnalyticsDbContext>();
        if (analyticsDb == null)
        {
            return Result<FmcsaAnalyticsRefreshDto>.Failure(
                "SAFETY_ANALYTICS_NOT_CONFIGURED",
                "Safety analytics database is not configured. Add ConnectionStrings:SafetyAnalyticsConnection.");
        }

        snapshotMonth = string.IsNullOrWhiteSpace(snapshotMonth)
            ? DateTime.UtcNow.ToString("yyyy-MM")
            : snapshotMonth.Trim();

        var now = DateTime.UtcNow;
        var windowStart = DateOnly.FromDateTime(now.AddMonths(-24));
        var carrierSnapshots = await _appDb.FmcsaCarrierSnapshots
            .AsNoTracking()
            .ToListAsync(ct);
        var latestCarriers = carrierSnapshots
            .GroupBy(c => c.UsDotNumber, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(c => c.SnapshotMonth).First())
            .ToList();

        if (latestCarriers.Count == 0)
        {
            return Result<FmcsaAnalyticsRefreshDto>.Success(new FmcsaAnalyticsRefreshDto
            {
                SnapshotMonth = snapshotMonth,
                RefreshedAt = now,
            });
        }

        var dotNumbers = latestCarriers.Select(c => c.UsDotNumber).ToList();
        var inspections = await _appDb.FmcsaInspections
            .AsNoTracking()
            .Where(i => dotNumbers.Contains(i.UsDotNumber) && i.InspectionDate >= windowStart)
            .ToListAsync(ct);
        var violations = await _appDb.FmcsaViolations
            .AsNoTracking()
            .Include(v => v.Inspection)
            .Where(v => dotNumbers.Contains(v.UsDotNumber) && v.Inspection.InspectionDate >= windowStart)
            .ToListAsync(ct);
        var scoringRuns = await _appDb.FmcsaScoringRuns
            .AsNoTracking()
            .Include(r => r.BasicScores)
            .Where(r => dotNumbers.Contains(r.UsDotNumber))
            .ToListAsync(ct);

        var batch = new FmcsaAnalyticsImportBatch
        {
            SnapshotMonth = snapshotMonth,
            SourceName = "SIMS imported FMCSA carriers",
            Status = "Running",
            StartedAt = now,
        };
        analyticsDb.FmcsaAnalyticsImportBatches.Add(batch);

        var existingCarriers = await analyticsDb.FmcsaCarrierPeerSnapshots
            .Where(c => c.SnapshotMonth == snapshotMonth)
            .ToDictionaryAsync(c => c.UsDotNumber, StringComparer.OrdinalIgnoreCase, ct);
        var existingMeasures = await analyticsDb.FmcsaBasicPeerMeasures
            .Where(m => m.SnapshotMonth == snapshotMonth)
            .ToDictionaryAsync(m => $"{m.UsDotNumber}|{m.Basic}", StringComparer.OrdinalIgnoreCase, ct);

        foreach (var carrier in latestCarriers)
        {
            var carrierInspections = inspections.Where(i => i.UsDotNumber == carrier.UsDotNumber).ToList();
            var carrierViolations = violations.Where(v => v.UsDotNumber == carrier.UsDotNumber).ToList();
            var latestScoringRun = scoringRuns
                .Where(r => r.UsDotNumber == carrier.UsDotNumber)
                .OrderByDescending(r => r.SnapshotMonth)
                .ThenByDescending(r => r.GeneratedAt)
                .FirstOrDefault();

            UpsertCarrierSnapshot(analyticsDb, existingCarriers, carrier, carrierInspections, snapshotMonth);
            foreach (var basic in Basics)
                UpsertBasicMeasure(analyticsDb, existingMeasures, carrier, latestScoringRun, carrierInspections, carrierViolations, basic, snapshotMonth, now);
        }

        await analyticsDb.SaveChangesAsync(ct);
        await RecalculatePercentilesAsync(analyticsDb, snapshotMonth, ct);

        batch.Status = "Completed";
        batch.CompletedAt = DateTime.UtcNow;
        batch.RowsImported = latestCarriers.Count + existingMeasures.Count;
        await analyticsDb.SaveChangesAsync(ct);

        return Result<FmcsaAnalyticsRefreshDto>.Success(new FmcsaAnalyticsRefreshDto
        {
            SnapshotMonth = snapshotMonth,
            CarrierCount = latestCarriers.Count,
            BasicMeasureCount = existingMeasures.Count,
            RefreshedAt = batch.CompletedAt.Value,
        });
    }

    public async Task<Result<FmcsaAnalyticsRefreshDto>> RefreshOfficialSmsPeerAnalyticsAsync(string? snapshotMonth = null, int? maxRowsPerDataset = null, CancellationToken ct = default)
    {
        var analyticsDb = _serviceProvider.GetService<SafetyAnalyticsDbContext>();
        if (analyticsDb == null)
        {
            return Result<FmcsaAnalyticsRefreshDto>.Failure(
                "SAFETY_ANALYTICS_NOT_CONFIGURED",
                "Safety analytics database is not configured. Add ConnectionStrings:SafetyAnalyticsConnection.");
        }

        snapshotMonth = string.IsNullOrWhiteSpace(snapshotMonth)
            ? DateTime.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture)
            : snapshotMonth.Trim();

        var now = DateTime.UtcNow;
        var runningImport = await analyticsDb.FmcsaAnalyticsImportBatches
            .AsNoTracking()
            .AnyAsync(b => b.SourceName == "FMCSA official SMS pass-property population" && b.Status == "Running" && b.StartedAt > now.AddHours(-4), ct);
        if (runningImport)
        {
            return Result<FmcsaAnalyticsRefreshDto>.Failure(
                "FMCSA_ANALYTICS_IMPORT_RUNNING",
                "An SMS peer import is already running. Wait for it to complete before starting another one.");
        }

        var pageSize = Math.Clamp(_settings.AnalyticsPageSize, 1, 50000);
        var maxRows = Math.Max(pageSize, maxRowsPerDataset ?? _settings.AnalyticsMaxRowsPerDataset);
        var batch = new FmcsaAnalyticsImportBatch
        {
            SnapshotMonth = snapshotMonth,
            SourceName = "FMCSA official SMS pass-property population",
            Status = "Running",
            StartedAt = now,
        };
        analyticsDb.FmcsaAnalyticsImportBatches.Add(batch);
        await analyticsDb.SaveChangesAsync(ct);

        var carriersSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var measureCount = 0;

        try
        {
            for (var offset = 0; offset < maxRows; offset += pageSize)
            {
                var rows = await _socrata.GetSmsAbPassPropertyPageAsync(pageSize, offset, ct);
                if (rows.Count == 0) break;

                var counts = await UpsertOfficialSmsRowsAsync(analyticsDb, rows, snapshotMonth, carriersSeen, ct);
                measureCount += counts.BasicMeasures;
                if (rows.Count < pageSize) break;
            }

            for (var offset = 0; offset < maxRows; offset += pageSize)
            {
                var rows = await _socrata.GetSmsCPassPropertyPageAsync(pageSize, offset, ct);
                if (rows.Count == 0) break;

                var counts = await UpsertOfficialSmsRowsAsync(analyticsDb, rows, snapshotMonth, carriersSeen, ct);
                measureCount += counts.BasicMeasures;
                if (rows.Count < pageSize) break;
            }

            await RecalculatePercentilesAsync(analyticsDb, snapshotMonth, ct);

            batch.Status = "Completed";
            batch.CompletedAt = DateTime.UtcNow;
            batch.RowsImported = carriersSeen.Count + measureCount;
            await analyticsDb.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or DbUpdateException or JsonException or OperationCanceledException)
        {
            batch.Status = "Failed";
            batch.CompletedAt = DateTime.UtcNow;
            batch.ErrorMessage = ex.GetBaseException().Message;
            await analyticsDb.SaveChangesAsync(CancellationToken.None);

            return Result<FmcsaAnalyticsRefreshDto>.Failure("FMCSA_ANALYTICS_IMPORT_FAILED", batch.ErrorMessage);
        }

        return Result<FmcsaAnalyticsRefreshDto>.Success(new FmcsaAnalyticsRefreshDto
        {
            SnapshotMonth = snapshotMonth,
            CarrierCount = carriersSeen.Count,
            BasicMeasureCount = measureCount,
            RefreshedAt = batch.CompletedAt!.Value,
        });
    }

    private List<FmcsaScheduledJobDto> BuildScheduledJobs()
    {
        var now = DateTime.UtcNow;
        return
        [
            new()
            {
                Name = "Imported carrier analytics",
                Enabled = _jobSettings.Enabled && _jobSettings.RunImportedCarrierAnalytics,
                Schedule = $"Daily at {_jobSettings.DailyRunTimeUtc} UTC",
                NextRunAtUtc = _jobSettings.Enabled && _jobSettings.RunImportedCarrierAnalytics
                    ? NextDailyRun(now, ParseTime(_jobSettings.DailyRunTimeUtc))
                    : null,
                Status = _jobSettings.Enabled && _jobSettings.RunImportedCarrierAnalytics ? "Scheduled" : "Off",
            },
            new()
            {
                Name = "Inspection detail enrichment",
                Enabled = _jobSettings.Enabled && _jobSettings.RunInspectionEnrichment,
                Schedule = $"Daily at {_jobSettings.DailyRunTimeUtc} UTC",
                NextRunAtUtc = _jobSettings.Enabled && _jobSettings.RunInspectionEnrichment
                    ? NextDailyRun(now, ParseTime(_jobSettings.DailyRunTimeUtc))
                    : null,
                Status = _jobSettings.Enabled && _jobSettings.RunInspectionEnrichment ? "Scheduled" : "Off",
            },
            new()
            {
                Name = "Official SMS peer import",
                Enabled = _jobSettings.Enabled && _jobSettings.RunOfficialSmsPeerImport,
                Schedule = $"Monthly on day {Math.Clamp(_jobSettings.MonthlySmsImportDay, 1, 28)} at {_jobSettings.MonthlySmsImportTimeUtc} UTC",
                NextRunAtUtc = _jobSettings.Enabled && _jobSettings.RunOfficialSmsPeerImport
                    ? NextMonthlyRun(now, Math.Clamp(_jobSettings.MonthlySmsImportDay, 1, 28), ParseTime(_jobSettings.MonthlySmsImportTimeUtc))
                    : null,
                Status = _jobSettings.Enabled && _jobSettings.RunOfficialSmsPeerImport ? "Scheduled" : "Off",
            },
        ];
    }

    private static TimeSpan ParseTime(string value)
        => TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var parsed) ? parsed : TimeSpan.FromHours(6);

    private static DateTime NextDailyRun(DateTime now, TimeSpan runTime)
    {
        var next = now.Date.Add(runTime);
        return next > now ? next : next.AddDays(1);
    }

    private static DateTime NextMonthlyRun(DateTime now, int day, TimeSpan runTime)
    {
        var next = new DateTime(now.Year, now.Month, day, 0, 0, 0, DateTimeKind.Utc).Add(runTime);
        return next > now ? next : next.AddMonths(1);
    }

    private static async Task<(int Carriers, int BasicMeasures)> UpsertOfficialSmsRowsAsync(
        SafetyAnalyticsDbContext analyticsDb,
        List<Dictionary<string, JsonElement>> rows,
        string snapshotMonth,
        HashSet<string> carriersSeen,
        CancellationToken ct)
    {
        var dots = rows
            .Select(GetDotNumber)
            .Where(dot => !string.IsNullOrWhiteSpace(dot))
            .Select(dot => dot!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (dots.Count == 0)
            return (0, 0);

        var existingCarriers = await analyticsDb.FmcsaCarrierPeerSnapshots
            .Where(c => c.SnapshotMonth == snapshotMonth && dots.Contains(c.UsDotNumber))
            .ToDictionaryAsync(c => c.UsDotNumber, StringComparer.OrdinalIgnoreCase, ct);
        var existingMeasures = await analyticsDb.FmcsaBasicPeerMeasures
            .Where(m => m.SnapshotMonth == snapshotMonth && dots.Contains(m.UsDotNumber))
            .ToDictionaryAsync(m => $"{m.UsDotNumber}|{m.Basic}", StringComparer.OrdinalIgnoreCase, ct);

        var carrierCount = 0;
        var measureCount = 0;
        foreach (var row in rows)
        {
            var dot = GetDotNumber(row);
            if (string.IsNullOrWhiteSpace(dot))
                continue;

            if (!existingCarriers.TryGetValue(dot, out var carrier))
            {
                carrier = new FmcsaCarrierPeerSnapshot { SnapshotMonth = snapshotMonth, UsDotNumber = dot };
                analyticsDb.FmcsaCarrierPeerSnapshots.Add(carrier);
                existingCarriers[dot] = carrier;
            }

            carrier.LegalName = GetString(row, "legal_name", "carrier_name", "entity_name", "name") ?? carrier.LegalName;
            carrier.State = GetString(row, "phy_state", "state", "carrier_state") ?? carrier.State;
            carrier.PowerUnits = GetInt(row, "power_units", "nbr_power_unit", "nbr_power_units") ?? carrier.PowerUnits;
            carrier.DriverCount = GetInt(row, "driver_count", "drivers", "nbr_drivers") ?? carrier.DriverCount;
            carrier.Mileage = GetInt(row, "mileage", "mcs_150_mileage", "vmt") ?? carrier.Mileage;
            carrier.MileageYear = GetInt(row, "mileage_year", "mcs_150_mileage_year", "vmt_year") ?? carrier.MileageYear;

            if (carriersSeen.Add(dot))
                carrierCount++;

            foreach (var mapping in OfficialSmsMappings)
            {
                var officialMeasure = GetDecimal(row, mapping.MeasureFields);
                var officialPercentile = GetDecimal(row, mapping.PercentileFields);
                var eventCount = GetInt(row, mapping.EventCountFields);
                var oosCount = GetInt(row, mapping.OosCountFields);
                if (officialMeasure == null && officialPercentile == null && eventCount == null && oosCount == null)
                    continue;

                var key = $"{dot}|{mapping.Basic}";
                if (!existingMeasures.TryGetValue(key, out var measure))
                {
                    measure = new FmcsaBasicPeerMeasure { SnapshotMonth = snapshotMonth, UsDotNumber = dot, Basic = mapping.Basic };
                    analyticsDb.FmcsaBasicPeerMeasures.Add(measure);
                    existingMeasures[key] = measure;
                }

                measure.OfficialMeasure = officialMeasure ?? measure.OfficialMeasure;
                measure.SimsMeasure = officialMeasure ?? measure.SimsMeasure;
                measure.SimsPercentile = officialPercentile ?? measure.SimsPercentile;
                measure.InspectionWithViolationCount = eventCount ?? measure.InspectionWithViolationCount;
                measure.ViolationCount = eventCount ?? measure.ViolationCount;
                measure.OutOfServiceCount = oosCount ?? measure.OutOfServiceCount;
                measure.WeightedViolationScore = officialMeasure ?? measure.WeightedViolationScore;
                measure.Exposure = carrier.PowerUnits ?? 0;
                measure.PeerGroupKey = "official-sms:all";
                measureCount++;
            }
        }

        await analyticsDb.SaveChangesAsync(ct);
        return (carrierCount, measureCount);
    }

    private static void UpsertCarrierSnapshot(
        SafetyAnalyticsDbContext analyticsDb,
        Dictionary<string, FmcsaCarrierPeerSnapshot> existing,
        FmcsaCarrierSnapshot carrier,
        List<FmcsaInspection> inspections,
        string snapshotMonth)
    {
        if (!existing.TryGetValue(carrier.UsDotNumber, out var snapshot))
        {
            snapshot = new FmcsaCarrierPeerSnapshot { SnapshotMonth = snapshotMonth, UsDotNumber = carrier.UsDotNumber };
            analyticsDb.FmcsaCarrierPeerSnapshots.Add(snapshot);
            existing[carrier.UsDotNumber] = snapshot;
        }

        snapshot.LegalName = carrier.LegalName;
        snapshot.State = carrier.State;
        snapshot.PowerUnits = carrier.PowerUnits;
        snapshot.DriverCount = carrier.DriverCount;
        snapshot.Mileage = carrier.Mileage;
        snapshot.MileageYear = carrier.MileageYear;
        snapshot.InspectionCount = inspections.Count;
        snapshot.DriverInspectionCount = inspections.Count;
        snapshot.VehicleInspectionCount = inspections.Count(IsVehicleInspection);
        snapshot.DriverOosInspectionCount = inspections.Count(i => i.DriverOutOfService);
        snapshot.VehicleOosInspectionCount = inspections.Count(i => i.VehicleOutOfService);
    }

    private static void UpsertBasicMeasure(
        SafetyAnalyticsDbContext analyticsDb,
        Dictionary<string, FmcsaBasicPeerMeasure> existing,
        FmcsaCarrierSnapshot carrier,
        FmcsaScoringRun? latestScoringRun,
        List<FmcsaInspection> inspections,
        List<FmcsaViolation> violations,
        string basic,
        string snapshotMonth,
        DateTime now)
    {
        var key = $"{carrier.UsDotNumber}|{basic}";
        if (!existing.TryGetValue(key, out var measure))
        {
            measure = new FmcsaBasicPeerMeasure { SnapshotMonth = snapshotMonth, UsDotNumber = carrier.UsDotNumber, Basic = basic };
            analyticsDb.FmcsaBasicPeerMeasures.Add(measure);
            existing[key] = measure;
        }

        var basicViolations = violations
            .Where(v => string.Equals(v.Basic, basic, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var official = latestScoringRun?.BasicScores.FirstOrDefault(s => string.Equals(s.Basic, basic, StringComparison.OrdinalIgnoreCase));
        var exposure = CalculateExposure(basic, carrier, inspections);
        var weightedScore = basicViolations.Sum(v => CalculateWeightedViolationScore(v, now));
        decimal? simsMeasure = exposure <= 0 ? null : Math.Round(weightedScore / exposure, 2);

        measure.OfficialMeasure = official?.Measure;
        measure.SimsMeasure = simsMeasure;
        measure.InspectionWithViolationCount = basicViolations.Select(v => v.ReportNumber).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        measure.ViolationCount = basicViolations.Count;
        measure.OutOfServiceCount = basicViolations.Count(v => v.IsOutOfService || v.IsDriverDisqualifying);
        measure.WeightedViolationScore = Math.Round(weightedScore, 2);
        measure.Exposure = Math.Round(exposure, 2);
        measure.PeerGroupKey = BuildPeerGroupKey(basic, exposure);
    }

    private static async Task RecalculatePercentilesAsync(SafetyAnalyticsDbContext analyticsDb, string snapshotMonth, CancellationToken ct)
    {
        await analyticsDb.Database.ExecuteSqlInterpolatedAsync($"""
            WITH ranked AS (
                SELECT
                    id,
                    row_number() OVER (
                        PARTITION BY snapshot_month, basic, peer_group_key
                        ORDER BY sims_measure, us_dot_number
                    ) AS peer_rank,
                    count(*) OVER (
                        PARTITION BY snapshot_month, basic, peer_group_key
                    ) AS peer_population
                FROM fmcsa_basic_peer_measures
                WHERE snapshot_month = {snapshotMonth}
                  AND sims_measure IS NOT NULL
                  AND is_deleted = false
            )
            UPDATE fmcsa_basic_peer_measures AS m
            SET
                peer_rank = ranked.peer_rank,
                peer_population = ranked.peer_population,
                sims_percentile = CASE
                    WHEN ranked.peer_population <= 1 THEN NULL
                    ELSE round((ranked.peer_rank::numeric * 100.0) / ranked.peer_population, 0)
                END
            FROM ranked
            WHERE m.id = ranked.id
            """, ct);
    }

    private static decimal CalculateExposure(string basic, FmcsaCarrierSnapshot carrier, List<FmcsaInspection> inspections)
    {
        if (basic is "Unsafe Driving" or "Crash Indicator")
            return Math.Max(1, carrier.PowerUnits ?? 1);

        if (basic == "Vehicle Maintenance")
            return Math.Max(1, inspections.Count(IsVehicleInspection));

        if (basic == "Hazardous Materials Compliance")
        {
            var hazmatInspections = inspections.Count(i => i.HazmatViolationCount > 0 || i.HazmatOutOfService);
            return Math.Max(1, hazmatInspections);
        }

        return Math.Max(1, inspections.Count);
    }

    private static decimal CalculateWeightedViolationScore(FmcsaViolation violation, DateTime now)
    {
        var monthsOld = ((now.Year - violation.Inspection.InspectionDate.Year) * 12) + now.Month - violation.Inspection.InspectionDate.Month;
        var timeWeight = monthsOld < 6 ? 3m : monthsOld < 12 ? 2m : 1m;
        var severity = violation.IsOutOfService || violation.IsDriverDisqualifying
            ? Math.Max(10, violation.SeverityWeight)
            : Math.Clamp(violation.SeverityWeight, 1, 10);

        return severity * timeWeight;
    }

    private static string BuildPeerGroupKey(string basic, decimal exposure)
    {
        var prefix = basic is "Unsafe Driving" or "Crash Indicator" ? "power-units" : "inspections";
        var bucket = exposure switch
        {
            <= 2 => "0-2",
            <= 5 => "3-5",
            <= 10 => "6-10",
            <= 20 => "11-20",
            <= 50 => "21-50",
            _ => "51+",
        };

        return $"{prefix}:{bucket}";
    }

    private static bool IsVehicleInspection(FmcsaInspection inspection) =>
        inspection.VehicleViolationCount > 0 ||
        inspection.VehicleOutOfService ||
        !string.IsNullOrWhiteSpace(inspection.Vin) ||
        !string.IsNullOrWhiteSpace(inspection.Vin2) ||
        inspection.InspectionLevel is 1 or 2 or 5 or 6;

    private static string? GetDotNumber(Dictionary<string, JsonElement> row) =>
        NormalizeDigits(GetString(row, "dot_number", "usdot_number", "us_dot_number", "usdot", "dot"));

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

    private static bool TryGetValue(Dictionary<string, JsonElement> row, string name, out JsonElement value)
    {
        if (row.TryGetValue(name, out value)) return true;
        foreach (var key in row.Keys)
        {
            if (key.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = row[key];
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? NormalizeDigits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = new string(value.Where(char.IsDigit).ToArray());
        return string.IsNullOrWhiteSpace(digits) ? null : digits;
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record OfficialSmsBasicMapping(
        string Basic,
        string[] MeasureFields,
        string[] PercentileFields,
        string[] EventCountFields,
        string[] OosCountFields);
}
