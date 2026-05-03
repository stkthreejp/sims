using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Accounting_PayeeStatements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payee_statements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    PayeeName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StatementDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BlobPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ApLedgerAccountId = table.Column<int>(type: "integer", nullable: false),
                    StatementTotal = table.Column<decimal>(type: "numeric(19,4)", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payee_statements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payee_statements_ledger_accounts_ApLedgerAccountId",
                        column: x => x.ApLedgerAccountId,
                        principalTable: "ledger_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payee_statement_lines",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PayeeStatementId = table.Column<long>(type: "bigint", nullable: false),
                    PolicyNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StateCode = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(19,4)", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MatchStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MatchedInvoiceLineId = table.Column<long>(type: "bigint", nullable: true),
                    ReconciliationTransactionId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payee_statement_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payee_statement_lines_invoice_lines_MatchedInvoiceLineId",
                        column: x => x.MatchedInvoiceLineId,
                        principalTable: "invoice_lines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_payee_statement_lines_payee_statements_PayeeStatementId",
                        column: x => x.PayeeStatementId,
                        principalTable: "payee_statements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_payee_statement_lines_MatchedInvoiceLineId",
                table: "payee_statement_lines",
                column: "MatchedInvoiceLineId");

            migrationBuilder.CreateIndex(
                name: "IX_payee_statement_lines_MatchStatus",
                table: "payee_statement_lines",
                column: "MatchStatus");

            migrationBuilder.CreateIndex(
                name: "IX_payee_statement_lines_PayeeStatementId",
                table: "payee_statement_lines",
                column: "PayeeStatementId");

            migrationBuilder.CreateIndex(
                name: "IX_payee_statements_ApLedgerAccountId",
                table: "payee_statements",
                column: "ApLedgerAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_payee_statements_StatementDate",
                table: "payee_statements",
                column: "StatementDate");

            migrationBuilder.CreateIndex(
                name: "IX_payee_statements_Status",
                table: "payee_statements",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payee_statement_lines");

            migrationBuilder.DropTable(
                name: "payee_statements");
        }
    }
}
