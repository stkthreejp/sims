using System.Text;

namespace SIMS.API.Configuration;

public static class StartupConfigurationValidator
{
    private const int MinJwtKeyBytes = 32;

    public static void ValidateSecurityConfiguration(this IConfiguration configuration, IHostEnvironment environment)
    {
        var jwtKey = configuration["Jwt:Key"];
        var issuer = configuration["Jwt:Issuer"];
        var audience = configuration["Jwt:Audience"];

        if (string.IsNullOrWhiteSpace(jwtKey))
            throw new InvalidOperationException("Jwt:Key must be configured.");

        if (Encoding.UTF8.GetByteCount(jwtKey) < MinJwtKeyBytes)
            throw new InvalidOperationException($"Jwt:Key must be at least {MinJwtKeyBytes} bytes long.");

        if (string.IsNullOrWhiteSpace(issuer))
            throw new InvalidOperationException("Jwt:Issuer must be configured.");

        if (string.IsNullOrWhiteSpace(audience))
            throw new InvalidOperationException("Jwt:Audience must be configured.");

        if (environment.IsProduction())
        {
            var forbiddenKeys = new[]
            {
                "CHANGE_THIS_TO_A_256_BIT_SECRET_KEY_MINIMUM_32_CHARACTERS_LONG",
                "development",
                "secret",
            };

            if (forbiddenKeys.Any(k => jwtKey.Contains(k, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Jwt:Key is using a development placeholder and cannot be used in production.");
        }
    }
}
