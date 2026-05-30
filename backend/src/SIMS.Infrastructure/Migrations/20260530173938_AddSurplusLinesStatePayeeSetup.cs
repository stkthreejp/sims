using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSurplusLinesStatePayeeSetup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "StatePayeeId",
                table: "surplus_lines_state_setups",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_surplus_lines_state_setups_StatePayeeId",
                table: "surplus_lines_state_setups",
                column: "StatePayeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_surplus_lines_state_setups_payees_StatePayeeId",
                table: "surplus_lines_state_setups",
                column: "StatePayeeId",
                principalTable: "payees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_surplus_lines_state_setups_payees_StatePayeeId",
                table: "surplus_lines_state_setups");

            migrationBuilder.DropIndex(
                name: "IX_surplus_lines_state_setups_StatePayeeId",
                table: "surplus_lines_state_setups");

            migrationBuilder.DropColumn(
                name: "StatePayeeId",
                table: "surplus_lines_state_setups");
        }
    }
}
