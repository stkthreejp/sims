using System;
using SIMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260507005550_AddSubmissionLossHistory")]
    public partial class AddSubmissionLossHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "submission_loss_years",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyYear = table.Column<int>(type: "integer", nullable: false),
                    LineOfBusiness = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CarrierName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PolicyNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PremiumAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PremiumBasis = table.Column<int>(type: "integer", nullable: false),
                    IsSmmWritten = table.Column<bool>(type: "boolean", nullable: false),
                    Source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AsOfDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PaidOverride = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ReservedOverride = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ExpenseOverride = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submission_loss_years", x => x.Id);
                    table.ForeignKey(
                        name: "FK_submission_loss_years_submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "submission_loss_claims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionLossYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    DateOfLoss = table.Column<DateOnly>(type: "date", nullable: true),
                    ClaimNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CoverageType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Paid = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Reserved = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Expense = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submission_loss_claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_submission_loss_claims_submission_loss_years_SubmissionLossY~",
                        column: x => x.SubmissionLossYearId,
                        principalTable: "submission_loss_years",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_submission_loss_claims_SubmissionLossYearId",
                table: "submission_loss_claims",
                column: "SubmissionLossYearId");

            migrationBuilder.CreateIndex(
                name: "IX_submission_loss_years_SubmissionId",
                table: "submission_loss_years",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_submission_loss_years_SubmissionId_PolicyYear_LineOfBusiness",
                table: "submission_loss_years",
                columns: new[] { "SubmissionId", "PolicyYear", "LineOfBusiness" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "submission_loss_claims");

            migrationBuilder.DropTable(
                name: "submission_loss_years");
        }
    }
}
