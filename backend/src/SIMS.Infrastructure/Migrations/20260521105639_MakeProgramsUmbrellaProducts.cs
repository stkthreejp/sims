using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeProgramsUmbrellaProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_program_configurations_carriers_CarrierId",
                table: "program_configurations");

            migrationBuilder.DropIndex(
                name: "IX_program_configurations_CarrierId_LineOfBusiness_StateCode_I~",
                table: "program_configurations");

            migrationBuilder.DropColumn(
                name: "CarrierId",
                table: "program_configurations");

            migrationBuilder.DropColumn(
                name: "LineOfBusiness",
                table: "program_configurations");

            migrationBuilder.DropColumn(
                name: "StateCode",
                table: "program_configurations");

            migrationBuilder.CreateIndex(
                name: "IX_program_configurations_IsActive",
                table: "program_configurations",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_program_configurations_IsActive",
                table: "program_configurations");

            migrationBuilder.AddColumn<Guid>(
                name: "CarrierId",
                table: "program_configurations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LineOfBusiness",
                table: "program_configurations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "StateCode",
                table: "program_configurations",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_program_configurations_CarrierId_LineOfBusiness_StateCode_I~",
                table: "program_configurations",
                columns: new[] { "CarrierId", "LineOfBusiness", "StateCode", "IsActive" });

            migrationBuilder.AddForeignKey(
                name: "FK_program_configurations_carriers_CarrierId",
                table: "program_configurations",
                column: "CarrierId",
                principalTable: "carriers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
