using Microsoft.EntityFrameworkCore;
using SIMS.Application.DTOs.CompanyLicenses;
using SIMS.Application.Services;
using SIMS.Infrastructure.Data;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class CompanyLicenseServiceTests
{
    private static UpsertCompanyLicenseRequest Valid(string holder = "Specialty Market Managers, LLC", string state = "TX") =>
        new(holder, "SL-12345", state, "Surplus Lines Broker", new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1),
            "1 Main St", null, "Austin", "TX", "78701", "USA", true, null);

    [Fact]
    public async Task CreateAsync_PersistsAndNormalizesState()
    {
        await using var db = CreateDb();
        var result = await new CompanyLicenseService(db).CreateAsync(Valid(state: "tx"));

        Assert.True(result.IsSuccess);
        Assert.Equal("TX", result.Value!.LicenseState);

        var all = await new CompanyLicenseService(db).GetAllAsync(includeInactive: true);
        Assert.Single(all);
    }

    [Fact]
    public async Task CreateAsync_RejectsMissingHolderAndBadState()
    {
        await using var db = CreateDb();
        var service = new CompanyLicenseService(db);

        var noHolder = await service.CreateAsync(Valid(holder: "  "));
        Assert.False(noHolder.IsSuccess);
        Assert.Equal("VALIDATION", noHolder.ErrorCode);

        var badState = await service.CreateAsync(Valid(state: "TEX"));
        Assert.False(badState.IsSuccess);
    }

    [Fact]
    public async Task UpdateAsync_ModifiesExisting()
    {
        await using var db = CreateDb();
        var created = await new CompanyLicenseService(db).CreateAsync(Valid());
        var updated = await new CompanyLicenseService(db).UpdateAsync(created.Value!.Id, Valid(holder: "Jeremiah O'Donovan"));

        Assert.True(updated.IsSuccess);
        Assert.Equal("Jeremiah O'Donovan", updated.Value!.HolderName);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletesAndHidesFromList()
    {
        await using var db = CreateDb();
        var created = await new CompanyLicenseService(db).CreateAsync(Valid());

        var deleted = await new CompanyLicenseService(db).DeleteAsync(created.Value!.Id);
        Assert.True(deleted.IsSuccess);

        var all = await new CompanyLicenseService(db).GetAllAsync(includeInactive: true);
        Assert.Empty(all);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }
}
