using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInsuredGeocodeFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "latitude",
                table: "insureds",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "longitude",
                table: "insureds",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "geocode_precision",
                table: "insureds",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "geocode_provider",
                table: "insureds",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "google_place_id",
                table: "insureds",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "geocoded_at",
                table: "insureds",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "latitude", table: "insureds");
            migrationBuilder.DropColumn(name: "longitude", table: "insureds");
            migrationBuilder.DropColumn(name: "geocode_precision", table: "insureds");
            migrationBuilder.DropColumn(name: "geocode_provider", table: "insureds");
            migrationBuilder.DropColumn(name: "google_place_id", table: "insureds");
            migrationBuilder.DropColumn(name: "geocoded_at", table: "insureds");
        }
    }
}
