using Microsoft.Extensions.Configuration;

namespace SIMS.Application.Configuration;

public static class MicrosoftAuthConfiguration
{
    public static string GetTenantId(IConfiguration configuration) =>
        GetRequired(configuration, "MicrosoftAuth:TenantId", "AzureAd:TenantId");

    public static string GetClientId(IConfiguration configuration) =>
        GetRequired(configuration, "MicrosoftAuth:ClientId", "AzureAd:ClientId");

    private static string GetRequired(IConfiguration configuration, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = configuration[key];
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        throw new InvalidOperationException($"{string.Join(" or ", keys)} is not configured.");
    }
}
