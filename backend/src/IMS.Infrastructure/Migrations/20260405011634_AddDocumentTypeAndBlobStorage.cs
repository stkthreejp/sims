using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentTypeAndBlobStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "StoredFileName",
                table: "attachments",
                newName: "BlobPath");

            migrationBuilder.AlterColumn<Guid>(
                name: "QuoteId",
                table: "attachments",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "AgentId",
                table: "attachments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CarrierId",
                table: "attachments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DocumentType",
                table: "attachments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EntityType",
                table: "attachments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "SubmissionId",
                table: "attachments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_attachments_AgentId",
                table: "attachments",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_attachments_CarrierId",
                table: "attachments",
                column: "CarrierId");

            migrationBuilder.CreateIndex(
                name: "IX_attachments_SubmissionId",
                table: "attachments",
                column: "SubmissionId");

            migrationBuilder.AddForeignKey(
                name: "FK_attachments_agents_AgentId",
                table: "attachments",
                column: "AgentId",
                principalTable: "agents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_attachments_carriers_CarrierId",
                table: "attachments",
                column: "CarrierId",
                principalTable: "carriers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_attachments_submissions_SubmissionId",
                table: "attachments",
                column: "SubmissionId",
                principalTable: "submissions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_attachments_agents_AgentId",
                table: "attachments");

            migrationBuilder.DropForeignKey(
                name: "FK_attachments_carriers_CarrierId",
                table: "attachments");

            migrationBuilder.DropForeignKey(
                name: "FK_attachments_submissions_SubmissionId",
                table: "attachments");

            migrationBuilder.DropIndex(
                name: "IX_attachments_AgentId",
                table: "attachments");

            migrationBuilder.DropIndex(
                name: "IX_attachments_CarrierId",
                table: "attachments");

            migrationBuilder.DropIndex(
                name: "IX_attachments_SubmissionId",
                table: "attachments");

            migrationBuilder.DropColumn(
                name: "AgentId",
                table: "attachments");

            migrationBuilder.DropColumn(
                name: "CarrierId",
                table: "attachments");

            migrationBuilder.DropColumn(
                name: "DocumentType",
                table: "attachments");

            migrationBuilder.DropColumn(
                name: "EntityType",
                table: "attachments");

            migrationBuilder.DropColumn(
                name: "SubmissionId",
                table: "attachments");

            migrationBuilder.RenameColumn(
                name: "BlobPath",
                table: "attachments",
                newName: "StoredFileName");

            migrationBuilder.AlterColumn<Guid>(
                name: "QuoteId",
                table: "attachments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
