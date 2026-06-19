using System.Text;

namespace SIMS.API.Configuration;

public static class StartupConfigurationValidator
{
    private const int MinJwtKeyBytes = 32;

    public static void ValidateSecurityConfiguration(this IConfiguration configuration, IHostEnvironment environment)
    {
        ValidateJwt(configuration, environment);
        ValidateConnectionString(configuration);

        if (environment.IsDevelopment())
            return;

        // Staging / production: fail closed on every required external service.
        ValidateNotPlaceholder(configuration, "Storage:AzureBlobConnectionString",
            "Storage:AzureBlobConnectionString must be set in staging/production.");

        // Xero secrets live in Key Vault under flat names (XeroClientID, XeroClientSecret,
        // XeroTenantId) because ':' is not allowed in secret names; the "Xero:*" section is the
        // dev/appsettings fallback. Accept either.
        ValidateNotPlaceholderAny(configuration, ["XeroClientId", "Xero:ClientId"],
            "XeroClientID (or Xero:ClientId) must be configured in staging/production.");
        ValidateNotPlaceholderAny(configuration, ["XeroClientSecret", "Xero:ClientSecret"],
            "XeroClientSecret (or Xero:ClientSecret) must be configured in staging/production.");
        ValidateNotPlaceholderAny(configuration, ["XeroTenantId", "Xero:TenantId"],
            "XeroTenantId (the Xero organisation id) must be configured in staging/production.");

        ValidateNotPlaceholder(configuration, "GraphApi:ClientSecret",
            "GraphApi:ClientSecret must be configured in staging/production.");

        var origins = configuration.GetSection("AllowedOrigins").Get<string[]>();
        if (origins == null || origins.Length == 0 || origins.All(o => o.Contains("localhost", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("AllowedOrigins must contain non-localhost origins in staging/production.");

        var malwareProvider = configuration["Uploads:MalwareScanning:Provider"];
        if (string.IsNullOrWhiteSpace(malwareProvider) ||
            malwareProvider.StartsWith("SET_VIA", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Uploads:MalwareScanning:Provider must be explicitly set in staging/production. " +
                "Use 'ClamAV' to enable scanning, or 'NoOp' to acknowledge that uploads are not scanned.");
    }

    private static void ValidateJwt(IConfiguration configuration, IHostEnvironment environment)
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

    private static void ValidateConnectionString(IConfiguration configuration)
    {
        var conn = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(conn) || conn.Contains("SET VIA", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection must be set via user secrets, Key Vault, or environment variable.");
    }

    private static void ValidateNotPlaceholder(IConfiguration configuration, string key, string errorMessage)
    {
        if (IsPlaceholder(configuration[key]))
            throw new InvalidOperationException(errorMessage);
    }

    /// <summary>Passes if at least one of the candidate keys holds a real (non-placeholder) value.</summary>
    private static void ValidateNotPlaceholderAny(IConfiguration configuration, string[] keys, string errorMessage)
    {
        if (keys.All(k => IsPlaceholder(configuration[k])))
            throw new InvalidOperationException(errorMessage);
    }

    private static bool IsPlaceholder(string? value) =>
        string.IsNullOrWhiteSpace(value) ||
        value.StartsWith("SET_VIA", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("SET VIA", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("PLACEHOLDER", StringComparison.OrdinalIgnoreCase);
}
