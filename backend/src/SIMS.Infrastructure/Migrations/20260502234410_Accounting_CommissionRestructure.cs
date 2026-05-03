using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Accounting_CommissionRestructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommissionAmount",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "CommissionRate",
                table: "quotes");

            migrationBuilder.AddColumn<decimal>(
                name: "AgentCommissionRate",
                table: "quotes",
                type: "numeric(8,6)",
                precision: 8,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CarrierCommissionRate",
                table: "quotes",
                type: "numeric(8,6)",
                precision: 8,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CommissionOverrideAgentRate",
                table: "quotes",
                type: "numeric(8,6)",
                precision: 8,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CommissionOverrideAt",
                table: "quotes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CommissionOverrideBy",
                table: "quotes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CommissionOverrideCarrierRate",
                table: "quotes",
                type: "numeric(8,6)",
                precision: 8,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CommissionOverrideSMMRate",
                table: "quotes",
                type: "numeric(8,6)",
                precision: 8,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SMMRetentionRate",
                table: "quotes",
                type: "numeric(8,6)",
                precision: 8,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SMMRetentionRate",
                table: "carrier_commissions",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AgentCommissionRate",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "CarrierCommissionRate",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "CommissionOverrideAgentRate",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "CommissionOverrideAt",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "CommissionOverrideBy",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "CommissionOverrideCarrierRate",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "CommissionOverrideSMMRate",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "SMMRetentionRate",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "SMMRetentionRate",
                table: "carrier_commissions");

            migrationBuilder.AddColumn<decimal>(
                name: "CommissionAmount",
                table: "quotes",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CommissionRate",
                table: "quotes",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: false,
                defaultValue: 0m);
        }
    }
}
