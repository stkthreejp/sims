using SIMS.Application.Common;
using SIMS.Application.DTOs.Roles;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace SIMS.Application.Services;

public class RoleService : IRoleService
{
    private readonly RoleManager<Role> _roleManager;
    private readonly DbContext _db;

    public RoleService(RoleManager<Role> roleManager, DbContext db)
    {
        _roleManager = roleManager;
        _db = db;
    }

    public async Task<IReadOnlyList<RoleDto>> GetAllAsync()
    {
        var roles = await _roleManager.Roles
            .OrderBy(r => r.Name)
            .ToListAsync();

        var result = new List<RoleDto>();
        foreach (var role in roles)
        {
            var permNames = await _db.Set<RolePermission>()
                .Where(rp => rp.RoleId == role.Id)
                .Join(_db.Set<Permission>(), rp => rp.PermissionId, p => p.Id, (rp, p) => p.Name)
                .ToListAsync();

            result.Add(new RoleDto(role.Id, role.Name!, role.Description, role.IsSystemRole, permNames));
        }
        return result;
    }

    public async Task<IReadOnlyList<PermissionDto>> GetAllPermissionsAsync()
    {
        return await _db.Set<Permission>()
            .OrderBy(p => p.Category)
            .ThenBy(p => p.DisplayName)
            .Select(p => new PermissionDto(p.Id, p.Name, p.DisplayName, p.Category))
            .ToListAsync();
    }

    public async Task<Result> UpdateRolePermissionsAsync(Guid roleId, IReadOnlyList<int> permissionIds)
    {
        var role = await _roleManager.FindByIdAsync(roleId.ToString());
        if (role == null)
            return Result.Failure("NOT_FOUND", "Role not found");

        // Admin is always full-access — do not allow editing its permissions
        if (role.Name == "Admin")
            return Result.Failure("FORBIDDEN", "Admin role permissions cannot be modified");

        var existing = await _db.Set<RolePermission>()
            .Where(rp => rp.RoleId == roleId)
            .ToListAsync();

        _db.Set<RolePermission>().RemoveRange(existing);

        var validIds = await _db.Set<Permission>()
            .Where(p => permissionIds.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync();

        foreach (var permId in validIds)
            _db.Set<RolePermission>().Add(new RolePermission { RoleId = roleId, PermissionId = permId });

        await _db.SaveChangesAsync();
        return Result.Success();
    }
}
