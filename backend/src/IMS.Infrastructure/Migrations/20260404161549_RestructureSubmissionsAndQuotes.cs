using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RestructureSubmissionsAndQuotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_attachments_policies_PolicyId",
                table: "attachments");

            migrationBuilder.DropForeignKey(
                name: "FK_notes_policies_PolicyId",
                table: "notes");

            migrationBuilder.DropForeignKey(
                name: "FK_policy_transactions_policies_PolicyId",
                table: "policy_transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_policy_transactions_policies_RenewalPolicyId",
                table: "policy_transactions");

            migrationBuilder.DropTable(
                name: "policies");

            migrationBuilder.RenameColumn(
                name: "RenewalPolicyId",
                table: "policy_transactions",
                newName: "RenewalQuoteId");

            migrationBuilder.RenameColumn(
                name: "PolicyId",
                table: "policy_transactions",
                newName: "QuoteId");

            migrationBuilder.RenameIndex(
                name: "IX_policy_transactions_RenewalPolicyId",
                table: "policy_transactions",
                newName: "IX_policy_transactions_RenewalQuoteId");

            migrationBuilder.RenameIndex(
                name: "IX_policy_transactions_PolicyId",
                table: "policy_transactions",
                newName: "IX_policy_transactions_QuoteId");

            migrationBuilder.RenameColumn(
                name: "PolicyId",
                table: "notes",
                newName: "QuoteId");

            migrationBuilder.RenameIndex(
                name: "IX_notes_PolicyId",
                table: "notes",
                newName: "IX_notes_QuoteId");

            migrationBuilder.RenameColumn(
                name: "PolicyId",
                table: "attachments",
                newName: "QuoteId");

            migrationBuilder.RenameIndex(
                name: "IX_attachments_PolicyId",
                table: "attachments",
                newName: "IX_attachments_QuoteId");

            migrationBuilder.CreateTable(
                name: "agents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AgencyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LicenseNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    State = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "submissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    InsuredId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: true),
                    UnderwriterId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssistantUWId = table.Column<Guid>(type: "uuid", nullable: true),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpirationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_submissions_agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_submissions_insureds_InsuredId",
                        column: x => x.InsuredId,
                        principalTable: "insureds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_submissions_users_AssistantUWId",
                        column: x => x.AssistantUWId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_submissions_users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_submissions_users_UnderwriterId",
                        column: x => x.UnderwriterId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "quotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CarrierId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineOfBusiness = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PolicyNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BoundDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IssuedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CancelledDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpirationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PremiumAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxesAndFees = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalPremium = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CommissionRate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    CommissionAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CoverageDescription = table.Column<string>(type: "text", nullable: true),
                    Deductible = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Limit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_quotes_carriers_CarrierId",
                        column: x => x.CarrierId,
                        principalTable: "carriers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_quotes_submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_quotes_users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_quotes_CarrierId",
                table: "quotes",
                column: "CarrierId");

            migrationBuilder.CreateIndex(
                name: "IX_quotes_CreatedById",
                table: "quotes",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_quotes_PolicyNumber",
                table: "quotes",
                column: "PolicyNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_quotes_QuoteNumber",
                table: "quotes",
                column: "QuoteNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_quotes_SubmissionId",
                table: "quotes",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_submissions_AgentId",
                table: "submissions",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_submissions_AssistantUWId",
                table: "submissions",
                column: "AssistantUWId");

            migrationBuilder.CreateIndex(
                name: "IX_submissions_CreatedById",
                table: "submissions",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_submissions_InsuredId",
                table: "submissions",
                column: "InsuredId");

            migrationBuilder.CreateIndex(
                name: "IX_submissions_SubmissionNumber",
                table: "submissions",
                column: "SubmissionNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_submissions_UnderwriterId",
                table: "submissions",
                column: "UnderwriterId");

            migrationBuilder.AddForeignKey(
                name: "FK_attachments_quotes_QuoteId",
                table: "attachments",
                column: "QuoteId",
                principalTable: "quotes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_notes_quotes_QuoteId",
                table: "notes",
                column: "QuoteId",
                principalTable: "quotes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_policy_transactions_quotes_QuoteId",
                table: "policy_transactions",
                column: "QuoteId",
                principalTable: "quotes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_policy_transactions_quotes_RenewalQuoteId",
                table: "policy_transactions",
                column: "RenewalQuoteId",
                principalTable: "quotes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_attachments_quotes_QuoteId",
                table: "attachments");

            migrationBuilder.DropForeignKey(
                name: "FK_notes_quotes_QuoteId",
                table: "notes");

            migrationBuilder.DropForeignKey(
                name: "FK_policy_transactions_quotes_QuoteId",
                table: "policy_transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_policy_transactions_quotes_RenewalQuoteId",
                table: "policy_transactions");

            migrationBuilder.DropTable(
                name: "quotes");

            migrationBuilder.DropTable(
                name: "submissions");

            migrationBuilder.DropTable(
                name: "agents");

            migrationBuilder.RenameColumn(
                name: "RenewalQuoteId",
                table: "policy_transactions",
                newName: "RenewalPolicyId");

            migrationBuilder.RenameColumn(
                name: "QuoteId",
                table: "policy_transactions",
                newName: "PolicyId");

            migrationBuilder.RenameIndex(
                name: "IX_policy_transactions_RenewalQuoteId",
                table: "policy_transactions",
                newName: "IX_policy_transactions_RenewalPolicyId");

            migrationBuilder.RenameIndex(
                name: "IX_policy_transactions_QuoteId",
                table: "policy_transactions",
                newName: "IX_policy_transactions_PolicyId");

            migrationBuilder.RenameColumn(
                name: "QuoteId",
                table: "notes",
                newName: "PolicyId");

            migrationBuilder.RenameIndex(
                name: "IX_notes_QuoteId",
                table: "notes",
                newName: "IX_notes_PolicyId");

            migrationBuilder.RenameColumn(
                name: "QuoteId",
                table: "attachments",
                newName: "PolicyId");

            migrationBuilder.RenameIndex(
                name: "IX_attachments_QuoteId",
                table: "attachments",
                newName: "IX_attachments_PolicyId");

            migrationBuilder.CreateTable(
                name: "policies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedToId = table.Column<Guid>(type: "uuid", nullable: true),
                    CarrierId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    InsuredId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoundDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CancelledDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CarrierPolicyNumber = table.Column<string>(type: "text", nullable: true),
                    CommissionAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CommissionRate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    CoverageDescription = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Deductible = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpirationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IssuedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Limit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    LineOfBusiness = table.Column<int>(type: "integer", nullable: false),
                    PolicyNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PremiumAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    QuoteDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TaxesAndFees = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalPremium = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_policies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_policies_carriers_CarrierId",
                        column: x => x.CarrierId,
                        principalTable: "carriers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_policies_insureds_InsuredId",
                        column: x => x.InsuredId,
                        principalTable: "insureds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_policies_users_AssignedToId",
                        column: x => x.AssignedToId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_policies_users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_policies_AssignedToId",
                table: "policies",
                column: "AssignedToId");

            migrationBuilder.CreateIndex(
                name: "IX_policies_CarrierId",
                table: "policies",
                column: "CarrierId");

            migrationBuilder.CreateIndex(
                name: "IX_policies_CreatedById",
                table: "policies",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_policies_InsuredId",
                table: "policies",
                column: "InsuredId");

            migrationBuilder.CreateIndex(
                name: "IX_policies_PolicyNumber",
                table: "policies",
                column: "PolicyNumber",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_attachments_policies_PolicyId",
                table: "attachments",
                column: "PolicyId",
                principalTable: "policies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_notes_policies_PolicyId",
                table: "notes",
                column: "PolicyId",
                principalTable: "policies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_policy_transactions_policies_PolicyId",
                table: "policy_transactions",
                column: "PolicyId",
                principalTable: "policies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_policy_transactions_policies_RenewalPolicyId",
                table: "policy_transactions",
                column: "RenewalPolicyId",
                principalTable: "policies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
