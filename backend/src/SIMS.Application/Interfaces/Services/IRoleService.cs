using SIMS.Application.Common;
using SIMS.Application.DTOs.Roles;

namespace SIMS.Application.Interfaces.Services;

public interface IRoleService
{
    Task<IReadOnlyList<RoleDto>> GetAllAsync();
    Task<IReadOnlyList<PermissionDto>> GetAllPermissionsAsync();
    Task<Result> UpdateRolePermissionsAsync(Guid roleId, IReadOnlyList<int> permissionIds);
}
