using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLegalRequirementSections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "legal_requirement_sections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    LineOfBusiness = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Category = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Topic = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    RequirementText = table.Column<string>(type: "text", nullable: false),
                    Citations = table.Column<string[]>(type: "text[]", nullable: false),
                    SourceName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SourceDocument = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    SourceCreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    LastVerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_legal_requirement_sections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_legal_requirement_sections_LineOfBusiness_Action",
                table: "legal_requirement_sections",
                columns: new[] { "LineOfBusiness", "Action" });

            migrationBuilder.CreateIndex(
                name: "IX_legal_requirement_sections_ReviewStatus",
                table: "legal_requirement_sections",
                column: "ReviewStatus");

            migrationBuilder.CreateIndex(
                name: "IX_legal_requirement_sections_State_Category_Topic",
                table: "legal_requirement_sections",
                columns: new[] { "State", "Category", "Topic" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "legal_requirement_sections");
        }
    }
}
