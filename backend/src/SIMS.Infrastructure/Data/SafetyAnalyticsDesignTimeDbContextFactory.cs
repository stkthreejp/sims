using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SIMS.Infrastructure.Data;

public class SafetyAnalyticsDesignTimeDbContextFactory : IDesignTimeDbContextFactory<SafetyAnalyticsDbContext>
{
    public SafetyAnalyticsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("SAFETY_ANALYTICS_CONNECTION")
            ?? "Host=localhost;Database=sims_safety_analytics;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<SafetyAnalyticsDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(SafetyAnalyticsDbContext).Assembly.FullName))
            .Options;

        return new SafetyAnalyticsDbContext(options);
    }
}
