using Microsoft.EntityFrameworkCore;
using SIMS.Application.DTOs.Accounting;
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

    [Fact]
    public async Task CreateVersionAsync_RejectsEntityPayableWithoutPayee()
    {
        await using var db = CreateDb();
        var fee = new FeeDefinition
        {
            Code = "SL_TAX",
            DisplayName = "Surplus Lines Tax",
            FeeCategory = "Tax",
            IsTaxable = false,
            CalculationOrder = 10,
            LedgerAccountId = 1,
        };
        db.Add(fee);
        await db.SaveChangesAsync();

        var request = ValidRequest(fee.Id) with
        {
            PayableRouting = "Entity",
            PayablePayeeId = null
        };

        var result = await new FeeAdminService(new TestServiceProvider(db)).CreateVersionAsync(Guid.NewGuid(), request);

        Assert.False(result.IsSuccess);
        Assert.Equal("PAYABLE_PAYEE_REQUIRED", result.ErrorCode);
    }

    [Fact]
    public async Task CreateVersionAsync_AllowsEntityPayableWithActivePayee()
    {
        await using var db = CreateDb();
        var fee = new FeeDefinition
        {
            Code = "SL_TAX",
            DisplayName = "Surplus Lines Tax",
            FeeCategory = "Tax",
            IsTaxable = false,
            CalculationOrder = 10,
            LedgerAccountId = 1,
        };
        var payee = new Payee
        {
            Name = "State Filing Vendor",
            PayeeType = "TaxFilingService",
            IsActive = true,
        };
        db.AddRange(fee, payee);
        await db.SaveChangesAsync();

        var request = ValidRequest(fee.Id) with
        {
            PayableRouting = "Entity",
            PayablePayeeId = payee.Id
        };

        var result = await new FeeAdminService(new TestServiceProvider(db)).CreateVersionAsync(Guid.NewGuid(), request);

        Assert.True(result.IsSuccess);
        Assert.Equal("Entity", result.Value!.PayableRouting);
        Assert.Equal(payee.Id, result.Value.PayablePayeeId);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static CreateFeeRuleVersionRequest ValidRequest(long feeDefinitionId) =>
        new(
            FeeDefinitionId: feeDefinitionId,
            ProgramConfigurationId: null,
            CarrierId: null,
            CompanyId: null,
            ProducerId: null,
            LineOfBusiness: null,
            StateCode: "TX",
            City: null,
            LicenseType: null,
            EffectiveDate: new DateOnly(2026, 1, 1),
            CalcType: "Percent",
            FlatAmount: null,
            PercentRate: 0.0485m,
            PercentOfNet: false,
            MinimumAmount: null,
            MaxPercent: null,
            MaxAmount: null,
            Commissionable: false,
            InstallmentBehavior: "PerInstallment",
            SplitByParticipation: false,
            FullyEarned: false,
            FullyEarnedDays: null,
            ExcludeTerrorism: false,
            MultiplyByLocations: false,
            MultiplyByVehicles: false,
            SendToAccounting: true,
            ApplyOnlyOnce: false,
            MandatoryCharge: true,
            ApplyAutomatically: true,
            ApplyWhenPackagePolicyOnly: false,
            DoNotApplyWhenPackagePolicyOnly: false,
            ApplyToChildLines: false,
            OnlyAppliesToIssuanceState: true,
            AppliesToFlatCancellations: false,
            PremiumMinThreshold: null,
            PremiumMaxThreshold: null,
            PremiumThresholdBasis: null,
            StateCountMin: null,
            StateCountMax: null,
            RoundingMode: "NearestCent",
            ExcludeWhenNotFiling: false,
            ExcludeOnEndorsements: false,
            ExcludeOnRenewal: false,
            ExcludeOnOriginalBinder: false,
            ExcludeOnMultiCarrierPolicy: false,
            PayHomeState: false,
            ExcludedPolicyTransactionTypes: null,
            PayableRouting: "NotPayable",
            PayablePayeeId: null,
            MasterPayeeWhenHomeState: false,
            Notes: null,
            PremiumBrackets: []);

    private sealed class TestServiceProvider(DbContext db) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(DbContext) ? db : null;
    }
}
