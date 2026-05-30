using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRenewingPolicyIdToSubmission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RenewingPolicyId",
                table: "submissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_submissions_RenewingPolicyId",
                table: "submissions",
                column: "RenewingPolicyId");

            migrationBuilder.AddForeignKey(
                name: "FK_submissions_policies_RenewingPolicyId",
                table: "submissions",
                column: "RenewingPolicyId",
                principalTable: "policies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_submissions_policies_RenewingPolicyId",
                table: "submissions");

            migrationBuilder.DropIndex(
                name: "IX_submissions_RenewingPolicyId",
                table: "submissions");

            migrationBuilder.DropColumn(
                name: "RenewingPolicyId",
                table: "submissions");
        }
    }
}
