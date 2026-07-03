using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentComplianceEoAndContinuousFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "eo_carrier_name",
                table: "agent_compliance_docs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "eo_limit",
                table: "agent_compliance_docs",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_continuous",
                table: "agent_compliance_docs",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "eo_carrier_name",
                table: "agent_compliance_docs");

            migrationBuilder.DropColumn(
                name: "eo_limit",
                table: "agent_compliance_docs");

            migrationBuilder.DropColumn(
                name: "is_continuous",
                table: "agent_compliance_docs");
        }
    }
}
