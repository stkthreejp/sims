using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFmcsaInspectionDetailEnrichment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>("inspection_county", "fmcsa_inspections", type: "character varying(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<string>("inspection_location", "fmcsa_inspections", type: "character varying(200)", maxLength: 200, nullable: true);
            migrationBuilder.AddColumn<string>("inspection_facility", "fmcsa_inspections", type: "character varying(50)", maxLength: 50, nullable: true);
            migrationBuilder.AddColumn<string>("start_time", "fmcsa_inspections", type: "character varying(20)", maxLength: 20, nullable: true);
            migrationBuilder.AddColumn<string>("end_time", "fmcsa_inspections", type: "character varying(20)", maxLength: 20, nullable: true);
            migrationBuilder.AddColumn<bool>("post_crash", "fmcsa_inspections", type: "boolean", nullable: true);
            migrationBuilder.AddColumn<bool>("hazmat_placard_required", "fmcsa_inspections", type: "boolean", nullable: true);
            migrationBuilder.AddColumn<string>("inspection_level_description", "fmcsa_inspections", type: "character varying(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<decimal>("latitude", "fmcsa_inspections", type: "numeric(9,6)", precision: 9, scale: 6, nullable: true);
            migrationBuilder.AddColumn<decimal>("longitude", "fmcsa_inspections", type: "numeric(9,6)", precision: 9, scale: 6, nullable: true);
            migrationBuilder.AddColumn<string>("geocode_precision", "fmcsa_inspections", type: "character varying(50)", maxLength: 50, nullable: true);
            migrationBuilder.AddColumn<string>("detail_source_url", "fmcsa_inspections", type: "character varying(500)", maxLength: 500, nullable: true);
            migrationBuilder.AddColumn<DateTime>("detail_enriched_at", "fmcsa_inspections", type: "timestamp with time zone", nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn("inspection_county", "fmcsa_inspections");
            migrationBuilder.DropColumn("inspection_location", "fmcsa_inspections");
            migrationBuilder.DropColumn("inspection_facility", "fmcsa_inspections");
            migrationBuilder.DropColumn("start_time", "fmcsa_inspections");
            migrationBuilder.DropColumn("end_time", "fmcsa_inspections");
            migrationBuilder.DropColumn("post_crash", "fmcsa_inspections");
            migrationBuilder.DropColumn("hazmat_placard_required", "fmcsa_inspections");
            migrationBuilder.DropColumn("inspection_level_description", "fmcsa_inspections");
            migrationBuilder.DropColumn("latitude", "fmcsa_inspections");
            migrationBuilder.DropColumn("longitude", "fmcsa_inspections");
            migrationBuilder.DropColumn("geocode_precision", "fmcsa_inspections");
            migrationBuilder.DropColumn("detail_source_url", "fmcsa_inspections");
            migrationBuilder.DropColumn("detail_enriched_at", "fmcsa_inspections");
        }
    }
}
