using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SIMS.Application.DTOs.Accounting;
using SIMS.Application.DTOs.Reports;
using SIMS.Application.DTOs.UWWriteup;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Entities.Accounting;
using SIMS.Domain.Enums;

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

    public async Task<PostBindFollowUpDto> GetPostBindFollowUpAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var openItems = await Db.Set<QuoteChecklistItem>()
            .Where(i => !i.IsDeleted
                        && i.Stage == UnderwritingControlStage.PostBind
                        && i.IsBlocker
                        && !i.IsCompleted)
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.Label)
            .ToListAsync(ct);

        if (openItems.Count == 0)
            return new PostBindFollowUpDto(Array.Empty<PostBindFollowUpRowDto>());

        var quoteIds = openItems.Select(i => i.QuoteId).Distinct().ToList();
        var policies = await Db.Set<Policy>()
            .Include(p => p.Submission)
                .ThenInclude(s => s.Insured)
            .Include(p => p.Carrier)
            .Include(p => p.Program)
            .Where(p => !p.IsDeleted
                        && p.Status == PolicyStatus.Active
                        && quoteIds.Contains(p.BoundQuoteId))
            .ToListAsync(ct);

        var ownerIds = policies
            .Select(p => p.Submission.AssistantUWId ?? p.Submission.UnderwriterId)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        var users = await Db.Set<User>()
            .Where(u => ownerIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, ct);

        var itemsByQuote = openItems
            .GroupBy(i => i.QuoteId)
            .ToDictionary(g => g.Key, g => g.Select(i => i.Label).ToList());

        var rows = policies
            .Select(p =>
            {
                var items = itemsByQuote[p.BoundQuoteId];
                var ownerId = p.Submission.AssistantUWId ?? p.Submission.UnderwriterId;
                users.TryGetValue(ownerId, out var owner);
                var dueDate = (p.IssuedDate ?? p.BoundDate).AddDays(7);
                var daysUntilDue = dueDate.DayNumber - today.DayNumber;
                return new PostBindFollowUpRowDto(
                    p.Id,
                    p.PolicyNumber,
                    p.BoundQuoteId,
                    p.Submission.Insured.DisplayName,
                    p.Carrier.Name,
                    p.LineOfBusiness,
                    p.ProgramId,
                    p.Program?.Name,
                    p.Program?.Code,
                    p.Submission.Insured.State,
                    p.BoundDate,
                    p.IssuedDate,
                    Math.Max(0, today.DayNumber - p.BoundDate.DayNumber),
                    p.IssuedDate.HasValue ? Math.Max(0, today.DayNumber - p.IssuedDate.Value.DayNumber) : null,
                    ownerId == Guid.Empty ? null : ownerId,
                    string.IsNullOrWhiteSpace(owner?.FullName) ? owner?.Email : owner.FullName,
                    dueDate,
                    daysUntilDue,
                    SlaStatusFor(daysUntilDue),
                    items.Count,
                    items);
            })
            .OrderByDescending(r => r.DaysSinceBind)
            .ThenBy(r => r.PolicyNumber)
            .ToList();

        return new PostBindFollowUpDto(rows);
    }

    public async Task<ManagerQueueDto> GetManagerQueueAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var rows = new List<ManagerQueueRowDto>();

        var referrals = await Db.Set<UnderwritingReferral>()
            .Include(r => r.Submission).ThenInclude(s => s.Insured)
            .Include(r => r.Submission).ThenInclude(s => s.AssistantUW)
            .Include(r => r.Submission).ThenInclude(s => s.Underwriter)
            .Include(r => r.Quote)
            .Where(r => r.Status == UnderwritingReferralStatus.Open)
            .ToListAsync(ct);

        rows.AddRange(referrals.Select(r =>
        {
            var owner = r.Submission.AssistantUW ?? r.Submission.Underwriter;
            var dueDate = DateOnly.FromDateTime(r.RequestedAt).AddDays(r.Required ? 2 : 5);
            var daysUntilDue = dueDate.DayNumber - today.DayNumber;
            return new ManagerQueueRowDto(
                r.Id,
                "Referral",
                r.ReferralType,
                r.Reason,
                r.Required ? "Required" : "Standard",
                r.Quote?.QuoteNumber ?? r.Submission.SubmissionNumber,
                r.Submission.Insured.DisplayName,
                r.SubmissionId,
                r.QuoteId,
                null,
                owner.Id,
                DisplayName(owner),
                r.RequestedAt,
                dueDate,
                Math.Max(0, today.DayNumber - DateOnly.FromDateTime(r.RequestedAt).DayNumber),
                SlaStatusFor(daysUntilDue),
                $"/submissions/{r.SubmissionId}");
        }));

        var approvals = await Db.Set<AuthorityApprovalRequest>()
            .Include(a => a.AssignedToUser)
            .Where(a => a.Status == AuthorityApprovalStatus.Pending)
            .ToListAsync(ct);

        var quoteTargets = approvals
            .Where(a => a.TargetType == AuthorityApprovalTargetType.Quote)
            .Select(a => a.TargetId)
            .Distinct()
            .ToList();
        var quotes = await Db.Set<Quote>()
            .Include(q => q.Submission).ThenInclude(s => s.Insured)
            .Include(q => q.Program)
            .Where(q => quoteTargets.Contains(q.Id))
            .ToDictionaryAsync(q => q.Id, ct);

        var policyTargets = approvals
            .Where(a => a.TargetType == AuthorityApprovalTargetType.Policy)
            .Select(a => a.TargetId)
            .Distinct()
            .ToList();
        var policies = await Db.Set<Policy>()
            .Include(p => p.Submission).ThenInclude(s => s.Insured)
            .Include(p => p.Program)
            .Where(p => policyTargets.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        rows.AddRange(approvals.Select(a =>
        {
            quotes.TryGetValue(a.TargetId, out var quote);
            policies.TryGetValue(a.TargetId, out var policy);
            var dueDate = a.DueAt.HasValue ? DateOnly.FromDateTime(a.DueAt.Value) : DateOnly.FromDateTime(a.RequestedAt).AddDays(2);
            var daysUntilDue = dueDate.DayNumber - today.DayNumber;
            return new ManagerQueueRowDto(
                a.Id,
                "AuthorityApproval",
                a.ActionLabel,
                a.Reason,
                "Authority",
                quote?.QuoteNumber ?? policy?.PolicyNumber ?? a.ApprovalType,
                quote?.Submission.Insured.DisplayName ?? policy?.Submission.Insured.DisplayName,
                quote?.SubmissionId ?? policy?.SubmissionId,
                quote?.Id,
                policy?.Id,
                a.AssignedToUserId,
                DisplayName(a.AssignedToUser),
                a.RequestedAt,
                dueDate,
                Math.Max(0, today.DayNumber - DateOnly.FromDateTime(a.RequestedAt).DayNumber),
                SlaStatusFor(daysUntilDue),
                ActionUrlFor(a, quote, policy));
        }));

        var postBind = await GetPostBindFollowUpAsync(ct);
        rows.AddRange(postBind.Rows.Select(r =>
        {
            var createdAt = r.IssuedDate?.ToDateTime(TimeOnly.MinValue) ?? r.BoundDate.ToDateTime(TimeOnly.MinValue);
            return new ManagerQueueRowDto(
                r.PolicyId,
                "PostBind",
                "Post-bind follow-up",
                string.Join("; ", r.OpenRequiredItems),
                "Required",
                r.PolicyNumber,
                r.InsuredName,
                null,
                r.BoundQuoteId,
                r.PolicyId,
                r.OwnerId,
                r.OwnerName,
                createdAt,
                r.DueDate,
                r.DaysSinceBind,
                r.SlaStatus,
                $"/policies/{r.PolicyId}");
        }));

        return new ManagerQueueDto(
            referrals.Count,
            approvals.Count,
            postBind.Rows.Count,
            rows
                .OrderBy(r => r.DueDate ?? DateOnly.MaxValue)
                .ThenByDescending(r => r.Priority == "Required" || r.Priority == "Authority")
                .ThenBy(r => r.WorkType)
                .ToList());
    }

    public async Task<UnassignedProgramCleanupDto> GetUnassignedProgramCleanupAsync(CancellationToken ct = default)
    {
        var openQuoteStatuses = new[] { QuoteStatus.Draft, QuoteStatus.Submitted, QuoteStatus.Quoted };

        var quotes = await Db.Set<Quote>()
            .Include(q => q.Submission).ThenInclude(s => s.Insured)
            .Include(q => q.Carrier)
            .Where(q => !q.IsDeleted
                        && q.ProgramId == null
                        && openQuoteStatuses.Contains(q.Status))
            .ToListAsync(ct);

        var policies = await Db.Set<Policy>()
            .Include(p => p.Submission).ThenInclude(s => s.Insured)
            .Include(p => p.Carrier)
            .Where(p => !p.IsDeleted
                        && p.ProgramId == null
                        && p.Status == PolicyStatus.Active)
            .ToListAsync(ct);

        var rows = quotes
            .Select(q => new UnassignedProgramCleanupRowDto(
                q.Id,
                "Quote",
                q.QuoteNumber,
                q.Submission.Insured.DisplayName,
                q.Carrier.Name,
                q.LineOfBusiness,
                q.Submission.Insured.State,
                q.Status.ToString(),
                q.EffectiveDate,
                q.ExpirationDate,
                q.SubmissionId,
                q.Id,
                null,
                $"/quotes/{q.Id}"))
            .Concat(policies.Select(p => new UnassignedProgramCleanupRowDto(
                p.Id,
                "Policy",
                p.PolicyNumber,
                p.Submission.Insured.DisplayName,
                p.Carrier.Name,
                p.LineOfBusiness,
                p.Submission.Insured.State,
                p.Status.ToString(),
                p.EffectiveDate,
                p.ExpirationDate,
                p.SubmissionId,
                p.BoundQuoteId,
                p.Id,
                $"/policies/{p.Id}")))
            .OrderBy(r => r.EffectiveDate)
            .ThenBy(r => r.RecordType)
            .ThenBy(r => r.ReferenceNumber)
            .ToList();

        return new UnassignedProgramCleanupDto(quotes.Count, policies.Count, rows);
    }

    public async Task<AuthorityApprovalActivityDto> GetAuthorityApprovalActivityAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var approvals = await Db.Set<AuthorityApprovalRequest>()
            .Include(a => a.RequestedBy)
            .Include(a => a.AssignedToUser)
            .Include(a => a.DecisionBy)
            .OrderByDescending(a => a.RequestedAt)
            .ToListAsync(ct);

        var quoteIds = approvals
            .Where(a => a.TargetType == AuthorityApprovalTargetType.Quote)
            .Select(a => a.TargetId)
            .Distinct()
            .ToList();
        var quotes = await Db.Set<Quote>()
            .Include(q => q.Submission).ThenInclude(s => s.Insured)
            .Where(q => quoteIds.Contains(q.Id))
            .ToDictionaryAsync(q => q.Id, ct);

        var policyIds = approvals
            .Where(a => a.TargetType == AuthorityApprovalTargetType.Policy)
            .Select(a => a.TargetId)
            .Distinct()
            .ToList();
        var policies = await Db.Set<Policy>()
            .Include(p => p.Submission).ThenInclude(s => s.Insured)
            .Where(p => policyIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var submissionIds = approvals
            .Where(a => a.TargetType == AuthorityApprovalTargetType.Submission)
            .Select(a => a.TargetId)
            .Distinct()
            .ToList();
        var submissions = await Db.Set<Submission>()
            .Include(s => s.Insured)
            .Where(s => submissionIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, ct);

        var transactionIds = approvals
            .Where(a => a.TargetType == AuthorityApprovalTargetType.PolicyTransaction)
            .Select(a => a.TargetId)
            .Distinct()
            .ToList();
        var transactions = await Db.Set<PolicyTransaction>()
            .Include(t => t.Policy).ThenInclude(p => p.Submission).ThenInclude(s => s.Insured)
            .Include(t => t.Policy).ThenInclude(p => p.Program)
            .Where(t => transactionIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, ct);

        var rows = approvals.Select(a =>
        {
            var target = TargetContextFor(a, quotes, policies, submissions, transactions);
            var decisionHours = a.DecisionAt.HasValue
                ? Math.Round((decimal)(a.DecisionAt.Value - a.RequestedAt).TotalHours, 2)
                : (decimal?)null;
            var hoursUntilDue = a.DueAt.HasValue
                ? (int)Math.Round((a.DueAt.Value - now).TotalHours, MidpointRounding.AwayFromZero)
                : (int?)null;

            return new AuthorityApprovalActivityRowDto(
                a.Id,
                a.TargetType,
                a.TargetId,
                a.ActionCode,
                a.ActionLabel,
                a.ApprovalType,
                IsOverrideApproval(a),
                a.Reason,
                a.Status.ToString(),
                target.ReferenceNumber,
                target.InsuredName,
                target.ProgramId,
                target.ProgramName,
                target.ProgramCode,
                target.LineOfBusiness,
                target.State,
                a.RequestedById,
                DisplayName(a.RequestedBy),
                a.AssignedToUserId,
                DisplayName(a.AssignedToUser),
                a.DecisionById,
                DisplayName(a.DecisionBy),
                a.RequestedAt,
                a.DueAt,
                a.DecisionAt,
                decisionHours,
                hoursUntilDue,
                ApprovalSlaStatusFor(a, now),
                target.ActionUrl);
        }).ToList();

        var closedDecisionHours = rows
            .Where(r => r.DecisionHours.HasValue)
            .Select(r => r.DecisionHours!.Value)
            .ToList();
        var averageDecisionHours = closedDecisionHours.Count == 0
            ? (decimal?)null
            : Math.Round(closedDecisionHours.Average(), 2);

        return new AuthorityApprovalActivityDto(
            rows.Count(r => r.Status == AuthorityApprovalStatus.Pending.ToString()),
            rows.Count(r => r.Status == AuthorityApprovalStatus.Approved.ToString()),
            rows.Count(r => r.Status == AuthorityApprovalStatus.Declined.ToString()),
            rows.Count(r => r.Status == AuthorityApprovalStatus.Cancelled.ToString()),
            rows.Count(r => r.IsOverride),
            rows.Count(r => r.Status == AuthorityApprovalStatus.Pending.ToString() && r.DueAt.HasValue && r.DueAt.Value < now),
            averageDecisionHours,
            rows);
    }

    public async Task<DeclineReasonReportDto> GetDeclineReasonReportAsync(CancellationToken ct = default)
    {
        var quotes = await Db.Set<Quote>()
            .Include(q => q.Submission).ThenInclude(s => s.Insured)
            .Include(q => q.Carrier)
            .Include(q => q.Program)
            .Include(q => q.UWWriteup)
            .Where(q => !q.IsDeleted
                        && (q.Status == QuoteStatus.Declined
                            || (q.UWWriteup != null && q.UWWriteup.Decision == UWWriteupDecision.Decline)))
            .ToListAsync(ct);

        var rows = quotes.Select(q =>
        {
            var payload = ParseWriteupPayload(q.UWWriteup?.PayloadJson);
            var reason = FirstNonBlank(payload?.DecisionRationale, payload?.ReasonSubmitted) ?? "Unspecified";
            return new DeclineReasonRowDto(
                q.Id,
                q.QuoteNumber,
                q.SubmissionId,
                q.Submission.SubmissionNumber,
                q.Submission.Insured.DisplayName,
                q.Carrier.Name,
                q.LineOfBusiness,
                q.ProgramId,
                q.Program?.Name,
                q.Program?.Code,
                q.Submission.Insured.State,
                reason,
                q.UWWriteup?.SubmittedAt ?? q.UpdatedAt,
                $"/quotes/{q.Id}");
        })
        .OrderByDescending(r => r.DeclinedAt)
        .ThenBy(r => r.QuoteNumber)
        .ToList();

        var total = rows.Count;
        var reasons = rows
            .GroupBy(r => r.Reason)
            .Select(g => new DeclineReasonSummaryDto(
                g.Key,
                g.Count(),
                total == 0 ? 0 : (decimal)g.Count() / total))
            .OrderByDescending(r => r.Count)
            .ThenBy(r => r.Reason)
            .ToList();

        return new DeclineReasonReportDto(
            total,
            rows.Count(r => r.Reason != "Unspecified"),
            rows.Count(r => r.Reason == "Unspecified"),
            reasons,
            rows);
    }

    public async Task<ClearanceOverrideReportDto> GetClearanceOverrideReportAsync(CancellationToken ct = default)
    {
        var overrides = await Db.Set<UnderwritingClearanceResult>()
            .Include(r => r.Submission).ThenInclude(s => s.Insured)
            .Where(r => r.IsOverridden)
            .OrderByDescending(r => r.OverriddenAt ?? r.UpdatedAt)
            .ToListAsync(ct);

        var submissionIds = overrides.Select(r => r.SubmissionId).Distinct().ToList();
        var quotes = await Db.Set<Quote>()
            .Include(q => q.Program)
            .Where(q => submissionIds.Contains(q.SubmissionId))
            .ToListAsync(ct);
        var quoteContexts = quotes
            .GroupBy(q => q.SubmissionId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(q => q.CreatedAt).First());

        var userIds = overrides
            .Select(r => r.OverriddenById)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var users = await Db.Set<User>()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, ct);

        var rows = overrides.Select(r =>
        {
            quoteContexts.TryGetValue(r.SubmissionId, out var quote);
            var lob = quote?.LineOfBusiness ?? FirstLineOfBusiness(r.Submission.LinesOfBusiness);
            var overriddenBy = r.OverriddenById.HasValue && users.TryGetValue(r.OverriddenById.Value, out var user)
                ? user
                : null;

            return new ClearanceOverrideRowDto(
                r.Id,
                r.SubmissionId,
                r.Submission.SubmissionNumber,
                r.Submission.Insured.DisplayName,
                quote?.ProgramId,
                quote?.Program?.Name,
                quote?.Program?.Code,
                r.Submission.Insured.State,
                lob,
                r.CheckType,
                r.Status,
                r.MatchedRecordId,
                r.MatchedRecordLabel,
                r.Explanation,
                r.OverriddenById,
                DisplayName(overriddenBy),
                r.OverriddenAt,
                r.OverrideReason ?? string.Empty,
                r.ReviewedAt,
                $"/submissions/{r.SubmissionId}");
        }).ToList();

        var summaries = rows
            .GroupBy(r => r.CheckType)
            .Select(g => new ClearanceOverrideSummaryDto(g.Key, g.Count()))
            .OrderByDescending(r => r.Count)
            .ThenBy(r => r.CheckType)
            .ToList();

        return new ClearanceOverrideReportDto(
            rows.Count,
            rows.Count(r => r.Status == UnderwritingClearanceStatus.Blocked),
            rows.Count(r => r.Status == UnderwritingClearanceStatus.Warning),
            summaries,
            rows);
    }

    private static string SlaStatusFor(int daysUntilDue)
    {
        if (daysUntilDue < 0) return "Overdue";
        if (daysUntilDue == 0) return "DueToday";
        if (daysUntilDue <= 3) return "DueSoon";
        return "OnTrack";
    }

    private static string ApprovalSlaStatusFor(AuthorityApprovalRequest approval, DateTime now)
    {
        if (approval.Status != AuthorityApprovalStatus.Pending) return "Closed";
        if (!approval.DueAt.HasValue) return "Open";
        var hoursUntilDue = (approval.DueAt.Value - now).TotalHours;
        if (hoursUntilDue < 0) return "Overdue";
        if (hoursUntilDue <= 24) return "DueSoon";
        return "OnTrack";
    }

    private static bool IsOverrideApproval(AuthorityApprovalRequest approval) =>
        approval.ActionCode.Contains("override", StringComparison.OrdinalIgnoreCase)
        || approval.ActionLabel.Contains("override", StringComparison.OrdinalIgnoreCase)
        || approval.ApprovalType.Contains("override", StringComparison.OrdinalIgnoreCase);

    private static string? DisplayName(User? user)
    {
        if (user == null) return null;
        return string.IsNullOrWhiteSpace(user.FullName) ? user.Email : user.FullName;
    }

    private static string ActionUrlFor(AuthorityApprovalRequest approval, Quote? quote, Policy? policy)
    {
        if (quote != null) return $"/quotes/{quote.Id}";
        if (policy != null) return $"/policies/{policy.Id}";
        if (approval.TargetType == AuthorityApprovalTargetType.RatingPlanVersion) return $"/admin/rating/versions/{approval.TargetId}";
        return "/reports?r=manager-queue";
    }

    private static ApprovalTargetContext TargetContextFor(
        AuthorityApprovalRequest approval,
        IReadOnlyDictionary<Guid, Quote> quotes,
        IReadOnlyDictionary<Guid, Policy> policies,
        IReadOnlyDictionary<Guid, Submission> submissions,
        IReadOnlyDictionary<Guid, PolicyTransaction> transactions)
    {
        if (approval.TargetType == AuthorityApprovalTargetType.Quote && quotes.TryGetValue(approval.TargetId, out var quote))
            return new ApprovalTargetContext(
                quote.QuoteNumber,
                quote.Submission.Insured.DisplayName,
                quote.ProgramId,
                quote.Program?.Name,
                quote.Program?.Code,
                quote.LineOfBusiness,
                quote.Submission.Insured.State,
                $"/quotes/{quote.Id}");

        if (approval.TargetType == AuthorityApprovalTargetType.Policy && policies.TryGetValue(approval.TargetId, out var policy))
            return new ApprovalTargetContext(
                policy.PolicyNumber,
                policy.Submission.Insured.DisplayName,
                policy.ProgramId,
                policy.Program?.Name,
                policy.Program?.Code,
                policy.LineOfBusiness,
                policy.Submission.Insured.State,
                $"/policies/{policy.Id}");

        if (approval.TargetType == AuthorityApprovalTargetType.Submission && submissions.TryGetValue(approval.TargetId, out var submission))
            return new ApprovalTargetContext(
                submission.SubmissionNumber,
                submission.Insured.DisplayName,
                null,
                null,
                null,
                FirstLineOfBusiness(submission.LinesOfBusiness),
                submission.Insured.State,
                $"/submissions/{submission.Id}");

        if (approval.TargetType == AuthorityApprovalTargetType.PolicyTransaction && transactions.TryGetValue(approval.TargetId, out var transaction))
            return new ApprovalTargetContext(
                transaction.TransactionNumber,
                transaction.Policy.Submission.Insured.DisplayName,
                transaction.Policy.ProgramId,
                transaction.Policy.Program?.Name,
                transaction.Policy.Program?.Code,
                transaction.Policy.LineOfBusiness,
                transaction.Policy.Submission.Insured.State,
                $"/policies/{transaction.PolicyId}/transactions/{transaction.Id}");

        if (approval.TargetType == AuthorityApprovalTargetType.RatingPlanVersion)
            return new ApprovalTargetContext(approval.ApprovalType, null, null, null, null, null, null, $"/admin/rating/versions/{approval.TargetId}");

        return new ApprovalTargetContext(approval.ApprovalType, null, null, null, null, null, null, "/reports?r=authority-approvals");
    }

    private record ApprovalTargetContext(
        string ReferenceNumber,
        string? InsuredName,
        Guid? ProgramId,
        string? ProgramName,
        string? ProgramCode,
        PolicyLineOfBusiness? LineOfBusiness,
        string? State,
        string ActionUrl);

    private static IMWriteupPayload? ParseWriteupPayload(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson)) return null;

        try
        {
            return JsonSerializer.Deserialize<IMWriteupPayload>(payloadJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? FirstNonBlank(params string?[] values) =>
        values.Select(v => v?.Trim()).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static PolicyLineOfBusiness? FirstLineOfBusiness(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        try
        {
            var names = JsonSerializer.Deserialize<string[]>(value) ?? [];
            foreach (var name in names)
            {
                if (Enum.TryParse<PolicyLineOfBusiness>(name, out var lob))
                    return lob;
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    public async Task<RenewalsUpcomingDto> GetRenewalsUpcomingAsync(int daysAhead = 90, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var horizon = today.AddDays(daysAhead < 1 ? 90 : daysAhead);

        var policies = await Db.Set<Policy>()
            .Include(p => p.Submission).ThenInclude(s => s.Insured)
            .Include(p => p.Submission).ThenInclude(s => s.Agent)
            .Include(p => p.Carrier)
            .Include(p => p.Program)
            .Where(p => !p.IsDeleted
                        && p.Status == PolicyStatus.Active
                        && p.ExpirationDate >= today
                        && p.ExpirationDate <= horizon)
            .OrderBy(p => p.ExpirationDate)
            .ToListAsync(ct);

        var policyIds = policies.Select(p => p.Id).ToHashSet();
        var renewedPolicyIds = (await Db.Set<Submission>()
            .Where(s => !s.IsDeleted && s.RenewingPolicyId.HasValue
                        && policyIds.Contains(s.RenewingPolicyId!.Value))
            .Select(s => s.RenewingPolicyId!.Value)
            .Distinct()
            .ToListAsync(ct))
            .ToHashSet();

        var rows = policies.Select(p => new RenewalsUpcomingRowDto(
            p.Id,
            p.PolicyNumber,
            p.Submission.Insured.DisplayName,
            p.Submission.Agent?.Name,
            p.ProgramId,
            p.Program?.Code,
            p.Program?.Name,
            p.CarrierId,
            p.Carrier.Name,
            p.LineOfBusiness,
            p.EffectiveDate,
            p.ExpirationDate,
            p.ExpirationDate.DayNumber - today.DayNumber,
            p.PremiumAmount,
            renewedPolicyIds.Contains(p.Id)
        )).ToList();

        return new RenewalsUpcomingDto(daysAhead, rows.Count, rows);
    }

    public async Task<BoundByPeriodDto> GetBoundByPeriodAsync(DateOnly? dateFrom = null, DateOnly? dateTo = null, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = dateFrom ?? new DateOnly(today.Year, 1, 1);
        var to = dateTo ?? today;

        var policies = await Db.Set<Policy>()
            .Include(p => p.Carrier)
            .Include(p => p.Program)
            .Where(p => !p.IsDeleted && p.BoundDate >= from && p.BoundDate <= to)
            .OrderBy(p => p.BoundDate)
            .ToListAsync(ct);

        var periods = policies
            .GroupBy(p => new { p.BoundDate.Year, p.BoundDate.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new BoundByPeriodPeriodRowDto(
                g.Key.Year, g.Key.Month,
                g.Count(),
                g.Sum(p => p.PremiumAmount),
                g.Sum(p => p.TotalPremium)))
            .ToList();

        var breakdown = policies
            .GroupBy(p => new
            {
                p.ProgramId,
                ProgramCode = p.Program?.Code,
                ProgramName = p.Program?.Name ?? "Unassigned",
                p.CarrierId,
                CarrierName = p.Carrier?.Name ?? "Unknown",
                p.LineOfBusiness
            })
            .OrderByDescending(g => g.Sum(p => p.PremiumAmount))
            .Select(g => new BoundByPeriodBreakdownRowDto(
                g.Key.ProgramId,
                g.Key.ProgramCode,
                g.Key.ProgramName,
                g.Key.CarrierId,
                g.Key.CarrierName,
                g.Key.LineOfBusiness,
                g.Count(),
                g.Sum(p => p.PremiumAmount),
                g.Sum(p => p.TotalPremium)))
            .ToList();

        return new BoundByPeriodDto(
            from, to,
            policies.Count,
            policies.Sum(p => p.PremiumAmount),
            periods, breakdown);
    }

    public async Task<HitRatioByCarrierDto> GetHitRatioByCarrierAsync(DateOnly? dateFrom = null, DateOnly? dateTo = null, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = dateFrom ?? new DateOnly(today.Year, 1, 1);
        var to = dateTo ?? today;

        var quotes = await Db.Set<Quote>()
            .Include(q => q.Carrier)
            .Where(q => !q.IsDeleted
                        && q.EffectiveDate >= from
                        && q.EffectiveDate <= to)
            .ToListAsync(ct);

        var rows = quotes
            .GroupBy(q => new { q.CarrierId, CarrierName = q.Carrier?.Name ?? "Unknown" })
            .OrderByDescending(g => g.Count())
            .Select(g =>
            {
                var bound = g.Count(q => q.Status == QuoteStatus.Bound);
                var declined = g.Count(q => q.Status == QuoteStatus.Declined);
                var expired = g.Count(q => q.Status == QuoteStatus.Expired);
                var open = g.Count(q => q.Status is QuoteStatus.Draft or QuoteStatus.Submitted or QuoteStatus.Quoted);
                var closed = bound + declined + expired;
                return new HitRatioByCarrierRowDto(
                    g.Key.CarrierId,
                    g.Key.CarrierName,
                    g.Count(),
                    bound, declined, expired, open,
                    closed > 0 ? Math.Round((decimal)bound / closed * 100, 1) : 0m);
            })
            .ToList();

        var totalBound = rows.Sum(r => r.BoundCount);
        var totalClosed = rows.Sum(r => r.BoundCount + r.DeclinedCount + r.ExpiredCount);

        return new HitRatioByCarrierDto(
            from, to,
            quotes.Count, totalBound,
            totalClosed > 0 ? Math.Round((decimal)totalBound / totalClosed * 100, 1) : 0m,
            rows);
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
