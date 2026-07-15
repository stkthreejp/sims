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
            // Fully idempotent — handles fresh DB and partially-applied states
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    -- 1. Create policies table if it doesn't exist
                    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'policies') THEN
                        CREATE TABLE policies (
                            id uuid NOT NULL,
                            policy_number character varying(50) NOT NULL,
                            submission_id uuid NOT NULL,
                            bound_quote_id uuid NOT NULL,
                            carrier_id uuid NOT NULL,
                            line_of_business integer NOT NULL,
                            effective_date date NOT NULL,
                            expiration_date date NOT NULL,
                            premium_amount numeric(18,2) NOT NULL,
                            taxes_and_fees numeric(18,2) NOT NULL,
                            total_premium numeric(18,2) NOT NULL,
                            status integer NOT NULL DEFAULT 1,
                            bound_date date NOT NULL,
                            issued_date date,
                            cancelled_date date,
                            non_renewed_date date,
                            created_at timestamp with time zone NOT NULL,
                            updated_at timestamp with time zone NOT NULL,
                            is_deleted boolean NOT NULL DEFAULT false,
                            deleted_at timestamp with time zone,
                            CONSTRAINT pk_policies PRIMARY KEY (id),
                            CONSTRAINT fk_policies_submissions FOREIGN KEY (submission_id) REFERENCES submissions(id) ON DELETE RESTRICT,
                            CONSTRAINT fk_policies_bound_quote FOREIGN KEY (bound_quote_id) REFERENCES quotes(id) ON DELETE RESTRICT,
                            CONSTRAINT fk_policies_carriers FOREIGN KEY (carrier_id) REFERENCES carriers(id) ON DELETE RESTRICT
                        );
                        CREATE UNIQUE INDEX ix_policies_policy_number ON policies(policy_number);
                    END IF;

                    -- 2. Seed policies from existing bound quotes (idempotent)
                    INSERT INTO policies (
                        id, policy_number, submission_id, bound_quote_id, carrier_id,
                        line_of_business, effective_date, expiration_date,
                        premium_amount, taxes_and_fees, total_premium,
                        status, bound_date, issued_date, cancelled_date,
                        created_at, updated_at, is_deleted
                    )
                    SELECT
                        gen_random_uuid(),
                        q.""PolicyNumber"",
                        q.""SubmissionId"",
                        q.id,
                        q.""CarrierId"",
                        q.""LineOfBusiness"",
                        q.""EffectiveDate"",
                        q.""ExpirationDate"",
                        q.""PremiumAmount"",
                        q.""TaxesAndFees"",
                        q.""TotalPremium"",
                        1,
                        q.""BoundDate"",
                        q.""IssuedDate"",
                        q.""CancelledDate"",
                        q.created_at,
                        q.updated_at,
                        q.is_deleted
                    FROM quotes q
                    WHERE q.""Status"" = 4
                      AND q.""PolicyNumber"" IS NOT NULL
                      AND NOT EXISTS (SELECT 1 FROM policies p WHERE p.bound_quote_id = q.id);

                    -- 3. Add policy_id column if missing
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                                   WHERE table_name = 'policy_transactions' AND column_name = 'policy_id') THEN
                        ALTER TABLE policy_transactions ADD COLUMN policy_id uuid;
                    END IF;

                    -- 4. Add status column if missing
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                                   WHERE table_name = 'policy_transactions' AND column_name = 'status') THEN
                        ALTER TABLE policy_transactions ADD COLUMN status integer NOT NULL DEFAULT 2;
                    END IF;

                    -- 5. Add prior_policy_id column if missing
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                                   WHERE table_name = 'policy_transactions' AND column_name = 'prior_policy_id') THEN
                        ALTER TABLE policy_transactions ADD COLUMN prior_policy_id uuid;
                    END IF;

                    -- 6. Populate policy_id
                    UPDATE policy_transactions pt
                    SET policy_id = p.id
                    FROM policies p
                    WHERE p.bound_quote_id = pt.policy_id OR (
                        pt.policy_id IS NULL AND EXISTS (
                            SELECT 1 FROM pg_constraint c JOIN pg_class t ON t.oid = c.conrelid
                            WHERE t.relname = 'policy_transactions'
                            AND c.conname IN ('FK_policy_transactions_quotes_QuoteId','fk_policy_transactions_quotes_quote_id')
                        ) AND p.bound_quote_id = (
                            SELECT id FROM quotes q2 WHERE q2.id = pt.policy_id LIMIT 1
                        )
                    );

                    -- Simpler populate: match via QuoteId or quote_id column
                    IF EXISTS (SELECT 1 FROM information_schema.columns
                               WHERE table_name = 'policy_transactions'
                               AND column_name IN ('QuoteId', 'quote_id')) THEN
                        EXECUTE (
                            'UPDATE policy_transactions pt SET policy_id = p.id '
                            'FROM policies p WHERE p.bound_quote_id = pt.' || quote_ident(
                                (SELECT column_name FROM information_schema.columns
                                 WHERE table_name = 'policy_transactions'
                                 AND column_name IN ('QuoteId', 'quote_id') LIMIT 1)
                            )
                        );
                    END IF;

                    -- 7. Delete orphaned transactions
                    DELETE FROM policy_transactions WHERE policy_id IS NULL;

                    -- 8. Make policy_id NOT NULL if still nullable
                    IF EXISTS (SELECT 1 FROM information_schema.columns
                               WHERE table_name = 'policy_transactions'
                               AND column_name = 'policy_id'
                               AND is_nullable = 'YES') THEN
                        ALTER TABLE policy_transactions ALTER COLUMN policy_id SET NOT NULL;
                    END IF;

                    -- 9. Create index if missing
                    IF NOT EXISTS (SELECT 1 FROM pg_indexes
                                   WHERE tablename = 'policy_transactions'
                                   AND indexname = 'ix_policy_transactions_policy_id') THEN
                        CREATE INDEX ix_policy_transactions_policy_id ON policy_transactions(policy_id);
                    END IF;

                    -- 10. Add FK policy_transactions -> policies if missing
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_policy_transactions_policies') THEN
                        ALTER TABLE policy_transactions
                            ADD CONSTRAINT fk_policy_transactions_policies
                            FOREIGN KEY (policy_id) REFERENCES policies(id) ON DELETE CASCADE;
                    END IF;

                    -- 11. Add FK policy_transactions -> prior_policy if missing
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_policy_transactions_prior_policy') THEN
                        ALTER TABLE policy_transactions
                            ADD CONSTRAINT fk_policy_transactions_prior_policy
                            FOREIGN KEY (prior_policy_id) REFERENCES policies(id) ON DELETE SET NULL;
                    END IF;

                    -- 12. Drop old quote FK (either naming convention)
                    IF EXISTS (SELECT 1 FROM pg_constraint c JOIN pg_class t ON t.oid = c.conrelid
                               WHERE t.relname = 'policy_transactions'
                               AND c.conname IN ('FK_policy_transactions_quotes_QuoteId','fk_policy_transactions_quotes_quote_id')) THEN
                        EXECUTE (SELECT 'ALTER TABLE policy_transactions DROP CONSTRAINT ' || quote_ident(c.conname)
                                 FROM pg_constraint c JOIN pg_class t ON t.oid = c.conrelid
                                 WHERE t.relname = 'policy_transactions'
                                 AND c.conname IN ('FK_policy_transactions_quotes_QuoteId','fk_policy_transactions_quotes_quote_id')
                                 LIMIT 1);
                    END IF;

                    IF EXISTS (SELECT 1 FROM pg_constraint c JOIN pg_class t ON t.oid = c.conrelid
                               WHERE t.relname = 'policy_transactions'
                               AND c.conname IN ('FK_policy_transactions_quotes_RenewalQuoteId','fk_policy_transactions_quotes_renewal_quote_id')) THEN
                        EXECUTE (SELECT 'ALTER TABLE policy_transactions DROP CONSTRAINT ' || quote_ident(c.conname)
                                 FROM pg_constraint c JOIN pg_class t ON t.oid = c.conrelid
                                 WHERE t.relname = 'policy_transactions'
                                 AND c.conname IN ('FK_policy_transactions_quotes_RenewalQuoteId','fk_policy_transactions_quotes_renewal_quote_id')
                                 LIMIT 1);
                    END IF;

                    -- 13. Drop old indexes
                    IF EXISTS (SELECT 1 FROM pg_indexes WHERE tablename = 'policy_transactions'
                               AND indexname IN ('IX_policy_transactions_QuoteId','ix_policy_transactions_quote_id')) THEN
                        EXECUTE (SELECT 'DROP INDEX ' || quote_ident(indexname)
                                 FROM pg_indexes WHERE tablename = 'policy_transactions'
                                 AND indexname IN ('IX_policy_transactions_QuoteId','ix_policy_transactions_quote_id')
                                 LIMIT 1);
                    END IF;
                    IF EXISTS (SELECT 1 FROM pg_indexes WHERE tablename = 'policy_transactions'
                               AND indexname IN ('IX_policy_transactions_RenewalQuoteId','ix_policy_transactions_renewal_quote_id')) THEN
                        EXECUTE (SELECT 'DROP INDEX ' || quote_ident(indexname)
                                 FROM pg_indexes WHERE tablename = 'policy_transactions'
                                 AND indexname IN ('IX_policy_transactions_RenewalQuoteId','ix_policy_transactions_renewal_quote_id')
                                 LIMIT 1);
                    END IF;

                    -- 14. Drop old columns
                    IF EXISTS (SELECT 1 FROM information_schema.columns
                               WHERE table_name = 'policy_transactions'
                               AND column_name IN ('QuoteId','quote_id')) THEN
                        EXECUTE (SELECT 'ALTER TABLE policy_transactions DROP COLUMN ' || quote_ident(column_name)
                                 FROM information_schema.columns
                                 WHERE table_name = 'policy_transactions'
                                 AND column_name IN ('QuoteId','quote_id') LIMIT 1);
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns
                               WHERE table_name = 'policy_transactions'
                               AND column_name IN ('RenewalQuoteId','renewal_quote_id')) THEN
                        EXECUTE (SELECT 'ALTER TABLE policy_transactions DROP COLUMN ' || quote_ident(column_name)
                                 FROM information_schema.columns
                                 WHERE table_name = 'policy_transactions'
                                 AND column_name IN ('RenewalQuoteId','renewal_quote_id') LIMIT 1);
                    END IF;

                    -- 15. New columns on quotes (idempotent)
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'quotes' AND column_name = 'company_id') THEN
                        ALTER TABLE quotes ADD COLUMN company_id integer;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'quotes' AND column_name = 'producer_id') THEN
                        ALTER TABLE quotes ADD COLUMN producer_id integer;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'quotes' AND column_name = 'is_filing_state') THEN
                        ALTER TABLE quotes ADD COLUMN is_filing_state boolean NOT NULL DEFAULT false;
                    END IF;

                    -- 16. Replace unconditional policy_number unique index with filtered one
                    IF EXISTS (SELECT 1 FROM pg_indexes WHERE tablename = 'quotes'
                               AND indexname IN ('IX_quotes_PolicyNumber','ix_quotes_policy_number')) THEN
                        EXECUTE (SELECT 'DROP INDEX ' || quote_ident(indexname)
                                 FROM pg_indexes WHERE tablename = 'quotes'
                                 AND indexname IN ('IX_quotes_PolicyNumber','ix_quotes_policy_number') LIMIT 1);
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE tablename = 'quotes'
                                   AND indexname IN ('IX_quotes_PolicyNumber','ix_quotes_policy_number',
                                                     'IX_quotes_policy_number','ix_quotes_PolicyNumber')) THEN
                        CREATE UNIQUE INDEX ix_quotes_policy_number ON quotes(""PolicyNumber"") WHERE ""PolicyNumber"" IS NOT NULL;
                    END IF;

                    -- 17. producer_id on submissions
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'submissions' AND column_name = 'producer_id') THEN
                        ALTER TABLE submissions ADD COLUMN producer_id integer;
                    END IF;
                END$$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'policies') THEN
                        ALTER TABLE policy_transactions DROP CONSTRAINT IF EXISTS fk_policy_transactions_policies;
                        ALTER TABLE policy_transactions DROP CONSTRAINT IF EXISTS fk_policy_transactions_prior_policy;
                        DROP TABLE policies;
                    END IF;
                END$$;
            ");
        }
    }
}
