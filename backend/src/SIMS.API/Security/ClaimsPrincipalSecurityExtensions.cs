using System.Security.Claims;
using SIMS.Application.Security;

namespace SIMS.API.Security;

public static class ClaimsPrincipalSecurityExtensions
{
    public static UserAccessScope ToBusinessDataAccessScope(this ClaimsPrincipal user)
    {
        var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return new UserAccessScope(userId, user.CanAccessAllBusinessData());
    }

    public static bool CanAccessAllBusinessData(this ClaimsPrincipal user) =>
        user.HasPermission(AppPermissions.AdminSystemManage) ||
        user.HasPermission(AppPermissions.UnderwritingManage);

    private static bool HasPermission(this ClaimsPrincipal user, string permission) =>
        user.HasClaim("permission", permission);
}
