using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramScopedRatingAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_carrier_rating_assignments_carrier_id_line_of_business";
                DROP INDEX IF EXISTS "IX_carrier_rating_assignments_CarrierId_LineOfBusiness";
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "program_configuration_id",
                table: "carrier_rating_assignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_carrier_rating_assignments_carrier_id_line_of_business",
                table: "carrier_rating_assignments",
                columns: new[] { "carrier_id", "line_of_business" },
                unique: true,
                filter: "program_configuration_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_carrier_rating_assignments_program_configuration_id_carrier~",
                table: "carrier_rating_assignments",
                columns: new[] { "program_configuration_id", "carrier_id", "line_of_business" },
                unique: true,
                filter: "program_configuration_id IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_carrier_rating_assignments_program_configurations_program_c~",
                table: "carrier_rating_assignments",
                column: "program_configuration_id",
                principalTable: "program_configurations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_carrier_rating_assignments_program_configurations_program_c~",
                table: "carrier_rating_assignments");

            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_carrier_rating_assignments_carrier_id_line_of_business";
                DROP INDEX IF EXISTS "IX_carrier_rating_assignments_program_configuration_id_carrier~";
                """);

            migrationBuilder.DropColumn(
                name: "program_configuration_id",
                table: "carrier_rating_assignments");

            migrationBuilder.CreateIndex(
                name: "IX_carrier_rating_assignments_carrier_id_line_of_business",
                table: "carrier_rating_assignments",
                columns: new[] { "carrier_id", "line_of_business" },
                unique: true);
        }
    }
}
