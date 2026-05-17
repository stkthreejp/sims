using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPolicyVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "policy_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    CreatedByPolicyTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    PriorPolicyVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpirationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PremiumAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxesAndFees = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalPremium = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CoverageSnapshotJson = table.Column<string>(type: "text", nullable: false),
                    ExposureSnapshotJson = table.Column<string>(type: "text", nullable: false),
                    RatingSnapshotId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_policy_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_policy_versions_policies_PolicyId",
                        column: x => x.PolicyId,
                        principalTable: "policies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_policy_versions_policy_transactions_CreatedByPolicyTransact~",
                        column: x => x.CreatedByPolicyTransactionId,
                        principalTable: "policy_transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_policy_versions_policy_versions_PriorPolicyVersionId",
                        column: x => x.PriorPolicyVersionId,
                        principalTable: "policy_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_policy_versions_quote_rating_snapshots_RatingSnapshotId",
                        column: x => x.RatingSnapshotId,
                        principalTable: "quote_rating_snapshots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_policy_versions_users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_policy_versions_CreatedById",
                table: "policy_versions",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_policy_versions_CreatedByPolicyTransactionId",
                table: "policy_versions",
                column: "CreatedByPolicyTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_policy_versions_PolicyId_VersionNumber",
                table: "policy_versions",
                columns: new[] { "PolicyId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_policy_versions_PriorPolicyVersionId",
                table: "policy_versions",
                column: "PriorPolicyVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_policy_versions_RatingSnapshotId",
                table: "policy_versions",
                column: "RatingSnapshotId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "policy_versions");
        }
    }
}
