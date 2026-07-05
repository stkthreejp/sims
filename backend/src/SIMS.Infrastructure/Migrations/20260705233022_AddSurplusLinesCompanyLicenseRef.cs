using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSurplusLinesCompanyLicenseRef : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CompanyLicenseId",
                table: "surplus_lines_state_setups",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_surplus_lines_state_setups_CompanyLicenseId",
                table: "surplus_lines_state_setups",
                column: "CompanyLicenseId");

            migrationBuilder.AddForeignKey(
                name: "FK_surplus_lines_state_setups_company_licenses_CompanyLicenseId",
                table: "surplus_lines_state_setups",
                column: "CompanyLicenseId",
                principalTable: "company_licenses",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_surplus_lines_state_setups_company_licenses_CompanyLicenseId",
                table: "surplus_lines_state_setups");

            migrationBuilder.DropIndex(
                name: "IX_surplus_lines_state_setups_CompanyLicenseId",
                table: "surplus_lines_state_setups");

            migrationBuilder.DropColumn(
                name: "CompanyLicenseId",
                table: "surplus_lines_state_setups");
        }
    }
}
