using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInsuredAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "InsuredId",
                table: "attachments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_attachments_EntityType_InsuredId_IsDeleted",
                table: "attachments",
                columns: new[] { "EntityType", "InsuredId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_attachments_InsuredId",
                table: "attachments",
                column: "InsuredId");

            migrationBuilder.AddForeignKey(
                name: "FK_attachments_insureds_InsuredId",
                table: "attachments",
                column: "InsuredId",
                principalTable: "insureds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_attachments_insureds_InsuredId",
                table: "attachments");

            migrationBuilder.DropIndex(
                name: "IX_attachments_EntityType_InsuredId_IsDeleted",
                table: "attachments");

            migrationBuilder.DropIndex(
                name: "IX_attachments_InsuredId",
                table: "attachments");

            migrationBuilder.DropColumn(
                name: "InsuredId",
                table: "attachments");
        }
    }
}
