using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Accounting_Disbursements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"ALTER TABLE invoices ALTER COLUMN ""PolicyTransactionId"" TYPE uuid USING NULL::uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "PolicyTransactionId",
                table: "invoices",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "disbursements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    DisbursementNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PayeeName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CarrierId = table.Column<Guid>(type: "uuid", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "numeric(19,4)", nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    LedgerTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_disbursements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "payables",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: false),
                    CarrierId = table.Column<Guid>(type: "uuid", nullable: true),
                    PayeeName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    GlAccountId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(19,4)", nullable: false),
                    PaidAmount = table.Column<decimal>(type: "numeric(19,4)", nullable: false),
                    InvoiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payables_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payables_ledger_accounts_GlAccountId",
                        column: x => x.GlAccountId,
                        principalTable: "ledger_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "disbursement_lines",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DisbursementId = table.Column<long>(type: "bigint", nullable: false),
                    PayableId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(19,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_disbursement_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_disbursement_lines_disbursements_DisbursementId",
                        column: x => x.DisbursementId,
                        principalTable: "disbursements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_disbursement_lines_payables_PayableId",
                        column: x => x.PayableId,
                        principalTable: "payables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_disbursement_lines_DisbursementId",
                table: "disbursement_lines",
                column: "DisbursementId");

            migrationBuilder.CreateIndex(
                name: "IX_disbursement_lines_PayableId",
                table: "disbursement_lines",
                column: "PayableId");

            migrationBuilder.CreateIndex(
                name: "IX_disbursements_CarrierId",
                table: "disbursements",
                column: "CarrierId");

            migrationBuilder.CreateIndex(
                name: "IX_disbursements_DisbursementNumber",
                table: "disbursements",
                column: "DisbursementNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_disbursements_Status",
                table: "disbursements",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_payables_CarrierId",
                table: "payables",
                column: "CarrierId");

            migrationBuilder.CreateIndex(
                name: "IX_payables_DueDate",
                table: "payables",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_payables_GlAccountId",
                table: "payables",
                column: "GlAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_payables_InvoiceId",
                table: "payables",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_payables_Status",
                table: "payables",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "disbursement_lines");

            migrationBuilder.DropTable(
                name: "disbursements");

            migrationBuilder.DropTable(
                name: "payables");

            migrationBuilder.AlterColumn<long>(
                name: "PolicyTransactionId",
                table: "invoices",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
