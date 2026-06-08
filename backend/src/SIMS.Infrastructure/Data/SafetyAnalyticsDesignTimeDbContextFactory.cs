using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SIMS.Infrastructure.Data;

public class SafetyAnalyticsDesignTimeDbContextFactory : IDesignTimeDbContextFactory<SafetyAnalyticsDbContext>
{
    public SafetyAnalyticsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__SafetyAnalyticsConnection")
            ?? Environment.GetEnvironmentVariable("SIMS_SAFETY_ANALYTICS_CONNECTION");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Safety Analytics database connection string is required for design-time EF operations. " +
                "Set ConnectionStrings__SafetyAnalyticsConnection or SIMS_SAFETY_ANALYTICS_CONNECTION.");
        }

        var options = new DbContextOptionsBuilder<SafetyAnalyticsDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(SafetyAnalyticsDbContext).Assembly.FullName))
            .Options;

        return new SafetyAnalyticsDbContext(options);
    }
}
