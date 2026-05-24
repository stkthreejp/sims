using Microsoft.EntityFrameworkCore;
using SIMS.Application.DTOs.Underwriting;
using SIMS.Application.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;
using SIMS.Infrastructure.Data;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class ProgramConfigurationServiceTests
{
    [Fact]
    public async Task CreateAsync_NormalizesProgramCode()
    {
        await using var db = CreateDb();
        var service = new ProgramConfigurationService(db);

        var result = await service.CreateAsync(new CreateProgramConfigurationRequest(
            Name: "Longleaf Inland Marine",
            Code: " longleaf-im ",
            IsActive: true,
            Notes: "Preferred guideline scope"));

        Assert.True(result.IsSuccess);
        Assert.Equal("LONGLEAF-IM", result.Value!.Code);
    }

    [Fact]
    public async Task CreateAsync_RejectsDuplicateProgramCode()
    {
        await using var db = CreateDb();
        var service = new ProgramConfigurationService(db);

        await service.CreateAsync(new CreateProgramConfigurationRequest(
            "Longleaf Inland Marine",
            "LONGLEAF-IM",
            true,
            null));

        var duplicate = await service.CreateAsync(new CreateProgramConfigurationRequest(
            "Longleaf IM Duplicate",
            "longleaf-im",
            true,
            null));

        Assert.False(duplicate.IsSuccess);
        Assert.Equal("PROGRAM_CODE_DUPLICATE", duplicate.ErrorCode);
    }

    [Fact]
    public async Task AddCarrierAsync_AddsCarrierUnderProgram()
    {
        await using var db = CreateDb();
        var program = new ProgramConfiguration
        {
            Name = "Longleaf",
            Code = "LONGLEAF",
            IsActive = true
        };
        var carrier = new Carrier
        {
            Name = "Falls Lake",
            IsActive = true
        };
        db.AddRange(program, carrier);
        await db.SaveChangesAsync();

        var service = new ProgramConfigurationService(db);
        var result = await service.AddCarrierAsync(program.Id, new UpsertProgramCarrierRequest(
            CarrierId: carrier.Id,
            IsActive: true,
            EffectiveDate: new DateOnly(2026, 1, 1),
            ExpirationDate: null,
            Notes: "Primary carrier"));

        Assert.True(result.IsSuccess);
        Assert.Equal(program.Id, result.Value!.ProgramConfigurationId);
        Assert.Equal(carrier.Id, result.Value.CarrierId);
        Assert.Equal("Falls Lake", result.Value.CarrierName);
    }

    [Fact]
    public async Task AddCarrierAsync_RejectsDuplicateCarrierForSameProgram()
    {
        await using var db = CreateDb();
        var program = new ProgramConfiguration
        {
            Name = "Longleaf",
            Code = "LONGLEAF",
            IsActive = true
        };
        var carrier = new Carrier
        {
            Name = "Falls Lake",
            IsActive = true
        };
        db.AddRange(program, carrier);
        await db.SaveChangesAsync();

        var service = new ProgramConfigurationService(db);
        var request = new UpsertProgramCarrierRequest(carrier.Id, true, new DateOnly(2026, 1, 1), null, null);

        await service.AddCarrierAsync(program.Id, request);
        var duplicate = await service.AddCarrierAsync(program.Id, request);

        Assert.False(duplicate.IsSuccess);
        Assert.Equal("PROGRAM_CARRIER_DUPLICATE", duplicate.ErrorCode);
    }

    [Fact]
    public async Task CopyStateAsync_CopiesStateSetupUnderSameProgramCarrierLob()
    {
        await using var db = CreateDb();
        var program = new ProgramConfiguration { Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        var carrier = new Carrier { Name = "Falls Lake", IsActive = true };
        db.AddRange(program, carrier);
        await db.SaveChangesAsync();

        var service = new ProgramConfigurationService(db);
        var programCarrier = await service.AddCarrierAsync(program.Id, new UpsertProgramCarrierRequest(carrier.Id, true, new DateOnly(2026, 1, 1), null, null));
        var lob = await service.AddLineOfBusinessAsync(program.Id, programCarrier.Value!.Id, new UpsertProgramCarrierLineOfBusinessRequest(
            PolicyLineOfBusiness.InlandMarine,
            true,
            new DateOnly(2026, 1, 1),
            null,
            "IM setup"));
        await service.AddStateAsync(program.Id, programCarrier.Value.Id, lob.Value!.Id, new UpsertProgramCarrierLobStateRequest(
            "tx",
            true,
            new DateOnly(2026, 1, 1),
            null,
            "Texas details"));

        var copy = await service.CopyStateAsync(program.Id, programCarrier.Value.Id, lob.Value.Id, new CopyProgramCarrierLobStateRequest("TX", "SC"));

        Assert.True(copy.IsSuccess);
        Assert.Equal("SC", copy.Value!.StateCode);
        Assert.Equal("Texas details", copy.Value.Notes);
    }

    [Fact]
    public async Task AddLineOfBusinessAsync_SavesBillingModeAndPaymentTerms()
    {
        await using var db = CreateDb();
        var program = new ProgramConfiguration { Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        var carrier = new Carrier { Name = "Falls Lake", IsActive = true };
        db.AddRange(program, carrier);
        await db.SaveChangesAsync();

        var service = new ProgramConfigurationService(db);
        var programCarrier = await service.AddCarrierAsync(program.Id, new UpsertProgramCarrierRequest(carrier.Id, true, new DateOnly(2026, 1, 1), null, null));

        var result = await service.AddLineOfBusinessAsync(program.Id, programCarrier.Value!.Id, new UpsertProgramCarrierLineOfBusinessRequest(
            PolicyLineOfBusiness.InlandMarine,
            true,
            new DateOnly(2026, 1, 1),
            null,
            "IM setup",
            "AgencyBill",
            30));

        Assert.True(result.IsSuccess);
        Assert.Equal("AgencyBill", result.Value!.BillingMode);
        Assert.Equal(30, result.Value.PaymentTermsDays);
    }

    [Fact]
    public async Task CreateDocumentAsync_UsesProgramIdentityWithoutOverridingCarrierLineOrState()
    {
        await using var db = CreateDb();
        var program = new ProgramConfiguration
        {
            Name = "Longleaf Inland Marine",
            Code = "LONGLEAF-IM",
            IsActive = true
        };
        db.Add(program);
        await db.SaveChangesAsync();

        var service = new UnderwritingGuidelineControlService(db);
        var result = await service.CreateDocumentAsync(new CreateUnderwritingGuidelineDocumentRequest(
            ProgramName: "Ignored free text",
            CarrierId: null,
            LineOfBusiness: PolicyLineOfBusiness.GeneralLiability,
            StateCode: "ALL",
            Title: "Texas Longleaf Guidelines",
            SourceFileName: null,
            SourceBlobName: null,
            Notes: null,
            ProgramId: program.Id), Guid.NewGuid());

        Assert.True(result.IsSuccess);
        Assert.Equal(program.Id, result.Value!.ProgramId);
        Assert.Equal("LONGLEAF-IM", result.Value.ProgramCode);
        Assert.Equal("Longleaf Inland Marine", result.Value.ProgramName);
        Assert.Null(result.Value.CarrierId);
        Assert.Equal(PolicyLineOfBusiness.GeneralLiability, result.Value.LineOfBusiness);
        Assert.Equal("ALL", result.Value.StateCode);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
