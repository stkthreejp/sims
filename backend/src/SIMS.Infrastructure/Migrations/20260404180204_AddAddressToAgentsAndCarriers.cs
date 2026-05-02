using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAddressToAgentsAndCarriers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AddressLine1",
                table: "carriers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressLine2",
                table: "carriers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "carriers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "carriers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ZipCode",
                table: "carriers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressLine1",
                table: "agents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressLine2",
                table: "agents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "agents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ZipCode",
                table: "agents",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AddressLine1",
                table: "carriers");

            migrationBuilder.DropColumn(
                name: "AddressLine2",
                table: "carriers");

            migrationBuilder.DropColumn(
                name: "City",
                table: "carriers");

            migrationBuilder.DropColumn(
                name: "State",
                table: "carriers");

            migrationBuilder.DropColumn(
                name: "ZipCode",
                table: "carriers");

            migrationBuilder.DropColumn(
                name: "AddressLine1",
                table: "agents");

            migrationBuilder.DropColumn(
                name: "AddressLine2",
                table: "agents");

            migrationBuilder.DropColumn(
                name: "City",
                table: "agents");

            migrationBuilder.DropColumn(
                name: "ZipCode",
                table: "agents");
        }
    }
}
