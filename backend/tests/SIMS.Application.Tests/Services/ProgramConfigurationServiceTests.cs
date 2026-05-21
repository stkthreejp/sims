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
