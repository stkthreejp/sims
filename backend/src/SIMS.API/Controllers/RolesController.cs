using SIMS.Application.DTOs.Roles;
using SIMS.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/roles")]
[Authorize(Policy = AppPermissions.AdminRolesManage)]
public class RolesController : ControllerBase
{
    private readonly IRoleService _roleService;

    public RolesController(IRoleService roleService) => _roleService = roleService;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var roles = await _roleService.GetAllAsync();
        return Ok(roles);
    }

    [HttpGet("permissions")]
    public async Task<IActionResult> GetPermissions()
    {
        var permissions = await _roleService.GetAllPermissionsAsync();
        return Ok(permissions);
    }

    [HttpPut("{roleId:guid}/permissions")]
    public async Task<IActionResult> UpdatePermissions(Guid roleId, [FromBody] UpdateRolePermissionsDto dto)
    {
        var result = await _roleService.UpdateRolePermissionsAsync(roleId, dto.PermissionIds);
        if (!result.IsSuccess)
            return result.ErrorCode == "NOT_FOUND"
                ? NotFound(new { result.ErrorMessage })
                : BadRequest(new { result.ErrorCode, result.ErrorMessage });
        return NoContent();
    }
}
