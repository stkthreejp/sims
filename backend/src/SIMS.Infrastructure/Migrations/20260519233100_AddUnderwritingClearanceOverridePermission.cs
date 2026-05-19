using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SIMS.Infrastructure.Data;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260519233100_AddUnderwritingClearanceOverridePermission")]
    public partial class AddUnderwritingClearanceOverridePermission : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO permissions ("Name", "DisplayName", "Category")
                SELECT 'underwriting.clearance.override', 'Override Underwriting Clearance Blocks', 'Underwriting'
                WHERE NOT EXISTS (
                    SELECT 1 FROM permissions WHERE "Name" = 'underwriting.clearance.override'
                );

                INSERT INTO role_permissions ("RoleId", "PermissionId")
                SELECT r."Id", p."Id"
                FROM roles r
                JOIN permissions p ON p."Name" = 'underwriting.clearance.override'
                WHERE r."Name" = 'Admin'
                ON CONFLICT DO NOTHING;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM role_permissions
                WHERE "PermissionId" IN (
                    SELECT "Id" FROM permissions WHERE "Name" = 'underwriting.clearance.override'
                );

                DELETE FROM permissions
                WHERE "Name" = 'underwriting.clearance.override';
                """);
        }
    }
}
