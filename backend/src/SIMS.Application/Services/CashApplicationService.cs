using Microsoft.EntityFrameworkCore;
using SIMS.Application.Common;
using SIMS.Application.DTOs.Accounting;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities.Accounting;

namespace SIMS.Application.Services;

public class CashApplicationService : ICashApplicationService
{
    private readonly IServiceProvider _sp;
    private readonly ILedgerService _ledger;
    private readonly ICashDistributionService _dist;
    private DbContext Db => (DbContext)_sp.GetService(typeof(DbContext))!;

    public CashApplicationService(IServiceProvider sp, ILedgerService ledger, ICashDistributionService dist)
    {
        _sp = sp;
        _ledger = ledger;
        _dist = dist;
    }

    public async Task<IReadOnlyList<OpenInvoiceDto>> GetOpenInvoicesAsync(CancellationToken ct = default)
    {
        return await Db.Set<Invoice>()
            .Where(i => i.Status == "Posted" || i.Status == "PartiallyPaid")
            .OrderByDescending(i => i.InvoiceDate)
            .Select(i => new OpenInvoiceDto(
                i.Id, i.InvoiceNumber, i.InvoiceDate,
                i.TotalAmount, i.ClearedAmount,
                i.TotalAmount - i.ClearedAmount, i.Status))
            .ToListAsync(ct);
    }

    public async Task<Result<ApplyCashResultDto>> ApplyAsync(
        ApplyCashRequest req, Guid userId, CancellationToken ct = default)
    {
        if (req.Lines.Count == 0)
            return Result<ApplyCashResultDto>.Failure("NO_LINES", "At least one application line is required");

        var db = Db;

        var receipt = await db.Set<Receipt>()
            .Include(r => r.Applications)
            .FirstOrDefaultAsync(r => r.Id == req.ReceiptId, ct);

        if (receipt == null)
            return Result<ApplyCashResultDto>.Failure("NOT_FOUND", $"Receipt {req.ReceiptId} not found");

        if (receipt.Status == "Applied")
            return Result<ApplyCashResultDto>.Failure("ALREADY_APPLIED", "Receipt is fully applied");

        if (receipt.Status == "Voided")
            return Result<ApplyCashResultDto>.Failure("VOIDED", "Receipt has been voided");

        var unappliedAccount = await db.Set<LedgerAccount>()
            .FirstOrDefaultAsync(a => a.InternalCode == "1250" && a.TenantId == 1, ct);
        var commExpAccount = await db.Set<LedgerAccount>()
            .FirstOrDefaultAsync(a => a.InternalCode == "5100" && a.TenantId == 1, ct);
        var arAccount = await db.Set<LedgerAccount>()
            .FirstOrDefaultAsync(a => a.InternalCode == "1200" && a.TenantId == 1, ct);

        if (unappliedAccount == null || commExpAccount == null || arAccount == null)
            return Result<ApplyCashResultDto>.Failure("MISSING_GL_ACCOUNTS",
                "Required GL accounts (1250, 5100, 1200) not found");

        var invoiceIds = req.Lines.Select(l => l.InvoiceId).ToList();
        var invoices = await db.Set<Invoice>()
            .Include(i => i.Lines)
            .Where(i => invoiceIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, ct);

        foreach (var line in req.Lines)
        {
            if (!invoices.TryGetValue(line.InvoiceId, out var invoice))
                return Result<ApplyCashResultDto>.Failure("INVOICE_NOT_FOUND",
                    $"Invoice {line.InvoiceId} not found");

            var openBalance = invoice.TotalAmount - invoice.ClearedAmount;
            if (line.GrossApplied > openBalance + 0.005m)
                return Result<ApplyCashResultDto>.Failure("OVER_APPLIED",
                    $"Invoice {invoice.InvoiceNumber} open balance is {openBalance:F2}, cannot apply {line.GrossApplied:F2}");

            if (line.CommissionAmount < 0 || line.CommissionAmount > line.GrossApplied)
                return Result<ApplyCashResultDto>.Failure("INVALID_COMMISSION",
                    $"Commission amount must be between 0 and gross applied for invoice {invoice.InvoiceNumber}");
        }

        var totalGrossApplied = req.Lines.Sum(l => l.GrossApplied);
        var remainingCapacity = receipt.Amount - receipt.AppliedAmount;
        if (totalGrossApplied > remainingCapacity + 0.005m)
            return Result<ApplyCashResultDto>.Failure("EXCEEDS_RECEIPT",
                $"Gross applied ({totalGrossApplied:F2}) exceeds receipt remaining balance ({remainingCapacity:F2})");

        var newApplications = new List<CashApplication>();

        foreach (var line in req.Lines)
        {
            var invoice = invoices[line.InvoiceId];
            var netApplied = line.GrossApplied - line.CommissionAmount;

            var txnId = await _ledger.PostCashApplicationAsync(
                receipt, invoice,
                line.GrossApplied, line.CommissionAmount,
                unappliedAccount.Id, commExpAccount.Id, arAccount.Id,
                userId, ct);

            var application = new CashApplication
            {
                ReceiptId = receipt.Id,
                InvoiceId = invoice.Id,
                GrossApplied = line.GrossApplied,
                CommissionAmount = line.CommissionAmount,
                NetApplied = netApplied,
                LedgerTransactionId = txnId,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow
            };

            db.Set<CashApplication>().Add(application);
            newApplications.Add(application);

            // Update invoice cleared amount and status
            invoice.ClearedAmount += line.GrossApplied;
            invoice.Status = invoice.ClearedAmount >= invoice.TotalAmount - 0.005m
                ? "Paid"
                : "PartiallyPaid";
        }

        // Update receipt applied amount and status (track gross so receipt.Amount - AppliedAmount = unused wire)
        receipt.AppliedAmount += req.Lines.Sum(l => l.GrossApplied);
        receipt.Status = receipt.AppliedAmount >= receipt.Amount - 0.005m
            ? "Applied"
            : "PartiallyApplied";

        await db.SaveChangesAsync(ct);

        // Generate distribution instructions for each payable invoice line
        var trustAccount = await db.Set<LedgerAccount>()
            .FirstOrDefaultAsync(a => a.InternalCode == "1100" && a.TenantId == 1, ct);
        if (trustAccount != null)
        {
            foreach (var app in newApplications)
                await _dist.GenerateInstructionsForApplicationAsync(
                    app, invoices[app.InvoiceId], trustAccount.Id, userId, ct);
        }

        // Reload for response
        var updatedReceipt = await db.Set<Receipt>()
            .Include(r => r.Applications)
                .ThenInclude(a => a.Invoice)
            .FirstAsync(r => r.Id == receipt.Id, ct);

        return Result<ApplyCashResultDto>.Success(new ApplyCashResultDto(
            updatedReceipt.Id,
            updatedReceipt.ReceiptNumber,
            updatedReceipt.Amount,
            updatedReceipt.AppliedAmount,
            updatedReceipt.Amount - updatedReceipt.AppliedAmount,
            updatedReceipt.Status,
            updatedReceipt.Applications
                .OrderBy(a => a.Id)
                .Select(a => new ReceiptApplicationDto(
                    a.Id, a.InvoiceId, a.Invoice.InvoiceNumber,
                    a.GrossApplied, a.CommissionAmount, a.NetApplied, a.CreatedAt))
                .ToList()
        ));
    }
}
