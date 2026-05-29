using Microsoft.Extensions.Configuration;
using SIMS.Application.Configuration;
using Xunit;

namespace SIMS.Application.Tests.Configuration;

public class MicrosoftAuthConfigurationTests
{
    [Fact]
    public void GetTenantId_UsesAzureAdFallback()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["AzureAd:TenantId"] = "tenant-from-azure-ad",
        });

        Assert.Equal("tenant-from-azure-ad", MicrosoftAuthConfiguration.GetTenantId(configuration));
    }

    [Fact]
    public void GetClientId_PrefersMicrosoftAuth()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["MicrosoftAuth:ClientId"] = "client-from-microsoft-auth",
            ["AzureAd:ClientId"] = "client-from-azure-ad",
        });

        Assert.Equal("client-from-microsoft-auth", MicrosoftAuthConfiguration.GetClientId(configuration));
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
}
