using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPolicyTransactionComplianceChecklists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "policy_transaction_compliance_checklists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_policy_transaction_compliance_checklists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_policy_transaction_compliance_checklists_policy_transaction~",
                        column: x => x.PolicyTransactionId,
                        principalTable: "policy_transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "policy_transaction_compliance_checklist_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyTransactionComplianceChecklistId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Label = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    LegalRequirementSectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SnapshotJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_policy_transaction_compliance_checklist_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_policy_transaction_compliance_checklist_items_legal_require~",
                        column: x => x.LegalRequirementSectionId,
                        principalTable: "legal_requirement_sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_policy_transaction_compliance_checklist_items_policy_transa~",
                        column: x => x.PolicyTransactionComplianceChecklistId,
                        principalTable: "policy_transaction_compliance_checklists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_policy_transaction_compliance_checklist_items_users_Complet~",
                        column: x => x.CompletedById,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_policy_transaction_compliance_checklist_items_CompletedById",
                table: "policy_transaction_compliance_checklist_items",
                column: "CompletedById");

            migrationBuilder.CreateIndex(
                name: "IX_policy_transaction_compliance_checklist_items_LegalRequirem~",
                table: "policy_transaction_compliance_checklist_items",
                column: "LegalRequirementSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_policy_transaction_compliance_checklist_items_PolicyTransac~",
                table: "policy_transaction_compliance_checklist_items",
                column: "PolicyTransactionComplianceChecklistId");

            migrationBuilder.CreateIndex(
                name: "IX_policy_transaction_compliance_checklists_PolicyTransactionId",
                table: "policy_transaction_compliance_checklists",
                column: "PolicyTransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "policy_transaction_compliance_checklist_items");

            migrationBuilder.DropTable(
                name: "policy_transaction_compliance_checklists");
        }
    }
}
