using SIMS.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace SIMS.Domain.Entities;

public class User : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public UserStatus Status { get; set; } = UserStatus.Active;
    public DateTime? LastLoginAt { get; set; }
    public bool MustChangePassword { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;

    public string FullName => $"{FirstName} {LastName}".Trim();

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
