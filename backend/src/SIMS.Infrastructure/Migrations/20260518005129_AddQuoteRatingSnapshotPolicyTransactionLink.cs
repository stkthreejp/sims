using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteRatingSnapshotPolicyTransactionLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "policy_transaction_id",
                table: "quote_rating_snapshots",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_quote_rating_snapshots_policy_transaction_id",
                table: "quote_rating_snapshots",
                column: "policy_transaction_id");

            migrationBuilder.AddForeignKey(
                name: "FK_quote_rating_snapshots_policy_transactions_policy_transacti~",
                table: "quote_rating_snapshots",
                column: "policy_transaction_id",
                principalTable: "policy_transactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_quote_rating_snapshots_policy_transactions_policy_transacti~",
                table: "quote_rating_snapshots");

            migrationBuilder.DropIndex(
                name: "IX_quote_rating_snapshots_policy_transaction_id",
                table: "quote_rating_snapshots");

            migrationBuilder.DropColumn(
                name: "policy_transaction_id",
                table: "quote_rating_snapshots");
        }
    }
}
