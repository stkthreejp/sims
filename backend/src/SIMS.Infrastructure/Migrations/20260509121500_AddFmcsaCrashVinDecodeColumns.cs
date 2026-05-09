using SIMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260509121500_AddFmcsaCrashVinDecodeColumns")]
    public partial class AddFmcsaCrashVinDecodeColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>("vehicle_make", "fmcsa_crashes", type: "character varying(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<string>("vehicle_model", "fmcsa_crashes", type: "character varying(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<int>("vehicle_year", "fmcsa_crashes", type: "integer", nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn("vehicle_make", "fmcsa_crashes");
            migrationBuilder.DropColumn("vehicle_model", "fmcsa_crashes");
            migrationBuilder.DropColumn("vehicle_year", "fmcsa_crashes");
        }
    }
}
