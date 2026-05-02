using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Accounting_CashDistribution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cash_distribution_batches",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    BatchNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TotalInstructions = table.Column<int>(type: "integer", nullable: false),
                    TotalWires = table.Column<int>(type: "integer", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(19,4)", nullable: false),
                    PdfBlobPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ExecutedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExecutedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    BankReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_distribution_batches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cash_movement_instructions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    ReceiptId = table.Column<long>(type: "bigint", nullable: false),
                    CashApplicationId = table.Column<long>(type: "bigint", nullable: false),
                    InvoiceLineId = table.Column<long>(type: "bigint", nullable: false),
                    PayeeId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(19,4)", nullable: false),
                    SourceGlAccountId = table.Column<int>(type: "integer", nullable: false),
                    DistributionGlAccountId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BatchId = table.Column<long>(type: "bigint", nullable: true),
                    LedgerTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_movement_instructions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cash_movement_instructions_cash_applications_CashApplicatio~",
                        column: x => x.CashApplicationId,
                        principalTable: "cash_applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cash_movement_instructions_cash_distribution_batches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "cash_distribution_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cash_movement_instructions_invoice_lines_InvoiceLineId",
                        column: x => x.InvoiceLineId,
                        principalTable: "invoice_lines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cash_movement_instructions_payees_PayeeId",
                        column: x => x.PayeeId,
                        principalTable: "payees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cash_movement_instructions_receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalTable: "receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cash_distribution_batches_BatchNumber",
                table: "cash_distribution_batches",
                column: "BatchNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cash_distribution_batches_Status",
                table: "cash_distribution_batches",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_cash_movement_instructions_BatchId",
                table: "cash_movement_instructions",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_cash_movement_instructions_CashApplicationId",
                table: "cash_movement_instructions",
                column: "CashApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_cash_movement_instructions_InvoiceLineId",
                table: "cash_movement_instructions",
                column: "InvoiceLineId");

            migrationBuilder.CreateIndex(
                name: "IX_cash_movement_instructions_PayeeId",
                table: "cash_movement_instructions",
                column: "PayeeId");

            migrationBuilder.CreateIndex(
                name: "IX_cash_movement_instructions_ReceiptId",
                table: "cash_movement_instructions",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_cash_movement_instructions_Status",
                table: "cash_movement_instructions",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cash_movement_instructions");

            migrationBuilder.DropTable(
                name: "cash_distribution_batches");
        }
    }
}
