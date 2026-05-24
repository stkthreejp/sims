using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramScopedPolicyPackages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_policy_package_configurations_CarrierId_LineOfBusiness_Stat~",
                table: "policy_package_configurations");

            migrationBuilder.AddColumn<Guid>(
                name: "ProgramConfigurationId",
                table: "policy_package_configurations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_policy_package_configurations_CarrierId",
                table: "policy_package_configurations",
                column: "CarrierId");

            migrationBuilder.CreateIndex(
                name: "ix_policy_package_program_lookup",
                table: "policy_package_configurations",
                columns: new[] { "ProgramConfigurationId", "CarrierId", "LineOfBusiness", "State", "IsDeleted" });

            migrationBuilder.AddForeignKey(
                name: "FK_policy_package_configurations_program_configurations_Progra~",
                table: "policy_package_configurations",
                column: "ProgramConfigurationId",
                principalTable: "program_configurations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_policy_package_configurations_program_configurations_Progra~",
                table: "policy_package_configurations");

            migrationBuilder.DropIndex(
                name: "IX_policy_package_configurations_CarrierId",
                table: "policy_package_configurations");

            migrationBuilder.DropIndex(
                name: "ix_policy_package_program_lookup",
                table: "policy_package_configurations");

            migrationBuilder.DropColumn(
                name: "ProgramConfigurationId",
                table: "policy_package_configurations");

            migrationBuilder.CreateIndex(
                name: "IX_policy_package_configurations_CarrierId_LineOfBusiness_Stat~",
                table: "policy_package_configurations",
                columns: new[] { "CarrierId", "LineOfBusiness", "State", "IsDeleted" });
        }
    }
}
