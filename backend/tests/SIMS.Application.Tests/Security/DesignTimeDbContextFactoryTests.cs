using Xunit;

namespace SIMS.Application.Tests.Security;

public class DesignTimeDbContextFactoryTests
{
    [Fact]
    public void DesignTimeFactory_DoesNotContainHardcodedPostgresCredential()
    {
        var source = File.ReadAllText(FindRepoFile(
            "backend",
            "src",
            "SIMS.Infrastructure",
            "Data",
            "DesignTimeDbContextFactory.cs"));

        Assert.DoesNotContain("Password=", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sims.postgres.database.azure.com", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sims_admin", source, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepoFile(params string[] pathParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(pathParts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repository file: {Path.Combine(pathParts)}");
    }
}
