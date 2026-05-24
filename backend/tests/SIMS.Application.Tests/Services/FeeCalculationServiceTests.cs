using Microsoft.EntityFrameworkCore;
using SIMS.Application.DTOs.Accounting;
using SIMS.Application.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Entities.Accounting;
using SIMS.Infrastructure.Data;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class FeeCalculationServiceTests
{
    [Fact]
    public async Task CalculateAsync_PrefersProgramSpecificFeeRuleOverAllProgramDefault()
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
            LedgerAccountId = 1
        };
        db.AddRange(program, fee);
        await db.SaveChangesAsync();

        db.AddRange(
            BuildFlatFeeRule(fee.Id, null, 25m),
            BuildFlatFeeRule(fee.Id, program.Id, 75m));
        await db.SaveChangesAsync();

        var service = new FeeCalculationService(new TestServiceProvider(db));
        var result = await service.CalculateAsync(BuildContext(program.Id));

        var line = Assert.Single(result.Lines);
        Assert.Equal(75m, line.Amount);
    }

    [Fact]
    public async Task CalculateAsync_UsesAllProgramFeeRuleWhenNoProgramSpecificRuleMatches()
    {
        await using var db = CreateDb();
        var longleaf = new ProgramConfiguration { Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        var shuttlebee = new ProgramConfiguration { Name = "Shuttlebee", Code = "SHUTTLEBEE", IsActive = true };
        var fee = new FeeDefinition
        {
            Code = "MGA",
            DisplayName = "MGA Fee",
            FeeCategory = "PolicyFee",
            IsTaxable = false,
            CalculationOrder = 100,
            LedgerAccountId = 1
        };
        db.AddRange(longleaf, shuttlebee, fee);
        await db.SaveChangesAsync();

        db.AddRange(
            BuildFlatFeeRule(fee.Id, null, 25m),
            BuildFlatFeeRule(fee.Id, longleaf.Id, 75m));
        await db.SaveChangesAsync();

        var service = new FeeCalculationService(new TestServiceProvider(db));
        var result = await service.CalculateAsync(BuildContext(shuttlebee.Id));

        var line = Assert.Single(result.Lines);
        Assert.Equal(25m, line.Amount);
    }

    private static PolicyContext BuildContext(Guid? programId) =>
        new(
            EffectiveDate: new DateOnly(2026, 1, 1),
            GrossPremium: 1000m,
            StateCode: "TX",
            IsEndorsement: false,
            IsFilingState: true,
            CarrierId: null,
            CompanyId: null,
            ProducerId: null,
            LineOfBusiness: "InlandMarine",
            City: null,
            LicenseType: "Non-Admitted",
            ProgramConfigurationId: programId);

    private static FeeRuleVersion BuildFlatFeeRule(long feeDefinitionId, Guid? programId, decimal flatAmount) =>
        new()
        {
            FeeDefinitionId = feeDefinitionId,
            ProgramConfigurationId = programId,
            EffectiveDate = new DateOnly(2026, 1, 1),
            CalcType = "Flat",
            FlatAmount = flatAmount,
            SendToAccounting = true,
            ApplyAutomatically = true,
            InstallmentBehavior = "PerInstallment",
            RoundingMode = "NearestCent",
            PayableRouting = "NotPayable",
            CreatedBy = Guid.NewGuid()
        };

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed class TestServiceProvider : IServiceProvider
    {
        private readonly DbContext _db;

        public TestServiceProvider(DbContext db) => _db = db;

        public object? GetService(Type serviceType) =>
            serviceType == typeof(DbContext) ? _db : null;
    }
}
