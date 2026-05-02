using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Accounting_DeferBalancedTrigger : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Replace the row-level trigger with a deferred constraint trigger so that
            // multi-row transactions (debit + multiple credits) can be inserted in one
            // SaveChangesAsync call and the balance check fires at COMMIT time, not
            // after each individual row.
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_enforce_balanced_transaction ON ledger_transactions;");
            migrationBuilder.Sql(@"
                CREATE CONSTRAINT TRIGGER trg_enforce_balanced_transaction
                AFTER INSERT ON ledger_transactions
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION enforce_balanced_transaction();
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_enforce_balanced_transaction ON ledger_transactions;");
            migrationBuilder.Sql(@"
                CREATE TRIGGER trg_enforce_balanced_transaction
                AFTER INSERT ON ledger_transactions
                FOR EACH ROW EXECUTE FUNCTION enforce_balanced_transaction();
            ");
        }
    }
}
