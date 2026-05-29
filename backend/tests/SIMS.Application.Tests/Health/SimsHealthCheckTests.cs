using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using SIMS.API.Health;
using SIMS.Infrastructure.Data;
using Xunit;

namespace SIMS.Application.Tests.Health;

public class SimsHealthCheckTests
{
    [Fact]
    public async Task AddSimsHealthChecks_RegistersReadinessDependencyCheck()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddSimsHealthChecks();
        await using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;
        var registration = Assert.Single(options.Registrations, r => r.Name == SimsHealthChecks.ReadinessCheckName);
        var report = await provider.GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(r => r.Tags.Contains(SimsHealthChecks.ReadinessTag));

        Assert.Contains(SimsHealthChecks.ReadinessTag, registration.Tags);
        Assert.Equal(HealthStatus.Healthy, report.Status);
    }
}
