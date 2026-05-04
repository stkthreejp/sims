using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Rating_AddMakerCheckerFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "rating_plan_versions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastEditedById",
                table: "rating_plan_versions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_rating_plan_versions_CreatedById",
                table: "rating_plan_versions",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_rating_plan_versions_LastEditedById",
                table: "rating_plan_versions",
                column: "LastEditedById");

            migrationBuilder.AddForeignKey(
                name: "FK_rating_plan_versions_AspNetUsers_CreatedById",
                table: "rating_plan_versions",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_rating_plan_versions_AspNetUsers_LastEditedById",
                table: "rating_plan_versions",
                column: "LastEditedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_rating_plan_versions_AspNetUsers_CreatedById",
                table: "rating_plan_versions");

            migrationBuilder.DropForeignKey(
                name: "FK_rating_plan_versions_AspNetUsers_LastEditedById",
                table: "rating_plan_versions");

            migrationBuilder.DropIndex(
                name: "IX_rating_plan_versions_CreatedById",
                table: "rating_plan_versions");

            migrationBuilder.DropIndex(
                name: "IX_rating_plan_versions_LastEditedById",
                table: "rating_plan_versions");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "rating_plan_versions");

            migrationBuilder.DropColumn(
                name: "LastEditedById",
                table: "rating_plan_versions");
        }
    }
}
