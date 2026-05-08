using SIMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260508113500_AddFmcsaCabSafetyRollups")]
    public partial class AddFmcsaCabSafetyRollups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "hazmat_out_of_service",
                table: "fmcsa_inspections",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "hazmat_violation_count",
                table: "fmcsa_inspections",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "hazmat_out_of_service",
                table: "fmcsa_inspections");

            migrationBuilder.DropColumn(
                name: "hazmat_violation_count",
                table: "fmcsa_inspections");
        }
    }
}
