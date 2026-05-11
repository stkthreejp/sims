using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SIMS.Infrastructure.Data;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260510231000_AddFmcsaInspectionFileLinkFields")]
    public partial class AddFmcsaInspectionFileLinkFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "external_inspection_id",
                table: "fmcsa_inspections",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "county_code",
                table: "fmcsa_inspections",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_fmcsa_inspections_us_dot_number_external_inspection_id",
                table: "fmcsa_inspections",
                columns: new[] { "us_dot_number", "external_inspection_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_fmcsa_inspections_us_dot_number_external_inspection_id",
                table: "fmcsa_inspections");

            migrationBuilder.DropColumn(name: "external_inspection_id", table: "fmcsa_inspections");
            migrationBuilder.DropColumn(name: "county_code", table: "fmcsa_inspections");
        }
    }
}
