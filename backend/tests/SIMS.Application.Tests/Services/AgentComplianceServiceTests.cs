using Microsoft.EntityFrameworkCore;
using SIMS.Application.DTOs.Agents;
using SIMS.Application.Services;
using SIMS.Domain.Entities;
using SIMS.Infrastructure.Data;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class AgentComplianceServiceTests
{
    [Fact]
    public async Task UpsertComplianceDoc_RejectsStateLicense()
    {
        await using var db = CreateDb();
        var (service, agentId) = await SeedAsync(db);

        var result = await service.UpsertComplianceDocAsync(agentId, "StateLicense", new AgentComplianceDocUpsertDto());

        Assert.False(result.IsSuccess);
        Assert.Equal("USE_LICENSE_ENDPOINT", result.ErrorCode);
    }

    [Fact]
    public async Task UpsertComplianceDoc_StoresEoLimitAndCarrier()
    {
        await using var db = CreateDb();
        var (service, agentId) = await SeedAsync(db);

        var result = await service.UpsertComplianceDocAsync(agentId, "EOCertificate", new AgentComplianceDocUpsertDto
        {
            ExpirationDate = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1),
            EoLimit = 1_000_000m,
            EoCarrierName = " Lloyd's ",
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(1_000_000m, result.Value!.EoLimit);
        Assert.Equal("Lloyd's", result.Value.EoCarrierName);
    }

    [Fact]
    public async Task UpsertComplianceDoc_StoresBrokerContinuousFlag()
    {
        await using var db = CreateDb();
        var (service, agentId) = await SeedAsync(db);

        var result = await service.UpsertComplianceDocAsync(agentId, "BrokerAgreement", new AgentComplianceDocUpsertDto
        {
            IsContinuous = true,
        });

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsContinuous);
    }

    [Fact]
    public async Task AddStateLicense_AllowsMultipleStates()
    {
        await using var db = CreateDb();
        var (service, agentId) = await SeedAsync(db);

        var tx = await service.AddStateLicenseAsync(agentId, new AgentComplianceDocUpsertDto { LicenseState = "tx" });
        var al = await service.AddStateLicenseAsync(agentId, new AgentComplianceDocUpsertDto { LicenseState = "AL" });

        Assert.True(tx.IsSuccess);
        Assert.Equal("TX", tx.Value!.LicenseState);
        Assert.True(al.IsSuccess);

        var status = await service.GetComplianceStatusAsync(agentId);
        Assert.Equal(2, status.StateLicenses.Count);
    }

    [Fact]
    public async Task AddStateLicense_RejectsDuplicateState()
    {
        await using var db = CreateDb();
        var (service, agentId) = await SeedAsync(db);

        await service.AddStateLicenseAsync(agentId, new AgentComplianceDocUpsertDto { LicenseState = "TX" });
        var dup = await service.AddStateLicenseAsync(agentId, new AgentComplianceDocUpsertDto { LicenseState = "tx" });

        Assert.False(dup.IsSuccess);
        Assert.Equal("DUPLICATE_STATE", dup.ErrorCode);
    }

    [Fact]
    public async Task AddStateLicense_RequiresState()
    {
        await using var db = CreateDb();
        var (service, agentId) = await SeedAsync(db);

        var result = await service.AddStateLicenseAsync(agentId, new AgentComplianceDocUpsertDto { LicenseState = "  " });

        Assert.False(result.IsSuccess);
        Assert.Equal("STATE_REQUIRED", result.ErrorCode);
    }

    [Fact]
    public async Task GetComplianceStatus_QuoteReadyRequiresAllThree()
    {
        await using var db = CreateDb();
        var (service, agentId) = await SeedAsync(db);
        var future = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1);

        var empty = await service.GetComplianceStatusAsync(agentId);
        Assert.False(empty.IsQuoteReady);
        Assert.Contains("EOCertificate", empty.MissingOrExpired);
        Assert.Contains("BrokerAgreement", empty.MissingOrExpired);
        Assert.Contains("StateLicense", empty.MissingOrExpired);

        await service.UpsertComplianceDocAsync(agentId, "EOCertificate", new AgentComplianceDocUpsertDto { ExpirationDate = future });
        await service.UpsertComplianceDocAsync(agentId, "BrokerAgreement", new AgentComplianceDocUpsertDto { IsContinuous = true });
        await service.AddStateLicenseAsync(agentId, new AgentComplianceDocUpsertDto { LicenseState = "TX", ExpirationDate = future });

        var ready = await service.GetComplianceStatusAsync(agentId);
        Assert.True(ready.IsQuoteReady);
        Assert.Empty(ready.MissingOrExpired);
    }

    [Fact]
    public async Task GetComplianceStatus_ExpiredLicenseBlocksReadiness()
    {
        await using var db = CreateDb();
        var (service, agentId) = await SeedAsync(db);
        var future = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1);
        var past = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);

        await service.UpsertComplianceDocAsync(agentId, "EOCertificate", new AgentComplianceDocUpsertDto { ExpirationDate = future });
        await service.UpsertComplianceDocAsync(agentId, "BrokerAgreement", new AgentComplianceDocUpsertDto { IsContinuous = true });
        await service.AddStateLicenseAsync(agentId, new AgentComplianceDocUpsertDto { LicenseState = "TX", ExpirationDate = future });
        await service.AddStateLicenseAsync(agentId, new AgentComplianceDocUpsertDto { LicenseState = "AL", ExpirationDate = past });

        var status = await service.GetComplianceStatusAsync(agentId);
        Assert.False(status.IsQuoteReady);
        Assert.Contains("StateLicense", status.MissingOrExpired);
    }

    private static async Task<(AgentService service, Guid agentId)> SeedAsync(ApplicationDbContext db)
    {
        var agent = new Agent { Name = "Test Agency" };
        db.Set<Agent>().Add(agent);
        await db.SaveChangesAsync();
        return (new AgentService(new TestServiceProvider(db)), agent.Id);
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
