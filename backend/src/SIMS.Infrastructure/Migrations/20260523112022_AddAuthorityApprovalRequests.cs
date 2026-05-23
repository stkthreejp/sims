using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthorityApprovalRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                INSERT INTO permissions (""Name"", ""DisplayName"", ""Category"")
                SELECT 'underwriting.authority.approve', 'Approve Underwriting Authority Exceptions', 'Underwriting'
                WHERE NOT EXISTS (
                    SELECT 1 FROM permissions WHERE ""Name"" = 'underwriting.authority.approve'
                );

                INSERT INTO role_permissions (""RoleId"", ""PermissionId"")
                SELECT r.""Id"", p.""Id""
                FROM roles r
                JOIN permissions p ON p.""Name"" = 'underwriting.authority.approve'
                WHERE r.""Name"" = 'Admin'
                AND NOT EXISTS (
                    SELECT 1 FROM role_permissions rp
                    WHERE rp.""RoleId"" = r.""Id"" AND rp.""PermissionId"" = p.""Id""
                );
            ");

            migrationBuilder.CreateTable(
                name: "authority_approval_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ActionLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RequiredPermission = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ApprovalType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    InputSnapshotJson = table.Column<string>(type: "jsonb", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RequestedById = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AssignedToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecisionById = table.Column<Guid>(type: "uuid", nullable: true),
                    DecisionAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecisionNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_authority_approval_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_authority_approval_requests_users_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_authority_approval_requests_users_DecisionById",
                        column: x => x.DecisionById,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_authority_approval_requests_users_RequestedById",
                        column: x => x.RequestedById,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_authority_approval_requests_AssignedToUserId",
                table: "authority_approval_requests",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_authority_approval_requests_DecisionById",
                table: "authority_approval_requests",
                column: "DecisionById");

            migrationBuilder.CreateIndex(
                name: "IX_authority_approval_requests_RequestedById",
                table: "authority_approval_requests",
                column: "RequestedById");

            migrationBuilder.CreateIndex(
                name: "IX_authority_approval_requests_TargetType_TargetId_ActionCode_~",
                table: "authority_approval_requests",
                columns: new[] { "TargetType", "TargetId", "ActionCode", "ApprovalType", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "authority_approval_requests");

            migrationBuilder.Sql(@"
                DELETE FROM role_permissions
                WHERE ""PermissionId"" IN (
                    SELECT ""Id"" FROM permissions WHERE ""Name"" = 'underwriting.authority.approve'
                );

                DELETE FROM permissions
                WHERE ""Name"" = 'underwriting.authority.approve';
            ");
        }
    }
}
