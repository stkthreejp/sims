using Microsoft.EntityFrameworkCore;
using SIMS.Application.DTOs.Claims;
using SIMS.Application.Security;
using SIMS.Application.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;
using SIMS.Infrastructure.Data;
using Xunit;
using Claim = SIMS.Domain.Entities.Claim;

namespace SIMS.Application.Tests.Services;

public class ClaimsServiceTests
{
    [Fact]
    public async Task GetClaims_RestrictedUserSeesOnlyOwnPolicyClaims()
    {
        await using var db = CreateDb();
        var (ownerA, policyA) = await SeedPolicyAsync(db, "POL-A", "Alpha Trucking");
        var (_, policyB) = await SeedPolicyAsync(db, "POL-B", "Beta Trucking");

        db.AddRange(
            ClaimFor(policyA, "CLM-A1"),
            ClaimFor(policyB, "CLM-B1"),
            ClaimFor(null, "CLM-UNLINKED"));
        await db.SaveChangesAsync();

        var service = new ClaimsService(db);

        var restricted = await service.GetClaimsAsync(new UserAccessScope(ownerA, false));
        Assert.Single(restricted);
        Assert.Equal("CLM-A1", restricted[0].ClaimNumber);

        var all = await service.GetClaimsAsync(UserAccessScope.All(ownerA));
        Assert.Equal(3, all.Count);
    }

    [Fact]
    public async Task GetClaim_RestrictedUserCannotReadForeignClaim()
    {
        await using var db = CreateDb();
        var (ownerA, _) = await SeedPolicyAsync(db, "POL-A", "Alpha Trucking");
        var (_, policyB) = await SeedPolicyAsync(db, "POL-B", "Beta Trucking");
        var claim = ClaimFor(policyB, "CLM-B1");
        db.Add(claim);
        await db.SaveChangesAsync();

        var service = new ClaimsService(db);
        var result = await service.GetClaimAsync(claim.Id, new UserAccessScope(ownerA, false));

        Assert.False(result.IsSuccess);
        Assert.Equal("CLAIM_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task GetLossRun_ForeignInsured_ReturnsAccessDenied()
    {
        await using var db = CreateDb();
        var (ownerA, _) = await SeedPolicyAsync(db, "POL-A", "Alpha Trucking");
        var (_, policyB) = await SeedPolicyAsync(db, "POL-B", "Beta Trucking");

        var service = new ClaimsService(db);
        var result = await service.GetLossRunAsync(
            policyB.Submission.InsuredId, null, new DateOnly(2026, 6, 1),
            new UserAccessScope(ownerA, false));

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessDataAccess.AccessDeniedCode, result.ErrorCode);
    }

    [Fact]
    public async Task Import_OlderValuation_DoesNotRegressCurrentValues()
    {
        await using var db = CreateDb();
        var (owner, policy) = await SeedPolicyAsync(db, "POL-A", "Alpha Trucking");
        var service = new ClaimsService(db);

        var may = await service.ImportClaimsAsync(
            ImportRequest(new DateOnly(2026, 5, 31), Row(policy.PolicyNumber, "CLM-1", lossPaid: 100m)), owner);
        Assert.True(may.IsSuccess);

        var april = await service.ImportClaimsAsync(
            ImportRequest(new DateOnly(2026, 4, 30), Row(policy.PolicyNumber, "CLM-1", lossPaid: 50m)), owner);
        Assert.True(april.IsSuccess);

        var claim = await db.Set<Claim>().SingleAsync(c => c.ClaimNumber == "CLM-1");
        Assert.Equal(100m, claim.Paid);
        Assert.Equal(new DateOnly(2026, 5, 31), claim.LastValuationDate);

        var snapshots = await db.Set<ClaimValuation>().Where(v => v.ClaimId == claim.Id).ToListAsync();
        Assert.Equal(2, snapshots.Count);
        Assert.Equal(50m, snapshots.Single(s => s.ValuationDate == new DateOnly(2026, 4, 30)).Paid);
        Assert.Equal(100m, snapshots.Single(s => s.ValuationDate == new DateOnly(2026, 5, 31)).Paid);
    }

    [Fact]
    public async Task GetLossRun_ValuesClaimsFromSnapshotAsOfDate()
    {
        await using var db = CreateDb();
        var (owner, policy) = await SeedPolicyAsync(db, "POL-A", "Alpha Trucking");
        var service = new ClaimsService(db);

        await service.ImportClaimsAsync(
            ImportRequest(new DateOnly(2026, 4, 30), Row(policy.PolicyNumber, "CLM-1", lossPaid: 50m)), owner);
        await service.ImportClaimsAsync(
            ImportRequest(new DateOnly(2026, 5, 31), Row(policy.PolicyNumber, "CLM-1", lossPaid: 100m)), owner);

        var midMay = await service.GetLossRunAsync(null, policy.Id, new DateOnly(2026, 5, 15), UserAccessScope.All(owner));
        Assert.True(midMay.IsSuccess);
        var claim = Assert.Single(midMay.Value!.Claims);
        Assert.Equal(50m, claim.Paid);
        Assert.Equal(new DateOnly(2026, 4, 30), claim.LastValuationDate);
        Assert.Equal(50m, midMay.Value.TotalPaid);

        var june = await service.GetLossRunAsync(null, policy.Id, new DateOnly(2026, 6, 1), UserAccessScope.All(owner));
        Assert.Equal(100m, Assert.Single(june.Value!.Claims).Paid);

        // Before the first valuation the claim was not yet valued
        var march = await service.GetLossRunAsync(null, policy.Id, new DateOnly(2026, 3, 31), UserAccessScope.All(owner));
        Assert.Empty(march.Value!.Claims);
    }

    [Fact]
    public async Task Import_SkipsRowsCollidingWithManualClaims()
    {
        await using var db = CreateDb();
        var (owner, policy) = await SeedPolicyAsync(db, "POL-A", "Alpha Trucking");
        var manual = ClaimFor(policy, "CLM-1");
        manual.IsManualEntry = true;
        manual.Paid = 999m;
        db.Add(manual);
        await db.SaveChangesAsync();

        var service = new ClaimsService(db);
        var result = await service.ImportClaimsAsync(
            ImportRequest(new DateOnly(2026, 5, 31), Row(policy.PolicyNumber, "CLM-1", lossPaid: 1m)), owner);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.SkippedCount);
        Assert.Equal(0, result.Value.UpdatedCount);

        var claim = await db.Set<Claim>().SingleAsync(c => c.ClaimNumber == "CLM-1");
        Assert.Equal(999m, claim.Paid);
        Assert.True(claim.IsManualEntry);
    }

    [Fact]
    public async Task Import_MapsLossAndExpenseColumnsSeparately()
    {
        await using var db = CreateDb();
        var (owner, policy) = await SeedPolicyAsync(db, "POL-A", "Alpha Trucking");
        var service = new ClaimsService(db);

        var row = Row(policy.PolicyNumber, "CLM-1", lossPaid: 10m);
        row.TotalExpPaid = 5m;
        row.TotalOsLoss = 20m;
        row.TotalOsExp = 3m;
        row.TotalIncurred = null;

        await service.ImportClaimsAsync(ImportRequest(new DateOnly(2026, 5, 31), row), owner);

        var claim = await db.Set<Claim>().SingleAsync(c => c.ClaimNumber == "CLM-1");
        Assert.Equal(10m, claim.Paid);
        Assert.Equal(20m, claim.Reserved);
        Assert.Equal(8m, claim.Expense);
        Assert.Equal(38m, claim.Incurred);
    }

    [Fact]
    public async Task Import_RejectsOversizeBatch()
    {
        await using var db = CreateDb();
        var service = new ClaimsService(db);

        var request = new ImportClaimsRequest
        {
            FileName = "huge.csv",
            ValuationDate = new DateOnly(2026, 5, 31),
            Rows = Enumerable.Range(0, ClaimsService.MaxImportRows + 1)
                .Select(i => new UnifiedClaimImportRow { ClaimNumber = $"CLM-{i}" })
                .ToList(),
        };

        var result = await service.ImportClaimsAsync(request, Guid.NewGuid());
        Assert.False(result.IsSuccess);
        Assert.Equal("IMPORT_TOO_LARGE", result.ErrorCode);
    }

    [Fact]
    public async Task Update_ImportedClaim_FinancialsAreFeedOwned()
    {
        await using var db = CreateDb();
        var (owner, policy) = await SeedPolicyAsync(db, "POL-A", "Alpha Trucking");
        var imported = ClaimFor(policy, "CLM-1");
        imported.IsManualEntry = false;
        imported.Paid = 100m;
        imported.Incurred = 100m;
        db.Add(imported);
        await db.SaveChangesAsync();

        var service = new ClaimsService(db);
        var result = await service.UpdateClaimAsync(imported.Id, new UpsertClaimRequest
        {
            ClaimNumber = "CLM-1",
            DateOfLoss = imported.DateOfLoss,
            ReportDate = imported.ReportDate,
            AdjusterName = "New Adjuster",
            Paid = 5m,
            LastValuationDate = imported.LastValuationDate,
        }, owner, UserAccessScope.All(owner));

        Assert.True(result.IsSuccess);
        var claim = await db.Set<Claim>().SingleAsync(c => c.Id == imported.Id);
        Assert.Equal(100m, claim.Paid);
        Assert.Equal("New Adjuster", claim.AdjusterName);
        Assert.Equal(owner, claim.UpdatedById);
    }

    // ── fixtures ────────────────────────────────────────────────────────────────

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task<(Guid ownerId, Policy policy)> SeedPolicyAsync(
        ApplicationDbContext db, string policyNumber, string insuredName)
    {
        var ownerId = Guid.NewGuid();
        var carrier = new Carrier { Id = Guid.NewGuid(), Name = $"Carrier {policyNumber}", IsActive = true };
        var insured = new Insured
        {
            Id = Guid.NewGuid(),
            InsuredType = InsuredType.Commercial,
            CompanyName = insuredName,
            State = "TX",
            CreatedById = ownerId,
        };
        var submission = new Submission
        {
            Id = Guid.NewGuid(),
            SubmissionNumber = $"SUB-{policyNumber}",
            InsuredId = insured.Id,
            Insured = insured,
            UnderwriterId = ownerId,
            CreatedById = ownerId,
            Status = SubmissionStatus.Quoted,
        };
        var quote = new Quote
        {
            Id = Guid.NewGuid(),
            QuoteNumber = $"QTE-{policyNumber}",
            SubmissionId = submission.Id,
            Submission = submission,
            CarrierId = carrier.Id,
            Carrier = carrier,
            LineOfBusiness = PolicyLineOfBusiness.InlandMarine,
            Status = QuoteStatus.Bound,
            EffectiveDate = new DateOnly(2026, 1, 1),
            ExpirationDate = new DateOnly(2027, 1, 1),
            PremiumAmount = 900m,
            TotalPremium = 1000m,
            CreatedById = ownerId,
        };
        var policy = new Policy
        {
            Id = Guid.NewGuid(),
            PolicyNumber = policyNumber,
            SubmissionId = submission.Id,
            Submission = submission,
            BoundQuoteId = quote.Id,
            BoundQuote = quote,
            CarrierId = carrier.Id,
            Carrier = carrier,
            LineOfBusiness = quote.LineOfBusiness,
            EffectiveDate = quote.EffectiveDate,
            ExpirationDate = quote.ExpirationDate,
            PremiumAmount = quote.PremiumAmount,
            TotalPremium = quote.TotalPremium,
            Status = PolicyStatus.Active,
            BoundDate = new DateOnly(2026, 1, 1),
        };

        db.AddRange(carrier, insured, submission, quote, policy);
        await db.SaveChangesAsync();
        return (ownerId, policy);
    }

    private static Claim ClaimFor(Policy? policy, string claimNumber) => new()
    {
        PolicyId = policy?.Id,
        PolicyNumber = policy?.PolicyNumber,
        InsuredId = policy?.Submission?.InsuredId,
        ClaimNumber = claimNumber,
        SourcePolicyReference = policy?.PolicyNumber ?? "EXT-REF",
        DateOfLoss = new DateOnly(2026, 2, 1),
        ReportDate = new DateOnly(2026, 2, 5),
        Status = ClaimStatus.Open,
        LastValuationDate = new DateOnly(2026, 3, 1),
        IsManualEntry = false,
    };

    private static ImportClaimsRequest ImportRequest(DateOnly valuationDate, params UnifiedClaimImportRow[] rows) => new()
    {
        FileName = "test.csv",
        CarrierName = "Test Carrier",
        ValuationDate = valuationDate,
        Rows = rows.ToList(),
    };

    private static UnifiedClaimImportRow Row(string policyNumber, string claimNumber, decimal lossPaid) => new()
    {
        ClaimNumber = claimNumber,
        CarrierPolicyNum = policyNumber,
        DateOfClaim = "2026-02-01",
        DateReported = "2026-02-05",
        ClaimStatusDesc = "Open",
        TotalLossPaid = lossPaid,
        TotalIncurred = null,
    };
}
