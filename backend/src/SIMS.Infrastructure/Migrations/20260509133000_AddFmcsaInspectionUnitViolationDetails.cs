using SIMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260509133000_AddFmcsaInspectionUnitViolationDetails")]
    public partial class AddFmcsaInspectionUnitViolationDetails : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>("unit_type", "fmcsa_inspections", type: "character varying(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<string>("unit_make", "fmcsa_inspections", type: "character varying(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<string>("unit_license", "fmcsa_inspections", type: "character varying(50)", maxLength: 50, nullable: true);
            migrationBuilder.AddColumn<string>("unit_license_state", "fmcsa_inspections", type: "character varying(2)", maxLength: 2, nullable: true);
            migrationBuilder.AddColumn<string>("vin", "fmcsa_inspections", type: "character varying(50)", maxLength: 50, nullable: true);
            migrationBuilder.AddColumn<string>("unit_type_2", "fmcsa_inspections", type: "character varying(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<string>("unit_make_2", "fmcsa_inspections", type: "character varying(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<string>("unit_license_2", "fmcsa_inspections", type: "character varying(50)", maxLength: 50, nullable: true);
            migrationBuilder.AddColumn<string>("unit_license_state_2", "fmcsa_inspections", type: "character varying(2)", maxLength: 2, nullable: true);
            migrationBuilder.AddColumn<string>("vin_2", "fmcsa_inspections", type: "character varying(50)", maxLength: 50, nullable: true);
            migrationBuilder.AddColumn<string>("unit_number", "fmcsa_violations", type: "character varying(20)", maxLength: 20, nullable: true);
            migrationBuilder.AddColumn<decimal>("oos_weight", "fmcsa_violations", type: "numeric(8,4)", precision: 8, scale: 4, nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn("unit_type", "fmcsa_inspections");
            migrationBuilder.DropColumn("unit_make", "fmcsa_inspections");
            migrationBuilder.DropColumn("unit_license", "fmcsa_inspections");
            migrationBuilder.DropColumn("unit_license_state", "fmcsa_inspections");
            migrationBuilder.DropColumn("vin", "fmcsa_inspections");
            migrationBuilder.DropColumn("unit_type_2", "fmcsa_inspections");
            migrationBuilder.DropColumn("unit_make_2", "fmcsa_inspections");
            migrationBuilder.DropColumn("unit_license_2", "fmcsa_inspections");
            migrationBuilder.DropColumn("unit_license_state_2", "fmcsa_inspections");
            migrationBuilder.DropColumn("vin_2", "fmcsa_inspections");
            migrationBuilder.DropColumn("unit_number", "fmcsa_violations");
            migrationBuilder.DropColumn("oos_weight", "fmcsa_violations");
        }
    }
}
