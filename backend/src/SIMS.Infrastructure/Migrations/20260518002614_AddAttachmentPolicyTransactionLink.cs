using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAttachmentPolicyTransactionLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PolicyTransactionId",
                table: "attachments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_attachments_PolicyTransactionId",
                table: "attachments",
                column: "PolicyTransactionId");

            migrationBuilder.AddForeignKey(
                name: "FK_attachments_policy_transactions_PolicyTransactionId",
                table: "attachments",
                column: "PolicyTransactionId",
                principalTable: "policy_transactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_attachments_policy_transactions_PolicyTransactionId",
                table: "attachments");

            migrationBuilder.DropIndex(
                name: "IX_attachments_PolicyTransactionId",
                table: "attachments");

            migrationBuilder.DropColumn(
                name: "PolicyTransactionId",
                table: "attachments");
        }
    }
}
