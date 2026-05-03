using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Rating_Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    -- ================================================================
                    -- PART 1: Clean up old QuoteId/FK leftovers (idempotent)
                    -- ================================================================
                    IF EXISTS (SELECT 1 FROM pg_constraint c JOIN pg_class t ON t.oid = c.conrelid
                               WHERE t.relname = 'policy_transactions' AND c.conname = 'FK_policy_transactions_quotes_QuoteId') THEN
                        ALTER TABLE policy_transactions DROP CONSTRAINT ""FK_policy_transactions_quotes_QuoteId"";
                    END IF;
                    IF EXISTS (SELECT 1 FROM pg_constraint c JOIN pg_class t ON t.oid = c.conrelid
                               WHERE t.relname = 'policy_transactions' AND c.conname = 'FK_policy_transactions_quotes_RenewalQuoteId') THEN
                        ALTER TABLE policy_transactions DROP CONSTRAINT ""FK_policy_transactions_quotes_RenewalQuoteId"";
                    END IF;
                    IF EXISTS (SELECT 1 FROM pg_indexes WHERE tablename = 'quotes' AND indexname = 'IX_quotes_PolicyNumber') THEN
                        DROP INDEX ""IX_quotes_PolicyNumber"";
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'policy_transactions' AND column_name = 'QuoteId') THEN
                        ALTER TABLE policy_transactions RENAME COLUMN ""QuoteId"" TO ""PolicyId"";
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'policy_transactions' AND column_name = 'RenewalQuoteId') THEN
                        ALTER TABLE policy_transactions RENAME COLUMN ""RenewalQuoteId"" TO ""PriorPolicyId"";
                    END IF;
                    IF EXISTS (SELECT 1 FROM pg_indexes WHERE tablename = 'policy_transactions' AND indexname = 'IX_policy_transactions_RenewalQuoteId') THEN
                        ALTER INDEX ""IX_policy_transactions_RenewalQuoteId"" RENAME TO ""IX_policy_transactions_PriorPolicyId"";
                    END IF;
                    IF EXISTS (SELECT 1 FROM pg_indexes WHERE tablename = 'policy_transactions' AND indexname = 'IX_policy_transactions_QuoteId') THEN
                        ALTER INDEX ""IX_policy_transactions_QuoteId"" RENAME TO ""IX_policy_transactions_PolicyId"";
                    END IF;

                    -- ================================================================
                    -- PART 2: Add new columns to existing tables (IF NOT EXISTS)
                    -- ================================================================
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'submissions' AND column_name = 'producer_id') THEN
                        ALTER TABLE submissions ADD COLUMN producer_id integer;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'submission_equipment' AND column_name = 'deductible') THEN
                        ALTER TABLE submission_equipment ADD COLUMN deductible numeric(18,2);
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'submission_equipment' AND column_name = 'equipment_type_id') THEN
                        ALTER TABLE submission_equipment ADD COLUMN equipment_type_id uuid;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'submission_equipment' AND column_name = 'settlement_basis') THEN
                        ALTER TABLE submission_equipment ADD COLUMN settlement_basis character varying(10);
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'submission_equipment' AND column_name = 'territory_code') THEN
                        ALTER TABLE submission_equipment ADD COLUMN territory_code character varying(20);
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'quotes' AND column_name = 'company_id') THEN
                        ALTER TABLE quotes ADD COLUMN company_id integer;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'quotes' AND column_name = 'is_filing_state') THEN
                        ALTER TABLE quotes ADD COLUMN is_filing_state boolean NOT NULL DEFAULT false;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'quotes' AND column_name = 'producer_id') THEN
                        ALTER TABLE quotes ADD COLUMN producer_id integer;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'policy_transactions' AND column_name = 'status') THEN
                        ALTER TABLE policy_transactions ADD COLUMN status integer NOT NULL DEFAULT 0;
                    END IF;

                    -- ================================================================
                    -- PART 3: Create new tables (IF NOT EXISTS)
                    -- ================================================================

                    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'equipment_types') THEN
                        CREATE TABLE equipment_types (
                            id uuid NOT NULL,
                            type_number integer NOT NULL,
                            name character varying(100) NOT NULL,
                            created_at timestamp with time zone NOT NULL,
                            updated_at timestamp with time zone NOT NULL,
                            is_deleted boolean NOT NULL,
                            deleted_at timestamp with time zone,
                            CONSTRAINT ""PK_equipment_types"" PRIMARY KEY (id)
                        );
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'pending_qbo_syncs') THEN
                        CREATE TABLE pending_qbo_syncs (
                            id bigint GENERATED BY DEFAULT AS IDENTITY,
                            tenant_id integer NOT NULL,
                            rollup_id bigint NOT NULL,
                            status character varying(20) NOT NULL,
                            attempt_count integer NOT NULL,
                            next_retry_at timestamp with time zone,
                            last_error character varying(2000),
                            created_at timestamp with time zone NOT NULL,
                            updated_at timestamp with time zone NOT NULL,
                            CONSTRAINT ""PK_pending_qbo_syncs"" PRIMARY KEY (id),
                            CONSTRAINT ""FK_pending_qbo_syncs_journal_entry_rollups_RollupId""
                                FOREIGN KEY (rollup_id) REFERENCES journal_entry_rollups ON DELETE CASCADE
                        );
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'period_close_checklist') THEN
                        CREATE TABLE period_close_checklist (
                            id bigint GENERATED BY DEFAULT AS IDENTITY,
                            tenant_id integer NOT NULL,
                            period_id bigint NOT NULL,
                            check_key character varying(30) NOT NULL,
                            issue_count integer NOT NULL,
                            is_blocking boolean NOT NULL,
                            last_checked_at timestamp with time zone NOT NULL,
                            CONSTRAINT ""PK_period_close_checklist"" PRIMARY KEY (id),
                            CONSTRAINT ""FK_period_close_checklist_accounting_periods_PeriodId""
                                FOREIGN KEY (period_id) REFERENCES accounting_periods ON DELETE CASCADE
                        );
                    END IF;

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
                            status integer NOT NULL,
                            bound_date date NOT NULL,
                            issued_date date,
                            cancelled_date date,
                            non_renewed_date date,
                            created_at timestamp with time zone NOT NULL,
                            updated_at timestamp with time zone NOT NULL,
                            is_deleted boolean NOT NULL,
                            deleted_at timestamp with time zone,
                            CONSTRAINT ""PK_policies"" PRIMARY KEY (id),
                            CONSTRAINT ""FK_policies_carriers_CarrierId"" FOREIGN KEY (carrier_id) REFERENCES carriers ON DELETE RESTRICT,
                            CONSTRAINT ""FK_policies_quotes_BoundQuoteId"" FOREIGN KEY (bound_quote_id) REFERENCES quotes ON DELETE RESTRICT,
                            CONSTRAINT ""FK_policies_submissions_SubmissionId"" FOREIGN KEY (submission_id) REFERENCES submissions ON DELETE RESTRICT
                        );
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'qbo_oauth_tokens') THEN
                        CREATE TABLE qbo_oauth_tokens (
                            id integer GENERATED BY DEFAULT AS IDENTITY,
                            tenant_id integer NOT NULL,
                            realm_id character varying(50) NOT NULL,
                            access_token character varying(4000) NOT NULL,
                            refresh_token character varying(500) NOT NULL,
                            access_token_expires_at timestamp with time zone NOT NULL,
                            refresh_token_expires_at timestamp with time zone NOT NULL,
                            updated_at timestamp with time zone NOT NULL,
                            CONSTRAINT ""PK_qbo_oauth_tokens"" PRIMARY KEY (id)
                        );
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'rating_plans') THEN
                        CREATE TABLE rating_plans (
                            id uuid NOT NULL,
                            line_of_business integer NOT NULL,
                            name character varying(200) NOT NULL,
                            formula_key character varying(50) NOT NULL,
                            status integer NOT NULL,
                            created_at timestamp with time zone NOT NULL,
                            updated_at timestamp with time zone NOT NULL,
                            is_deleted boolean NOT NULL,
                            deleted_at timestamp with time zone,
                            CONSTRAINT ""PK_rating_plans"" PRIMARY KEY (id)
                        );
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'territories') THEN
                        CREATE TABLE territories (
                            id uuid NOT NULL,
                            territory_number integer NOT NULL,
                            states character varying(200) NOT NULL,
                            modifier numeric(8,6) NOT NULL,
                            created_at timestamp with time zone NOT NULL,
                            updated_at timestamp with time zone NOT NULL,
                            is_deleted boolean NOT NULL,
                            deleted_at timestamp with time zone,
                            CONSTRAINT ""PK_territories"" PRIMARY KEY (id)
                        );
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'rating_plan_versions') THEN
                        CREATE TABLE rating_plan_versions (
                            id uuid NOT NULL,
                            rating_plan_id uuid NOT NULL,
                            version_number integer NOT NULL,
                            effective_date date NOT NULL,
                            expiration_date date,
                            status integer NOT NULL,
                            promoted_at timestamp with time zone,
                            promoted_by_id uuid,
                            notes character varying(1000),
                            schedule_min numeric(6,4) NOT NULL,
                            schedule_max numeric(6,4) NOT NULL,
                            minimum_premium numeric(18,2),
                            created_at timestamp with time zone NOT NULL,
                            updated_at timestamp with time zone NOT NULL,
                            is_deleted boolean NOT NULL,
                            deleted_at timestamp with time zone,
                            CONSTRAINT ""PK_rating_plan_versions"" PRIMARY KEY (id),
                            CONSTRAINT ""FK_rating_plan_versions_rating_plans_RatingPlanId""
                                FOREIGN KEY (rating_plan_id) REFERENCES rating_plans(id) ON DELETE CASCADE,
                            CONSTRAINT ""FK_rating_plan_versions_users_PromotedById""
                                FOREIGN KEY (promoted_by_id) REFERENCES users ON DELETE SET NULL
                        );
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'carrier_rating_assignments') THEN
                        CREATE TABLE carrier_rating_assignments (
                            id uuid NOT NULL,
                            carrier_id uuid NOT NULL,
                            line_of_business integer NOT NULL,
                            rating_plan_version_id uuid NOT NULL,
                            created_at timestamp with time zone NOT NULL,
                            updated_at timestamp with time zone NOT NULL,
                            is_deleted boolean NOT NULL,
                            deleted_at timestamp with time zone,
                            CONSTRAINT ""PK_carrier_rating_assignments"" PRIMARY KEY (id),
                            CONSTRAINT ""FK_carrier_rating_assignments_carriers_CarrierId""
                                FOREIGN KEY (carrier_id) REFERENCES carriers ON DELETE RESTRICT,
                            CONSTRAINT ""FK_carrier_rating_assignments_rating_plan_versions_RatingPlanV~""
                                FOREIGN KEY (rating_plan_version_id) REFERENCES rating_plan_versions(id) ON DELETE RESTRICT
                        );
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'eligibility_rules') THEN
                        CREATE TABLE eligibility_rules (
                            id uuid NOT NULL,
                            rating_plan_version_id uuid NOT NULL,
                            equipment_type_id uuid NOT NULL,
                            accepted boolean NOT NULL,
                            created_at timestamp with time zone NOT NULL,
                            updated_at timestamp with time zone NOT NULL,
                            is_deleted boolean NOT NULL,
                            deleted_at timestamp with time zone,
                            CONSTRAINT ""PK_eligibility_rules"" PRIMARY KEY (id),
                            CONSTRAINT ""FK_eligibility_rules_equipment_types_EquipmentTypeId""
                                FOREIGN KEY (equipment_type_id) REFERENCES equipment_types(id) ON DELETE RESTRICT,
                            CONSTRAINT ""FK_eligibility_rules_rating_plan_versions_RatingPlanVersionId""
                                FOREIGN KEY (rating_plan_version_id) REFERENCES rating_plan_versions(id) ON DELETE CASCADE
                        );
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'factor_tables') THEN
                        CREATE TABLE factor_tables (
                            id uuid NOT NULL,
                            rating_plan_version_id uuid NOT NULL,
                            code character varying(50) NOT NULL,
                            dimension_names jsonb NOT NULL,
                            value_semantics integer NOT NULL,
                            created_at timestamp with time zone NOT NULL,
                            updated_at timestamp with time zone NOT NULL,
                            is_deleted boolean NOT NULL,
                            deleted_at timestamp with time zone,
                            CONSTRAINT ""PK_factor_tables"" PRIMARY KEY (id),
                            CONSTRAINT ""FK_factor_tables_rating_plan_versions_RatingPlanVersionId""
                                FOREIGN KEY (rating_plan_version_id) REFERENCES rating_plan_versions(id) ON DELETE CASCADE
                        );
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'quote_rating_snapshots') THEN
                        CREATE TABLE quote_rating_snapshots (
                            id uuid NOT NULL,
                            quote_id uuid NOT NULL,
                            rating_plan_version_id uuid NOT NULL,
                            rated_at timestamp with time zone NOT NULL,
                            rated_by_id uuid NOT NULL,
                            manual_premium numeric(18,2) NOT NULL,
                            schedule_modifier numeric(6,4) NOT NULL,
                            schedule_modifier_reason character varying(500),
                            newly_acquired_equipment boolean NOT NULL,
                            debris_removal boolean NOT NULL,
                            rental_reimbursement boolean NOT NULL,
                            towing_storage_recovery boolean NOT NULL,
                            tria boolean NOT NULL,
                            endorsement_premium numeric(18,2) NOT NULL,
                            grand_total_premium numeric(18,2) NOT NULL,
                            is_bound_snapshot boolean NOT NULL,
                            created_at timestamp with time zone NOT NULL,
                            updated_at timestamp with time zone NOT NULL,
                            is_deleted boolean NOT NULL,
                            deleted_at timestamp with time zone,
                            CONSTRAINT ""PK_quote_rating_snapshots"" PRIMARY KEY (id),
                            CONSTRAINT ""FK_quote_rating_snapshots_quotes_QuoteId""
                                FOREIGN KEY (quote_id) REFERENCES quotes ON DELETE RESTRICT,
                            CONSTRAINT ""FK_quote_rating_snapshots_rating_plan_versions_RatingPlanVersi~""
                                FOREIGN KEY (rating_plan_version_id) REFERENCES rating_plan_versions(id) ON DELETE RESTRICT,
                            CONSTRAINT ""FK_quote_rating_snapshots_users_RatedById""
                                FOREIGN KEY (rated_by_id) REFERENCES users ON DELETE RESTRICT
                        );
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'factor_rows') THEN
                        CREATE TABLE factor_rows (
                            id uuid NOT NULL,
                            factor_table_id uuid NOT NULL,
                            dimension_values jsonb NOT NULL,
                            factor numeric(18,6) NOT NULL,
                            created_at timestamp with time zone NOT NULL,
                            updated_at timestamp with time zone NOT NULL,
                            is_deleted boolean NOT NULL,
                            deleted_at timestamp with time zone,
                            CONSTRAINT ""PK_factor_rows"" PRIMARY KEY (id),
                            CONSTRAINT ""FK_factor_rows_factor_tables_FactorTableId""
                                FOREIGN KEY (factor_table_id) REFERENCES factor_tables(id) ON DELETE CASCADE
                        );
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'quote_rating_lines') THEN
                        CREATE TABLE quote_rating_lines (
                            id uuid NOT NULL,
                            quote_rating_snapshot_id uuid NOT NULL,
                            exposure_ref character varying(100) NOT NULL,
                            inputs jsonb NOT NULL,
                            factors_applied jsonb NOT NULL,
                            line_premium numeric(18,2) NOT NULL,
                            created_at timestamp with time zone NOT NULL,
                            updated_at timestamp with time zone NOT NULL,
                            is_deleted boolean NOT NULL,
                            deleted_at timestamp with time zone,
                            CONSTRAINT ""PK_quote_rating_lines"" PRIMARY KEY (id),
                            CONSTRAINT ""FK_quote_rating_lines_quote_rating_snapshots_QuoteRatingSnapsh~""
                                FOREIGN KEY (quote_rating_snapshot_id) REFERENCES quote_rating_snapshots(id) ON DELETE CASCADE
                        );
                    END IF;

                    -- ================================================================
                    -- PART 4: Create indexes for NEW tables only (IF NOT EXISTS)
                    -- Pre-existing tables (policies, pending_qbo_syncs, qbo_oauth_tokens,
                    -- period_close_checklist) already have indexes from their own migrations.
                    -- ================================================================
                    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE tablename = 'submission_equipment' AND indexname = 'IX_submission_equipment_EquipmentTypeId') THEN
                        CREATE INDEX ""IX_submission_equipment_EquipmentTypeId"" ON submission_equipment(equipment_type_id);
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE tablename = 'quotes'
                                   AND indexname IN ('IX_quotes_PolicyNumber', 'ix_quotes_policy_number')) THEN
                        CREATE UNIQUE INDEX ""IX_quotes_PolicyNumber"" ON quotes(policy_number) WHERE policy_number IS NOT NULL;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE tablename = 'carrier_rating_assignments' AND indexname = 'IX_carrier_rating_assignments_CarrierId_LineOfBusiness') THEN
                        CREATE UNIQUE INDEX ""IX_carrier_rating_assignments_CarrierId_LineOfBusiness"" ON carrier_rating_assignments(carrier_id, line_of_business);
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE tablename = 'carrier_rating_assignments' AND indexname = 'IX_carrier_rating_assignments_RatingPlanVersionId') THEN
                        CREATE INDEX ""IX_carrier_rating_assignments_RatingPlanVersionId"" ON carrier_rating_assignments(rating_plan_version_id);
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE tablename = 'eligibility_rules' AND indexname = 'IX_eligibility_rules_EquipmentTypeId') THEN
                        CREATE INDEX ""IX_eligibility_rules_EquipmentTypeId"" ON eligibility_rules(equipment_type_id);
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE tablename = 'eligibility_rules' AND indexname = 'IX_eligibility_rules_RatingPlanVersionId_EquipmentTypeId') THEN
                        CREATE UNIQUE INDEX ""IX_eligibility_rules_RatingPlanVersionId_EquipmentTypeId"" ON eligibility_rules(rating_plan_version_id, equipment_type_id);
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE tablename = 'equipment_types' AND indexname = 'IX_equipment_types_TypeNumber') THEN
                        CREATE UNIQUE INDEX ""IX_equipment_types_TypeNumber"" ON equipment_types(type_number);
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE tablename = 'factor_rows' AND indexname = 'IX_factor_rows_DimensionValues') THEN
                        CREATE INDEX ""IX_factor_rows_DimensionValues"" ON factor_rows USING gin (dimension_values jsonb_path_ops);
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE tablename = 'factor_rows' AND indexname = 'IX_factor_rows_FactorTableId') THEN
                        CREATE INDEX ""IX_factor_rows_FactorTableId"" ON factor_rows(factor_table_id);
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE tablename = 'factor_tables' AND indexname = 'IX_factor_tables_RatingPlanVersionId') THEN
                        CREATE INDEX ""IX_factor_tables_RatingPlanVersionId"" ON factor_tables(rating_plan_version_id);
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE tablename = 'quote_rating_lines' AND indexname = 'IX_quote_rating_lines_QuoteRatingSnapshotId') THEN
                        CREATE INDEX ""IX_quote_rating_lines_QuoteRatingSnapshotId"" ON quote_rating_lines(quote_rating_snapshot_id);
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE tablename = 'quote_rating_snapshots' AND indexname = 'IX_quote_rating_snapshots_QuoteId') THEN
                        CREATE INDEX ""IX_quote_rating_snapshots_QuoteId"" ON quote_rating_snapshots(quote_id);
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE tablename = 'quote_rating_snapshots' AND indexname = 'IX_quote_rating_snapshots_RatedById') THEN
                        CREATE INDEX ""IX_quote_rating_snapshots_RatedById"" ON quote_rating_snapshots(rated_by_id);
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE tablename = 'quote_rating_snapshots' AND indexname = 'IX_quote_rating_snapshots_RatingPlanVersionId') THEN
                        CREATE INDEX ""IX_quote_rating_snapshots_RatingPlanVersionId"" ON quote_rating_snapshots(rating_plan_version_id);
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE tablename = 'rating_plan_versions' AND indexname = 'IX_rating_plan_versions_PromotedById') THEN
                        CREATE INDEX ""IX_rating_plan_versions_PromotedById"" ON rating_plan_versions(promoted_by_id);
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE tablename = 'rating_plan_versions' AND indexname = 'IX_rating_plan_versions_RatingPlanId') THEN
                        CREATE INDEX ""IX_rating_plan_versions_RatingPlanId"" ON rating_plan_versions(rating_plan_id);
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE tablename = 'rating_plans' AND indexname = 'IX_rating_plans_LineOfBusiness_Name') THEN
                        CREATE UNIQUE INDEX ""IX_rating_plans_LineOfBusiness_Name"" ON rating_plans(line_of_business, name);
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE tablename = 'territories' AND indexname = 'IX_territories_TerritoryNumber') THEN
                        CREATE UNIQUE INDEX ""IX_territories_TerritoryNumber"" ON territories(territory_number);
                    END IF;

                    -- ================================================================
                    -- PART 5: Add FKs on existing tables (IF NOT EXISTS)
                    -- Note: policy_transactions -> policies FKs are already added by
                    -- PolicyEntitySeparation (as fk_policy_transactions_policies /
                    -- fk_policy_transactions_prior_policy). Only add the new one.
                    -- ================================================================
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_submission_equipment_equipment_types_EquipmentTypeId') THEN
                        ALTER TABLE submission_equipment
                            ADD CONSTRAINT ""FK_submission_equipment_equipment_types_EquipmentTypeId""
                            FOREIGN KEY (equipment_type_id) REFERENCES equipment_types(id) ON DELETE RESTRICT;
                    END IF;
                END$$;
            ");

        }


        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_policy_transactions_policies_PolicyId",
                table: "policy_transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_policy_transactions_policies_PriorPolicyId",
                table: "policy_transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_submission_equipment_equipment_types_EquipmentTypeId",
                table: "submission_equipment");

            migrationBuilder.DropTable(
                name: "carrier_rating_assignments");

            migrationBuilder.DropTable(
                name: "eligibility_rules");

            migrationBuilder.DropTable(
                name: "factor_rows");

            migrationBuilder.DropTable(
                name: "pending_qbo_syncs");

            migrationBuilder.DropTable(
                name: "period_close_checklist");

            migrationBuilder.DropTable(
                name: "policies");

            migrationBuilder.DropTable(
                name: "qbo_oauth_tokens");

            migrationBuilder.DropTable(
                name: "quote_rating_lines");

            migrationBuilder.DropTable(
                name: "territories");

            migrationBuilder.DropTable(
                name: "equipment_types");

            migrationBuilder.DropTable(
                name: "factor_tables");

            migrationBuilder.DropTable(
                name: "quote_rating_snapshots");

            migrationBuilder.DropTable(
                name: "rating_plan_versions");

            migrationBuilder.DropTable(
                name: "rating_plans");

            migrationBuilder.DropIndex(
                name: "IX_submission_equipment_EquipmentTypeId",
                table: "submission_equipment");

            migrationBuilder.DropIndex(
                name: "IX_quotes_PolicyNumber",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "ProducerId",
                table: "submissions");

            migrationBuilder.DropColumn(
                name: "Deductible",
                table: "submission_equipment");

            migrationBuilder.DropColumn(
                name: "EquipmentTypeId",
                table: "submission_equipment");

            migrationBuilder.DropColumn(
                name: "SettlementBasis",
                table: "submission_equipment");

            migrationBuilder.DropColumn(
                name: "TerritoryCode",
                table: "submission_equipment");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "IsFilingState",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "ProducerId",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "policy_transactions");

            migrationBuilder.RenameColumn(
                name: "PriorPolicyId",
                table: "policy_transactions",
                newName: "RenewalQuoteId");

            migrationBuilder.RenameColumn(
                name: "PolicyId",
                table: "policy_transactions",
                newName: "QuoteId");

            migrationBuilder.RenameIndex(
                name: "IX_policy_transactions_PriorPolicyId",
                table: "policy_transactions",
                newName: "IX_policy_transactions_RenewalQuoteId");

            migrationBuilder.RenameIndex(
                name: "IX_policy_transactions_PolicyId",
                table: "policy_transactions",
                newName: "IX_policy_transactions_QuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_quotes_PolicyNumber",
                table: "quotes",
                column: "PolicyNumber",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_policy_transactions_quotes_QuoteId",
                table: "policy_transactions",
                column: "QuoteId",
                principalTable: "quotes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_policy_transactions_quotes_RenewalQuoteId",
                table: "policy_transactions",
                column: "RenewalQuoteId",
                principalTable: "quotes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}


