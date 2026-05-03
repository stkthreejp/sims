namespace SIMS.Domain.Entities.Accounting;

public class QboOAuthToken
{
    public int Id { get; set; }
    public int TenantId { get; set; } = 1;
    public string RealmId { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public DateTime AccessTokenExpiresAt { get; set; }
    public DateTime RefreshTokenExpiresAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
