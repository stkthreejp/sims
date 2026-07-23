using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FilterSoftDeleteOnSetupUniqueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_policy_number_sequences_Name",
                table: "policy_number_sequences");

            migrationBuilder.DropIndex(
                name: "IX_carriers_Name",
                table: "carriers");

            migrationBuilder.DropIndex(
                name: "IX_carrier_rating_assignments_carrier_id_line_of_business",
                table: "carrier_rating_assignments");

            migrationBuilder.DropIndex(
                name: "IX_carrier_rating_assignments_program_configuration_id_carrier~",
                table: "carrier_rating_assignments");

            migrationBuilder.CreateIndex(
                name: "IX_policy_number_sequences_Name",
                table: "policy_number_sequences",
                column: "Name",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_carriers_Name",
                table: "carriers",
                column: "Name",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_carrier_rating_assignments_carrier_id_line_of_business",
                table: "carrier_rating_assignments",
                columns: new[] { "carrier_id", "line_of_business" },
                unique: true,
                filter: "program_configuration_id IS NULL AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_carrier_rating_assignments_program_configuration_id_carrier~",
                table: "carrier_rating_assignments",
                columns: new[] { "program_configuration_id", "carrier_id", "line_of_business" },
                unique: true,
                filter: "program_configuration_id IS NOT NULL AND is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_policy_number_sequences_Name",
                table: "policy_number_sequences");

            migrationBuilder.DropIndex(
                name: "IX_carriers_Name",
                table: "carriers");

            migrationBuilder.DropIndex(
                name: "IX_carrier_rating_assignments_carrier_id_line_of_business",
                table: "carrier_rating_assignments");

            migrationBuilder.DropIndex(
                name: "IX_carrier_rating_assignments_program_configuration_id_carrier~",
                table: "carrier_rating_assignments");

            migrationBuilder.CreateIndex(
                name: "IX_policy_number_sequences_Name",
                table: "policy_number_sequences",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_carriers_Name",
                table: "carriers",
                column: "Name",
                unique: true);

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
        }
    }
}
