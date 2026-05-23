using System.Security.Claims;
using SIMS.API.Security;
using SIMS.Application.Security;
using Xunit;

namespace SIMS.Application.Tests.Security;

public class ClaimsPrincipalSecurityExtensionsTests
{
    [Fact]
    public void PermissionNames_ReturnsPermissionClaimsOnly()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("permission", AppPermissions.AccountingAdmin),
            new Claim(ClaimTypes.Name, "Test User"),
            new Claim("permission", AppPermissions.RatingAdmin),
        }));

        var permissions = principal.PermissionNames();

        Assert.Equal(new[] { AppPermissions.AccountingAdmin, AppPermissions.RatingAdmin }, permissions);
    }

    [Fact]
    public void HasPermission_UsesPermissionClaims()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("permission", AppPermissions.UnderwritingAuthorityApprove),
        }));

        Assert.True(principal.HasPermission(AppPermissions.UnderwritingAuthorityApprove));
        Assert.False(principal.HasPermission(AppPermissions.AccountingAdmin));
    }
}
