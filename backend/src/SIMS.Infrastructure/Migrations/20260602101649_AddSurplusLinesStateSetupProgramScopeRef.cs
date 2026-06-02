using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSurplusLinesStateSetupProgramScopeRef : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProgramCarrierLobStateId",
                table: "surplus_lines_state_setups",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE surplus_lines_state_setups
                SET "StateCode" = UPPER(TRIM("StateCode"))
                WHERE "StateCode" IS NOT NULL;

                UPDATE surplus_lines_state_setups sls
                SET "ProgramCarrierLobStateId" = pcs."Id"
                FROM program_carrier_lob_states pcs
                INNER JOIN program_carrier_lines_of_business pcl ON pcl."Id" = pcs."ProgramCarrierLineOfBusinessId"
                INNER JOIN program_carriers pc ON pc."Id" = pcl."ProgramCarrierId"
                WHERE sls."ProgramConfigurationId" IS NOT NULL
                  AND sls."CarrierId" IS NOT NULL
                  AND sls."LineOfBusiness" IS NOT NULL
                  AND pc."ProgramConfigurationId" = sls."ProgramConfigurationId"
                  AND pc."CarrierId" = sls."CarrierId"
                  AND pcl."LineOfBusiness" = sls."LineOfBusiness"
                  AND pcs."StateCode" = sls."StateCode"
                  AND pc."IsActive" = TRUE
                  AND pc."IsDeleted" = FALSE
                  AND pcl."IsActive" = TRUE
                  AND pcl."IsDeleted" = FALSE
                  AND pcs."IsActive" = TRUE
                  AND pcs."IsDeleted" = FALSE
                  AND pc."EffectiveDate" <= sls."EffectiveDate"
                  AND (pc."ExpirationDate" IS NULL OR pc."ExpirationDate" >= sls."EffectiveDate")
                  AND pcl."EffectiveDate" <= sls."EffectiveDate"
                  AND (pcl."ExpirationDate" IS NULL OR pcl."ExpirationDate" >= sls."EffectiveDate")
                  AND pcs."EffectiveDate" <= sls."EffectiveDate"
                  AND (pcs."ExpirationDate" IS NULL OR pcs."ExpirationDate" >= sls."EffectiveDate");

                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM surplus_lines_state_setups sls
                        WHERE sls."ProgramConfigurationId" IS NOT NULL
                          AND NOT EXISTS (
                              SELECT 1
                              FROM program_configurations pcfg
                              WHERE pcfg."Id" = sls."ProgramConfigurationId"
                                AND pcfg."IsActive" = TRUE
                                AND pcfg."IsDeleted" = FALSE
                          )
                    ) THEN
                        RAISE EXCEPTION 'Cannot add surplus lines Program SOT constraint: at least one setup references an inactive or deleted Program.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM surplus_lines_state_setups sls
                        WHERE sls."ProgramConfigurationId" IS NOT NULL
                          AND (sls."CarrierId" IS NULL OR sls."LineOfBusiness" IS NULL)
                    ) THEN
                        RAISE EXCEPTION 'Cannot add surplus lines Program SOT constraint: Program-scoped surplus lines setup requires Program, Carrier, LOB, and State.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM surplus_lines_state_setups sls
                        WHERE sls."ProgramConfigurationId" IS NOT NULL
                          AND sls."CarrierId" IS NOT NULL
                          AND sls."LineOfBusiness" IS NOT NULL
                          AND sls."ProgramCarrierLobStateId" IS NULL
                    ) THEN
                        RAISE EXCEPTION 'Cannot add surplus lines Program SOT constraint: a Program/Carrier/LOB/State setup has no matching active ProgramCarrierLobState path.';
                    END IF;
                END $$;

                CREATE OR REPLACE FUNCTION validate_surplus_lines_state_setup_program_scope()
                RETURNS trigger AS $$
                BEGIN
                    IF NEW."ProgramConfigurationId" IS NULL THEN
                        IF NEW."ProgramCarrierLobStateId" IS NOT NULL THEN
                            RAISE EXCEPTION 'Surplus lines setup without ProgramConfigurationId cannot reference ProgramCarrierLobStateId.';
                        END IF;

                        RETURN NEW;
                    END IF;

                    IF NEW."CarrierId" IS NULL OR NEW."LineOfBusiness" IS NULL OR NEW."ProgramCarrierLobStateId" IS NULL THEN
                        RAISE EXCEPTION 'Program-scoped surplus lines setup requires Program, Carrier, LOB, State, and ProgramCarrierLobStateId.';
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1
                        FROM program_configurations pcfg
                        WHERE pcfg."Id" = NEW."ProgramConfigurationId"
                          AND pcfg."IsActive" = TRUE
                          AND pcfg."IsDeleted" = FALSE
                    ) THEN
                        RAISE EXCEPTION 'Surplus lines setup ProgramConfigurationId is not active.';
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
                          AND pcs."StateCode" = NEW."StateCode"
                          AND pc."IsActive" = TRUE
                          AND pc."IsDeleted" = FALSE
                          AND pcl."IsActive" = TRUE
                          AND pcl."IsDeleted" = FALSE
                          AND pcs."IsActive" = TRUE
                          AND pcs."IsDeleted" = FALSE
                          AND pc."EffectiveDate" <= NEW."EffectiveDate"
                          AND (pc."ExpirationDate" IS NULL OR pc."ExpirationDate" >= NEW."EffectiveDate")
                          AND pcl."EffectiveDate" <= NEW."EffectiveDate"
                          AND (pcl."ExpirationDate" IS NULL OR pcl."ExpirationDate" >= NEW."EffectiveDate")
                          AND pcs."EffectiveDate" <= NEW."EffectiveDate"
                          AND (pcs."ExpirationDate" IS NULL OR pcs."ExpirationDate" >= NEW."EffectiveDate")
                    ) THEN
                        RAISE EXCEPTION 'Surplus lines setup ProgramCarrierLobStateId does not match Program, Carrier, LineOfBusiness, StateCode, and EffectiveDate.';
                    END IF;

                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_validate_surplus_lines_state_setup_program_scope
                BEFORE INSERT OR UPDATE OF "ProgramConfigurationId", "CarrierId", "LineOfBusiness", "StateCode", "ProgramCarrierLobStateId", "EffectiveDate"
                ON surplus_lines_state_setups
                FOR EACH ROW
                EXECUTE FUNCTION validate_surplus_lines_state_setup_program_scope();

                CREATE OR REPLACE FUNCTION validate_existing_surplus_lines_state_setup_program_scopes()
                RETURNS trigger AS $$
                BEGIN
                    IF TG_TABLE_NAME = 'program_carriers' THEN
                        IF EXISTS (
                            SELECT 1
                            FROM surplus_lines_state_setups sls
                            INNER JOIN program_carrier_lob_states pcs ON pcs."Id" = sls."ProgramCarrierLobStateId"
                            INNER JOIN program_carrier_lines_of_business pcl ON pcl."Id" = pcs."ProgramCarrierLineOfBusinessId"
                            WHERE pcl."ProgramCarrierId" = NEW."Id"
                              AND (sls."ProgramConfigurationId" <> NEW."ProgramConfigurationId" OR sls."CarrierId" <> NEW."CarrierId")
                        ) THEN
                            RAISE EXCEPTION 'Program setup change would invalidate existing surplus lines setup ProgramCarrierLobStateId.';
                        END IF;
                    END IF;

                    IF TG_TABLE_NAME = 'program_carrier_lines_of_business' THEN
                        IF EXISTS (
                            SELECT 1
                            FROM surplus_lines_state_setups sls
                            INNER JOIN program_carrier_lob_states pcs ON pcs."Id" = sls."ProgramCarrierLobStateId"
                            INNER JOIN program_carriers pc ON pc."Id" = NEW."ProgramCarrierId"
                            WHERE pcs."ProgramCarrierLineOfBusinessId" = NEW."Id"
                              AND (
                                  sls."LineOfBusiness" <> NEW."LineOfBusiness"
                                  OR sls."ProgramConfigurationId" <> pc."ProgramConfigurationId"
                                  OR sls."CarrierId" <> pc."CarrierId"
                              )
                        ) THEN
                            RAISE EXCEPTION 'Program setup change would invalidate existing surplus lines setup ProgramCarrierLobStateId.';
                        END IF;
                    END IF;

                    IF TG_TABLE_NAME = 'program_carrier_lob_states' THEN
                        IF EXISTS (
                            SELECT 1
                            FROM surplus_lines_state_setups sls
                            INNER JOIN program_carrier_lines_of_business pcl ON pcl."Id" = NEW."ProgramCarrierLineOfBusinessId"
                            INNER JOIN program_carriers pc ON pc."Id" = pcl."ProgramCarrierId"
                            WHERE sls."ProgramCarrierLobStateId" = NEW."Id"
                              AND (
                                  sls."StateCode" <> NEW."StateCode"
                                  OR sls."LineOfBusiness" <> pcl."LineOfBusiness"
                                  OR sls."ProgramConfigurationId" <> pc."ProgramConfigurationId"
                                  OR sls."CarrierId" <> pc."CarrierId"
                              )
                        ) THEN
                            RAISE EXCEPTION 'Program setup change would invalidate existing surplus lines setup ProgramCarrierLobStateId.';
                        END IF;
                    END IF;

                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_validate_surplus_lines_setups_after_program_carrier_change
                AFTER UPDATE OF "ProgramConfigurationId", "CarrierId"
                ON program_carriers
                FOR EACH ROW
                EXECUTE FUNCTION validate_existing_surplus_lines_state_setup_program_scopes();

                CREATE TRIGGER trg_validate_surplus_lines_setups_after_program_lob_change
                AFTER UPDATE OF "ProgramCarrierId", "LineOfBusiness"
                ON program_carrier_lines_of_business
                FOR EACH ROW
                EXECUTE FUNCTION validate_existing_surplus_lines_state_setup_program_scopes();

                CREATE TRIGGER trg_validate_surplus_lines_setups_after_program_state_change
                AFTER UPDATE OF "ProgramCarrierLineOfBusinessId", "StateCode"
                ON program_carrier_lob_states
                FOR EACH ROW
                EXECUTE FUNCTION validate_existing_surplus_lines_state_setup_program_scopes();
                """);

            migrationBuilder.CreateIndex(
                name: "ix_surplus_lines_state_setup_program_state_scope",
                table: "surplus_lines_state_setups",
                column: "ProgramCarrierLobStateId");

            migrationBuilder.AddCheckConstraint(
                name: "ck_surplus_lines_state_setup_program_scope_canonical",
                table: "surplus_lines_state_setups",
                sql: "(\n    \"ProgramConfigurationId\" IS NULL\n    AND \"ProgramCarrierLobStateId\" IS NULL\n)\nOR (\n    \"ProgramConfigurationId\" IS NOT NULL\n    AND \"CarrierId\" IS NOT NULL\n    AND \"LineOfBusiness\" IS NOT NULL\n    AND \"ProgramCarrierLobStateId\" IS NOT NULL\n)");

            migrationBuilder.AddForeignKey(
                name: "FK_surplus_lines_state_setups_program_carrier_lob_states_Progr~",
                table: "surplus_lines_state_setups",
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
                DROP TRIGGER IF EXISTS trg_validate_surplus_lines_setups_after_program_state_change ON program_carrier_lob_states;
                DROP TRIGGER IF EXISTS trg_validate_surplus_lines_setups_after_program_lob_change ON program_carrier_lines_of_business;
                DROP TRIGGER IF EXISTS trg_validate_surplus_lines_setups_after_program_carrier_change ON program_carriers;
                DROP TRIGGER IF EXISTS trg_validate_surplus_lines_state_setup_program_scope ON surplus_lines_state_setups;
                DROP FUNCTION IF EXISTS validate_existing_surplus_lines_state_setup_program_scopes();
                DROP FUNCTION IF EXISTS validate_surplus_lines_state_setup_program_scope();
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_surplus_lines_state_setups_program_carrier_lob_states_Progr~",
                table: "surplus_lines_state_setups");

            migrationBuilder.DropIndex(
                name: "ix_surplus_lines_state_setup_program_state_scope",
                table: "surplus_lines_state_setups");

            migrationBuilder.DropCheckConstraint(
                name: "ck_surplus_lines_state_setup_program_scope_canonical",
                table: "surplus_lines_state_setups");

            migrationBuilder.DropColumn(
                name: "ProgramCarrierLobStateId",
                table: "surplus_lines_state_setups");
        }
    }
}
