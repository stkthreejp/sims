using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAttachmentPolicyVersionLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PolicyVersionId",
                table: "attachments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_attachments_PolicyVersionId",
                table: "attachments",
                column: "PolicyVersionId");

            migrationBuilder.AddForeignKey(
                name: "FK_attachments_policy_versions_PolicyVersionId",
                table: "attachments",
                column: "PolicyVersionId",
                principalTable: "policy_versions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_attachments_policy_versions_PolicyVersionId",
                table: "attachments");

            migrationBuilder.DropIndex(
                name: "IX_attachments_PolicyVersionId",
                table: "attachments");

            migrationBuilder.DropColumn(
                name: "PolicyVersionId",
                table: "attachments");
        }
    }
}
