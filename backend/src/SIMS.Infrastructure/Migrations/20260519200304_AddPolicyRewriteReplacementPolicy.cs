using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPolicyRewriteReplacementPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReplacementPolicyId",
                table: "policy_rewrite_details",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_policy_rewrite_details_ReplacementPolicyId",
                table: "policy_rewrite_details",
                column: "ReplacementPolicyId");

            migrationBuilder.AddForeignKey(
                name: "FK_policy_rewrite_details_policies_ReplacementPolicyId",
                table: "policy_rewrite_details",
                column: "ReplacementPolicyId",
                principalTable: "policies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_policy_rewrite_details_policies_ReplacementPolicyId",
                table: "policy_rewrite_details");

            migrationBuilder.DropIndex(
                name: "IX_policy_rewrite_details_ReplacementPolicyId",
                table: "policy_rewrite_details");

            migrationBuilder.DropColumn(
                name: "ReplacementPolicyId",
                table: "policy_rewrite_details");
        }
    }
}
