using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMS.Application.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Entities.Accounting;
using SIMS.Domain.Enums;
using SIMS.Infrastructure.Data;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class ReportServiceTests
{
    [Fact]
    public async Task GetInvoiceTotalsByProgramAsync_GroupsPostedInvoiceTotalsByPolicyProgram()
    {
        await using var db = CreateDb();
        var longleaf = new ProgramConfiguration { Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        var shuttleBee = new ProgramConfiguration { Name = "ShuttleBee", Code = "SHUTTLEBEE", IsActive = true };
        db.AddRange(longleaf, shuttleBee);

        var longleafPolicy = PolicyFor(longleaf.Id);
        var shuttleBeePolicy = PolicyFor(shuttleBee.Id);
        var unassignedPolicy = PolicyFor(null);
        var longleafTxn = TransactionFor(longleafPolicy);
        var shuttleBeeTxn = TransactionFor(shuttleBeePolicy);
        var unassignedTxn = TransactionFor(unassignedPolicy);

        db.AddRange(
            longleafPolicy,
            shuttleBeePolicy,
            unassignedPolicy,
            longleafTxn,
            shuttleBeeTxn,
            unassignedTxn,
            InvoiceFor(longleafTxn.Id, "INV-LL-1", 1000m, 100m, 1100m, 150m, 50m),
            InvoiceFor(longleafTxn.Id, "INV-LL-2", 250m, 25m, 275m, 30m, 10m),
            InvoiceFor(shuttleBeeTxn.Id, "INV-SB-1", 500m, 50m, 550m, 75m, 25m),
            InvoiceFor(unassignedTxn.Id, "INV-UN-1", 300m, 30m, 330m, 45m, 15m),
            InvoiceFor(longleafTxn.Id, "INV-VOID", 999m, 99m, 1098m, 99m, 99m, status: "Voided"));
        await db.SaveChangesAsync();

        var reports = new ReportService(new ServiceCollection().AddSingleton<DbContext>(db).BuildServiceProvider());

        var result = await reports.GetInvoiceTotalsByProgramAsync();

        Assert.Equal(3, result.Rows.Count);
        var longleafRow = Assert.Single(result.Rows, r => r.ProgramId == longleaf.Id);
        Assert.Equal("Longleaf", longleafRow.ProgramName);
        Assert.Equal("LONGLEAF", longleafRow.ProgramCode);
        Assert.Equal(2, longleafRow.InvoiceCount);
        Assert.Equal(1250m, longleafRow.GrossPremium);
        Assert.Equal(125m, longleafRow.TotalFees);
        Assert.Equal(1375m, longleafRow.TotalAmount);
        Assert.Equal(180m, longleafRow.CommissionAmount);
        Assert.Equal(60m, longleafRow.AgentCommissionAmount);
        Assert.Equal(120m, longleafRow.NetRetained);

        var shuttleBeeRow = Assert.Single(result.Rows, r => r.ProgramId == shuttleBee.Id);
        Assert.Equal(550m, shuttleBeeRow.TotalAmount);

        var unassignedRow = Assert.Single(result.Rows, r => r.ProgramId == null);
        Assert.Equal("Unassigned", unassignedRow.ProgramName);
        Assert.Equal(330m, unassignedRow.TotalAmount);
    }

    [Fact]
    public async Task GetInvoiceTotalsByProgramAsync_FiltersToSelectedProgram()
    {
        await using var db = CreateDb();
        var longleaf = new ProgramConfiguration { Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        var shuttleBee = new ProgramConfiguration { Name = "ShuttleBee", Code = "SHUTTLEBEE", IsActive = true };
        db.AddRange(longleaf, shuttleBee);

        var longleafPolicy = PolicyFor(longleaf.Id);
        var shuttleBeePolicy = PolicyFor(shuttleBee.Id);
        var longleafTxn = TransactionFor(longleafPolicy);
        var shuttleBeeTxn = TransactionFor(shuttleBeePolicy);

        db.AddRange(
            longleafPolicy,
            shuttleBeePolicy,
            longleafTxn,
            shuttleBeeTxn,
            InvoiceFor(longleafTxn.Id, "INV-LL-1", 1000m, 100m, 1100m, 150m, 50m),
            InvoiceFor(shuttleBeeTxn.Id, "INV-SB-1", 500m, 50m, 550m, 75m, 25m));
        await db.SaveChangesAsync();

        var reports = new ReportService(new ServiceCollection().AddSingleton<DbContext>(db).BuildServiceProvider());

        var result = await reports.GetInvoiceTotalsByProgramAsync(longleaf.Id);

        var row = Assert.Single(result.Rows);
        Assert.Equal(longleaf.Id, row.ProgramId);
        Assert.Equal("Longleaf", row.ProgramName);
        Assert.Equal(1100m, row.TotalAmount);
    }

    [Fact]
    public async Task GetPostBindFollowUpAsync_ReturnsActivePoliciesWithIncompleteRequiredPostBindItems()
    {
        await using var db = CreateDb();
        var quoteId = Guid.NewGuid();
        var ignoredQuoteId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var program = new ProgramConfiguration { Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        var carrier = new Carrier { Name = "Great American" };
        var insured = new Insured
        {
            InsuredType = InsuredType.Commercial,
            CompanyName = "Acme Transit",
            State = "TX"
        };
        var submission = new Submission
        {
            SubmissionNumber = "SUB-1",
            Insured = insured,
            InsuredId = insured.Id,
            UnderwriterId = Guid.NewGuid(),
            CreatedById = Guid.NewGuid()
        };
        var policy = PolicyFor(program.Id);
        policy.PolicyNumber = "POL-1";
        policy.BoundQuoteId = quoteId;
        policy.BoundDate = today.AddDays(-8);
        policy.IssuedDate = today.AddDays(-3);
        policy.Carrier = carrier;
        policy.CarrierId = carrier.Id;
        policy.Program = program;
        policy.Submission = submission;
        policy.SubmissionId = submission.Id;

        var cancelledPolicy = PolicyFor(program.Id);
        cancelledPolicy.BoundQuoteId = ignoredQuoteId;
        cancelledPolicy.Status = PolicyStatus.Cancelled;

        db.AddRange(
            program,
            carrier,
            insured,
            submission,
            policy,
            cancelledPolicy,
            new QuoteChecklistItem
            {
                QuoteId = quoteId,
                Stage = UnderwritingControlStage.PostBind,
                Label = "Signed subjectivities returned",
                IsBlocker = true,
                IsCompleted = false,
                SortOrder = 1
            },
            new QuoteChecklistItem
            {
                QuoteId = quoteId,
                Stage = UnderwritingControlStage.PostBind,
                Label = "Signed policy forms received",
                IsBlocker = true,
                IsCompleted = false,
                SortOrder = 2
            },
            new QuoteChecklistItem
            {
                QuoteId = quoteId,
                Stage = UnderwritingControlStage.PostBind,
                Label = "Completed item",
                IsBlocker = true,
                IsCompleted = true
            },
            new QuoteChecklistItem
            {
                QuoteId = quoteId,
                Stage = UnderwritingControlStage.Bind,
                Label = "Wrong stage",
                IsBlocker = true,
                IsCompleted = false
            },
            new QuoteChecklistItem
            {
                QuoteId = ignoredQuoteId,
                Stage = UnderwritingControlStage.PostBind,
                Label = "Cancelled policy item",
                IsBlocker = true,
                IsCompleted = false
            });
        await db.SaveChangesAsync();

        var reports = new ReportService(new ServiceCollection().AddSingleton<DbContext>(db).BuildServiceProvider());

        var result = await reports.GetPostBindFollowUpAsync();

        var row = Assert.Single(result.Rows);
        Assert.Equal(policy.Id, row.PolicyId);
        Assert.Equal("POL-1", row.PolicyNumber);
        Assert.Equal("Acme Transit", row.InsuredName);
        Assert.Equal("Great American", row.CarrierName);
        Assert.Equal(PolicyLineOfBusiness.InlandMarine, row.LineOfBusiness);
        Assert.Equal(program.Id, row.ProgramId);
        Assert.Equal("Longleaf", row.ProgramName);
        Assert.Equal("LONGLEAF", row.ProgramCode);
        Assert.Equal("TX", row.State);
        Assert.Equal(8, row.DaysSinceBind);
        Assert.Equal(3, row.DaysSinceIssue);
        Assert.Equal(2, row.OpenRequiredItemCount);
        Assert.Equal(new[] { "Signed subjectivities returned", "Signed policy forms received" }, row.OpenRequiredItems);
    }

    [Fact]
    public async Task GetPostBindFollowUpAsync_AddsOwnerDueDateAndSlaStatusForFiltering()
    {
        await using var db = CreateDb();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var assistant = new User
        {
            Id = Guid.NewGuid(),
            FirstName = "Casey",
            LastName = "Assistant",
            UserName = "casey@example.com",
            Email = "casey@example.com"
        };
        var underwriter = new User
        {
            Id = Guid.NewGuid(),
            FirstName = "Jordan",
            LastName = "Underwriter",
            UserName = "jordan@example.com",
            Email = "jordan@example.com"
        };
        var submission = new Submission
        {
            SubmissionNumber = "SUB-2",
            Insured = new Insured
            {
                InsuredType = InsuredType.Commercial,
                CompanyName = "Late Forms LLC",
                State = "FL"
            },
            UnderwriterId = underwriter.Id,
            Underwriter = underwriter,
            AssistantUWId = assistant.Id,
            AssistantUW = assistant,
            CreatedById = underwriter.Id,
            CreatedBy = underwriter
        };
        var policy = PolicyFor(null);
        policy.PolicyNumber = "POL-LATE";
        policy.BoundDate = today.AddDays(-10);
        policy.IssuedDate = today.AddDays(-8);
        policy.Submission = submission;
        policy.SubmissionId = submission.Id;
        policy.Carrier = new Carrier { Name = "Follow-Up Carrier" };

        db.AddRange(
            assistant,
            underwriter,
            submission,
            policy,
            new QuoteChecklistItem
            {
                QuoteId = policy.BoundQuoteId,
                Stage = UnderwritingControlStage.PostBind,
                Label = "Signed policy forms received",
                IsBlocker = true,
                IsCompleted = false
            });
        await db.SaveChangesAsync();

        var reports = new ReportService(new ServiceCollection().AddSingleton<DbContext>(db).BuildServiceProvider());

        var result = await reports.GetPostBindFollowUpAsync();

        var row = Assert.Single(result.Rows);
        Assert.Equal(assistant.Id, row.OwnerId);
        Assert.Equal("Casey Assistant", row.OwnerName);
        Assert.Equal(today.AddDays(-1), row.DueDate);
        Assert.Equal(-1, row.DaysUntilDue);
        Assert.Equal("Overdue", row.SlaStatus);
    }

    [Fact]
    public async Task GetManagerQueueAsync_ReturnsPendingReferralsAuthorityApprovalsAndPostBindFollowUp()
    {
        await using var db = CreateDb();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var manager = new User
        {
            Id = Guid.NewGuid(),
            FirstName = "Morgan",
            LastName = "Manager",
            UserName = "morgan@example.com",
            Email = "morgan@example.com"
        };
        var requester = new User
        {
            Id = Guid.NewGuid(),
            FirstName = "Casey",
            LastName = "Requester",
            UserName = "casey@example.com",
            Email = "casey@example.com"
        };
        var insured = new Insured
        {
            InsuredType = InsuredType.Commercial,
            CompanyName = "Queue Timber",
            State = "GA"
        };
        var submission = new Submission
        {
            SubmissionNumber = "SUB-Q",
            Insured = insured,
            InsuredId = insured.Id,
            UnderwriterId = manager.Id,
            Underwriter = manager,
            CreatedById = requester.Id,
            CreatedBy = requester
        };
        var quote = new Quote
        {
            Id = Guid.NewGuid(),
            QuoteNumber = "Q-1",
            Submission = submission,
            SubmissionId = submission.Id,
            CarrierId = Guid.NewGuid(),
            LineOfBusiness = PolicyLineOfBusiness.InlandMarine,
            EffectiveDate = today.AddDays(30),
            ExpirationDate = today.AddDays(395),
            CreatedById = requester.Id,
            CreatedBy = requester
        };
        var policy = PolicyFor(null);
        policy.PolicyNumber = "POL-Q";
        policy.BoundQuoteId = quote.Id;
        policy.BoundDate = today.AddDays(-9);
        policy.IssuedDate = today.AddDays(-8);
        policy.Submission = submission;
        policy.SubmissionId = submission.Id;
        policy.Carrier = new Carrier { Name = "Queue Carrier" };

        db.AddRange(
            manager,
            requester,
            insured,
            submission,
            quote,
            policy,
            new UnderwritingReferral
            {
                Submission = submission,
                SubmissionId = submission.Id,
                Quote = quote,
                QuoteId = quote.Id,
                ReferralType = "LargeLoss",
                Required = true,
                Reason = "Large prior loss requires review.",
                RequestedById = requester.Id,
                RequestedBy = requester,
                RequestedAt = DateTime.UtcNow.AddDays(-2)
            },
            new AuthorityApprovalRequest
            {
                TargetType = AuthorityApprovalTargetType.Quote,
                TargetId = quote.Id,
                ActionCode = "quote.commission-override",
                ActionLabel = "Commission override",
                RequiredPermission = "underwriting.authority.approve",
                ApprovalType = "CommissionOverride",
                Reason = "Commission override requires approval.",
                RequestedById = requester.Id,
                RequestedBy = requester,
                AssignedToUserId = manager.Id,
                AssignedToUser = manager,
                DueAt = DateTime.UtcNow.AddDays(1)
            },
            new QuoteChecklistItem
            {
                QuoteId = quote.Id,
                Stage = UnderwritingControlStage.PostBind,
                Label = "Signed subjectivities returned",
                IsBlocker = true,
                IsCompleted = false
            });
        await db.SaveChangesAsync();

        var reports = new ReportService(new ServiceCollection().AddSingleton<DbContext>(db).BuildServiceProvider());

        var result = await reports.GetManagerQueueAsync();

        Assert.Equal(3, result.Rows.Count);
        Assert.Equal(1, result.PendingReferralCount);
        Assert.Equal(1, result.PendingAuthorityApprovalCount);
        Assert.Equal(1, result.PostBindFollowUpCount);

        var referral = Assert.Single(result.Rows, r => r.WorkType == "Referral");
        Assert.Equal(submission.Id, referral.SubmissionId);
        Assert.Equal(quote.Id, referral.QuoteId);
        Assert.Equal("Queue Timber", referral.InsuredName);
        Assert.Equal("LargeLoss", referral.Title);
        Assert.Contains("Large prior loss", referral.Detail);
        Assert.Equal("Required", referral.Priority);
        Assert.Equal($"/submissions/{submission.Id}", referral.ActionUrl);

        var approval = Assert.Single(result.Rows, r => r.WorkType == "AuthorityApproval");
        Assert.Equal(quote.Id, approval.QuoteId);
        Assert.Equal("Commission override", approval.Title);
        Assert.Equal(manager.Id, approval.OwnerId);
        Assert.Equal("Morgan Manager", approval.OwnerName);
        Assert.Equal("DueSoon", approval.SlaStatus);

        var postBind = Assert.Single(result.Rows, r => r.WorkType == "PostBind");
        Assert.Equal(policy.Id, postBind.PolicyId);
        Assert.Equal("POL-Q", postBind.ReferenceNumber);
        Assert.Equal("Overdue", postBind.SlaStatus);
        Assert.Equal($"/policies/{policy.Id}", postBind.ActionUrl);
    }

    [Fact]
    public async Task GetUnassignedProgramCleanupAsync_ReturnsOpenQuotesAndActivePoliciesWithoutProgram()
    {
        await using var db = CreateDb();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var program = new ProgramConfiguration { Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        var carrier = new Carrier { Name = "Cleanup Carrier" };
        var insured = new Insured
        {
            InsuredType = InsuredType.Commercial,
            CompanyName = "Cleanup Timber",
            State = "MS"
        };
        var submission = new Submission
        {
            SubmissionNumber = "SUB-CLEAN",
            Insured = insured,
            InsuredId = insured.Id,
            UnderwriterId = Guid.NewGuid(),
            CreatedById = Guid.NewGuid()
        };
        var openQuote = new Quote
        {
            Id = Guid.NewGuid(),
            QuoteNumber = "Q-CLEAN",
            Submission = submission,
            SubmissionId = submission.Id,
            Carrier = carrier,
            CarrierId = carrier.Id,
            ProgramId = null,
            LineOfBusiness = PolicyLineOfBusiness.InlandMarine,
            Status = QuoteStatus.Quoted,
            EffectiveDate = today.AddDays(10),
            ExpirationDate = today.AddDays(375),
            CreatedById = Guid.NewGuid()
        };
        var assignedQuote = new Quote
        {
            Id = Guid.NewGuid(),
            QuoteNumber = "Q-ASSIGNED",
            Submission = submission,
            SubmissionId = submission.Id,
            Carrier = carrier,
            CarrierId = carrier.Id,
            Program = program,
            ProgramId = program.Id,
            LineOfBusiness = PolicyLineOfBusiness.InlandMarine,
            Status = QuoteStatus.Quoted,
            EffectiveDate = today.AddDays(10),
            ExpirationDate = today.AddDays(375),
            CreatedById = Guid.NewGuid()
        };
        var boundQuote = new Quote
        {
            Id = Guid.NewGuid(),
            QuoteNumber = "Q-BOUND",
            Submission = submission,
            SubmissionId = submission.Id,
            Carrier = carrier,
            CarrierId = carrier.Id,
            ProgramId = null,
            LineOfBusiness = PolicyLineOfBusiness.InlandMarine,
            Status = QuoteStatus.Bound,
            EffectiveDate = today.AddDays(10),
            ExpirationDate = today.AddDays(375),
            CreatedById = Guid.NewGuid()
        };
        var activePolicy = PolicyFor(null);
        activePolicy.PolicyNumber = "POL-CLEAN";
        activePolicy.Submission = submission;
        activePolicy.SubmissionId = submission.Id;
        activePolicy.Carrier = carrier;
        activePolicy.CarrierId = carrier.Id;
        activePolicy.LineOfBusiness = PolicyLineOfBusiness.InlandMarine;
        activePolicy.EffectiveDate = today.AddDays(-30);
        activePolicy.ExpirationDate = today.AddDays(335);
        var expiredPolicy = PolicyFor(null);
        expiredPolicy.PolicyNumber = "POL-EXPIRED";
        expiredPolicy.Submission = submission;
        expiredPolicy.SubmissionId = submission.Id;
        expiredPolicy.Carrier = carrier;
        expiredPolicy.CarrierId = carrier.Id;
        expiredPolicy.Status = PolicyStatus.Expired;

        db.AddRange(program, carrier, insured, submission, openQuote, assignedQuote, boundQuote, activePolicy, expiredPolicy);
        await db.SaveChangesAsync();

        var reports = new ReportService(new ServiceCollection().AddSingleton<DbContext>(db).BuildServiceProvider());

        var result = await reports.GetUnassignedProgramCleanupAsync();

        Assert.Equal(1, result.OpenQuoteCount);
        Assert.Equal(1, result.ActivePolicyCount);
        Assert.Equal(2, result.Rows.Count);

        var quoteRow = Assert.Single(result.Rows, r => r.RecordType == "Quote");
        Assert.Equal(openQuote.Id, quoteRow.Id);
        Assert.Equal("Q-CLEAN", quoteRow.ReferenceNumber);
        Assert.Equal("Cleanup Timber", quoteRow.InsuredName);
        Assert.Equal("Cleanup Carrier", quoteRow.CarrierName);
        Assert.Equal("MS", quoteRow.State);
        Assert.Equal("Quoted", quoteRow.Status);
        Assert.Equal($"/quotes/{openQuote.Id}", quoteRow.ActionUrl);

        var policyRow = Assert.Single(result.Rows, r => r.RecordType == "Policy");
        Assert.Equal(activePolicy.Id, policyRow.Id);
        Assert.Equal("POL-CLEAN", policyRow.ReferenceNumber);
        Assert.Equal("Active", policyRow.Status);
        Assert.Equal($"/policies/{activePolicy.Id}", policyRow.ActionUrl);
    }

    private static Policy PolicyFor(Guid? programId) => new()
    {
        Id = Guid.NewGuid(),
        PolicyNumber = Guid.NewGuid().ToString("N"),
        SubmissionId = Guid.NewGuid(),
        BoundQuoteId = Guid.NewGuid(),
        ProgramId = programId,
        CarrierId = Guid.NewGuid(),
        LineOfBusiness = PolicyLineOfBusiness.InlandMarine,
        EffectiveDate = new DateOnly(2026, 1, 1),
        ExpirationDate = new DateOnly(2027, 1, 1),
        BoundDate = new DateOnly(2026, 1, 1)
    };

    private static PolicyTransaction TransactionFor(Policy policy) => new()
    {
        Id = Guid.NewGuid(),
        PolicyId = policy.Id,
        TransactionType = TransactionType.NewBusiness,
        Status = PolicyTransactionStatus.Issued,
        TransactionNumber = Guid.NewGuid().ToString("N"),
        EffectiveDate = policy.EffectiveDate,
        ProcessedById = Guid.NewGuid()
    };

    private static Invoice InvoiceFor(
        Guid policyTransactionId,
        string invoiceNumber,
        decimal grossPremium,
        decimal totalFees,
        decimal totalAmount,
        decimal commissionAmount,
        decimal agentCommissionAmount,
        string status = "Posted") => new()
    {
        InvoiceNumber = invoiceNumber,
        PolicyTransactionId = policyTransactionId,
        EffectiveDate = new DateOnly(2026, 1, 1),
        InvoiceDate = new DateOnly(2026, 1, 1),
        GrossPremium = grossPremium,
        TotalFees = totalFees,
        TotalAmount = totalAmount,
        CommissionAmount = commissionAmount,
        AgentCommissionAmount = agentCommissionAmount,
        LedgerTransactionId = Guid.NewGuid(),
        CreatedBy = Guid.NewGuid(),
        Status = status
    };

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
