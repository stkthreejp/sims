namespace SIMS.Application.DTOs.Roles;

public record PermissionDto(int Id, string Name, string DisplayName, string Category);

public record RoleDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystemRole,
    IReadOnlyList<string> Permissions
);

public record UpdateRolePermissionsDto(IReadOnlyList<int> PermissionIds);
