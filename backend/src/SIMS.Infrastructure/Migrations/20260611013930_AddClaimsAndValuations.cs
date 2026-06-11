using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimsAndValuations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "claim_import_batches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CarrierName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TpaName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ValuationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RecordCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedCount = table.Column<int>(type: "integer", nullable: false),
                    UpdatedCount = table.Column<int>(type: "integer", nullable: false),
                    SkippedCount = table.Column<int>(type: "integer", nullable: false),
                    ErrorCount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ErrorSummaryJson = table.Column<string>(type: "text", nullable: true),
                    ImportedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_claim_import_batches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_claim_import_batches_users_ImportedById",
                        column: x => x.ImportedById,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "claims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyId = table.Column<Guid>(type: "uuid", nullable: true),
                    PolicyNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    InsuredId = table.Column<Guid>(type: "uuid", nullable: true),
                    InsuredName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ClaimNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CarrierClaimNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SourcePolicyReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Account = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CarrierName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DateOfLoss = table.Column<DateOnly>(type: "date", nullable: false),
                    ReportDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ClosedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CoverageType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ClaimTypeDesc = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LossCause = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RiskState = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    AccidentState = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    ClaimantName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AdjusterName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TpaName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TpaClaimNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Paid = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Reserved = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Expense = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Recovery = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Incurred = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LastValuationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ImportBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsManualEntry = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_claims_claim_import_batches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalTable: "claim_import_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_claims_policies_PolicyId",
                        column: x => x.PolicyId,
                        principalTable: "policies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "claim_valuations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: false),
                    ValuationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Paid = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Reserved = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Expense = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Recovery = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Incurred = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ImportBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_claim_valuations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_claim_valuations_claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_claim_import_batches_CreatedAt",
                table: "claim_import_batches",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_claim_import_batches_ImportedById",
                table: "claim_import_batches",
                column: "ImportedById");

            migrationBuilder.CreateIndex(
                name: "IX_claim_import_batches_ValuationDate",
                table: "claim_import_batches",
                column: "ValuationDate");

            migrationBuilder.CreateIndex(
                name: "IX_claim_valuations_ClaimId_ValuationDate",
                table: "claim_valuations",
                columns: new[] { "ClaimId", "ValuationDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_claim_valuations_ValuationDate",
                table: "claim_valuations",
                column: "ValuationDate");

            migrationBuilder.CreateIndex(
                name: "IX_claims_Account",
                table: "claims",
                column: "Account");

            migrationBuilder.CreateIndex(
                name: "IX_claims_DateOfLoss",
                table: "claims",
                column: "DateOfLoss");

            migrationBuilder.CreateIndex(
                name: "IX_claims_ImportBatchId",
                table: "claims",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_claims_InsuredId",
                table: "claims",
                column: "InsuredId");

            migrationBuilder.CreateIndex(
                name: "IX_claims_PolicyId",
                table: "claims",
                column: "PolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_claims_SourcePolicyReference_ClaimNumber",
                table: "claims",
                columns: new[] { "SourcePolicyReference", "ClaimNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_claims_Status",
                table: "claims",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "claim_valuations");

            migrationBuilder.DropTable(
                name: "claims");

            migrationBuilder.DropTable(
                name: "claim_import_batches");
        }
    }
}
