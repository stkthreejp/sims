using Microsoft.EntityFrameworkCore;
using SIMS.Application.DTOs.Accounting;
using SIMS.Application.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Entities.Accounting;
using SIMS.Domain.Enums;
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

    [Fact]
    public async Task CalculateAsync_NormalizesContextStateAndLineOfBusinessBeforeMatching()
    {
        await using var db = CreateDb();
        var fee = BuildFee("MGA", "MGA Fee", "PolicyFee", 100);
        db.Add(fee);
        await db.SaveChangesAsync();

        var request = new CreateFeeRuleVersionRequest(
            FeeDefinitionId: fee.Id,
            ProgramConfigurationId: null,
            CarrierId: null,
            CompanyId: null,
            ProducerId: null,
            LineOfBusiness: " generalliability ",
            StateCode: " tx ",
            City: null,
            LicenseType: null,
            EffectiveDate: new DateOnly(2026, 1, 1),
            CalcType: "Flat",
            FlatAmount: 33m,
            PercentRate: null,
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

        var created = await new FeeAdminService(new TestServiceProvider(db)).CreateVersionAsync(Guid.NewGuid(), request);
        Assert.True(created.IsSuccess);

        var service = new FeeCalculationService(new TestServiceProvider(db));
        var result = await service.CalculateAsync(BuildContext(
            null,
            null,
            " generalliability ",
            " tx "));

        var line = Assert.Single(result.Lines);
        Assert.Equal(33m, line.Amount);
        Assert.Equal(created.Value!.Id, line.FeeRuleVersionId);
    }

    [Fact]
    public async Task CalculateAsync_AppliesProgramCarrierLobAllStateDefault()
    {
        await using var db = CreateDb();
        var program = new ProgramConfiguration { Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        var carrier = new Carrier { Name = "BRACE", IsActive = true };
        var fee = BuildFee("MGA", "MGA Fee", "PolicyFee", 100);
        db.AddRange(program, carrier, fee);
        await db.SaveChangesAsync();

        var programCarrier = new ProgramCarrier
        {
            ProgramConfigurationId = program.Id,
            CarrierId = carrier.Id,
            IsActive = true,
            EffectiveDate = new DateOnly(2026, 1, 1)
        };
        db.Add(programCarrier);
        await db.SaveChangesAsync();

        var programLob = new ProgramCarrierLineOfBusiness
        {
            ProgramCarrierId = programCarrier.Id,
            LineOfBusiness = PolicyLineOfBusiness.GeneralLiability,
            IsActive = true,
            EffectiveDate = new DateOnly(2026, 1, 1)
        };
        db.Add(programLob);
        await db.SaveChangesAsync();

        var rule = BuildFlatFeeRule(fee.Id, program.Id, 40m);
        rule.CarrierId = carrier.Id;
        rule.LineOfBusiness = PolicyLineOfBusiness.GeneralLiability.ToString();
        rule.ProgramCarrierLineOfBusinessId = programLob.Id;
        db.Add(rule);
        await db.SaveChangesAsync();

        var service = new FeeCalculationService(new TestServiceProvider(db));
        var result = await service.CalculateAsync(BuildContext(
            program.Id,
            carrier.Id,
            PolicyLineOfBusiness.GeneralLiability.ToString(),
            "TX"));

        var line = Assert.Single(result.Lines);
        Assert.Equal(40m, line.Amount);
        Assert.Equal(rule.Id, line.FeeRuleVersionId);
    }

    [Fact]
    public async Task CalculateAsync_PrefersStateSpecificProgramFeeOverLobDefault()
    {
        await using var db = CreateDb();
        var program = new ProgramConfiguration { Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        var carrier = new Carrier { Name = "BRACE", IsActive = true };
        var fee = BuildFee("SL_TAX", "Surplus Lines Tax", "Tax", 10);
        db.AddRange(program, carrier, fee);
        await db.SaveChangesAsync();

        var programCarrier = new ProgramCarrier
        {
            ProgramConfigurationId = program.Id,
            CarrierId = carrier.Id,
            IsActive = true,
            EffectiveDate = new DateOnly(2026, 1, 1)
        };
        db.Add(programCarrier);
        await db.SaveChangesAsync();

        var programLob = new ProgramCarrierLineOfBusiness
        {
            ProgramCarrierId = programCarrier.Id,
            LineOfBusiness = PolicyLineOfBusiness.GeneralLiability,
            IsActive = true,
            EffectiveDate = new DateOnly(2026, 1, 1)
        };
        db.Add(programLob);
        await db.SaveChangesAsync();

        var programState = new ProgramCarrierLobState
        {
            ProgramCarrierLineOfBusinessId = programLob.Id,
            StateCode = "TX",
            IsActive = true,
            EffectiveDate = new DateOnly(2026, 1, 1)
        };
        db.Add(programState);
        await db.SaveChangesAsync();

        var lobDefault = BuildFlatFeeRule(fee.Id, program.Id, 40m);
        lobDefault.CarrierId = carrier.Id;
        lobDefault.LineOfBusiness = PolicyLineOfBusiness.GeneralLiability.ToString();
        lobDefault.ProgramCarrierLineOfBusinessId = programLob.Id;

        var stateSpecific = BuildFlatFeeRule(fee.Id, program.Id, 100m);
        stateSpecific.CarrierId = carrier.Id;
        stateSpecific.LineOfBusiness = PolicyLineOfBusiness.GeneralLiability.ToString();
        stateSpecific.StateCode = "TX";
        stateSpecific.ProgramCarrierLobStateId = programState.Id;

        db.AddRange(lobDefault, stateSpecific);
        await db.SaveChangesAsync();

        var service = new FeeCalculationService(new TestServiceProvider(db));
        var result = await service.CalculateAsync(BuildContext(
            program.Id,
            carrier.Id,
            PolicyLineOfBusiness.GeneralLiability.ToString(),
            "TX"));

        var line = Assert.Single(result.Lines);
        Assert.Equal(100m, line.Amount);
        Assert.Equal(stateSpecific.Id, line.FeeRuleVersionId);
    }

    private static PolicyContext BuildContext(Guid? programId) =>
        BuildContext(programId, null, "InlandMarine", "TX");

    private static PolicyContext BuildContext(Guid? programId, Guid? carrierId, string? lineOfBusiness, string stateCode) =>
        new(
            EffectiveDate: new DateOnly(2026, 1, 1),
            GrossPremium: 1000m,
            StateCode: stateCode,
            IsEndorsement: false,
            IsFilingState: true,
            CarrierId: carrierId,
            CompanyId: null,
            ProducerId: null,
            LineOfBusiness: lineOfBusiness,
            City: null,
            LicenseType: "Non-Admitted",
            ProgramConfigurationId: programId);

    private static FeeDefinition BuildFee(string code, string displayName, string category, int order) =>
        new()
        {
            Code = code,
            DisplayName = displayName,
            FeeCategory = category,
            IsTaxable = false,
            CalculationOrder = order,
            LedgerAccountId = 1
        };

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
