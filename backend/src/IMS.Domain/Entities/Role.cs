using Microsoft.AspNetCore.Identity;

namespace IMS.Domain.Entities;

public class Role : IdentityRole<Guid>
{
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; } = false;

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
