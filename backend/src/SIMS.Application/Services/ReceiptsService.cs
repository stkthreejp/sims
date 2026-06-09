using Microsoft.EntityFrameworkCore;
using SIMS.Application.Common;
using SIMS.Application.DTOs.Accounting;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities.Accounting;

namespace SIMS.Application.Services;

public class ReceiptsService : IReceiptsService
{
    private readonly IServiceProvider _sp;
    private readonly ILedgerService _ledger;
    private DbContext Db => (DbContext)_sp.GetService(typeof(DbContext))!;

    public ReceiptsService(IServiceProvider sp, ILedgerService ledger)
    {
        _sp = sp;
        _ledger = ledger;
    }

    public async Task<Result<ReceiptDetailDto>> CreateAsync(
        CreateReceiptRequest req, Guid userId, CancellationToken ct = default)
    {
        var db = Db;

        var trustAccount = await db.Set<LedgerAccount>()
            .FirstOrDefaultAsync(a => a.InternalCode == "1100" && a.TenantId == 1, ct);
        var unappliedAccount = await db.Set<LedgerAccount>()
            .FirstOrDefaultAsync(a => a.InternalCode == "1250" && a.TenantId == 1, ct);

        if (trustAccount == null || unappliedAccount == null)
            return Result<ReceiptDetailDto>.Failure("MISSING_GL_ACCOUNTS",
                "Required GL accounts (1100 Trust, 1250 Unapplied Cash) not found");

        var seq = await db.Database.SqlQueryRaw<long>("SELECT nextval('receipt_number_seq')").FirstAsync(ct);
        var receiptNumber = $"RCT-{req.ReceivedDate.Year}-{seq:D5}";

        var receipt = new Receipt
        {
            ReceiptNumber = receiptNumber,
            ReceivedDate = req.ReceivedDate,
            Amount = req.Amount,
            PayerName = req.PayerName,
            Reference = req.Reference,
            Status = "Open",
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow
        };

        await using var dbTransaction = db.Database.IsRelational() && db.Database.CurrentTransaction == null
            ? await db.Database.BeginTransactionAsync(ct)
            : null;

        db.Set<Receipt>().Add(receipt);
        await db.SaveChangesAsync(ct);

        var txnId = await _ledger.PostReceiptAsync(
            receipt, trustAccount.Id, unappliedAccount.Id, userId, ct);

        receipt.LedgerTransactionId = txnId;
        await db.SaveChangesAsync(ct);

        if (dbTransaction != null)
            await dbTransaction.CommitAsync(ct);

        return Result<ReceiptDetailDto>.Success(await LoadDetailAsync(receipt.Id, ct));
    }

    public async Task<IReadOnlyList<ReceiptSummaryDto>> GetReceiptsAsync(CancellationToken ct = default)
    {
        return await Db.Set<Receipt>()
            .OrderByDescending(r => r.ReceivedDate)
            .ThenByDescending(r => r.Id)
            .Select(r => new ReceiptSummaryDto(
                r.Id, r.ReceiptNumber, r.ReceivedDate, r.PayerName,
                r.Amount, r.AppliedAmount, r.Status))
            .ToListAsync(ct);
    }

    public async Task<Result<ReceiptDetailDto>> GetReceiptAsync(long id, CancellationToken ct = default)
    {
        var exists = await Db.Set<Receipt>().AnyAsync(r => r.Id == id, ct);
        if (!exists)
            return Result<ReceiptDetailDto>.Failure("NOT_FOUND", $"Receipt {id} not found");
        return Result<ReceiptDetailDto>.Success(await LoadDetailAsync(id, ct));
    }

    private async Task<ReceiptDetailDto> LoadDetailAsync(long id, CancellationToken ct)
    {
        var receipt = await Db.Set<Receipt>()
            .Include(r => r.Applications)
                .ThenInclude(a => a.Invoice)
            .FirstAsync(r => r.Id == id, ct);

        return new ReceiptDetailDto(
            receipt.Id,
            receipt.ReceiptNumber,
            receipt.ReceivedDate,
            receipt.PayerName,
            receipt.Amount,
            receipt.AppliedAmount,
            receipt.Status,
            receipt.LedgerTransactionId,
            receipt.Applications
                .OrderBy(a => a.Id)
                .Select(a => new ReceiptApplicationDto(
                    a.Id, a.InvoiceId, a.Invoice.InvoiceNumber,
                    a.GrossApplied, a.CommissionAmount, a.NetApplied, a.CreatedAt))
                .ToList()
        );
    }
}
