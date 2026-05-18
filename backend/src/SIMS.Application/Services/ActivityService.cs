using Microsoft.EntityFrameworkCore;
using SIMS.Application.DTOs.Accounting;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Entities.Accounting;

namespace SIMS.Application.Services;

public class ActivityService : IActivityService
{
    private readonly IServiceProvider _sp;
    private DbContext Db => (DbContext)_sp.GetService(typeof(DbContext))!;

    public ActivityService(IServiceProvider sp) => _sp = sp;

    public async Task<IReadOnlyList<ActivityEventDto>> GetActivityAsync(
        ActivityFilterRequest filter, bool isAdmin, CancellationToken ct = default)
    {
        var query = Db.Set<LedgerTransaction>()
            .Include(t => t.Account)
            .Where(t => t.TenantId == 1)
            .AsQueryable();

        if (filter.FromDate.HasValue)
            query = query.Where(t => t.EffectiveDate >= filter.FromDate.Value);
        if (filter.ToDate.HasValue)
            query = query.Where(t => t.EffectiveDate <= filter.ToDate.Value);
        if (!string.IsNullOrEmpty(filter.SourceType))
            query = query.Where(t => t.SourceType == filter.SourceType);
        if (!string.IsNullOrEmpty(filter.PostingStatus))
            query = query.Where(t => t.PostingStatus == filter.PostingStatus);

        var rows = await query
            .OrderByDescending(t => t.PostedAt)
            .ToListAsync(ct);

        // Group by TransactionId and build events
        var groups = rows
            .GroupBy(t => t.TransactionId)
            .ToList();

        var events = new List<ActivityEventDto>();

        foreach (var group in groups)
        {
            var first = group.First();
            var source = await ResolveSourceAsync(first.SourceType, first.SourceId, ct);

            var postingStatus = DetermineGroupStatus(group.ToList());
            var voidedRow = group.FirstOrDefault(t => t.VoidedByTransactionId != null);
            var reversalRow = group.FirstOrDefault(t => t.ReversesTransactionId != null);

            var canVoid = postingStatus == "Posted";
            var voidBlock = canVoid ? await CheckVoidBlockAsync(first.SourceType, first.SourceId, ct) : null;

            events.Add(new ActivityEventDto(
                TransactionId: group.Key,
                SourceType: first.SourceType,
                SourceId: first.SourceId,
                SourceNumber: source.Number,
                SourceDescription: source.Description,
                SourcePolicyTransactionId: source.PolicyTransactionId,
                SourcePolicyTransactionNumber: source.PolicyTransactionNumber,
                SourcePolicyTransactionType: source.PolicyTransactionType,
                SourcePolicyVersionId: source.PolicyVersionId,
                SourcePolicyVersionNumber: source.PolicyVersionNumber,
                EffectiveDate: first.EffectiveDate,
                PostedAt: first.PostedAt,
                TotalDebits: group.Where(t => t.PostingStatus != "Reversal").Sum(t => t.Debit),
                TotalCredits: group.Where(t => t.PostingStatus != "Reversal").Sum(t => t.Credit),
                PostingStatus: postingStatus,
                VoidedByTransactionId: voidedRow?.VoidedByTransactionId,
                ReversesTransactionId: reversalRow?.ReversesTransactionId,
                VoidReason: voidedRow?.VoidReason ?? reversalRow?.VoidReason,
                VoidedAt: voidedRow?.VoidedAt ?? reversalRow?.VoidedAt,
                CanVoid: canVoid && voidBlock == null,
                VoidBlockReason: voidBlock,
                Lines: group
                    .OrderBy(t => t.Id)
                    .Select(t => new ActivityLedgerLineDto(
                        t.Id,
                        t.Account.InternalCode,
                        t.Account.ExternalLabel,
                        t.Debit,
                        t.Credit,
                        t.Memo,
                        t.PostingStatus))
                    .ToList()
            ));
        }

        return events.OrderByDescending(e => e.PostedAt).ToList();
    }

    public async Task<ActivityEventDto?> GetEventAsync(
        Guid transactionId, bool isAdmin, CancellationToken ct = default)
    {
        var rows = await Db.Set<LedgerTransaction>()
            .Include(t => t.Account)
            .Where(t => t.TransactionId == transactionId && t.TenantId == 1)
            .ToListAsync(ct);

        if (rows.Count == 0) return null;

        var allEvents = await GetActivityAsync(
            new ActivityFilterRequest(null, null, null, null), isAdmin, ct);

        return allEvents.FirstOrDefault(e => e.TransactionId == transactionId);
    }

    // ---- Helpers ----

    private static string DetermineGroupStatus(List<LedgerTransaction> rows)
    {
        if (rows.All(t => t.PostingStatus == "Voided")) return "Voided";
        if (rows.Any(t => t.PostingStatus == "Reversal")) return "Reversal";
        return "Posted";
    }

    private async Task<ActivitySourceContext> ResolveSourceAsync(
        string sourceType, long sourceId, CancellationToken ct)
    {
        var db = Db;
        return sourceType switch
        {
            "Invoice" => await ResolveInvoiceSourceAsync(sourceId, ct),

            "Receipt" => await db.Set<Receipt>()
                .Where(r => r.Id == sourceId)
                .Select(r => new { r.ReceiptNumber, Desc = r.PayerName })
                .FirstOrDefaultAsync(ct) is { } rct
                    ? new ActivitySourceContext(rct.ReceiptNumber, rct.Desc, null, null, null, null, null)
                    : new ActivitySourceContext($"RCT-{sourceId}", null, null, null, null, null, null),

            "CashApplication" => await db.Set<CashApplication>()
                .Include(a => a.Receipt)
                .Include(a => a.Invoice)
                .Where(a => a.ReceiptId == sourceId)
                .Select(a => new { Num = a.Receipt.ReceiptNumber + " → " + a.Invoice.InvoiceNumber, Desc = (string?)null })
                .FirstOrDefaultAsync(ct) is { } ca
                    ? new ActivitySourceContext(ca.Num, ca.Desc, null, null, null, null, null)
                    : new ActivitySourceContext($"APPLY-{sourceId}", null, null, null, null, null, null),

            "Disbursement" => await db.Set<Disbursement>()
                .Where(d => d.Id == sourceId)
                .Select(d => new { d.DisbursementNumber, Desc = d.PayeeName })
                .FirstOrDefaultAsync(ct) is { } disb
                    ? new ActivitySourceContext(disb.DisbursementNumber, disb.Desc, null, null, null, null, null)
                    : new ActivitySourceContext($"DISB-{sourceId}", null, null, null, null, null, null),

            "Distribution" => new ActivitySourceContext($"DIST-{sourceId}", null, null, null, null, null, null),

            _ => new ActivitySourceContext($"{sourceType}-{sourceId}", null, null, null, null, null, null)
        };
    }

    private async Task<ActivitySourceContext> ResolveInvoiceSourceAsync(long sourceId, CancellationToken ct)
    {
        var invoice = await Db.Set<Invoice>()
            .Include(i => i.PolicyVersion)
            .FirstOrDefaultAsync(i => i.Id == sourceId, ct);
        if (invoice == null)
            return new ActivitySourceContext($"INV-{sourceId}", null, null, null, null, null, null);

        var transaction = invoice.PolicyTransactionId.HasValue
            ? await Db.Set<PolicyTransaction>().FirstOrDefaultAsync(t => t.Id == invoice.PolicyTransactionId.Value, ct)
            : null;

        return new ActivitySourceContext(
            invoice.InvoiceNumber,
            null,
            invoice.PolicyTransactionId,
            transaction?.TransactionNumber,
            transaction?.TransactionType,
            invoice.PolicyVersionId,
            invoice.PolicyVersion?.VersionNumber);
    }

    private async Task<string?> CheckVoidBlockAsync(string sourceType, long sourceId, CancellationToken ct)
    {
        var db = Db;
        switch (sourceType)
        {
            case "Receipt":
                var receipt = await db.Set<Receipt>()
                    .Include(r => r.Applications)
                    .FirstOrDefaultAsync(r => r.Id == sourceId, ct);
                if (receipt == null) return null;
                if (receipt.Status == "Voided") return "Already voided";
                var activeApps = receipt.Applications.Count(a =>
                    db.Set<LedgerTransaction>()
                        .Any(t => t.TransactionId == a.LedgerTransactionId && t.PostingStatus == "Posted"));
                if (activeApps > 0)
                    return $"{activeApps} active cash application(s) must be voided first";
                break;

            case "Invoice":
                var invoice = await db.Set<Invoice>()
                    .FirstOrDefaultAsync(i => i.Id == sourceId, ct);
                if (invoice == null) return null;
                if (invoice.Status == "Voided") return "Already voided";
                if (invoice.ClearedAmount > 0)
                    return "Cash applications exist — void those first";
                break;

            case "Disbursement":
                var disb = await db.Set<Disbursement>()
                    .FirstOrDefaultAsync(d => d.Id == sourceId, ct);
                if (disb == null) return null;
                if (disb.Status == "Voided") return "Already voided";
                if (disb.Status == "Draft") return null; // Draft can always be voided
                break;
        }
        return null;
    }

    private sealed record ActivitySourceContext(
        string Number,
        string? Description,
        Guid? PolicyTransactionId,
        string? PolicyTransactionNumber,
        Domain.Enums.TransactionType? PolicyTransactionType,
        Guid? PolicyVersionId,
        int? PolicyVersionNumber);
}
