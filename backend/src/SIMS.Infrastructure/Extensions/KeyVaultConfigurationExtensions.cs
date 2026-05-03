using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace SIMS.Infrastructure.Extensions;

public static class KeyVaultConfigurationExtensions
{
    public static IConfigurationBuilder AddSimsKeyVault(this IConfigurationBuilder builder)
    {
        var keyVaultUri = new Uri("https://simskey.vault.azure.net/");
        builder.AddAzureKeyVault(keyVaultUri, new DefaultAzureCredential());
        return builder;
    }
}
