using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Accounting_CoaSeed : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ----------------------------------------------------------------
            // Chart of Accounts seed — realistic test data per design doc §1.4
            // Swap ExternalLabel values when accounting hands us the real COA.
            // ----------------------------------------------------------------

            // Top-level accounts (no parent)
            migrationBuilder.Sql(@"
                INSERT INTO ledger_accounts (""TenantId"", ""InternalCode"", ""ExternalLabel"", ""AccountType"", ""ParentId"", ""IsActive"")
                VALUES
                (1, '1000', 'Cash — Operating Account',           'Asset',     NULL, true),
                (1, '1100', 'Cash — Trust Account (Fiduciary)',   'Asset',     NULL, true),
                (1, '1200', 'Accounts Receivable — Brokers',      'Asset',     NULL, true),
                (1, '1250', 'Unapplied Cash',                     'Asset',     NULL, true),
                (1, '1300', 'Commission Receivable from Trust',   'Asset',     NULL, true),
                (1, '2100', 'Accounts Payable — Carriers',        'Liability', NULL, true),
                (1, '2200', 'Surplus Lines Tax Payable',          'Liability', NULL, true),
                (1, '2300', 'Stamping Fee Payable',               'Liability', NULL, true),
                (1, '2400', 'AP — Tax Filing Service',            'Liability', NULL, true),
                (1, '2500', 'AP — Premium Finance Companies',     'Liability', NULL, true),
                (1, '4100', 'Commission Revenue',                 'Revenue',   NULL, true),
                (1, '4200', 'Policy Fee Revenue',                 'Revenue',   NULL, true),
                (1, '4300', 'Inspection Fee Revenue',             'Revenue',   NULL, true),
                (1, '5100', 'Broker Commission Expense',          'Expense',   NULL, true),
                (1, '5200', 'Bank Fees',                          'Expense',   NULL, true);
            ");

            // Carrier AP sub-accounts (children of 2100)
            migrationBuilder.Sql(@"
                INSERT INTO ledger_accounts (""TenantId"", ""InternalCode"", ""ExternalLabel"", ""AccountType"", ""ParentId"", ""IsActive"")
                SELECT 1, '210' || n::text, 'AP — Carrier #' || n::text, 'Liability',
                       (SELECT ""Id"" FROM ledger_accounts WHERE ""InternalCode"" = '2100' AND ""TenantId"" = 1),
                       true
                FROM generate_series(1, 5) n;
            ");

            // SL Tax Payable sub-accounts by state (children of 2200)
            migrationBuilder.Sql(@"
                INSERT INTO ledger_accounts (""TenantId"", ""InternalCode"", ""ExternalLabel"", ""AccountType"", ""ParentId"", ""IsActive"")
                VALUES
                (1, '2201', 'SL Tax Payable — AL', 'Liability', (SELECT ""Id"" FROM ledger_accounts WHERE ""InternalCode"" = '2200' AND ""TenantId"" = 1), true),
                (1, '2202', 'SL Tax Payable — AR', 'Liability', (SELECT ""Id"" FROM ledger_accounts WHERE ""InternalCode"" = '2200' AND ""TenantId"" = 1), true),
                (1, '2203', 'SL Tax Payable — AZ', 'Liability', (SELECT ""Id"" FROM ledger_accounts WHERE ""InternalCode"" = '2200' AND ""TenantId"" = 1), true),
                (1, '2204', 'SL Tax Payable — CA', 'Liability', (SELECT ""Id"" FROM ledger_accounts WHERE ""InternalCode"" = '2200' AND ""TenantId"" = 1), true),
                (1, '2205', 'SL Tax Payable — CO', 'Liability', (SELECT ""Id"" FROM ledger_accounts WHERE ""InternalCode"" = '2200' AND ""TenantId"" = 1), true),
                (1, '2206', 'SL Tax Payable — FL', 'Liability', (SELECT ""Id"" FROM ledger_accounts WHERE ""InternalCode"" = '2200' AND ""TenantId"" = 1), true),
                (1, '2207', 'SL Tax Payable — GA', 'Liability', (SELECT ""Id"" FROM ledger_accounts WHERE ""InternalCode"" = '2200' AND ""TenantId"" = 1), true),
                (1, '2208', 'SL Tax Payable — IL', 'Liability', (SELECT ""Id"" FROM ledger_accounts WHERE ""InternalCode"" = '2200' AND ""TenantId"" = 1), true),
                (1, '2209', 'SL Tax Payable — LA', 'Liability', (SELECT ""Id"" FROM ledger_accounts WHERE ""InternalCode"" = '2200' AND ""TenantId"" = 1), true),
                (1, '2210', 'SL Tax Payable — MS', 'Liability', (SELECT ""Id"" FROM ledger_accounts WHERE ""InternalCode"" = '2200' AND ""TenantId"" = 1), true),
                (1, '2211', 'SL Tax Payable — NC', 'Liability', (SELECT ""Id"" FROM ledger_accounts WHERE ""InternalCode"" = '2200' AND ""TenantId"" = 1), true),
                (1, '2212', 'SL Tax Payable — NM', 'Liability', (SELECT ""Id"" FROM ledger_accounts WHERE ""InternalCode"" = '2200' AND ""TenantId"" = 1), true),
                (1, '2213', 'SL Tax Payable — NY', 'Liability', (SELECT ""Id"" FROM ledger_accounts WHERE ""InternalCode"" = '2200' AND ""TenantId"" = 1), true),
                (1, '2214', 'SL Tax Payable — OK', 'Liability', (SELECT ""Id"" FROM ledger_accounts WHERE ""InternalCode"" = '2200' AND ""TenantId"" = 1), true),
                (1, '2215', 'SL Tax Payable — PA', 'Liability', (SELECT ""Id"" FROM ledger_accounts WHERE ""InternalCode"" = '2200' AND ""TenantId"" = 1), true),
                (1, '2216', 'SL Tax Payable — SC', 'Liability', (SELECT ""Id"" FROM ledger_accounts WHERE ""InternalCode"" = '2200' AND ""TenantId"" = 1), true),
                (1, '2217', 'SL Tax Payable — TN', 'Liability', (SELECT ""Id"" FROM ledger_accounts WHERE ""InternalCode"" = '2200' AND ""TenantId"" = 1), true),
                (1, '2218', 'SL Tax Payable — TX', 'Liability', (SELECT ""Id"" FROM ledger_accounts WHERE ""InternalCode"" = '2200' AND ""TenantId"" = 1), true),
                (1, '2219', 'SL Tax Payable — VA', 'Liability', (SELECT ""Id"" FROM ledger_accounts WHERE ""InternalCode"" = '2200' AND ""TenantId"" = 1), true),
                (1, '2220', 'SL Tax Payable — WV', 'Liability', (SELECT ""Id"" FROM ledger_accounts WHERE ""InternalCode"" = '2200' AND ""TenantId"" = 1), true);
            ");

            // Stamping Fee Payable sub-accounts (children of 2300)
            migrationBuilder.Sql(@"
                INSERT INTO ledger_accounts (""TenantId"", ""InternalCode"", ""ExternalLabel"", ""AccountType"", ""ParentId"", ""IsActive"")
                VALUES
                (1, '2301', 'Stamping Fee Payable — TX', 'Liability', (SELECT ""Id"" FROM ledger_accounts WHERE ""InternalCode"" = '2300' AND ""TenantId"" = 1), true),
                (1, '2302', 'Stamping Fee Payable — CA', 'Liability', (SELECT ""Id"" FROM ledger_accounts WHERE ""InternalCode"" = '2300' AND ""TenantId"" = 1), true),
                (1, '2303', 'Stamping Fee Payable — FL', 'Liability', (SELECT ""Id"" FROM ledger_accounts WHERE ""InternalCode"" = '2300' AND ""TenantId"" = 1), true),
                (1, '2304', 'Stamping Fee Payable — IL', 'Liability', (SELECT ""Id"" FROM ledger_accounts WHERE ""InternalCode"" = '2300' AND ""TenantId"" = 1), true),
                (1, '2305', 'Stamping Fee Payable — NY', 'Liability', (SELECT ""Id"" FROM ledger_accounts WHERE ""InternalCode"" = '2300' AND ""TenantId"" = 1), true);
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DELETE FROM ledger_accounts WHERE ""TenantId"" = 1;");
        }
    }
}
