using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLegalScanAuditTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "geocode_precision",
                table: "insureds",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "geocode_provider",
                table: "insureds",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "geocoded_at",
                table: "insureds",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "google_place_id",
                table: "insureds",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "latitude",
                table: "insureds",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "longitude",
                table: "insureds",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "legal_source_scan_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SourceType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResultsFound = table.Column<int>(type: "integer", nullable: false),
                    PossibleChanges = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    StartedById = table.Column<Guid>(type: "uuid", nullable: true),
                    StartedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_legal_source_scan_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_legal_source_scan_runs_users_StartedById",
                        column: x => x.StartedById,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "legal_source_scan_results",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScanRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequirementSectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    State = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Category = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Topic = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    MatchStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SourceCitation = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SourceText = table.Column<string>(type: "text", nullable: false),
                    SuggestedRequirementText = table.Column<string>(type: "text", nullable: true),
                    ConfidenceScore = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    ReviewStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ReviewedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_legal_source_scan_results", x => x.Id);
                    table.ForeignKey(
                        name: "FK_legal_source_scan_results_legal_requirement_sections_Requir~",
                        column: x => x.RequirementSectionId,
                        principalTable: "legal_requirement_sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_legal_source_scan_results_legal_source_scan_runs_ScanRunId",
                        column: x => x.ScanRunId,
                        principalTable: "legal_source_scan_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_legal_source_scan_results_users_ReviewedById",
                        column: x => x.ReviewedById,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "legal_requirement_change_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequirementSectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScanResultId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChangeType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    FieldName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    OldValue = table.Column<string>(type: "text", nullable: true),
                    NewValue = table.Column<string>(type: "text", nullable: true),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ChangedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ChangedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_legal_requirement_change_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_legal_requirement_change_logs_legal_requirement_sections_Re~",
                        column: x => x.RequirementSectionId,
                        principalTable: "legal_requirement_sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_legal_requirement_change_logs_legal_source_scan_results_Sca~",
                        column: x => x.ScanResultId,
                        principalTable: "legal_source_scan_results",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_legal_requirement_change_logs_users_ChangedById",
                        column: x => x.ChangedById,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_legal_requirement_change_logs_ChangedById",
                table: "legal_requirement_change_logs",
                column: "ChangedById");

            migrationBuilder.CreateIndex(
                name: "IX_legal_requirement_change_logs_RequirementSectionId_ChangedAt",
                table: "legal_requirement_change_logs",
                columns: new[] { "RequirementSectionId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_legal_requirement_change_logs_ScanResultId",
                table: "legal_requirement_change_logs",
                column: "ScanResultId");

            migrationBuilder.CreateIndex(
                name: "IX_legal_source_scan_results_MatchStatus",
                table: "legal_source_scan_results",
                column: "MatchStatus");

            migrationBuilder.CreateIndex(
                name: "IX_legal_source_scan_results_RequirementSectionId",
                table: "legal_source_scan_results",
                column: "RequirementSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_legal_source_scan_results_ReviewedById",
                table: "legal_source_scan_results",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_legal_source_scan_results_ReviewStatus",
                table: "legal_source_scan_results",
                column: "ReviewStatus");

            migrationBuilder.CreateIndex(
                name: "IX_legal_source_scan_results_ScanRunId",
                table: "legal_source_scan_results",
                column: "ScanRunId");

            migrationBuilder.CreateIndex(
                name: "IX_legal_source_scan_results_State_Category_Topic",
                table: "legal_source_scan_results",
                columns: new[] { "State", "Category", "Topic" });

            migrationBuilder.CreateIndex(
                name: "IX_legal_source_scan_runs_SourceName_StartedAt",
                table: "legal_source_scan_runs",
                columns: new[] { "SourceName", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_legal_source_scan_runs_StartedById",
                table: "legal_source_scan_runs",
                column: "StartedById");

            migrationBuilder.CreateIndex(
                name: "IX_legal_source_scan_runs_Status",
                table: "legal_source_scan_runs",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "legal_requirement_change_logs");

            migrationBuilder.DropTable(
                name: "legal_source_scan_results");

            migrationBuilder.DropTable(
                name: "legal_source_scan_runs");

            migrationBuilder.DropColumn(
                name: "geocode_precision",
                table: "insureds");

            migrationBuilder.DropColumn(
                name: "geocode_provider",
                table: "insureds");

            migrationBuilder.DropColumn(
                name: "geocoded_at",
                table: "insureds");

            migrationBuilder.DropColumn(
                name: "google_place_id",
                table: "insureds");

            migrationBuilder.DropColumn(
                name: "latitude",
                table: "insureds");

            migrationBuilder.DropColumn(
                name: "longitude",
                table: "insureds");
        }
    }
}
