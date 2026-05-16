using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPayablePayeeSubledger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "PayeeId",
                table: "payables",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_payables_PayeeId",
                table: "payables",
                column: "PayeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_payables_payees_PayeeId",
                table: "payables",
                column: "PayeeId",
                principalTable: "payees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_payables_payees_PayeeId",
                table: "payables");

            migrationBuilder.DropIndex(
                name: "IX_payables_PayeeId",
                table: "payables");

            migrationBuilder.DropColumn(
                name: "PayeeId",
                table: "payables");
        }
    }
}
