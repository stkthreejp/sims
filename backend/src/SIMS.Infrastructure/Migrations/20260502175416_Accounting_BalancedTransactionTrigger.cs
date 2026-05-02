using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Accounting_BalancedTransactionTrigger : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Partial index: fast scan for unrolled ledger rows
            migrationBuilder.Sql(@"
                CREATE INDEX ix_ledger_unrolled
                ON ledger_transactions(""EffectiveDate"")
                WHERE ""RolledUpIn"" IS NULL;
            ");

            // Enforce that every transaction_id group is balanced (sum debit = sum credit)
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION enforce_balanced_transaction()
                RETURNS trigger AS $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM ledger_transactions
                        WHERE ""TransactionId"" = NEW.""TransactionId""
                        GROUP BY ""TransactionId""
                        HAVING SUM(""Debit"") <> SUM(""Credit"")
                    ) THEN
                        RAISE EXCEPTION 'Unbalanced transaction %', NEW.""TransactionId"";
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
            ");

            migrationBuilder.Sql(@"
                CREATE TRIGGER trg_enforce_balanced_transaction
                AFTER INSERT ON ledger_transactions
                FOR EACH ROW EXECUTE FUNCTION enforce_balanced_transaction();
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_enforce_balanced_transaction ON ledger_transactions;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS enforce_balanced_transaction();");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_ledger_unrolled;");
        }
    }
}
