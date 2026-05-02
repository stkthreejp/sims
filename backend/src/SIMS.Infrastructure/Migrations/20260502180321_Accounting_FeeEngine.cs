using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Accounting_FeeEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fee_definitions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FeeCategory = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsTaxable = table.Column<bool>(type: "boolean", nullable: false),
                    CalculationOrder = table.Column<int>(type: "integer", nullable: false),
                    LedgerAccountId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fee_definitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fee_definitions_ledger_accounts_LedgerAccountId",
                        column: x => x.LedgerAccountId,
                        principalTable: "ledger_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fee_rule_versions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FeeDefinitionId = table.Column<long>(type: "bigint", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: true),
                    ProducerId = table.Column<int>(type: "integer", nullable: true),
                    LineOfBusiness = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StateCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LicenseType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DisabledDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CalcType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FlatAmount = table.Column<decimal>(type: "numeric(19,4)", nullable: true),
                    PercentRate = table.Column<decimal>(type: "numeric(9,6)", nullable: true),
                    PercentOfNet = table.Column<bool>(type: "boolean", nullable: false),
                    MinimumAmount = table.Column<decimal>(type: "numeric(19,4)", nullable: true),
                    MaxPercent = table.Column<decimal>(type: "numeric(9,6)", nullable: true),
                    MaxAmount = table.Column<decimal>(type: "numeric(19,4)", nullable: true),
                    Commissionable = table.Column<bool>(type: "boolean", nullable: false),
                    OfficeId = table.Column<int>(type: "integer", nullable: true),
                    InstallmentBehavior = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SplitByParticipation = table.Column<bool>(type: "boolean", nullable: false),
                    FullyEarned = table.Column<bool>(type: "boolean", nullable: false),
                    FullyEarnedDays = table.Column<int>(type: "integer", nullable: true),
                    ExcludeTerrorism = table.Column<bool>(type: "boolean", nullable: false),
                    MultiplyByLocations = table.Column<bool>(type: "boolean", nullable: false),
                    MultiplyByVehicles = table.Column<bool>(type: "boolean", nullable: false),
                    SendToAccounting = table.Column<bool>(type: "boolean", nullable: false),
                    ApplyAutomatically = table.Column<bool>(type: "boolean", nullable: false),
                    PremiumMinThreshold = table.Column<decimal>(type: "numeric(19,4)", nullable: true),
                    PremiumMaxThreshold = table.Column<decimal>(type: "numeric(19,4)", nullable: true),
                    PremiumThresholdBasis = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    RoundingMode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ExcludeWhenNotFiling = table.Column<bool>(type: "boolean", nullable: false),
                    ExcludeOnEndorsements = table.Column<bool>(type: "boolean", nullable: false),
                    PayableRouting = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PayablePayeeId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastEditedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    LastEditedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fee_rule_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fee_rule_versions_fee_definitions_FeeDefinitionId",
                        column: x => x.FeeDefinitionId,
                        principalTable: "fee_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_fee_rule_versions_payees_PayablePayeeId",
                        column: x => x.PayablePayeeId,
                        principalTable: "payees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fee_state_taxability",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FeeDefinitionId = table.Column<long>(type: "bigint", nullable: false),
                    StateCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    IsTaxable = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fee_state_taxability", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fee_state_taxability_fee_definitions_FeeDefinitionId",
                        column: x => x.FeeDefinitionId,
                        principalTable: "fee_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fee_audit_log",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FeeRuleVersionId = table.Column<long>(type: "bigint", nullable: false),
                    EditedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    EditedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChangeType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FieldChanges = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fee_audit_log", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fee_audit_log_fee_rule_versions_FeeRuleVersionId",
                        column: x => x.FeeRuleVersionId,
                        principalTable: "fee_rule_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fee_premium_brackets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FeeRuleVersionId = table.Column<long>(type: "bigint", nullable: false),
                    TierFrom = table.Column<decimal>(type: "numeric(19,4)", nullable: false),
                    TierTo = table.Column<decimal>(type: "numeric(19,4)", nullable: true),
                    PercentRate = table.Column<decimal>(type: "numeric(9,6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fee_premium_brackets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fee_premium_brackets_fee_rule_versions_FeeRuleVersionId",
                        column: x => x.FeeRuleVersionId,
                        principalTable: "fee_rule_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fee_audit_log_FeeRuleVersionId",
                table: "fee_audit_log",
                column: "FeeRuleVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_fee_definitions_LedgerAccountId",
                table: "fee_definitions",
                column: "LedgerAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_fee_definitions_TenantId_Code",
                table: "fee_definitions",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fee_premium_brackets_FeeRuleVersionId_TierFrom",
                table: "fee_premium_brackets",
                columns: new[] { "FeeRuleVersionId", "TierFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fee_rule_lookup",
                table: "fee_rule_versions",
                columns: new[] { "FeeDefinitionId", "StateCode", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_fee_rule_versions_PayablePayeeId",
                table: "fee_rule_versions",
                column: "PayablePayeeId");

            migrationBuilder.CreateIndex(
                name: "IX_fee_state_taxability_FeeDefinitionId_StateCode",
                table: "fee_state_taxability",
                columns: new[] { "FeeDefinitionId", "StateCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fee_audit_log");

            migrationBuilder.DropTable(
                name: "fee_premium_brackets");

            migrationBuilder.DropTable(
                name: "fee_state_taxability");

            migrationBuilder.DropTable(
                name: "fee_rule_versions");

            migrationBuilder.DropTable(
                name: "fee_definitions");
        }
    }
}
