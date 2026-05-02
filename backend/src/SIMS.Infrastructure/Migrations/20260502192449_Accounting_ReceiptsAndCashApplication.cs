using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Accounting_ReceiptsAndCashApplication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ClearedAmount",
                table: "invoices",
                type: "numeric(19,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "receipts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    ReceiptNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ReceivedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(19,4)", nullable: false),
                    PayerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RemittanceBlobPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LedgerTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AppliedAmount = table.Column<decimal>(type: "numeric(19,4)", nullable: false, defaultValue: 0m),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receipts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cash_applications",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    ReceiptId = table.Column<long>(type: "bigint", nullable: false),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: false),
                    GrossApplied = table.Column<decimal>(type: "numeric(19,4)", nullable: false),
                    CommissionAmount = table.Column<decimal>(type: "numeric(19,4)", nullable: false),
                    NetApplied = table.Column<decimal>(type: "numeric(19,4)", nullable: false),
                    LedgerTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_applications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cash_applications_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cash_applications_receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalTable: "receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cash_applications_InvoiceId",
                table: "cash_applications",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_cash_applications_LedgerTransactionId",
                table: "cash_applications",
                column: "LedgerTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_cash_applications_ReceiptId_InvoiceId",
                table: "cash_applications",
                columns: new[] { "ReceiptId", "InvoiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_receipts_ReceiptNumber",
                table: "receipts",
                column: "ReceiptNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_receipts_TenantId_ReceivedDate",
                table: "receipts",
                columns: new[] { "TenantId", "ReceivedDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cash_applications");

            migrationBuilder.DropTable(
                name: "receipts");

            migrationBuilder.DropColumn(
                name: "ClearedAmount",
                table: "invoices");
        }
    }
}
