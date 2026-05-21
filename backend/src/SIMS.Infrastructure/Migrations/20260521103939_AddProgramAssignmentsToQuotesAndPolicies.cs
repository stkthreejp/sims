using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramAssignmentsToQuotesAndPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProgramId",
                table: "quotes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProgramId",
                table: "policies",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_quotes_ProgramId",
                table: "quotes",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_policies_ProgramId",
                table: "policies",
                column: "ProgramId");

            migrationBuilder.AddForeignKey(
                name: "FK_policies_program_configurations_ProgramId",
                table: "policies",
                column: "ProgramId",
                principalTable: "program_configurations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_quotes_program_configurations_ProgramId",
                table: "quotes",
                column: "ProgramId",
                principalTable: "program_configurations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_policies_program_configurations_ProgramId",
                table: "policies");

            migrationBuilder.DropForeignKey(
                name: "FK_quotes_program_configurations_ProgramId",
                table: "quotes");

            migrationBuilder.DropIndex(
                name: "IX_quotes_ProgramId",
                table: "quotes");

            migrationBuilder.DropIndex(
                name: "IX_policies_ProgramId",
                table: "policies");

            migrationBuilder.DropColumn(
                name: "ProgramId",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "ProgramId",
                table: "policies");
        }
    }
}
