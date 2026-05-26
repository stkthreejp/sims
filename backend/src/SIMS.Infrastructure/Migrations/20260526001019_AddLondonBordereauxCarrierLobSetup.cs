using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLondonBordereauxCarrierLobSetup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LondonClassOfBusiness",
                table: "program_carrier_lines_of_business",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LondonInsuranceType",
                table: "program_carrier_lines_of_business",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LondonRiskCode",
                table: "program_carrier_lines_of_business",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LondonSectionNumber",
                table: "program_carrier_lines_of_business",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LondonUmr",
                table: "program_carrier_lines_of_business",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultCurrencyCode",
                table: "carriers",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "USD");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LondonClassOfBusiness",
                table: "program_carrier_lines_of_business");

            migrationBuilder.DropColumn(
                name: "LondonInsuranceType",
                table: "program_carrier_lines_of_business");

            migrationBuilder.DropColumn(
                name: "LondonRiskCode",
                table: "program_carrier_lines_of_business");

            migrationBuilder.DropColumn(
                name: "LondonSectionNumber",
                table: "program_carrier_lines_of_business");

            migrationBuilder.DropColumn(
                name: "LondonUmr",
                table: "program_carrier_lines_of_business");

            migrationBuilder.DropColumn(
                name: "DefaultCurrencyCode",
                table: "carriers");
        }
    }
}
