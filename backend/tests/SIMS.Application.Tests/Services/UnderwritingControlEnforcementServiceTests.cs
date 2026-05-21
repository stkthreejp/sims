using Microsoft.EntityFrameworkCore;
using SIMS.Application.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;
using SIMS.Infrastructure.Data;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class UnderwritingControlEnforcementServiceTests
{
    [Fact]
    public async Task EvaluateQuoteAsync_DoesNotApplyProgramSpecificControlsToUnassignedQuote()
    {
        await using var db = CreateDb();
        var fixture = await CreateQuoteFixtureAsync(db, programId: null);
        await AddPublishedControlAsync(db, fixture.Program.Id, fixture.Carrier.Id, isBlocking: true);

        var summary = await new UnderwritingControlEnforcementService(db)
            .EvaluateQuoteAsync(fixture.Quote.Id, UnderwritingControlStage.Bind, Guid.NewGuid());

        Assert.Empty(summary.Results);
    }

    [Fact]
    public async Task EvaluateQuoteAsync_AppliesProgramSpecificControlsToAssignedQuote()
    {
        await using var db = CreateDb();
        var programId = Guid.NewGuid();
        var fixture = await CreateQuoteFixtureAsync(db, programId);
        await AddPublishedControlAsync(db, programId, fixture.Carrier.Id, isBlocking: true);

        var summary = await new UnderwritingControlEnforcementService(db)
            .EvaluateQuoteAsync(fixture.Quote.Id, UnderwritingControlStage.Bind, Guid.NewGuid());

        var result = Assert.Single(summary.Results);
        Assert.Equal(UnderwritingControlEvaluationStatus.Blocked, result.Status);
        Assert.True(summary.HasBlockingResults);
    }

    [Fact]
    public async Task EvaluateQuoteAsync_KeepsLegacyScopeMatchingForControlsWithoutProgram()
    {
        await using var db = CreateDb();
        var fixture = await CreateQuoteFixtureAsync(db, programId: null);
        await AddPublishedControlAsync(db, programId: null, fixture.Carrier.Id, isBlocking: true);

        var summary = await new UnderwritingControlEnforcementService(db)
            .EvaluateQuoteAsync(fixture.Quote.Id, UnderwritingControlStage.Bind, Guid.NewGuid());

        var result = Assert.Single(summary.Results);
        Assert.Equal(UnderwritingControlEvaluationStatus.Blocked, result.Status);
    }

    [Fact]
    public async Task EvaluatePolicyAsync_UsesPolicyProgramAssignment()
    {
        await using var db = CreateDb();
        var programId = Guid.NewGuid();
        var fixture = await CreateQuoteFixtureAsync(db, programId);
        var policy = new Policy
        {
            PolicyNumber = "POL-1",
            Submission = fixture.Quote.Submission,
            BoundQuote = fixture.Quote,
            ProgramId = programId,
            Carrier = fixture.Carrier,
            LineOfBusiness = PolicyLineOfBusiness.InlandMarine,
            EffectiveDate = new DateOnly(2026, 6, 1),
            ExpirationDate = new DateOnly(2027, 6, 1),
            PremiumAmount = 1000m,
            TotalPremium = 1000m,
            BoundDate = new DateOnly(2026, 6, 1)
        };
        db.Add(policy);
        await AddPublishedControlAsync(db, programId, fixture.Carrier.Id, isBlocking: true, UnderwritingControlStage.Issue);

        var summary = await new UnderwritingControlEnforcementService(db)
            .EvaluatePolicyAsync(policy.Id, UnderwritingControlStage.Issue, Guid.NewGuid());

        var result = Assert.Single(summary.Results);
        Assert.Equal(UnderwritingControlEvaluationStatus.Blocked, result.Status);
    }

    private static async Task<QuoteFixture> CreateQuoteFixtureAsync(ApplicationDbContext db, Guid? programId)
    {
        var insured = new Insured
        {
            InsuredType = InsuredType.Commercial,
            CompanyName = "Longleaf Logging",
            State = "TX",
            CreatedById = Guid.NewGuid()
        };
        var submission = new Submission
        {
            SubmissionNumber = "SUB-1",
            Insured = insured,
            UnderwriterId = Guid.NewGuid(),
            CreatedById = Guid.NewGuid()
        };
        var carrier = new Carrier { Name = "Longleaf Insurance Company", IsActive = true };
        var program = new ProgramConfiguration
        {
            Id = programId ?? Guid.NewGuid(),
            Name = "Longleaf Inland Marine",
            Code = "LONGLEAF-IM",
            Carrier = carrier,
            LineOfBusiness = PolicyLineOfBusiness.InlandMarine,
            StateCode = "TX",
            IsActive = true
        };
        var quote = new Quote
        {
            QuoteNumber = "QTE-1",
            Submission = submission,
            Carrier = carrier,
            ProgramId = programId,
            LineOfBusiness = PolicyLineOfBusiness.InlandMarine,
            EffectiveDate = new DateOnly(2026, 6, 1),
            ExpirationDate = new DateOnly(2027, 6, 1),
            PremiumAmount = 1000m,
            TotalPremium = 1000m,
            CreatedById = Guid.NewGuid()
        };

        db.AddRange(program, quote);
        await db.SaveChangesAsync();
        return new QuoteFixture(carrier, program, quote);
    }

    private static async Task AddPublishedControlAsync(
        ApplicationDbContext db,
        Guid? programId,
        Guid carrierId,
        bool isBlocking,
        UnderwritingControlStage stage = UnderwritingControlStage.Bind)
    {
        var document = new UnderwritingGuidelineDocument
        {
            ProgramId = programId,
            ProgramName = "Longleaf Inland Marine",
            CarrierId = carrierId,
            LineOfBusiness = PolicyLineOfBusiness.InlandMarine,
            StateCode = "TX",
            Title = "Longleaf Guidelines",
            CreatedByUserId = Guid.NewGuid()
        };
        db.Add(document);
        db.Add(new UnderwritingGuidelineControl
        {
            GuidelineDocument = document,
            ProgramId = programId,
            ProgramName = document.ProgramName,
            CarrierId = carrierId,
            LineOfBusiness = PolicyLineOfBusiness.InlandMarine,
            StateCode = "TX",
            ItemType = UnderwritingControlItemType.AppetiteRule,
            Stage = stage,
            Severity = UnderwritingControlSeverity.HardBlock,
            Status = UnderwritingControlStatus.Published,
            RuleKey = "program-block",
            Label = "Program blocker",
            IsBlocking = isBlocking,
            OverrideAllowed = true
        });
        await db.SaveChangesAsync();
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed record QuoteFixture(Carrier Carrier, ProgramConfiguration Program, Quote Quote);
}
