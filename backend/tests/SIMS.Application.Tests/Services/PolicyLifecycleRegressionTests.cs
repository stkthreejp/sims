using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SIMS.Application.Common;
using SIMS.Application.DTOs;
using SIMS.Application.DTOs.Accounting;
using SIMS.Application.DTOs.Attachments;
using SIMS.Application.DTOs.Policies;
using SIMS.Application.DTOs.Quotes;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Security;
using SIMS.Application.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Entities.Accounting;
using SIMS.Domain.Entities.Rating;
using SIMS.Domain.Enums;
using SIMS.Infrastructure.Data;
using System.Text.Json;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class PolicyLifecycleRegressionTests
{
    [Fact]
    public async Task QuoteBind_CreatesPolicy()
    {
        await using var db = CreateDb();
        var fixture = await SeedBindableQuoteAsync(db);
        var invoicing = new RecordingInvoicingService();
        var quoteService = CreateQuoteService(db, invoicing);

        var result = await quoteService.BindAsync(fixture.Quote.Id, BindRequest(), UserAccessScope.All(fixture.UserId));

        Assert.True(result.IsSuccess);
        var policy = await db.Set<Policy>().SingleAsync();
        Assert.Equal(fixture.Quote.Id, policy.BoundQuoteId);
        Assert.Equal("POL-TEST-0001", policy.PolicyNumber);
        Assert.Equal(PolicyStatus.Active, policy.Status);
        Assert.Equal(fixture.Quote.TotalPremium, policy.TotalPremium);
    }

    [Fact]
    public async Task QuoteBind_CreatesNewBusinessTransaction()
    {
        await using var db = CreateDb();
        var fixture = await SeedBindableQuoteAsync(db);
        var quoteService = CreateQuoteService(db, new RecordingInvoicingService());

        var result = await quoteService.BindAsync(fixture.Quote.Id, BindRequest(), UserAccessScope.All(fixture.UserId));

        Assert.True(result.IsSuccess);
        var transaction = await db.Set<PolicyTransaction>().SingleAsync();
        Assert.Equal(TransactionType.NewBusiness, transaction.TransactionType);
        Assert.Equal(PolicyTransactionStatus.Issued, transaction.Status);
        Assert.Equal(fixture.Quote.TotalPremium, transaction.PremiumChange);
        Assert.Equal(fixture.Quote.TotalPremium, transaction.NewTotalPremium);
        var history = await db.Set<PolicyTransactionStatusHistory>()
            .Where(h => h.PolicyTransactionId == transaction.Id)
            .OrderBy(h => h.ChangedAt)
            .ToListAsync();
        Assert.Equal(
            new[] { "policy.transaction.created", "policy.transaction.issued" },
            history.Select(h => h.EventName).ToArray());
    }

    [Fact]
    public async Task QuoteBind_CreatesInitialPolicyVersionAndLinksTransaction()
    {
        await using var db = CreateDb();
        var fixture = await SeedBindableQuoteAsync(db);
        await SeedSnapshotExposureDataAsync(db, fixture);
        var quoteService = CreateQuoteService(db, new RecordingInvoicingService());

        var result = await quoteService.BindAsync(fixture.Quote.Id, BindRequest(), UserAccessScope.All(fixture.UserId));

        Assert.True(result.IsSuccess);
        var policy = await db.Set<Policy>().SingleAsync();
        var transaction = await db.Set<PolicyTransaction>().SingleAsync();
        var version = await db.Set<PolicyVersion>().SingleAsync();
        Assert.Equal(policy.Id, version.PolicyId);
        Assert.Equal(1, version.VersionNumber);
        Assert.Null(version.PriorPolicyVersionId);
        Assert.Equal(transaction.Id, version.CreatedByPolicyTransactionId);
        Assert.Equal(version.Id, transaction.ResultingPolicyVersionId);
        Assert.Null(transaction.PriorPolicyVersionId);
        Assert.Equal(policy.EffectiveDate, version.EffectiveDate);
        Assert.Equal(policy.ExpirationDate, version.ExpirationDate);
        Assert.Equal(PolicyStatus.Active, version.Status);
        Assert.Equal(policy.PremiumAmount, version.PremiumAmount);
        Assert.Equal(policy.TaxesAndFees, version.TaxesAndFees);
        Assert.Equal(policy.TotalPremium, version.TotalPremium);

        using var coverageJson = JsonDocument.Parse(version.CoverageSnapshotJson);
        var coverage = coverageJson.RootElement;
        Assert.Equal("Scheduled inland marine", coverage.GetProperty("CoverageDescription").GetString());
        Assert.Equal("InlandMarine", coverage.GetProperty("LineOfBusiness").GetString());
        Assert.Equal(2500m, coverage.GetProperty("Deductible").GetDecimal());
        Assert.Equal(100000m, coverage.GetProperty("Limit").GetDecimal());

        using var exposureJson = JsonDocument.Parse(version.ExposureSnapshotJson);
        var exposure = exposureJson.RootElement;
        Assert.Equal(1, exposure.GetProperty("LocationCount").GetInt32());
        Assert.Equal("100 Main St", exposure.GetProperty("Locations")[0].GetProperty("Address").GetString());
        Assert.Equal("Jane Driver", exposure.GetProperty("Drivers")[0].GetProperty("Name").GetString());
        Assert.Equal("VIN123", exposure.GetProperty("Vehicles")[0].GetProperty("Vin").GetString());
        Assert.Equal("Truck", exposure.GetProperty("Vehicles")[0].GetProperty("VehicleClass").GetString());
        Assert.Equal("Excavator", exposure.GetProperty("Equipment")[0].GetProperty("Description").GetString());
        Assert.Equal(50000m, exposure.GetProperty("Equipment")[0].GetProperty("Value").GetDecimal());
        Assert.Equal("Bank of Testing", exposure.GetProperty("AdditionalInterests")[0].GetProperty("Name").GetString());
        Assert.True(exposure.GetProperty("BlanketAdditionalInterests")[0].GetProperty("AdditionalInsured").GetBoolean());
        Assert.Equal("PF-1", exposure.GetProperty("PolicyForms")[0].GetProperty("FormNumber").GetString());
    }

    [Fact]
    public async Task QuoteBind_LocksLatestRatingSnapshot()
    {
        await using var db = CreateDb();
        var fixture = await SeedBindableQuoteAsync(db);
        var olderSnapshot = new QuoteRatingSnapshot
        {
            QuoteId = fixture.Quote.Id,
            RatingPlanVersionId = Guid.NewGuid(),
            RatedById = fixture.UserId,
            RatedAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            GrandTotalPremium = 900m,
        };
        var latestSnapshot = new QuoteRatingSnapshot
        {
            QuoteId = fixture.Quote.Id,
            RatingPlanVersionId = Guid.NewGuid(),
            RatedById = fixture.UserId,
            RatedAt = new DateTime(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc),
            GrandTotalPremium = 1000m,
        };
        db.AddRange(olderSnapshot, latestSnapshot);
        await db.SaveChangesAsync();
        var quoteService = CreateQuoteService(db, new RecordingInvoicingService());

        var result = await quoteService.BindAsync(fixture.Quote.Id, BindRequest(), UserAccessScope.All(fixture.UserId));

        Assert.True(result.IsSuccess);
        Assert.False(olderSnapshot.IsBoundSnapshot);
        Assert.True(latestSnapshot.IsBoundSnapshot);
    }

    [Fact]
    public async Task QuoteBind_CreatesInvoice()
    {
        await using var db = CreateDb();
        var fixture = await SeedBindableQuoteAsync(db);
        var invoicing = new RecordingInvoicingService();
        var quoteService = CreateQuoteService(db, invoicing);

        var result = await quoteService.BindAsync(fixture.Quote.Id, BindRequest(), UserAccessScope.All(fixture.UserId));

        Assert.True(result.IsSuccess);
        var invoiceRequest = Assert.Single(invoicing.BindRequests);
        Assert.False(invoiceRequest.IsEndorsement);
        Assert.Equal(fixture.Quote.PremiumAmount, invoiceRequest.GrossPremium);
        Assert.Equal(fixture.Quote.CarrierId, invoiceRequest.CarrierId);
        Assert.NotNull(invoiceRequest.PolicyTransactionId);
        var transaction = await db.Set<PolicyTransaction>().SingleAsync();
        Assert.Equal(transaction.ResultingPolicyVersionId, invoiceRequest.PolicyVersionId);
    }

    [Fact]
    public async Task Invoice_CanReconcileToResultingPolicyVersion()
    {
        await using var db = CreateDb();
        var fixture = await SeedBoundPolicyAsync(db);
        await SeedLedgerAccountsAsync(db);
        var version = new PolicyVersion
        {
            PolicyId = fixture.Policy.Id,
            Policy = fixture.Policy,
            VersionNumber = 1,
            EffectiveDate = fixture.Policy.EffectiveDate,
            ExpirationDate = fixture.Policy.ExpirationDate,
            Status = fixture.Policy.Status,
            PremiumAmount = fixture.Policy.PremiumAmount,
            TaxesAndFees = fixture.Policy.TaxesAndFees,
            TotalPremium = fixture.Policy.TotalPremium,
            CreatedById = fixture.UserId,
        };
        var transaction = new PolicyTransaction
        {
            PolicyId = fixture.Policy.Id,
            Policy = fixture.Policy,
            TransactionType = TransactionType.NewBusiness,
            Status = PolicyTransactionStatus.Issued,
            TransactionNumber = "TXN-INVOICE-1",
            EffectiveDate = fixture.Policy.EffectiveDate,
            ResultingPolicyVersionId = version.Id,
            PremiumChange = fixture.Policy.TotalPremium,
            NewTotalPremium = fixture.Policy.TotalPremium,
            ProcessedById = fixture.UserId,
        };
        db.AddRange(version, transaction);
        await db.SaveChangesAsync();
        var services = new ServiceCollection()
            .AddSingleton<DbContext>(db)
            .BuildServiceProvider();
        var invoicing = new InvoicingService(services, new EmptyFeeCalculationService(), new RecordingLedgerService());

        var result = await invoicing.BindAsync(new CreateInvoiceRequest(
            EffectiveDate: fixture.Policy.EffectiveDate,
            GrossPremium: fixture.Policy.PremiumAmount,
            StateCode: fixture.Insured.State,
            IsEndorsement: false,
            IsFilingState: false,
            CarrierId: fixture.Policy.CarrierId,
            CompanyId: fixture.Quote.CompanyId,
            ProducerId: fixture.Quote.ProducerId,
            LineOfBusiness: fixture.Policy.LineOfBusiness.ToString(),
            City: null,
            LicenseType: null,
            PolicyTransactionId: transaction.Id), fixture.UserId);

        Assert.True(result.IsSuccess, $"{result.ErrorCode}: {result.ErrorMessage}");
        Assert.Equal(transaction.Id, result.Value!.PolicyTransactionId);
        Assert.Equal(version.Id, result.Value.PolicyVersionId);
        var invoice = await db.Set<Invoice>().SingleAsync();
        Assert.Equal(transaction.Id, invoice.PolicyTransactionId);
        Assert.Equal(version.Id, invoice.PolicyVersionId);
    }

    [Fact]
    public async Task IssuePolicy_RequiresReadyForms()
    {
        await using var db = CreateDb();
        var fixture = await SeedBoundPolicyAsync(db);
        var assembly = new RecordingPolicyAssemblyService();
        var policyService = CreatePolicyService(db, new RecordingInvoicingService(), assembly: assembly);

        var result = await policyService.IssueAsync(fixture.Policy.Id, new IssuePolicyDto
        {
            IssuedDate = new DateOnly(2026, 1, 5),
        }, UserAccessScope.All(fixture.UserId));

        Assert.False(result.IsSuccess);
        Assert.Equal("FORMS_REQUIRED", result.ErrorCode);
        Assert.False(assembly.WasCalled);
    }

    [Fact]
    public async Task IssuePolicy_CompletesNewBusinessTransaction()
    {
        await using var db = CreateDb();
        var fixture = await SeedBoundPolicyAsync(db);
        await SeedReadyPolicyFormsAsync(db, fixture.Quote);
        var version = new PolicyVersion
        {
            PolicyId = fixture.Policy.Id,
            Policy = fixture.Policy,
            VersionNumber = 1,
            EffectiveDate = fixture.Policy.EffectiveDate,
            ExpirationDate = fixture.Policy.ExpirationDate,
            Status = fixture.Policy.Status,
            PremiumAmount = fixture.Policy.PremiumAmount,
            TaxesAndFees = fixture.Policy.TaxesAndFees,
            TotalPremium = fixture.Policy.TotalPremium,
            CreatedById = fixture.UserId,
        };
        var transaction = new PolicyTransaction
        {
            PolicyId = fixture.Policy.Id,
            Policy = fixture.Policy,
            TransactionType = TransactionType.NewBusiness,
            Status = PolicyTransactionStatus.Issued,
            TransactionNumber = "TXN-ISSUE-1",
            EffectiveDate = fixture.Policy.EffectiveDate,
            ResultingPolicyVersionId = version.Id,
            PremiumChange = fixture.Policy.TotalPremium,
            NewTotalPremium = fixture.Policy.TotalPremium,
            ProcessedById = fixture.UserId,
        };
        db.AddRange(version, transaction);
        await db.SaveChangesAsync();
        var assembly = new RecordingPolicyAssemblyService();
        var policyService = CreatePolicyService(db, new RecordingInvoicingService(), assembly: assembly);

        var result = await policyService.IssueAsync(fixture.Policy.Id, new IssuePolicyDto
        {
            IssuedDate = new DateOnly(2026, 1, 5),
        }, UserAccessScope.All(fixture.UserId));

        Assert.True(result.IsSuccess, $"{result.ErrorCode}: {result.ErrorMessage}");
        Assert.Equal(new DateOnly(2026, 1, 5), fixture.Policy.IssuedDate);
        Assert.Equal(PolicyTransactionStatus.Completed, transaction.Status);
        Assert.Equal(fixture.UserId, transaction.CompletedById);
        Assert.NotNull(transaction.CompletedAt);
        Assert.Equal(transaction.ResultingPolicyVersionId, assembly.AssembledPolicyVersionId);
        var history = await db.Set<PolicyTransactionStatusHistory>()
            .Where(h => h.PolicyTransactionId == transaction.Id)
            .OrderBy(h => h.ChangedAt)
            .ToListAsync();
        Assert.Equal("policy.transaction.completed", Assert.Single(history).EventName);
    }

    [Fact]
    public async Task Endorsement_CanBeCreatedAndIssued()
    {
        await using var db = CreateDb();
        var fixture = await SeedBoundPolicyAsync(db);
        var invoicing = new RecordingInvoicingService();
        var policyService = CreatePolicyService(db, invoicing);

        var createResult = await policyService.AddEndorsementAsync(fixture.Policy.Id, new CreateEndorsementDto
        {
            EffectiveDate = new DateOnly(2026, 6, 1),
            PremiumChange = 125m,
            EndorsementDescription = "Add scheduled equipment",
        }, UserAccessScope.All(fixture.UserId));

        Assert.True(createResult.IsSuccess);
        Assert.Equal(PolicyTransactionStatus.Submitted, createResult.Value!.Status);
        Assert.NotEqual(Guid.Empty, createResult.Value.Id);
        Assert.Equal(1, await db.Set<PolicyTransaction>().CountAsync(t => t.PolicyId == fixture.Policy.Id));
        Assert.NotNull(await db.Set<PolicyTransaction>().FindAsync(createResult.Value.Id));
        db.ChangeTracker.Clear();

        var issueResult = await policyService.IssueEndorsementAsync(
            fixture.Policy.Id,
            createResult.Value.Id,
            new IssueEndorsementDto(),
            UserAccessScope.All(fixture.UserId));

        Assert.True(issueResult.IsSuccess, $"{issueResult.ErrorCode}: {issueResult.ErrorMessage}");
        var transaction = await db.Set<PolicyTransaction>().SingleAsync(t => t.Id == createResult.Value.Id);
        Assert.Equal(PolicyTransactionStatus.Issued, transaction.Status);
        Assert.Equal(1125m, transaction.Policy.TotalPremium);
        var versions = await db.Set<PolicyVersion>()
            .Where(v => v.PolicyId == fixture.Policy.Id)
            .OrderBy(v => v.VersionNumber)
            .ToListAsync();
        Assert.Equal(2, versions.Count);
        Assert.Equal(1000m, versions[0].TotalPremium);
        Assert.Equal(1125m, versions[1].TotalPremium);
        Assert.Equal(versions[0].Id, versions[1].PriorPolicyVersionId);
        Assert.Equal(versions[0].Id, transaction.PriorPolicyVersionId);
        Assert.Equal(versions[1].Id, transaction.ResultingPolicyVersionId);
        var history = await db.Set<PolicyTransactionStatusHistory>()
            .Where(h => h.PolicyTransactionId == transaction.Id)
            .OrderBy(h => h.ChangedAt)
            .ToListAsync();
        Assert.Equal(
            new[] { "policy.transaction.created", "policy.transaction.submitted", "policy.transaction.issued" },
            history.Select(h => h.EventName).ToArray());
        var invoiceRequest = Assert.Single(invoicing.BindRequests);
        Assert.True(invoiceRequest.IsEndorsement);
        Assert.Equal(transaction.Id, invoiceRequest.PolicyTransactionId);
        Assert.Equal(transaction.ResultingPolicyVersionId, invoiceRequest.PolicyVersionId);

        var detailResult = await policyService.GetByIdAsync(fixture.Policy.Id, UserAccessScope.All(fixture.UserId));

        Assert.True(detailResult.IsSuccess);
        var transactionDto = Assert.Single(detailResult.Value!.Transactions, t => t.Id == transaction.Id);
        Assert.NotNull(transactionDto.PriorVersion);
        Assert.NotNull(transactionDto.ResultingVersion);
        Assert.Equal(1, transactionDto.PriorVersion.VersionNumber);
        Assert.Equal(2, transactionDto.ResultingVersion.VersionNumber);
        Assert.Equal(PolicyStatus.Active, transactionDto.PriorVersion.Status);
        Assert.Equal(PolicyStatus.Active, transactionDto.ResultingVersion.Status);
        Assert.Equal(1000m, transactionDto.PriorVersion.TotalPremium);
        Assert.Equal(1125m, transactionDto.ResultingVersion.TotalPremium);
    }

    [Fact]
    public async Task Cancellation_RecordsLegalAndComplianceSnapshot()
    {
        await using var db = CreateDb();
        var fixture = await SeedBoundPolicyAsync(db);
        var requirement = new LegalRequirementSection
        {
            Id = Guid.NewGuid(),
            State = "North Carolina",
            LineOfBusiness = "InlandMarine",
            Action = "Cancellation",
            Category = "NOTICE REQUIREMENTS",
            Topic = "Notice timing",
            RequirementText = "Send written notice before cancellation.",
            Citations = ["NC-1"],
        };
        db.Add(requirement);
        await db.SaveChangesAsync();
        var policyService = CreatePolicyService(db, new RecordingInvoicingService());

        var result = await policyService.CancelAsync(fixture.Policy.Id, new CancelPolicyDto
        {
            CancelledDate = new DateOnly(2026, 7, 1),
            Reason = "Non-payment",
            Method = "Certified Mail",
            PremiumChange = -250m,
            ComplianceChecklist =
            [
                new CancellationComplianceChecklistItemDto
                {
                    Key = "notice",
                    Label = "Notice sent",
                    IsCompleted = true,
                    RequirementSectionIds = [requirement.Id],
                }
            ],
            LegalRequirementSectionIds = [requirement.Id],
        }, UserAccessScope.All(fixture.UserId));

        Assert.True(result.IsSuccess);
        var transaction = await db.Set<PolicyTransaction>().SingleAsync(t => t.TransactionType == TransactionType.Cancellation);
        Assert.Equal(PolicyTransactionStatus.Issued, transaction.Status);
        Assert.Contains("notice", transaction.CancellationComplianceChecklistJson);
        Assert.Contains("Send written notice before cancellation.", transaction.CancellationLegalRequirementSnapshotJson);
        var versions = await db.Set<PolicyVersion>()
            .Where(v => v.PolicyId == fixture.Policy.Id)
            .OrderBy(v => v.VersionNumber)
            .ToListAsync();
        Assert.Equal(2, versions.Count);
        Assert.Equal(PolicyStatus.Active, versions[0].Status);
        Assert.Equal(PolicyStatus.Cancelled, versions[1].Status);
        Assert.Equal(versions[0].Id, versions[1].PriorPolicyVersionId);
        Assert.Equal(versions[0].Id, transaction.PriorPolicyVersionId);
        Assert.Equal(versions[1].Id, transaction.ResultingPolicyVersionId);
        var history = await db.Set<PolicyTransactionStatusHistory>()
            .Where(h => h.PolicyTransactionId == transaction.Id)
            .OrderBy(h => h.ChangedAt)
            .ToListAsync();
        Assert.Equal(
            new[] { "policy.transaction.created", "policy.transaction.issued" },
            history.Select(h => h.EventName).ToArray());
        Assert.Equal(PolicyStatus.Cancelled, fixture.Policy.Status);
    }

    [Fact]
    public async Task NonRenewal_UpdatesPolicyStatus()
    {
        await using var db = CreateDb();
        var fixture = await SeedBoundPolicyAsync(db);
        var policyService = CreatePolicyService(db, new RecordingInvoicingService());
        var nonRenewedDate = new DateOnly(2026, 12, 31);

        var result = await policyService.NonRenewAsync(fixture.Policy.Id, new NonRenewPolicyDto
        {
            NonRenewedDate = nonRenewedDate,
            Reason = "Carrier appetite change",
        }, UserAccessScope.All(fixture.UserId));

        Assert.True(result.IsSuccess);
        Assert.Equal(PolicyStatus.NonRenewed, fixture.Policy.Status);
        Assert.Equal(nonRenewedDate, fixture.Policy.NonRenewedDate);
        var transaction = await db.Set<PolicyTransaction>().SingleAsync(t => t.TransactionType == TransactionType.NonRenewal);
        Assert.Equal(PolicyTransactionStatus.Completed, transaction.Status);
        Assert.Equal(nonRenewedDate, transaction.EffectiveDate);
        Assert.Equal("Carrier appetite change", transaction.ReasonText);
        Assert.Equal(fixture.Policy.BoundQuoteId, transaction.SourceQuoteId);
        Assert.Equal(fixture.Policy.TotalPremium, transaction.PremiumBefore);
        Assert.Equal(fixture.Policy.TotalPremium, transaction.NewTotalPremium);
        Assert.Equal(fixture.Policy.TotalPremium, transaction.PremiumAfter);
        var versions = await db.Set<PolicyVersion>()
            .Where(v => v.PolicyId == fixture.Policy.Id)
            .OrderBy(v => v.VersionNumber)
            .ToListAsync();
        Assert.Equal(2, versions.Count);
        Assert.Equal(PolicyStatus.Active, versions[0].Status);
        Assert.Equal(PolicyStatus.NonRenewed, versions[1].Status);
        Assert.Equal(versions[0].Id, versions[1].PriorPolicyVersionId);
        Assert.Equal(versions[0].Id, transaction.PriorPolicyVersionId);
        Assert.Equal(versions[1].Id, transaction.ResultingPolicyVersionId);
        var history = await db.Set<PolicyTransactionStatusHistory>()
            .Where(h => h.PolicyTransactionId == transaction.Id)
            .OrderBy(h => h.ChangedAt)
            .ToListAsync();
        Assert.Equal(
            new[] { "policy.transaction.created", "policy.transaction.completed" },
            history.Select(h => h.EventName).ToArray());
    }

    [Fact]
    public async Task Renewal_CreatesRenewalQuote()
    {
        await using var db = CreateDb();
        var fixture = await SeedBoundPolicyAsync(db);
        var renewalQuotes = new RecordingQuoteService(db);
        var policyService = CreatePolicyService(db, new RecordingInvoicingService(), quoteService: renewalQuotes);

        var result = await policyService.CreateRenewalQuoteAsync(fixture.Policy.Id, UserAccessScope.All(fixture.UserId));

        Assert.True(result.IsSuccess);
        var request = Assert.Single(renewalQuotes.CreateRequests);
        Assert.Equal(fixture.Policy.SubmissionId, request.SubmissionId);
        Assert.Equal(fixture.Policy.CarrierId, request.CarrierId);
        Assert.Equal(fixture.Policy.ExpirationDate, request.EffectiveDate);
        Assert.Equal(fixture.Policy.ExpirationDate.AddYears(1), request.ExpirationDate);
        Assert.Equal(fixture.Policy.PremiumAmount, request.PremiumAmount);
        var transaction = await db.Set<PolicyTransaction>().SingleAsync(t => t.TransactionType == TransactionType.Renewal);
        Assert.Equal(PolicyTransactionStatus.Submitted, transaction.Status);
        Assert.Equal(fixture.Policy.ExpirationDate, transaction.EffectiveDate);
        Assert.Equal(fixture.Policy.Id, transaction.PriorPolicyId);
        Assert.Equal(fixture.Policy.BoundQuoteId, transaction.SourceQuoteId);
        Assert.Equal(result.Value!.Id, transaction.RenewalQuoteId);
        Assert.Equal(fixture.Policy.TotalPremium, transaction.PremiumBefore);
        Assert.Equal(0m, transaction.PremiumChange);
        Assert.Equal(fixture.Policy.TotalPremium, transaction.NewTotalPremium);
        var history = await db.Set<PolicyTransactionStatusHistory>()
            .Where(h => h.PolicyTransactionId == transaction.Id)
            .OrderBy(h => h.ChangedAt)
            .ToListAsync();
        Assert.Equal(
            new[] { "policy.transaction.created", "policy.transaction.submitted" },
            history.Select(h => h.EventName).ToArray());
    }

    [Fact]
    public async Task VoidTestBind_ProtectsNonTestRecords()
    {
        await using var db = CreateDb();
        var fixture = await SeedBoundPolicyAsync(db, insuredName: "Acme Hauling");
        db.Add(new PolicyTransaction
        {
            PolicyId = fixture.Policy.Id,
            Policy = fixture.Policy,
            TransactionType = TransactionType.NewBusiness,
            Status = PolicyTransactionStatus.Issued,
            TransactionNumber = "TXN-VOID-1",
            EffectiveDate = fixture.Policy.EffectiveDate,
            PremiumChange = fixture.Policy.TotalPremium,
            NewTotalPremium = fixture.Policy.TotalPremium,
            ProcessedById = fixture.UserId,
        });
        await db.SaveChangesAsync();
        var policyService = CreatePolicyService(db, new RecordingInvoicingService());

        var result = await policyService.VoidTestBindAsync(fixture.Policy.Id, new VoidTestBindDto
        {
            Reason = "cleanup",
        }, UserAccessScope.All(fixture.UserId), isAdmin: true);

        Assert.False(result.IsSuccess);
        Assert.Equal("NOT_TEST_RECORD", result.ErrorCode);
        Assert.False(fixture.Policy.IsDeleted);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static QuoteService CreateQuoteService(
        ApplicationDbContext db,
        RecordingInvoicingService invoicing,
        RecordingWorkflowEngineService? workflow = null)
    {
        workflow ??= new RecordingWorkflowEngineService();
        var services = new ServiceCollection()
            .AddSingleton<DbContext>(db)
            .AddSingleton<IInvoicingService>(invoicing)
            .BuildServiceProvider();

        return new QuoteService(
            services,
            workflow,
            new NoOpCarrierCommissionService(),
            new NoOpAgentCommissionService(),
            new NoOpQuoteChecklistService(),
            new StubPolicyNumberService(),
            new PolicyTransactionLifecycleService(db, workflow),
            new PolicyVersionService(db));
    }

    private static PolicyService CreatePolicyService(
        ApplicationDbContext db,
        RecordingInvoicingService invoicing,
        RecordingPolicyAssemblyService? assembly = null,
        IQuoteService? quoteService = null,
        RecordingWorkflowEngineService? workflow = null)
    {
        assembly ??= new RecordingPolicyAssemblyService();
        quoteService ??= new RecordingQuoteService(db);
        workflow ??= new RecordingWorkflowEngineService();
        var services = new ServiceCollection()
            .AddSingleton<DbContext>(db)
            .AddSingleton<IQuotePolicyFormSelectionService>(new NoOpQuotePolicyFormSelectionService())
            .AddSingleton<IPolicyAssemblyService>(assembly)
            .AddSingleton(quoteService)
            .BuildServiceProvider();

        return new PolicyService(services, invoicing, new RecordingVoidService(), new PolicyTransactionLifecycleService(db, workflow), new PolicyVersionService(db));
    }

    private static QuoteBindDto BindRequest() => new()
    {
        BoundDate = new DateOnly(2026, 1, 5),
        EffectiveDate = new DateOnly(2026, 1, 5),
        ExpirationDate = new DateOnly(2027, 1, 5),
    };

    private static async Task<QuoteFixture> SeedBindableQuoteAsync(ApplicationDbContext db)
    {
        var fixture = CreateQuoteFixture("Test Logistics");
        var template = new PolicyFormTemplate
        {
            Id = Guid.NewGuid(),
            FormNumber = "PF-1",
            Name = "Coverage Form",
            FileName = "coverage.pdf",
            StoragePath = "forms/coverage.pdf",
        };
        var selection = new QuotePolicyFormSelection
        {
            Id = Guid.NewGuid(),
            QuoteId = fixture.Quote.Id,
            Quote = fixture.Quote,
            PolicyFormTemplateId = template.Id,
            PolicyFormTemplate = template,
            IsIncluded = true,
            SequenceOrder = 1,
        };
        db.AddRange(fixture.User, fixture.Carrier, fixture.Insured, fixture.Submission, fixture.Quote, template, selection);
        await db.SaveChangesAsync();
        return fixture;
    }

    private static async Task SeedSnapshotExposureDataAsync(ApplicationDbContext db, QuoteFixture fixture)
    {
        fixture.Quote.CoverageDescription = "Scheduled inland marine";
        fixture.Quote.Deductible = 2500m;
        fixture.Quote.Limit = 100000m;
        db.AddRange(
            new SubmissionLocation
            {
                SubmissionId = fixture.Submission.Id,
                LocationNumber = 1,
                Address = "100 Main St",
                ZipCode = "27601",
            },
            new SubmissionDriver
            {
                SubmissionId = fixture.Submission.Id,
                DriverNumber = 1,
                Name = "Jane Driver",
                LicenseNumber = "D123",
                LicenseState = "NC",
            },
            new SubmissionVehicle
            {
                SubmissionId = fixture.Submission.Id,
                UnitNumber = 1,
                Year = 2024,
                Make = "Peterbilt",
                Model = "579",
                Vin = "VIN123",
                VehicleClass = VehicleClass.Truck,
                GaragingZip = "27601",
                Radius = OperatingRadius.Local,
            },
            new SubmissionEquipment
            {
                SubmissionId = fixture.Submission.Id,
                ItemNumber = 1,
                Year = 2023,
                Make = "CAT",
                Model = "320",
                Description = "Excavator",
                SerialNumber = "EQ123",
                Value = 50000m,
                TerritoryCode = "NC-01",
                Deductible = 2500m,
            },
            new SubmissionAdditionalInterest
            {
                SubmissionId = fixture.Submission.Id,
                LineOfBusiness = PolicyLineOfBusiness.InlandMarine,
                Name = "Bank of Testing",
                City = "Raleigh",
                State = "NC",
                ZipCode = "27601",
                AppliesToType = AdditionalInterestAppliesToType.ScheduledItems,
                ScheduledItemNumbers = "1",
                LossPayee = true,
            },
            new SubmissionAdditionalInterestBlanket
            {
                SubmissionId = fixture.Submission.Id,
                LineOfBusiness = PolicyLineOfBusiness.InlandMarine,
                AdditionalInsured = true,
                WaiverOfSubrogation = true,
            });

        await db.SaveChangesAsync();
    }

    private static async Task<PolicyFixture> SeedBoundPolicyAsync(ApplicationDbContext db, string insuredName = "Test Logistics")
    {
        var quoteFixture = CreateQuoteFixture(insuredName);
        quoteFixture.Quote.Status = QuoteStatus.Bound;
        quoteFixture.Quote.PolicyNumber = "POL-BOUND-1";
        quoteFixture.Quote.BoundDate = new DateOnly(2026, 1, 1);
        var policy = new Policy
        {
            Id = Guid.NewGuid(),
            PolicyNumber = "POL-BOUND-1",
            SubmissionId = quoteFixture.Submission.Id,
            Submission = quoteFixture.Submission,
            BoundQuoteId = quoteFixture.Quote.Id,
            BoundQuote = quoteFixture.Quote,
            CarrierId = quoteFixture.Carrier.Id,
            Carrier = quoteFixture.Carrier,
            LineOfBusiness = quoteFixture.Quote.LineOfBusiness,
            EffectiveDate = quoteFixture.Quote.EffectiveDate,
            ExpirationDate = quoteFixture.Quote.ExpirationDate,
            PremiumAmount = quoteFixture.Quote.PremiumAmount,
            TaxesAndFees = quoteFixture.Quote.TaxesAndFees,
            TotalPremium = quoteFixture.Quote.TotalPremium,
            Status = PolicyStatus.Active,
            BoundDate = quoteFixture.Quote.BoundDate.Value,
        };

        db.AddRange(quoteFixture.User, quoteFixture.Carrier, quoteFixture.Insured, quoteFixture.Submission, quoteFixture.Quote, policy);
        await db.SaveChangesAsync();
        return new PolicyFixture(quoteFixture.UserId, quoteFixture.Insured, quoteFixture.Submission, quoteFixture.Quote, policy);
    }

    private static async Task SeedReadyPolicyFormsAsync(ApplicationDbContext db, Quote quote)
    {
        var template = new PolicyFormTemplate
        {
            Id = Guid.NewGuid(),
            FormNumber = "PF-ISSUE",
            Name = "Issuance Form",
            FileName = "issuance.pdf",
            StoragePath = "forms/issuance.pdf",
        };
        var selection = new QuotePolicyFormSelection
        {
            Id = Guid.NewGuid(),
            QuoteId = quote.Id,
            Quote = quote,
            PolicyFormTemplateId = template.Id,
            PolicyFormTemplate = template,
            IsIncluded = true,
            SequenceOrder = 1,
        };

        db.AddRange(template, selection);
        await db.SaveChangesAsync();
    }

    private static async Task SeedLedgerAccountsAsync(ApplicationDbContext db)
    {
        db.AddRange(
            new LedgerAccount { InternalCode = "1200", ExternalLabel = "Accounts Receivable", AccountType = "Asset" },
            new LedgerAccount { InternalCode = "2100", ExternalLabel = "Carrier Payable", AccountType = "Liability" },
            new LedgerAccount { InternalCode = "4100", ExternalLabel = "Commission Revenue", AccountType = "Revenue" },
            new LedgerAccount { InternalCode = "5100", ExternalLabel = "Commission Expense", AccountType = "Expense" });
        await db.SaveChangesAsync();
    }

    private static QuoteFixture CreateQuoteFixture(string insuredName)
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            UserName = "regression@sims.test",
            Email = "regression@sims.test",
            FirstName = "Regression",
            LastName = "User",
        };
        var carrier = new Carrier
        {
            Id = Guid.NewGuid(),
            Name = "Oden Specialty",
            IsActive = true,
        };
        var insured = new Insured
        {
            Id = Guid.NewGuid(),
            InsuredType = InsuredType.Commercial,
            CompanyName = insuredName,
            State = "NC",
            CreatedById = userId,
        };
        var submission = new Submission
        {
            Id = Guid.NewGuid(),
            SubmissionNumber = "SUB-REG-1",
            InsuredId = insured.Id,
            Insured = insured,
            UnderwriterId = userId,
            CreatedById = userId,
            Status = SubmissionStatus.Quoted,
        };
        var quote = new Quote
        {
            Id = Guid.NewGuid(),
            QuoteNumber = "QTE-REG-1",
            SubmissionId = submission.Id,
            Submission = submission,
            CarrierId = carrier.Id,
            Carrier = carrier,
            LineOfBusiness = PolicyLineOfBusiness.InlandMarine,
            Status = QuoteStatus.Quoted,
            EffectiveDate = new DateOnly(2026, 1, 1),
            ExpirationDate = new DateOnly(2027, 1, 1),
            PremiumAmount = 900m,
            TaxesAndFees = 100m,
            TotalPremium = 1000m,
            CreatedById = userId,
        };

        return new QuoteFixture(userId, user, carrier, insured, submission, quote);
    }

    private sealed record QuoteFixture(Guid UserId, User User, Carrier Carrier, Insured Insured, Submission Submission, Quote Quote);
    private sealed record PolicyFixture(Guid UserId, Insured Insured, Submission Submission, Quote Quote, Policy Policy);

    private sealed class StubPolicyNumberService : IPolicyNumberService
    {
        public Task<Result<PolicyNumberGenerationResult>> GenerateForBindAsync(Quote quote, Guid assignedById)
            => Task.FromResult(Result<PolicyNumberGenerationResult>.Success(new PolicyNumberGenerationResult(
                "POL-TEST-0001",
                "POL-TEST-0001",
                1,
                null,
                null,
                null)));
    }

    private sealed class RecordingInvoicingService : IInvoicingService
    {
        public List<CreateInvoiceRequest> BindRequests { get; } = [];

        public Task<Result<InvoiceDetailDto>> BindAsync(CreateInvoiceRequest req, Guid userId, CancellationToken ct = default)
        {
            BindRequests.Add(req);
            return Task.FromResult(Result<InvoiceDetailDto>.Success(new InvoiceDetailDto(
                BindRequests.Count,
                $"INV-{BindRequests.Count:000}",
                DateOnly.FromDateTime(DateTime.UtcNow),
                req.EffectiveDate,
                req.GrossPremium,
                0m,
                req.GrossPremium,
                "Posted",
                req.PolicyTransactionId,
                req.PolicyVersionId,
                Guid.NewGuid(),
                [],
                [])));
        }

        public Task<IReadOnlyList<InvoiceSummaryDto>> GetInvoicesAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<InvoiceSummaryDto>>([]);

        public Task<Result<InvoiceDetailDto>> GetInvoiceAsync(long id, CancellationToken ct = default)
            => Task.FromResult(Result<InvoiceDetailDto>.Failure("NOT_FOUND", "Invoice not found."));
    }

    private sealed class EmptyFeeCalculationService : IFeeCalculationService
    {
        public Task<FeeCalculationResult> CalculateAsync(PolicyContext ctx, CancellationToken ct = default)
            => Task.FromResult(new FeeCalculationResult([]));
    }

    private sealed class RecordingLedgerService : ILedgerService
    {
        public Task<Guid> PostInvoiceAsync(Invoice invoice, int arAccountId, int carrierApAccountId, int commissionAccountId, int agentCommissionExpenseAccountId, Guid userId, CancellationToken ct = default)
            => Task.FromResult(Guid.NewGuid());

        public Task<Guid> PostReceiptAsync(Receipt receipt, int trustAccountId, int unappliedCashAccountId, Guid userId, CancellationToken ct = default)
            => Task.FromResult(Guid.NewGuid());

        public Task<Guid> PostCashApplicationAsync(Receipt receipt, Invoice invoice, decimal grossApplied, decimal commissionAmount, int unappliedCashAccountId, int commissionExpenseAccountId, int arAccountId, Guid userId, CancellationToken ct = default)
            => Task.FromResult(Guid.NewGuid());

        public Task<Guid> PostDisbursementAsync(Disbursement disbursementWithLines, int trustAccountId, Guid userId, CancellationToken ct = default)
            => Task.FromResult(Guid.NewGuid());

        public Task<Guid> PostDistributionSweepAsync(CashMovementInstruction instruction, int trustAccountId, Guid userId, CancellationToken ct = default)
            => Task.FromResult(Guid.NewGuid());

        public Task<Guid> ReverseTransactionGroupAsync(Guid transactionId, string voidReason, Guid userId, DateOnly effectiveDate, CancellationToken ct = default)
            => Task.FromResult(Guid.NewGuid());
    }

    private sealed class RecordingPolicyAssemblyService : IPolicyAssemblyService
    {
        public bool WasCalled { get; private set; }
        public Guid? AssembledPolicyVersionId { get; private set; }

        public Task<Result<GeneratedDocumentDto>> AssembleAndFileAsync(Guid policyId, Guid userId, bool isPreview = false, Guid? policyVersionId = null)
        {
            WasCalled = true;
            AssembledPolicyVersionId = policyVersionId;
            return Task.FromResult(Result<GeneratedDocumentDto>.Success(DocumentResult()));
        }

        public Task<Result<GeneratedDocumentDto>> TestMergeTemplateAsync(Guid templateId, Guid policyId, Guid userId)
            => Task.FromResult(Result<GeneratedDocumentDto>.Success(DocumentResult()));

        private static GeneratedDocumentDto DocumentResult() => new(
            "/documents/test.pdf",
            new AttachmentDto
            {
                Id = Guid.NewGuid(),
                EntityType = DocumentEntityType.Policy,
                DocumentType = DocumentType.IssuedPolicyPacket,
                FileName = "test.pdf",
                ContentType = "application/pdf",
                CreatedAt = DateTime.UtcNow,
            });
    }

    private sealed class RecordingQuoteService(ApplicationDbContext db) : IQuoteService
    {
        public List<QuoteCreateDto> CreateRequests { get; } = [];

        public async Task<Result<QuoteDto>> CreateAsync(QuoteCreateDto dto, Guid createdById)
        {
            CreateRequests.Add(dto);
            var quote = new Quote
            {
                Id = Guid.NewGuid(),
                QuoteNumber = $"REN-{CreateRequests.Count:000}",
                SubmissionId = dto.SubmissionId,
                CarrierId = dto.CarrierId,
                LineOfBusiness = dto.LineOfBusiness,
                Status = QuoteStatus.Draft,
                EffectiveDate = dto.EffectiveDate,
                ExpirationDate = dto.ExpirationDate,
                PremiumAmount = dto.PremiumAmount,
                TaxesAndFees = dto.TaxesAndFees,
                TotalPremium = dto.PremiumAmount + dto.TaxesAndFees,
                CreatedById = createdById,
            };
            db.Add(quote);
            await db.SaveChangesAsync();

            return Result<QuoteDto>.Success(new QuoteDto
            {
                Id = quote.Id,
                QuoteNumber = quote.QuoteNumber,
                SubmissionId = quote.SubmissionId,
                CarrierId = quote.CarrierId,
                LineOfBusiness = quote.LineOfBusiness,
                Status = quote.Status,
                EffectiveDate = quote.EffectiveDate,
                ExpirationDate = quote.ExpirationDate,
                PremiumAmount = quote.PremiumAmount,
                TaxesAndFees = quote.TaxesAndFees,
                TotalPremium = quote.TotalPremium,
            });
        }

        public Task<Result<QuoteDto>> ApplyCommissionOverrideAsync(Guid id, CommissionOverrideRequest req, UserAccessScope access)
            => throw new NotSupportedException();

        public Task<Result<QuoteDto>> BindAsync(Guid id, QuoteBindDto dto, UserAccessScope access)
            => throw new NotSupportedException();

        public Task<Result> DeleteAsync(Guid id, UserAccessScope access)
            => throw new NotSupportedException();

        public Task<PagedResult<QuoteListItemDto>> GetAllAsync(QueryParameters query, UserAccessScope access)
            => throw new NotSupportedException();

        public Task<IEnumerable<QuoteListItemDto>> GetBoundByInsuredAsync(Guid insuredId)
            => throw new NotSupportedException();

        public Task<Result<QuoteDto>> GetByIdAsync(Guid id, UserAccessScope access)
            => throw new NotSupportedException();

        public Task<IEnumerable<QuoteListItemDto>> GetBySubmissionAsync(Guid submissionId, UserAccessScope access)
            => throw new NotSupportedException();

        public Task<Result<InvoicePreviewDto>> GetInvoicePreviewAsync(Guid id, UserAccessScope access)
            => throw new NotSupportedException();

        public Task<Result<QuoteDto>> UpdateAsync(Guid id, QuoteUpdateDto dto, UserAccessScope access)
            => throw new NotSupportedException();
    }

    private sealed class RecordingWorkflowEngineService : IWorkflowEngineService
    {
        public List<(string EventName, Guid EntityId)> Events { get; } = [];

        public Task FireEventAsync(string eventName, TaskEntityType entityType, Guid entityId, Dictionary<string, object> context)
        {
            Events.Add((eventName, entityId));
            return Task.CompletedTask;
        }

        public Task FireStepCompletedAsync(Guid completedStepId, TaskEntityType entityType, Guid entityId, Dictionary<string, object> context)
            => Task.CompletedTask;
    }

    private sealed class NoOpCarrierCommissionService : ICarrierCommissionService
    {
        public Task<Result<CarrierCommissionDto>> CreateAsync(Guid carrierId, CreateCarrierCommissionRequest req, Guid userId, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<Result<CarrierCommissionDto>> DisableAsync(long id, DateOnly? disabledDate, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<CarrierCommissionRates?> GetActiveRatesAsync(Guid carrierId, string? lineOfBusiness, DateOnly asOfDate, CancellationToken ct = default)
            => Task.FromResult<CarrierCommissionRates?>(null);

        public Task<IReadOnlyList<CarrierCommissionDto>> GetAllAsync(Guid carrierId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CarrierCommissionDto>>([]);
    }

    private sealed class NoOpAgentCommissionService : IAgentCommissionService
    {
        public Task<Result<AgentCommissionDto>> CreateAsync(Guid agentId, CreateAgentCommissionRequest req, Guid userId, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<Result<AgentCommissionDto>> DisableAsync(long id, DateOnly? disabledDate, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<decimal?> GetActiveRateAsync(Guid agentId, string? lineOfBusiness, DateOnly asOfDate, CancellationToken ct = default)
            => Task.FromResult<decimal?>(null);

        public Task<IReadOnlyList<AgentCommissionDto>> GetAllAsync(Guid agentId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<AgentCommissionDto>>([]);
    }

    private sealed class NoOpQuoteChecklistService : IQuoteChecklistService
    {
        public Task<Result<List<QuoteChecklistItemDto>>> GetForQuoteAsync(Guid quoteId)
            => Task.FromResult(Result<List<QuoteChecklistItemDto>>.Success([]));

        public Task SeedDefaultsAsync(Guid quoteId, PolicyLineOfBusiness lob)
            => Task.CompletedTask;

        public Task<Result<QuoteChecklistItemDto>> ToggleAsync(Guid itemId, bool completed, Guid userId, string userName)
            => throw new NotSupportedException();
    }

    private sealed class NoOpQuotePolicyFormSelectionService : IQuotePolicyFormSelectionService
    {
        public Task<Result<IReadOnlyList<QuotePolicyFormSelectionDto>>> GetOrSeedAsync(Guid quoteId)
            => Task.FromResult(Result<IReadOnlyList<QuotePolicyFormSelectionDto>>.Success([]));

        public Task<Result<IReadOnlyList<QuotePolicyFormSelectionDto>>> ResetFromPackageAsync(Guid quoteId)
            => throw new NotSupportedException();

        public Task<Result<IReadOnlyList<QuotePolicyFormSelectionDto>>> SaveAsync(Guid quoteId, IReadOnlyList<QuotePolicyFormSelectionUpsertDto> forms)
            => throw new NotSupportedException();
    }

    private sealed class RecordingVoidService : IVoidService
    {
        public Task<VoidResultDto> VoidCashApplicationAsync(long cashApplicationId, string? reason, Guid userId, bool isAdmin, CancellationToken ct = default)
            => Task.FromResult(new VoidResultDto(true, null, null, Guid.NewGuid()));

        public Task<VoidResultDto> VoidDisbursementAsync(long disbursementId, string? reason, Guid userId, bool isAdmin, CancellationToken ct = default)
            => Task.FromResult(new VoidResultDto(true, null, null, Guid.NewGuid()));

        public Task<VoidResultDto> VoidInvoiceAsync(long invoiceId, string? reason, Guid userId, bool isAdmin, CancellationToken ct = default)
            => Task.FromResult(new VoidResultDto(true, null, null, Guid.NewGuid()));

        public Task<VoidResultDto> VoidReceiptAsync(long receiptId, string? reason, Guid userId, bool isAdmin, CancellationToken ct = default)
            => Task.FromResult(new VoidResultDto(true, null, null, Guid.NewGuid()));
    }
}
