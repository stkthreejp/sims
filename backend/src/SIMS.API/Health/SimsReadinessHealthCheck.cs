using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SIMS.Infrastructure.Data;

namespace SIMS.API.Health;

public sealed class SimsReadinessHealthCheck : IHealthCheck
{
    private readonly IServiceScopeFactory _scopeFactory;

    public SimsReadinessHealthCheck(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (!await appDb.Database.CanConnectAsync(cancellationToken))
            return HealthCheckResult.Unhealthy("Application database is unavailable.");

        var safetyDb = scope.ServiceProvider.GetService<SafetyAnalyticsDbContext>();
        if (safetyDb is not null && !await safetyDb.Database.CanConnectAsync(cancellationToken))
            return HealthCheckResult.Unhealthy("Safety analytics database is unavailable.");

        return HealthCheckResult.Healthy("Required dependencies are available.");
    }
}
