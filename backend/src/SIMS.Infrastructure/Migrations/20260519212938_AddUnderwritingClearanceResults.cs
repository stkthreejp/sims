using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUnderwritingClearanceResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UnderwritingClearanceResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    MatchedRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    MatchedRecordLabel = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Explanation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ReviewedById = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnderwritingClearanceResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnderwritingClearanceResults_submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnderwritingClearanceResults_users_ReviewedById",
                        column: x => x.ReviewedById,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UnderwritingClearanceResults_ReviewedById",
                table: "UnderwritingClearanceResults",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_UnderwritingClearanceResults_SubmissionId_CheckType",
                table: "UnderwritingClearanceResults",
                columns: new[] { "SubmissionId", "CheckType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UnderwritingClearanceResults");
        }
    }
}
