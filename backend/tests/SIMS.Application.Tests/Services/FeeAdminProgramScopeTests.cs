using Microsoft.EntityFrameworkCore;
using SIMS.Application.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Entities.Accounting;
using SIMS.Infrastructure.Data;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class FeeAdminProgramScopeTests
{
    [Fact]
    public async Task GetVersionsAsync_ReturnsProgramNameForProgramScopedFeeRules()
    {
        await using var db = CreateDb();
        var program = new ProgramConfiguration { Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        var fee = new FeeDefinition
        {
            Code = "MGA",
            DisplayName = "MGA Fee",
            FeeCategory = "PolicyFee",
            IsTaxable = false,
            CalculationOrder = 100,
            LedgerAccountId = 1,
        };
        db.AddRange(program, fee);
        await db.SaveChangesAsync();

        db.Add(new FeeRuleVersion
        {
            FeeDefinitionId = fee.Id,
            ProgramConfigurationId = program.Id,
            EffectiveDate = new DateOnly(2026, 1, 1),
            CalcType = "Flat",
            FlatAmount = 75m,
            SendToAccounting = true,
            ApplyAutomatically = true,
            InstallmentBehavior = "PerInstallment",
            RoundingMode = "NearestCent",
            PayableRouting = "NotPayable",
            CreatedBy = Guid.NewGuid(),
        });
        await db.SaveChangesAsync();

        var result = await new FeeAdminService(new TestServiceProvider(db)).GetVersionsAsync(fee.Id);

        var version = Assert.Single(result);
        Assert.Equal(program.Id, version.ProgramConfigurationId);
        Assert.Equal("Longleaf", version.ProgramName);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed class TestServiceProvider(DbContext db) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(DbContext) ? db : null;
    }
}
