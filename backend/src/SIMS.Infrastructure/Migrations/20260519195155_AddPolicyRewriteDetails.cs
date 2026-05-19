using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPolicyRewriteDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "policy_rewrite_details",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourcePolicyId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourcePolicyVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReplacementQuoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_policy_rewrite_details", x => x.Id);
                    table.ForeignKey(
                        name: "FK_policy_rewrite_details_policies_SourcePolicyId",
                        column: x => x.SourcePolicyId,
                        principalTable: "policies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_policy_rewrite_details_policy_transactions_PolicyTransactio~",
                        column: x => x.PolicyTransactionId,
                        principalTable: "policy_transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_policy_rewrite_details_policy_versions_SourcePolicyVersionId",
                        column: x => x.SourcePolicyVersionId,
                        principalTable: "policy_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_policy_rewrite_details_quotes_ReplacementQuoteId",
                        column: x => x.ReplacementQuoteId,
                        principalTable: "quotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_policy_rewrite_details_PolicyTransactionId",
                table: "policy_rewrite_details",
                column: "PolicyTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_policy_rewrite_details_ReplacementQuoteId",
                table: "policy_rewrite_details",
                column: "ReplacementQuoteId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_policy_rewrite_details_SourcePolicyId",
                table: "policy_rewrite_details",
                column: "SourcePolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_policy_rewrite_details_SourcePolicyVersionId",
                table: "policy_rewrite_details",
                column: "SourcePolicyVersionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "policy_rewrite_details");
        }
    }
}
