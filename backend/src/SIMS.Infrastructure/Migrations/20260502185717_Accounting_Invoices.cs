using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Accounting_Invoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "invoices",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PolicyTransactionId = table.Column<long>(type: "bigint", nullable: true),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    InvoiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    GrossPremium = table.Column<decimal>(type: "numeric(19,4)", nullable: false),
                    TotalFees = table.Column<decimal>(type: "numeric(19,4)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(19,4)", nullable: false),
                    LedgerTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "invoice_lines",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: false),
                    FeeRuleVersionId = table.Column<long>(type: "bigint", nullable: false),
                    FeeCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FeeDisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FeeCategory = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(19,4)", nullable: false),
                    IsTaxable = table.Column<bool>(type: "boolean", nullable: false),
                    LedgerAccountId = table.Column<int>(type: "integer", nullable: false),
                    PayableRouting = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PayablePayeeId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_invoice_lines_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_invoice_lines_ledger_accounts_LedgerAccountId",
                        column: x => x.LedgerAccountId,
                        principalTable: "ledger_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_lines_InvoiceId",
                table: "invoice_lines",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_lines_LedgerAccountId",
                table: "invoice_lines",
                column: "LedgerAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_InvoiceNumber",
                table: "invoices",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoices_LedgerTransactionId",
                table: "invoices",
                column: "LedgerTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_TenantId_InvoiceDate",
                table: "invoices",
                columns: new[] { "TenantId", "InvoiceDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "invoice_lines");

            migrationBuilder.DropTable(
                name: "invoices");
        }
    }
}
