using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUnderwritingGuidelineControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO permissions ("Name", "DisplayName", "Category")
                SELECT 'admin.underwriting-controls.manage', 'Manage Underwriting Control Setup', 'Admin'
                WHERE NOT EXISTS (
                    SELECT 1 FROM permissions WHERE "Name" = 'admin.underwriting-controls.manage'
                );

                INSERT INTO permissions ("Name", "DisplayName", "Category")
                SELECT 'admin.underwriting-controls.publish', 'Publish Underwriting Controls', 'Admin'
                WHERE NOT EXISTS (
                    SELECT 1 FROM permissions WHERE "Name" = 'admin.underwriting-controls.publish'
                );

                INSERT INTO role_permissions ("RoleId", "PermissionId")
                SELECT r."Id", p."Id"
                FROM roles r
                JOIN permissions p ON p."Name" IN (
                    'admin.underwriting-controls.manage',
                    'admin.underwriting-controls.publish'
                )
                WHERE r."Name" = 'Admin'
                ON CONFLICT DO NOTHING;
                """);

            migrationBuilder.CreateTable(
                name: "underwriting_guideline_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CarrierId = table.Column<Guid>(type: "uuid", nullable: true),
                    LineOfBusiness = table.Column<int>(type: "integer", nullable: false),
                    StateCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Title = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    SourceFileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    SourceBlobName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_underwriting_guideline_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_underwriting_guideline_documents_carriers_CarrierId",
                        column: x => x.CarrierId,
                        principalTable: "carriers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_underwriting_guideline_documents_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "underwriting_guideline_controls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GuidelineDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CarrierId = table.Column<Guid>(type: "uuid", nullable: true),
                    LineOfBusiness = table.Column<int>(type: "integer", nullable: false),
                    StateCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ItemType = table.Column<int>(type: "integer", nullable: false),
                    Stage = table.Column<int>(type: "integer", nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RuleKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Label = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ConditionJson = table.Column<string>(type: "jsonb", nullable: true),
                    IsBlocking = table.Column<bool>(type: "boolean", nullable: false),
                    OverrideAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    OverridePermission = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SourceCitation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AiConfidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PublishedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RetiredByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RetiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RetirementReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_underwriting_guideline_controls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_underwriting_guideline_controls_carriers_CarrierId",
                        column: x => x.CarrierId,
                        principalTable: "carriers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_underwriting_guideline_controls_underwriting_guideline_docu~",
                        column: x => x.GuidelineDocumentId,
                        principalTable: "underwriting_guideline_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_underwriting_guideline_controls_users_PublishedByUserId",
                        column: x => x.PublishedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_underwriting_guideline_controls_users_RetiredByUserId",
                        column: x => x.RetiredByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_underwriting_guideline_controls_users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "underwriting_guideline_audit_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GuidelineDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    GuidelineControlId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    BeforeJson = table.Column<string>(type: "jsonb", nullable: true),
                    AfterJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_underwriting_guideline_audit_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_underwriting_guideline_audit_logs_underwriting_guideline_co~",
                        column: x => x.GuidelineControlId,
                        principalTable: "underwriting_guideline_controls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_underwriting_guideline_audit_logs_underwriting_guideline_do~",
                        column: x => x.GuidelineDocumentId,
                        principalTable: "underwriting_guideline_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_underwriting_guideline_audit_logs_users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_underwriting_guideline_audit_logs_ActorUserId",
                table: "underwriting_guideline_audit_logs",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_underwriting_guideline_audit_logs_GuidelineControlId_Create~",
                table: "underwriting_guideline_audit_logs",
                columns: new[] { "GuidelineControlId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_underwriting_guideline_audit_logs_GuidelineDocumentId_Creat~",
                table: "underwriting_guideline_audit_logs",
                columns: new[] { "GuidelineDocumentId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_underwriting_guideline_controls_CarrierId",
                table: "underwriting_guideline_controls",
                column: "CarrierId");

            migrationBuilder.CreateIndex(
                name: "IX_underwriting_guideline_controls_GuidelineDocumentId_RuleKey",
                table: "underwriting_guideline_controls",
                columns: new[] { "GuidelineDocumentId", "RuleKey" });

            migrationBuilder.CreateIndex(
                name: "IX_underwriting_guideline_controls_PublishedByUserId",
                table: "underwriting_guideline_controls",
                column: "PublishedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_underwriting_guideline_controls_RetiredByUserId",
                table: "underwriting_guideline_controls",
                column: "RetiredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_underwriting_guideline_controls_ReviewedByUserId",
                table: "underwriting_guideline_controls",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_underwriting_guideline_controls_Status_ProgramName_CarrierI~",
                table: "underwriting_guideline_controls",
                columns: new[] { "Status", "ProgramName", "CarrierId", "LineOfBusiness", "StateCode" });

            migrationBuilder.CreateIndex(
                name: "IX_underwriting_guideline_documents_CarrierId",
                table: "underwriting_guideline_documents",
                column: "CarrierId");

            migrationBuilder.CreateIndex(
                name: "IX_underwriting_guideline_documents_CreatedByUserId",
                table: "underwriting_guideline_documents",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_underwriting_guideline_documents_ProgramName_CarrierId_Line~",
                table: "underwriting_guideline_documents",
                columns: new[] { "ProgramName", "CarrierId", "LineOfBusiness", "StateCode", "Version" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "underwriting_guideline_audit_logs");

            migrationBuilder.DropTable(
                name: "underwriting_guideline_controls");

            migrationBuilder.DropTable(
                name: "underwriting_guideline_documents");

            migrationBuilder.Sql("""
                DELETE FROM role_permissions
                WHERE "PermissionId" IN (
                    SELECT "Id" FROM permissions
                    WHERE "Name" IN (
                        'admin.underwriting-controls.manage',
                        'admin.underwriting-controls.publish'
                    )
                );

                DELETE FROM permissions
                WHERE "Name" IN (
                    'admin.underwriting-controls.manage',
                    'admin.underwriting-controls.publish'
                );
                """);
        }
    }
}
