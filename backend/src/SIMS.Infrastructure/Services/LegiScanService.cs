using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SIMS.Application.Common;
using SIMS.Application.Configuration;
using SIMS.Application.DTOs.Legal;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Infrastructure.Data;

namespace SIMS.Infrastructure.Services;

public class LegiScanService : ILegiScanService
{
    private readonly ApplicationDbContext _db;
    private readonly LegiScanClient _client;
    private readonly LegiScanSettings _settings;

    public LegiScanService(ApplicationDbContext db, LegiScanClient client, IOptions<LegiScanSettings> settings)
    {
        _db = db;
        _client = client;
        _settings = settings.Value;
    }

    public async Task<LegiScanStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        var count = await _db.LegiScanTrackedBills.CountAsync(b => b.IsActive, ct);
        return new LegiScanStatusDto(
            !string.IsNullOrWhiteSpace(_settings.ApiKey),
            MaxMonitoredBills,
            Math.Max(1, _settings.MonthlyQueryLimit),
            count);
    }

    public async Task<IReadOnlyList<LegiScanTrackedBillDto>> GetTrackedBillsAsync(CancellationToken ct = default)
    {
        var bills = await _db.LegiScanTrackedBills
            .AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.State)
            .ThenBy(b => b.BillNumber)
            .ToListAsync(ct);

        return bills.Select(ToDto).ToList();
    }

    public async Task<Result<IReadOnlyList<LegiScanTrackedBillDto>>> AddToMonitorAsync(int[] billIds, string? stance, CancellationToken ct = default)
    {
        var requested = billIds.Where(id => id > 0).Distinct().ToArray();
        if (requested.Length == 0)
            return Result<IReadOnlyList<LegiScanTrackedBillDto>>.Failure("NO_BILLS", "At least one bill id is required.");

        var existingActiveIds = await _db.LegiScanTrackedBills
            .Where(b => b.IsActive)
            .Select(b => b.BillId)
            .ToListAsync(ct);

        var totalAfterAdd = existingActiveIds.Union(requested).Count();
        if (totalAfterAdd > MaxMonitoredBills)
            return Result<IReadOnlyList<LegiScanTrackedBillDto>>.Failure("LEGISCAN_MONITOR_LIMIT", $"SIMS can monitor up to {MaxMonitoredBills} LegiScan bills.");

        var normalizedStance = NormalizeStance(stance);
        try
        {
            var remoteResult = await _client.SetMonitorAsync(requested, "monitor", normalizedStance, ct);
            var failed = remoteResult.Where(r => r.Value.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (failed.Length > 0)
                return Result<IReadOnlyList<LegiScanTrackedBillDto>>.Failure("LEGISCAN_MONITOR_ERROR", string.Join("; ", failed.Select(f => f.Value)));

            var existingBills = await _db.LegiScanTrackedBills
                .Where(b => requested.Contains(b.BillId))
                .ToListAsync(ct);

            foreach (var billId in requested)
            {
                var bill = existingBills.FirstOrDefault(b => b.BillId == billId);
                if (bill == null)
                {
                    _db.LegiScanTrackedBills.Add(new LegiScanTrackedBill
                    {
                        BillId = billId,
                        State = string.Empty,
                        BillNumber = billId.ToString(),
                        Title = "Pending LegiScan sync",
                        Stance = normalizedStance,
                        IsActive = true
                    });
                }
                else
                {
                    bill.Stance = normalizedStance;
                    bill.IsActive = true;
                    bill.DeletedAt = null;
                }
            }

            await _db.SaveChangesAsync(ct);
            await SyncMonitorAsync(null, "LegiScan monitor add", ct);
            return Result<IReadOnlyList<LegiScanTrackedBillDto>>.Success(await GetTrackedBillsAsync(ct));
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
        {
            return Result<IReadOnlyList<LegiScanTrackedBillDto>>.Failure("LEGISCAN_API_ERROR", ex.Message);
        }
    }

    public async Task<Result> RemoveFromMonitorAsync(int billId, CancellationToken ct = default)
    {
        if (billId <= 0)
            return Result.Failure("INVALID_BILL_ID", "Bill id is required.");

        try
        {
            var remoteResult = await _client.SetMonitorAsync([billId], "remove", "watch", ct);
            if (remoteResult.TryGetValue(billId, out var message) &&
                message.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase))
                return Result.Failure("LEGISCAN_MONITOR_ERROR", message);

            var bill = await _db.LegiScanTrackedBills.FirstOrDefaultAsync(b => b.BillId == billId, ct);
            if (bill != null)
            {
                bill.IsActive = false;
                await _db.SaveChangesAsync(ct);
            }

            return Result.Success();
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
        {
            return Result.Failure("LEGISCAN_API_ERROR", ex.Message);
        }
    }

    public async Task<Result<LegiScanSyncResultDto>> SyncMonitorAsync(Guid? startedById, string? startedByName, CancellationToken ct = default)
    {
        var run = new LegalSourceScanRun
        {
            SourceName = "LegiScan Monitor",
            SourceType = "LegiScan API",
            Status = "Running",
            StartedById = startedById,
            StartedByName = startedByName
        };
        _db.LegalSourceScanRuns.Add(run);
        await _db.SaveChangesAsync(ct);

        try
        {
            var allRemoteBills = await _client.GetMonitorListRawAsync(ct);
            var remoteMonitorCount = allRemoteBills.Count;
            var remoteBills = allRemoteBills.Take(MaxMonitoredBills).ToList();
            var warnings = new List<string>();
            if (remoteMonitorCount > MaxMonitoredBills)
                warnings.Add($"LegiScan returned {remoteMonitorCount} monitored bills; SIMS only tracks the first {MaxMonitoredBills}.");

            var remoteIds = remoteBills.Select(b => b.BillId).ToArray();
            var localBills = await _db.LegiScanTrackedBills
                .Where(b => remoteIds.Contains(b.BillId))
                .ToListAsync(ct);

            var changed = new List<LegiScanTrackedBill>();
            var now = DateTime.UtcNow;
            foreach (var remote in remoteBills)
            {
                var local = localBills.FirstOrDefault(b => b.BillId == remote.BillId);
                if (local == null)
                {
                    local = new LegiScanTrackedBill
                    {
                        BillId = remote.BillId,
                        State = NormalizeState(remote.State),
                        BillNumber = string.IsNullOrWhiteSpace(remote.BillNumber) ? remote.BillId.ToString() : remote.BillNumber,
                        Title = "Pending LegiScan sync",
                        Stance = "watch",
                        IsActive = true
                    };
                    _db.LegiScanTrackedBills.Add(local);
                    localBills.Add(local);
                }

                var needsDetail = string.IsNullOrWhiteSpace(local.ChangeHash) ||
                    !string.Equals(local.ChangeHash, remote.ChangeHash, StringComparison.OrdinalIgnoreCase);

                local.State = NormalizeState(remote.State);
                local.BillNumber = string.IsNullOrWhiteSpace(remote.BillNumber) ? local.BillNumber : remote.BillNumber;
                local.ChangeHash = remote.ChangeHash;
                local.Status = remote.Status;
                local.StatusDate = remote.StatusDate;
                local.Url = remote.Url;
                local.IsActive = true;

                if (needsDetail)
                    changed.Add(local);
            }

            foreach (var local in changed)
            {
                var detail = await _client.GetBillAsync(local.BillId, ct);
                local.State = NormalizeState(detail.State);
                local.BillNumber = string.IsNullOrWhiteSpace(detail.BillNumber) ? local.BillNumber : detail.BillNumber;
                local.Title = string.IsNullOrWhiteSpace(detail.Title) ? local.Title : detail.Title;
                local.Description = detail.Description;
                local.ChangeHash = detail.ChangeHash ?? local.ChangeHash;
                local.Status = detail.Status ?? local.Status;
                local.StatusDate = detail.StatusDate ?? local.StatusDate;
                local.Url = detail.Url ?? local.Url;
                local.RawBillJson = detail.RawJson;
                local.LastSyncedAt = now;

                _db.LegalSourceScanResults.Add(new LegalSourceScanResult
                {
                    ScanRun = run,
                    State = local.State,
                    Category = "Legislative Monitoring",
                    Topic = local.BillNumber,
                    MatchStatus = "PossibleChange",
                    SourceUrl = local.Url ?? string.Empty,
                    SourceCitation = $"LegiScan bill_id {local.BillId}",
                    SourceText = BuildSourceText(local),
                    SuggestedRequirementText = null,
                    ConfidenceScore = 0.5m,
                    ReviewStatus = "Pending"
                });
            }

            run.Status = "Completed";
            run.CompletedAt = now;
            run.ResultsFound = remoteBills.Count;
            run.PossibleChanges = changed.Count;
            await _db.SaveChangesAsync(ct);

            return Result<LegiScanSyncResultDto>.Success(new LegiScanSyncResultDto(
                run.Id,
                remoteMonitorCount,
                remoteBills.Count,
                changed.Count,
                1 + changed.Count,
                warnings.ToArray()));
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
        {
            run.Status = "Failed";
            run.CompletedAt = DateTime.UtcNow;
            run.ErrorMessage = ex.Message;
            await _db.SaveChangesAsync(ct);
            return Result<LegiScanSyncResultDto>.Failure("LEGISCAN_API_ERROR", ex.Message);
        }
    }

    private int MaxMonitoredBills => Math.Clamp(_settings.MaxMonitoredBills, 1, 50);

    private static string NormalizeStance(string? stance)
    {
        var value = string.IsNullOrWhiteSpace(stance) ? "watch" : stance.Trim().ToLowerInvariant();
        return value is "support" or "oppose" ? value : "watch";
    }

    private static string NormalizeState(string? state)
    {
        return string.IsNullOrWhiteSpace(state) ? string.Empty : state.Trim().ToUpperInvariant();
    }

    private static string BuildSourceText(LegiScanTrackedBill bill)
    {
        var parts = new[]
        {
            $"{bill.State} {bill.BillNumber}: {bill.Title}",
            bill.Description,
            bill.Url
        };

        return string.Join(Environment.NewLine, parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private static LegiScanTrackedBillDto ToDto(LegiScanTrackedBill bill)
    {
        return new LegiScanTrackedBillDto(
            bill.Id,
            bill.BillId,
            bill.State,
            bill.BillNumber,
            bill.Title,
            bill.ChangeHash,
            bill.Status,
            bill.StatusDate,
            bill.Url,
            bill.Stance,
            bill.IsActive,
            bill.LastSyncedAt,
            bill.CreatedAt,
            bill.UpdatedAt);
    }
}
