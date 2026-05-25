using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentCommissionCarrierStateScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_agent_commissions_ProgramConfigurationId_AgentId_LineOfBusi~",
                table: "agent_commissions");

            migrationBuilder.AddColumn<Guid>(
                name: "CarrierId",
                table: "agent_commissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StateCode",
                table: "agent_commissions",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_agent_commissions_CarrierId",
                table: "agent_commissions",
                column: "CarrierId");

            migrationBuilder.CreateIndex(
                name: "IX_agent_commissions_ProgramConfigurationId_CarrierId_AgentId_~",
                table: "agent_commissions",
                columns: new[] { "ProgramConfigurationId", "CarrierId", "AgentId", "LineOfBusiness", "StateCode", "EffectiveDate" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_agent_commissions_carriers_CarrierId",
                table: "agent_commissions",
                column: "CarrierId",
                principalTable: "carriers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_agent_commissions_carriers_CarrierId",
                table: "agent_commissions");

            migrationBuilder.DropIndex(
                name: "IX_agent_commissions_CarrierId",
                table: "agent_commissions");

            migrationBuilder.DropIndex(
                name: "IX_agent_commissions_ProgramConfigurationId_CarrierId_AgentId_~",
                table: "agent_commissions");

            migrationBuilder.DropColumn(
                name: "CarrierId",
                table: "agent_commissions");

            migrationBuilder.DropColumn(
                name: "StateCode",
                table: "agent_commissions");

            migrationBuilder.CreateIndex(
                name: "IX_agent_commissions_ProgramConfigurationId_AgentId_LineOfBusi~",
                table: "agent_commissions",
                columns: new[] { "ProgramConfigurationId", "AgentId", "LineOfBusiness", "EffectiveDate" },
                unique: true);
        }
    }
}
