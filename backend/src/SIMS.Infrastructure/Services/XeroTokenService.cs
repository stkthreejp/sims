using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SIMS.Application.Configuration;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities.Accounting;
using SIMS.Infrastructure.Data;

namespace SIMS.Infrastructure.Services;

/// <summary>
/// Obtains and caches a Xero access token using the OAuth2 client-credentials grant
/// (Xero "Custom connection"). There is no refresh token: when the cached access token
/// expires we simply request a new one with the configured client id/secret.
/// </summary>
public class XeroTokenService : IXeroTokenService
{
    private static readonly TimeSpan RefreshBuffer = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ApplicationDbContext _db;
    private readonly XeroSettings _settings;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<XeroTokenService> _logger;

    public XeroTokenService(
        ApplicationDbContext db,
        IOptions<XeroSettings> settings,
        IHttpClientFactory httpFactory,
        ILogger<XeroTokenService> logger)
    {
        _db = db;
        _settings = settings.Value;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public string TenantId => _settings.TenantId;

    private bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_settings.ClientId)
        && !string.IsNullOrWhiteSpace(_settings.ClientSecret)
        && !string.IsNullOrWhiteSpace(_settings.TenantId);

    public async Task<bool> IsConnectedAsync(CancellationToken ct = default)
    {
        if (!IsConfigured)
            return false;

        try
        {
            await GetAccessTokenAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Xero connectivity check failed");
            return false;
        }
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Xero is not configured. Set Xero:ClientId, Xero:ClientSecret and Xero:TenantId.");

        var token = await _db.XeroOAuthTokens.FirstOrDefaultAsync(t => t.TenantId == 1, ct);

        if (token != null && token.AccessTokenExpiresAt - DateTime.UtcNow > RefreshBuffer)
            return token.AccessToken;

        var minted = await RequestClientCredentialsTokenAsync(ct);

        if (token == null)
        {
            token = new XeroOAuthToken { TenantId = 1, XeroTenantId = _settings.TenantId };
            _db.XeroOAuthTokens.Add(token);
        }

        token.AccessToken = minted.AccessToken;
        token.XeroTenantId = _settings.TenantId;
        token.AccessTokenExpiresAt = DateTime.UtcNow.AddSeconds(minted.ExpiresIn - 60);
        token.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogDebug("Xero access token minted (expires in {ExpiresIn}s)", minted.ExpiresIn);
        return token.AccessToken;
    }

    public async Task BootstrapFromConfigAsync(CancellationToken ct = default)
    {
        if (!IsConfigured)
            return;

        try
        {
            await GetAccessTokenAsync(ct);
            _logger.LogInformation("Xero token bootstrapped for tenant {TenantId}", _settings.TenantId);
        }
        catch (Exception ex)
        {
            // Don't block startup if Xero is unreachable; the driver will surface errors at export time.
            _logger.LogWarning(ex, "Xero token bootstrap failed (credentials configured but token request errored)");
        }
    }

    private async Task<XeroTokenResponse> RequestClientCredentialsTokenAsync(CancellationToken ct)
    {
        var client = _httpFactory.CreateClient("xero_oauth");
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_settings.ClientId}:{_settings.ClientSecret}"));

        var request = new HttpRequestMessage(HttpMethod.Post, _settings.IdentityUrl)
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Basic", credentials) },
            Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("scope", _settings.Scopes),
            }),
        };

        var response = await client.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Xero token request failed ({(int)response.StatusCode} {response.ReasonPhrase}).");

        return JsonSerializer.Deserialize<XeroTokenResponse>(body, JsonOptions)
            ?? throw new InvalidOperationException("Empty token response from Xero.");
    }

    private record XeroTokenResponse(string access_token, int expires_in, string token_type)
    {
        public string AccessToken => access_token;
        public int ExpiresIn => expires_in;
    }
}
