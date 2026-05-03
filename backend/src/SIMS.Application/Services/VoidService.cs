using Microsoft.EntityFrameworkCore;
using SIMS.Application.Common;
using SIMS.Application.DTOs.Accounting;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities.Accounting;

namespace SIMS.Application.Services;

public class VoidService : IVoidService
{
    private readonly IServiceProvider _sp;
    private readonly ILedgerService _ledger;
    private DbContext Db => (DbContext)_sp.GetService(typeof(DbContext))!;

    public VoidService(IServiceProvider sp, ILedgerService ledger)
    {
        _sp = sp;
        _ledger = ledger;
    }

    public async Task<VoidResultDto> VoidReceiptAsync(
        long receiptId, string? reason, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        var db = Db;
        var receipt = await db.Set<Receipt>()
            .Include(r => r.Applications)
            .FirstOrDefaultAsync(r => r.Id == receiptId && r.TenantId == 1, ct);

        if (receipt == null)
            return Fail("NOT_FOUND", $"Receipt {receiptId} not found");
        if (receipt.Status == "Voided")
            return Fail("ALREADY_VOIDED", "Receipt is already voided");

        var priorDay = CheckPriorDay(receipt.ReceivedDate, isAdmin);
        if (priorDay != null) return priorDay;

        var activeApps = receipt.Applications
            .Where(a => !IsVoidedApplication(db, a.LedgerTransactionId))
            .ToList();

        if (activeApps.Count > 0)
            return Fail("HAS_APPLICATIONS",
                $"Receipt has {activeApps.Count} active cash application(s). Void those first.");

        var effectiveDate = receipt.ReceivedDate;
        var reversalId = await _ledger.ReverseTransactionGroupAsync(
            receipt.LedgerTransactionId, reason ?? "Void", userId, effectiveDate, ct);

        receipt.Status = "Voided";
        await db.SaveChangesAsync(ct);

        return Ok(reversalId);
    }

    public async Task<VoidResultDto> VoidCashApplicationAsync(
        long cashApplicationId, string? reason, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        var db = Db;
        var app = await db.Set<CashApplication>()
            .Include(a => a.Receipt)
            .Include(a => a.Invoice)
            .FirstOrDefaultAsync(a => a.Id == cashApplicationId && a.TenantId == 1, ct);

        if (app == null)
            return Fail("NOT_FOUND", $"Cash application {cashApplicationId} not found");

        if (IsVoidedApplication(db, app.LedgerTransactionId))
            return Fail("ALREADY_VOIDED", "Cash application is already voided");

        var priorDay = CheckPriorDay(app.Receipt.ReceivedDate, isAdmin);
        if (priorDay != null) return priorDay;

        var effectiveDate = app.Receipt.ReceivedDate;
        var reversalId = await _ledger.ReverseTransactionGroupAsync(
            app.LedgerTransactionId, reason ?? "Void", userId, effectiveDate, ct);

        // Restore receipt and invoice balances
        app.Receipt.AppliedAmount -= app.GrossApplied;
        if (app.Receipt.AppliedAmount <= 0)
        {
            app.Receipt.AppliedAmount = 0;
            app.Receipt.Status = "Open";
        }
        else
        {
            app.Receipt.Status = "PartiallyApplied";
        }

        app.Invoice.ClearedAmount -= app.GrossApplied;
        if (app.Invoice.ClearedAmount <= 0)
        {
            app.Invoice.ClearedAmount = 0;
            app.Invoice.Status = "Posted";
        }
        else if (app.Invoice.ClearedAmount < app.Invoice.TotalAmount)
        {
            app.Invoice.Status = "PartiallyPaid";
        }

        db.Set<CashApplication>().Remove(app);
        await db.SaveChangesAsync(ct);

        return Ok(reversalId);
    }

    public async Task<VoidResultDto> VoidInvoiceAsync(
        long invoiceId, string? reason, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        var db = Db;
        var invoice = await db.Set<Invoice>()
            .FirstOrDefaultAsync(i => i.Id == invoiceId && i.TenantId == 1, ct);

        if (invoice == null)
            return Fail("NOT_FOUND", $"Invoice {invoiceId} not found");
        if (invoice.Status == "Voided")
            return Fail("ALREADY_VOIDED", "Invoice is already voided");

        if (invoice.ClearedAmount > 0)
            return Fail("HAS_PAYMENTS",
                "Invoice has cash applications. Void the cash applications first.");

        var priorDay = CheckPriorDay(invoice.EffectiveDate, isAdmin);
        if (priorDay != null) return priorDay;

        var reversalId = await _ledger.ReverseTransactionGroupAsync(
            invoice.LedgerTransactionId, reason ?? "Void", userId, invoice.EffectiveDate, ct);

        invoice.Status = "Voided";
        await db.SaveChangesAsync(ct);

        return Ok(reversalId);
    }

    public async Task<VoidResultDto> VoidDisbursementAsync(
        long disbursementId, string? reason, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        var db = Db;
        var disbursement = await db.Set<Disbursement>()
            .Include(d => d.Lines)
                .ThenInclude(l => l.Payable)
            .FirstOrDefaultAsync(d => d.Id == disbursementId && d.TenantId == 1, ct);

        if (disbursement == null)
            return Fail("NOT_FOUND", $"Disbursement {disbursementId} not found");
        if (disbursement.Status == "Voided")
            return Fail("ALREADY_VOIDED", "Disbursement is already voided");

        Guid? reversalId = null;

        if (disbursement.Status == "Posted" && disbursement.LedgerTransactionId.HasValue)
        {
            var priorDay = CheckPriorDay(disbursement.PaymentDate, isAdmin);
            if (priorDay != null) return priorDay;

            reversalId = await _ledger.ReverseTransactionGroupAsync(
                disbursement.LedgerTransactionId.Value, reason ?? "Void", userId,
                disbursement.PaymentDate, ct);

            // Restore payable balances
            foreach (var line in disbursement.Lines)
            {
                line.Payable.PaidAmount -= line.Amount;
                if (line.Payable.PaidAmount < 0) line.Payable.PaidAmount = 0;
                line.Payable.Status = line.Payable.PaidAmount <= 0
                    ? "Open"
                    : "PartiallyPaid";
            }
        }

        disbursement.Status = "Voided";
        disbursement.Notes = string.IsNullOrWhiteSpace(reason)
            ? disbursement.Notes
            : $"{disbursement.Notes}\nVoided: {reason}".Trim();

        await db.SaveChangesAsync(ct);
        return Ok(reversalId);
    }

    // ---- Helpers ----

    private static VoidResultDto? CheckPriorDay(DateOnly effectiveDate, bool isAdmin)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (effectiveDate < today && !isAdmin)
            return Fail("PRIOR_PERIOD",
                "Prior-period voids require Admin access. Contact your administrator.");
        return null;
    }

    private static bool IsVoidedApplication(DbContext db, Guid ledgerTransactionId)
    {
        return db.Set<LedgerTransaction>()
            .Any(t => t.TransactionId == ledgerTransactionId && t.PostingStatus == "Voided");
    }

    private static VoidResultDto Fail(string code, string message) =>
        new(false, code, message, null);

    private static VoidResultDto Ok(Guid? reversalId) =>
        new(true, null, null, reversalId);
}
