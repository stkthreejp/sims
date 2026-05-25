using Microsoft.EntityFrameworkCore;
using SIMS.Application.DTOs.Bordereaux;
using SIMS.Application.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;
using SIMS.Infrastructure.Data;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class BordereauxServiceTests
{
    [Fact]
    public async Task CreateProfileAsync_StoresLondonBdxAccountCurrentProfile()
    {
        await using var db = CreateDb();
        var (program, carrier) = await SeedProgramCarrierAsync(db);
        var service = new BordereauxService(db);

        var result = await service.CreateProfileAsync(new UpsertBordereauxProfileRequest(
            Name: " BRACE London BDX ",
            ProgramConfigurationId: program.Id,
            CarrierId: carrier.Id,
            LineOfBusiness: PolicyLineOfBusiness.GeneralLiability,
            StateCode: " ms ",
            ReportType: BordereauxReportType.Premium,
            Frequency: BordereauxFrequency.Monthly,
            OutputFormat: BordereauxOutputFormat.Xlsx,
            DateBasis: BordereauxDateBasis.EffectiveOrBoundDateGreater,
            RequiresAccountCurrent: true,
            IsActive: true,
            RequiredTabsJson: """["General Liability (Section 1)","Auto Veh Info","IM Unit Info","Acct Current"]""",
            RequiredColumnsJson: """["Certificate Ref","Gross premium paid this time","Net Premium to London in original currency"]""",
            MappingRulesJson: """{"commissionBasis":"commissionPlusBrokerage"}""",
            StaticValuesJson: """{"umr":"BRACE-SMM-2025-LOGGING","coverholderPin":"USA00060"}""",
            ValidationRulesJson: """{"requireReconciliation":true}""",
            IncludedTransactionTypesJson: """["NewBusiness","Endorsement"]""",
            Notes: " Monthly London package "));

        Assert.True(result.IsSuccess);
        Assert.Equal("BRACE London BDX", result.Value!.Name);
        Assert.Equal("MS", result.Value.StateCode);
        Assert.True(result.Value.RequiresAccountCurrent);
        Assert.Contains("Acct Current", result.Value.RequiredTabsJson);
        Assert.Equal("Monthly London package", result.Value.Notes);
    }

    [Fact]
    public async Task CreateProfileAsync_RejectsInvalidJsonConfiguration()
    {
        await using var db = CreateDb();
        var (program, carrier) = await SeedProgramCarrierAsync(db);
        var service = new BordereauxService(db);

        var result = await service.CreateProfileAsync(ValidRequest(program.Id, carrier.Id) with
        {
            MappingRulesJson = "{not-json",
        });

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_MAPPING_RULES_JSON", result.ErrorCode);
    }

    [Fact]
    public async Task CreateProfileAsync_RejectsDuplicateActiveScope()
    {
        await using var db = CreateDb();
        var (program, carrier) = await SeedProgramCarrierAsync(db);
        var service = new BordereauxService(db);

        await service.CreateProfileAsync(ValidRequest(program.Id, carrier.Id));
        var duplicate = await service.CreateProfileAsync(ValidRequest(program.Id, carrier.Id) with
        {
            Name = "Duplicate BRACE London BDX",
        });

        Assert.False(duplicate.IsSuccess);
        Assert.Equal("DUPLICATE_ACTIVE_PROFILE", duplicate.ErrorCode);
    }

    [Fact]
    public async Task GetProfilesAsync_FiltersByProgramAndActiveStatus()
    {
        await using var db = CreateDb();
        var (program, carrier) = await SeedProgramCarrierAsync(db);
        var otherProgram = new ProgramConfiguration { Name = "Shuttlebee", Code = "SHUTTLEBEE", IsActive = true };
        db.Add(otherProgram);
        await db.SaveChangesAsync();
        var service = new BordereauxService(db);

        await service.CreateProfileAsync(ValidRequest(program.Id, carrier.Id));
        await service.CreateProfileAsync(ValidRequest(otherProgram.Id, carrier.Id) with
        {
            Name = "Other Program",
            IsActive = false,
        });

        var active = await service.GetProfilesAsync(programId: program.Id);
        var allForOtherProgram = await service.GetProfilesAsync(programId: otherProgram.Id, includeInactive: true);

        Assert.Single(active);
        Assert.Equal(program.Id, active[0].ProgramConfigurationId);
        Assert.Single(allForOtherProgram);
        Assert.False(allForOtherProgram[0].IsActive);
    }

    [Fact]
    public async Task UpdateProfileAsync_ChangesEditableFields()
    {
        await using var db = CreateDb();
        var (program, carrier) = await SeedProgramCarrierAsync(db);
        var service = new BordereauxService(db);
        var create = await service.CreateProfileAsync(ValidRequest(program.Id, carrier.Id));

        var update = await service.UpdateProfileAsync(create.Value!.Id, ValidRequest(program.Id, carrier.Id) with
        {
            Name = "BRACE Updated",
            StateCode = "AL",
            RequiredColumnsJson = """["Policy Number","Gross Premium","Gross Commission","Net Due Carrier"]""",
        });

        Assert.True(update.IsSuccess);
        Assert.Equal("BRACE Updated", update.Value!.Name);
        Assert.Equal("AL", update.Value.StateCode);
        Assert.Contains("Gross Commission", update.Value.RequiredColumnsJson);
    }

    private static UpsertBordereauxProfileRequest ValidRequest(Guid programId, Guid carrierId) => new(
        Name: "BRACE London BDX",
        ProgramConfigurationId: programId,
        CarrierId: carrierId,
        LineOfBusiness: PolicyLineOfBusiness.GeneralLiability,
        StateCode: null,
        ReportType: BordereauxReportType.Premium,
        Frequency: BordereauxFrequency.Monthly,
        OutputFormat: BordereauxOutputFormat.Xlsx,
        DateBasis: BordereauxDateBasis.EffectiveOrBoundDateGreater,
        RequiresAccountCurrent: true,
        IsActive: true,
        RequiredTabsJson: """["General Liability (Section 1)","Auto Veh Info","IM Unit Info","Acct Current"]""",
        RequiredColumnsJson: """["Certificate Ref","Gross premium paid this time","Net Premium to London in original currency"]""",
        MappingRulesJson: """{"commissionBasis":"commissionPlusBrokerage"}""",
        StaticValuesJson: """{"umr":"BRACE-SMM-2025-LOGGING","coverholderPin":"USA00060"}""",
        ValidationRulesJson: """{"requireReconciliation":true}""",
        IncludedTransactionTypesJson: """["NewBusiness","Endorsement"]""",
        Notes: null);

    private static async Task<(ProgramConfiguration Program, Carrier Carrier)> SeedProgramCarrierAsync(ApplicationDbContext db)
    {
        var program = new ProgramConfiguration { Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        var carrier = new Carrier { Name = "BRACE", IsActive = true };
        db.AddRange(program, carrier);
        await db.SaveChangesAsync();
        return (program, carrier);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
