using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SIMS.Application.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;
using SIMS.Infrastructure.Data;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class UnderwritingClearanceServiceTests
{
    [Fact]
    public async Task EvaluateSubmissionAsync_WarnsWhenOpenSubmissionMatchesRiskAndEffectiveDate()
    {
        await using var db = CreateDb();
        var insuredId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        db.Add(CreateUser(userId));
        db.Add(CreateInsured(insuredId, userId, "Twin Pines Logging"));
        db.Add(CreateSubmission(
            insuredId,
            userId,
            "SUB-EXISTING",
            new DateOnly(2026, 6, 1),
            SubmissionStatus.InProgress,
            PolicyLineOfBusiness.InlandMarine));
        var target = CreateSubmission(
            insuredId,
            userId,
            "SUB-TARGET",
            new DateOnly(2026, 6, 15),
            SubmissionStatus.New,
            PolicyLineOfBusiness.InlandMarine);
        db.Add(target);
        await db.SaveChangesAsync();
        var service = new UnderwritingClearanceService(db);

        var result = await service.EvaluateSubmissionAsync(target.Id, userId);

        Assert.Equal(UnderwritingClearanceStatus.Warning, result.OverallStatus);
        var duplicate = Assert.Single(result.Results, r => r.CheckType == UnderwritingClearanceCheckType.DuplicateSubmission);
        Assert.Equal(UnderwritingClearanceStatus.Warning, duplicate.Status);
        Assert.Equal("SUB-EXISTING", duplicate.MatchedRecordLabel);
        Assert.Contains("open submission", duplicate.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateSubmissionAsync_BlocksWhenActivePolicyOverlapsSameInsuredAndLob()
    {
        await using var db = CreateDb();
        var insuredId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var carrierId = Guid.NewGuid();
        db.AddRange(
            CreateUser(userId),
            CreateInsured(insuredId, userId, "Cypress Hauling"),
            new Carrier { Id = carrierId, Name = "Test Carrier", Naic = "12345" });
        var existingSubmission = CreateSubmission(
            insuredId,
            userId,
            "SUB-POLICY",
            new DateOnly(2026, 1, 1),
            SubmissionStatus.Bound,
            PolicyLineOfBusiness.CommercialAuto);
        var target = CreateSubmission(
            insuredId,
            userId,
            "SUB-TARGET",
            new DateOnly(2026, 6, 1),
            SubmissionStatus.New,
            PolicyLineOfBusiness.CommercialAuto);
        db.AddRange(existingSubmission, target);
        db.Add(CreatePolicy(
            existingSubmission.Id,
            carrierId,
            "POL-2026-001",
            new DateOnly(2026, 1, 1),
            new DateOnly(2027, 1, 1),
            PolicyLineOfBusiness.CommercialAuto));
        await db.SaveChangesAsync();
        var service = new UnderwritingClearanceService(db);

        var result = await service.EvaluateSubmissionAsync(target.Id, userId);

        Assert.Equal(UnderwritingClearanceStatus.Blocked, result.OverallStatus);
        var overlap = Assert.Single(result.Results, r => r.CheckType == UnderwritingClearanceCheckType.ActivePolicyOverlap);
        Assert.Equal(UnderwritingClearanceStatus.Blocked, overlap.Status);
        Assert.Equal("POL-2026-001", overlap.MatchedRecordLabel);
        Assert.Contains("overlaps", overlap.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateSubmissionAsync_PersistsLatestClearanceResults()
    {
        await using var db = CreateDb();
        var insuredId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        db.Add(CreateUser(userId));
        db.Add(CreateInsured(insuredId, userId, "Summit Timber"));
        db.Add(CreateSubmission(
            insuredId,
            userId,
            "SUB-EXISTING",
            new DateOnly(2026, 6, 1),
            SubmissionStatus.InProgress,
            PolicyLineOfBusiness.InlandMarine));
        var target = CreateSubmission(
            insuredId,
            userId,
            "SUB-TARGET",
            new DateOnly(2026, 6, 15),
            SubmissionStatus.New,
            PolicyLineOfBusiness.InlandMarine);
        db.Add(target);
        await db.SaveChangesAsync();
        var service = new UnderwritingClearanceService(db);

        await service.EvaluateSubmissionAsync(target.Id, userId);

        var saved = await db.Set<UnderwritingClearanceResult>().SingleAsync();
        Assert.Equal(target.Id, saved.SubmissionId);
        Assert.Equal(UnderwritingClearanceCheckType.DuplicateSubmission, saved.CheckType);
        Assert.Equal(UnderwritingClearanceStatus.Warning, saved.Status);
        Assert.Equal(userId, saved.ReviewedById);
        Assert.Equal("SUB-EXISTING", saved.MatchedRecordLabel);
    }

    [Fact]
    public async Task OverrideSubmissionAsync_RecordsBlockedClearanceOverrideAudit()
    {
        await using var db = CreateDb();
        var insuredId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var carrierId = Guid.NewGuid();
        db.AddRange(
            CreateUser(userId),
            CreateInsured(insuredId, userId, "Red Cedar Trucking"),
            new Carrier { Id = carrierId, Name = "Test Carrier", Naic = "12345" });
        var existingSubmission = CreateSubmission(
            insuredId,
            userId,
            "SUB-POLICY",
            new DateOnly(2026, 1, 1),
            SubmissionStatus.Bound,
            PolicyLineOfBusiness.CommercialAuto);
        var target = CreateSubmission(
            insuredId,
            userId,
            "SUB-TARGET",
            new DateOnly(2026, 6, 1),
            SubmissionStatus.New,
            PolicyLineOfBusiness.CommercialAuto);
        db.AddRange(existingSubmission, target);
        db.Add(CreatePolicy(
            existingSubmission.Id,
            carrierId,
            "POL-2026-001",
            new DateOnly(2026, 1, 1),
            new DateOnly(2027, 1, 1),
            PolicyLineOfBusiness.CommercialAuto));
        await db.SaveChangesAsync();
        var service = new UnderwritingClearanceService(db);
        await service.EvaluateSubmissionAsync(target.Id, userId);

        var result = await service.OverrideSubmissionAsync(target.Id, userId, "Existing policy is being cancelled before bind.");

        Assert.Equal(UnderwritingClearanceStatus.Warning, result.OverallStatus);
        var saved = await db.Set<UnderwritingClearanceResult>().SingleAsync();
        Assert.True(saved.IsOverridden);
        Assert.Equal(userId, saved.OverriddenById);
        Assert.NotNull(saved.OverriddenAt);
        Assert.Equal("Existing policy is being cancelled before bind.", saved.OverrideReason);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static User CreateUser(Guid userId) => new()
    {
        Id = userId,
        UserName = "clearance@sims.test",
        Email = "clearance@sims.test",
        FirstName = "Clearance",
        LastName = "User",
    };

    private static Insured CreateInsured(Guid insuredId, Guid userId, string name) => new()
    {
        Id = insuredId,
        InsuredType = InsuredType.Commercial,
        CompanyName = name,
        AddressLine1 = "100 Main St",
        City = "Tyler",
        State = "TX",
        ZipCode = "75701",
        CreatedById = userId,
    };

    private static Submission CreateSubmission(
        Guid insuredId,
        Guid userId,
        string submissionNumber,
        DateOnly effectiveDate,
        SubmissionStatus status,
        PolicyLineOfBusiness lob) => new()
    {
        Id = Guid.NewGuid(),
        SubmissionNumber = submissionNumber,
        InsuredId = insuredId,
        UnderwriterId = userId,
        EffectiveDate = effectiveDate,
        ExpirationDate = effectiveDate.AddYears(1),
        Status = status,
        LinesOfBusiness = $"[\"{lob}\"]",
        CreatedById = userId,
    };

    private static Policy CreatePolicy(
        Guid submissionId,
        Guid carrierId,
        string policyNumber,
        DateOnly effectiveDate,
        DateOnly expirationDate,
        PolicyLineOfBusiness lob) => new()
    {
        Id = Guid.NewGuid(),
        PolicyNumber = policyNumber,
        SubmissionId = submissionId,
        BoundQuoteId = Guid.NewGuid(),
        CarrierId = carrierId,
        LineOfBusiness = lob,
        EffectiveDate = effectiveDate,
        ExpirationDate = expirationDate,
        Status = PolicyStatus.Active,
        BoundDate = effectiveDate,
    };
}
