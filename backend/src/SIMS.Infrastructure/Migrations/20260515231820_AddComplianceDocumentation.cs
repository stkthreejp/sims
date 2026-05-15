using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddComplianceDocumentation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "compliance_attestation_campaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Statement = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compliance_attestation_campaigns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_compliance_attestation_campaigns_users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "compliance_attestation_recipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AttestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compliance_attestation_recipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_compliance_attestation_recipients_compliance_attestation_ca~",
                        column: x => x.CampaignId,
                        principalTable: "compliance_attestation_campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_compliance_attestation_recipients_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "compliance_audit_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    FieldName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    OldValue = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    NewValue = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compliance_audit_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_compliance_audit_logs_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "compliance_document_reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ReviewedById = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NextReviewDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compliance_document_reviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_compliance_document_reviews_users_ReviewedById",
                        column: x => x.ReviewedById,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "compliance_document_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    HtmlContent = table.Column<string>(type: "text", nullable: false),
                    PlainText = table.Column<string>(type: "text", nullable: false),
                    ChangeSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compliance_document_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_compliance_document_versions_users_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_compliance_document_versions_users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "compliance_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApproverId = table.Column<Guid>(type: "uuid", nullable: true),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LastReviewedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    NextReviewDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ReviewCadence = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Tags = table.Column<string[]>(type: "text[]", nullable: false),
                    CurrentPublishedVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrentDraftVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compliance_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_compliance_documents_compliance_document_versions_CurrentDr~",
                        column: x => x.CurrentDraftVersionId,
                        principalTable: "compliance_document_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_compliance_documents_compliance_document_versions_CurrentPu~",
                        column: x => x.CurrentPublishedVersionId,
                        principalTable: "compliance_document_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_compliance_documents_users_ApproverId",
                        column: x => x.ApproverId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_compliance_documents_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "compliance_evidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EvidenceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compliance_evidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_compliance_evidence_compliance_document_reviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "compliance_document_reviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_compliance_evidence_compliance_documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "compliance_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_compliance_evidence_users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_compliance_attestation_campaigns_CreatedById",
                table: "compliance_attestation_campaigns",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_attestation_campaigns_DocumentId",
                table: "compliance_attestation_campaigns",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_attestation_campaigns_DueDate",
                table: "compliance_attestation_campaigns",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_attestation_campaigns_VersionId",
                table: "compliance_attestation_campaigns",
                column: "VersionId");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_attestation_recipients_CampaignId_UserId",
                table: "compliance_attestation_recipients",
                columns: new[] { "CampaignId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_compliance_attestation_recipients_UserId",
                table: "compliance_attestation_recipients",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_audit_logs_CreatedAt",
                table: "compliance_audit_logs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_audit_logs_DocumentId",
                table: "compliance_audit_logs",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_audit_logs_UserId",
                table: "compliance_audit_logs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_audit_logs_VersionId",
                table: "compliance_audit_logs",
                column: "VersionId");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_document_reviews_DocumentId",
                table: "compliance_document_reviews",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_document_reviews_ReviewedAt",
                table: "compliance_document_reviews",
                column: "ReviewedAt");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_document_reviews_ReviewedById",
                table: "compliance_document_reviews",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_document_reviews_VersionId",
                table: "compliance_document_reviews",
                column: "VersionId");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_document_versions_ApprovedById",
                table: "compliance_document_versions",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_document_versions_CreatedById",
                table: "compliance_document_versions",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_document_versions_DocumentId_VersionNumber",
                table: "compliance_document_versions",
                columns: new[] { "DocumentId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_compliance_documents_ApproverId",
                table: "compliance_documents",
                column: "ApproverId");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_documents_CurrentDraftVersionId",
                table: "compliance_documents",
                column: "CurrentDraftVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_documents_CurrentPublishedVersionId",
                table: "compliance_documents",
                column: "CurrentPublishedVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_documents_NextReviewDate",
                table: "compliance_documents",
                column: "NextReviewDate");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_documents_OwnerId",
                table: "compliance_documents",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_documents_Status",
                table: "compliance_documents",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_evidence_CreatedById",
                table: "compliance_evidence",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_evidence_DocumentId",
                table: "compliance_evidence",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_evidence_ReviewId",
                table: "compliance_evidence",
                column: "ReviewId");

            migrationBuilder.AddForeignKey(
                name: "FK_compliance_attestation_campaigns_compliance_document_versio~",
                table: "compliance_attestation_campaigns",
                column: "VersionId",
                principalTable: "compliance_document_versions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_compliance_attestation_campaigns_compliance_documents_Docum~",
                table: "compliance_attestation_campaigns",
                column: "DocumentId",
                principalTable: "compliance_documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_compliance_audit_logs_compliance_document_versions_VersionId",
                table: "compliance_audit_logs",
                column: "VersionId",
                principalTable: "compliance_document_versions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_compliance_audit_logs_compliance_documents_DocumentId",
                table: "compliance_audit_logs",
                column: "DocumentId",
                principalTable: "compliance_documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_compliance_document_reviews_compliance_document_versions_Ve~",
                table: "compliance_document_reviews",
                column: "VersionId",
                principalTable: "compliance_document_versions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_compliance_document_reviews_compliance_documents_DocumentId",
                table: "compliance_document_reviews",
                column: "DocumentId",
                principalTable: "compliance_documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_compliance_document_versions_compliance_documents_DocumentId",
                table: "compliance_document_versions",
                column: "DocumentId",
                principalTable: "compliance_documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_compliance_documents_compliance_document_versions_CurrentDr~",
                table: "compliance_documents");

            migrationBuilder.DropForeignKey(
                name: "FK_compliance_documents_compliance_document_versions_CurrentPu~",
                table: "compliance_documents");

            migrationBuilder.DropTable(
                name: "compliance_attestation_recipients");

            migrationBuilder.DropTable(
                name: "compliance_audit_logs");

            migrationBuilder.DropTable(
                name: "compliance_evidence");

            migrationBuilder.DropTable(
                name: "compliance_attestation_campaigns");

            migrationBuilder.DropTable(
                name: "compliance_document_reviews");

            migrationBuilder.DropTable(
                name: "compliance_document_versions");

            migrationBuilder.DropTable(
                name: "compliance_documents");
        }
    }
}
