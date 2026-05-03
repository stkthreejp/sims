using Microsoft.EntityFrameworkCore;
using SIMS.Application.DTOs.Accounting;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities.Accounting;

namespace SIMS.Application.Services;

public class PeriodCloseService : IPeriodCloseService
{
    private readonly IServiceProvider _sp;
    private DbContext Db => (DbContext)_sp.GetService(typeof(DbContext))!;

    public PeriodCloseService(IServiceProvider sp) => _sp = sp;

    public async Task<IReadOnlyList<AccountingPeriodDto>> GetPeriodsAsync(CancellationToken ct = default)
    {
        var db = Db;
        var periods = await db.Set<AccountingPeriod>()
            .Where(p => p.TenantId == 1)
            .OrderByDescending(p => p.PeriodYear)
            .ThenByDescending(p => p.PeriodMonth)
            .ToListAsync(ct);

        var checklists = await db.Set<PeriodCloseChecklistItem>()
            .Where(c => c.TenantId == 1)
            .ToListAsync(ct);

        return periods.Select(p => MapPeriod(p, checklists.Where(c => c.PeriodId == p.Id).ToList())).ToList();
    }

    public async Task<AccountingPeriodDto> GetOrCreatePeriodAsync(int year, int month, CancellationToken ct = default)
    {
        var db = Db;
        var period = await db.Set<AccountingPeriod>()
            .FirstOrDefaultAsync(p => p.TenantId == 1 && p.PeriodYear == year && p.PeriodMonth == month, ct);

        if (period == null)
        {
            period = new AccountingPeriod { PeriodYear = year, PeriodMonth = month, Status = "Open" };
            db.Set<AccountingPeriod>().Add(period);
            await db.SaveChangesAsync(ct);
        }

        return await EvaluateChecklistAsync(period.Id, ct);
    }

    public async Task<AccountingPeriodDto> EvaluateChecklistAsync(long periodId, CancellationToken ct = default)
    {
        var db = Db;
        var period = await db.Set<AccountingPeriod>()
            .FirstAsync(p => p.Id == periodId, ct);

        var now = DateTime.UtcNow;
        var items = await ComputeChecklistAsync(period, ct);

        // Upsert checklist items
        foreach (var item in items)
        {
            var existing = await db.Set<PeriodCloseChecklistItem>()
                .FirstOrDefaultAsync(c => c.PeriodId == periodId && c.CheckKey == item.CheckKey, ct);

            if (existing == null)
            {
                db.Set<PeriodCloseChecklistItem>().Add(new PeriodCloseChecklistItem
                {
                    PeriodId = periodId,
                    CheckKey = item.CheckKey,
                    IssueCount = item.IssueCount,
                    IsBlocking = item.IsBlocking,
                    LastCheckedAt = now,
                });
            }
            else
            {
                existing.IssueCount = item.IssueCount;
                existing.IsBlocking = item.IsBlocking;
                existing.LastCheckedAt = now;
            }
        }

        await db.SaveChangesAsync(ct);

        var savedItems = await db.Set<PeriodCloseChecklistItem>()
            .Where(c => c.PeriodId == periodId)
            .ToListAsync(ct);

        return MapPeriod(period, savedItems);
    }

    public async Task<PeriodCloseResultDto> ClosePeriodAsync(
        long periodId, string? notes, Guid userId, CancellationToken ct = default)
    {
        var db = Db;
        var period = await db.Set<AccountingPeriod>()
            .FirstOrDefaultAsync(p => p.Id == periodId, ct);

        if (period == null)
            return Fail("NOT_FOUND", "Period not found");
        if (period.Status == "Closed")
            return Fail("ALREADY_CLOSED", "Period is already closed");

        // Re-evaluate checklist and check for hard blockers
        await EvaluateChecklistAsync(periodId, ct);

        var blockers = await db.Set<PeriodCloseChecklistItem>()
            .Where(c => c.PeriodId == periodId && c.IsBlocking && c.IssueCount > 0)
            .ToListAsync(ct);

        if (blockers.Any())
        {
            var reasons = blockers.Select(b => $"{b.CheckKey}: {b.IssueCount} issue(s)");
            return Fail("BLOCKED", $"Cannot close: {string.Join("; ", reasons)}");
        }

        period.Status = "Closed";
        period.ClosedBy = userId;
        period.ClosedAt = DateTime.UtcNow;
        period.Notes = notes;

        await db.SaveChangesAsync(ct);

        var savedItems = await db.Set<PeriodCloseChecklistItem>()
            .Where(c => c.PeriodId == periodId)
            .ToListAsync(ct);

        return new PeriodCloseResultDto(true, null, null, MapPeriod(period, savedItems));
    }

    public async Task<PeriodCloseResultDto> ReopenPeriodAsync(
        long periodId, string? reason, Guid userId, CancellationToken ct = default)
    {
        var db = Db;
        var period = await db.Set<AccountingPeriod>()
            .FirstOrDefaultAsync(p => p.Id == periodId, ct);

        if (period == null)
            return Fail("NOT_FOUND", "Period not found");
        if (period.Status != "Closed")
            return Fail("NOT_CLOSED", "Period is not closed");

        period.Status = "Reopened";
        period.ReopenedBy = userId;
        period.ReopenedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(reason))
            period.Notes = $"{period.Notes}\nReopened: {reason}".Trim();

        await db.SaveChangesAsync(ct);

        var savedItems = await db.Set<PeriodCloseChecklistItem>()
            .Where(c => c.PeriodId == periodId)
            .ToListAsync(ct);

        return new PeriodCloseResultDto(true, null, null, MapPeriod(period, savedItems));
    }

    public async Task<AccountingPeriod?> GetCurrentOpenPeriodAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await Db.Set<AccountingPeriod>()
            .Where(p => p.TenantId == 1
                && p.PeriodYear == today.Year
                && p.PeriodMonth == today.Month
                && (p.Status == "Open" || p.Status == "Reopened"))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<string?> GetPeriodStatusForDateAsync(DateOnly date, CancellationToken ct = default)
    {
        var period = await Db.Set<AccountingPeriod>()
            .FirstOrDefaultAsync(p => p.TenantId == 1
                && p.PeriodYear == date.Year
                && p.PeriodMonth == date.Month, ct);
        return period?.Status;
    }

    // ---- Helpers ----

    private async Task<List<(string CheckKey, int IssueCount, bool IsBlocking)>> ComputeChecklistAsync(
        AccountingPeriod period, CancellationToken ct)
    {
        var db = Db;
        var fromDate = new DateOnly(period.PeriodYear, period.PeriodMonth, 1);
        var toDate = fromDate.AddMonths(1).AddDays(-1);

        // PendingSync: posted LedgerTransactions in this period not yet rolled into a JournalEntryRollup
        var pendingSync = await db.Set<LedgerTransaction>()
            .CountAsync(t => t.TenantId == 1
                && t.EffectiveDate >= fromDate
                && t.EffectiveDate <= toDate
                && t.PostingStatus == "Posted"
                && t.RolledUpIn == null, ct);

        // UnappliedCash: receipts received in this period still open
        var unappliedCash = await db.Set<Receipt>()
            .CountAsync(r => r.TenantId == 1
                && r.ReceivedDate >= fromDate
                && r.ReceivedDate <= toDate
                && r.Status == "Open", ct);

        // OpenRecs: payee statements with statement date in period not yet reconciled
        var openRecs = await db.Set<PayeeStatement>()
            .CountAsync(s => s.TenantId == 1
                && s.StatementDate >= fromDate
                && s.StatementDate <= toDate
                && s.Status == "Imported", ct);

        return
        [
            ("PendingSync", pendingSync, true),   // hard blocker
            ("UnappliedCash", unappliedCash, false),
            ("OpenRecs", openRecs, false),
        ];
    }

    private static AccountingPeriodDto MapPeriod(
        AccountingPeriod period, List<PeriodCloseChecklistItem> items)
    {
        var labels = new Dictionary<string, string>
        {
            ["PendingSync"] = "Transactions pending QB sync",
            ["UnappliedCash"] = "Open (unapplied) receipts",
            ["OpenRecs"] = "Unreconciled payee statements",
        };

        var checklist = items
            .OrderBy(i => i.CheckKey)
            .Select(i => new ChecklistItemDto(
                i.CheckKey,
                labels.GetValueOrDefault(i.CheckKey, i.CheckKey),
                i.IssueCount,
                i.IsBlocking,
                i.IssueCount == 0,
                i.LastCheckedAt))
            .ToList();

        return new AccountingPeriodDto(
            period.Id, period.PeriodYear, period.PeriodMonth,
            period.Status, period.ClosedAt, period.ReopenedAt,
            period.Notes, checklist);
    }

    private static PeriodCloseResultDto Fail(string code, string message) =>
        new(false, code, message, null);
}
