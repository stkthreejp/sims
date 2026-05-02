using Microsoft.EntityFrameworkCore;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities.Accounting;

namespace SIMS.Application.Services;

public class LedgerService : ILedgerService
{
    private readonly IServiceProvider _sp;
    private DbContext Db => (DbContext)_sp.GetService(typeof(DbContext))!;

    public LedgerService(IServiceProvider sp) => _sp = sp;

    public async Task<Guid> PostInvoiceAsync(
        Invoice invoice, int arAccountId, int carrierApAccountId,
        Guid userId, CancellationToken ct = default)
    {
        var db = Db;
        var txnId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var rows = new List<LedgerTransaction>();

        // DR: Accounts Receivable = total invoice amount
        rows.Add(new LedgerTransaction
        {
            TransactionId = txnId,
            EffectiveDate = invoice.EffectiveDate,
            AccountId = arAccountId,
            Debit = invoice.TotalAmount,
            Credit = 0,
            SourceType = "Invoice",
            SourceId = invoice.Id,
            Memo = $"Invoice {invoice.InvoiceNumber}",
            CreatedBy = userId,
            PostedAt = now
        });

        // CR: Carrier AP = gross premium
        rows.Add(new LedgerTransaction
        {
            TransactionId = txnId,
            EffectiveDate = invoice.EffectiveDate,
            AccountId = carrierApAccountId,
            Debit = 0,
            Credit = invoice.GrossPremium,
            SourceType = "Invoice",
            SourceId = invoice.Id,
            Memo = $"Gross premium — {invoice.InvoiceNumber}",
            CreatedBy = userId,
            PostedAt = now
        });

        // CR: one entry per fee line to its designated GL account
        foreach (var line in invoice.Lines)
        {
            rows.Add(new LedgerTransaction
            {
                TransactionId = txnId,
                EffectiveDate = invoice.EffectiveDate,
                AccountId = line.LedgerAccountId,
                Debit = 0,
                Credit = line.Amount,
                SourceType = "Invoice",
                SourceId = invoice.Id,
                Memo = $"{line.FeeDisplayName} — {invoice.InvoiceNumber}",
                CreatedBy = userId,
                PostedAt = now
            });
        }

        var totalDebit = rows.Sum(r => r.Debit);
        var totalCredit = rows.Sum(r => r.Credit);
        if (totalDebit != totalCredit)
            throw new InvalidOperationException(
                $"Transaction not balanced: DR {totalDebit:F4} ≠ CR {totalCredit:F4}");

        db.Set<LedgerTransaction>().AddRange(rows);
        await db.SaveChangesAsync(ct);

        return txnId;
    }
}
