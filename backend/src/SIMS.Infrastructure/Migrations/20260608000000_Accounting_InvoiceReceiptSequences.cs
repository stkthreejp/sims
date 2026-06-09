using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    public partial class Accounting_InvoiceReceiptSequences : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE SEQUENCE IF NOT EXISTS invoice_number_seq;");
            migrationBuilder.Sql(@"
                DO $$
                DECLARE max_seq INTEGER;
                BEGIN
                    SELECT COALESCE(MAX(CAST(SPLIT_PART(invoice_number, '-', 3) AS INTEGER)), 0)
                    INTO max_seq FROM invoices WHERE invoice_number LIKE 'INV-%-%';
                    IF max_seq > 0 THEN
                        PERFORM setval('invoice_number_seq', max_seq);
                    END IF;
                END;
                $$;
            ");

            migrationBuilder.Sql("CREATE SEQUENCE IF NOT EXISTS receipt_number_seq;");
            migrationBuilder.Sql(@"
                DO $$
                DECLARE max_seq INTEGER;
                BEGIN
                    SELECT COALESCE(MAX(CAST(SPLIT_PART(receipt_number, '-', 3) AS INTEGER)), 0)
                    INTO max_seq FROM receipts WHERE receipt_number LIKE 'RCT-%-%';
                    IF max_seq > 0 THEN
                        PERFORM setval('receipt_number_seq', max_seq);
                    END IF;
                END;
                $$;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP SEQUENCE IF EXISTS invoice_number_seq;");
            migrationBuilder.Sql("DROP SEQUENCE IF EXISTS receipt_number_seq;");
        }
    }
}
