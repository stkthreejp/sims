using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace SIMS.Infrastructure.Extensions;

public static class KeyVaultConfigurationExtensions
{
    public static IConfigurationBuilder AddSimsKeyVault(this IConfigurationBuilder builder)
    {
        try
        {
            var keyVaultUri = new Uri("https://simskey.vault.azure.net/");
            var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ExcludeEnvironmentCredential = false,
                ExcludeManagedIdentityCredential = false
            });
            builder.AddAzureKeyVault(keyVaultUri, credential);
        }
        catch
        {
            // Key Vault unavailable (no Azure credentials in dev) — fall back to user secrets/appsettings
        }
        return builder;
    }
}
