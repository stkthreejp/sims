using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUnderwritingControlEnforcementResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "underwriting_control_enforcement_results",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GuidelineControlId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Stage = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsBlocking = table.Column<bool>(type: "boolean", nullable: false),
                    OverrideAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    OverridePermission = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ConditionJson = table.Column<string>(type: "jsonb", nullable: true),
                    InputSnapshotJson = table.Column<string>(type: "jsonb", nullable: true),
                    EvaluatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OverriddenByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    OverriddenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OverrideReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_underwriting_control_enforcement_results", x => x.Id);
                    table.ForeignKey(
                        name: "FK_underwriting_control_enforcement_results_underwriting_guide~",
                        column: x => x.GuidelineControlId,
                        principalTable: "underwriting_guideline_controls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_underwriting_control_enforcement_results_users_OverriddenBy~",
                        column: x => x.OverriddenByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_underwriting_control_enforcement_results_GuidelineControlId~",
                table: "underwriting_control_enforcement_results",
                columns: new[] { "GuidelineControlId", "TargetType", "TargetId", "Stage" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_underwriting_control_enforcement_results_OverriddenByUserId",
                table: "underwriting_control_enforcement_results",
                column: "OverriddenByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_underwriting_control_enforcement_results_TargetType_TargetI~",
                table: "underwriting_control_enforcement_results",
                columns: new[] { "TargetType", "TargetId", "Stage", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "underwriting_control_enforcement_results");
        }
    }
}
