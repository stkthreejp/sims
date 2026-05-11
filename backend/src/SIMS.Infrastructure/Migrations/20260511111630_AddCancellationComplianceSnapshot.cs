using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCancellationComplianceSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationComplianceChecklistJson",
                table: "policy_transactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationLegalRequirementSnapshotJson",
                table: "policy_transactions",
                type: "text",
                nullable: true);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancellationComplianceChecklistJson",
                table: "policy_transactions");

            migrationBuilder.DropColumn(
                name: "CancellationLegalRequirementSnapshotJson",
                table: "policy_transactions");
        }
    }
}
