using Microsoft.EntityFrameworkCore;
using SIMS.Application.DTOs.Accounting;
using SIMS.Application.DTOs.Reports;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Entities.Accounting;

namespace SIMS.Application.Services;

public class ReportService : IReportService
{
    private readonly IServiceProvider _sp;
    private DbContext Db => (DbContext)_sp.GetService(typeof(DbContext))!;

    public ReportService(IServiceProvider sp) => _sp = sp;

    public async Task<TrustReconciliationDto> GetTrustReconciliationAsync(
        DateOnly? asOf = null, CancellationToken ct = default)
    {
        var effectiveAsOf = asOf ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var trustAccount = await Db.Set<LedgerAccount>()
            .FirstOrDefaultAsync(a => a.InternalCode == "1100" && a.TenantId == 1, ct);

        if (trustAccount == null)
            return new TrustReconciliationDto(effectiveAsOf, 0, 0, 0, 0, Array.Empty<TrustTransactionLineDto>());

        var allTxns = await Db.Set<LedgerTransaction>()
            .Where(t => t.AccountId == trustAccount.Id
                        && t.TenantId == 1
                        && t.PostingStatus == "Posted"
                        && t.EffectiveDate <= effectiveAsOf)
            .OrderBy(t => t.EffectiveDate)
            .ThenBy(t => t.Id)
            .ToListAsync(ct);

        // Asset account: Debit increases balance, Credit decreases
        decimal running = 0;
        var allLines = new List<TrustTransactionLineDto>();
        foreach (var t in allTxns)
        {
            running += t.Debit - t.Credit;
            allLines.Add(new TrustTransactionLineDto(
                t.PostedAt, t.EffectiveDate, t.SourceType, t.Memo,
                t.Debit, t.Credit, running));
        }

        var recentCutoff = effectiveAsOf.AddDays(-30);
        var recentActivity = allLines.Where(l => l.EffectiveDate >= recentCutoff).ToList();

        var unappliedReceipts = await Db.Set<Receipt>()
            .Where(r => r.TenantId == 1 && (r.Status == "Open" || r.Status == "PartiallyApplied"))
            .SumAsync(r => r.Amount - r.AppliedAmount, ct);

        var openInvoices = await Db.Set<Invoice>()
            .Where(i => i.TenantId == 1 && (i.Status == "Posted" || i.Status == "PartiallyPaid"))
            .SumAsync(i => i.TotalAmount - i.ClearedAmount, ct);

        // Difference: trust balance should equal unapplied receipts + outstanding AR
        var reconcilingDifference = running - unappliedReceipts - openInvoices;

        return new TrustReconciliationDto(
            effectiveAsOf, running, unappliedReceipts, openInvoices,
            reconcilingDifference, recentActivity);
    }

    public async Task<PayableAgingDto> GetCarrierPayableAgingAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var payables = await Db.Set<Payable>()
            .Include(p => p.Invoice)
            .Where(p => p.TenantId == 1
                        && p.CarrierId != null
                        && (p.Status == "Open" || p.Status == "PartiallyPaid"))
            .OrderBy(p => p.DueDate)
            .Select(p => new OpenPayableDto(
                p.Id, p.InvoiceId, p.Invoice.InvoiceNumber,
                p.PayeeName, p.PayeeId, p.CarrierId,
                p.Amount, p.PaidAmount, p.Amount - p.PaidAmount,
                p.InvoiceDate, p.DueDate,
                Math.Max(0, today.DayNumber - p.DueDate.DayNumber),
                p.Status))
            .ToListAsync(ct);

        return BuildPayableAging(payables);
    }

    public async Task<PayableAgingDto> GetSlTaxAgingAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var payables = await Db.Set<Payable>()
            .Include(p => p.Invoice)
            .Where(p => p.TenantId == 1
                        && p.CarrierId == null
                        && (p.Status == "Open" || p.Status == "PartiallyPaid"))
            .OrderBy(p => p.DueDate)
            .Select(p => new OpenPayableDto(
                p.Id, p.InvoiceId, p.Invoice.InvoiceNumber,
                p.PayeeName, p.PayeeId, null,
                p.Amount, p.PaidAmount, p.Amount - p.PaidAmount,
                p.InvoiceDate, p.DueDate,
                Math.Max(0, today.DayNumber - p.DueDate.DayNumber),
                p.Status))
            .ToListAsync(ct);

        return BuildPayableAging(payables);
    }

    public async Task<BrokerArAgingDto> GetBrokerArAgingAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var raw = await (
            from inv in Db.Set<Invoice>()
            where inv.TenantId == 1 && (inv.Status == "Posted" || inv.Status == "PartiallyPaid")
            join pt in Db.Set<PolicyTransaction>() on inv.PolicyTransactionId equals pt.Id into ptGroup
            from pt in ptGroup.DefaultIfEmpty()
            join pol in Db.Set<Policy>() on pt.PolicyId equals pol.Id into polGroup
            from pol in polGroup.DefaultIfEmpty()
            join sub in Db.Set<Submission>() on pol.SubmissionId equals sub.Id into subGroup
            from sub in subGroup.DefaultIfEmpty()
            join agent in Db.Set<Agent>() on sub.AgentId equals agent.Id into agentGroup
            from agent in agentGroup.DefaultIfEmpty()
            select new
            {
                inv.Id,
                inv.InvoiceNumber,
                inv.TotalAmount,
                inv.ClearedAmount,
                inv.InvoiceDate,
                inv.Status,
                AgentName = agent.Name,  // null when left join finds no match
                AgentId = agent.Id,
            }
        ).ToListAsync(ct);

        var receivables = raw.Select(r =>
        {
            var dueDate = r.InvoiceDate.AddDays(30);
            return new OpenReceivableDto(
                r.Id,
                r.InvoiceNumber,
                r.AgentName ?? "Direct",
                r.AgentId == Guid.Empty ? (Guid?)null : r.AgentId,
                r.TotalAmount,
                r.ClearedAmount,
                r.TotalAmount - r.ClearedAmount,
                r.InvoiceDate,
                dueDate,
                Math.Max(0, today.DayNumber - dueDate.DayNumber),
                r.Status
            );
        }).ToList();

        decimal Bucket(OpenReceivableDto r, int from, int to)
        {
            var d = r.DaysOutstanding;
            return d >= from && (to < 0 || d <= to) ? r.Balance : 0;
        }

        var summary = new AgingBucketDto(
            receivables.Sum(r => Bucket(r, 0, 30)),
            receivables.Sum(r => Bucket(r, 31, 60)),
            receivables.Sum(r => Bucket(r, 61, 90)),
            receivables.Sum(r => Bucket(r, 91, -1)),
            receivables.Sum(r => r.Balance)
        );

        var rows = receivables
            .GroupBy(r => r.AgentName)
            .Select(g => new BrokerArRowDto(
                g.Key,
                g.First().AgentId,
                g.Sum(r => Bucket(r, 0, 30)),
                g.Sum(r => Bucket(r, 31, 60)),
                g.Sum(r => Bucket(r, 61, 90)),
                g.Sum(r => Bucket(r, 91, -1)),
                g.Sum(r => r.Balance)
            ))
            .OrderByDescending(r => r.Total)
            .ToList();

        return new BrokerArAgingDto(summary, rows, receivables);
    }

    public async Task<CommissionSummaryDto> GetCommissionSummaryAsync(
        int months = 12, CancellationToken ct = default)
    {
        var today = DateTime.UtcNow;
        var cutoff = new DateOnly(today.Year, today.Month, 1).AddMonths(-(months - 1));

        var invoiceData = await Db.Set<Invoice>()
            .Where(i => i.TenantId == 1 && i.Status != "Voided" && i.InvoiceDate >= cutoff)
            .GroupBy(i => new { i.InvoiceDate.Year, i.InvoiceDate.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Earned = g.Sum(i => i.CommissionAmount),
                AgentPaid = g.Sum(i => i.AgentCommissionAmount),
                Count = g.Count(),
            })
            .ToListAsync(ct);

        var receiptData = await Db.Set<Receipt>()
            .Where(r => r.TenantId == 1 && r.Status != "Voided" && r.ReceivedDate >= cutoff)
            .GroupBy(r => new { r.ReceivedDate.Year, r.ReceivedDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Cash = g.Sum(r => r.Amount) })
            .ToListAsync(ct);

        var cashByPeriod = receiptData.ToDictionary(r => (r.Year, r.Month), r => r.Cash);

        var periods = invoiceData
            .Select(i => new CommissionPeriodDto(
                i.Year, i.Month,
                i.Earned,
                i.AgentPaid,
                i.Earned - i.AgentPaid,
                cashByPeriod.TryGetValue((i.Year, i.Month), out var cash) ? cash : 0,
                i.Count))
            .OrderBy(p => p.Year).ThenBy(p => p.Month)
            .ToList();

        return new CommissionSummaryDto(
            periods,
            periods.Sum(p => p.Earned),
            periods.Sum(p => p.AgentPaid),
            periods.Sum(p => p.NetRetained),
            periods.Sum(p => p.CashReceived));
    }

    public async Task<InvoiceTotalsByPolicyTransactionDto> GetInvoiceTotalsByPolicyTransactionAsync(CancellationToken ct = default)
    {
        var rows = await (
            from invoice in Db.Set<Invoice>()
            where invoice.TenantId == 1 && invoice.Status != "Voided"
            join txn in Db.Set<PolicyTransaction>() on invoice.PolicyTransactionId equals txn.Id into txnGroup
            from txn in txnGroup.DefaultIfEmpty()
            join version in Db.Set<PolicyVersion>() on invoice.PolicyVersionId equals version.Id into versionGroup
            from version in versionGroup.DefaultIfEmpty()
            group new { invoice, txn, version } by new
            {
                invoice.PolicyTransactionId,
                TransactionNumber = txn != null ? txn.TransactionNumber : null,
                TransactionType = txn != null ? (Domain.Enums.TransactionType?)txn.TransactionType : null,
                invoice.PolicyVersionId,
                PolicyVersionNumber = version != null ? (int?)version.VersionNumber : null,
            }
            into g
            select new InvoiceTotalsByPolicyTransactionRowDto(
                g.Key.PolicyTransactionId,
                g.Key.TransactionNumber ?? "Unlinked",
                g.Key.TransactionType,
                g.Key.PolicyVersionId,
                g.Key.PolicyVersionNumber,
                g.Count(),
                g.Sum(x => x.invoice.GrossPremium),
                g.Sum(x => x.invoice.TotalFees),
                g.Sum(x => x.invoice.TotalAmount))
        ).ToListAsync(ct);

        return new InvoiceTotalsByPolicyTransactionDto(rows.OrderByDescending(r => r.TotalAmount).ToList());
    }

    public async Task<InvoiceTotalsByProgramDto> GetInvoiceTotalsByProgramAsync(Guid? programId = null, CancellationToken ct = default)
    {
        var availablePrograms = await Db.Set<ProgramConfiguration>()
            .Where(p => p.IsActive && !p.IsDeleted)
            .OrderBy(p => p.Name)
            .Select(p => new InvoiceTotalsByProgramOptionDto(p.Id, p.Name, p.Code))
            .ToListAsync(ct);

        var rows = await (
            from invoice in Db.Set<Invoice>()
            where invoice.TenantId == 1 && invoice.Status != "Voided"
            join txn in Db.Set<PolicyTransaction>() on invoice.PolicyTransactionId equals txn.Id into txnGroup
            from txn in txnGroup.DefaultIfEmpty()
            join policy in Db.Set<Policy>() on txn.PolicyId equals policy.Id into policyGroup
            from policy in policyGroup.DefaultIfEmpty()
            where !programId.HasValue || (policy != null && policy.ProgramId == programId.Value)
            join program in Db.Set<ProgramConfiguration>() on policy.ProgramId equals program.Id into programGroup
            from program in programGroup.DefaultIfEmpty()
            group invoice by new
            {
                ProgramId = policy != null ? policy.ProgramId : null,
                ProgramName = program != null ? program.Name : "Unassigned",
                ProgramCode = program != null ? program.Code : null
            }
            into g
            select new InvoiceTotalsByProgramRowDto(
                g.Key.ProgramId,
                g.Key.ProgramName,
                g.Key.ProgramCode,
                g.Count(),
                g.Sum(i => i.GrossPremium),
                g.Sum(i => i.TotalFees),
                g.Sum(i => i.TotalAmount),
                g.Sum(i => i.CommissionAmount),
                g.Sum(i => i.AgentCommissionAmount),
                g.Sum(i => i.CommissionAmount - i.AgentCommissionAmount))
        ).ToListAsync(ct);

        return new InvoiceTotalsByProgramDto(rows.OrderByDescending(r => r.TotalAmount).ToList(), availablePrograms);
    }

    private static PayableAgingDto BuildPayableAging(List<OpenPayableDto> payables)
    {
        decimal Bucket(OpenPayableDto p, int from, int to)
        {
            var d = p.DaysOutstanding;
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
            .GroupBy(p => new { p.PayeeId, p.PayeeName })
            .Select(g => new AgingRowDto(
                g.Key.PayeeName, g.Key.PayeeId, g.First().CarrierId,
                g.Sum(p => Bucket(p, 0, 30)),
                g.Sum(p => Bucket(p, 31, 60)),
                g.Sum(p => Bucket(p, 61, 90)),
                g.Sum(p => Bucket(p, 91, -1)),
                g.Sum(p => p.Balance)))
            .OrderByDescending(r => r.Total)
            .ToList();

        return new PayableAgingDto(summary, rows, payables);
    }
}
