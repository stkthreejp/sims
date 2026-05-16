using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExpandFeeRuleControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AppliesToFlatCancellations",
                table: "fee_rule_versions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ApplyOnlyOnce",
                table: "fee_rule_versions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ApplyToChildLines",
                table: "fee_rule_versions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ApplyWhenPackagePolicyOnly",
                table: "fee_rule_versions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "DoNotApplyWhenPackagePolicyOnly",
                table: "fee_rule_versions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ExcludeOnMultiCarrierPolicy",
                table: "fee_rule_versions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ExcludeOnOriginalBinder",
                table: "fee_rule_versions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ExcludeOnRenewal",
                table: "fee_rule_versions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ExcludedPolicyTransactionTypes",
                table: "fee_rule_versions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MandatoryCharge",
                table: "fee_rule_versions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "MasterPayeeWhenHomeState",
                table: "fee_rule_versions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "OnlyAppliesToIssuanceState",
                table: "fee_rule_versions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PayHomeState",
                table: "fee_rule_versions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "StateCountMax",
                table: "fee_rule_versions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StateCountMin",
                table: "fee_rule_versions",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppliesToFlatCancellations",
                table: "fee_rule_versions");

            migrationBuilder.DropColumn(
                name: "ApplyOnlyOnce",
                table: "fee_rule_versions");

            migrationBuilder.DropColumn(
                name: "ApplyToChildLines",
                table: "fee_rule_versions");

            migrationBuilder.DropColumn(
                name: "ApplyWhenPackagePolicyOnly",
                table: "fee_rule_versions");

            migrationBuilder.DropColumn(
                name: "DoNotApplyWhenPackagePolicyOnly",
                table: "fee_rule_versions");

            migrationBuilder.DropColumn(
                name: "ExcludeOnMultiCarrierPolicy",
                table: "fee_rule_versions");

            migrationBuilder.DropColumn(
                name: "ExcludeOnOriginalBinder",
                table: "fee_rule_versions");

            migrationBuilder.DropColumn(
                name: "ExcludeOnRenewal",
                table: "fee_rule_versions");

            migrationBuilder.DropColumn(
                name: "ExcludedPolicyTransactionTypes",
                table: "fee_rule_versions");

            migrationBuilder.DropColumn(
                name: "MandatoryCharge",
                table: "fee_rule_versions");

            migrationBuilder.DropColumn(
                name: "MasterPayeeWhenHomeState",
                table: "fee_rule_versions");

            migrationBuilder.DropColumn(
                name: "OnlyAppliesToIssuanceState",
                table: "fee_rule_versions");

            migrationBuilder.DropColumn(
                name: "PayHomeState",
                table: "fee_rule_versions");

            migrationBuilder.DropColumn(
                name: "StateCountMax",
                table: "fee_rule_versions");

            migrationBuilder.DropColumn(
                name: "StateCountMin",
                table: "fee_rule_versions");
        }
    }
}
