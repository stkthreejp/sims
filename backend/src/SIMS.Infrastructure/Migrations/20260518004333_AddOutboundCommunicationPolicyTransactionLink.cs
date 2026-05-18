using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboundCommunicationPolicyTransactionLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PolicyTransactionId",
                table: "outbound_communications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Purpose",
                table: "outbound_communications",
                type: "integer",
                nullable: false,
                defaultValue: 9);

            migrationBuilder.CreateIndex(
                name: "IX_outbound_communications_PolicyTransactionId",
                table: "outbound_communications",
                column: "PolicyTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_outbound_communications_Purpose",
                table: "outbound_communications",
                column: "Purpose");

            migrationBuilder.AddForeignKey(
                name: "FK_outbound_communications_policy_transactions_PolicyTransacti~",
                table: "outbound_communications",
                column: "PolicyTransactionId",
                principalTable: "policy_transactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_outbound_communications_policy_transactions_PolicyTransacti~",
                table: "outbound_communications");

            migrationBuilder.DropIndex(
                name: "IX_outbound_communications_PolicyTransactionId",
                table: "outbound_communications");

            migrationBuilder.DropIndex(
                name: "IX_outbound_communications_Purpose",
                table: "outbound_communications");

            migrationBuilder.DropColumn(
                name: "PolicyTransactionId",
                table: "outbound_communications");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "outbound_communications");
        }
    }
}
