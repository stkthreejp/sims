using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PolicyEntitySeparation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Create policies table
            migrationBuilder.CreateTable(
                name: "policies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bound_quote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    carrier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_of_business = table.Column<int>(type: "integer", nullable: false),
                    effective_date = table.Column<DateOnly>(type: "date", nullable: false),
                    expiration_date = table.Column<DateOnly>(type: "date", nullable: false),
                    premium_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    taxes_and_fees = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total_premium = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    bound_date = table.Column<DateOnly>(type: "date", nullable: false),
                    issued_date = table.Column<DateOnly>(type: "date", nullable: true),
                    cancelled_date = table.Column<DateOnly>(type: "date", nullable: true),
                    non_renewed_date = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_policies", x => x.id);
                    table.ForeignKey("fk_policies_submissions", x => x.submission_id, "submissions", "id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("fk_policies_bound_quote", x => x.bound_quote_id, "quotes", "id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("fk_policies_carriers", x => x.carrier_id, "carriers", "id", onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_policies_policy_number",
                table: "policies",
                column: "policy_number",
                unique: true);

            // 2. Seed policies from existing bound quotes (status = 4)
            migrationBuilder.Sql(@"
                INSERT INTO policies (
                    id, policy_number, submission_id, bound_quote_id, carrier_id,
                    line_of_business, effective_date, expiration_date,
                    premium_amount, taxes_and_fees, total_premium,
                    status, bound_date, issued_date, cancelled_date,
                    created_at, updated_at, is_deleted
                )
                SELECT
                    gen_random_uuid(),
                    q.policy_number,
                    q.submission_id,
                    q.id,
                    q.carrier_id,
                    q.line_of_business,
                    q.effective_date,
                    q.expiration_date,
                    q.premium_amount,
                    q.taxes_and_fees,
                    q.total_premium,
                    1,
                    q.bound_date,
                    q.issued_date,
                    q.cancelled_date,
                    q.created_at,
                    q.updated_at,
                    q.is_deleted
                FROM quotes q
                WHERE q.status = 4
                  AND q.policy_number IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM policies p WHERE p.bound_quote_id = q.id)
            ");

            // 3. Add policy_id (nullable initially) and status to policy_transactions
            migrationBuilder.AddColumn<Guid>(
                name: "policy_id",
                table: "policy_transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "status",
                table: "policy_transactions",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<Guid>(
                name: "prior_policy_id",
                table: "policy_transactions",
                type: "uuid",
                nullable: true);

            // 4. Populate policy_id from existing quote-linked transactions
            migrationBuilder.Sql(@"
                UPDATE policy_transactions pt
                SET policy_id = p.id
                FROM policies p
                WHERE p.bound_quote_id = pt.quote_id
            ");

            // 5. Delete orphaned transactions (no matching bound quote)
            migrationBuilder.Sql(@"
                DELETE FROM policy_transactions WHERE policy_id IS NULL
            ");

            // 6. Make policy_id non-nullable
            migrationBuilder.AlterColumn<Guid>(
                name: "policy_id",
                table: "policy_transactions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldNullable: true);

            // 7. Add FK and drop old quote FK
            migrationBuilder.CreateIndex(
                name: "ix_policy_transactions_policy_id",
                table: "policy_transactions",
                column: "policy_id");

            migrationBuilder.AddForeignKey(
                name: "fk_policy_transactions_policies",
                table: "policy_transactions",
                column: "policy_id",
                principalTable: "policies",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_policy_transactions_prior_policy",
                table: "policy_transactions",
                column: "prior_policy_id",
                principalTable: "policies",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // Drop old quote FK and column
            migrationBuilder.DropForeignKey(
                name: "fk_policy_transactions_quotes_quote_id",
                table: "policy_transactions");

            migrationBuilder.DropIndex(
                name: "ix_policy_transactions_quote_id",
                table: "policy_transactions");

            migrationBuilder.DropColumn(
                name: "quote_id",
                table: "policy_transactions");

            migrationBuilder.DropColumn(
                name: "renewal_quote_id",
                table: "policy_transactions");

            // 8. New columns on quotes
            migrationBuilder.AddColumn<int>(
                name: "company_id",
                table: "quotes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "producer_id",
                table: "quotes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_filing_state",
                table: "quotes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Drop old unconditional unique index on policy_number; add filtered one
            migrationBuilder.DropIndex(
                name: "ix_quotes_policy_number",
                table: "quotes");

            migrationBuilder.CreateIndex(
                name: "ix_quotes_policy_number",
                table: "quotes",
                column: "policy_number",
                unique: true,
                filter: "policy_number IS NOT NULL");

            // 9. New column on submissions
            migrationBuilder.AddColumn<int>(
                name: "producer_id",
                table: "submissions",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "policies");

            migrationBuilder.DropColumn(name: "policy_id", table: "policy_transactions");
            migrationBuilder.DropColumn(name: "status", table: "policy_transactions");
            migrationBuilder.DropColumn(name: "prior_policy_id", table: "policy_transactions");

            migrationBuilder.AddColumn<Guid>(name: "quote_id", table: "policy_transactions", type: "uuid", nullable: false, defaultValue: new Guid());
            migrationBuilder.AddColumn<Guid>(name: "renewal_quote_id", table: "policy_transactions", type: "uuid", nullable: true);

            migrationBuilder.DropColumn(name: "company_id", table: "quotes");
            migrationBuilder.DropColumn(name: "producer_id", table: "quotes");
            migrationBuilder.DropColumn(name: "is_filing_state", table: "quotes");
            migrationBuilder.DropColumn(name: "producer_id", table: "submissions");

            migrationBuilder.CreateIndex(name: "ix_quotes_policy_number", table: "quotes", column: "policy_number", unique: true);
        }
    }
}
