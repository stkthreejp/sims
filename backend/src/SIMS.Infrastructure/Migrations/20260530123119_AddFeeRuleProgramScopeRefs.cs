using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFeeRuleProgramScopeRefs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProgramCarrierId",
                table: "fee_rule_versions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProgramCarrierLineOfBusinessId",
                table: "fee_rule_versions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProgramCarrierLobStateId",
                table: "fee_rule_versions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_fee_rule_program_carrier_scope",
                table: "fee_rule_versions",
                column: "ProgramCarrierId");

            migrationBuilder.CreateIndex(
                name: "ix_fee_rule_program_lob_scope",
                table: "fee_rule_versions",
                column: "ProgramCarrierLineOfBusinessId");

            migrationBuilder.CreateIndex(
                name: "ix_fee_rule_program_state_scope",
                table: "fee_rule_versions",
                column: "ProgramCarrierLobStateId");

            migrationBuilder.Sql(
                """
                UPDATE fee_rule_versions
                SET "StateCode" = UPPER(TRIM("StateCode"))
                WHERE "ProgramConfigurationId" IS NOT NULL
                  AND "StateCode" IS NOT NULL;

                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM fee_rule_versions v
                        WHERE v."ProgramConfigurationId" IS NOT NULL
                          AND NOT EXISTS (
                              SELECT 1
                              FROM program_configurations p
                              WHERE p."Id" = v."ProgramConfigurationId"
                                AND p."IsActive" = TRUE
                                AND p."IsDeleted" = FALSE
                          )
                    ) THEN
                        RAISE EXCEPTION 'Cannot add fee Program SOT constraint: at least one Program-scoped fee rule references an inactive or deleted Program.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM fee_rule_versions v
                        WHERE v."ProgramConfigurationId" IS NOT NULL
                          AND v."LineOfBusiness" IS NOT NULL
                          AND v."LineOfBusiness" NOT IN (
                              'GeneralLiability',
                              'InlandMarine',
                              'AutoLiability',
                              'AutoPhysicalDamage',
                              'Property',
                              'CommercialAuto',
                              'BusinessOwners',
                              'WorkersCompensation',
                              'ProfessionalLiability',
                              'Umbrella',
                              'Cyber',
                              'ExcessLiability',
                              'Other'
                          )
                    ) THEN
                        RAISE EXCEPTION 'Cannot add fee Program SOT constraint: at least one Program-scoped fee rule has an unsupported LineOfBusiness value.';
                    END IF;
                END $$;

                UPDATE fee_rule_versions v
                SET "ProgramCarrierId" = pc."Id"
                FROM program_carriers pc
                WHERE v."ProgramConfigurationId" IS NOT NULL
                  AND v."CarrierId" IS NOT NULL
                  AND v."LineOfBusiness" IS NULL
                  AND v."StateCode" IS NULL
                  AND pc."ProgramConfigurationId" = v."ProgramConfigurationId"
                  AND pc."CarrierId" = v."CarrierId"
                  AND pc."IsActive" = TRUE
                  AND pc."IsDeleted" = FALSE
                  AND pc."EffectiveDate" <= v."EffectiveDate"
                  AND (pc."ExpirationDate" IS NULL OR pc."ExpirationDate" >= v."EffectiveDate");

                UPDATE fee_rule_versions v
                SET "ProgramCarrierLineOfBusinessId" = pcl."Id"
                FROM program_carrier_lines_of_business pcl
                INNER JOIN program_carriers pc ON pc."Id" = pcl."ProgramCarrierId"
                WHERE v."ProgramConfigurationId" IS NOT NULL
                  AND v."CarrierId" IS NOT NULL
                  AND v."LineOfBusiness" IS NOT NULL
                  AND v."StateCode" IS NULL
                  AND pc."ProgramConfigurationId" = v."ProgramConfigurationId"
                  AND pc."CarrierId" = v."CarrierId"
                  AND pcl."LineOfBusiness" = CASE v."LineOfBusiness"
                        WHEN 'GeneralLiability' THEN 1
                        WHEN 'InlandMarine' THEN 10
                        WHEN 'AutoLiability' THEN 11
                        WHEN 'AutoPhysicalDamage' THEN 12
                        WHEN 'Property' THEN 2
                        WHEN 'CommercialAuto' THEN 3
                        WHEN 'BusinessOwners' THEN 4
                        WHEN 'WorkersCompensation' THEN 5
                        WHEN 'ProfessionalLiability' THEN 6
                        WHEN 'Umbrella' THEN 7
                        WHEN 'Cyber' THEN 8
                        WHEN 'ExcessLiability' THEN 9
                        WHEN 'Other' THEN 99
                        ELSE -1
                      END
                  AND pc."IsActive" = TRUE
                  AND pc."IsDeleted" = FALSE
                  AND pcl."IsActive" = TRUE
                  AND pcl."IsDeleted" = FALSE
                  AND pc."EffectiveDate" <= v."EffectiveDate"
                  AND (pc."ExpirationDate" IS NULL OR pc."ExpirationDate" >= v."EffectiveDate")
                  AND pcl."EffectiveDate" <= v."EffectiveDate"
                  AND (pcl."ExpirationDate" IS NULL OR pcl."ExpirationDate" >= v."EffectiveDate");

                UPDATE fee_rule_versions v
                SET "ProgramCarrierLobStateId" = pcs."Id"
                FROM program_carrier_lob_states pcs
                INNER JOIN program_carrier_lines_of_business pcl ON pcl."Id" = pcs."ProgramCarrierLineOfBusinessId"
                INNER JOIN program_carriers pc ON pc."Id" = pcl."ProgramCarrierId"
                WHERE v."ProgramConfigurationId" IS NOT NULL
                  AND v."CarrierId" IS NOT NULL
                  AND v."LineOfBusiness" IS NOT NULL
                  AND v."StateCode" IS NOT NULL
                  AND pc."ProgramConfigurationId" = v."ProgramConfigurationId"
                  AND pc."CarrierId" = v."CarrierId"
                  AND pcl."LineOfBusiness" = CASE v."LineOfBusiness"
                        WHEN 'GeneralLiability' THEN 1
                        WHEN 'InlandMarine' THEN 10
                        WHEN 'AutoLiability' THEN 11
                        WHEN 'AutoPhysicalDamage' THEN 12
                        WHEN 'Property' THEN 2
                        WHEN 'CommercialAuto' THEN 3
                        WHEN 'BusinessOwners' THEN 4
                        WHEN 'WorkersCompensation' THEN 5
                        WHEN 'ProfessionalLiability' THEN 6
                        WHEN 'Umbrella' THEN 7
                        WHEN 'Cyber' THEN 8
                        WHEN 'ExcessLiability' THEN 9
                        WHEN 'Other' THEN 99
                        ELSE -1
                      END
                  AND pcs."StateCode" = UPPER(v."StateCode")
                  AND pc."IsActive" = TRUE
                  AND pc."IsDeleted" = FALSE
                  AND pcl."IsActive" = TRUE
                  AND pcl."IsDeleted" = FALSE
                  AND pcs."IsActive" = TRUE
                  AND pcs."IsDeleted" = FALSE
                  AND pc."EffectiveDate" <= v."EffectiveDate"
                  AND (pc."ExpirationDate" IS NULL OR pc."ExpirationDate" >= v."EffectiveDate")
                  AND pcl."EffectiveDate" <= v."EffectiveDate"
                  AND (pcl."ExpirationDate" IS NULL OR pcl."ExpirationDate" >= v."EffectiveDate")
                  AND pcs."EffectiveDate" <= v."EffectiveDate"
                  AND (pcs."ExpirationDate" IS NULL OR pcs."ExpirationDate" >= v."EffectiveDate");
                """);

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM fee_rule_versions v
                        WHERE v."ProgramConfigurationId" IS NOT NULL
                          AND v."CarrierId" IS NOT NULL
                          AND v."LineOfBusiness" IS NULL
                          AND v."StateCode" IS NULL
                          AND v."ProgramCarrierId" IS NULL
                    ) THEN
                        RAISE EXCEPTION 'Cannot add fee Program SOT constraint: at least one Program/Carrier fee rule has no matching active ProgramCarrier path.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM fee_rule_versions v
                        WHERE v."ProgramConfigurationId" IS NOT NULL
                          AND v."CarrierId" IS NOT NULL
                          AND v."LineOfBusiness" IS NOT NULL
                          AND v."StateCode" IS NULL
                          AND v."ProgramCarrierLineOfBusinessId" IS NULL
                    ) THEN
                        RAISE EXCEPTION 'Cannot add fee Program SOT constraint: at least one Program/Carrier/LOB fee rule has no matching active ProgramCarrierLineOfBusiness path.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM fee_rule_versions v
                        WHERE v."ProgramConfigurationId" IS NOT NULL
                          AND v."CarrierId" IS NOT NULL
                          AND v."LineOfBusiness" IS NOT NULL
                          AND v."StateCode" IS NOT NULL
                          AND v."ProgramCarrierLobStateId" IS NULL
                    ) THEN
                        RAISE EXCEPTION 'Cannot add fee Program SOT constraint: at least one Program/Carrier/LOB/State fee rule has no matching active ProgramCarrierLobState path.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM fee_rule_versions v
                        WHERE v."ProgramConfigurationId" IS NOT NULL
                          AND (
                              (v."CarrierId" IS NULL AND (v."LineOfBusiness" IS NOT NULL OR v."StateCode" IS NOT NULL))
                              OR (v."CarrierId" IS NOT NULL AND v."LineOfBusiness" IS NULL AND v."StateCode" IS NOT NULL)
                          )
                    ) THEN
                        RAISE EXCEPTION 'Cannot add fee Program SOT constraint: Program-scoped fee rules cannot skip carrier or LOB levels before state.';
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION validate_fee_rule_program_scope()
                RETURNS trigger AS $$
                DECLARE
                    lob_value integer;
                    mismatch_exists boolean;
                BEGIN
                    lob_value := CASE NEW."LineOfBusiness"
                        WHEN 'GeneralLiability' THEN 1
                        WHEN 'InlandMarine' THEN 10
                        WHEN 'AutoLiability' THEN 11
                        WHEN 'AutoPhysicalDamage' THEN 12
                        WHEN 'Property' THEN 2
                        WHEN 'CommercialAuto' THEN 3
                        WHEN 'BusinessOwners' THEN 4
                        WHEN 'WorkersCompensation' THEN 5
                        WHEN 'ProfessionalLiability' THEN 6
                        WHEN 'Umbrella' THEN 7
                        WHEN 'Cyber' THEN 8
                        WHEN 'ExcessLiability' THEN 9
                        WHEN 'Other' THEN 99
                        ELSE NULL
                    END;

                    IF NEW."ProgramConfigurationId" IS NOT NULL THEN
                        SELECT NOT EXISTS (
                            SELECT 1
                            FROM program_configurations p
                            WHERE p."Id" = NEW."ProgramConfigurationId"
                              AND p."IsActive" = TRUE
                              AND p."IsDeleted" = FALSE
                        ) INTO mismatch_exists;
                        IF mismatch_exists THEN
                            RAISE EXCEPTION 'Fee rule ProgramConfigurationId is not active.';
                        END IF;
                    END IF;

                    IF NEW."ProgramCarrierId" IS NOT NULL THEN
                        SELECT NOT EXISTS (
                            SELECT 1
                            FROM program_carriers pc
                            WHERE pc."Id" = NEW."ProgramCarrierId"
                              AND pc."ProgramConfigurationId" = NEW."ProgramConfigurationId"
                              AND pc."CarrierId" = NEW."CarrierId"
                              AND pc."IsActive" = TRUE
                              AND pc."IsDeleted" = FALSE
                              AND pc."EffectiveDate" <= NEW."EffectiveDate"
                              AND (pc."ExpirationDate" IS NULL OR pc."ExpirationDate" >= NEW."EffectiveDate")
                        ) INTO mismatch_exists;
                        IF mismatch_exists THEN
                            RAISE EXCEPTION 'Fee rule ProgramCarrierId does not match ProgramConfigurationId and CarrierId.';
                        END IF;
                    END IF;

                    IF NEW."ProgramCarrierLineOfBusinessId" IS NOT NULL THEN
                        SELECT NOT EXISTS (
                            SELECT 1
                            FROM program_carrier_lines_of_business pcl
                            INNER JOIN program_carriers pc ON pc."Id" = pcl."ProgramCarrierId"
                            WHERE pcl."Id" = NEW."ProgramCarrierLineOfBusinessId"
                              AND pc."ProgramConfigurationId" = NEW."ProgramConfigurationId"
                              AND pc."CarrierId" = NEW."CarrierId"
                              AND pcl."LineOfBusiness" = lob_value
                              AND pc."IsActive" = TRUE
                              AND pc."IsDeleted" = FALSE
                              AND pcl."IsActive" = TRUE
                              AND pcl."IsDeleted" = FALSE
                              AND pc."EffectiveDate" <= NEW."EffectiveDate"
                              AND (pc."ExpirationDate" IS NULL OR pc."ExpirationDate" >= NEW."EffectiveDate")
                              AND pcl."EffectiveDate" <= NEW."EffectiveDate"
                              AND (pcl."ExpirationDate" IS NULL OR pcl."ExpirationDate" >= NEW."EffectiveDate")
                        ) INTO mismatch_exists;
                        IF mismatch_exists THEN
                            RAISE EXCEPTION 'Fee rule ProgramCarrierLineOfBusinessId does not match Program, Carrier, and LineOfBusiness.';
                        END IF;
                    END IF;

                    IF NEW."ProgramCarrierLobStateId" IS NOT NULL THEN
                        SELECT NOT EXISTS (
                            SELECT 1
                            FROM program_carrier_lob_states pcs
                            INNER JOIN program_carrier_lines_of_business pcl ON pcl."Id" = pcs."ProgramCarrierLineOfBusinessId"
                            INNER JOIN program_carriers pc ON pc."Id" = pcl."ProgramCarrierId"
                            WHERE pcs."Id" = NEW."ProgramCarrierLobStateId"
                              AND pc."ProgramConfigurationId" = NEW."ProgramConfigurationId"
                              AND pc."CarrierId" = NEW."CarrierId"
                              AND pcl."LineOfBusiness" = lob_value
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
                        ) INTO mismatch_exists;
                        IF mismatch_exists THEN
                            RAISE EXCEPTION 'Fee rule ProgramCarrierLobStateId does not match Program, Carrier, LineOfBusiness, and StateCode.';
                        END IF;
                    END IF;

                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_validate_fee_rule_program_scope
                BEFORE INSERT OR UPDATE OF "ProgramConfigurationId", "CarrierId", "LineOfBusiness", "StateCode", "ProgramCarrierId", "ProgramCarrierLineOfBusinessId", "ProgramCarrierLobStateId", "EffectiveDate"
                ON fee_rule_versions
                FOR EACH ROW
                EXECUTE FUNCTION validate_fee_rule_program_scope();

                CREATE OR REPLACE FUNCTION validate_existing_fee_rule_program_scopes()
                RETURNS trigger AS $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM fee_rule_versions v
                        WHERE v."ProgramConfigurationId" IS NOT NULL
                          AND NOT EXISTS (
                              SELECT 1
                              FROM program_configurations p
                              WHERE p."Id" = v."ProgramConfigurationId"
                                AND p."IsActive" = TRUE
                                AND p."IsDeleted" = FALSE
                          )
                    ) THEN
                        RAISE EXCEPTION 'Program setup change would invalidate existing fee rule ProgramConfigurationId.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM fee_rule_versions v
                        WHERE v."ProgramCarrierId" IS NOT NULL
                          AND NOT EXISTS (
                              SELECT 1
                              FROM program_carriers pc
                              WHERE pc."Id" = v."ProgramCarrierId"
                                AND pc."ProgramConfigurationId" = v."ProgramConfigurationId"
                                AND pc."CarrierId" = v."CarrierId"
                                AND pc."IsActive" = TRUE
                                AND pc."IsDeleted" = FALSE
                                AND pc."EffectiveDate" <= v."EffectiveDate"
                                AND (pc."ExpirationDate" IS NULL OR pc."ExpirationDate" >= v."EffectiveDate")
                          )
                    ) THEN
                        RAISE EXCEPTION 'Program setup change would invalidate existing fee rule ProgramCarrierId.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM fee_rule_versions v
                        WHERE v."ProgramCarrierLineOfBusinessId" IS NOT NULL
                          AND NOT EXISTS (
                              SELECT 1
                              FROM program_carrier_lines_of_business pcl
                              INNER JOIN program_carriers pc ON pc."Id" = pcl."ProgramCarrierId"
                              WHERE pcl."Id" = v."ProgramCarrierLineOfBusinessId"
                                AND pc."ProgramConfigurationId" = v."ProgramConfigurationId"
                                AND pc."CarrierId" = v."CarrierId"
                                AND pcl."LineOfBusiness" = CASE v."LineOfBusiness"
                                      WHEN 'GeneralLiability' THEN 1
                                      WHEN 'InlandMarine' THEN 10
                                      WHEN 'AutoLiability' THEN 11
                                      WHEN 'AutoPhysicalDamage' THEN 12
                                      WHEN 'Property' THEN 2
                                      WHEN 'CommercialAuto' THEN 3
                                      WHEN 'BusinessOwners' THEN 4
                                      WHEN 'WorkersCompensation' THEN 5
                                      WHEN 'ProfessionalLiability' THEN 6
                                      WHEN 'Umbrella' THEN 7
                                      WHEN 'Cyber' THEN 8
                                      WHEN 'ExcessLiability' THEN 9
                                      WHEN 'Other' THEN 99
                                      ELSE -1
                                    END
                                AND pc."IsActive" = TRUE
                                AND pc."IsDeleted" = FALSE
                                AND pcl."IsActive" = TRUE
                                AND pcl."IsDeleted" = FALSE
                                AND pc."EffectiveDate" <= v."EffectiveDate"
                                AND (pc."ExpirationDate" IS NULL OR pc."ExpirationDate" >= v."EffectiveDate")
                                AND pcl."EffectiveDate" <= v."EffectiveDate"
                                AND (pcl."ExpirationDate" IS NULL OR pcl."ExpirationDate" >= v."EffectiveDate")
                          )
                    ) THEN
                        RAISE EXCEPTION 'Program setup change would invalidate existing fee rule ProgramCarrierLineOfBusinessId.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM fee_rule_versions v
                        WHERE v."ProgramCarrierLobStateId" IS NOT NULL
                          AND NOT EXISTS (
                              SELECT 1
                              FROM program_carrier_lob_states pcs
                              INNER JOIN program_carrier_lines_of_business pcl ON pcl."Id" = pcs."ProgramCarrierLineOfBusinessId"
                              INNER JOIN program_carriers pc ON pc."Id" = pcl."ProgramCarrierId"
                              WHERE pcs."Id" = v."ProgramCarrierLobStateId"
                                AND pc."ProgramConfigurationId" = v."ProgramConfigurationId"
                                AND pc."CarrierId" = v."CarrierId"
                                AND pcl."LineOfBusiness" = CASE v."LineOfBusiness"
                                      WHEN 'GeneralLiability' THEN 1
                                      WHEN 'InlandMarine' THEN 10
                                      WHEN 'AutoLiability' THEN 11
                                      WHEN 'AutoPhysicalDamage' THEN 12
                                      WHEN 'Property' THEN 2
                                      WHEN 'CommercialAuto' THEN 3
                                      WHEN 'BusinessOwners' THEN 4
                                      WHEN 'WorkersCompensation' THEN 5
                                      WHEN 'ProfessionalLiability' THEN 6
                                      WHEN 'Umbrella' THEN 7
                                      WHEN 'Cyber' THEN 8
                                      WHEN 'ExcessLiability' THEN 9
                                      WHEN 'Other' THEN 99
                                      ELSE -1
                                    END
                                AND pcs."StateCode" = v."StateCode"
                                AND pc."IsActive" = TRUE
                                AND pc."IsDeleted" = FALSE
                                AND pcl."IsActive" = TRUE
                                AND pcl."IsDeleted" = FALSE
                                AND pcs."IsActive" = TRUE
                                AND pcs."IsDeleted" = FALSE
                                AND pc."EffectiveDate" <= v."EffectiveDate"
                                AND (pc."ExpirationDate" IS NULL OR pc."ExpirationDate" >= v."EffectiveDate")
                                AND pcl."EffectiveDate" <= v."EffectiveDate"
                                AND (pcl."ExpirationDate" IS NULL OR pcl."ExpirationDate" >= v."EffectiveDate")
                                AND pcs."EffectiveDate" <= v."EffectiveDate"
                                AND (pcs."ExpirationDate" IS NULL OR pcs."ExpirationDate" >= v."EffectiveDate")
                          )
                    ) THEN
                        RAISE EXCEPTION 'Program setup change would invalidate existing fee rule ProgramCarrierLobStateId.';
                    END IF;

                    RETURN NULL;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_validate_fee_rules_after_program_configuration_change
                AFTER UPDATE OF "IsActive", "IsDeleted"
                ON program_configurations
                FOR EACH STATEMENT
                EXECUTE FUNCTION validate_existing_fee_rule_program_scopes();

                CREATE TRIGGER trg_validate_fee_rules_after_program_carrier_change
                AFTER UPDATE OF "ProgramConfigurationId", "CarrierId", "IsActive", "IsDeleted", "EffectiveDate", "ExpirationDate"
                ON program_carriers
                FOR EACH STATEMENT
                EXECUTE FUNCTION validate_existing_fee_rule_program_scopes();

                CREATE TRIGGER trg_validate_fee_rules_after_program_lob_change
                AFTER UPDATE OF "ProgramCarrierId", "LineOfBusiness", "IsActive", "IsDeleted", "EffectiveDate", "ExpirationDate"
                ON program_carrier_lines_of_business
                FOR EACH STATEMENT
                EXECUTE FUNCTION validate_existing_fee_rule_program_scopes();

                CREATE TRIGGER trg_validate_fee_rules_after_program_state_change
                AFTER UPDATE OF "ProgramCarrierLineOfBusinessId", "StateCode", "IsActive", "IsDeleted", "EffectiveDate", "ExpirationDate"
                ON program_carrier_lob_states
                FOR EACH STATEMENT
                EXECUTE FUNCTION validate_existing_fee_rule_program_scopes();
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_fee_rule_program_scope_canonical",
                table: "fee_rule_versions",
                sql: "(\n    \"ProgramConfigurationId\" IS NULL\n    AND \"ProgramCarrierId\" IS NULL\n    AND \"ProgramCarrierLineOfBusinessId\" IS NULL\n    AND \"ProgramCarrierLobStateId\" IS NULL\n)\nOR (\n    \"ProgramConfigurationId\" IS NOT NULL\n    AND \"CarrierId\" IS NULL\n    AND \"LineOfBusiness\" IS NULL\n    AND \"StateCode\" IS NULL\n    AND \"ProgramCarrierId\" IS NULL\n    AND \"ProgramCarrierLineOfBusinessId\" IS NULL\n    AND \"ProgramCarrierLobStateId\" IS NULL\n)\nOR (\n    \"ProgramConfigurationId\" IS NOT NULL\n    AND \"CarrierId\" IS NOT NULL\n    AND \"LineOfBusiness\" IS NULL\n    AND \"StateCode\" IS NULL\n    AND \"ProgramCarrierId\" IS NOT NULL\n    AND \"ProgramCarrierLineOfBusinessId\" IS NULL\n    AND \"ProgramCarrierLobStateId\" IS NULL\n)\nOR (\n    \"ProgramConfigurationId\" IS NOT NULL\n    AND \"CarrierId\" IS NOT NULL\n    AND \"LineOfBusiness\" IS NOT NULL\n    AND \"StateCode\" IS NULL\n    AND \"ProgramCarrierId\" IS NULL\n    AND \"ProgramCarrierLineOfBusinessId\" IS NOT NULL\n    AND \"ProgramCarrierLobStateId\" IS NULL\n)\nOR (\n    \"ProgramConfigurationId\" IS NOT NULL\n    AND \"CarrierId\" IS NOT NULL\n    AND \"LineOfBusiness\" IS NOT NULL\n    AND \"StateCode\" IS NOT NULL\n    AND \"ProgramCarrierId\" IS NULL\n    AND \"ProgramCarrierLineOfBusinessId\" IS NULL\n    AND \"ProgramCarrierLobStateId\" IS NOT NULL\n)");

            migrationBuilder.AddForeignKey(
                name: "FK_fee_rule_versions_program_carrier_lines_of_business_Program~",
                table: "fee_rule_versions",
                column: "ProgramCarrierLineOfBusinessId",
                principalTable: "program_carrier_lines_of_business",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_fee_rule_versions_program_carrier_lob_states_ProgramCarrier~",
                table: "fee_rule_versions",
                column: "ProgramCarrierLobStateId",
                principalTable: "program_carrier_lob_states",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_fee_rule_versions_program_carriers_ProgramCarrierId",
                table: "fee_rule_versions",
                column: "ProgramCarrierId",
                principalTable: "program_carriers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS trg_validate_fee_rules_after_program_state_change ON program_carrier_lob_states;
                DROP TRIGGER IF EXISTS trg_validate_fee_rules_after_program_lob_change ON program_carrier_lines_of_business;
                DROP TRIGGER IF EXISTS trg_validate_fee_rules_after_program_carrier_change ON program_carriers;
                DROP TRIGGER IF EXISTS trg_validate_fee_rules_after_program_configuration_change ON program_configurations;
                DROP TRIGGER IF EXISTS trg_validate_fee_rule_program_scope ON fee_rule_versions;
                DROP FUNCTION IF EXISTS validate_existing_fee_rule_program_scopes();
                DROP FUNCTION IF EXISTS validate_fee_rule_program_scope();
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_fee_rule_versions_program_carrier_lines_of_business_Program~",
                table: "fee_rule_versions");

            migrationBuilder.DropForeignKey(
                name: "FK_fee_rule_versions_program_carrier_lob_states_ProgramCarrier~",
                table: "fee_rule_versions");

            migrationBuilder.DropForeignKey(
                name: "FK_fee_rule_versions_program_carriers_ProgramCarrierId",
                table: "fee_rule_versions");

            migrationBuilder.DropIndex(
                name: "ix_fee_rule_program_carrier_scope",
                table: "fee_rule_versions");

            migrationBuilder.DropIndex(
                name: "ix_fee_rule_program_lob_scope",
                table: "fee_rule_versions");

            migrationBuilder.DropIndex(
                name: "ix_fee_rule_program_state_scope",
                table: "fee_rule_versions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_fee_rule_program_scope_canonical",
                table: "fee_rule_versions");

            migrationBuilder.DropColumn(
                name: "ProgramCarrierId",
                table: "fee_rule_versions");

            migrationBuilder.DropColumn(
                name: "ProgramCarrierLineOfBusinessId",
                table: "fee_rule_versions");

            migrationBuilder.DropColumn(
                name: "ProgramCarrierLobStateId",
                table: "fee_rule_versions");
        }
    }
}
