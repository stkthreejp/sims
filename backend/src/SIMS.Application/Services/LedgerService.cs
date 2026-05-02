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

        rows.Add(new LedgerTransaction
        {
            TransactionId = txnId, EffectiveDate = invoice.EffectiveDate,
            AccountId = arAccountId, Debit = invoice.TotalAmount, Credit = 0,
            SourceType = "Invoice", SourceId = invoice.Id,
            Memo = $"Invoice {invoice.InvoiceNumber}", CreatedBy = userId, PostedAt = now
        });

        rows.Add(new LedgerTransaction
        {
            TransactionId = txnId, EffectiveDate = invoice.EffectiveDate,
            AccountId = carrierApAccountId, Debit = 0, Credit = invoice.GrossPremium,
            SourceType = "Invoice", SourceId = invoice.Id,
            Memo = $"Gross premium — {invoice.InvoiceNumber}", CreatedBy = userId, PostedAt = now
        });

        foreach (var line in invoice.Lines)
        {
            rows.Add(new LedgerTransaction
            {
                TransactionId = txnId, EffectiveDate = invoice.EffectiveDate,
                AccountId = line.LedgerAccountId, Debit = 0, Credit = line.Amount,
                SourceType = "Invoice", SourceId = invoice.Id,
                Memo = $"{line.FeeDisplayName} — {invoice.InvoiceNumber}", CreatedBy = userId, PostedAt = now
            });
        }

        AssertBalanced(rows);
        db.Set<LedgerTransaction>().AddRange(rows);
        await db.SaveChangesAsync(ct);
        return txnId;
    }

    public async Task<Guid> PostReceiptAsync(
        Receipt receipt, int trustAccountId, int unappliedCashAccountId,
        Guid userId, CancellationToken ct = default)
    {
        var db = Db;
        var txnId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var effectiveDate = receipt.ReceivedDate;

        var rows = new List<LedgerTransaction>
        {
            new() {
                TransactionId = txnId, EffectiveDate = effectiveDate,
                AccountId = trustAccountId, Debit = receipt.Amount, Credit = 0,
                SourceType = "Receipt", SourceId = receipt.Id,
                Memo = $"Wire received — {receipt.ReceiptNumber}", CreatedBy = userId, PostedAt = now
            },
            new() {
                TransactionId = txnId, EffectiveDate = effectiveDate,
                AccountId = unappliedCashAccountId, Debit = 0, Credit = receipt.Amount,
                SourceType = "Receipt", SourceId = receipt.Id,
                Memo = $"Unapplied — {receipt.ReceiptNumber}", CreatedBy = userId, PostedAt = now
            }
        };

        AssertBalanced(rows);
        db.Set<LedgerTransaction>().AddRange(rows);
        await db.SaveChangesAsync(ct);
        return txnId;
    }

    public async Task<Guid> PostCashApplicationAsync(
        Receipt receipt, Invoice invoice,
        decimal grossApplied, decimal commissionAmount,
        int unappliedCashAccountId, int commissionExpenseAccountId, int arAccountId,
        Guid userId, CancellationToken ct = default)
    {
        var db = Db;
        var txnId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var netApplied = grossApplied - commissionAmount;
        var effectiveDate = receipt.ReceivedDate;

        var rows = new List<LedgerTransaction>
        {
            // DR Unapplied Cash (net of commission)
            new() {
                TransactionId = txnId, EffectiveDate = effectiveDate,
                AccountId = unappliedCashAccountId, Debit = netApplied, Credit = 0,
                SourceType = "CashApplication", SourceId = receipt.Id,
                Memo = $"Apply {receipt.ReceiptNumber} → {invoice.InvoiceNumber}", CreatedBy = userId, PostedAt = now
            },
            // DR Broker Commission Expense
            new() {
                TransactionId = txnId, EffectiveDate = effectiveDate,
                AccountId = commissionExpenseAccountId, Debit = commissionAmount, Credit = 0,
                SourceType = "CashApplication", SourceId = receipt.Id,
                Memo = $"Commission — {invoice.InvoiceNumber}", CreatedBy = userId, PostedAt = now
            },
            // CR Accounts Receivable
            new() {
                TransactionId = txnId, EffectiveDate = effectiveDate,
                AccountId = arAccountId, Debit = 0, Credit = grossApplied,
                SourceType = "CashApplication", SourceId = receipt.Id,
                Memo = $"Clear AR — {invoice.InvoiceNumber}", CreatedBy = userId, PostedAt = now
            }
        };

        AssertBalanced(rows);
        db.Set<LedgerTransaction>().AddRange(rows);
        await db.SaveChangesAsync(ct);
        return txnId;
    }

    public async Task<Guid> PostDisbursementAsync(
        Disbursement disbursement,
        int trustAccountId,
        Guid userId,
        CancellationToken ct = default)
    {
        var db = Db;
        var txnId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var effectiveDate = disbursement.PaymentDate;

        var rows = new List<LedgerTransaction>();
        foreach (var line in disbursement.Lines)
        {
            rows.Add(new LedgerTransaction
            {
                TransactionId = txnId, EffectiveDate = effectiveDate,
                AccountId = line.Payable.GlAccountId, Debit = line.Amount, Credit = 0,
                SourceType = "Disbursement", SourceId = disbursement.Id,
                Memo = $"{disbursement.DisbursementNumber} — {line.Payable.Invoice.InvoiceNumber}",
                CreatedBy = userId, PostedAt = now
            });
            rows.Add(new LedgerTransaction
            {
                TransactionId = txnId, EffectiveDate = effectiveDate,
                AccountId = trustAccountId, Debit = 0, Credit = line.Amount,
                SourceType = "Disbursement", SourceId = disbursement.Id,
                Memo = $"{disbursement.DisbursementNumber} — {disbursement.PayeeName}",
                CreatedBy = userId, PostedAt = now
            });
        }

        AssertBalanced(rows);
        db.Set<LedgerTransaction>().AddRange(rows);
        await db.SaveChangesAsync(ct);
        return txnId;
    }

    public async Task<Guid> PostDistributionSweepAsync(
        CashMovementInstruction instruction,
        int trustAccountId,
        Guid userId,
        CancellationToken ct = default)
    {
        var db = Db;
        var txnId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var rows = new List<LedgerTransaction>
        {
            // DR: payable liability (clears the balance owed to payee)
            new() {
                TransactionId = txnId, EffectiveDate = DateOnly.FromDateTime(now),
                AccountId = instruction.DistributionGlAccountId, Debit = instruction.Amount, Credit = 0,
                SourceType = "Distribution", SourceId = instruction.Id,
                Memo = $"Wire sweep — instruction {instruction.Id}", CreatedBy = userId, PostedAt = now
            },
            // CR: Trust Account (cash leaves trust)
            new() {
                TransactionId = txnId, EffectiveDate = DateOnly.FromDateTime(now),
                AccountId = trustAccountId, Debit = 0, Credit = instruction.Amount,
                SourceType = "Distribution", SourceId = instruction.Id,
                Memo = $"Wire sweep — instruction {instruction.Id}", CreatedBy = userId, PostedAt = now
            }
        };

        AssertBalanced(rows);
        db.Set<LedgerTransaction>().AddRange(rows);
        await db.SaveChangesAsync(ct);
        return txnId;
    }

    private static void AssertBalanced(List<LedgerTransaction> rows)
    {
        var dr = rows.Sum(r => r.Debit);
        var cr = rows.Sum(r => r.Credit);
        if (dr != cr)
            throw new InvalidOperationException($"Transaction not balanced: DR {dr:F4} ≠ CR {cr:F4}");
    }
}
