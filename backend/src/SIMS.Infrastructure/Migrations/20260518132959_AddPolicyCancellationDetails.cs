using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPolicyCancellationDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "policy_cancellation_details",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ReasonLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ReasonCategory = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ReasonLanguageTemplate = table.Column<string>(type: "text", nullable: false),
                    ReasonInputsJson = table.Column<string>(type: "text", nullable: false),
                    ResolvedReasonLanguage = table.Column<string>(type: "text", nullable: false),
                    NoticeMailingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    NoticeRequirementDays = table.Column<int>(type: "integer", nullable: false),
                    MailingDays = table.Column<int>(type: "integer", nullable: false),
                    CancellationEffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Method = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NoticeTemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    LegalRequirementSnapshotJson = table.Column<string>(type: "text", nullable: true),
                    ComplianceChecklistSnapshotJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_policy_cancellation_details", x => x.Id);
                    table.ForeignKey(
                        name: "FK_policy_cancellation_details_DocumentTemplates_NoticeTemplat~",
                        column: x => x.NoticeTemplateId,
                        principalTable: "DocumentTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_policy_cancellation_details_policy_transactions_PolicyTrans~",
                        column: x => x.PolicyTransactionId,
                        principalTable: "policy_transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_policy_cancellation_details_NoticeTemplateId",
                table: "policy_cancellation_details",
                column: "NoticeTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_policy_cancellation_details_PolicyTransactionId",
                table: "policy_cancellation_details",
                column: "PolicyTransactionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "policy_cancellation_details");
        }
    }
}
