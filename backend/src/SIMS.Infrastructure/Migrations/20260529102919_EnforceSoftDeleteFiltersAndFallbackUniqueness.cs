using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnforceSoftDeleteFiltersAndFallbackUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_carrier_commissions_ProgramConfigurationId_CarrierId_LineOf~",
                table: "carrier_commissions");

            migrationBuilder.DropIndex(
                name: "IX_bordereaux_profiles_ProgramConfigurationId_CarrierId_Report~",
                table: "bordereaux_profiles");

            migrationBuilder.DropIndex(
                name: "IX_agent_commissions_ProgramConfigurationId_CarrierId_AgentId_~",
                table: "agent_commissions");

            migrationBuilder.CreateIndex(
                name: "IX_carrier_commissions_ProgramConfigurationId_CarrierId_LineOf~",
                table: "carrier_commissions",
                columns: new[] { "ProgramConfigurationId", "CarrierId", "LineOfBusiness", "EffectiveDate" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_bordereaux_profiles_ProgramConfigurationId_CarrierId_Report~",
                table: "bordereaux_profiles",
                columns: new[] { "ProgramConfigurationId", "CarrierId", "ReportType", "LineOfBusiness", "StateCode", "IsActive" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_agent_commissions_ProgramConfigurationId_CarrierId_AgentId_~",
                table: "agent_commissions",
                columns: new[] { "ProgramConfigurationId", "CarrierId", "AgentId", "LineOfBusiness", "StateCode", "EffectiveDate" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_carrier_commissions_ProgramConfigurationId_CarrierId_LineOf~",
                table: "carrier_commissions");

            migrationBuilder.DropIndex(
                name: "IX_bordereaux_profiles_ProgramConfigurationId_CarrierId_Report~",
                table: "bordereaux_profiles");

            migrationBuilder.DropIndex(
                name: "IX_agent_commissions_ProgramConfigurationId_CarrierId_AgentId_~",
                table: "agent_commissions");

            migrationBuilder.CreateIndex(
                name: "IX_carrier_commissions_ProgramConfigurationId_CarrierId_LineOf~",
                table: "carrier_commissions",
                columns: new[] { "ProgramConfigurationId", "CarrierId", "LineOfBusiness", "EffectiveDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bordereaux_profiles_ProgramConfigurationId_CarrierId_Report~",
                table: "bordereaux_profiles",
                columns: new[] { "ProgramConfigurationId", "CarrierId", "ReportType", "LineOfBusiness", "StateCode", "IsActive" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_agent_commissions_ProgramConfigurationId_CarrierId_AgentId_~",
                table: "agent_commissions",
                columns: new[] { "ProgramConfigurationId", "CarrierId", "AgentId", "LineOfBusiness", "StateCode", "EffectiveDate" },
                unique: true);
        }
    }
}
