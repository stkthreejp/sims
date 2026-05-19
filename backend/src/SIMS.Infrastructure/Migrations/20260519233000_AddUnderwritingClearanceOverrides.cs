using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SIMS.Infrastructure.Data;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260519233000_AddUnderwritingClearanceOverrides")]
    public partial class AddUnderwritingClearanceOverrides : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOverridden",
                table: "UnderwritingClearanceResults",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "OverriddenAt",
                table: "UnderwritingClearanceResults",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OverriddenById",
                table: "UnderwritingClearanceResults",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OverrideReason",
                table: "UnderwritingClearanceResults",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsOverridden",
                table: "UnderwritingClearanceResults");

            migrationBuilder.DropColumn(
                name: "OverriddenAt",
                table: "UnderwritingClearanceResults");

            migrationBuilder.DropColumn(
                name: "OverriddenById",
                table: "UnderwritingClearanceResults");

            migrationBuilder.DropColumn(
                name: "OverrideReason",
                table: "UnderwritingClearanceResults");
        }
    }
}
