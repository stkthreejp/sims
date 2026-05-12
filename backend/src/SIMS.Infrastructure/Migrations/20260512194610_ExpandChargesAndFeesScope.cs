using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExpandChargesAndFeesScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_carrier_additional_interest_rates_carriers_CarrierId",
                table: "carrier_additional_interest_rates");

            migrationBuilder.AddColumn<Guid>(
                name: "CarrierId",
                table: "fee_rule_versions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "LineOfBusiness",
                table: "carrier_additional_interest_rates",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<Guid>(
                name: "CarrierId",
                table: "carrier_additional_interest_rates",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateIndex(
                name: "ix_fee_rule_carrier_lob_lookup",
                table: "fee_rule_versions",
                columns: new[] { "FeeDefinitionId", "CarrierId", "LineOfBusiness", "StateCode", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_fee_rule_versions_CarrierId",
                table: "fee_rule_versions",
                column: "CarrierId");

            migrationBuilder.AddForeignKey(
                name: "FK_carrier_additional_interest_rates_carriers_CarrierId",
                table: "carrier_additional_interest_rates",
                column: "CarrierId",
                principalTable: "carriers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_fee_rule_versions_carriers_CarrierId",
                table: "fee_rule_versions",
                column: "CarrierId",
                principalTable: "carriers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_carrier_additional_interest_rates_carriers_CarrierId",
                table: "carrier_additional_interest_rates");

            migrationBuilder.DropForeignKey(
                name: "FK_fee_rule_versions_carriers_CarrierId",
                table: "fee_rule_versions");

            migrationBuilder.DropIndex(
                name: "ix_fee_rule_carrier_lob_lookup",
                table: "fee_rule_versions");

            migrationBuilder.DropIndex(
                name: "IX_fee_rule_versions_CarrierId",
                table: "fee_rule_versions");

            migrationBuilder.DropColumn(
                name: "CarrierId",
                table: "fee_rule_versions");

            migrationBuilder.AlterColumn<int>(
                name: "LineOfBusiness",
                table: "carrier_additional_interest_rates",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CarrierId",
                table: "carrier_additional_interest_rates",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_carrier_additional_interest_rates_carriers_CarrierId",
                table: "carrier_additional_interest_rates",
                column: "CarrierId",
                principalTable: "carriers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
