using Microsoft.EntityFrameworkCore;
using SIMS.Application.Common;
using SIMS.Application.DTOs.Accounting;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities.Accounting;

namespace SIMS.Application.Services;

public class DisbursementService : IDisbursementService
{
    private readonly IServiceProvider _sp;
    private readonly ILedgerService _ledger;
    private DbContext Db => (DbContext)_sp.GetService(typeof(DbContext))!;

    public DisbursementService(IServiceProvider sp, ILedgerService ledger)
    {
        _sp = sp;
        _ledger = ledger;
    }

    // ---- Aging ----

    public async Task<PayableAgingDto> GetAgingAsync(CancellationToken ct = default)
    {
        var payables = await GetOpenPayablesAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        int AgeDays(DateOnly due) => Math.Max(0, today.DayNumber - due.DayNumber);

        decimal Bucket(OpenPayableDto p, int from, int to)
        {
            var d = AgeDays(p.DueDate);
            return d >= from && (to < 0 || d <= to) ? p.Balance : 0;
        }

        var summary = new AgingBucketDto(
            payables.Sum(p => Bucket(p, 0, 30)),
            payables.Sum(p => Bucket(p, 31, 60)),
            payables.Sum(p => Bucket(p, 61, 90)),
            payables.Sum(p => Bucket(p, 91, -1)),
            payables.Sum(p => p.Balance)
        );

        var rows = payables
            .GroupBy(p => p.PayeeName)
            .Select(g => new AgingRowDto(
                g.Key,
                g.First().CarrierId,
                g.Sum(p => Bucket(p, 0, 30)),
                g.Sum(p => Bucket(p, 31, 60)),
                g.Sum(p => Bucket(p, 61, 90)),
                g.Sum(p => Bucket(p, 91, -1)),
                g.Sum(p => p.Balance)
            ))
            .OrderByDescending(r => r.Total)
            .ToList();

        return new PayableAgingDto(summary, rows, payables);
    }

    public async Task<IReadOnlyList<OpenPayableDto>> GetOpenPayablesAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return await Db.Set<Payable>()
            .Include(p => p.Invoice)
            .Where(p => p.TenantId == 1 && (p.Status == "Open" || p.Status == "PartiallyPaid"))
            .OrderBy(p => p.DueDate)
            .Select(p => new OpenPayableDto(
                p.Id,
                p.InvoiceId,
                p.Invoice.InvoiceNumber,
                p.PayeeName,
                p.CarrierId,
                p.Amount,
                p.PaidAmount,
                p.Amount - p.PaidAmount,
                p.InvoiceDate,
                p.DueDate,
                Math.Max(0, today.DayNumber - p.DueDate.DayNumber),
                p.Status))
            .ToListAsync(ct);
    }

    // ---- Disbursements ----

    public async Task<IReadOnlyList<DisbursementSummaryDto>> GetDisbursementsAsync(CancellationToken ct = default)
    {
        return await Db.Set<Disbursement>()
            .Where(d => d.TenantId == 1)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new DisbursementSummaryDto(
                d.Id, d.DisbursementNumber, d.PayeeName, d.CarrierId,
                d.TotalAmount, d.PaymentDate, d.PaymentMethod,
                d.Reference, d.Status, d.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<Result<DisbursementDetailDto>> GetDisbursementAsync(long id, CancellationToken ct = default)
    {
        var detail = await LoadDetailAsync(id, ct);
        if (detail == null)
            return Result<DisbursementDetailDto>.Failure("NOT_FOUND", $"Disbursement {id} not found");
        return Result<DisbursementDetailDto>.Success(detail);
    }

    public async Task<Result<DisbursementDetailDto>> CreateDisbursementAsync(
        CreateDisbursementRequest req, Guid userId, CancellationToken ct = default)
    {
        if (req.Lines.Count == 0)
            return Result<DisbursementDetailDto>.Failure("NO_LINES", "At least one payable line is required");

        var db = Db;

        var payableIds = req.Lines.Select(l => l.PayableId).ToList();
        var payables = await db.Set<Payable>()
            .Include(p => p.Invoice)
            .Where(p => payableIds.Contains(p.Id) && p.TenantId == 1)
            .ToDictionaryAsync(p => p.Id, ct);

        foreach (var line in req.Lines)
        {
            if (!payables.TryGetValue(line.PayableId, out var payable))
                return Result<DisbursementDetailDto>.Failure("PAYABLE_NOT_FOUND",
                    $"Payable {line.PayableId} not found");

            if (payable.Status == "Paid" || payable.Status == "Voided")
                return Result<DisbursementDetailDto>.Failure("PAYABLE_CLOSED",
                    $"Payable {line.PayableId} is already {payable.Status}");

            var available = payable.Amount - payable.PaidAmount;
            if (line.Amount > available + 0.005m)
                return Result<DisbursementDetailDto>.Failure("OVER_PAYMENT",
                    $"Disbursement amount {line.Amount:F2} exceeds open balance {available:F2} on payable {line.PayableId}");
        }

        // Derive payee from the payables (first payable's payee)
        var firstPayable = payables[req.Lines[0].PayableId];
        var payeeName = firstPayable.PayeeName;
        var carrierId = firstPayable.CarrierId;
        var totalAmount = req.Lines.Sum(l => l.Amount);

        var disbursement = new Disbursement
        {
            TenantId = 1,
            PayeeName = payeeName,
            CarrierId = carrierId,
            TotalAmount = totalAmount,
            PaymentDate = req.PaymentDate,
            PaymentMethod = req.PaymentMethod,
            Reference = req.Reference,
            Status = "Draft",
            Notes = req.Notes,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow
        };

        db.Set<Disbursement>().Add(disbursement);
        await db.SaveChangesAsync(ct); // get disbursement.Id

        disbursement.DisbursementNumber = $"DISB-{DateTime.UtcNow.Year}-{disbursement.Id:D5}";

        var lines = req.Lines.Select(l => new DisbursementLine
        {
            DisbursementId = disbursement.Id,
            PayableId = l.PayableId,
            Amount = l.Amount
        }).ToList();

        db.Set<DisbursementLine>().AddRange(lines);
        await db.SaveChangesAsync(ct);

        return Result<DisbursementDetailDto>.Success((await LoadDetailAsync(disbursement.Id, ct))!);
    }

    public async Task<Result<DisbursementDetailDto>> PostDisbursementAsync(
        long id, Guid userId, CancellationToken ct = default)
    {
        var db = Db;

        var disbursement = await db.Set<Disbursement>()
            .Include(d => d.Lines)
                .ThenInclude(l => l.Payable)
                    .ThenInclude(p => p.Invoice)
            .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == 1, ct);

        if (disbursement == null)
            return Result<DisbursementDetailDto>.Failure("NOT_FOUND", $"Disbursement {id} not found");

        if (disbursement.Status != "Draft")
            return Result<DisbursementDetailDto>.Failure("INVALID_STATUS",
                $"Disbursement is {disbursement.Status} — only Draft disbursements can be posted");

        var trustAccount = await db.Set<LedgerAccount>()
            .FirstOrDefaultAsync(a => a.InternalCode == "1100" && a.TenantId == 1, ct);

        if (trustAccount == null)
            return Result<DisbursementDetailDto>.Failure("MISSING_GL_ACCOUNT",
                "Trust account (1100) not found");

        var txnId = await _ledger.PostDisbursementAsync(disbursement, trustAccount.Id, userId, ct);

        disbursement.LedgerTransactionId = txnId;
        disbursement.Status = "Posted";

        // Update each payable's paid amount and status
        foreach (var line in disbursement.Lines)
        {
            var payable = line.Payable;
            payable.PaidAmount += line.Amount;
            payable.Status = payable.PaidAmount >= payable.Amount - 0.005m
                ? "Paid"
                : "PartiallyPaid";
        }

        await db.SaveChangesAsync(ct);
        return Result<DisbursementDetailDto>.Success((await LoadDetailAsync(id, ct))!);
    }

    public async Task<Result<DisbursementDetailDto>> VoidDisbursementAsync(
        long id, string? reason, Guid userId, CancellationToken ct = default)
    {
        var db = Db;

        var disbursement = await db.Set<Disbursement>()
            .Include(d => d.Lines)
                .ThenInclude(l => l.Payable)
            .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == 1, ct);

        if (disbursement == null)
            return Result<DisbursementDetailDto>.Failure("NOT_FOUND", $"Disbursement {id} not found");

        if (disbursement.Status == "Voided")
            return Result<DisbursementDetailDto>.Failure("ALREADY_VOIDED", "Disbursement is already voided");

        if (disbursement.Status == "Posted")
            return Result<DisbursementDetailDto>.Failure("POSTED",
                "Posted disbursements cannot be voided — create a reversing entry instead");

        // Only Draft can be voided without a reversing JE
        disbursement.Status = "Voided";
        disbursement.Notes = string.IsNullOrWhiteSpace(reason)
            ? disbursement.Notes
            : $"{disbursement.Notes}\nVoided: {reason}".Trim();

        await db.SaveChangesAsync(ct);
        return Result<DisbursementDetailDto>.Success((await LoadDetailAsync(id, ct))!);
    }

    // ---- Helpers ----

    private async Task<DisbursementDetailDto?> LoadDetailAsync(long id, CancellationToken ct)
    {
        var d = await Db.Set<Disbursement>()
            .Include(x => x.Lines)
                .ThenInclude(l => l.Payable)
                    .ThenInclude(p => p.Invoice)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (d == null) return null;

        return new DisbursementDetailDto(
            d.Id, d.DisbursementNumber, d.PayeeName, d.CarrierId,
            d.TotalAmount, d.PaymentDate, d.PaymentMethod,
            d.Reference, d.Status, d.LedgerTransactionId,
            d.Notes, d.CreatedAt,
            d.Lines.OrderBy(l => l.Id)
                .Select(l => new DisbursementLineSummaryDto(
                    l.Id, l.PayableId,
                    l.Payable.Invoice.InvoiceNumber,
                    l.Payable.PayeeName,
                    l.Amount))
                .ToList()
        );
    }
}
