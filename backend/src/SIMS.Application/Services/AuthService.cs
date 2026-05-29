using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using SIMS.Application.Common;
using SIMS.Application.Configuration;
using SIMS.Application.DTOs.Auth;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace SIMS.Application.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<User> _userManager;
    private readonly IConfiguration _config;
    private readonly IServiceProvider _sp;
    private readonly IConfigurationManager<OpenIdConnectConfiguration> _oidcConfigManager;

    public AuthService(
        UserManager<User> userManager,
        IConfiguration config,
        IServiceProvider sp,
        IConfigurationManager<OpenIdConnectConfiguration> oidcConfigManager)
    {
        _userManager = userManager;
        _config = config;
        _sp = sp;
        _oidcConfigManager = oidcConfigManager;
    }

    public async Task<Result<LoginResponseDto>> LoginAsync(LoginRequestDto dto, string ipAddress)
    {
        var user = await _userManager.FindByNameAsync(dto.UserName);
        if (user == null || user.IsDeleted)
            return Result<LoginResponseDto>.Failure("INVALID_CREDENTIALS", "Invalid username or password.");

        if (user.Status == Domain.Enums.UserStatus.Locked)
            return Result<LoginResponseDto>.Failure("ACCOUNT_LOCKED", "Account is locked.");

        if (user.Status == Domain.Enums.UserStatus.Inactive)
            return Result<LoginResponseDto>.Failure("ACCOUNT_INACTIVE", "Account is inactive.");

        if (!await _userManager.CheckPasswordAsync(user, dto.Password))
        {
            await _userManager.AccessFailedAsync(user);
            return Result<LoginResponseDto>.Failure("INVALID_CREDENTIALS", "Invalid username or password.");
        }

        await _userManager.ResetAccessFailedCountAsync(user);
        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        var roles = await _userManager.GetRolesAsync(user);
        var permissions = await GetUserPermissionsAsync(user, roles);
        var (accessToken, expiresAt) = GenerateAccessToken(user, roles, permissions);
        var refreshToken = GenerateRefreshToken(user.Id, ipAddress);

        user.RefreshTokens.Add(refreshToken);
        await _userManager.UpdateAsync(user);

        return Result<LoginResponseDto>.Success(new LoginResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken.Token,
            ExpiresAt = expiresAt,
            User = new UserInfoDto
            {
                Id = user.Id,
                UserName = user.UserName!,
                Email = user.Email!,
                FullName = user.FullName,
                Roles = roles,
                Permissions = permissions,
                MustChangePassword = user.MustChangePassword
            }
        });
    }

    public async Task<Result<LoginResponseDto>> LoginWithMicrosoftAsync(MicrosoftLoginRequestDto dto, string ipAddress)
    {
        // ── 1. Validate the Microsoft ID token ───────────────────────────────
        var tenantId = MicrosoftAuthConfiguration.GetTenantId(_config);
        var clientId = MicrosoftAuthConfiguration.GetClientId(_config);

        OpenIdConnectConfiguration oidcConfig;
        try
        {
            oidcConfig = await _oidcConfigManager.GetConfigurationAsync(CancellationToken.None);
        }
        catch
        {
            return Result<LoginResponseDto>.Failure("MS_AUTH_ERROR", "Could not reach Microsoft identity endpoint.");
        }

        var validationParams = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = new[]
            {
                $"https://login.microsoftonline.com/{tenantId}/v2.0",
                $"https://sts.windows.net/{tenantId}/"
            },
            ValidateAudience = true,
            ValidAudiences = new[] { clientId },
            ValidateLifetime = true,
            IssuerSigningKeys = oidcConfig.SigningKeys,
            ValidateIssuerSigningKey = true,
        };

        ClaimsPrincipal principal;
        try
        {
            var handler = new JwtSecurityTokenHandler();
            principal = handler.ValidateToken(dto.IdToken, validationParams, out _);
        }
        catch (Exception ex)
        {
            return Result<LoginResponseDto>.Failure("MS_TOKEN_INVALID", $"Microsoft token validation failed: {ex.Message}");
        }

        // ── 2. Extract claims ────────────────────────────────────────────────
        var email = principal.FindFirstValue(ClaimTypes.Email)
                 ?? principal.FindFirstValue("preferred_username")
                 ?? principal.FindFirstValue("upn");

        if (string.IsNullOrWhiteSpace(email))
            return Result<LoginResponseDto>.Failure("MS_NO_EMAIL", "No email claim found in Microsoft token.");

        var firstName = principal.FindFirstValue(ClaimTypes.GivenName)
                     ?? principal.FindFirstValue("given_name")
                     ?? string.Empty;
        var lastName = principal.FindFirstValue(ClaimTypes.Surname)
                    ?? principal.FindFirstValue("family_name")
                    ?? string.Empty;

        if (string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(lastName))
        {
            // Fall back to display name split
            var displayName = principal.FindFirstValue("name") ?? email.Split('@')[0];
            var parts = displayName.Split(' ', 2);
            firstName = parts[0];
            lastName = parts.Length > 1 ? parts[1] : string.Empty;
        }

        // ── 3. Find or provision the user ────────────────────────────────────
        var user = await _userManager.FindByEmailAsync(email);

        if (user == null)
        {
            // Auto-provision: derive a username from the email local part
            var baseUsername = email.Split('@')[0].ToLowerInvariant();
            var userName = baseUsername;
            var suffix = 1;
            while (await _userManager.FindByNameAsync(userName) != null)
                userName = $"{baseUsername}{suffix++}";

            user = new User
            {
                UserName = userName,
                Email = email,
                EmailConfirmed = true,
                FirstName = firstName,
                LastName = lastName,
                MustChangePassword = false,
                Status = Domain.Enums.UserStatus.Inactive, // Admin must activate before first use
            };

            // Create with a random password (user will always sign in via Microsoft)
            var tempPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) + "Aa1!";
            var createResult = await _userManager.CreateAsync(user, tempPassword);
            if (!createResult.Succeeded)
                return Result<LoginResponseDto>.Failure(
                    "PROVISION_FAILED",
                    string.Join(", ", createResult.Errors.Select(e => e.Description)));

            await _userManager.AddToRoleAsync(user, "ReadOnly");
            return Result<LoginResponseDto>.Failure(
                "ACCOUNT_PENDING",
                "Your account has been created and is pending administrator approval. Please contact your system administrator.");
        }
        else
        {
            if (user.IsDeleted)
                return Result<LoginResponseDto>.Failure("ACCOUNT_DELETED", "This account has been removed.");
            if (user.Status == Domain.Enums.UserStatus.Locked)
                return Result<LoginResponseDto>.Failure("ACCOUNT_LOCKED", "Account is locked.");
            if (user.Status == Domain.Enums.UserStatus.Inactive)
                return Result<LoginResponseDto>.Failure("ACCOUNT_INACTIVE", "Account is inactive.");

            // Update name if it changed in Entra
            var nameChanged = user.FirstName != firstName || user.LastName != lastName;
            if (nameChanged && (!string.IsNullOrWhiteSpace(firstName) || !string.IsNullOrWhiteSpace(lastName)))
            {
                user.FirstName = firstName;
                user.LastName = lastName;
            }
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        // ── 4. Issue our own JWT pair ─────────────────────────────────────────
        var roles = await _userManager.GetRolesAsync(user);
        var permissions = await GetUserPermissionsAsync(user, roles);
        var (accessToken, expiresAt) = GenerateAccessToken(user, roles, permissions);
        var refreshToken = GenerateRefreshToken(user.Id, ipAddress);

        user.RefreshTokens.Add(refreshToken);
        await _userManager.UpdateAsync(user);

        return Result<LoginResponseDto>.Success(new LoginResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken.Token,
            ExpiresAt = expiresAt,
            User = new UserInfoDto
            {
                Id = user.Id,
                UserName = user.UserName!,
                Email = user.Email!,
                FullName = user.FullName,
                Roles = roles,
                Permissions = permissions,
                MustChangePassword = false
            }
        });
    }

    public async Task<Result<LoginResponseDto>> RefreshTokenAsync(string refreshToken, string ipAddress)
    {
        var user = await _userManager.Users
            .Include(u => u.RefreshTokens)
            .SingleOrDefaultAsync(u => u.RefreshTokens.Any(rt => rt.Token == refreshToken));

        if (user == null)
            return Result<LoginResponseDto>.Failure("INVALID_TOKEN", "Invalid refresh token.");

        var token = user.RefreshTokens.Single(rt => rt.Token == refreshToken);

        if (!token.IsActive)
        {
            if (!string.IsNullOrWhiteSpace(token.ReplacedByToken))
            {
                RevokeRefreshTokenFamily(user, ipAddress);
                await _userManager.UpdateAsync(user);
                return Result<LoginResponseDto>.Failure("TOKEN_REUSE_DETECTED", "Refresh token reuse detected.");
            }

            return Result<LoginResponseDto>.Failure("TOKEN_EXPIRED", "Refresh token has expired or been revoked.");
        }

        if (user.IsDeleted || user.Status != Domain.Enums.UserStatus.Active)
            return Result<LoginResponseDto>.Failure("ACCOUNT_INACTIVE", "Account is inactive.");

        // Rotate
        token.RevokedAt = DateTime.UtcNow;
        token.RevokedByIp = ipAddress;
        var newRefreshToken = GenerateRefreshToken(user.Id, ipAddress);
        token.ReplacedByToken = newRefreshToken.Token;
        user.RefreshTokens.Add(newRefreshToken);
        RemoveExpiredRefreshTokens(user);
        await _userManager.UpdateAsync(user);

        var roles = await _userManager.GetRolesAsync(user);
        var permissions = await GetUserPermissionsAsync(user, roles);
        var (accessToken, expiresAt) = GenerateAccessToken(user, roles, permissions);

        return Result<LoginResponseDto>.Success(new LoginResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshToken.Token,
            ExpiresAt = expiresAt,
            User = new UserInfoDto
            {
                Id = user.Id,
                UserName = user.UserName!,
                Email = user.Email!,
                FullName = user.FullName,
                Roles = roles,
                Permissions = permissions,
                MustChangePassword = user.MustChangePassword
            }
        });
    }

    public async Task<Result> LogoutAsync(string refreshToken, string ipAddress)
    {
        var user = await _userManager.Users
            .Include(u => u.RefreshTokens)
            .SingleOrDefaultAsync(u => u.RefreshTokens.Any(rt => rt.Token == refreshToken));

        if (user == null)
            return Result.Success(); // Already logged out

        var token = user.RefreshTokens.SingleOrDefault(rt => rt.Token == refreshToken);
        if (token != null && token.IsActive)
        {
            token.RevokedAt = DateTime.UtcNow;
            token.RevokedByIp = ipAddress;
            await _userManager.UpdateAsync(user);
        }

        return Result.Success();
    }

    public async Task<Result<UserInfoDto>> GetCurrentUserAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
            return Result<UserInfoDto>.Failure("NOT_FOUND", "User not found.");

        var roles = await _userManager.GetRolesAsync(user);
        var permissions = await GetUserPermissionsAsync(user, roles);

        return Result<UserInfoDto>.Success(new UserInfoDto
        {
            Id = user.Id,
            UserName = user.UserName!,
            Email = user.Email!,
            FullName = user.FullName,
            Roles = roles,
            Permissions = permissions,
            MustChangePassword = user.MustChangePassword
        });
    }

    public async Task<Result> ChangePasswordAsync(Guid userId, ChangePasswordDto dto)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
            return Result.Failure("NOT_FOUND", "User not found.");

        var result = await _userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
        if (!result.Succeeded)
            return Result.Failure("PASSWORD_CHANGE_FAILED", string.Join(", ", result.Errors.Select(e => e.Description)));

        user.MustChangePassword = false;
        await _userManager.UpdateAsync(user);
        return Result.Success();
    }

    private (string token, DateTime expiresAt) GenerateAccessToken(User user, IList<string> roles, IEnumerable<string> permissions)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var expiryMinutes = int.Parse(_config["Jwt:AccessTokenExpiryMinutes"] ?? "15");
        var expiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName!),
            new(ClaimTypes.Email, user.Email ?? ""),
            new("fullName", user.FullName),
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: expiresAt,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    private static RefreshToken GenerateRefreshToken(Guid userId, string ipAddress)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        return new RefreshToken
        {
            UserId = userId,
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedByIp = ipAddress
        };
    }

    private static void RevokeRefreshTokenFamily(User user, string ipAddress)
    {
        foreach (var token in user.RefreshTokens.Where(t => t.IsActive))
        {
            token.RevokedAt = DateTime.UtcNow;
            token.RevokedByIp = ipAddress;
        }
    }

    private static void RemoveExpiredRefreshTokens(User user)
    {
        var cutoff = DateTime.UtcNow.AddDays(-30);
        var stale = user.RefreshTokens
            .Where(t => t.IsExpired && t.ExpiresAt < cutoff)
            .ToList();

        foreach (var token in stale)
            user.RefreshTokens.Remove(token);
    }

    private async Task<IEnumerable<string>> GetUserPermissionsAsync(User user, IList<string> roles)
    {
        // Load permissions from the custom RolePermissions table via role names
        using var scope = _sp.CreateScope();
        var db = (Microsoft.EntityFrameworkCore.DbContext)scope.ServiceProvider
            .GetService(typeof(Microsoft.EntityFrameworkCore.DbContext))!;

        var permissions = await db.Set<RolePermission>()
            .Include(rp => rp.Role)
            .Include(rp => rp.Permission)
            .Where(rp => roles.Contains(rp.Role.Name!))
            .Select(rp => rp.Permission.Name)
            .Distinct()
            .ToListAsync();

        return permissions;
    }
}
