using SIMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260508143000_AddFmcsaCrashDetailFields")]
    public partial class AddFmcsaCrashDetailFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>("agency", "fmcsa_crashes", type: "character varying(150)", maxLength: 150, nullable: true);
            migrationBuilder.AddColumn<string>("cargo_body_type_id", "fmcsa_crashes", type: "character varying(20)", maxLength: 20, nullable: true);
            migrationBuilder.AddColumn<string>("city", "fmcsa_crashes", type: "character varying(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<string>("county_code", "fmcsa_crashes", type: "character varying(20)", maxLength: 20, nullable: true);
            migrationBuilder.AddColumn<string>("gvw_rating_id", "fmcsa_crashes", type: "character varying(20)", maxLength: 20, nullable: true);
            migrationBuilder.AddColumn<bool>("hazmat_placard", "fmcsa_crashes", type: "boolean", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<bool>("hazmat_released", "fmcsa_crashes", type: "boolean", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<string>("light_condition_id", "fmcsa_crashes", type: "character varying(20)", maxLength: 20, nullable: true);
            migrationBuilder.AddColumn<string>("location", "fmcsa_crashes", type: "character varying(200)", maxLength: 200, nullable: true);
            migrationBuilder.AddColumn<string>("road_surface_condition_id", "fmcsa_crashes", type: "character varying(20)", maxLength: 20, nullable: true);
            migrationBuilder.AddColumn<string>("trafficway_id", "fmcsa_crashes", type: "character varying(20)", maxLength: 20, nullable: true);
            migrationBuilder.AddColumn<string>("vehicle_configuration_id", "fmcsa_crashes", type: "character varying(20)", maxLength: 20, nullable: true);
            migrationBuilder.AddColumn<string>("vehicle_identification_number", "fmcsa_crashes", type: "character varying(50)", maxLength: 50, nullable: true);
            migrationBuilder.AddColumn<string>("vehicle_make", "fmcsa_crashes", type: "character varying(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<string>("vehicle_model", "fmcsa_crashes", type: "character varying(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<int>("vehicle_year", "fmcsa_crashes", type: "integer", nullable: true);
            migrationBuilder.AddColumn<string>("vehicle_license_number", "fmcsa_crashes", type: "character varying(50)", maxLength: 50, nullable: true);
            migrationBuilder.AddColumn<string>("vehicle_license_state", "fmcsa_crashes", type: "character varying(2)", maxLength: 2, nullable: true);
            migrationBuilder.AddColumn<int>("vehicles_in_accident", "fmcsa_crashes", type: "integer", nullable: true);
            migrationBuilder.AddColumn<string>("weather_condition_id", "fmcsa_crashes", type: "character varying(20)", maxLength: 20, nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn("agency", "fmcsa_crashes");
            migrationBuilder.DropColumn("cargo_body_type_id", "fmcsa_crashes");
            migrationBuilder.DropColumn("city", "fmcsa_crashes");
            migrationBuilder.DropColumn("county_code", "fmcsa_crashes");
            migrationBuilder.DropColumn("gvw_rating_id", "fmcsa_crashes");
            migrationBuilder.DropColumn("hazmat_placard", "fmcsa_crashes");
            migrationBuilder.DropColumn("hazmat_released", "fmcsa_crashes");
            migrationBuilder.DropColumn("light_condition_id", "fmcsa_crashes");
            migrationBuilder.DropColumn("location", "fmcsa_crashes");
            migrationBuilder.DropColumn("road_surface_condition_id", "fmcsa_crashes");
            migrationBuilder.DropColumn("trafficway_id", "fmcsa_crashes");
            migrationBuilder.DropColumn("vehicle_configuration_id", "fmcsa_crashes");
            migrationBuilder.DropColumn("vehicle_identification_number", "fmcsa_crashes");
            migrationBuilder.DropColumn("vehicle_make", "fmcsa_crashes");
            migrationBuilder.DropColumn("vehicle_model", "fmcsa_crashes");
            migrationBuilder.DropColumn("vehicle_year", "fmcsa_crashes");
            migrationBuilder.DropColumn("vehicle_license_number", "fmcsa_crashes");
            migrationBuilder.DropColumn("vehicle_license_state", "fmcsa_crashes");
            migrationBuilder.DropColumn("vehicles_in_accident", "fmcsa_crashes");
            migrationBuilder.DropColumn("weather_condition_id", "fmcsa_crashes");
        }
    }
}
