using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramScopedPolicyNumberAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_policy_number_assignments_CarrierId_WritingCompanyId_LineOf~",
                table: "policy_number_assignments");

            migrationBuilder.AddColumn<Guid>(
                name: "ProgramConfigurationId",
                table: "policy_number_assignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_policy_number_assignments_CarrierId",
                table: "policy_number_assignments",
                column: "CarrierId");

            migrationBuilder.CreateIndex(
                name: "ix_policy_number_assignments_program_lookup",
                table: "policy_number_assignments",
                columns: new[] { "ProgramConfigurationId", "CarrierId", "WritingCompanyId", "LineOfBusiness", "State", "IsActive" });

            migrationBuilder.AddForeignKey(
                name: "FK_policy_number_assignments_program_configurations_ProgramCon~",
                table: "policy_number_assignments",
                column: "ProgramConfigurationId",
                principalTable: "program_configurations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_policy_number_assignments_program_configurations_ProgramCon~",
                table: "policy_number_assignments");

            migrationBuilder.DropIndex(
                name: "IX_policy_number_assignments_CarrierId",
                table: "policy_number_assignments");

            migrationBuilder.DropIndex(
                name: "ix_policy_number_assignments_program_lookup",
                table: "policy_number_assignments");

            migrationBuilder.DropColumn(
                name: "ProgramConfigurationId",
                table: "policy_number_assignments");

            migrationBuilder.CreateIndex(
                name: "IX_policy_number_assignments_CarrierId_WritingCompanyId_LineOf~",
                table: "policy_number_assignments",
                columns: new[] { "CarrierId", "WritingCompanyId", "LineOfBusiness", "State", "IsActive" });
        }
    }
}
