using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SIMS.Application.DTOs.Accounting;
using SIMS.Application.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Entities.Accounting;
using SIMS.Domain.Enums;
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

    [Fact]
    public async Task CreateVersionAsync_RejectsProgramCarrierFromWrongProgramPath()
    {
        await using var db = CreateDb();
        var selectedProgram = new ProgramConfiguration { Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        var otherProgram = new ProgramConfiguration { Name = "Shuttlebee", Code = "SHUTTLEBEE", IsActive = true };
        var carrier = BuildCarrier();
        var fee = BuildFee("MGA", "MGA Fee", "PolicyFee", 100);
        db.AddRange(selectedProgram, otherProgram, carrier, fee);
        await db.SaveChangesAsync();

        db.Add(BuildProgramCarrier(otherProgram, carrier));
        await db.SaveChangesAsync();

        var request = ValidRequest(fee.Id) with
        {
            ProgramConfigurationId = selectedProgram.Id,
            CarrierId = carrier.Id,
            LineOfBusiness = null,
            StateCode = null,
            CalcType = "Flat",
            FlatAmount = 50m,
            PercentRate = null
        };

        var result = await new FeeAdminService(new TestServiceProvider(db)).CreateVersionAsync(Guid.NewGuid(), request);

        Assert.False(result.IsSuccess);
        Assert.Equal("PROGRAM_SCOPE_PATH_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task CreateVersionAsync_RejectsProgramLobFromWrongParentCarrierPath()
    {
        await using var db = CreateDb();
        var program = new ProgramConfiguration { Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        var selectedCarrier = BuildCarrier("BRACE");
        var otherCarrier = BuildCarrier("Pioneer");
        var fee = BuildFee("MGA", "MGA Fee", "PolicyFee", 100);
        db.AddRange(program, selectedCarrier, otherCarrier, fee);
        await db.SaveChangesAsync();

        var selectedProgramCarrier = BuildProgramCarrier(program, selectedCarrier);
        var otherProgramCarrier = BuildProgramCarrier(program, otherCarrier);
        db.AddRange(selectedProgramCarrier, otherProgramCarrier);
        await db.SaveChangesAsync();

        db.Add(BuildProgramLob(otherProgramCarrier, PolicyLineOfBusiness.GeneralLiability));
        await db.SaveChangesAsync();

        var request = ValidRequest(fee.Id) with
        {
            ProgramConfigurationId = program.Id,
            CarrierId = selectedCarrier.Id,
            LineOfBusiness = PolicyLineOfBusiness.GeneralLiability.ToString(),
            StateCode = null,
            CalcType = "Flat",
            FlatAmount = 50m,
            PercentRate = null
        };

        var result = await new FeeAdminService(new TestServiceProvider(db)).CreateVersionAsync(Guid.NewGuid(), request);

        Assert.False(result.IsSuccess);
        Assert.Equal("PROGRAM_SCOPE_PATH_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task CreateVersionAsync_RejectsProgramCarrierLobStateOutsideProgramSetupPath()
    {
        await using var db = CreateDb();
        var program = new ProgramConfiguration { Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        var carrier = BuildCarrier();
        var fee = BuildFee("SL_TAX", "Surplus Lines Tax", "Tax", 10);
        db.AddRange(program, carrier, fee);
        await db.SaveChangesAsync();

        var request = ValidRequest(fee.Id) with
        {
            ProgramConfigurationId = program.Id,
            CarrierId = carrier.Id,
            LineOfBusiness = PolicyLineOfBusiness.GeneralLiability.ToString(),
            StateCode = "TX"
        };

        var result = await new FeeAdminService(new TestServiceProvider(db)).CreateVersionAsync(Guid.NewGuid(), request);

        Assert.False(result.IsSuccess);
        Assert.Equal("PROGRAM_SCOPE_PATH_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task CreateVersionAsync_SavesCanonicalLobScopeForProgramCarrierLobAllStates()
    {
        await using var db = CreateDb();
        var program = new ProgramConfiguration { Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        var carrier = BuildCarrier();
        var fee = BuildFee("MGA", "MGA Fee", "PolicyFee", 100);
        db.AddRange(program, carrier, fee);
        await db.SaveChangesAsync();

        var programCarrier = BuildProgramCarrier(program, carrier);
        db.Add(programCarrier);
        await db.SaveChangesAsync();

        var programLob = BuildProgramLob(programCarrier, PolicyLineOfBusiness.GeneralLiability);
        db.Add(programLob);
        await db.SaveChangesAsync();

        var request = ValidRequest(fee.Id) with
        {
            ProgramConfigurationId = program.Id,
            CarrierId = carrier.Id,
            LineOfBusiness = "generalliability",
            StateCode = null,
            CalcType = "Flat",
            FlatAmount = 50m,
            PercentRate = null
        };

        var result = await new FeeAdminService(new TestServiceProvider(db)).CreateVersionAsync(Guid.NewGuid(), request);

        Assert.True(result.IsSuccess);
        var saved = await db.Set<FeeRuleVersion>().SingleAsync(v => v.Id == result.Value!.Id);
        Assert.Equal(programLob.Id, saved.ProgramCarrierLineOfBusinessId);
        Assert.Null(saved.ProgramCarrierId);
        Assert.Null(saved.ProgramCarrierLobStateId);
        Assert.Equal(program.Id, saved.ProgramConfigurationId);
        Assert.Equal(carrier.Id, saved.CarrierId);
        Assert.Equal(PolicyLineOfBusiness.GeneralLiability.ToString(), saved.LineOfBusiness);
        Assert.Null(saved.StateCode);
    }

    [Fact]
    public async Task CreateVersionAsync_SavesCanonicalStateScopeForProgramCarrierLobState()
    {
        await using var db = CreateDb();
        var program = new ProgramConfiguration { Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        var carrier = BuildCarrier();
        var fee = BuildFee("SL_TAX", "Surplus Lines Tax", "Tax", 10);
        db.AddRange(program, carrier, fee);
        await db.SaveChangesAsync();

        var programCarrier = BuildProgramCarrier(program, carrier);
        db.Add(programCarrier);
        await db.SaveChangesAsync();

        var programLob = BuildProgramLob(programCarrier, PolicyLineOfBusiness.GeneralLiability);
        db.Add(programLob);
        await db.SaveChangesAsync();

        var programState = BuildProgramState(programLob, "TX");
        db.Add(programState);
        await db.SaveChangesAsync();

        var request = ValidRequest(fee.Id) with
        {
            ProgramConfigurationId = program.Id,
            CarrierId = carrier.Id,
            LineOfBusiness = PolicyLineOfBusiness.GeneralLiability.ToString(),
            StateCode = " tx "
        };

        var result = await new FeeAdminService(new TestServiceProvider(db)).CreateVersionAsync(Guid.NewGuid(), request);

        Assert.True(result.IsSuccess);
        var saved = await db.Set<FeeRuleVersion>().SingleAsync(v => v.Id == result.Value!.Id);
        Assert.Equal(programState.Id, saved.ProgramCarrierLobStateId);
        Assert.Null(saved.ProgramCarrierId);
        Assert.Null(saved.ProgramCarrierLineOfBusinessId);
        Assert.Equal("TX", saved.StateCode);
    }

    [Fact]
    public async Task CreateVersionAsync_RejectsExpiredProgramPathForFeeEffectiveDate()
    {
        await using var db = CreateDb();
        var program = new ProgramConfiguration { Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        var carrier = BuildCarrier();
        var fee = BuildFee("MGA", "MGA Fee", "PolicyFee", 100);
        db.AddRange(program, carrier, fee);
        await db.SaveChangesAsync();

        var programCarrier = BuildProgramCarrier(program, carrier);
        programCarrier.ExpirationDate = new DateOnly(2026, 1, 31);
        db.Add(programCarrier);
        await db.SaveChangesAsync();

        var request = ValidRequest(fee.Id) with
        {
            ProgramConfigurationId = program.Id,
            CarrierId = carrier.Id,
            LineOfBusiness = null,
            StateCode = null,
            EffectiveDate = new DateOnly(2026, 2, 1),
            CalcType = "Flat",
            FlatAmount = 50m,
            PercentRate = null
        };

        var result = await new FeeAdminService(new TestServiceProvider(db)).CreateVersionAsync(Guid.NewGuid(), request);

        Assert.False(result.IsSuccess);
        Assert.Equal("PROGRAM_SCOPE_PATH_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task CreateVersionAsync_RejectsExpiredProgramLobPathForFeeEffectiveDate()
    {
        await using var db = CreateDb();
        var program = new ProgramConfiguration { Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        var carrier = BuildCarrier();
        var fee = BuildFee("MGA", "MGA Fee", "PolicyFee", 100);
        db.AddRange(program, carrier, fee);
        await db.SaveChangesAsync();

        var programCarrier = BuildProgramCarrier(program, carrier);
        db.Add(programCarrier);
        await db.SaveChangesAsync();

        var programLob = BuildProgramLob(programCarrier, PolicyLineOfBusiness.GeneralLiability);
        programLob.ExpirationDate = new DateOnly(2026, 1, 31);
        db.Add(programLob);
        await db.SaveChangesAsync();

        var request = ValidRequest(fee.Id) with
        {
            ProgramConfigurationId = program.Id,
            CarrierId = carrier.Id,
            LineOfBusiness = PolicyLineOfBusiness.GeneralLiability.ToString(),
            StateCode = null,
            EffectiveDate = new DateOnly(2026, 2, 1),
            CalcType = "Flat",
            FlatAmount = 50m,
            PercentRate = null
        };

        var result = await new FeeAdminService(new TestServiceProvider(db)).CreateVersionAsync(Guid.NewGuid(), request);

        Assert.False(result.IsSuccess);
        Assert.Equal("PROGRAM_SCOPE_PATH_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task CreateVersionAsync_RejectsExpiredProgramStatePathForFeeEffectiveDate()
    {
        await using var db = CreateDb();
        var program = new ProgramConfiguration { Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        var carrier = BuildCarrier();
        var fee = BuildFee("SL_TAX", "Surplus Lines Tax", "Tax", 10);
        db.AddRange(program, carrier, fee);
        await db.SaveChangesAsync();

        var programCarrier = BuildProgramCarrier(program, carrier);
        db.Add(programCarrier);
        await db.SaveChangesAsync();

        var programLob = BuildProgramLob(programCarrier, PolicyLineOfBusiness.GeneralLiability);
        db.Add(programLob);
        await db.SaveChangesAsync();

        var programState = BuildProgramState(programLob, "TX");
        programState.ExpirationDate = new DateOnly(2026, 1, 31);
        db.Add(programState);
        await db.SaveChangesAsync();

        var request = ValidRequest(fee.Id) with
        {
            ProgramConfigurationId = program.Id,
            CarrierId = carrier.Id,
            LineOfBusiness = PolicyLineOfBusiness.GeneralLiability.ToString(),
            StateCode = "TX",
            EffectiveDate = new DateOnly(2026, 2, 1)
        };

        var result = await new FeeAdminService(new TestServiceProvider(db)).CreateVersionAsync(Guid.NewGuid(), request);

        Assert.False(result.IsSuccess);
        Assert.Equal("PROGRAM_SCOPE_PATH_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task CreateVersionAsync_RejectsInvalidStateCode()
    {
        await using var db = CreateDb();
        var fee = BuildFee("SL_TAX", "Surplus Lines Tax", "Tax", 10);
        db.Add(fee);
        await db.SaveChangesAsync();

        var request = ValidRequest(fee.Id) with { StateCode = "TEX" };

        var result = await new FeeAdminService(new TestServiceProvider(db)).CreateVersionAsync(Guid.NewGuid(), request);

        Assert.False(result.IsSuccess);
        Assert.Equal("STATE_CODE_INVALID", result.ErrorCode);
        Assert.Equal("State code must be two characters.", result.ErrorMessage);
    }

    [Fact]
    public async Task CreateVersionAsync_NormalizesLineOfBusinessWithoutProgramScope()
    {
        await using var db = CreateDb();
        var fee = BuildFee("MGA", "MGA Fee", "PolicyFee", 100);
        db.Add(fee);
        await db.SaveChangesAsync();

        var request = ValidRequest(fee.Id) with
        {
            LineOfBusiness = " generalliability ",
            StateCode = "tx",
            CalcType = "Flat",
            FlatAmount = 50m,
            PercentRate = null
        };

        var result = await new FeeAdminService(new TestServiceProvider(db)).CreateVersionAsync(Guid.NewGuid(), request);

        Assert.True(result.IsSuccess);
        var saved = await db.Set<FeeRuleVersion>().SingleAsync(v => v.Id == result.Value!.Id);
        Assert.Equal(PolicyLineOfBusiness.GeneralLiability.ToString(), saved.LineOfBusiness);
        Assert.Equal("TX", saved.StateCode);
        Assert.Null(saved.ProgramCarrierId);
        Assert.Null(saved.ProgramCarrierLineOfBusinessId);
        Assert.Null(saved.ProgramCarrierLobStateId);
    }

    [Fact]
    public async Task CreateVersionAsync_RejectsInvalidLineOfBusinessWithoutProgramScope()
    {
        await using var db = CreateDb();
        var fee = BuildFee("MGA", "MGA Fee", "PolicyFee", 100);
        db.Add(fee);
        await db.SaveChangesAsync();

        var request = ValidRequest(fee.Id) with { LineOfBusiness = "NotALob" };

        var result = await new FeeAdminService(new TestServiceProvider(db)).CreateVersionAsync(Guid.NewGuid(), request);

        Assert.False(result.IsSuccess);
        Assert.Equal("LOB_INVALID", result.ErrorCode);
    }

    [Fact]
    public async Task CreateVersionAsync_RejectsNumericLineOfBusinessWithoutProgramScope()
    {
        await using var db = CreateDb();
        var fee = BuildFee("MGA", "MGA Fee", "PolicyFee", 100);
        db.Add(fee);
        await db.SaveChangesAsync();

        var request = ValidRequest(fee.Id) with { LineOfBusiness = "10" };

        var result = await new FeeAdminService(new TestServiceProvider(db)).CreateVersionAsync(Guid.NewGuid(), request);

        Assert.False(result.IsSuccess);
        Assert.Equal("LOB_INVALID", result.ErrorCode);
    }

    [Fact]
    public async Task SaveChangesAsync_RejectsProgramCarrierLobScopeWithoutCanonicalReference()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new SqliteFeeAdminDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var program = new ProgramConfiguration { Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        var carrier = BuildCarrier();
        var fee = BuildFee("MGA", "MGA Fee", "PolicyFee", 100);
        var account = new LedgerAccount { Id = 1, InternalCode = "4000", ExternalLabel = "Fees", AccountType = "Revenue" };
        db.Add(account);
        await db.SaveChangesAsync();

        db.AddRange(program, carrier, fee);
        await db.SaveChangesAsync();

        db.Add(new FeeRuleVersion
        {
            FeeDefinitionId = fee.Id,
            ProgramConfigurationId = program.Id,
            CarrierId = carrier.Id,
            LineOfBusiness = PolicyLineOfBusiness.GeneralLiability.ToString(),
            EffectiveDate = new DateOnly(2026, 1, 1),
            CalcType = "Flat",
            FlatAmount = 50m,
            SendToAccounting = true,
            ApplyAutomatically = true,
            InstallmentBehavior = "PerInstallment",
            RoundingMode = "NearestCent",
            PayableRouting = "NotPayable",
            CreatedBy = Guid.NewGuid(),
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static Carrier BuildCarrier(string name = "BRACE") =>
        new()
        {
            Name = name,
            IsActive = true
        };

    private static FeeDefinition BuildFee(string code, string displayName, string category, int order) =>
        new()
        {
            Code = code,
            DisplayName = displayName,
            FeeCategory = category,
            IsTaxable = false,
            CalculationOrder = order,
            LedgerAccountId = 1,
        };

    private static ProgramCarrier BuildProgramCarrier(ProgramConfiguration program, Carrier carrier) =>
        new()
        {
            ProgramConfigurationId = program.Id,
            CarrierId = carrier.Id,
            IsActive = true,
            EffectiveDate = new DateOnly(2026, 1, 1)
        };

    private static ProgramCarrierLineOfBusiness BuildProgramLob(ProgramCarrier programCarrier, PolicyLineOfBusiness lob) =>
        new()
        {
            ProgramCarrierId = programCarrier.Id,
            LineOfBusiness = lob,
            IsActive = true,
            EffectiveDate = new DateOnly(2026, 1, 1)
        };

    private static ProgramCarrierLobState BuildProgramState(ProgramCarrierLineOfBusiness programLob, string stateCode) =>
        new()
        {
            ProgramCarrierLineOfBusinessId = programLob.Id,
            StateCode = stateCode,
            IsActive = true,
            EffectiveDate = new DateOnly(2026, 1, 1)
        };

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

    private sealed class SqliteFeeAdminDbContext(DbContextOptions<ApplicationDbContext> options)
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
                    if (property.GetDefaultValueSql()?.Contains("::", StringComparison.Ordinal) == true)
                        property.SetDefaultValueSql(null);
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
    }
}
