using Microsoft.EntityFrameworkCore;
using SIMS.Application.Common;
using SIMS.Application.DTOs.Accounting;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Entities.Accounting;
using DomainInvoiceLine = SIMS.Domain.Entities.Accounting.InvoiceLine;

namespace SIMS.Application.Services;

public class InvoicingService : IInvoicingService
{
    private readonly IServiceProvider _sp;
    private readonly IFeeCalculationService _feeCalc;
    private readonly ILedgerService _ledger;
    private readonly ICarrierCommissionService _commissions;

    private DbContext Db => (DbContext)_sp.GetService(typeof(DbContext))!;

    public InvoicingService(IServiceProvider sp, IFeeCalculationService feeCalc,
        ILedgerService ledger, ICarrierCommissionService commissions)
    {
        _sp = sp;
        _feeCalc = feeCalc;
        _ledger = ledger;
        _commissions = commissions;
    }

    public async Task<Result<InvoiceDetailDto>> BindAsync(
        CreateInvoiceRequest req, Guid userId, CancellationToken ct = default)
    {
        var ctx = new PolicyContext(
            req.EffectiveDate, req.GrossPremium, req.StateCode,
            req.IsEndorsement, req.IsFilingState,
            req.CompanyId, req.ProducerId, req.LineOfBusiness,
            req.City, req.LicenseType, req.LocationCount, req.VehicleCount);

        var calcResult = await _feeCalc.CalculateAsync(ctx, ct);

        var db = Db;

        var arAccount = await db.Set<LedgerAccount>()
            .FirstOrDefaultAsync(a => a.InternalCode == "1200" && a.TenantId == 1, ct);
        var carrierApAccount = await db.Set<LedgerAccount>()
            .FirstOrDefaultAsync(a => a.InternalCode == "2100" && a.TenantId == 1, ct);
        var commissionAccount = await db.Set<LedgerAccount>()
            .FirstOrDefaultAsync(a => a.InternalCode == "4100" && a.TenantId == 1, ct);

        if (arAccount == null || carrierApAccount == null || commissionAccount == null)
            return Result<InvoiceDetailDto>.Failure("MISSING_GL_ACCOUNTS",
                "Required GL accounts (1200 AR, 2100 Carrier AP, 4100 Commission) not found");

        // Resolve carrier before building invoice so commission can be computed
        Guid? carrierId = null;
        string payeeName = "Carrier";

        if (req.PolicyTransactionId.HasValue)
        {
            var resolvedCarrierId = await db.Set<PolicyTransaction>()
                .Where(pt => pt.Id == req.PolicyTransactionId.Value)
                .Select(pt => (Guid?)pt.Quote.CarrierId)
                .FirstOrDefaultAsync(ct);

            if (resolvedCarrierId.HasValue)
            {
                carrierId = resolvedCarrierId;
                var name = await db.Set<Carrier>()
                    .Where(c => c.Id == resolvedCarrierId.Value)
                    .Select(c => c.Name)
                    .FirstOrDefaultAsync(ct);
                if (name != null) payeeName = name;
            }
        }

        // Look up commission rate for this carrier + LOB
        var invoiceDate = DateOnly.FromDateTime(DateTime.UtcNow);
        decimal commissionAmount = 0;
        if (carrierId.HasValue && req.GrossPremium > 0)
        {
            var rate = await _commissions.GetActiveRateAsync(carrierId.Value, req.LineOfBusiness, invoiceDate, ct);
            if (rate.HasValue)
                commissionAmount = Math.Round(req.GrossPremium * rate.Value, 4);
        }

        var seq = await db.Set<Invoice>()
            .CountAsync(i => i.TenantId == 1 && i.InvoiceDate.Year == invoiceDate.Year, ct) + 1;
        var invoiceNumber = $"INV-{invoiceDate.Year}-{seq:D5}";

        var totalFees = calcResult.Lines.Sum(l => l.Amount);
        var totalAmount = req.GrossPremium + totalFees;

        var invoice = new Invoice
        {
            InvoiceNumber = invoiceNumber,
            PolicyTransactionId = req.PolicyTransactionId,
            EffectiveDate = req.EffectiveDate,
            InvoiceDate = invoiceDate,
            GrossPremium = req.GrossPremium,
            CommissionAmount = commissionAmount,
            TotalFees = totalFees,
            TotalAmount = totalAmount,
            Status = "Posted",
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
            Lines = calcResult.Lines.Select(l => new DomainInvoiceLine
            {
                FeeRuleVersionId = l.FeeRuleVersionId,
                FeeCode = l.FeeCode,
                FeeDisplayName = l.FeeDisplayName,
                FeeCategory = l.FeeCategory,
                Amount = l.Amount,
                IsTaxable = l.IsTaxable,
                LedgerAccountId = l.LedgerAccountId,
                PayableRouting = l.PayableRouting,
                PayablePayeeId = l.PayablePayeeId
            }).ToList()
        };

        db.Set<Invoice>().Add(invoice);
        await db.SaveChangesAsync(ct);

        var txnId = await _ledger.PostInvoiceAsync(
            invoice, arAccount.Id, carrierApAccount.Id, commissionAccount.Id, userId, ct);

        invoice.LedgerTransactionId = txnId;
        await db.SaveChangesAsync(ct);

        // Carrier payable is net of commission (SMM retains commission before remitting)
        if (invoice.GrossPremium > 0)
        {
            db.Set<Payable>().Add(new Payable
            {
                TenantId = 1,
                InvoiceId = invoice.Id,
                CarrierId = carrierId,
                PayeeName = payeeName,
                GlAccountId = carrierApAccount.Id,
                Amount = invoice.GrossPremium - commissionAmount,
                PaidAmount = 0,
                InvoiceDate = invoice.InvoiceDate,
                DueDate = invoice.InvoiceDate.AddDays(30),
                Status = "Open",
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);
        }

        return Result<InvoiceDetailDto>.Success(await LoadDetailAsync(invoice.Id, ct));
    }

    public async Task<IReadOnlyList<InvoiceSummaryDto>> GetInvoicesAsync(CancellationToken ct = default)
    {
        return await Db.Set<Invoice>()
            .OrderByDescending(i => i.InvoiceDate)
            .ThenByDescending(i => i.Id)
            .Select(i => new InvoiceSummaryDto(
                i.Id, i.InvoiceNumber, i.InvoiceDate, i.EffectiveDate,
                i.GrossPremium, i.TotalFees, i.TotalAmount, i.Status))
            .ToListAsync(ct);
    }

    public async Task<Result<InvoiceDetailDto>> GetInvoiceAsync(long id, CancellationToken ct = default)
    {
        var exists = await Db.Set<Invoice>().AnyAsync(i => i.Id == id, ct);
        if (!exists)
            return Result<InvoiceDetailDto>.Failure("NOT_FOUND", $"Invoice {id} not found");

        return Result<InvoiceDetailDto>.Success(await LoadDetailAsync(id, ct));
    }

    private async Task<InvoiceDetailDto> LoadDetailAsync(long id, CancellationToken ct)
    {
        var db = Db;

        var invoice = await db.Set<Invoice>()
            .Include(i => i.Lines)
                .ThenInclude(l => l.LedgerAccount)
            .FirstAsync(i => i.Id == id, ct);

        var ledgerRows = await db.Set<LedgerTransaction>()
            .Include(t => t.Account)
            .Where(t => t.TransactionId == invoice.LedgerTransactionId)
            .OrderBy(t => t.Id)
            .ToListAsync(ct);

        return new InvoiceDetailDto(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.InvoiceDate,
            invoice.EffectiveDate,
            invoice.GrossPremium,
            invoice.TotalFees,
            invoice.TotalAmount,
            invoice.Status,
            invoice.LedgerTransactionId,
            invoice.Lines
                .OrderBy(l => l.Id)
                .Select(l => new InvoiceLineDto(
                    l.Id,
                    l.FeeCode,
                    l.FeeDisplayName,
                    l.FeeCategory,
                    l.Amount,
                    l.IsTaxable,
                    l.LedgerAccount.InternalCode,
                    l.LedgerAccount.ExternalLabel))
                .ToList(),
            ledgerRows.Select(t => new LedgerEntryDto(
                t.Id,
                t.Account.InternalCode,
                t.Account.ExternalLabel,
                t.Debit,
                t.Credit,
                t.Memo))
                .ToList()
        );
    }
}
