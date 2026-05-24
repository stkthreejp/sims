using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMS.Application.Services;
using SIMS.Domain.Entities;
using SIMS.Infrastructure.Data;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class CommissionProgramScopeTests
{
    [Fact]
    public async Task CarrierCommission_PrefersProgramSpecificLobRateOverGenericLobRate()
    {
        await using var db = CreateDb();
        var programId = Guid.NewGuid();
        var carrier = new Carrier { Id = Guid.NewGuid(), Name = "Oden Specialty", IsActive = true };
        db.AddRange(
            carrier,
            new CarrierCommission
            {
                CarrierId = carrier.Id,
                LineOfBusiness = "InlandMarine",
                CommissionRate = 0.10m,
                SMMRetentionRate = 0.03m,
                EffectiveDate = new DateOnly(2026, 1, 1),
                CreatedBy = Guid.NewGuid(),
            },
            new CarrierCommission
            {
                CarrierId = carrier.Id,
                ProgramConfigurationId = programId,
                LineOfBusiness = "InlandMarine",
                CommissionRate = 0.14m,
                SMMRetentionRate = 0.05m,
                EffectiveDate = new DateOnly(2026, 1, 1),
                CreatedBy = Guid.NewGuid(),
            });
        await db.SaveChangesAsync();

        var result = await CreateCarrierService(db).GetActiveRatesAsync(carrier.Id, "InlandMarine", new DateOnly(2026, 6, 1), programId);

        Assert.NotNull(result);
        Assert.Equal(0.14m, result!.CommissionRate);
        Assert.Equal(0.05m, result.SMMRetentionRate);
    }

    [Fact]
    public async Task AgentCommission_FallsBackToGenericLobRateWhenProgramSpecificRateIsMissing()
    {
        await using var db = CreateDb();
        var agent = new Agent { Id = Guid.NewGuid(), Name = "Pine Agency", Email = "agent@example.com", IsActive = true };
        db.AddRange(
            agent,
            new AgentCommission
            {
                AgentId = agent.Id,
                LineOfBusiness = "GeneralLiability",
                CommissionRate = 0.12m,
                EffectiveDate = new DateOnly(2026, 1, 1),
                CreatedBy = Guid.NewGuid(),
            });
        await db.SaveChangesAsync();

        var result = await CreateAgentService(db).GetActiveRateAsync(agent.Id, "GeneralLiability", new DateOnly(2026, 6, 1), Guid.NewGuid());

        Assert.Equal(0.12m, result);
    }

    private static CarrierCommissionService CreateCarrierService(ApplicationDbContext db)
    {
        var provider = new ServiceCollection()
            .AddSingleton<DbContext>(db)
            .BuildServiceProvider();

        return new CarrierCommissionService(provider);
    }

    private static AgentCommissionService CreateAgentService(ApplicationDbContext db)
    {
        var provider = new ServiceCollection()
            .AddSingleton<DbContext>(db)
            .BuildServiceProvider();

        return new AgentCommissionService(provider);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
