using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIMS.API.Controllers;
using SIMS.Infrastructure.Data;
using Xunit;

namespace SIMS.Application.Tests.Controllers;

public class LegalRequirementsControllerTests
{
    [Fact]
    public async Task CreateSource_SavesOpenLawApiKeyWithoutReturningSecret()
    {
        await using var db = CreateDbContext();
        var controller = new LegalRequirementsController(db);
        var input = new LegalTrackedSourceUpsertDto(
            "All",
            "OpenLaw Test",
            "OpenLaw API",
            "https://api.openlaw.test",
            "openlaw-test-key",
            true,
            "Manual",
            null);

        var result = await controller.CreateSource(input);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var dto = Assert.IsType<LegalTrackedSourceDto>(created.Value);
        Assert.True(dto.HasApiKey);
        Assert.Equal("OpenLaw Test", dto.Name);

        var saved = await db.LegalTrackedSources.SingleAsync();
        Assert.Equal("openlaw-test-key", saved.ApiKey);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
