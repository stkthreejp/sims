using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMS.Application.Common;
using SIMS.Application.DTOs.Fmcsa;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities.Fmcsa;
using SIMS.Domain.Entities.FmcsaAnalytics;
using SIMS.Infrastructure.Data;

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

    private readonly ApplicationDbContext _appDb;
    private readonly IServiceProvider _serviceProvider;

    public FmcsaSafetyAnalyticsService(ApplicationDbContext appDb, IServiceProvider serviceProvider)
    {
        _appDb = appDb;
        _serviceProvider = serviceProvider;
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
        var measures = await analyticsDb.FmcsaBasicPeerMeasures
            .Where(m => m.SnapshotMonth == snapshotMonth && m.SimsMeasure.HasValue)
            .ToListAsync(ct);

        foreach (var group in measures.GroupBy(m => new { m.Basic, m.PeerGroupKey }))
        {
            var ranked = group
                .OrderBy(m => m.SimsMeasure!.Value)
                .ThenBy(m => m.UsDotNumber)
                .ToList();

            for (var i = 0; i < ranked.Count; i++)
            {
                ranked[i].PeerRank = i + 1;
                ranked[i].PeerPopulation = ranked.Count;
                ranked[i].SimsPercentile = ranked.Count <= 1
                    ? null
                    : Math.Round((i + 1) * 100m / ranked.Count, 0);
            }
        }

        await analyticsDb.SaveChangesAsync(ct);
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
}
