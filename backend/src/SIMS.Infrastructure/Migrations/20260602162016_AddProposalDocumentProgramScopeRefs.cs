using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProposalDocumentProgramScopeRefs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProgramCarrierLineOfBusinessId",
                table: "proposal_document_configurations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProgramCarrierLobStateId",
                table: "proposal_document_configurations",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE proposal_document_configurations
                SET "State" = NULLIF(UPPER(TRIM("State")), '')
                WHERE "State" IS NOT NULL;

                UPDATE proposal_document_configurations p
                SET "ProgramCarrierLineOfBusinessId" = pcl."Id"
                FROM program_carrier_lines_of_business pcl
                INNER JOIN program_carriers pc ON pc."Id" = pcl."ProgramCarrierId"
                WHERE p."ProgramConfigurationId" IS NOT NULL
                  AND p."State" IS NULL
                  AND pc."ProgramConfigurationId" = p."ProgramConfigurationId"
                  AND pc."CarrierId" = p."CarrierId"
                  AND pcl."LineOfBusiness" = p."LineOfBusiness"
                  AND pc."IsActive" = TRUE
                  AND pc."IsDeleted" = FALSE
                  AND pcl."IsActive" = TRUE
                  AND pcl."IsDeleted" = FALSE
                  AND pc."EffectiveDate" <= COALESCE(p."EffectiveDate", CURRENT_DATE)
                  AND (pc."ExpirationDate" IS NULL OR pc."ExpirationDate" >= COALESCE(p."EffectiveDate", CURRENT_DATE))
                  AND pcl."EffectiveDate" <= COALESCE(p."EffectiveDate", CURRENT_DATE)
                  AND (pcl."ExpirationDate" IS NULL OR pcl."ExpirationDate" >= COALESCE(p."EffectiveDate", CURRENT_DATE));

                UPDATE proposal_document_configurations p
                SET "ProgramCarrierLobStateId" = pcs."Id"
                FROM program_carrier_lob_states pcs
                INNER JOIN program_carrier_lines_of_business pcl ON pcl."Id" = pcs."ProgramCarrierLineOfBusinessId"
                INNER JOIN program_carriers pc ON pc."Id" = pcl."ProgramCarrierId"
                WHERE p."ProgramConfigurationId" IS NOT NULL
                  AND p."State" IS NOT NULL
                  AND pc."ProgramConfigurationId" = p."ProgramConfigurationId"
                  AND pc."CarrierId" = p."CarrierId"
                  AND pcl."LineOfBusiness" = p."LineOfBusiness"
                  AND pcs."StateCode" = p."State"
                  AND pc."IsActive" = TRUE
                  AND pc."IsDeleted" = FALSE
                  AND pcl."IsActive" = TRUE
                  AND pcl."IsDeleted" = FALSE
                  AND pcs."IsActive" = TRUE
                  AND pcs."IsDeleted" = FALSE
                  AND pc."EffectiveDate" <= COALESCE(p."EffectiveDate", CURRENT_DATE)
                  AND (pc."ExpirationDate" IS NULL OR pc."ExpirationDate" >= COALESCE(p."EffectiveDate", CURRENT_DATE))
                  AND pcl."EffectiveDate" <= COALESCE(p."EffectiveDate", CURRENT_DATE)
                  AND (pcl."ExpirationDate" IS NULL OR pcl."ExpirationDate" >= COALESCE(p."EffectiveDate", CURRENT_DATE))
                  AND pcs."EffectiveDate" <= COALESCE(p."EffectiveDate", CURRENT_DATE)
                  AND (pcs."ExpirationDate" IS NULL OR pcs."ExpirationDate" >= COALESCE(p."EffectiveDate", CURRENT_DATE));

                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM proposal_document_configurations p
                        WHERE p."Role" = 1
                          AND p."State" IS NULL
                    ) THEN
                        RAISE EXCEPTION 'Cannot add proposal document Program SOT constraint: StateNotice rows require a state.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM proposal_document_configurations p
                        WHERE p."ProgramConfigurationId" IS NOT NULL
                          AND NOT EXISTS (
                              SELECT 1
                              FROM program_configurations pcfg
                              WHERE pcfg."Id" = p."ProgramConfigurationId"
                                AND pcfg."IsActive" = TRUE
                                AND pcfg."IsDeleted" = FALSE
                          )
                    ) THEN
                        RAISE EXCEPTION 'Cannot add proposal document Program SOT constraint: at least one setup references an inactive or deleted Program.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM proposal_document_configurations p
                        WHERE p."ProgramConfigurationId" IS NOT NULL
                          AND p."State" IS NULL
                          AND p."ProgramCarrierLineOfBusinessId" IS NULL
                    ) THEN
                        RAISE EXCEPTION 'Cannot add proposal document Program SOT constraint: a Program/Carrier/LOB setup has no matching active ProgramCarrierLineOfBusiness path.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM proposal_document_configurations p
                        WHERE p."ProgramConfigurationId" IS NOT NULL
                          AND p."State" IS NOT NULL
                          AND p."ProgramCarrierLobStateId" IS NULL
                    ) THEN
                        RAISE EXCEPTION 'Cannot add proposal document Program SOT constraint: a Program/Carrier/LOB/State setup has no matching active ProgramCarrierLobState path.';
                    END IF;
                END $$;

                CREATE OR REPLACE FUNCTION validate_proposal_document_program_scope()
                RETURNS trigger AS $$
                BEGIN
                    IF NEW."Role" = 1 AND NEW."State" IS NULL THEN
                        RAISE EXCEPTION 'StateNotice proposal document setup requires State.';
                    END IF;

                    IF NEW."ProgramConfigurationId" IS NULL THEN
                        IF NEW."ProgramCarrierLineOfBusinessId" IS NOT NULL OR NEW."ProgramCarrierLobStateId" IS NOT NULL THEN
                            RAISE EXCEPTION 'Proposal document setup without ProgramConfigurationId cannot reference Program setup scope ids.';
                        END IF;

                        RETURN NEW;
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1
                        FROM program_configurations pcfg
                        WHERE pcfg."Id" = NEW."ProgramConfigurationId"
                          AND pcfg."IsActive" = TRUE
                          AND pcfg."IsDeleted" = FALSE
                    ) THEN
                        RAISE EXCEPTION 'Proposal document ProgramConfigurationId is not active.';
                    END IF;

                    IF NEW."State" IS NULL THEN
                        IF NEW."ProgramCarrierLineOfBusinessId" IS NULL OR NEW."ProgramCarrierLobStateId" IS NOT NULL THEN
                            RAISE EXCEPTION 'Program all-state proposal document setup requires ProgramCarrierLineOfBusinessId only.';
                        END IF;

                        IF NOT EXISTS (
                            SELECT 1
                            FROM program_carrier_lines_of_business pcl
                            INNER JOIN program_carriers pc ON pc."Id" = pcl."ProgramCarrierId"
                            WHERE pcl."Id" = NEW."ProgramCarrierLineOfBusinessId"
                              AND pc."ProgramConfigurationId" = NEW."ProgramConfigurationId"
                              AND pc."CarrierId" = NEW."CarrierId"
                              AND pcl."LineOfBusiness" = NEW."LineOfBusiness"
                              AND pc."IsActive" = TRUE
                              AND pc."IsDeleted" = FALSE
                              AND pcl."IsActive" = TRUE
                              AND pcl."IsDeleted" = FALSE
                              AND pc."EffectiveDate" <= COALESCE(NEW."EffectiveDate", CURRENT_DATE)
                              AND (pc."ExpirationDate" IS NULL OR pc."ExpirationDate" >= COALESCE(NEW."EffectiveDate", CURRENT_DATE))
                              AND pcl."EffectiveDate" <= COALESCE(NEW."EffectiveDate", CURRENT_DATE)
                              AND (pcl."ExpirationDate" IS NULL OR pcl."ExpirationDate" >= COALESCE(NEW."EffectiveDate", CURRENT_DATE))
                        ) THEN
                            RAISE EXCEPTION 'Proposal document ProgramCarrierLineOfBusinessId does not match Program, Carrier, LineOfBusiness, and EffectiveDate.';
                        END IF;

                        RETURN NEW;
                    END IF;

                    IF NEW."ProgramCarrierLobStateId" IS NULL OR NEW."ProgramCarrierLineOfBusinessId" IS NOT NULL THEN
                        RAISE EXCEPTION 'Program state-specific proposal document setup requires ProgramCarrierLobStateId only.';
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1
                        FROM program_carrier_lob_states pcs
                        INNER JOIN program_carrier_lines_of_business pcl ON pcl."Id" = pcs."ProgramCarrierLineOfBusinessId"
                        INNER JOIN program_carriers pc ON pc."Id" = pcl."ProgramCarrierId"
                        WHERE pcs."Id" = NEW."ProgramCarrierLobStateId"
                          AND pc."ProgramConfigurationId" = NEW."ProgramConfigurationId"
                          AND pc."CarrierId" = NEW."CarrierId"
                          AND pcl."LineOfBusiness" = NEW."LineOfBusiness"
                          AND pcs."StateCode" = NEW."State"
                          AND pc."IsActive" = TRUE
                          AND pc."IsDeleted" = FALSE
                          AND pcl."IsActive" = TRUE
                          AND pcl."IsDeleted" = FALSE
                          AND pcs."IsActive" = TRUE
                          AND pcs."IsDeleted" = FALSE
                          AND pc."EffectiveDate" <= COALESCE(NEW."EffectiveDate", CURRENT_DATE)
                          AND (pc."ExpirationDate" IS NULL OR pc."ExpirationDate" >= COALESCE(NEW."EffectiveDate", CURRENT_DATE))
                          AND pcl."EffectiveDate" <= COALESCE(NEW."EffectiveDate", CURRENT_DATE)
                          AND (pcl."ExpirationDate" IS NULL OR pcl."ExpirationDate" >= COALESCE(NEW."EffectiveDate", CURRENT_DATE))
                          AND pcs."EffectiveDate" <= COALESCE(NEW."EffectiveDate", CURRENT_DATE)
                          AND (pcs."ExpirationDate" IS NULL OR pcs."ExpirationDate" >= COALESCE(NEW."EffectiveDate", CURRENT_DATE))
                    ) THEN
                        RAISE EXCEPTION 'Proposal document ProgramCarrierLobStateId does not match Program, Carrier, LineOfBusiness, State, and EffectiveDate.';
                    END IF;

                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_validate_proposal_document_program_scope
                BEFORE INSERT OR UPDATE OF "ProgramConfigurationId", "CarrierId", "LineOfBusiness", "State", "Role", "ProgramCarrierLineOfBusinessId", "ProgramCarrierLobStateId", "EffectiveDate"
                ON proposal_document_configurations
                FOR EACH ROW
                EXECUTE FUNCTION validate_proposal_document_program_scope();

                CREATE OR REPLACE FUNCTION validate_existing_proposal_document_program_scopes()
                RETURNS trigger AS $$
                BEGIN
                    IF TG_TABLE_NAME = 'program_carriers' THEN
                        IF EXISTS (
                            SELECT 1
                            FROM proposal_document_configurations p
                            INNER JOIN program_carrier_lines_of_business pcl ON pcl."Id" = p."ProgramCarrierLineOfBusinessId"
                            WHERE pcl."ProgramCarrierId" = NEW."Id"
                              AND (p."ProgramConfigurationId" <> NEW."ProgramConfigurationId" OR p."CarrierId" <> NEW."CarrierId")
                        ) THEN
                            RAISE EXCEPTION 'Program setup change would invalidate existing proposal document ProgramCarrierLineOfBusinessId.';
                        END IF;

                        IF EXISTS (
                            SELECT 1
                            FROM proposal_document_configurations p
                            INNER JOIN program_carrier_lob_states pcs ON pcs."Id" = p."ProgramCarrierLobStateId"
                            INNER JOIN program_carrier_lines_of_business pcl ON pcl."Id" = pcs."ProgramCarrierLineOfBusinessId"
                            WHERE pcl."ProgramCarrierId" = NEW."Id"
                              AND (p."ProgramConfigurationId" <> NEW."ProgramConfigurationId" OR p."CarrierId" <> NEW."CarrierId")
                        ) THEN
                            RAISE EXCEPTION 'Program setup change would invalidate existing proposal document ProgramCarrierLobStateId.';
                        END IF;
                    END IF;

                    IF TG_TABLE_NAME = 'program_carrier_lines_of_business' THEN
                        IF EXISTS (
                            SELECT 1
                            FROM proposal_document_configurations p
                            INNER JOIN program_carriers pc ON pc."Id" = NEW."ProgramCarrierId"
                            WHERE p."ProgramCarrierLineOfBusinessId" = NEW."Id"
                              AND (
                                  p."LineOfBusiness" <> NEW."LineOfBusiness"
                                  OR p."ProgramConfigurationId" <> pc."ProgramConfigurationId"
                                  OR p."CarrierId" <> pc."CarrierId"
                              )
                        ) THEN
                            RAISE EXCEPTION 'Program setup change would invalidate existing proposal document ProgramCarrierLineOfBusinessId.';
                        END IF;

                        IF EXISTS (
                            SELECT 1
                            FROM proposal_document_configurations p
                            INNER JOIN program_carrier_lob_states pcs ON pcs."Id" = p."ProgramCarrierLobStateId"
                            INNER JOIN program_carriers pc ON pc."Id" = NEW."ProgramCarrierId"
                            WHERE pcs."ProgramCarrierLineOfBusinessId" = NEW."Id"
                              AND (
                                  p."LineOfBusiness" <> NEW."LineOfBusiness"
                                  OR p."ProgramConfigurationId" <> pc."ProgramConfigurationId"
                                  OR p."CarrierId" <> pc."CarrierId"
                              )
                        ) THEN
                            RAISE EXCEPTION 'Program setup change would invalidate existing proposal document ProgramCarrierLobStateId.';
                        END IF;
                    END IF;

                    IF TG_TABLE_NAME = 'program_carrier_lob_states' THEN
                        IF EXISTS (
                            SELECT 1
                            FROM proposal_document_configurations p
                            INNER JOIN program_carrier_lines_of_business pcl ON pcl."Id" = NEW."ProgramCarrierLineOfBusinessId"
                            INNER JOIN program_carriers pc ON pc."Id" = pcl."ProgramCarrierId"
                            WHERE p."ProgramCarrierLobStateId" = NEW."Id"
                              AND (
                                  p."State" <> NEW."StateCode"
                                  OR p."LineOfBusiness" <> pcl."LineOfBusiness"
                                  OR p."ProgramConfigurationId" <> pc."ProgramConfigurationId"
                                  OR p."CarrierId" <> pc."CarrierId"
                              )
                        ) THEN
                            RAISE EXCEPTION 'Program setup change would invalidate existing proposal document ProgramCarrierLobStateId.';
                        END IF;
                    END IF;

                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_validate_proposal_documents_after_program_carrier_change
                AFTER UPDATE OF "ProgramConfigurationId", "CarrierId"
                ON program_carriers
                FOR EACH ROW
                EXECUTE FUNCTION validate_existing_proposal_document_program_scopes();

                CREATE TRIGGER trg_validate_proposal_documents_after_program_lob_change
                AFTER UPDATE OF "ProgramCarrierId", "LineOfBusiness"
                ON program_carrier_lines_of_business
                FOR EACH ROW
                EXECUTE FUNCTION validate_existing_proposal_document_program_scopes();

                CREATE TRIGGER trg_validate_proposal_documents_after_program_state_change
                AFTER UPDATE OF "ProgramCarrierLineOfBusinessId", "StateCode"
                ON program_carrier_lob_states
                FOR EACH ROW
                EXECUTE FUNCTION validate_existing_proposal_document_program_scopes();
                """);

            migrationBuilder.CreateIndex(
                name: "ix_proposal_document_program_lob_scope",
                table: "proposal_document_configurations",
                column: "ProgramCarrierLineOfBusinessId");

            migrationBuilder.CreateIndex(
                name: "ix_proposal_document_program_state_scope",
                table: "proposal_document_configurations",
                column: "ProgramCarrierLobStateId");

            migrationBuilder.AddCheckConstraint(
                name: "ck_proposal_document_program_scope_canonical",
                table: "proposal_document_configurations",
                sql: "(\n    \"ProgramConfigurationId\" IS NULL\n    AND \"ProgramCarrierLineOfBusinessId\" IS NULL\n    AND \"ProgramCarrierLobStateId\" IS NULL\n)\nOR (\n    \"ProgramConfigurationId\" IS NOT NULL\n    AND \"State\" IS NULL\n    AND \"ProgramCarrierLineOfBusinessId\" IS NOT NULL\n    AND \"ProgramCarrierLobStateId\" IS NULL\n)\nOR (\n    \"ProgramConfigurationId\" IS NOT NULL\n    AND \"State\" IS NOT NULL\n    AND \"ProgramCarrierLineOfBusinessId\" IS NULL\n    AND \"ProgramCarrierLobStateId\" IS NOT NULL\n)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_proposal_document_state_notice_requires_state",
                table: "proposal_document_configurations",
                sql: "(\"Role\" <> 1 OR \"State\" IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_proposal_document_configurations_program_carrier_lines_of_b~",
                table: "proposal_document_configurations",
                column: "ProgramCarrierLineOfBusinessId",
                principalTable: "program_carrier_lines_of_business",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_proposal_document_configurations_program_carrier_lob_states~",
                table: "proposal_document_configurations",
                column: "ProgramCarrierLobStateId",
                principalTable: "program_carrier_lob_states",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS trg_validate_proposal_documents_after_program_state_change ON program_carrier_lob_states;
                DROP TRIGGER IF EXISTS trg_validate_proposal_documents_after_program_lob_change ON program_carrier_lines_of_business;
                DROP TRIGGER IF EXISTS trg_validate_proposal_documents_after_program_carrier_change ON program_carriers;
                DROP TRIGGER IF EXISTS trg_validate_proposal_document_program_scope ON proposal_document_configurations;
                DROP FUNCTION IF EXISTS validate_existing_proposal_document_program_scopes();
                DROP FUNCTION IF EXISTS validate_proposal_document_program_scope();
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_proposal_document_configurations_program_carrier_lines_of_b~",
                table: "proposal_document_configurations");

            migrationBuilder.DropForeignKey(
                name: "FK_proposal_document_configurations_program_carrier_lob_states~",
                table: "proposal_document_configurations");

            migrationBuilder.DropIndex(
                name: "ix_proposal_document_program_lob_scope",
                table: "proposal_document_configurations");

            migrationBuilder.DropIndex(
                name: "ix_proposal_document_program_state_scope",
                table: "proposal_document_configurations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_proposal_document_program_scope_canonical",
                table: "proposal_document_configurations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_proposal_document_state_notice_requires_state",
                table: "proposal_document_configurations");

            migrationBuilder.DropColumn(
                name: "ProgramCarrierLineOfBusinessId",
                table: "proposal_document_configurations");

            migrationBuilder.DropColumn(
                name: "ProgramCarrierLobStateId",
                table: "proposal_document_configurations");
        }
    }
}
