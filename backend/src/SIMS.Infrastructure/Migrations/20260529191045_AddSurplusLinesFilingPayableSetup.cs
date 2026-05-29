using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSurplusLinesFilingPayableSetup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AffidavitNotes",
                table: "surplus_lines_state_setups",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AffidavitRequired",
                table: "surplus_lines_state_setups",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CreateFilingPayable",
                table: "surplus_lines_state_setups",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DiligentSearchNotes",
                table: "surplus_lines_state_setups",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DiligentSearchRequired",
                table: "surplus_lines_state_setups",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "FilingDueDayOfMonth",
                table: "surplus_lines_state_setups",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FilingFrequency",
                table: "surplus_lines_state_setups",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FilingMethod",
                table: "surplus_lines_state_setups",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FilingPayeeId",
                table: "surplus_lines_state_setups",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FilingPaymentTermsDays",
                table: "surplus_lines_state_setups",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FilingPortalUrl",
                table: "surplus_lines_state_setups",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequiredFilingFormsJson",
                table: "surplus_lines_state_setups",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.CreateIndex(
                name: "IX_surplus_lines_state_setups_FilingPayeeId",
                table: "surplus_lines_state_setups",
                column: "FilingPayeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_surplus_lines_state_setups_payees_FilingPayeeId",
                table: "surplus_lines_state_setups",
                column: "FilingPayeeId",
                principalTable: "payees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_surplus_lines_state_setups_payees_FilingPayeeId",
                table: "surplus_lines_state_setups");

            migrationBuilder.DropIndex(
                name: "IX_surplus_lines_state_setups_FilingPayeeId",
                table: "surplus_lines_state_setups");

            migrationBuilder.DropColumn(
                name: "AffidavitNotes",
                table: "surplus_lines_state_setups");

            migrationBuilder.DropColumn(
                name: "AffidavitRequired",
                table: "surplus_lines_state_setups");

            migrationBuilder.DropColumn(
                name: "CreateFilingPayable",
                table: "surplus_lines_state_setups");

            migrationBuilder.DropColumn(
                name: "DiligentSearchNotes",
                table: "surplus_lines_state_setups");

            migrationBuilder.DropColumn(
                name: "DiligentSearchRequired",
                table: "surplus_lines_state_setups");

            migrationBuilder.DropColumn(
                name: "FilingDueDayOfMonth",
                table: "surplus_lines_state_setups");

            migrationBuilder.DropColumn(
                name: "FilingFrequency",
                table: "surplus_lines_state_setups");

            migrationBuilder.DropColumn(
                name: "FilingMethod",
                table: "surplus_lines_state_setups");

            migrationBuilder.DropColumn(
                name: "FilingPayeeId",
                table: "surplus_lines_state_setups");

            migrationBuilder.DropColumn(
                name: "FilingPaymentTermsDays",
                table: "surplus_lines_state_setups");

            migrationBuilder.DropColumn(
                name: "FilingPortalUrl",
                table: "surplus_lines_state_setups");

            migrationBuilder.DropColumn(
                name: "RequiredFilingFormsJson",
                table: "surplus_lines_state_setups");
        }
    }
}
