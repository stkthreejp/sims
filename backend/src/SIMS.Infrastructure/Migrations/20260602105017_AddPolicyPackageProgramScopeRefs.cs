using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPolicyPackageProgramScopeRefs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "State",
                table: "policy_package_configurations",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2)",
                oldMaxLength: 2);

            migrationBuilder.AddColumn<Guid>(
                name: "ProgramCarrierLineOfBusinessId",
                table: "policy_package_configurations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProgramCarrierLobStateId",
                table: "policy_package_configurations",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE policy_package_configurations
                SET "State" = NULLIF(UPPER(TRIM("State")), '');

                UPDATE policy_package_configurations p
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
                  AND pc."EffectiveDate" <= CURRENT_DATE
                  AND (pc."ExpirationDate" IS NULL OR pc."ExpirationDate" >= CURRENT_DATE)
                  AND pcl."EffectiveDate" <= CURRENT_DATE
                  AND (pcl."ExpirationDate" IS NULL OR pcl."ExpirationDate" >= CURRENT_DATE);

                UPDATE policy_package_configurations p
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
                  AND pc."EffectiveDate" <= CURRENT_DATE
                  AND (pc."ExpirationDate" IS NULL OR pc."ExpirationDate" >= CURRENT_DATE)
                  AND pcl."EffectiveDate" <= CURRENT_DATE
                  AND (pcl."ExpirationDate" IS NULL OR pcl."ExpirationDate" >= CURRENT_DATE)
                  AND pcs."EffectiveDate" <= CURRENT_DATE
                  AND (pcs."ExpirationDate" IS NULL OR pcs."ExpirationDate" >= CURRENT_DATE);

                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM policy_package_configurations p
                        WHERE p."ProgramConfigurationId" IS NOT NULL
                          AND NOT EXISTS (
                              SELECT 1
                              FROM program_configurations pcfg
                              WHERE pcfg."Id" = p."ProgramConfigurationId"
                                AND pcfg."IsActive" = TRUE
                                AND pcfg."IsDeleted" = FALSE
                          )
                    ) THEN
                        RAISE EXCEPTION 'Cannot add policy package Program SOT constraint: at least one package references an inactive or deleted Program.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM policy_package_configurations p
                        WHERE p."ProgramConfigurationId" IS NOT NULL
                          AND p."State" IS NULL
                          AND p."ProgramCarrierLineOfBusinessId" IS NULL
                    ) THEN
                        RAISE EXCEPTION 'Cannot add policy package Program SOT constraint: a Program/Carrier/LOB package has no matching active ProgramCarrierLineOfBusiness path.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM policy_package_configurations p
                        WHERE p."ProgramConfigurationId" IS NOT NULL
                          AND p."State" IS NOT NULL
                          AND p."ProgramCarrierLobStateId" IS NULL
                    ) THEN
                        RAISE EXCEPTION 'Cannot add policy package Program SOT constraint: a Program/Carrier/LOB/State package has no matching active ProgramCarrierLobState path.';
                    END IF;
                END $$;

                CREATE OR REPLACE FUNCTION validate_policy_package_program_scope()
                RETURNS trigger AS $$
                BEGIN
                    IF NEW."ProgramConfigurationId" IS NULL THEN
                        IF NEW."ProgramCarrierLineOfBusinessId" IS NOT NULL OR NEW."ProgramCarrierLobStateId" IS NOT NULL THEN
                            RAISE EXCEPTION 'Policy package without ProgramConfigurationId cannot reference Program setup scope ids.';
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
                        RAISE EXCEPTION 'Policy package ProgramConfigurationId is not active.';
                    END IF;

                    IF NEW."State" IS NULL THEN
                        IF NEW."ProgramCarrierLineOfBusinessId" IS NULL OR NEW."ProgramCarrierLobStateId" IS NOT NULL THEN
                            RAISE EXCEPTION 'Program all-state policy package requires ProgramCarrierLineOfBusinessId only.';
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
                              AND pc."EffectiveDate" <= CURRENT_DATE
                              AND (pc."ExpirationDate" IS NULL OR pc."ExpirationDate" >= CURRENT_DATE)
                              AND pcl."EffectiveDate" <= CURRENT_DATE
                              AND (pcl."ExpirationDate" IS NULL OR pcl."ExpirationDate" >= CURRENT_DATE)
                        ) THEN
                            RAISE EXCEPTION 'Policy package ProgramCarrierLineOfBusinessId does not match Program, Carrier, and LineOfBusiness.';
                        END IF;

                        RETURN NEW;
                    END IF;

                    IF NEW."ProgramCarrierLobStateId" IS NULL OR NEW."ProgramCarrierLineOfBusinessId" IS NOT NULL THEN
                        RAISE EXCEPTION 'Program state-specific policy package requires ProgramCarrierLobStateId only.';
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
                          AND pc."EffectiveDate" <= CURRENT_DATE
                          AND (pc."ExpirationDate" IS NULL OR pc."ExpirationDate" >= CURRENT_DATE)
                          AND pcl."EffectiveDate" <= CURRENT_DATE
                          AND (pcl."ExpirationDate" IS NULL OR pcl."ExpirationDate" >= CURRENT_DATE)
                          AND pcs."EffectiveDate" <= CURRENT_DATE
                          AND (pcs."ExpirationDate" IS NULL OR pcs."ExpirationDate" >= CURRENT_DATE)
                    ) THEN
                        RAISE EXCEPTION 'Policy package ProgramCarrierLobStateId does not match Program, Carrier, LineOfBusiness, and State.';
                    END IF;

                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_validate_policy_package_program_scope
                BEFORE INSERT OR UPDATE OF "ProgramConfigurationId", "CarrierId", "LineOfBusiness", "State", "ProgramCarrierLineOfBusinessId", "ProgramCarrierLobStateId"
                ON policy_package_configurations
                FOR EACH ROW
                EXECUTE FUNCTION validate_policy_package_program_scope();

                CREATE OR REPLACE FUNCTION validate_existing_policy_package_program_scopes()
                RETURNS trigger AS $$
                BEGIN
                    IF TG_TABLE_NAME = 'program_carriers' THEN
                        IF EXISTS (
                            SELECT 1
                            FROM policy_package_configurations p
                            INNER JOIN program_carrier_lines_of_business pcl ON pcl."Id" = p."ProgramCarrierLineOfBusinessId"
                            WHERE pcl."ProgramCarrierId" = NEW."Id"
                              AND (p."ProgramConfigurationId" <> NEW."ProgramConfigurationId" OR p."CarrierId" <> NEW."CarrierId")
                        ) THEN
                            RAISE EXCEPTION 'Program setup change would invalidate existing policy package ProgramCarrierLineOfBusinessId.';
                        END IF;

                        IF EXISTS (
                            SELECT 1
                            FROM policy_package_configurations p
                            INNER JOIN program_carrier_lob_states pcs ON pcs."Id" = p."ProgramCarrierLobStateId"
                            INNER JOIN program_carrier_lines_of_business pcl ON pcl."Id" = pcs."ProgramCarrierLineOfBusinessId"
                            WHERE pcl."ProgramCarrierId" = NEW."Id"
                              AND (p."ProgramConfigurationId" <> NEW."ProgramConfigurationId" OR p."CarrierId" <> NEW."CarrierId")
                        ) THEN
                            RAISE EXCEPTION 'Program setup change would invalidate existing policy package ProgramCarrierLobStateId.';
                        END IF;
                    END IF;

                    IF TG_TABLE_NAME = 'program_carrier_lines_of_business' THEN
                        IF EXISTS (
                            SELECT 1
                            FROM policy_package_configurations p
                            INNER JOIN program_carriers pc ON pc."Id" = NEW."ProgramCarrierId"
                            WHERE p."ProgramCarrierLineOfBusinessId" = NEW."Id"
                              AND (
                                  p."LineOfBusiness" <> NEW."LineOfBusiness"
                                  OR p."ProgramConfigurationId" <> pc."ProgramConfigurationId"
                                  OR p."CarrierId" <> pc."CarrierId"
                              )
                        ) THEN
                            RAISE EXCEPTION 'Program setup change would invalidate existing policy package ProgramCarrierLineOfBusinessId.';
                        END IF;

                        IF EXISTS (
                            SELECT 1
                            FROM policy_package_configurations p
                            INNER JOIN program_carrier_lob_states pcs ON pcs."Id" = p."ProgramCarrierLobStateId"
                            INNER JOIN program_carriers pc ON pc."Id" = NEW."ProgramCarrierId"
                            WHERE pcs."ProgramCarrierLineOfBusinessId" = NEW."Id"
                              AND (
                                  p."LineOfBusiness" <> NEW."LineOfBusiness"
                                  OR p."ProgramConfigurationId" <> pc."ProgramConfigurationId"
                                  OR p."CarrierId" <> pc."CarrierId"
                              )
                        ) THEN
                            RAISE EXCEPTION 'Program setup change would invalidate existing policy package ProgramCarrierLobStateId.';
                        END IF;
                    END IF;

                    IF TG_TABLE_NAME = 'program_carrier_lob_states' THEN
                        IF EXISTS (
                            SELECT 1
                            FROM policy_package_configurations p
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
                            RAISE EXCEPTION 'Program setup change would invalidate existing policy package ProgramCarrierLobStateId.';
                        END IF;
                    END IF;

                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_validate_policy_packages_after_program_carrier_change
                AFTER UPDATE OF "ProgramConfigurationId", "CarrierId"
                ON program_carriers
                FOR EACH ROW
                EXECUTE FUNCTION validate_existing_policy_package_program_scopes();

                CREATE TRIGGER trg_validate_policy_packages_after_program_lob_change
                AFTER UPDATE OF "ProgramCarrierId", "LineOfBusiness"
                ON program_carrier_lines_of_business
                FOR EACH ROW
                EXECUTE FUNCTION validate_existing_policy_package_program_scopes();

                CREATE TRIGGER trg_validate_policy_packages_after_program_state_change
                AFTER UPDATE OF "ProgramCarrierLineOfBusinessId", "StateCode"
                ON program_carrier_lob_states
                FOR EACH ROW
                EXECUTE FUNCTION validate_existing_policy_package_program_scopes();
                """);

            migrationBuilder.CreateIndex(
                name: "ix_policy_package_program_lob_scope",
                table: "policy_package_configurations",
                column: "ProgramCarrierLineOfBusinessId");

            migrationBuilder.CreateIndex(
                name: "ix_policy_package_program_state_scope",
                table: "policy_package_configurations",
                column: "ProgramCarrierLobStateId");

            migrationBuilder.AddCheckConstraint(
                name: "ck_policy_package_program_scope_canonical",
                table: "policy_package_configurations",
                sql: "(\n    \"ProgramConfigurationId\" IS NULL\n    AND \"ProgramCarrierLineOfBusinessId\" IS NULL\n    AND \"ProgramCarrierLobStateId\" IS NULL\n)\nOR (\n    \"ProgramConfigurationId\" IS NOT NULL\n    AND \"State\" IS NULL\n    AND \"ProgramCarrierLineOfBusinessId\" IS NOT NULL\n    AND \"ProgramCarrierLobStateId\" IS NULL\n)\nOR (\n    \"ProgramConfigurationId\" IS NOT NULL\n    AND \"State\" IS NOT NULL\n    AND \"ProgramCarrierLineOfBusinessId\" IS NULL\n    AND \"ProgramCarrierLobStateId\" IS NOT NULL\n)");

            migrationBuilder.AddForeignKey(
                name: "FK_policy_package_configurations_program_carrier_lines_of_busi~",
                table: "policy_package_configurations",
                column: "ProgramCarrierLineOfBusinessId",
                principalTable: "program_carrier_lines_of_business",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_policy_package_configurations_program_carrier_lob_states_Pr~",
                table: "policy_package_configurations",
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
                DROP TRIGGER IF EXISTS trg_validate_policy_packages_after_program_state_change ON program_carrier_lob_states;
                DROP TRIGGER IF EXISTS trg_validate_policy_packages_after_program_lob_change ON program_carrier_lines_of_business;
                DROP TRIGGER IF EXISTS trg_validate_policy_packages_after_program_carrier_change ON program_carriers;
                DROP TRIGGER IF EXISTS trg_validate_policy_package_program_scope ON policy_package_configurations;
                DROP FUNCTION IF EXISTS validate_existing_policy_package_program_scopes();
                DROP FUNCTION IF EXISTS validate_policy_package_program_scope();
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_policy_package_configurations_program_carrier_lines_of_busi~",
                table: "policy_package_configurations");

            migrationBuilder.DropForeignKey(
                name: "FK_policy_package_configurations_program_carrier_lob_states_Pr~",
                table: "policy_package_configurations");

            migrationBuilder.DropIndex(
                name: "ix_policy_package_program_lob_scope",
                table: "policy_package_configurations");

            migrationBuilder.DropIndex(
                name: "ix_policy_package_program_state_scope",
                table: "policy_package_configurations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_policy_package_program_scope_canonical",
                table: "policy_package_configurations");

            migrationBuilder.DropColumn(
                name: "ProgramCarrierLineOfBusinessId",
                table: "policy_package_configurations");

            migrationBuilder.DropColumn(
                name: "ProgramCarrierLobStateId",
                table: "policy_package_configurations");

            migrationBuilder.Sql(
                """
                UPDATE policy_package_configurations
                SET "State" = ''
                WHERE "State" IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "State",
                table: "policy_package_configurations",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(2)",
                oldMaxLength: 2,
                oldNullable: true);
        }
    }
}
