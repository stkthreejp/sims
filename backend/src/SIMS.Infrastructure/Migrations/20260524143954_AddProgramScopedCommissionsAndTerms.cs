using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramScopedCommissionsAndTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_carrier_commissions_CarrierId_LineOfBusiness_EffectiveDate",
                table: "carrier_commissions");

            migrationBuilder.DropIndex(
                name: "IX_agent_commissions_AgentId_LineOfBusiness_EffectiveDate",
                table: "agent_commissions");

            migrationBuilder.AddColumn<string>(
                name: "BillingMode",
                table: "program_carrier_lines_of_business",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentTermsDays",
                table: "program_carrier_lines_of_business",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProgramConfigurationId",
                table: "carrier_commissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProgramConfigurationId",
                table: "agent_commissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_carrier_commissions_ProgramConfigurationId_CarrierId_LineOf~",
                table: "carrier_commissions",
                columns: new[] { "ProgramConfigurationId", "CarrierId", "LineOfBusiness", "EffectiveDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_agent_commissions_ProgramConfigurationId_AgentId_LineOfBusi~",
                table: "agent_commissions",
                columns: new[] { "ProgramConfigurationId", "AgentId", "LineOfBusiness", "EffectiveDate" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_agent_commissions_program_configurations_ProgramConfigurati~",
                table: "agent_commissions",
                column: "ProgramConfigurationId",
                principalTable: "program_configurations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_carrier_commissions_program_configurations_ProgramConfigura~",
                table: "carrier_commissions",
                column: "ProgramConfigurationId",
                principalTable: "program_configurations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_agent_commissions_program_configurations_ProgramConfigurati~",
                table: "agent_commissions");

            migrationBuilder.DropForeignKey(
                name: "FK_carrier_commissions_program_configurations_ProgramConfigura~",
                table: "carrier_commissions");

            migrationBuilder.DropIndex(
                name: "IX_carrier_commissions_ProgramConfigurationId_CarrierId_LineOf~",
                table: "carrier_commissions");

            migrationBuilder.DropIndex(
                name: "IX_agent_commissions_ProgramConfigurationId_AgentId_LineOfBusi~",
                table: "agent_commissions");

            migrationBuilder.DropColumn(
                name: "BillingMode",
                table: "program_carrier_lines_of_business");

            migrationBuilder.DropColumn(
                name: "PaymentTermsDays",
                table: "program_carrier_lines_of_business");

            migrationBuilder.DropColumn(
                name: "ProgramConfigurationId",
                table: "carrier_commissions");

            migrationBuilder.DropColumn(
                name: "ProgramConfigurationId",
                table: "agent_commissions");

            migrationBuilder.CreateIndex(
                name: "IX_carrier_commissions_CarrierId_LineOfBusiness_EffectiveDate",
                table: "carrier_commissions",
                columns: new[] { "CarrierId", "LineOfBusiness", "EffectiveDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_agent_commissions_AgentId_LineOfBusiness_EffectiveDate",
                table: "agent_commissions",
                columns: new[] { "AgentId", "LineOfBusiness", "EffectiveDate" },
                unique: true);
        }
    }
}
