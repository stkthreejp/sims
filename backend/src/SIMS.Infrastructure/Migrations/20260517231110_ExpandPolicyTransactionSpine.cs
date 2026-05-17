using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExpandPolicyTransactionSpine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "policy_transactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedById",
                table: "policy_transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingModeSnapshot",
                table: "policy_transactions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CommissionDelta",
                table: "policy_transactions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "policy_transactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CompletedById",
                table: "policy_transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ExpirationDate",
                table: "policy_transactions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalReference",
                table: "policy_transactions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IssuedAt",
                table: "policy_transactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IssuedById",
                table: "policy_transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PremiumAfter",
                table: "policy_transactions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PremiumBefore",
                table: "policy_transactions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PriorPolicyVersionId",
                table: "policy_transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReasonCode",
                table: "policy_transactions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReasonText",
                table: "policy_transactions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RenewalQuoteId",
                table: "policy_transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RequestedAt",
                table: "policy_transactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RequestedById",
                table: "policy_transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResultingPolicyVersionId",
                table: "policy_transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReversesPolicyTransactionId",
                table: "policy_transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "policy_transactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedById",
                table: "policy_transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceQuoteId",
                table: "policy_transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxesAndFeesDelta",
                table: "policy_transactions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VoidsPolicyTransactionId",
                table: "policy_transactions",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "policy_transactions");

            migrationBuilder.DropColumn(
                name: "ApprovedById",
                table: "policy_transactions");

            migrationBuilder.DropColumn(
                name: "BillingModeSnapshot",
                table: "policy_transactions");

            migrationBuilder.DropColumn(
                name: "CommissionDelta",
                table: "policy_transactions");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "policy_transactions");

            migrationBuilder.DropColumn(
                name: "CompletedById",
                table: "policy_transactions");

            migrationBuilder.DropColumn(
                name: "ExpirationDate",
                table: "policy_transactions");

            migrationBuilder.DropColumn(
                name: "ExternalReference",
                table: "policy_transactions");

            migrationBuilder.DropColumn(
                name: "IssuedAt",
                table: "policy_transactions");

            migrationBuilder.DropColumn(
                name: "IssuedById",
                table: "policy_transactions");

            migrationBuilder.DropColumn(
                name: "PremiumAfter",
                table: "policy_transactions");

            migrationBuilder.DropColumn(
                name: "PremiumBefore",
                table: "policy_transactions");

            migrationBuilder.DropColumn(
                name: "PriorPolicyVersionId",
                table: "policy_transactions");

            migrationBuilder.DropColumn(
                name: "ReasonCode",
                table: "policy_transactions");

            migrationBuilder.DropColumn(
                name: "ReasonText",
                table: "policy_transactions");

            migrationBuilder.DropColumn(
                name: "RenewalQuoteId",
                table: "policy_transactions");

            migrationBuilder.DropColumn(
                name: "RequestedAt",
                table: "policy_transactions");

            migrationBuilder.DropColumn(
                name: "RequestedById",
                table: "policy_transactions");

            migrationBuilder.DropColumn(
                name: "ResultingPolicyVersionId",
                table: "policy_transactions");

            migrationBuilder.DropColumn(
                name: "ReversesPolicyTransactionId",
                table: "policy_transactions");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "policy_transactions");

            migrationBuilder.DropColumn(
                name: "ReviewedById",
                table: "policy_transactions");

            migrationBuilder.DropColumn(
                name: "SourceQuoteId",
                table: "policy_transactions");

            migrationBuilder.DropColumn(
                name: "TaxesAndFeesDelta",
                table: "policy_transactions");

            migrationBuilder.DropColumn(
                name: "VoidsPolicyTransactionId",
                table: "policy_transactions");
        }
    }
}
