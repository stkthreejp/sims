using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailIngestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inbound_emails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FromAddress = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    FromName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BodyText = table.Column<string>(type: "text", nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LinkedSubmissionId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsProcessed = table.Column<bool>(type: "boolean", nullable: false),
                    GraphMessageId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbound_emails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inbound_emails_submissions_LinkedSubmissionId",
                        column: x => x.LinkedSubmissionId,
                        principalTable: "submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "email_attachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InboundEmailId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BlobUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    DocumentType = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_email_attachments_inbound_emails_InboundEmailId",
                        column: x => x.InboundEmailId,
                        principalTable: "inbound_emails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_email_attachments_InboundEmailId",
                table: "email_attachments",
                column: "InboundEmailId");

            migrationBuilder.CreateIndex(
                name: "IX_inbound_emails_GraphMessageId",
                table: "inbound_emails",
                column: "GraphMessageId",
                unique: true,
                filter: "\"GraphMessageId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_inbound_emails_LinkedSubmissionId",
                table: "inbound_emails",
                column: "LinkedSubmissionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_attachments");

            migrationBuilder.DropTable(
                name: "inbound_emails");
        }
    }
}
