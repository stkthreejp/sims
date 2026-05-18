using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoicePolicyVersionLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PolicyVersionId",
                table: "invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoices_PolicyVersionId",
                table: "invoices",
                column: "PolicyVersionId");

            migrationBuilder.AddForeignKey(
                name: "FK_invoices_policy_versions_PolicyVersionId",
                table: "invoices",
                column: "PolicyVersionId",
                principalTable: "policy_versions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_invoices_policy_versions_PolicyVersionId",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "IX_invoices_PolicyVersionId",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "PolicyVersionId",
                table: "invoices");
        }
    }
}
