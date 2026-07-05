using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.Sqlite;
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
    public async Task QuoteCreate_RequiresProgram()
    {
        await using var db = CreateDb();
        var fixture = CreateQuoteFixture("No Program");
        db.AddRange(fixture.User, fixture.Carrier, fixture.Insured, fixture.Submission);
        await db.SaveChangesAsync();
        var quoteService = CreateQuoteService(db, new RecordingInvoicingService());

        var result = await quoteService.CreateAsync(CreateQuoteRequest(fixture, null), fixture.UserId);

        Assert.False(result.IsSuccess);
        Assert.Equal("PROGRAM_REQUIRED", result.ErrorCode);
    }

    [Fact]
    public async Task QuoteBind_RejectsDeclinedQuote()
    {
        await using var db = CreateDb();
        var fixture = await SeedBindableQuoteAsync(db);
        fixture.Quote.Status = QuoteStatus.Declined;
        await db.SaveChangesAsync();
        var quoteService = CreateQuoteService(db, new RecordingInvoicingService());

        var result = await quoteService.BindAsync(fixture.Quote.Id, BindRequest(), UserAccessScope.All(fixture.UserId));

        Assert.False(result.IsSuccess);
        Assert.Equal("QUOTE_NOT_BINDABLE", result.ErrorCode);
    }

    [Fact]
    public async Task QuoteBind_RequiresRerateWhenEffectiveDateChangedAfterRating()
    {
        await using var db = CreateDb();
        var fixture = await SeedBindableQuoteAsync(db); // quote effective 2026-01-01
        db.Add(new QuoteRatingSnapshot
        {
            QuoteId = fixture.Quote.Id,
            RatingPlanVersionId = Guid.NewGuid(),
            RatedById = fixture.UserId,
            RatedAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            GrandTotalPremium = 1000m,
        });
        await db.SaveChangesAsync();
        var quoteService = CreateQuoteService(db, new RecordingInvoicingService());

        // BindRequest() effective date (2026-01-05) differs from the rated 2026-01-01.
        var result = await quoteService.BindAsync(fixture.Quote.Id, BindRequest(), UserAccessScope.All(fixture.UserId));

        Assert.False(result.IsSuccess);
        Assert.Equal("RERATE_REQUIRED", result.ErrorCode);
    }

    [Fact]
    public async Task QuoteBind_FailsClosedWhenCommissionScheduleMissing()
    {
        await using var db = CreateDb();
        var fixture = await SeedBindableQuoteAsync(db);
        var quoteService = CreateQuoteService(db, new RecordingInvoicingService(), carrierCommissions: new MissingCarrierCommissionService());

        var result = await quoteService.BindAsync(fixture.Quote.Id, BindRequest(), UserAccessScope.All(fixture.UserId));

        Assert.False(result.IsSuccess);
        Assert.Equal("COMMISSION_SCHEDULE_MISSING", result.ErrorCode);
    }

    [Fact]
    public async Task QuoteBind_GeneratesPolicyNumberFromBindEffectiveDate()
    {
        await using var db = CreateDb();
        var fixture = await SeedBindableQuoteAsync(db);
        fixture.Quote.EffectiveDate = new DateOnly(2026, 12, 31);
        fixture.Quote.ExpirationDate = new DateOnly(2027, 12, 31);
        var sequence = new PolicyNumberSequence
        {
            Id = Guid.NewGuid(),
            Name = "Annual bind date",
            Format = "{YYYY}-{SEQ:000}",
            TermSuffixFormat = "-{TERM:00}",
            NextNumber = 88,
            ResetAnnually = true,
            LastResetYear = 2026,
            IsActive = true,
        };
        var assignment = new PolicyNumberAssignment
        {
            Id = Guid.NewGuid(),
            PolicyNumberSequenceId = sequence.Id,
            PolicyNumberSequence = sequence,
            CarrierId = fixture.Carrier.Id,
            Carrier = fixture.Carrier,
            LineOfBusiness = fixture.Quote.LineOfBusiness,
            IsActive = true,
        };
        db.AddRange(sequence, assignment);
        await db.SaveChangesAsync();
        var invoicing = new RecordingInvoicingService();
        var quoteService = CreateQuoteService(db, invoicing, policyNumbers: new PolicyNumberService(db));
        var bindRequest = new QuoteBindDto
        {
            BoundDate = new DateOnly(2027, 1, 1),
            EffectiveDate = new DateOnly(2027, 1, 1),
            ExpirationDate = new DateOnly(2028, 1, 1),
        };

        var result = await quoteService.BindAsync(fixture.Quote.Id, bindRequest, UserAccessScope.All(fixture.UserId));

        Assert.True(result.IsSuccess, $"{result.ErrorCode}: {result.ErrorMessage}");
        var policy = await db.Set<Policy>().SingleAsync();
        Assert.Equal("2027-001-01", policy.PolicyNumber);
        Assert.Equal("2027-001", policy.BasePolicyNumber);
        Assert.Equal(2027, sequence.LastResetYear);
        Assert.Equal(2, sequence.NextNumber);
        var usage = await db.Set<PolicyNumberSequenceUsage>().SingleAsync();
        Assert.Equal("2027-001-01", usage.FullPolicyNumber);
        Assert.Equal(1, usage.SequenceValue);
    }

    [Fact]
    public async Task QuoteCreate_RejectsProgramCarrierLobStatePathThatIsNotConfigured()
    {
        await using var db = CreateDb();
        var fixture = CreateQuoteFixture("Program Path Test");
        var program = new ProgramConfiguration { Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        db.AddRange(fixture.User, fixture.Carrier, fixture.Insured, fixture.Submission, program);
        await db.SaveChangesAsync();
        var quoteService = CreateQuoteService(db, new RecordingInvoicingService());

        var result = await quoteService.CreateAsync(CreateQuoteRequest(fixture, program.Id), fixture.UserId);

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_PROGRAM_SETUP_PATH", result.ErrorCode);
    }

    [Fact]
    public async Task QuoteCreate_AllowsConfiguredProgramCarrierLobStatePath()
    {
        await using var db = CreateDb();
        var fixture = CreateQuoteFixture("Program Path Test");
        var program = new ProgramConfiguration { Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        db.AddRange(fixture.User, fixture.Carrier, fixture.Insured, fixture.Submission, program);
        await db.SaveChangesAsync();
        db.Add(new ProgramCarrier
        {
            ProgramConfigurationId = program.Id,
            CarrierId = fixture.Carrier.Id,
            IsActive = true,
            EffectiveDate = new DateOnly(2026, 1, 1),
            LinesOfBusiness =
            {
                new ProgramCarrierLineOfBusiness
                {
                    LineOfBusiness = PolicyLineOfBusiness.InlandMarine,
                    IsActive = true,
                    EffectiveDate = new DateOnly(2026, 1, 1),
                    States =
                    {
                        new ProgramCarrierLobState
                        {
                            StateCode = fixture.Insured.State,
                            IsActive = true,
                            EffectiveDate = new DateOnly(2026, 1, 1)
                        }
                    }
                }
            }
        });
        await db.SaveChangesAsync();
        var quoteService = CreateQuoteService(db, new RecordingInvoicingService());

        var result = await quoteService.CreateAsync(CreateQuoteRequest(fixture, program.Id), fixture.UserId);

        Assert.True(result.IsSuccess);
        Assert.Equal(program.Id, result.Value!.ProgramId);
        Assert.Equal(fixture.Carrier.Id, result.Value.CarrierId);
        Assert.Equal(PolicyLineOfBusiness.InlandMarine, result.Value.LineOfBusiness);
    }

    [Fact]
    public async Task QuoteBind_BlocksPublishedHardControl()
    {
        await using var db = CreateDb();
        var fixture = await SeedBindableQuoteAsync(db);
        db.Add(new UnderwritingGuidelineDocument
        {
            Id = Guid.NewGuid(),
            ProgramName = "Longleaf",
            CarrierId = fixture.Carrier.Id,
            LineOfBusiness = fixture.Quote.LineOfBusiness,
            StateCode = "ALL",
            Title = "Test guideline",
            CreatedByUserId = fixture.UserId,
        });
        await db.SaveChangesAsync();
        var document = await db.Set<UnderwritingGuidelineDocument>().SingleAsync();
        db.Add(new UnderwritingGuidelineControl
        {
            GuidelineDocumentId = document.Id,
            ProgramName = document.ProgramName,
            CarrierId = fixture.Carrier.Id,
            LineOfBusiness = fixture.Quote.LineOfBusiness,
            StateCode = "ALL",
            ItemType = UnderwritingControlItemType.AppetiteRule,
            Stage = UnderwritingControlStage.Bind,
            Severity = UnderwritingControlSeverity.HardBlock,
            Status = UnderwritingControlStatus.Published,
            RuleKey = "test-hard-block",
            Label = "Test hard block",
            IsBlocking = true,
            OverrideAllowed = true,
            OverridePermission = AppPermissions.UnderwritingClearanceOverride,
            PublishedByUserId = fixture.UserId,
            PublishedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var quoteService = CreateQuoteService(db, new RecordingInvoicingService());
        var result = await quoteService.BindAsync(fixture.Quote.Id, BindRequest(), UserAccessScope.All(fixture.UserId));

        Assert.False(result.IsSuccess);
        Assert.Equal("UNDERWRITING_CONTROL_BLOCKED", result.ErrorCode);
        var enforcement = await db.Set<UnderwritingControlEnforcementResult>().SingleAsync();
        Assert.Equal(UnderwritingControlEvaluationStatus.Blocked, enforcement.Status);
    }

    [Fact]
    public async Task QuoteChecklist_ReturnsPublishedIssueAndPostBindDocumentItemsByStage()
    {
        await using var db = CreateDb();
        var fixture = await SeedBindableQuoteAsync(db);
        var document = new UnderwritingGuidelineDocument
        {
            Id = Guid.NewGuid(),
            ProgramName = "Longleaf",
            CarrierId = fixture.Carrier.Id,
            LineOfBusiness = fixture.Quote.LineOfBusiness,
            StateCode = "ALL",
            Title = "Test guideline",
            CreatedByUserId = fixture.UserId,
        };
        db.Add(document);
        db.AddRange(
            PublishedDocumentControl(document, fixture, UnderwritingControlStage.Bind, "bind-signed-app", "Signed application", 0),
            PublishedDocumentControl(document, fixture, UnderwritingControlStage.Issue, "issue-subjectivities", "Subjectivities satisfied", 1),
            PublishedDocumentControl(document, fixture, UnderwritingControlStage.PostBind, "post-bind-surplus-lines", "Surplus lines filing", 2));
        await db.SaveChangesAsync();
        var checklist = CreateQuoteChecklistService(db);

        var issueItems = await checklist.GetForQuoteAsync(fixture.Quote.Id, UserAccessScope.All(Guid.Empty), [UnderwritingControlStage.Issue, UnderwritingControlStage.PostBind]);

        Assert.True(issueItems.IsSuccess);
        Assert.NotNull(issueItems.Value);
        Assert.Equal(
            new[] { "Subjectivities satisfied", "Surplus lines filing" },
            issueItems.Value!.Select(i => i.Label).ToArray());
        Assert.All(issueItems.Value, item => Assert.True(item.Stage is UnderwritingControlStage.Issue or UnderwritingControlStage.PostBind));
        Assert.DoesNotContain(issueItems.Value, item => item.Label == "Signed application");
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
        // Align the quote's effective date with the bind date so the snapshot-locking assertion
        // isn't short-circuited by the RERATE_REQUIRED guard (WS5-R Batch 1).
        fixture.Quote.EffectiveDate = new DateOnly(2026, 1, 5);
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
        Assert.Null(latestSnapshot.PolicyTransactionId);
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
    public async Task QuoteBind_BlocksWhenClearanceHasActivePolicyOverlap()
    {
        await using var db = CreateDb();
        var fixture = await SeedBindableQuoteAsync(db);
        fixture.Submission.EffectiveDate = new DateOnly(2026, 1, 5);
        fixture.Submission.ExpirationDate = new DateOnly(2027, 1, 5);
        fixture.Submission.LinesOfBusiness = $"[\"{fixture.Quote.LineOfBusiness}\"]";
        db.Add(new Policy
        {
            Id = Guid.NewGuid(),
            PolicyNumber = "POL-OVERLAP-1",
            SubmissionId = fixture.Submission.Id,
            BoundQuoteId = Guid.NewGuid(),
            CarrierId = fixture.Carrier.Id,
            LineOfBusiness = fixture.Quote.LineOfBusiness,
            EffectiveDate = new DateOnly(2026, 1, 1),
            ExpirationDate = new DateOnly(2027, 1, 1),
            Status = PolicyStatus.Active,
            BoundDate = new DateOnly(2026, 1, 1),
        });
        await db.SaveChangesAsync();
        var invoicing = new RecordingInvoicingService();
        var quoteService = CreateQuoteService(db, invoicing);

        var result = await quoteService.BindAsync(fixture.Quote.Id, BindRequest(), UserAccessScope.All(fixture.UserId));

        Assert.False(result.IsSuccess);
        Assert.Equal("CLEARANCE_BLOCKED", result.ErrorCode);
        Assert.Empty(await db.Set<PolicyTransaction>().ToListAsync());
        Assert.Empty(invoicing.BindRequests);
    }

    [Fact]
    public async Task QuoteBind_AllowsBlockedClearanceWhenOverridden()
    {
        await using var db = CreateDb();
        var fixture = await SeedBindableQuoteAsync(db);
        fixture.Submission.EffectiveDate = new DateOnly(2026, 1, 5);
        fixture.Submission.ExpirationDate = new DateOnly(2027, 1, 5);
        fixture.Submission.LinesOfBusiness = $"[\"{fixture.Quote.LineOfBusiness}\"]";
        db.Add(new Policy
        {
            Id = Guid.NewGuid(),
            PolicyNumber = "POL-OVERLAP-1",
            SubmissionId = fixture.Submission.Id,
            BoundQuoteId = Guid.NewGuid(),
            CarrierId = fixture.Carrier.Id,
            LineOfBusiness = fixture.Quote.LineOfBusiness,
            EffectiveDate = new DateOnly(2026, 1, 1),
            ExpirationDate = new DateOnly(2027, 1, 1),
            Status = PolicyStatus.Active,
            BoundDate = new DateOnly(2026, 1, 1),
        });
        await db.SaveChangesAsync();
        var clearance = new UnderwritingClearanceService(db);
        await clearance.EvaluateSubmissionAsync(fixture.Submission.Id, fixture.UserId);
        await clearance.OverrideSubmissionAsync(fixture.Submission.Id, fixture.UserId, "Renewal replacement policy.");
        var invoicing = new RecordingInvoicingService();
        var quoteService = CreateQuoteService(db, invoicing);

        var result = await quoteService.BindAsync(fixture.Quote.Id, BindRequest(), UserAccessScope.All(fixture.UserId));

        Assert.True(result.IsSuccess, $"{result.ErrorCode}: {result.ErrorMessage}");
        Assert.Single(await db.Set<PolicyTransaction>().ToListAsync());
        Assert.Single(invoicing.BindRequests);
    }

    [Fact]
    public async Task QuoteBind_BlocksWhenRequiredReferralIsOpen()
    {
        await using var db = CreateDb();
        var fixture = await SeedBindableQuoteAsync(db);
        db.Add(new UnderwritingReferral
        {
            SubmissionId = fixture.Submission.Id,
            QuoteId = fixture.Quote.Id,
            ReferralType = "ReferralPremiumOver100k",
            Status = UnderwritingReferralStatus.Open,
            Required = true,
            Reason = "Premium over authority threshold.",
            RequestedById = fixture.UserId,
        });
        await db.SaveChangesAsync();
        var invoicing = new RecordingInvoicingService();
        var quoteService = CreateQuoteService(db, invoicing);

        var result = await quoteService.BindAsync(fixture.Quote.Id, BindRequest(), UserAccessScope.All(fixture.UserId));

        Assert.False(result.IsSuccess);
        Assert.Equal("REFERRAL_REQUIRED", result.ErrorCode);
        Assert.Empty(await db.Set<PolicyTransaction>().ToListAsync());
        Assert.Empty(invoicing.BindRequests);
    }

    [Fact]
    public async Task Invoice_CanReconcileToResultingPolicyVersion()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        long seq = 0;
        connection.CreateFunction("nextval", (string _) => ++seq);
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new SqlitePolicyLifecycleDbContext(dbOptions);
        await db.Database.EnsureCreatedAsync();
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
        Assert.Equal("TXN-INVOICE-1", result.Value.PolicyTransactionNumber);
        Assert.Equal(TransactionType.NewBusiness, result.Value.PolicyTransactionType);
        Assert.Equal(version.Id, result.Value.PolicyVersionId);
        Assert.Equal(1, result.Value.PolicyVersionNumber);
        var invoice = await db.Set<Invoice>().SingleAsync();
        Assert.Equal(transaction.Id, invoice.PolicyTransactionId);
        Assert.Equal(version.Id, invoice.PolicyVersionId);
    }

    [Fact]
    public async Task Activity_ExplainsInvoicePolicyTransactionSource()
    {
        await using var db = CreateDb();
        var fixture = await SeedBoundPolicyAsync(db);
        await SeedLedgerAccountsAsync(db);
        var account = await db.Set<LedgerAccount>().SingleAsync(a => a.InternalCode == "1200");
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
            TransactionNumber = "TXN-ACTIVITY-1",
            EffectiveDate = fixture.Policy.EffectiveDate,
            ResultingPolicyVersionId = version.Id,
            ProcessedById = fixture.UserId,
        };
        var ledgerTransactionId = Guid.NewGuid();
        var invoice = new Invoice
        {
            InvoiceNumber = "INV-ACTIVITY-1",
            PolicyTransactionId = transaction.Id,
            PolicyVersionId = version.Id,
            EffectiveDate = fixture.Policy.EffectiveDate,
            InvoiceDate = fixture.Policy.EffectiveDate,
            GrossPremium = 1000m,
            TotalAmount = 1000m,
            Status = "Posted",
            LedgerTransactionId = ledgerTransactionId,
            CreatedBy = fixture.UserId,
        };
        db.AddRange(version, transaction, invoice);
        await db.SaveChangesAsync();
        db.Add(new LedgerTransaction
        {
            TransactionId = ledgerTransactionId,
            EffectiveDate = invoice.EffectiveDate,
            AccountId = account.Id,
            Debit = invoice.TotalAmount,
            SourceType = "Invoice",
            SourceId = invoice.Id,
            Memo = "Invoice posting",
            CreatedBy = fixture.UserId,
        });
        await db.SaveChangesAsync();
        var services = new ServiceCollection()
            .AddSingleton<DbContext>(db)
            .BuildServiceProvider();
        var activity = new ActivityService(services);

        var events = await activity.GetActivityAsync(new ActivityFilterRequest(null, null, null, null), isAdmin: true);

        var evt = Assert.Single(events);
        Assert.Equal(transaction.Id, evt.SourcePolicyTransactionId);
        Assert.Equal("TXN-ACTIVITY-1", evt.SourcePolicyTransactionNumber);
        Assert.Equal(TransactionType.NewBusiness, evt.SourcePolicyTransactionType);
        Assert.Equal(version.Id, evt.SourcePolicyVersionId);
        Assert.Equal(1, evt.SourcePolicyVersionNumber);
    }

    [Fact]
    public async Task Reports_GroupInvoiceTotalsByPolicyTransaction()
    {
        await using var db = CreateDb();
        var fixture = await SeedBoundPolicyAsync(db);
        var firstTransaction = new PolicyTransaction
        {
            PolicyId = fixture.Policy.Id,
            Policy = fixture.Policy,
            TransactionType = TransactionType.NewBusiness,
            Status = PolicyTransactionStatus.Issued,
            TransactionNumber = "TXN-REPORT-1",
            EffectiveDate = fixture.Policy.EffectiveDate,
            ProcessedById = fixture.UserId,
        };
        var secondTransaction = new PolicyTransaction
        {
            PolicyId = fixture.Policy.Id,
            Policy = fixture.Policy,
            TransactionType = TransactionType.Endorsement,
            Status = PolicyTransactionStatus.Issued,
            TransactionNumber = "TXN-REPORT-2",
            EffectiveDate = fixture.Policy.EffectiveDate.AddMonths(1),
            ProcessedById = fixture.UserId,
        };
        db.AddRange(
            firstTransaction,
            secondTransaction,
            InvoiceFor("INV-REPORT-1", firstTransaction.Id, 1000m, fixture),
            InvoiceFor("INV-REPORT-2", firstTransaction.Id, 250m, fixture),
            InvoiceFor("INV-REPORT-3", secondTransaction.Id, 125m, fixture));
        await db.SaveChangesAsync();
        var services = new ServiceCollection()
            .AddSingleton<DbContext>(db)
            .BuildServiceProvider();
        var reports = new ReportService(services);

        var result = await reports.GetInvoiceTotalsByPolicyTransactionAsync();

        Assert.Equal(2, result.Rows.Count);
        var first = Assert.Single(result.Rows, r => r.PolicyTransactionId == firstTransaction.Id);
        Assert.Equal("TXN-REPORT-1", first.PolicyTransactionNumber);
        Assert.Equal(TransactionType.NewBusiness, first.PolicyTransactionType);
        Assert.Equal(1250m, first.TotalAmount);
        Assert.Equal(2, first.InvoiceCount);
        var second = Assert.Single(result.Rows, r => r.PolicyTransactionId == secondTransaction.Id);
        Assert.Equal(125m, second.TotalAmount);
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
    public async Task IssuePolicy_BlocksWhenRequiredReferralIsOpen()
    {
        await using var db = CreateDb();
        var fixture = await SeedBoundPolicyAsync(db);
        db.Add(new UnderwritingReferral
        {
            SubmissionId = fixture.Submission.Id,
            QuoteId = fixture.Quote.Id,
            ReferralType = "ReferralPremiumOver100k",
            Status = UnderwritingReferralStatus.Open,
            Required = true,
            Reason = "Premium over authority threshold.",
            RequestedById = fixture.UserId,
        });
        await db.SaveChangesAsync();
        var assembly = new RecordingPolicyAssemblyService();
        var policyService = CreatePolicyService(db, new RecordingInvoicingService(), assembly: assembly);

        var result = await policyService.IssueAsync(fixture.Policy.Id, new IssuePolicyDto
        {
            IssuedDate = new DateOnly(2026, 1, 5),
        }, UserAccessScope.All(fixture.UserId));

        Assert.False(result.IsSuccess);
        Assert.Equal("REFERRAL_REQUIRED", result.ErrorCode);
        Assert.False(assembly.WasCalled);
    }

    [Fact]
    public async Task IssuePolicy_BlocksWhenRequiredIssueDocumentsAreIncomplete()
    {
        await using var db = CreateDb();
        var fixture = await SeedBoundPolicyAsync(db);
        await SeedReadyPolicyFormsAsync(db, fixture.Quote);
        db.Add(new QuoteChecklistItem
        {
            QuoteId = fixture.Quote.Id,
            Stage = UnderwritingControlStage.Issue,
            TriggerKey = "guideline:issue-subjectivities",
            Label = "Subjectivities satisfied",
            IsBlocker = true,
            IsCompleted = false,
            SortOrder = 1,
        });
        await db.SaveChangesAsync();
        var assembly = new RecordingPolicyAssemblyService();
        var policyService = CreatePolicyService(
            db,
            new RecordingInvoicingService(),
            assembly: assembly,
            checklist: CreateQuoteChecklistService(db));

        var result = await policyService.IssueAsync(fixture.Policy.Id, new IssuePolicyDto
        {
            IssuedDate = new DateOnly(2026, 1, 5),
        }, UserAccessScope.All(fixture.UserId));

        Assert.False(result.IsSuccess);
        Assert.Equal("REQUIRED_DOCUMENTS_INCOMPLETE", result.ErrorCode);
        Assert.Contains("Subjectivities satisfied", result.ErrorMessage);
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
        Assert.Equal(transaction.Id, assembly.AssembledPolicyTransactionId);
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
    public async Task IssueEndorsement_RollsBackWhenInvoiceCreationFails()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new SqlitePolicyLifecycleDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var fixture = await SeedBoundPolicyAsync(db);
        var invoicing = new RecordingInvoicingService
        {
            BindResult = Result<InvoiceDetailDto>.Failure("INVOICE_FAILED", "Invoice could not be created.")
        };
        var policyService = CreatePolicyService(db, invoicing);
        var createResult = await policyService.AddEndorsementAsync(fixture.Policy.Id, new CreateEndorsementDto
        {
            EffectiveDate = new DateOnly(2026, 6, 1),
            PremiumChange = 125m,
            EndorsementDescription = "Add scheduled equipment",
        }, UserAccessScope.All(fixture.UserId));
        Assert.True(createResult.IsSuccess);

        var issueResult = await policyService.IssueEndorsementAsync(
            fixture.Policy.Id,
            createResult.Value!.Id,
            new IssueEndorsementDto(),
            UserAccessScope.All(fixture.UserId));

        Assert.False(issueResult.IsSuccess);
        Assert.Equal("INVOICE_FAILED", issueResult.ErrorCode);

        await using var verifyDb = new SqlitePolicyLifecycleDbContext(options);
        var transaction = await verifyDb.Set<PolicyTransaction>().SingleAsync(t => t.Id == createResult.Value.Id);
        Assert.Equal(PolicyTransactionStatus.Submitted, transaction.Status);
        Assert.Null(transaction.PriorPolicyVersionId);
        Assert.Null(transaction.ResultingPolicyVersionId);
        Assert.Equal(1000m, await verifyDb.Set<Policy>()
            .Where(p => p.Id == fixture.Policy.Id)
            .Select(p => p.TotalPremium)
            .SingleAsync());
        Assert.Equal(0, await verifyDb.Set<PolicyVersion>().CountAsync(v => v.PolicyId == fixture.Policy.Id));
        Assert.DoesNotContain(
            await verifyDb.Set<PolicyTransactionStatusHistory>()
                .Where(h => h.PolicyTransactionId == createResult.Value.Id)
                .Select(h => h.EventName)
                .ToListAsync(),
            eventName => eventName == "policy.transaction.issued");
    }

    [Fact]
    public async Task IssueEndorsement_RejectsReturnPremiumWithoutIssuing()
    {
        await using var db = CreateDb();
        var fixture = await SeedBoundPolicyAsync(db);
        var invoicing = new RecordingInvoicingService();
        var policyService = CreatePolicyService(db, invoicing);
        var createResult = await policyService.AddEndorsementAsync(fixture.Policy.Id, new CreateEndorsementDto
        {
            EffectiveDate = new DateOnly(2026, 6, 1),
            PremiumChange = -125m,
            EndorsementDescription = "Remove scheduled equipment",
        }, UserAccessScope.All(fixture.UserId));
        Assert.True(createResult.IsSuccess);

        var issueResult = await policyService.IssueEndorsementAsync(
            fixture.Policy.Id,
            createResult.Value!.Id,
            new IssueEndorsementDto(),
            UserAccessScope.All(fixture.UserId));

        Assert.False(issueResult.IsSuccess);
        Assert.Equal("RETURN_PREMIUM_ENDORSEMENT_ACCOUNTING_REQUIRED", issueResult.ErrorCode);
        Assert.Empty(invoicing.BindRequests);
        var transaction = await db.Set<PolicyTransaction>().SingleAsync(t => t.Id == createResult.Value.Id);
        Assert.Equal(PolicyTransactionStatus.Submitted, transaction.Status);
        Assert.Equal(1000m, await db.Set<Policy>()
            .Where(p => p.Id == fixture.Policy.Id)
            .Select(p => p.TotalPremium)
            .SingleAsync());
    }

    [Fact]
    public async Task IssueEndorsement_RequiresAuthorityApprovalForLargePremiumChange()
    {
        await using var db = CreateDb();
        var fixture = await SeedBoundPolicyAsync(db);
        var policyService = CreatePolicyService(db, new RecordingInvoicingService());

        var createResult = await policyService.AddEndorsementAsync(fixture.Policy.Id, new CreateEndorsementDto
        {
            EffectiveDate = new DateOnly(2026, 6, 1),
            PremiumChange = 30000m,
            EndorsementDescription = "Large scheduled equipment change",
        }, UserAccessScope.All(fixture.UserId));
        Assert.True(createResult.IsSuccess);

        var blocked = await policyService.IssueEndorsementAsync(
            fixture.Policy.Id,
            createResult.Value!.Id,
            new IssueEndorsementDto(),
            UserAccessScope.All(fixture.UserId),
            Array.Empty<string>());

        Assert.False(blocked.IsSuccess);
        Assert.Equal("AUTHORITY_APPROVAL_REQUIRED", blocked.ErrorCode);
        var approval = await db.Set<AuthorityApprovalRequest>().SingleAsync();
        Assert.Equal(AuthorityApprovalTargetType.PolicyTransaction, approval.TargetType);
        Assert.Equal(createResult.Value.Id, approval.TargetId);
        Assert.Equal("policy.endorsement.large-premium-change", approval.ActionCode);
        Assert.Equal("Large endorsement premium change", approval.ActionLabel);
        Assert.Equal("LargeEndorsementPremiumChange", approval.ApprovalType);
        Assert.Equal(AuthorityApprovalStatus.Pending, approval.Status);
        Assert.Contains("$30,000.00", approval.Reason);

        approval.Status = AuthorityApprovalStatus.Approved;
        approval.DecisionById = Guid.NewGuid();
        approval.DecisionAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var issued = await policyService.IssueEndorsementAsync(
            fixture.Policy.Id,
            createResult.Value.Id,
            new IssueEndorsementDto(),
            UserAccessScope.All(fixture.UserId),
            Array.Empty<string>());

        Assert.True(issued.IsSuccess, $"{issued.ErrorCode}: {issued.ErrorMessage}");
        Assert.Equal(PolicyTransactionStatus.Issued, issued.Value!.Status);
    }

    [Fact]
    public async Task AddEndorsement_BlocksWhenRequiredPostBindDocumentsAreIncomplete()
    {
        await using var db = CreateDb();
        var fixture = await SeedBoundPolicyAsync(db);
        db.Add(new QuoteChecklistItem
        {
            QuoteId = fixture.Quote.Id,
            Stage = UnderwritingControlStage.PostBind,
            TriggerKey = "guideline:post-bind-sl-filing",
            Label = "Surplus lines filing completed",
            IsBlocker = true,
            IsCompleted = false,
            SortOrder = 1,
        });
        await db.SaveChangesAsync();
        var policyService = CreatePolicyService(db, new RecordingInvoicingService(), checklist: CreateQuoteChecklistService(db));

        var result = await policyService.AddEndorsementAsync(fixture.Policy.Id, new CreateEndorsementDto
        {
            EffectiveDate = new DateOnly(2026, 6, 1),
            PremiumChange = 125m,
            EndorsementDescription = "Add scheduled equipment",
        }, UserAccessScope.All(fixture.UserId));

        Assert.False(result.IsSuccess);
        Assert.Equal("POST_BIND_REQUIREMENTS_INCOMPLETE", result.ErrorCode);
        Assert.Contains("Surplus lines filing completed", result.ErrorMessage);
        Assert.Empty(await db.Set<PolicyTransaction>().ToListAsync());
    }

    [Fact]
    public async Task PolicyTransactionArtifacts_ReturnsDocumentsInvoicesAndCommunications()
    {
        await using var db = CreateDb();
        var fixture = await SeedBoundPolicyAsync(db);
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
            TransactionNumber = "TXN-ARTIFACTS-1",
            EffectiveDate = fixture.Policy.EffectiveDate,
            ResultingPolicyVersionId = version.Id,
            PremiumChange = fixture.Policy.TotalPremium,
            NewTotalPremium = fixture.Policy.TotalPremium,
            ProcessedById = fixture.UserId,
        };
        var attachment = new Attachment
        {
            QuoteId = fixture.Policy.BoundQuoteId,
            EntityType = DocumentEntityType.Policy,
            DocumentType = DocumentType.IssuedPolicyPacket,
            PolicyTransactionId = transaction.Id,
            PolicyVersionId = version.Id,
            FileName = "issued.pdf",
            BlobPath = "issued.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 123,
            UploadedById = fixture.UserId,
            UploadedBy = fixture.Policy.BoundQuote.CreatedBy,
        };
        var unrelatedAttachment = new Attachment
        {
            QuoteId = fixture.Policy.BoundQuoteId,
            EntityType = DocumentEntityType.Policy,
            DocumentType = DocumentType.Other,
            FileName = "other.pdf",
            BlobPath = "other.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 123,
            UploadedById = fixture.UserId,
            UploadedBy = fixture.Policy.BoundQuote.CreatedBy,
        };
        var invoice = new Invoice
        {
            InvoiceNumber = "INV-ARTIFACTS-1",
            PolicyTransactionId = transaction.Id,
            PolicyVersionId = version.Id,
            EffectiveDate = fixture.Policy.EffectiveDate,
            InvoiceDate = fixture.Policy.EffectiveDate,
            GrossPremium = fixture.Policy.PremiumAmount,
            TotalFees = fixture.Policy.TaxesAndFees,
            TotalAmount = fixture.Policy.TotalPremium,
            LedgerTransactionId = Guid.NewGuid(),
            CreatedBy = fixture.UserId,
        };
        var communication = new OutboundCommunication
        {
            EntityType = OutboundCommunicationEntityType.Policy,
            EntityId = fixture.Policy.Id,
            PolicyTransactionId = transaction.Id,
            Purpose = OutboundCommunicationPurpose.PolicyIssue,
            ToAddress = "agent@example.com",
            FromAddress = "uw@example.com",
            SenderType = OutboundCommunicationSenderType.CurrentUser,
            Subject = "Issued policy",
            BodyHtml = "<p>Issued policy attached.</p>",
            Status = OutboundCommunicationStatus.Sent,
            GraphMessageId = "graph-message-1",
            GraphMessageWebLink = "https://graph.example/messages/1",
            CreatedById = fixture.UserId,
            CreatedBy = fixture.Policy.BoundQuote.CreatedBy,
            SentById = fixture.UserId,
            SentBy = fixture.Policy.BoundQuote.CreatedBy,
            SentAt = DateTime.UtcNow,
        };
        var unrelatedCommunication = new OutboundCommunication
        {
            EntityType = OutboundCommunicationEntityType.Policy,
            EntityId = fixture.Policy.Id,
            Purpose = OutboundCommunicationPurpose.Other,
            ToAddress = "agent@example.com",
            FromAddress = "uw@example.com",
            SenderType = OutboundCommunicationSenderType.CurrentUser,
            Subject = "Unlinked note",
            BodyHtml = "<p>Not tied to the transaction.</p>",
            Status = OutboundCommunicationStatus.Sent,
            CreatedById = fixture.UserId,
            CreatedBy = fixture.Policy.BoundQuote.CreatedBy,
        };
        db.AddRange(version, transaction, attachment, unrelatedAttachment, invoice, communication, unrelatedCommunication);
        await db.SaveChangesAsync();
        var policyService = CreatePolicyService(db, new RecordingInvoicingService());

        var result = await policyService.GetTransactionArtifactsAsync(fixture.Policy.Id, transaction.Id, UserAccessScope.All(fixture.UserId));

        Assert.True(result.IsSuccess, $"{result.ErrorCode}: {result.ErrorMessage}");
        Assert.Equal(transaction.Id, result.Value!.Transaction.Id);
        var document = Assert.Single(result.Value.Documents);
        Assert.Equal(attachment.Id, document.Id);
        Assert.Equal(transaction.Id, document.PolicyTransactionId);
        Assert.Equal(version.Id, document.PolicyVersionId);
        var invoiceDto = Assert.Single(result.Value.Invoices);
        Assert.Equal(invoice.Id, invoiceDto.Id);
        Assert.Equal(transaction.Id, invoiceDto.PolicyTransactionId);
        Assert.Equal(transaction.TransactionNumber, invoiceDto.PolicyTransactionNumber);
        Assert.Equal(transaction.TransactionType, invoiceDto.PolicyTransactionType);
        Assert.Equal(version.Id, invoiceDto.PolicyVersionId);
        var communicationDto = Assert.Single(result.Value.Communications);
        Assert.Equal(communication.Id, communicationDto.Id);
        Assert.Equal(transaction.Id, communicationDto.PolicyTransactionId);
        Assert.Equal(OutboundCommunicationPurpose.PolicyIssue, communicationDto.Purpose);
        Assert.Equal("graph-message-1", communicationDto.GraphMessageId);
        Assert.Equal("https://graph.example/messages/1", communicationDto.GraphMessageWebLink);
    }

    [Fact]
    public async Task PolicyTransactionArtifacts_ReturnsNoticeAndProofDocumentsForTransaction()
    {
        await using var db = CreateDb();
        var fixture = await SeedBoundPolicyAsync(db);
        var transaction = new PolicyTransaction
        {
            PolicyId = fixture.Policy.Id,
            Policy = fixture.Policy,
            TransactionType = TransactionType.Cancellation,
            Status = PolicyTransactionStatus.NoticeSent,
            TransactionNumber = "TXN-NOTICE-1",
            EffectiveDate = fixture.Policy.EffectiveDate.AddMonths(1),
            ProcessedById = fixture.UserId,
        };
        var notice = AttachmentFor(fixture, transaction.Id, DocumentType.CancellationNonRenewal, "notice.pdf");
        var proof = AttachmentFor(fixture, transaction.Id, DocumentType.ProofOfNotice, "proof.pdf");
        var unrelatedProof = AttachmentFor(fixture, null, DocumentType.ProofOfNotice, "unlinked-proof.pdf");
        db.AddRange(transaction, notice, proof, unrelatedProof);
        await db.SaveChangesAsync();
        var policyService = CreatePolicyService(db, new RecordingInvoicingService());

        var result = await policyService.GetTransactionArtifactsAsync(fixture.Policy.Id, transaction.Id, UserAccessScope.All(fixture.UserId));

        Assert.True(result.IsSuccess, $"{result.ErrorCode}: {result.ErrorMessage}");
        var documents = result.Value!.Documents.OrderBy(d => d.FileName).ToList();
        Assert.Equal(new[] { "notice.pdf", "proof.pdf" }, documents.Select(d => d.FileName).ToArray());
        Assert.Contains(documents, d => d.DocumentType == DocumentType.CancellationNonRenewal && d.PolicyTransactionId == transaction.Id);
        Assert.Contains(documents, d => d.DocumentType == DocumentType.ProofOfNotice && d.PolicyTransactionId == transaction.Id);
    }

    [Fact]
    public async Task Task_CanPointToPolicyTransactionAndExposeTransactionContext()
    {
        await using var db = CreateDb();
        var fixture = await SeedBoundPolicyAsync(db);
        var transaction = new PolicyTransaction
        {
            PolicyId = fixture.Policy.Id,
            Policy = fixture.Policy,
            TransactionType = TransactionType.Cancellation,
            Status = PolicyTransactionStatus.InReview,
            TransactionNumber = "TXN-TASK-1",
            EffectiveDate = fixture.Policy.EffectiveDate.AddMonths(1),
            ProcessedById = fixture.UserId,
        };
        var taskType = new TaskType { Name = "Review transaction", DefaultPriority = TaskPriority.High };
        var task = new TaskInstance
        {
            TaskType = taskType,
            TaskTypeId = taskType.Id,
            WorkflowStepId = Guid.NewGuid(),
            EntityType = TaskEntityType.PolicyTransaction,
            EntityId = transaction.Id,
            AssignedUserId = fixture.UserId,
            Status = TaskInstanceStatus.Open,
            Priority = TaskPriority.High,
            DueDate = DateTime.UtcNow.AddDays(1),
        };
        db.AddRange(transaction, taskType, task);
        await db.SaveChangesAsync();
        var workflow = new RecordingWorkflowEngineService();
        var services = new ServiceCollection()
            .AddSingleton<DbContext>(db)
            .BuildServiceProvider();
        var tasks = new TaskInstanceService(services, workflow);

        var listItem = Assert.Single(await tasks.GetByEntityAsync(TaskEntityType.PolicyTransaction, transaction.Id));
        Assert.Equal(TaskEntityType.PolicyTransaction, listItem.EntityType);
        Assert.Equal("TXN-TASK-1", listItem.PolicyTransactionNumber);
        Assert.Equal(TransactionType.Cancellation, listItem.PolicyTransactionType);
        Assert.Equal(PolicyTransactionStatus.InReview, listItem.PolicyTransactionStatus);

        var detail = await tasks.UpdateStatusAsync(task.Id, TaskInstanceStatus.Closed, fixture.UserId, "Approved to continue.");

        Assert.True(detail.IsSuccess, $"{detail.ErrorCode}: {detail.ErrorMessage}");
        Assert.Equal("TXN-TASK-1", detail.Value!.PolicyTransactionNumber);
        var workflowCall = Assert.Single(workflow.StepCompleted);
        Assert.Equal(TaskEntityType.PolicyTransaction, workflowCall.EntityType);
        Assert.Equal(transaction.Id, workflowCall.EntityId);
        Assert.Equal("TXN-TASK-1", workflowCall.Context["PolicyTransactionNumber"]);
        Assert.Equal("Cancellation", workflowCall.Context["PolicyTransactionType"]);
        Assert.Equal("InReview", workflowCall.Context["PolicyTransactionStatus"]);
    }

    [Fact]
    public async Task PolicyTransactionArtifacts_ReturnsApprovalHistory()
    {
        await using var db = CreateDb();
        var fixture = await SeedBoundPolicyAsync(db);
        var transaction = new PolicyTransaction
        {
            PolicyId = fixture.Policy.Id,
            Policy = fixture.Policy,
            TransactionType = TransactionType.Endorsement,
            Status = PolicyTransactionStatus.Approved,
            TransactionNumber = "TXN-APPROVAL-1",
            EffectiveDate = fixture.Policy.EffectiveDate.AddMonths(1),
            ProcessedById = fixture.UserId,
        };
        var approval = new PolicyTransactionApproval
        {
            PolicyTransactionId = transaction.Id,
            PolicyTransaction = transaction,
            ApprovalType = "LargePremiumEndorsement",
            RequestedById = fixture.UserId,
            RequestedAt = DateTime.UtcNow.AddHours(-2),
            DecisionById = fixture.UserId,
            DecisionAt = DateTime.UtcNow.AddHours(-1),
            Decision = "Approved",
            Notes = "Within program appetite.",
        };
        var taskType = new TaskType { Name = "Approval follow-up" };
        var task = new TaskInstance
        {
            TaskType = taskType,
            TaskTypeId = taskType.Id,
            EntityType = TaskEntityType.PolicyTransaction,
            EntityId = transaction.Id,
            AssignedUserId = fixture.UserId,
            Status = TaskInstanceStatus.Open,
            Priority = TaskPriority.Medium,
            DueDate = DateTime.UtcNow.AddDays(1),
        };
        db.AddRange(transaction, approval, taskType, task);
        await db.SaveChangesAsync();
        var policyService = CreatePolicyService(db, new RecordingInvoicingService());

        var result = await policyService.GetTransactionArtifactsAsync(fixture.Policy.Id, transaction.Id, UserAccessScope.All(fixture.UserId));

        Assert.True(result.IsSuccess, $"{result.ErrorCode}: {result.ErrorMessage}");
        var approvalDto = Assert.Single(result.Value!.Approvals);
        Assert.Equal("LargePremiumEndorsement", approvalDto.ApprovalType);
        Assert.Equal("Approved", approvalDto.Decision);
        Assert.Equal(fixture.UserId, approvalDto.RequestedById);
        Assert.Equal(fixture.UserId, approvalDto.DecisionById);
        Assert.Equal("Within program appetite.", approvalDto.Notes);
        var taskDto = Assert.Single(result.Value.Tasks);
        Assert.Equal(task.Id, taskDto.Id);
        Assert.Equal(TaskEntityType.PolicyTransaction, taskDto.EntityType);
        Assert.Equal("TXN-APPROVAL-1", taskDto.PolicyTransactionNumber);
    }

    [Fact]
    public async Task PolicyTransactionArtifacts_ReturnsLinkedRatingSnapshot()
    {
        await using var db = CreateDb();
        var fixture = await SeedBoundPolicyAsync(db);
        var transaction = new PolicyTransaction
        {
            PolicyId = fixture.Policy.Id,
            Policy = fixture.Policy,
            TransactionType = TransactionType.Endorsement,
            Status = PolicyTransactionStatus.Quoted,
            TransactionNumber = "TXN-RATING-1",
            EffectiveDate = fixture.Policy.EffectiveDate.AddMonths(1),
            ProcessedById = fixture.UserId,
        };
        var ratingPlan = new RatingPlan
        {
            Id = Guid.NewGuid(),
            Name = "IM Test Plan",
            LineOfBusiness = PolicyLineOfBusiness.InlandMarine,
        };
        var ratingVersion = new RatingPlanVersion
        {
            Id = Guid.NewGuid(),
            RatingPlanId = ratingPlan.Id,
            RatingPlan = ratingPlan,
            VersionNumber = 1,
            EffectiveDate = fixture.Policy.EffectiveDate,
            Status = PlanStatus.Active,
            ScheduleMin = 0.85m,
            ScheduleMax = 1.15m,
            MinimumPremium = 500m,
        };
        var snapshot = new QuoteRatingSnapshot
        {
            QuoteId = fixture.Policy.BoundQuoteId,
            Quote = fixture.Policy.BoundQuote,
            PolicyTransactionId = transaction.Id,
            RatingPlanVersionId = ratingVersion.Id,
            RatingPlanVersion = ratingVersion,
            RatedById = fixture.UserId,
            RatedBy = fixture.Policy.BoundQuote.CreatedBy,
            RatedAt = new DateTime(2026, 2, 1, 12, 0, 0, DateTimeKind.Utc),
            ManualPremium = 900m,
            ScheduleModifier = 1.05m,
            GrandTotalPremium = 945m,
        };
        db.AddRange(transaction, ratingPlan, ratingVersion, snapshot);
        await db.SaveChangesAsync();
        var policyService = CreatePolicyService(db, new RecordingInvoicingService());

        var result = await policyService.GetTransactionArtifactsAsync(fixture.Policy.Id, transaction.Id, UserAccessScope.All(fixture.UserId));

        Assert.True(result.IsSuccess, $"{result.ErrorCode}: {result.ErrorMessage}");
        var rating = Assert.Single(result.Value!.RatingSnapshots);
        Assert.Equal(snapshot.Id, rating.SnapshotId);
        Assert.Equal(transaction.Id, rating.PolicyTransactionId);
        Assert.Equal(945m, rating.GrandTotalPremium);
    }

    [Fact]
    public async Task OutboundCommunications_CanBeFilteredByPolicyTransaction()
    {
        await using var db = CreateDb();
        var fixture = await SeedBoundPolicyAsync(db);
        var firstTransaction = new PolicyTransaction
        {
            PolicyId = fixture.Policy.Id,
            Policy = fixture.Policy,
            TransactionType = TransactionType.NewBusiness,
            Status = PolicyTransactionStatus.Issued,
            TransactionNumber = "TXN-COMM-1",
            EffectiveDate = fixture.Policy.EffectiveDate,
            ProcessedById = fixture.UserId,
        };
        var secondTransaction = new PolicyTransaction
        {
            PolicyId = fixture.Policy.Id,
            Policy = fixture.Policy,
            TransactionType = TransactionType.Cancellation,
            Status = PolicyTransactionStatus.Submitted,
            TransactionNumber = "TXN-COMM-2",
            EffectiveDate = fixture.Policy.EffectiveDate.AddMonths(1),
            ProcessedById = fixture.UserId,
        };
        db.AddRange(
            firstTransaction,
            secondTransaction,
            CommunicationFor(fixture, firstTransaction.Id, OutboundCommunicationPurpose.PolicyIssue, "Policy issue notice"),
            CommunicationFor(fixture, secondTransaction.Id, OutboundCommunicationPurpose.CancellationNotice, "Cancellation notice"));
        await db.SaveChangesAsync();
        var service = new OutboundCommunicationService(db, new DocumentMergeService(), new RecordingOutboundEmailSender());

        var allPolicyCommunications = (await service.GetForEntityAsync(OutboundCommunicationEntityType.Policy, fixture.Policy.Id)).ToList();
        var filteredCommunications = (await service.GetForEntityAsync(OutboundCommunicationEntityType.Policy, fixture.Policy.Id, secondTransaction.Id)).ToList();

        Assert.Equal(2, allPolicyCommunications.Count);
        var filtered = Assert.Single(filteredCommunications);
        Assert.Equal(secondTransaction.Id, filtered.PolicyTransactionId);
        Assert.Equal(OutboundCommunicationPurpose.CancellationNotice, filtered.Purpose);
        Assert.Equal("Cancellation notice", filtered.Subject);
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
        var checklist = await db.Set<PolicyTransactionComplianceChecklist>()
            .Include(c => c.Items)
            .SingleAsync(c => c.PolicyTransactionId == transaction.Id);
        Assert.Equal("Cancellation", checklist.Purpose);
        var checklistItem = Assert.Single(checklist.Items);
        Assert.Equal("notice", checklistItem.Key);
        Assert.Equal("Notice sent", checklistItem.Label);
        Assert.True(checklistItem.IsCompleted);
        Assert.Equal(requirement.Id, checklistItem.LegalRequirementSectionId);
        Assert.Equal(fixture.UserId, checklistItem.CompletedById);
        Assert.NotNull(checklistItem.CompletedAt);
        Assert.Contains("Send written notice before cancellation.", checklistItem.SnapshotJson);

        requirement.RequirementText = "Updated requirement after the transaction.";
        await db.SaveChangesAsync();
        var artifacts = await policyService.GetTransactionArtifactsAsync(fixture.Policy.Id, transaction.Id, UserAccessScope.All(fixture.UserId));
        Assert.True(artifacts.IsSuccess);
        Assert.NotNull(artifacts.Value);
        var artifactChecklist = Assert.Single(artifacts.Value.ComplianceChecklists);
        var artifactItem = Assert.Single(artifactChecklist.Items);
        Assert.Equal("notice", artifactItem.Key);
        Assert.Contains("Send written notice before cancellation.", artifactItem.SnapshotJson);
        Assert.DoesNotContain("Updated requirement after the transaction.", artifactItem.SnapshotJson);

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
    public async Task CancellationNotice_CreatesPendingTransactionDetailWithoutCancellingPolicy()
    {
        await using var db = CreateDb();
        var fixture = await SeedBoundPolicyAsync(db);
        var policyService = CreatePolicyService(db, new RecordingInvoicingService());

        var result = await policyService.IssueCancellationNoticeAsync(fixture.Policy.Id, new IssueCancellationNoticeDto
        {
            ReasonCode = "NP-01",
            ReasonInputs = new Dictionary<string, string>
            {
                ["AMOUNT_DUE"] = "1,250.00",
            },
            NoticeMailingDate = new DateOnly(2026, 6, 1),
            NoticeRequirementDays = 10,
            MailingDays = 5,
            Method = "Certified Mail",
            Notes = "Manual notice test",
        }, UserAccessScope.All(fixture.UserId));

        Assert.True(result.IsSuccess, $"{result.ErrorCode}: {result.ErrorMessage}");
        Assert.Equal(PolicyStatus.Active, fixture.Policy.Status);
        var transaction = await db.Set<PolicyTransaction>().SingleAsync(t => t.TransactionType == TransactionType.Cancellation);
        Assert.Equal(PolicyTransactionStatus.NoticeSent, transaction.Status);
        Assert.Equal(new DateOnly(2026, 6, 16), transaction.EffectiveDate);

        var detail = await db.Set<PolicyCancellationDetail>().SingleAsync(d => d.PolicyTransactionId == transaction.Id);
        Assert.Equal("NP-01", detail.ReasonCode);
        Assert.Equal("Non-Payment - Standard", detail.ReasonLabel);
        Assert.Equal(new DateOnly(2026, 6, 1), detail.NoticeMailingDate);
        Assert.Equal(10, detail.NoticeRequirementDays);
        Assert.Equal(5, detail.MailingDays);
        Assert.Equal(new DateOnly(2026, 6, 16), detail.CancellationEffectiveDate);
        Assert.Equal("Certified Mail", detail.Method);
        Assert.Contains("$1,250.00", detail.ResolvedReasonLanguage);
        Assert.Contains("AMOUNT_DUE", detail.ReasonInputsJson);
        Assert.Null(fixture.Policy.CancelledDate);
    }

    [Fact]
    public async Task CancellationNotice_DetailIsReturnedWithPolicyTransaction()
    {
        await using var db = CreateDb();
        var fixture = await SeedBoundPolicyAsync(db);
        var template = new DocumentTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Commercial Cancellation Notice - Sample",
            EntityType = TemplateEntityType.Policy,
            Kind = DocumentTemplateKind.Document,
            HtmlContent = "<p>{{cancellation.reasonLanguageResolved}}</p>",
            CreatedById = fixture.UserId,
            CreatedBy = fixture.Policy.BoundQuote.CreatedBy,
            IsActive = true,
        };
        db.Add(template);
        await db.SaveChangesAsync();
        var policyService = CreatePolicyService(db, new RecordingInvoicingService());

        var result = await policyService.IssueCancellationNoticeAsync(fixture.Policy.Id, new IssueCancellationNoticeDto
        {
            ReasonCode = "NP-01",
            ReasonInputs = new Dictionary<string, string>
            {
                ["AMOUNT_DUE"] = "1,250.00",
            },
            NoticeMailingDate = new DateOnly(2026, 6, 1),
            NoticeRequirementDays = 10,
            MailingDays = 3,
            Method = "Certified Mail",
            NoticeTemplateId = template.Id,
        }, UserAccessScope.All(fixture.UserId));
        Assert.True(result.IsSuccess);

        var policyResult = await policyService.GetByIdAsync(fixture.Policy.Id, UserAccessScope.All(fixture.UserId));

        Assert.True(policyResult.IsSuccess);
        var transaction = Assert.Single(policyResult.Value!.Transactions, t => t.Id == result.Value!.Id);
        Assert.NotNull(transaction.CancellationDetail);
        Assert.Equal("NP-01", transaction.CancellationDetail!.ReasonCode);
        Assert.Equal("Non-Payment - Standard", transaction.CancellationDetail.ReasonLabel);
        Assert.Contains("$1,250.00", transaction.CancellationDetail.ResolvedReasonLanguage);
        Assert.Equal(new DateOnly(2026, 6, 1), transaction.CancellationDetail.NoticeMailingDate);
        Assert.Equal(10, transaction.CancellationDetail.NoticeRequirementDays);
        Assert.Equal(3, transaction.CancellationDetail.MailingDays);
        Assert.Equal(new DateOnly(2026, 6, 14), transaction.CancellationDetail.CancellationEffectiveDate);
        Assert.Equal("Certified Mail", transaction.CancellationDetail.Method);
        Assert.Equal(template.Id, transaction.CancellationDetail.NoticeTemplateId);
        Assert.Equal("Commercial Cancellation Notice - Sample", transaction.CancellationDetail.NoticeTemplateName);
    }

    [Fact]
    public async Task CancellationNotice_GeneratesNoticeDocumentForPolicyTransaction()
    {
        await using var db = CreateDb();
        var fixture = await SeedBoundPolicyAsync(db);
        var template = new DocumentTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Commercial Cancellation Notice - Sample",
            EntityType = TemplateEntityType.Policy,
            Kind = DocumentTemplateKind.Document,
            HtmlContent = "<p>{{cancellation.reasonLanguageResolved}}</p>",
            CreatedById = fixture.UserId,
            CreatedBy = fixture.Policy.BoundQuote.CreatedBy,
            IsActive = true,
        };
        db.Add(template);
        await db.SaveChangesAsync();
        var documents = new RecordingDocumentGenerationService(db);
        var policyService = CreatePolicyService(db, new RecordingInvoicingService(), documentGeneration: documents);

        var result = await policyService.IssueCancellationNoticeAsync(fixture.Policy.Id, new IssueCancellationNoticeDto
        {
            ReasonCode = "UW-02",
            ReasonInputs = new Dictionary<string, string>
            {
                ["DESCRIBE_CONDITIONS"] = "unacceptable housekeeping",
            },
            NoticeMailingDate = new DateOnly(2026, 6, 1),
            NoticeRequirementDays = 20,
            MailingDays = 2,
            Method = "First-Class Mail",
            NoticeTemplateId = template.Id,
        }, UserAccessScope.All(fixture.UserId));

        Assert.True(result.IsSuccess, $"{result.ErrorCode}: {result.ErrorMessage}");
        var transaction = await db.Set<PolicyTransaction>().SingleAsync(t => t.TransactionType == TransactionType.Cancellation);
        Assert.Equal(template.Id, documents.GeneratedTemplateId);
        Assert.Equal(fixture.Policy.Id, documents.GeneratedPolicyId);
        Assert.Equal(transaction.Id, documents.GeneratedPolicyTransactionId);
        var notice = await db.Set<Attachment>().SingleAsync(a => a.PolicyTransactionId == transaction.Id);
        Assert.Equal(DocumentType.CancellationNonRenewal, notice.DocumentType);
    }

    [Fact]
    public async Task CompleteCancellation_BeforeEffectiveDateFails()
    {
        await using var db = CreateDb();
        var fixture = await SeedBoundPolicyAsync(db);
        var policyService = CreatePolicyService(db, new RecordingInvoicingService());
        var notice = await policyService.IssueCancellationNoticeAsync(fixture.Policy.Id, new IssueCancellationNoticeDto
        {
            ReasonCode = "NP-01",
            ReasonInputs = new Dictionary<string, string>
            {
                ["AMOUNT_DUE"] = "500.00",
            },
            NoticeMailingDate = new DateOnly(2026, 6, 1),
            NoticeRequirementDays = 10,
            MailingDays = 0,
            Method = "Certified Mail",
        }, UserAccessScope.All(fixture.UserId));
        Assert.True(notice.IsSuccess);

        var result = await policyService.CompleteCancellationAsync(
            fixture.Policy.Id,
            notice.Value!.Id,
            new CompleteCancellationDto { CompletedDate = new DateOnly(2026, 6, 10) },
            UserAccessScope.All(fixture.UserId));

        Assert.False(result.IsSuccess);
        Assert.Equal("CANCELLATION_NOT_EFFECTIVE", result.ErrorCode);
        Assert.Equal(PolicyStatus.Active, fixture.Policy.Status);
    }

    [Fact]
    public async Task CompleteCancellation_OnEffectiveDateCancelsPolicyAndCreatesVersion()
    {
        await using var db = CreateDb();
        var fixture = await SeedBoundPolicyAsync(db);
        var policyService = CreatePolicyService(db, new RecordingInvoicingService());
        var notice = await policyService.IssueCancellationNoticeAsync(fixture.Policy.Id, new IssueCancellationNoticeDto
        {
            ReasonCode = "NP-01",
            ReasonInputs = new Dictionary<string, string>
            {
                ["AMOUNT_DUE"] = "500.00",
            },
            NoticeMailingDate = new DateOnly(2026, 6, 1),
            NoticeRequirementDays = 10,
            MailingDays = 0,
            Method = "Certified Mail",
        }, UserAccessScope.All(fixture.UserId));
        Assert.True(notice.IsSuccess);

        var result = await policyService.CompleteCancellationAsync(
            fixture.Policy.Id,
            notice.Value!.Id,
            new CompleteCancellationDto { CompletedDate = new DateOnly(2026, 6, 11) },
            UserAccessScope.All(fixture.UserId));

        Assert.True(result.IsSuccess, $"{result.ErrorCode}: {result.ErrorMessage}");
        Assert.Equal(PolicyStatus.Cancelled, fixture.Policy.Status);
        Assert.Equal(new DateOnly(2026, 6, 11), fixture.Policy.CancelledDate);
        var transaction = await db.Set<PolicyTransaction>().SingleAsync(t => t.Id == notice.Value.Id);
        Assert.Equal(PolicyTransactionStatus.Completed, transaction.Status);
        Assert.NotNull(transaction.PriorPolicyVersionId);
        Assert.NotNull(transaction.ResultingPolicyVersionId);
        var versions = await db.Set<PolicyVersion>()
            .Where(v => v.PolicyId == fixture.Policy.Id)
            .OrderBy(v => v.VersionNumber)
            .ToListAsync();
        Assert.Equal(2, versions.Count);
        Assert.Equal(PolicyStatus.Active, versions[0].Status);
        Assert.Equal(PolicyStatus.Cancelled, versions[1].Status);
        Assert.Equal(versions[0].Id, transaction.PriorPolicyVersionId);
        Assert.Equal(versions[1].Id, transaction.ResultingPolicyVersionId);
    }

    [Fact]
    public async Task Reinstatement_ActivePolicyFails()
    {
        await using var db = CreateDb();
        var fixture = await SeedBoundPolicyAsync(db);
        var policyService = CreatePolicyService(db, new RecordingInvoicingService());

        var result = await policyService.ReinstateAsync(fixture.Policy.Id, new ReinstatePolicyDto
        {
            ReinstatedDate = new DateOnly(2026, 7, 15),
            Reason = "Payment received",
        }, UserAccessScope.All(fixture.UserId));

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_STATUS", result.ErrorCode);
        Assert.Equal(PolicyStatus.Active, fixture.Policy.Status);
        Assert.Empty(await db.Set<PolicyTransaction>().Where(t => t.TransactionType == TransactionType.Reinstatement).ToListAsync());
    }

    [Fact]
    public async Task Reinstatement_CancelledPolicyRestoresActiveAndCreatesVersion()
    {
        await using var db = CreateDb();
        var fixture = await SeedBoundPolicyAsync(db);
        var policyService = CreatePolicyService(db, new RecordingInvoicingService());
        var cancellation = await policyService.CancelAsync(fixture.Policy.Id, new CancelPolicyDto
        {
            CancelledDate = new DateOnly(2026, 7, 1),
            Reason = "Non-payment",
            Method = "Certified Mail",
            PremiumChange = 0m,
            ComplianceChecklist =
            [
                new CancellationComplianceChecklistItemDto
                {
                    Key = "notice",
                    Label = "Notice sent",
                    IsCompleted = true,
                }
            ],
        }, UserAccessScope.All(fixture.UserId));
        Assert.True(cancellation.IsSuccess);

        var result = await policyService.ReinstateAsync(fixture.Policy.Id, new ReinstatePolicyDto
        {
            ReinstatedDate = new DateOnly(2026, 7, 15),
            Reason = "Payment received",
            Notes = "Producer confirmed payment.",
        }, UserAccessScope.All(fixture.UserId), [AppPermissions.UnderwritingAuthorityApprove]);

        Assert.True(result.IsSuccess, $"{result.ErrorCode}: {result.ErrorMessage}");
        Assert.Equal(PolicyStatus.Active, fixture.Policy.Status);
        Assert.Null(fixture.Policy.CancelledDate);
        var transaction = await db.Set<PolicyTransaction>().SingleAsync(t => t.TransactionType == TransactionType.Reinstatement);
        Assert.Equal(PolicyTransactionStatus.Issued, transaction.Status);
        Assert.Equal(new DateOnly(2026, 7, 15), transaction.EffectiveDate);
        Assert.Equal("Payment received", transaction.ReasonText);
        Assert.Equal(fixture.Policy.TotalPremium, transaction.PremiumBefore);
        Assert.Equal(0m, transaction.PremiumChange);
        Assert.Equal(fixture.Policy.TotalPremium, transaction.NewTotalPremium);
        var detail = await db.Set<PolicyReinstatementDetail>().SingleAsync(d => d.PolicyTransactionId == transaction.Id);
        Assert.Equal(new DateOnly(2026, 7, 15), detail.ReinstatementEffectiveDate);
        Assert.Equal("Payment received", detail.Reason);
        Assert.Equal("Producer confirmed payment.", detail.Notes);
        Assert.NotNull(transaction.PriorPolicyVersionId);
        Assert.NotNull(transaction.ResultingPolicyVersionId);
        var versions = await db.Set<PolicyVersion>()
            .Where(v => v.PolicyId == fixture.Policy.Id)
            .OrderBy(v => v.VersionNumber)
            .ToListAsync();
        Assert.Equal(3, versions.Count);
        Assert.Equal(PolicyStatus.Active, versions[0].Status);
        Assert.Equal(PolicyStatus.Cancelled, versions[1].Status);
        Assert.Equal(PolicyStatus.Active, versions[2].Status);
        Assert.Equal(versions[1].Id, transaction.PriorPolicyVersionId);
        Assert.Equal(versions[2].Id, transaction.ResultingPolicyVersionId);
        var history = await db.Set<PolicyTransactionStatusHistory>()
            .Where(h => h.PolicyTransactionId == transaction.Id)
            .OrderBy(h => h.ChangedAt)
            .ToListAsync();
        Assert.Equal(
            new[] { "policy.transaction.created", "policy.transaction.issued" },
            history.Select(h => h.EventName).ToArray());
        var confirmation = AttachmentFor(fixture, transaction.Id, DocumentType.ReinstatementApproval, "carrier-confirmation.pdf");
        db.Add(confirmation);
        await db.SaveChangesAsync();
        var artifacts = await policyService.GetTransactionArtifactsAsync(fixture.Policy.Id, transaction.Id, UserAccessScope.All(fixture.UserId));
        Assert.True(artifacts.IsSuccess);
        Assert.Equal(new DateOnly(2026, 7, 15), artifacts.Value!.Transaction.ReinstatementDetail?.ReinstatementEffectiveDate);
        Assert.Equal("Payment received", artifacts.Value.Transaction.ReinstatementDetail?.Reason);
        var document = Assert.Single(artifacts.Value.Documents);
        Assert.Equal(DocumentType.ReinstatementApproval, document.DocumentType);
        Assert.Equal(transaction.Id, document.PolicyTransactionId);
    }

    [Fact]
    public async Task Reinstatement_RequiresAuthorityApproval()
    {
        await using var db = CreateDb();
        var fixture = await SeedBoundPolicyAsync(db);
        var policyService = CreatePolicyService(db, new RecordingInvoicingService());
        var cancellation = await policyService.CancelAsync(fixture.Policy.Id, new CancelPolicyDto
        {
            CancelledDate = new DateOnly(2026, 7, 1),
            Reason = "Non-payment",
            Method = "Certified Mail",
            PremiumChange = 0m,
            ComplianceChecklist =
            [
                new CancellationComplianceChecklistItemDto
                {
                    Key = "notice",
                    Label = "Notice sent",
                    IsCompleted = true,
                }
            ],
        }, UserAccessScope.All(fixture.UserId));
        Assert.True(cancellation.IsSuccess);

        var blocked = await policyService.ReinstateAsync(fixture.Policy.Id, new ReinstatePolicyDto
        {
            ReinstatedDate = new DateOnly(2026, 7, 15),
            Reason = "Payment received",
        }, UserAccessScope.All(fixture.UserId));

        Assert.False(blocked.IsSuccess);
        Assert.Equal("AUTHORITY_APPROVAL_REQUIRED", blocked.ErrorCode);
        Assert.Equal(PolicyStatus.Cancelled, fixture.Policy.Status);
        var approval = await db.Set<AuthorityApprovalRequest>().SingleAsync();
        Assert.Equal(AuthorityApprovalTargetType.Policy, approval.TargetType);
        Assert.Equal(fixture.Policy.Id, approval.TargetId);
        Assert.Equal("policy.reinstatement", approval.ActionCode);
        Assert.Equal("Policy reinstatement", approval.ActionLabel);
        Assert.Equal("PolicyReinstatement", approval.ApprovalType);
        Assert.Equal(AuthorityApprovalStatus.Pending, approval.Status);
        Assert.Contains("reinstatement", approval.Reason, StringComparison.OrdinalIgnoreCase);

        approval.Status = AuthorityApprovalStatus.Approved;
        approval.DecisionById = Guid.NewGuid();
        approval.DecisionAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var reinstated = await policyService.ReinstateAsync(fixture.Policy.Id, new ReinstatePolicyDto
        {
            ReinstatedDate = new DateOnly(2026, 7, 15),
            Reason = "Payment received",
        }, UserAccessScope.All(fixture.UserId));

        Assert.True(reinstated.IsSuccess, $"{reinstated.ErrorCode}: {reinstated.ErrorMessage}");
        Assert.Equal(PolicyStatus.Active, fixture.Policy.Status);
    }

    [Fact]
    public async Task Reinstatement_PendingReinstatementBlocksDuplicate()
    {
        await using var db = CreateDb();
        var fixture = await SeedBoundPolicyAsync(db);
        var policyService = CreatePolicyService(db, new RecordingInvoicingService());
        var cancellation = await policyService.CancelAsync(fixture.Policy.Id, new CancelPolicyDto
        {
            CancelledDate = new DateOnly(2026, 7, 1),
            Reason = "Non-payment",
            Method = "Certified Mail",
            PremiumChange = 0m,
            ComplianceChecklist =
            [
                new CancellationComplianceChecklistItemDto
                {
                    Key = "notice",
                    Label = "Notice sent",
                    IsCompleted = true,
                }
            ],
        }, UserAccessScope.All(fixture.UserId));
        Assert.True(cancellation.IsSuccess);
        db.Set<PolicyTransaction>().Add(new PolicyTransaction
        {
            PolicyId = fixture.Policy.Id,
            Policy = fixture.Policy,
            TransactionType = TransactionType.Reinstatement,
            Status = PolicyTransactionStatus.Submitted,
            TransactionNumber = "TXN-REINSTATE-PENDING",
            EffectiveDate = new DateOnly(2026, 7, 15),
            RequestedById = fixture.UserId,
            RequestedAt = DateTime.UtcNow,
            ProcessedById = fixture.UserId,
            ProcessedAt = DateTime.UtcNow,
            ReasonText = "Pending reinstatement",
            PremiumChange = 0m,
            NewTotalPremium = fixture.Policy.TotalPremium,
        });
        await db.SaveChangesAsync();

        var result = await policyService.ReinstateAsync(fixture.Policy.Id, new ReinstatePolicyDto
        {
            ReinstatedDate = new DateOnly(2026, 7, 15),
            Reason = "Payment received",
        }, UserAccessScope.All(fixture.UserId));

        Assert.False(result.IsSuccess);
        Assert.Equal("REINSTATEMENT_ALREADY_EXISTS", result.ErrorCode);
        Assert.Equal(PolicyStatus.Cancelled, fixture.Policy.Status);
        Assert.Equal(1, await db.Set<PolicyTransaction>().CountAsync(t => t.TransactionType == TransactionType.Reinstatement));
    }

    [Fact]
    public async Task Rewrite_ActivePolicyCreatesReplacementQuoteAndSubmittedTransaction()
    {
        await using var db = CreateDb();
        var fixture = await SeedBoundPolicyAsync(db);
        var quotes = new RecordingQuoteService(db);
        var policyService = CreatePolicyService(db, new RecordingInvoicingService(), quoteService: quotes);

        var result = await policyService.StartRewriteAsync(fixture.Policy.Id, new StartRewritePolicyDto
        {
            EffectiveDate = new DateOnly(2026, 8, 1),
            Reason = "Carrier requested new paper",
            Notes = "Move to replacement policy form set.",
        }, UserAccessScope.All(fixture.UserId));

        Assert.True(result.IsSuccess, $"{result.ErrorCode}: {result.ErrorMessage}");
        Assert.Equal(TransactionType.Rewrite, result.Value!.TransactionType);
        Assert.Equal(PolicyTransactionStatus.Submitted, result.Value.Status);
        Assert.Equal(fixture.Policy.Id, result.Value.PriorPolicyId);
        Assert.NotNull(result.Value.PriorPolicyVersionId);
        Assert.NotNull(result.Value.RenewalQuoteId);
        Assert.Single(quotes.CreateRequests);
        var quote = await db.Set<Quote>().SingleAsync(q => q.Id == result.Value.RenewalQuoteId);
        Assert.Equal(QuoteStatus.Draft, quote.Status);
        Assert.Equal(fixture.Policy.SubmissionId, quote.SubmissionId);
        Assert.Equal(fixture.Policy.CarrierId, quote.CarrierId);
        Assert.Equal(new DateOnly(2026, 8, 1), quote.EffectiveDate);
        Assert.Equal(fixture.Policy.ExpirationDate, quote.ExpirationDate);
        var detail = await db.Set<PolicyRewriteDetail>().SingleAsync(d => d.PolicyTransactionId == result.Value.Id);
        Assert.Equal(fixture.Policy.Id, detail.SourcePolicyId);
        Assert.Equal(result.Value.PriorPolicyVersionId, detail.SourcePolicyVersionId);
        Assert.Equal(quote.Id, detail.ReplacementQuoteId);
        Assert.Equal("Carrier requested new paper", detail.Reason);
        Assert.Equal("Move to replacement policy form set.", detail.Notes);
        Assert.Equal(PolicyStatus.Active, fixture.Policy.Status);
    }

    [Fact]
    public async Task CompleteRewrite_BoundReplacementPolicySupersedesSourcePolicy()
    {
        await using var db = CreateDb();
        var fixture = await SeedBoundPolicyAsync(db);
        var quotes = new RecordingQuoteService(db);
        var policyService = CreatePolicyService(db, new RecordingInvoicingService(), quoteService: quotes);
        var rewrite = await policyService.StartRewriteAsync(fixture.Policy.Id, new StartRewritePolicyDto
        {
            EffectiveDate = new DateOnly(2026, 8, 1),
            Reason = "Carrier requested new paper",
        }, UserAccessScope.All(fixture.UserId));
        Assert.True(rewrite.IsSuccess);
        var rewriteTransaction = await db.Set<PolicyTransaction>().SingleAsync(t => t.TransactionType == TransactionType.Rewrite);
        var replacementQuote = await db.Set<Quote>().SingleAsync(q => q.Id == rewriteTransaction.RenewalQuoteId);
        replacementQuote.Status = QuoteStatus.Bound;
        replacementQuote.PolicyNumber = "POL-REWRITE-1";
        replacementQuote.BoundDate = new DateOnly(2026, 8, 1);
        var replacementPolicy = new Policy
        {
            Id = Guid.NewGuid(),
            PolicyNumber = "POL-REWRITE-1",
            SubmissionId = fixture.Policy.SubmissionId,
            Submission = fixture.Policy.Submission,
            BoundQuoteId = replacementQuote.Id,
            BoundQuote = replacementQuote,
            CarrierId = fixture.Policy.CarrierId,
            Carrier = fixture.Policy.Carrier,
            LineOfBusiness = fixture.Policy.LineOfBusiness,
            EffectiveDate = replacementQuote.EffectiveDate,
            ExpirationDate = replacementQuote.ExpirationDate,
            PremiumAmount = replacementQuote.PremiumAmount,
            TaxesAndFees = replacementQuote.TaxesAndFees,
            TotalPremium = replacementQuote.TotalPremium,
            Status = PolicyStatus.Active,
            BoundDate = replacementQuote.BoundDate.Value,
        };
        db.Add(replacementPolicy);
        await db.SaveChangesAsync();

        var result = await policyService.CompleteRewriteAsync(
            fixture.Policy.Id,
            rewriteTransaction.Id,
            new CompleteRewritePolicyDto { CompletedDate = new DateOnly(2026, 8, 1), Notes = "Replacement policy bound." },
            UserAccessScope.All(fixture.UserId),
            [AppPermissions.UnderwritingAuthorityApprove]);

        Assert.True(result.IsSuccess, $"{result.ErrorCode}: {result.ErrorMessage}");
        Assert.Equal(PolicyStatus.Renewed, fixture.Policy.Status);
        Assert.Equal(PolicyTransactionStatus.Completed, rewriteTransaction.Status);
        Assert.Equal(replacementPolicy.Id, rewriteTransaction.RewriteDetail?.ReplacementPolicyId);
        Assert.Equal("Replacement policy bound.", rewriteTransaction.Notes);
        Assert.NotNull(rewriteTransaction.ResultingPolicyVersionId);
        var sourceVersions = await db.Set<PolicyVersion>()
            .Where(v => v.PolicyId == fixture.Policy.Id)
            .OrderBy(v => v.VersionNumber)
            .ToListAsync();
        Assert.Equal(2, sourceVersions.Count);
        Assert.Equal(PolicyStatus.Active, sourceVersions[0].Status);
        Assert.Equal(PolicyStatus.Renewed, sourceVersions[1].Status);
        Assert.Equal(sourceVersions[1].Id, rewriteTransaction.ResultingPolicyVersionId);
        var artifacts = await policyService.GetTransactionArtifactsAsync(fixture.Policy.Id, rewriteTransaction.Id, UserAccessScope.All(fixture.UserId));
        Assert.True(artifacts.IsSuccess);
        Assert.Equal(replacementPolicy.Id, artifacts.Value!.Transaction.RewriteDetail?.ReplacementPolicyId);
        Assert.Equal(replacementQuote.QuoteNumber, artifacts.Value.Transaction.RewriteDetail?.ReplacementQuoteNumber);
        Assert.Equal(replacementPolicy.PolicyNumber, artifacts.Value.Transaction.RewriteDetail?.ReplacementPolicyNumber);
    }

    [Fact]
    public async Task CompleteRewrite_RequiresAuthorityApproval()
    {
        await using var db = CreateDb();
        var fixture = await SeedBoundPolicyAsync(db);
        var quotes = new RecordingQuoteService(db);
        var policyService = CreatePolicyService(db, new RecordingInvoicingService(), quoteService: quotes);
        var rewrite = await policyService.StartRewriteAsync(fixture.Policy.Id, new StartRewritePolicyDto
        {
            EffectiveDate = new DateOnly(2026, 8, 1),
            Reason = "Carrier requested new paper",
        }, UserAccessScope.All(fixture.UserId));
        Assert.True(rewrite.IsSuccess);
        var rewriteTransaction = await db.Set<PolicyTransaction>().SingleAsync(t => t.TransactionType == TransactionType.Rewrite);
        var replacementQuote = await db.Set<Quote>().SingleAsync(q => q.Id == rewriteTransaction.RenewalQuoteId);
        replacementQuote.Status = QuoteStatus.Bound;
        replacementQuote.PolicyNumber = "POL-REWRITE-1";
        replacementQuote.BoundDate = new DateOnly(2026, 8, 1);
        var replacementPolicy = new Policy
        {
            Id = Guid.NewGuid(),
            PolicyNumber = "POL-REWRITE-1",
            SubmissionId = fixture.Policy.SubmissionId,
            Submission = fixture.Policy.Submission,
            BoundQuoteId = replacementQuote.Id,
            BoundQuote = replacementQuote,
            CarrierId = fixture.Policy.CarrierId,
            Carrier = fixture.Policy.Carrier,
            LineOfBusiness = fixture.Policy.LineOfBusiness,
            EffectiveDate = replacementQuote.EffectiveDate,
            ExpirationDate = replacementQuote.ExpirationDate,
            PremiumAmount = replacementQuote.PremiumAmount,
            TaxesAndFees = replacementQuote.TaxesAndFees,
            TotalPremium = replacementQuote.TotalPremium,
            Status = PolicyStatus.Active,
            BoundDate = replacementQuote.BoundDate.Value,
        };
        db.Add(replacementPolicy);
        await db.SaveChangesAsync();

        var blocked = await policyService.CompleteRewriteAsync(
            fixture.Policy.Id,
            rewriteTransaction.Id,
            new CompleteRewritePolicyDto { CompletedDate = new DateOnly(2026, 8, 1), Notes = "Replacement policy bound." },
            UserAccessScope.All(fixture.UserId));

        Assert.False(blocked.IsSuccess);
        Assert.Equal("AUTHORITY_APPROVAL_REQUIRED", blocked.ErrorCode);
        Assert.Equal(PolicyStatus.Active, fixture.Policy.Status);
        Assert.Equal(PolicyTransactionStatus.Submitted, rewriteTransaction.Status);
        var approval = await db.Set<AuthorityApprovalRequest>().SingleAsync();
        Assert.Equal(AuthorityApprovalTargetType.PolicyTransaction, approval.TargetType);
        Assert.Equal(rewriteTransaction.Id, approval.TargetId);
        Assert.Equal("policy.rewrite.complete", approval.ActionCode);
        Assert.Equal("Complete policy rewrite", approval.ActionLabel);
        Assert.Equal("PolicyRewriteCompletion", approval.ApprovalType);
        Assert.Equal(AuthorityApprovalStatus.Pending, approval.Status);
        Assert.Contains("rewrite", approval.Reason, StringComparison.OrdinalIgnoreCase);

        approval.Status = AuthorityApprovalStatus.Approved;
        approval.DecisionById = Guid.NewGuid();
        approval.DecisionAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var completed = await policyService.CompleteRewriteAsync(
            fixture.Policy.Id,
            rewriteTransaction.Id,
            new CompleteRewritePolicyDto { CompletedDate = new DateOnly(2026, 8, 1), Notes = "Replacement policy bound." },
            UserAccessScope.All(fixture.UserId));

        Assert.True(completed.IsSuccess, $"{completed.ErrorCode}: {completed.ErrorMessage}");
        Assert.Equal(PolicyStatus.Renewed, fixture.Policy.Status);
        Assert.Equal(PolicyTransactionStatus.Completed, rewriteTransaction.Status);
    }

    [Fact]
    public async Task NonRenewal_CreatesNoticeDetailWithoutClosingPolicy()
    {
        await using var db = CreateDb();
        var fixture = await SeedBoundPolicyAsync(db);
        var template = new DocumentTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Commercial Non-Renewal Notice - Sample",
            EntityType = TemplateEntityType.Policy,
            Kind = DocumentTemplateKind.Document,
            HtmlContent = "<p>{{policy.policyNumber}}</p>",
            CreatedById = fixture.UserId,
            CreatedBy = fixture.Policy.BoundQuote.CreatedBy,
            IsActive = true,
        };
        db.Add(template);
        await db.SaveChangesAsync();
        var documents = new RecordingDocumentGenerationService(db);
        var policyService = CreatePolicyService(db, new RecordingInvoicingService(), documentGeneration: documents);

        var result = await policyService.NonRenewAsync(fixture.Policy.Id, new NonRenewPolicyDto
        {
            NonRenewedDate = new DateOnly(2026, 12, 31),
            Reason = "Carrier appetite change",
            NoticeMailingDate = new DateOnly(2026, 11, 1),
            NoticeRequirementDays = 45,
            MailingDays = 3,
            Method = "Certified Mail",
            NoticeTemplateId = template.Id,
        }, UserAccessScope.All(fixture.UserId), [AppPermissions.UnderwritingAuthorityApprove]);

        Assert.True(result.IsSuccess);
        Assert.Equal(PolicyStatus.Active, fixture.Policy.Status);
        Assert.Null(fixture.Policy.NonRenewedDate);
        var transaction = await db.Set<PolicyTransaction>().SingleAsync(t => t.TransactionType == TransactionType.NonRenewal);
        Assert.Equal(PolicyTransactionStatus.NoticeSent, transaction.Status);
        Assert.Equal(new DateOnly(2026, 12, 31), transaction.EffectiveDate);
        Assert.Equal("Carrier appetite change", transaction.ReasonText);
        Assert.Equal(fixture.Policy.BoundQuoteId, transaction.SourceQuoteId);
        Assert.Equal(fixture.Policy.TotalPremium, transaction.PremiumBefore);
        Assert.Equal(fixture.Policy.TotalPremium, transaction.NewTotalPremium);
        Assert.Equal(fixture.Policy.TotalPremium, transaction.PremiumAfter);

        var detail = await db.Set<PolicyNonRenewalDetail>().SingleAsync(d => d.PolicyTransactionId == transaction.Id);
        Assert.Equal("Carrier appetite change", detail.Reason);
        Assert.Equal(new DateOnly(2026, 11, 1), detail.NoticeMailingDate);
        Assert.Equal(45, detail.NoticeRequirementDays);
        Assert.Equal(3, detail.MailingDays);
        Assert.Equal(new DateOnly(2026, 12, 31), detail.NonRenewalEffectiveDate);
        Assert.Equal("Certified Mail", detail.Method);
        Assert.Equal(template.Id, detail.NoticeTemplateId);
        Assert.Equal(template.Id, documents.GeneratedTemplateId);
        Assert.Equal(fixture.Policy.Id, documents.GeneratedPolicyId);
        Assert.Equal(transaction.Id, documents.GeneratedPolicyTransactionId);
        var notice = await db.Set<Attachment>().SingleAsync(a => a.PolicyTransactionId == transaction.Id);
        Assert.Equal(DocumentType.CancellationNonRenewal, notice.DocumentType);

        var history = await db.Set<PolicyTransactionStatusHistory>()
            .Where(h => h.PolicyTransactionId == transaction.Id)
            .OrderBy(h => h.ChangedAt)
            .ToListAsync();
        Assert.Equal(
            new[] { "policy.transaction.created", "policy.transaction.notice_sent" },
            history.Select(h => h.EventName).ToArray());
    }

    [Fact]
    public async Task NonRenewal_RequiresAuthorityApprovalBeforeNoticeIssued()
    {
        await using var db = CreateDb();
        var fixture = await SeedBoundPolicyAsync(db);
        var policyService = CreatePolicyService(db, new RecordingInvoicingService());

        var blocked = await policyService.NonRenewAsync(fixture.Policy.Id, new NonRenewPolicyDto
        {
            NonRenewedDate = new DateOnly(2026, 12, 31),
            Reason = "Carrier appetite change",
            NoticeMailingDate = new DateOnly(2026, 11, 1),
            NoticeRequirementDays = 45,
            MailingDays = 3,
            Method = "Certified Mail",
        }, UserAccessScope.All(fixture.UserId));

        Assert.False(blocked.IsSuccess);
        Assert.Equal("AUTHORITY_APPROVAL_REQUIRED", blocked.ErrorCode);
        Assert.Equal(PolicyStatus.Active, fixture.Policy.Status);
        Assert.Empty(await db.Set<PolicyTransaction>().Where(t => t.TransactionType == TransactionType.NonRenewal).ToListAsync());
        var approval = await db.Set<AuthorityApprovalRequest>().SingleAsync();
        Assert.Equal(AuthorityApprovalTargetType.Policy, approval.TargetType);
        Assert.Equal(fixture.Policy.Id, approval.TargetId);
        Assert.Equal("policy.nonrenewal.issue-notice", approval.ActionCode);
        Assert.Equal("Issue non-renewal notice", approval.ActionLabel);
        Assert.Equal("PolicyNonRenewalNotice", approval.ApprovalType);
        Assert.Equal(AuthorityApprovalStatus.Pending, approval.Status);
        Assert.Contains("non-renewal", approval.Reason, StringComparison.OrdinalIgnoreCase);

        approval.Status = AuthorityApprovalStatus.Approved;
        approval.DecisionById = Guid.NewGuid();
        approval.DecisionAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var issued = await policyService.NonRenewAsync(fixture.Policy.Id, new NonRenewPolicyDto
        {
            NonRenewedDate = new DateOnly(2026, 12, 31),
            Reason = "Carrier appetite change",
            NoticeMailingDate = new DateOnly(2026, 11, 1),
            NoticeRequirementDays = 45,
            MailingDays = 3,
            Method = "Certified Mail",
        }, UserAccessScope.All(fixture.UserId));

        Assert.True(issued.IsSuccess, $"{issued.ErrorCode}: {issued.ErrorMessage}");
        var transaction = await db.Set<PolicyTransaction>().SingleAsync(t => t.TransactionType == TransactionType.NonRenewal);
        Assert.Equal(PolicyTransactionStatus.NoticeSent, transaction.Status);
    }

    [Fact]
    public async Task MarkForNonRenewal_CreatesNoticePendingTransactionAndAssistantTask()
    {
        await using var db = CreateDb();
        var fixture = await SeedBoundPolicyAsync(db);
        var assistant = new User
        {
            Id = Guid.NewGuid(),
            UserName = "assistant@sims.test",
            Email = "assistant@sims.test",
            FirstName = "Assistant",
            LastName = "User",
        };
        fixture.Submission.AssistantUWId = assistant.Id;
        db.Add(assistant);
        await db.SaveChangesAsync();
        var policyService = CreatePolicyService(db, new RecordingInvoicingService());

        var result = await policyService.MarkForNonRenewalAsync(fixture.Policy.Id, new MarkNonRenewalDto
        {
            NonRenewedDate = new DateOnly(2026, 12, 31),
            Reason = "Carrier appetite change",
            Notes = "UW approved non-renewal review.",
        }, UserAccessScope.All(fixture.UserId), [AppPermissions.UnderwritingAuthorityApprove]);

        Assert.True(result.IsSuccess, $"{result.ErrorCode}: {result.ErrorMessage}");
        Assert.Equal(PolicyStatus.Active, fixture.Policy.Status);
        Assert.Null(fixture.Policy.NonRenewedDate);
        var transaction = await db.Set<PolicyTransaction>()
            .Include(t => t.NonRenewalDetail)
            .SingleAsync(t => t.TransactionType == TransactionType.NonRenewal);
        Assert.Equal(PolicyTransactionStatus.NoticePending, transaction.Status);
        Assert.Equal(new DateOnly(2026, 12, 31), transaction.EffectiveDate);
        Assert.Equal("Carrier appetite change", transaction.ReasonText);
        Assert.Equal("UW approved non-renewal review.", transaction.Notes);
        Assert.Null(transaction.NonRenewalDetail);
        Assert.Empty(await db.Set<PolicyNonRenewalDetail>().ToListAsync());
        Assert.Empty(await db.Set<Attachment>().Where(a => a.PolicyTransactionId == transaction.Id).ToListAsync());

        var task = await db.Set<TaskInstance>()
            .Include(t => t.TaskType)
            .SingleAsync(t => t.EntityType == TaskEntityType.PolicyTransaction && t.EntityId == transaction.Id);
        Assert.Equal("Prepare non-renewal notice", task.TaskType.Name);
        Assert.Equal(assistant.Id, task.AssignedUserId);
        Assert.Equal(TaskInstanceStatus.Open, task.Status);
        Assert.Contains(fixture.Policy.Id.ToString(), task.ReferenceUrl);

        var history = await db.Set<PolicyTransactionStatusHistory>()
            .Where(h => h.PolicyTransactionId == transaction.Id)
            .OrderBy(h => h.ChangedAt)
            .ToListAsync();
        Assert.Equal(
            new[] { "policy.transaction.created", "policy.transaction.notice_pending" },
            history.Select(h => h.EventName).ToArray());
    }

    [Fact]
    public async Task MarkForNonRenewal_RequiresAuthorityApproval()
    {
        await using var db = CreateDb();
        var fixture = await SeedBoundPolicyAsync(db);
        var policyService = CreatePolicyService(db, new RecordingInvoicingService());

        var blocked = await policyService.MarkForNonRenewalAsync(fixture.Policy.Id, new MarkNonRenewalDto
        {
            NonRenewedDate = new DateOnly(2026, 12, 31),
            Reason = "Carrier appetite change",
        }, UserAccessScope.All(fixture.UserId));

        Assert.False(blocked.IsSuccess);
        Assert.Equal("AUTHORITY_APPROVAL_REQUIRED", blocked.ErrorCode);
        Assert.Empty(await db.Set<PolicyTransaction>().Where(t => t.TransactionType == TransactionType.NonRenewal).ToListAsync());
        Assert.Empty(await db.Set<TaskInstance>().ToListAsync());
        var approval = await db.Set<AuthorityApprovalRequest>().SingleAsync();
        Assert.Equal(AuthorityApprovalTargetType.Policy, approval.TargetType);
        Assert.Equal(fixture.Policy.Id, approval.TargetId);
        Assert.Equal("policy.nonrenewal.mark", approval.ActionCode);
        Assert.Equal("Mark policy for non-renewal", approval.ActionLabel);
        Assert.Equal("PolicyNonRenewalMark", approval.ApprovalType);
        Assert.Equal(AuthorityApprovalStatus.Pending, approval.Status);
    }

    [Fact]
    public async Task NonRenewal_UsesMarkedTransactionWhenNoticeIsIssued()
    {
        await using var db = CreateDb();
        var fixture = await SeedBoundPolicyAsync(db);
        var policyService = CreatePolicyService(db, new RecordingInvoicingService());
        var mark = await policyService.MarkForNonRenewalAsync(fixture.Policy.Id, new MarkNonRenewalDto
        {
            NonRenewedDate = new DateOnly(2026, 12, 31),
            Reason = "Carrier appetite change",
        }, UserAccessScope.All(fixture.UserId), [AppPermissions.UnderwritingAuthorityApprove]);
        Assert.True(mark.IsSuccess);

        var issued = await policyService.NonRenewAsync(fixture.Policy.Id, new NonRenewPolicyDto
        {
            NonRenewedDate = new DateOnly(2026, 12, 31),
            Reason = "Carrier appetite change",
            NoticeMailingDate = new DateOnly(2026, 11, 1),
            NoticeRequirementDays = 45,
            MailingDays = 3,
            Method = "Certified Mail",
        }, UserAccessScope.All(fixture.UserId), [AppPermissions.UnderwritingAuthorityApprove]);

        Assert.True(issued.IsSuccess, $"{issued.ErrorCode}: {issued.ErrorMessage}");
        var transaction = await db.Set<PolicyTransaction>()
            .Include(t => t.NonRenewalDetail)
            .SingleAsync(t => t.TransactionType == TransactionType.NonRenewal);
        Assert.Equal(mark.Value!.Id, transaction.Id);
        Assert.Equal(PolicyTransactionStatus.NoticeSent, transaction.Status);
        Assert.NotNull(transaction.NonRenewalDetail);
        Assert.Equal(new DateOnly(2026, 11, 1), transaction.NonRenewalDetail.NoticeMailingDate);
        Assert.Equal("Certified Mail", transaction.NonRenewalDetail.Method);

        var history = await db.Set<PolicyTransactionStatusHistory>()
            .Where(h => h.PolicyTransactionId == transaction.Id)
            .OrderBy(h => h.ChangedAt)
            .ToListAsync();
        Assert.Equal(
            new[] { "policy.transaction.created", "policy.transaction.notice_pending", "policy.transaction.notice_sent" },
            history.Select(h => h.EventName).ToArray());
    }

    [Fact]
    public async Task NonRenewal_RecordsLegalAndComplianceSnapshot()
    {
        await using var db = CreateDb();
        var fixture = await SeedBoundPolicyAsync(db);
        var requirement = new LegalRequirementSection
        {
            Id = Guid.NewGuid(),
            State = "North Carolina",
            LineOfBusiness = "InlandMarine",
            Action = "NonRenewal",
            Category = "NOTICE REQUIREMENTS",
            Topic = "Notice timing",
            RequirementText = "Send written non-renewal notice before expiration.",
            Citations = ["NC-NR-1"],
        };
        db.Add(requirement);
        await db.SaveChangesAsync();
        var policyService = CreatePolicyService(db, new RecordingInvoicingService());

        var result = await policyService.NonRenewAsync(fixture.Policy.Id, new NonRenewPolicyDto
        {
            NonRenewedDate = new DateOnly(2026, 12, 31),
            Reason = "Carrier appetite change",
            NoticeMailingDate = new DateOnly(2026, 11, 1),
            NoticeRequirementDays = 45,
            MailingDays = 3,
            Method = "Certified Mail",
            ComplianceChecklist =
            [
                new CancellationComplianceChecklistItemDto
                {
                    Key = "non-renewal-notice-period-reviewed",
                    Label = "Notice period reviewed for the non-renewal effective date.",
                    IsCompleted = true,
                    RequirementSectionIds = [requirement.Id],
                }
            ],
            LegalRequirementSectionIds = [requirement.Id],
        }, UserAccessScope.All(fixture.UserId), [AppPermissions.UnderwritingAuthorityApprove]);

        Assert.True(result.IsSuccess, $"{result.ErrorCode}: {result.ErrorMessage}");
        var transaction = await db.Set<PolicyTransaction>().SingleAsync(t => t.TransactionType == TransactionType.NonRenewal);
        Assert.Equal(PolicyTransactionStatus.NoticeSent, transaction.Status);
        var checklist = await db.Set<PolicyTransactionComplianceChecklist>()
            .Include(c => c.Items)
            .SingleAsync(c => c.PolicyTransactionId == transaction.Id);
        Assert.Equal("NonRenewal", checklist.Purpose);
        var checklistItem = Assert.Single(checklist.Items);
        Assert.Equal("non-renewal-notice-period-reviewed", checklistItem.Key);
        Assert.True(checklistItem.IsCompleted);
        Assert.Equal(requirement.Id, checklistItem.LegalRequirementSectionId);
        Assert.Equal(fixture.UserId, checklistItem.CompletedById);
        Assert.Contains("Send written non-renewal notice before expiration.", checklistItem.SnapshotJson);

        requirement.RequirementText = "Updated non-renewal requirement after the transaction.";
        await db.SaveChangesAsync();
        var artifacts = await policyService.GetTransactionArtifactsAsync(fixture.Policy.Id, transaction.Id, UserAccessScope.All(fixture.UserId));
        Assert.True(artifacts.IsSuccess);
        var artifactChecklist = Assert.Single(artifacts.Value!.ComplianceChecklists);
        var artifactItem = Assert.Single(artifactChecklist.Items);
        Assert.Equal("non-renewal-notice-period-reviewed", artifactItem.Key);
        Assert.Contains("Send written non-renewal notice before expiration.", artifactItem.SnapshotJson);
        Assert.DoesNotContain("Updated non-renewal requirement after the transaction.", artifactItem.SnapshotJson);
        Assert.Equal(PolicyStatus.Active, fixture.Policy.Status);
        Assert.Null(fixture.Policy.NonRenewedDate);
    }

    [Fact]
    public async Task CompleteNonRenewal_BeforeEffectiveDateFails()
    {
        await using var db = CreateDb();
        var fixture = await SeedBoundPolicyAsync(db);
        var policyService = CreatePolicyService(db, new RecordingInvoicingService());
        var notice = await policyService.NonRenewAsync(fixture.Policy.Id, new NonRenewPolicyDto
        {
            NonRenewedDate = new DateOnly(2026, 12, 31),
            Reason = "Carrier appetite change",
            NoticeMailingDate = new DateOnly(2026, 11, 1),
            NoticeRequirementDays = 45,
            MailingDays = 3,
            Method = "Certified Mail",
        }, UserAccessScope.All(fixture.UserId), [AppPermissions.UnderwritingAuthorityApprove]);
        Assert.True(notice.IsSuccess);

        var transaction = await db.Set<PolicyTransaction>().SingleAsync(t => t.TransactionType == TransactionType.NonRenewal);
        var result = await policyService.CompleteNonRenewalAsync(
            fixture.Policy.Id,
            transaction.Id,
            new CompleteNonRenewalDto { CompletedDate = new DateOnly(2026, 12, 30) },
            UserAccessScope.All(fixture.UserId));

        Assert.False(result.IsSuccess);
        Assert.Equal("NON_RENEWAL_NOT_EFFECTIVE", result.ErrorCode);
        Assert.Equal(PolicyStatus.Active, fixture.Policy.Status);
        Assert.Null(fixture.Policy.NonRenewedDate);
    }

    [Fact]
    public async Task CompleteNonRenewal_OnEffectiveDateNonRenewsPolicyAndCreatesVersion()
    {
        await using var db = CreateDb();
        var fixture = await SeedBoundPolicyAsync(db);
        var policyService = CreatePolicyService(db, new RecordingInvoicingService());
        var notice = await policyService.NonRenewAsync(fixture.Policy.Id, new NonRenewPolicyDto
        {
            NonRenewedDate = new DateOnly(2026, 12, 31),
            Reason = "Carrier appetite change",
            NoticeMailingDate = new DateOnly(2026, 11, 1),
            NoticeRequirementDays = 45,
            MailingDays = 3,
            Method = "Certified Mail",
        }, UserAccessScope.All(fixture.UserId), [AppPermissions.UnderwritingAuthorityApprove]);
        Assert.True(notice.IsSuccess);

        var transaction = await db.Set<PolicyTransaction>().SingleAsync(t => t.TransactionType == TransactionType.NonRenewal);
        var result = await policyService.CompleteNonRenewalAsync(
            fixture.Policy.Id,
            transaction.Id,
            new CompleteNonRenewalDto { CompletedDate = new DateOnly(2026, 12, 31) },
            UserAccessScope.All(fixture.UserId));

        Assert.True(result.IsSuccess, $"{result.ErrorCode}: {result.ErrorMessage}");
        Assert.Equal(PolicyStatus.NonRenewed, fixture.Policy.Status);
        Assert.Equal(new DateOnly(2026, 12, 31), fixture.Policy.NonRenewedDate);
        Assert.Equal(PolicyTransactionStatus.Completed, transaction.Status);
        Assert.NotNull(transaction.PriorPolicyVersionId);
        Assert.NotNull(transaction.ResultingPolicyVersionId);
        var versions = await db.Set<PolicyVersion>()
            .Where(v => v.PolicyId == fixture.Policy.Id)
            .OrderBy(v => v.VersionNumber)
            .ToListAsync();
        Assert.Equal(2, versions.Count);
        Assert.Equal(PolicyStatus.Active, versions[0].Status);
        Assert.Equal(PolicyStatus.NonRenewed, versions[1].Status);
        Assert.Equal(versions[0].Id, transaction.PriorPolicyVersionId);
        Assert.Equal(versions[1].Id, transaction.ResultingPolicyVersionId);
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

    private sealed class SqlitePolicyLifecycleDbContext(DbContextOptions<ApplicationDbContext> options)
        : ApplicationDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            RemoveNpgsqlAnnotations(builder.Model);
            foreach (var entity in builder.Model.GetEntityTypes())
            {
                RemoveNpgsqlAnnotations(entity);
                foreach (var property in entity.GetProperties())
                {
                    RemoveNpgsqlAnnotations(property);
                    NormalizeSqliteProperty(property);
                }
                foreach (var key in entity.GetKeys())
                    RemoveNpgsqlAnnotations(key);
                foreach (var index in entity.GetIndexes())
                    RemoveNpgsqlAnnotations(index);
                foreach (var foreignKey in entity.GetForeignKeys())
                    RemoveNpgsqlAnnotations(foreignKey);
            }

            builder.Entity<Quote>()
                .HasIndex(q => q.PolicyNumber)
                .IsUnique()
                .HasFilter(null);
        }

        private static void RemoveNpgsqlAnnotations(IMutableAnnotatable annotatable)
        {
            foreach (var annotation in annotatable.GetAnnotations()
                .Where(annotation => annotation.Name.StartsWith("Npgsql:", StringComparison.Ordinal))
                .ToList())
            {
                annotatable.RemoveAnnotation(annotation.Name);
            }
        }

        private static void NormalizeSqliteProperty(IMutableProperty property)
        {
            if (property.GetColumnType() is "jsonb" or "text[]")
                property.SetColumnType("TEXT");

            if (property.GetDefaultValueSql()?.Contains("::", StringComparison.Ordinal) == true)
                property.SetDefaultValueSql(null);
        }
    }

    private static QuoteService CreateQuoteService(
        ApplicationDbContext db,
        RecordingInvoicingService invoicing,
        RecordingWorkflowEngineService? workflow = null,
        IPolicyNumberService? policyNumbers = null,
        ICarrierCommissionService? carrierCommissions = null)
    {
        workflow ??= new RecordingWorkflowEngineService();
        policyNumbers ??= new StubPolicyNumberService();
        carrierCommissions ??= new NoOpCarrierCommissionService();
        var services = new ServiceCollection()
            .AddSingleton<DbContext>(db)
            .AddSingleton<IInvoicingService>(invoicing)
            .BuildServiceProvider();

        return new QuoteService(
            services,
            workflow,
            carrierCommissions,
            new NoOpAgentCommissionService(),
            new NoOpQuoteChecklistService(),
            policyNumbers,
            new PolicyTransactionLifecycleService(db, workflow),
            new PolicyVersionService(db),
            new UnderwritingClearanceService(db),
            new UnderwritingReferralService(db),
            new UnderwritingControlEnforcementService(db));
    }

    private static PolicyService CreatePolicyService(
        ApplicationDbContext db,
        RecordingInvoicingService invoicing,
        RecordingPolicyAssemblyService? assembly = null,
        IQuoteService? quoteService = null,
        RecordingWorkflowEngineService? workflow = null,
        IDocumentGenerationService? documentGeneration = null,
        IQuoteChecklistService? checklist = null)
    {
        assembly ??= new RecordingPolicyAssemblyService();
        quoteService ??= new RecordingQuoteService(db);
        workflow ??= new RecordingWorkflowEngineService();
        var services = new ServiceCollection()
            .AddSingleton<DbContext>(db)
            .AddSingleton<IQuotePolicyFormSelectionService>(new NoOpQuotePolicyFormSelectionService())
            .AddSingleton<IPolicyAssemblyService>(assembly)
            .AddSingleton<IDocumentGenerationService>(documentGeneration ?? new RecordingDocumentGenerationService(db))
            .AddSingleton<IUnderwritingReferralService>(new UnderwritingReferralService(db))
            .AddSingleton<IUnderwritingControlEnforcementService>(new UnderwritingControlEnforcementService(db))
            .AddSingleton<IAuthorityApprovalService>(new AuthorityApprovalService(db))
            .AddSingleton(checklist ?? new NoOpQuoteChecklistService())
            .AddSingleton(quoteService)
            .BuildServiceProvider();

        return new PolicyService(services, invoicing, new RecordingVoidService(), new PolicyTransactionLifecycleService(db, workflow), new PolicyVersionService(db));
    }

    private static QuoteChecklistService CreateQuoteChecklistService(ApplicationDbContext db)
    {
        var services = new ServiceCollection()
            .AddSingleton<DbContext>(db)
            .BuildServiceProvider();

        return new QuoteChecklistService(services);
    }

    private static QuoteBindDto BindRequest() => new()
    {
        BoundDate = new DateOnly(2026, 1, 5),
        EffectiveDate = new DateOnly(2026, 1, 5),
        ExpirationDate = new DateOnly(2027, 1, 5),
    };

    private static QuoteCreateDto CreateQuoteRequest(QuoteFixture fixture, Guid? programId) => new()
    {
        SubmissionId = fixture.Submission.Id,
        ProgramId = programId,
        CarrierId = fixture.Carrier.Id,
        LineOfBusiness = PolicyLineOfBusiness.InlandMarine,
        EffectiveDate = new DateOnly(2026, 1, 5),
        ExpirationDate = new DateOnly(2027, 1, 5),
        PremiumAmount = 900m,
        TaxesAndFees = 100m,
        IsFilingState = true,
    };

    private static async Task<QuoteFixture> SeedBindableQuoteAsync(ApplicationDbContext db)
    {
        var fixture = CreateQuoteFixture("Test Logistics");
        // WS5-R Batch 1: quotes now require a program and an active program→carrier→LOB→state
        // path to be bindable (the commission schedule is supplied by the NoOp stub below).
        var program = new ProgramConfiguration { Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        fixture.Quote.ProgramId = program.Id;
        fixture.Quote.Program = program;
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
        db.AddRange(fixture.User, fixture.Carrier, fixture.Insured, fixture.Submission, program, fixture.Quote, template, selection);
        await db.SaveChangesAsync();
        db.Add(new ProgramCarrier
        {
            ProgramConfigurationId = program.Id,
            CarrierId = fixture.Carrier.Id,
            IsActive = true,
            EffectiveDate = new DateOnly(2026, 1, 1),
            LinesOfBusiness =
            {
                new ProgramCarrierLineOfBusiness
                {
                    LineOfBusiness = fixture.Quote.LineOfBusiness,
                    IsActive = true,
                    EffectiveDate = new DateOnly(2026, 1, 1),
                    States =
                    {
                        new ProgramCarrierLobState
                        {
                            StateCode = fixture.Insured.State,
                            IsActive = true,
                            EffectiveDate = new DateOnly(2026, 1, 1)
                        }
                    }
                }
            }
        });
        await db.SaveChangesAsync();
        return fixture;
    }

    private static UnderwritingGuidelineControl PublishedDocumentControl(
        UnderwritingGuidelineDocument document,
        QuoteFixture fixture,
        UnderwritingControlStage stage,
        string ruleKey,
        string label,
        int sortOrder) => new()
        {
            GuidelineDocumentId = document.Id,
            GuidelineDocument = document,
            ProgramName = document.ProgramName,
            CarrierId = fixture.Carrier.Id,
            LineOfBusiness = fixture.Quote.LineOfBusiness,
            StateCode = "ALL",
            ItemType = UnderwritingControlItemType.DocumentChecklistItem,
            Stage = stage,
            Severity = UnderwritingControlSeverity.HardBlock,
            Status = UnderwritingControlStatus.Published,
            RuleKey = ruleKey,
            Label = label,
            IsBlocking = true,
            SortOrder = sortOrder,
            PublishedByUserId = fixture.UserId,
            PublishedAt = DateTime.UtcNow,
        };

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

    private static OutboundCommunication CommunicationFor(PolicyFixture fixture, Guid policyTransactionId, OutboundCommunicationPurpose purpose, string subject) => new()
    {
        EntityType = OutboundCommunicationEntityType.Policy,
        EntityId = fixture.Policy.Id,
        PolicyTransactionId = policyTransactionId,
        Purpose = purpose,
        ToAddress = "agent@example.com",
        FromAddress = "uw@example.com",
        SenderType = OutboundCommunicationSenderType.CurrentUser,
        Subject = subject,
        BodyHtml = $"<p>{subject}</p>",
        Status = OutboundCommunicationStatus.Sent,
        CreatedById = fixture.UserId,
        CreatedBy = fixture.Policy.BoundQuote.CreatedBy,
    };

    private static Attachment AttachmentFor(PolicyFixture fixture, Guid? policyTransactionId, DocumentType documentType, string fileName) => new()
    {
        QuoteId = fixture.Policy.BoundQuoteId,
        EntityType = DocumentEntityType.Policy,
        DocumentType = documentType,
        PolicyTransactionId = policyTransactionId,
        FileName = fileName,
        BlobPath = fileName,
        ContentType = "application/pdf",
        FileSizeBytes = 123,
        UploadedById = fixture.UserId,
        UploadedBy = fixture.Policy.BoundQuote.CreatedBy,
    };

    private static Invoice InvoiceFor(string invoiceNumber, Guid policyTransactionId, decimal totalAmount, PolicyFixture fixture) => new()
    {
        InvoiceNumber = invoiceNumber,
        PolicyTransactionId = policyTransactionId,
        EffectiveDate = fixture.Policy.EffectiveDate,
        InvoiceDate = fixture.Policy.EffectiveDate,
        GrossPremium = totalAmount,
        TotalAmount = totalAmount,
        Status = "Posted",
        LedgerTransactionId = Guid.NewGuid(),
        CreatedBy = fixture.UserId,
    };

    private sealed record QuoteFixture(Guid UserId, User User, Carrier Carrier, Insured Insured, Submission Submission, Quote Quote);
    private sealed record PolicyFixture(Guid UserId, Insured Insured, Submission Submission, Quote Quote, Policy Policy);

    private sealed class StubPolicyNumberService : IPolicyNumberService
    {
        public Task<Result<PolicyNumberGenerationResult>> GenerateForBindAsync(Quote quote, Guid assignedById, DateOnly? effectiveDate = null)
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
        public Result<InvoiceDetailDto>? BindResult { get; set; }

        public Task<Result<InvoiceDetailDto>> BindAsync(CreateInvoiceRequest req, Guid userId, CancellationToken ct = default)
        {
            BindRequests.Add(req);
            if (BindResult != null)
                return Task.FromResult(BindResult);

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
                null,
                null,
                req.PolicyVersionId,
                null,
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
        public Guid? AssembledPolicyTransactionId { get; private set; }

        public Task<Result<GeneratedDocumentDto>> AssembleAndFileAsync(Guid policyId, Guid userId, bool isPreview = false, Guid? policyVersionId = null, Guid? policyTransactionId = null)
        {
            WasCalled = true;
            AssembledPolicyVersionId = policyVersionId;
            AssembledPolicyTransactionId = policyTransactionId;
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

    private sealed class RecordingDocumentGenerationService(ApplicationDbContext db) : IDocumentGenerationService
    {
        public Guid? GeneratedTemplateId { get; private set; }
        public Guid? GeneratedPolicyId { get; private set; }
        public Guid? GeneratedPolicyTransactionId { get; private set; }

        public Task<Result<GeneratedDocumentDto>> GenerateAsync(Guid templateId, TemplateEntityType entityType, Guid entityId, DocumentType? documentType, Guid userId)
            => Task.FromResult(Result<GeneratedDocumentDto>.Failure("NOT_USED", "Use transaction-aware generation."));

        public async Task<Result<GeneratedDocumentDto>> GenerateForPolicyTransactionAsync(
            Guid templateId,
            Guid policyId,
            Guid policyTransactionId,
            DocumentType documentType,
            Guid userId)
        {
            GeneratedTemplateId = templateId;
            GeneratedPolicyId = policyId;
            GeneratedPolicyTransactionId = policyTransactionId;
            var policy = await db.Set<Policy>().SingleAsync(p => p.Id == policyId);
            var attachment = new Attachment
            {
                EntityType = DocumentEntityType.Policy,
                QuoteId = policy.BoundQuoteId,
                DocumentType = documentType,
                PolicyTransactionId = policyTransactionId,
                FileName = "cancellation-notice.pdf",
                BlobPath = "cancellation-notice.pdf",
                ContentType = "application/pdf",
                FileSizeBytes = 100,
                UploadedById = userId,
            };
            db.Add(attachment);
            await db.SaveChangesAsync();

            return Result<GeneratedDocumentDto>.Success(new GeneratedDocumentDto(
                "/documents/cancellation-notice.pdf",
                new AttachmentDto
                {
                    Id = attachment.Id,
                    EntityType = attachment.EntityType,
                    DocumentType = attachment.DocumentType,
                    PolicyTransactionId = attachment.PolicyTransactionId,
                    FileName = attachment.FileName,
                    ContentType = attachment.ContentType,
                    FileSizeBytes = attachment.FileSizeBytes,
                    CreatedAt = attachment.CreatedAt,
                }));
        }
    }

    private sealed class RecordingOutboundEmailSender : IOutboundEmailSenderService
    {
        public Task<Result<OutboundEmailSendResult>> SendAsync(OutboundCommunication communication, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<OutboundEmailSendResult>.Success(new OutboundEmailSendResult("message-id", "https://graph.example/messages/message-id")));
    }

    private sealed class RecordingQuoteService(ApplicationDbContext db) : IQuoteService
    {
        public List<QuoteCreateDto> CreateRequests { get; } = [];

        public async Task<Result<QuoteDto>> CreateAsync(QuoteCreateDto dto, Guid createdById, UserAccessScope? access = null)
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
        public List<(Guid StepId, TaskEntityType EntityType, Guid EntityId, Dictionary<string, object> Context)> StepCompleted { get; } = [];

        public Task FireEventAsync(string eventName, TaskEntityType entityType, Guid entityId, Dictionary<string, object> context)
        {
            Events.Add((eventName, entityId));
            return Task.CompletedTask;
        }

        public Task FireStepCompletedAsync(Guid completedStepId, TaskEntityType entityType, Guid entityId, Dictionary<string, object> context)
        {
            StepCompleted.Add((completedStepId, entityType, entityId, context));
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpCarrierCommissionService : ICarrierCommissionService
    {
        public Task<Result<CarrierCommissionDto>> CreateAsync(Guid carrierId, CreateCarrierCommissionRequest req, Guid userId, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<Result<CarrierCommissionDto>> DisableAsync(long id, DateOnly? disabledDate, CancellationToken ct = default)
            => throw new NotSupportedException();

        // Represents a configured carrier commission schedule so the bind-time fail-closed
        // guard (WS5-R Batch 1) is satisfied; these lifecycle tests don't assert on the rate.
        public Task<CarrierCommissionRates?> GetActiveRatesAsync(Guid carrierId, string? lineOfBusiness, DateOnly asOfDate, Guid? programConfigurationId = null, CancellationToken ct = default)
            => Task.FromResult<CarrierCommissionRates?>(new CarrierCommissionRates(0.15m, 0.05m));

        public Task<IReadOnlyList<CarrierCommissionDto>> GetAllAsync(Guid carrierId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CarrierCommissionDto>>([]);
    }

    // Represents an UNconfigured carrier commission schedule (returns null) to exercise the
    // bind-time fail-closed guard (WS5-R Batch 1, A1.1).
    private sealed class MissingCarrierCommissionService : ICarrierCommissionService
    {
        public Task<Result<CarrierCommissionDto>> CreateAsync(Guid carrierId, CreateCarrierCommissionRequest req, Guid userId, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<Result<CarrierCommissionDto>> DisableAsync(long id, DateOnly? disabledDate, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<CarrierCommissionRates?> GetActiveRatesAsync(Guid carrierId, string? lineOfBusiness, DateOnly asOfDate, Guid? programConfigurationId = null, CancellationToken ct = default)
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

        public Task<decimal?> GetActiveRateAsync(
            Guid agentId,
            string? lineOfBusiness,
            DateOnly asOfDate,
            Guid? programConfigurationId = null,
            Guid? carrierId = null,
            string? stateCode = null,
            CancellationToken ct = default)
            => Task.FromResult<decimal?>(0m);

        public Task<IReadOnlyList<AgentCommissionDto>> GetAllAsync(Guid agentId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<AgentCommissionDto>>([]);
    }

    private sealed class NoOpQuoteChecklistService : IQuoteChecklistService
    {
        public Task<Result<List<QuoteChecklistItemDto>>> GetForQuoteAsync(Guid quoteId, UserAccessScope access, IReadOnlyCollection<UnderwritingControlStage>? stages = null)
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
