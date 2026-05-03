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

public class QboTokenService : IQboTokenService
{
    private const string TokenUrl = "https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer";
    private static readonly TimeSpan RefreshBuffer = TimeSpan.FromMinutes(5);

    private readonly ApplicationDbContext _db;
    private readonly QboSettings _settings;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<QboTokenService> _logger;

    public QboTokenService(
        ApplicationDbContext db,
        IOptions<QboSettings> settings,
        IHttpClientFactory httpFactory,
        ILogger<QboTokenService> logger)
    {
        _db = db;
        _settings = settings.Value;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public async Task<bool> IsConnectedAsync(CancellationToken ct = default)
    {
        var token = await GetTokenRowAsync(ct);
        return token != null && token.RefreshTokenExpiresAt > DateTime.UtcNow;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        var token = await GetTokenRowAsync(ct)
            ?? throw new InvalidOperationException("QBO not connected. Run BootstrapFromConfigAsync first.");

        if (token.AccessTokenExpiresAt - DateTime.UtcNow > RefreshBuffer)
            return token.AccessToken;

        return await RefreshAccessTokenAsync(token, ct);
    }

    public async Task BootstrapFromConfigAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_settings.RefreshToken) || string.IsNullOrEmpty(_settings.RealmId))
            return;

        var existing = await GetTokenRowAsync(ct);
        if (existing != null)
            return; // already seeded

        var refreshed = await ExchangeRefreshTokenAsync(_settings.RefreshToken, ct);

        _db.QboOAuthTokens.Add(new QboOAuthToken
        {
            TenantId = 1,
            RealmId = _settings.RealmId,
            AccessToken = refreshed.AccessToken,
            RefreshToken = refreshed.RefreshToken,
            AccessTokenExpiresAt = DateTime.UtcNow.AddSeconds(refreshed.AccessTokenExpiresIn - 60),
            RefreshTokenExpiresAt = DateTime.UtcNow.AddSeconds(refreshed.RefreshTokenExpiresIn - 300),
        });
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("QBO token bootstrapped for realm {RealmId}", _settings.RealmId);
    }

    // ── private helpers ────────────────────────────────────────────────────────

    private Task<QboOAuthToken?> GetTokenRowAsync(CancellationToken ct) =>
        _db.QboOAuthTokens.FirstOrDefaultAsync(t => t.TenantId == 1, ct);

    private async Task<string> RefreshAccessTokenAsync(QboOAuthToken token, CancellationToken ct)
    {
        var refreshed = await ExchangeRefreshTokenAsync(token.RefreshToken, ct);
        token.AccessToken = refreshed.AccessToken;
        token.RefreshToken = refreshed.RefreshToken;
        token.AccessTokenExpiresAt = DateTime.UtcNow.AddSeconds(refreshed.AccessTokenExpiresIn - 60);
        token.RefreshTokenExpiresAt = DateTime.UtcNow.AddSeconds(refreshed.RefreshTokenExpiresIn - 300);
        token.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        _logger.LogDebug("QBO access token refreshed");
        return token.AccessToken;
    }

    private async Task<QboTokenResponse> ExchangeRefreshTokenAsync(string refreshToken, CancellationToken ct)
    {
        var client = _httpFactory.CreateClient("qbo_oauth");
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_settings.ClientId}:{_settings.ClientSecret}"));

        var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl)
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Basic", credentials) },
            Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", refreshToken),
            }),
        };

        var response = await client.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"QBO token refresh failed ({response.StatusCode}): {body}");

        return JsonSerializer.Deserialize<QboTokenResponse>(body, JsonOptions)
            ?? throw new InvalidOperationException("Empty token response from QBO");
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private record QboTokenResponse(
        string access_token,
        string refresh_token,
        int expires_in,
        int x_refresh_token_expires_in)
    {
        public string AccessToken => access_token;
        public string RefreshToken => refresh_token;
        public int AccessTokenExpiresIn => expires_in;
        public int RefreshTokenExpiresIn => x_refresh_token_expires_in;
    }
}
