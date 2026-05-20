using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SIMS.Application.DTOs.UWWriteup;
using SIMS.Application.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;
using SIMS.Infrastructure.Data;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class UnderwritingReferralServiceTests
{
    [Fact]
    public async Task SyncFromWriteupAsync_CreatesRequiredReferralForTriggeredAppetiteFlag()
    {
        await using var db = CreateDb();
        var fixture = await SeedQuoteAsync(db);
        var service = new UnderwritingReferralService(db);
        var payload = new IMWriteupPayload
        {
            ReferralPremiumOver100k = true,
        };

        await service.SyncFromWriteupAsync(fixture.Quote.Id, fixture.UserId, payload);

        var appetite = await db.Set<UnderwritingAppetiteResult>().SingleAsync();
        Assert.Equal(fixture.Submission.Id, appetite.SubmissionId);
        Assert.Equal(fixture.Quote.Id, appetite.QuoteId);
        Assert.Equal("ReferralPremiumOver100k", appetite.RuleCode);
        Assert.True(appetite.ReferralRequired);

        var referral = await db.Set<UnderwritingReferral>().SingleAsync();
        Assert.Equal(fixture.Submission.Id, referral.SubmissionId);
        Assert.Equal(fixture.Quote.Id, referral.QuoteId);
        Assert.Equal("ReferralPremiumOver100k", referral.ReferralType);
        Assert.Equal(UnderwritingReferralStatus.Open, referral.Status);
        Assert.True(referral.Required);
    }

    [Fact]
    public async Task DecideAsync_ApprovesOpenReferralAndRecordsDecisionAudit()
    {
        await using var db = CreateDb();
        var fixture = await SeedQuoteAsync(db);
        var referral = new UnderwritingReferral
        {
            SubmissionId = fixture.Submission.Id,
            QuoteId = fixture.Quote.Id,
            ReferralType = "ReferralPremiumOver100k",
            Status = UnderwritingReferralStatus.Open,
            Required = true,
            Reason = "Premium over authority threshold.",
            RequestedById = fixture.UserId,
        };
        db.Add(referral);
        await db.SaveChangesAsync();
        var service = new UnderwritingReferralService(db);

        var result = await service.DecideAsync(referral.Id, UnderwritingReferralStatus.Approved, fixture.UserId, "Within appetite.");

        Assert.Equal(UnderwritingReferralStatus.Approved, result.Status);
        Assert.Equal(fixture.UserId, result.DecisionById);
        Assert.NotNull(result.DecisionAt);
        Assert.Equal("Within appetite.", result.DecisionNotes);
        Assert.False(await service.HasOpenRequiredReferralsAsync(fixture.Submission.Id));
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task<QuoteFixture> SeedQuoteAsync(ApplicationDbContext db)
    {
        var userId = Guid.NewGuid();
        var carrierId = Guid.NewGuid();
        var insuredId = Guid.NewGuid();
        var submission = new Submission
        {
            Id = Guid.NewGuid(),
            SubmissionNumber = "SUB-REF-1",
            InsuredId = insuredId,
            UnderwriterId = userId,
            EffectiveDate = new DateOnly(2026, 1, 1),
            ExpirationDate = new DateOnly(2027, 1, 1),
            Status = SubmissionStatus.Quoted,
            LinesOfBusiness = "[\"CommercialAuto\"]",
            CreatedById = userId,
        };
        var quote = new Quote
        {
            Id = Guid.NewGuid(),
            QuoteNumber = "Q-REF-1",
            SubmissionId = submission.Id,
            CarrierId = carrierId,
            LineOfBusiness = PolicyLineOfBusiness.CommercialAuto,
            EffectiveDate = new DateOnly(2026, 1, 1),
            ExpirationDate = new DateOnly(2027, 1, 1),
            Status = QuoteStatus.Quoted,
            CreatedById = userId,
        };

        db.AddRange(
            new User { Id = userId, UserName = "referral@sims.test", Email = "referral@sims.test", FirstName = "Referral", LastName = "User" },
            new Insured { Id = insuredId, InsuredType = InsuredType.Commercial, CompanyName = "Referral Test", AddressLine1 = "1 Main St", City = "Tyler", State = "TX", ZipCode = "75701", CreatedById = userId },
            new Carrier { Id = carrierId, Name = "Test Carrier", Naic = "12345" },
            submission,
            quote);
        await db.SaveChangesAsync();

        return new QuoteFixture(userId, submission, quote);
    }

    private sealed record QuoteFixture(Guid UserId, Submission Submission, Quote Quote);
}
