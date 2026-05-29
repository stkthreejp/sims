using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace SIMS.API.Health;

public static class SimsHealthChecks
{
    public const string ReadinessTag = "ready";
    public const string ReadinessCheckName = "sims_dependencies";

    public static IServiceCollection AddSimsHealthChecks(this IServiceCollection services)
    {
        services
            .AddHealthChecks()
            .AddCheck<SimsReadinessHealthCheck>(ReadinessCheckName, tags: [ReadinessTag]);

        return services;
    }

    public static WebApplication MapSimsHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
        }).AllowAnonymous();

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(ReadinessTag),
        }).AllowAnonymous();

        return app;
    }
}
