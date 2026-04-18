using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCarriers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CarrierName",
                table: "policies");

            migrationBuilder.AddColumn<Guid>(
                name: "CarrierId",
                table: "policies",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "carriers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Naic = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    AmBestRating = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_carriers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "carrier_lines_of_business",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CarrierId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineOfBusiness = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_carrier_lines_of_business", x => x.Id);
                    table.ForeignKey(
                        name: "FK_carrier_lines_of_business_carriers_CarrierId",
                        column: x => x.CarrierId,
                        principalTable: "carriers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_policies_CarrierId",
                table: "policies",
                column: "CarrierId");

            migrationBuilder.CreateIndex(
                name: "IX_carrier_lines_of_business_CarrierId_LineOfBusiness",
                table: "carrier_lines_of_business",
                columns: new[] { "CarrierId", "LineOfBusiness" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_carriers_Name",
                table: "carriers",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_policies_carriers_CarrierId",
                table: "policies",
                column: "CarrierId",
                principalTable: "carriers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_policies_carriers_CarrierId",
                table: "policies");

            migrationBuilder.DropTable(
                name: "carrier_lines_of_business");

            migrationBuilder.DropTable(
                name: "carriers");

            migrationBuilder.DropIndex(
                name: "IX_policies_CarrierId",
                table: "policies");

            migrationBuilder.DropColumn(
                name: "CarrierId",
                table: "policies");

            migrationBuilder.AddColumn<string>(
                name: "CarrierName",
                table: "policies",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }
    }
}
