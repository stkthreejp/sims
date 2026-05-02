using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Accounting_CarrierCommissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CommissionAmount",
                table: "invoices",
                type: "numeric(19,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<long>(
                name: "FeeRuleVersionId",
                table: "invoice_lines",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.CreateTable(
                name: "carrier_commissions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CarrierId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineOfBusiness = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CommissionRate = table.Column<decimal>(type: "numeric(8,6)", nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DisabledDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_carrier_commissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_carrier_commissions_carriers_CarrierId",
                        column: x => x.CarrierId,
                        principalTable: "carriers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_carrier_commissions_CarrierId_DisabledDate",
                table: "carrier_commissions",
                columns: new[] { "CarrierId", "DisabledDate" });

            migrationBuilder.CreateIndex(
                name: "IX_carrier_commissions_CarrierId_LineOfBusiness_EffectiveDate",
                table: "carrier_commissions",
                columns: new[] { "CarrierId", "LineOfBusiness", "EffectiveDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "carrier_commissions");

            migrationBuilder.DropColumn(
                name: "CommissionAmount",
                table: "invoices");

            migrationBuilder.AlterColumn<long>(
                name: "FeeRuleVersionId",
                table: "invoice_lines",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);
        }
    }
}
