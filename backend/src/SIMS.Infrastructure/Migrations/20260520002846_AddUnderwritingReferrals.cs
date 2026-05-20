using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUnderwritingReferrals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UnderwritingAppetiteResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteId = table.Column<Guid>(type: "uuid", nullable: true),
                    RuleCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RuleName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Triggered = table.Column<bool>(type: "boolean", nullable: false),
                    ReferralRequired = table.Column<bool>(type: "boolean", nullable: false),
                    Explanation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EvaluatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    EvaluatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnderwritingAppetiteResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnderwritingAppetiteResults_quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "quotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UnderwritingAppetiteResults_submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnderwritingAppetiteResults_users_EvaluatedById",
                        column: x => x.EvaluatedById,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UnderwritingReferrals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReferralType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Required = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RequestedById = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DecisionById = table.Column<Guid>(type: "uuid", nullable: true),
                    DecisionAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecisionNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnderwritingReferrals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnderwritingReferrals_quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "quotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UnderwritingReferrals_submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnderwritingReferrals_users_DecisionById",
                        column: x => x.DecisionById,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UnderwritingReferrals_users_RequestedById",
                        column: x => x.RequestedById,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UnderwritingAppetiteResults_EvaluatedById",
                table: "UnderwritingAppetiteResults",
                column: "EvaluatedById");

            migrationBuilder.CreateIndex(
                name: "IX_UnderwritingAppetiteResults_QuoteId",
                table: "UnderwritingAppetiteResults",
                column: "QuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_UnderwritingAppetiteResults_SubmissionId_QuoteId_RuleCode",
                table: "UnderwritingAppetiteResults",
                columns: new[] { "SubmissionId", "QuoteId", "RuleCode" });

            migrationBuilder.CreateIndex(
                name: "IX_UnderwritingReferrals_DecisionById",
                table: "UnderwritingReferrals",
                column: "DecisionById");

            migrationBuilder.CreateIndex(
                name: "IX_UnderwritingReferrals_QuoteId",
                table: "UnderwritingReferrals",
                column: "QuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_UnderwritingReferrals_RequestedById",
                table: "UnderwritingReferrals",
                column: "RequestedById");

            migrationBuilder.CreateIndex(
                name: "IX_UnderwritingReferrals_SubmissionId_QuoteId_ReferralType",
                table: "UnderwritingReferrals",
                columns: new[] { "SubmissionId", "QuoteId", "ReferralType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UnderwritingAppetiteResults");

            migrationBuilder.DropTable(
                name: "UnderwritingReferrals");
        }
    }
}
