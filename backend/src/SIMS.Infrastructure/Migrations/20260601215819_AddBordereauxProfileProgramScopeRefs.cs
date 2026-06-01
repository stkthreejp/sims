using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBordereauxProfileProgramScopeRefs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProgramCarrierId",
                table: "bordereaux_profiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProgramCarrierLineOfBusinessId",
                table: "bordereaux_profiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProgramCarrierLobStateId",
                table: "bordereaux_profiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE bordereaux_profiles
                SET "StateCode" = UPPER(TRIM("StateCode"))
                WHERE "StateCode" IS NOT NULL;

                UPDATE bordereaux_profiles p
                SET "ProgramCarrierId" = pc."Id"
                FROM program_carriers pc
                WHERE p."LineOfBusiness" IS NULL
                  AND p."StateCode" IS NULL
                  AND pc."ProgramConfigurationId" = p."ProgramConfigurationId"
                  AND pc."CarrierId" = p."CarrierId"
                  AND pc."IsActive" = TRUE
                  AND pc."IsDeleted" = FALSE
                  AND pc."EffectiveDate" <= CURRENT_DATE
                  AND (pc."ExpirationDate" IS NULL OR pc."ExpirationDate" >= CURRENT_DATE);

                UPDATE bordereaux_profiles p
                SET "ProgramCarrierLineOfBusinessId" = pcl."Id"
                FROM program_carrier_lines_of_business pcl
                INNER JOIN program_carriers pc ON pc."Id" = pcl."ProgramCarrierId"
                WHERE p."LineOfBusiness" IS NOT NULL
                  AND p."StateCode" IS NULL
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

                UPDATE bordereaux_profiles p
                SET "ProgramCarrierLobStateId" = pcs."Id"
                FROM program_carrier_lob_states pcs
                INNER JOIN program_carrier_lines_of_business pcl ON pcl."Id" = pcs."ProgramCarrierLineOfBusinessId"
                INNER JOIN program_carriers pc ON pc."Id" = pcl."ProgramCarrierId"
                WHERE p."LineOfBusiness" IS NOT NULL
                  AND p."StateCode" IS NOT NULL
                  AND pc."ProgramConfigurationId" = p."ProgramConfigurationId"
                  AND pc."CarrierId" = p."CarrierId"
                  AND pcl."LineOfBusiness" = p."LineOfBusiness"
                  AND pcs."StateCode" = p."StateCode"
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
                        FROM bordereaux_profiles p
                        WHERE NOT EXISTS (
                            SELECT 1
                            FROM program_configurations pcfg
                            WHERE pcfg."Id" = p."ProgramConfigurationId"
                              AND pcfg."IsActive" = TRUE
                              AND pcfg."IsDeleted" = FALSE
                        )
                    ) THEN
                        RAISE EXCEPTION 'Cannot add bordereaux profile Program SOT constraint: at least one profile references an inactive or deleted Program.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM bordereaux_profiles p
                        WHERE p."StateCode" IS NOT NULL
                          AND p."LineOfBusiness" IS NULL
                    ) THEN
                        RAISE EXCEPTION 'Cannot add bordereaux profile Program SOT constraint: at least one profile cannot skip LOB before state.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM bordereaux_profiles p
                        WHERE p."LineOfBusiness" IS NULL
                          AND p."StateCode" IS NULL
                          AND p."ProgramCarrierId" IS NULL
                    ) THEN
                        RAISE EXCEPTION 'Cannot add bordereaux profile Program SOT constraint: a Program/Carrier profile has no matching active ProgramCarrier path.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM bordereaux_profiles p
                        WHERE p."LineOfBusiness" IS NOT NULL
                          AND p."StateCode" IS NULL
                          AND p."ProgramCarrierLineOfBusinessId" IS NULL
                    ) THEN
                        RAISE EXCEPTION 'Cannot add bordereaux profile Program SOT constraint: a Program/Carrier/LOB profile has no matching active ProgramCarrierLineOfBusiness path.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM bordereaux_profiles p
                        WHERE p."LineOfBusiness" IS NOT NULL
                          AND p."StateCode" IS NOT NULL
                          AND p."ProgramCarrierLobStateId" IS NULL
                    ) THEN
                        RAISE EXCEPTION 'Cannot add bordereaux profile Program SOT constraint: a Program/Carrier/LOB/State profile has no matching active ProgramCarrierLobState path.';
                    END IF;
                END $$;

                CREATE OR REPLACE FUNCTION validate_bordereaux_profile_program_scope()
                RETURNS trigger AS $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM program_configurations pcfg
                        WHERE pcfg."Id" = NEW."ProgramConfigurationId"
                          AND pcfg."IsActive" = TRUE
                          AND pcfg."IsDeleted" = FALSE
                    ) THEN
                        RAISE EXCEPTION 'Bordereaux profile ProgramConfigurationId is not active.';
                    END IF;

                    IF NEW."ProgramCarrierId" IS NOT NULL THEN
                        IF NOT EXISTS (
                            SELECT 1
                            FROM program_carriers pc
                            WHERE pc."Id" = NEW."ProgramCarrierId"
                              AND pc."ProgramConfigurationId" = NEW."ProgramConfigurationId"
                              AND pc."CarrierId" = NEW."CarrierId"
                              AND NEW."LineOfBusiness" IS NULL
                              AND NEW."StateCode" IS NULL
                              AND pc."IsActive" = TRUE
                              AND pc."IsDeleted" = FALSE
                              AND pc."EffectiveDate" <= CURRENT_DATE
                              AND (pc."ExpirationDate" IS NULL OR pc."ExpirationDate" >= CURRENT_DATE)
                        ) THEN
                            RAISE EXCEPTION 'Bordereaux profile ProgramCarrierId does not match ProgramConfigurationId and CarrierId.';
                        END IF;
                    END IF;

                    IF NEW."ProgramCarrierLineOfBusinessId" IS NOT NULL THEN
                        IF NOT EXISTS (
                            SELECT 1
                            FROM program_carrier_lines_of_business pcl
                            INNER JOIN program_carriers pc ON pc."Id" = pcl."ProgramCarrierId"
                            WHERE pcl."Id" = NEW."ProgramCarrierLineOfBusinessId"
                              AND pc."ProgramConfigurationId" = NEW."ProgramConfigurationId"
                              AND pc."CarrierId" = NEW."CarrierId"
                              AND pcl."LineOfBusiness" = NEW."LineOfBusiness"
                              AND NEW."StateCode" IS NULL
                              AND pc."IsActive" = TRUE
                              AND pc."IsDeleted" = FALSE
                              AND pcl."IsActive" = TRUE
                              AND pcl."IsDeleted" = FALSE
                              AND pc."EffectiveDate" <= CURRENT_DATE
                              AND (pc."ExpirationDate" IS NULL OR pc."ExpirationDate" >= CURRENT_DATE)
                              AND pcl."EffectiveDate" <= CURRENT_DATE
                              AND (pcl."ExpirationDate" IS NULL OR pcl."ExpirationDate" >= CURRENT_DATE)
                        ) THEN
                            RAISE EXCEPTION 'Bordereaux profile ProgramCarrierLineOfBusinessId does not match Program, Carrier, and LineOfBusiness.';
                        END IF;
                    END IF;

                    IF NEW."ProgramCarrierLobStateId" IS NOT NULL THEN
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
                              AND pc."EffectiveDate" <= CURRENT_DATE
                              AND (pc."ExpirationDate" IS NULL OR pc."ExpirationDate" >= CURRENT_DATE)
                              AND pcl."EffectiveDate" <= CURRENT_DATE
                              AND (pcl."ExpirationDate" IS NULL OR pcl."ExpirationDate" >= CURRENT_DATE)
                              AND pcs."EffectiveDate" <= CURRENT_DATE
                              AND (pcs."ExpirationDate" IS NULL OR pcs."ExpirationDate" >= CURRENT_DATE)
                        ) THEN
                            RAISE EXCEPTION 'Bordereaux profile ProgramCarrierLobStateId does not match Program, Carrier, LineOfBusiness, and StateCode.';
                        END IF;
                    END IF;

                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_validate_bordereaux_profile_program_scope
                BEFORE INSERT OR UPDATE OF "ProgramConfigurationId", "CarrierId", "LineOfBusiness", "StateCode", "ProgramCarrierId", "ProgramCarrierLineOfBusinessId", "ProgramCarrierLobStateId"
                ON bordereaux_profiles
                FOR EACH ROW
                EXECUTE FUNCTION validate_bordereaux_profile_program_scope();

                CREATE OR REPLACE FUNCTION validate_existing_bordereaux_profile_program_scopes()
                RETURNS trigger AS $$
                BEGIN
                    IF TG_TABLE_NAME = 'program_carriers' THEN
                        IF EXISTS (
                            SELECT 1
                            FROM bordereaux_profiles p
                            WHERE p."ProgramCarrierId" = NEW."Id"
                              AND (p."ProgramConfigurationId" <> NEW."ProgramConfigurationId" OR p."CarrierId" <> NEW."CarrierId")
                        ) THEN
                            RAISE EXCEPTION 'Program setup change would invalidate existing bordereaux profile ProgramCarrierId.';
                        END IF;

                        IF EXISTS (
                            SELECT 1
                            FROM bordereaux_profiles p
                            INNER JOIN program_carrier_lines_of_business pcl ON pcl."Id" = p."ProgramCarrierLineOfBusinessId"
                            WHERE pcl."ProgramCarrierId" = NEW."Id"
                              AND (p."ProgramConfigurationId" <> NEW."ProgramConfigurationId" OR p."CarrierId" <> NEW."CarrierId")
                        ) THEN
                            RAISE EXCEPTION 'Program setup change would invalidate existing bordereaux profile ProgramCarrierLineOfBusinessId.';
                        END IF;

                        IF EXISTS (
                            SELECT 1
                            FROM bordereaux_profiles p
                            INNER JOIN program_carrier_lob_states pcs ON pcs."Id" = p."ProgramCarrierLobStateId"
                            INNER JOIN program_carrier_lines_of_business pcl ON pcl."Id" = pcs."ProgramCarrierLineOfBusinessId"
                            WHERE pcl."ProgramCarrierId" = NEW."Id"
                              AND (p."ProgramConfigurationId" <> NEW."ProgramConfigurationId" OR p."CarrierId" <> NEW."CarrierId")
                        ) THEN
                            RAISE EXCEPTION 'Program setup change would invalidate existing bordereaux profile ProgramCarrierLobStateId.';
                        END IF;
                    END IF;

                    IF TG_TABLE_NAME = 'program_carrier_lines_of_business' THEN
                        IF EXISTS (
                            SELECT 1
                            FROM bordereaux_profiles p
                            WHERE p."ProgramCarrierLineOfBusinessId" = NEW."Id"
                              AND p."LineOfBusiness" <> NEW."LineOfBusiness"
                        ) THEN
                            RAISE EXCEPTION 'Program setup change would invalidate existing bordereaux profile ProgramCarrierLineOfBusinessId.';
                        END IF;

                        IF EXISTS (
                            SELECT 1
                            FROM bordereaux_profiles p
                            INNER JOIN program_carrier_lob_states pcs ON pcs."Id" = p."ProgramCarrierLobStateId"
                            WHERE pcs."ProgramCarrierLineOfBusinessId" = NEW."Id"
                              AND p."LineOfBusiness" <> NEW."LineOfBusiness"
                        ) THEN
                            RAISE EXCEPTION 'Program setup change would invalidate existing bordereaux profile ProgramCarrierLobStateId.';
                        END IF;
                    END IF;

                    IF TG_TABLE_NAME = 'program_carrier_lob_states' THEN
                        IF EXISTS (
                            SELECT 1
                            FROM bordereaux_profiles p
                            WHERE p."ProgramCarrierLobStateId" = NEW."Id"
                              AND p."StateCode" <> NEW."StateCode"
                        ) THEN
                            RAISE EXCEPTION 'Program setup change would invalidate existing bordereaux profile ProgramCarrierLobStateId.';
                        END IF;
                    END IF;

                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_validate_bordereaux_profiles_after_program_carrier_change
                AFTER UPDATE OF "ProgramConfigurationId", "CarrierId"
                ON program_carriers
                FOR EACH ROW
                EXECUTE FUNCTION validate_existing_bordereaux_profile_program_scopes();

                CREATE TRIGGER trg_validate_bordereaux_profiles_after_program_lob_change
                AFTER UPDATE OF "ProgramCarrierId", "LineOfBusiness"
                ON program_carrier_lines_of_business
                FOR EACH ROW
                EXECUTE FUNCTION validate_existing_bordereaux_profile_program_scopes();

                CREATE TRIGGER trg_validate_bordereaux_profiles_after_program_state_change
                AFTER UPDATE OF "ProgramCarrierLineOfBusinessId", "StateCode"
                ON program_carrier_lob_states
                FOR EACH ROW
                EXECUTE FUNCTION validate_existing_bordereaux_profile_program_scopes();
                """);

            migrationBuilder.CreateIndex(
                name: "ix_bordereaux_profiles_program_carrier_scope",
                table: "bordereaux_profiles",
                column: "ProgramCarrierId");

            migrationBuilder.CreateIndex(
                name: "ix_bordereaux_profiles_program_lob_scope",
                table: "bordereaux_profiles",
                column: "ProgramCarrierLineOfBusinessId");

            migrationBuilder.CreateIndex(
                name: "ix_bordereaux_profiles_program_state_scope",
                table: "bordereaux_profiles",
                column: "ProgramCarrierLobStateId");

            migrationBuilder.AddCheckConstraint(
                name: "ck_bordereaux_profile_program_scope_canonical",
                table: "bordereaux_profiles",
                sql: "(\n    \"ProgramConfigurationId\" IS NOT NULL\n    AND \"CarrierId\" IS NOT NULL\n    AND \"LineOfBusiness\" IS NULL\n    AND \"StateCode\" IS NULL\n    AND \"ProgramCarrierId\" IS NOT NULL\n    AND \"ProgramCarrierLineOfBusinessId\" IS NULL\n    AND \"ProgramCarrierLobStateId\" IS NULL\n)\nOR (\n    \"ProgramConfigurationId\" IS NOT NULL\n    AND \"CarrierId\" IS NOT NULL\n    AND \"LineOfBusiness\" IS NOT NULL\n    AND \"StateCode\" IS NULL\n    AND \"ProgramCarrierId\" IS NULL\n    AND \"ProgramCarrierLineOfBusinessId\" IS NOT NULL\n    AND \"ProgramCarrierLobStateId\" IS NULL\n)\nOR (\n    \"ProgramConfigurationId\" IS NOT NULL\n    AND \"CarrierId\" IS NOT NULL\n    AND \"LineOfBusiness\" IS NOT NULL\n    AND \"StateCode\" IS NOT NULL\n    AND \"ProgramCarrierId\" IS NULL\n    AND \"ProgramCarrierLineOfBusinessId\" IS NULL\n    AND \"ProgramCarrierLobStateId\" IS NOT NULL\n)");

            migrationBuilder.AddForeignKey(
                name: "FK_bordereaux_profiles_program_carrier_lines_of_business_Progr~",
                table: "bordereaux_profiles",
                column: "ProgramCarrierLineOfBusinessId",
                principalTable: "program_carrier_lines_of_business",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_bordereaux_profiles_program_carrier_lob_states_ProgramCarri~",
                table: "bordereaux_profiles",
                column: "ProgramCarrierLobStateId",
                principalTable: "program_carrier_lob_states",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_bordereaux_profiles_program_carriers_ProgramCarrierId",
                table: "bordereaux_profiles",
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
                DROP TRIGGER IF EXISTS trg_validate_bordereaux_profiles_after_program_state_change ON program_carrier_lob_states;
                DROP TRIGGER IF EXISTS trg_validate_bordereaux_profiles_after_program_lob_change ON program_carrier_lines_of_business;
                DROP TRIGGER IF EXISTS trg_validate_bordereaux_profiles_after_program_carrier_change ON program_carriers;
                DROP TRIGGER IF EXISTS trg_validate_bordereaux_profile_program_scope ON bordereaux_profiles;
                DROP FUNCTION IF EXISTS validate_existing_bordereaux_profile_program_scopes();
                DROP FUNCTION IF EXISTS validate_bordereaux_profile_program_scope();
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_bordereaux_profiles_program_carrier_lines_of_business_Progr~",
                table: "bordereaux_profiles");

            migrationBuilder.DropForeignKey(
                name: "FK_bordereaux_profiles_program_carrier_lob_states_ProgramCarri~",
                table: "bordereaux_profiles");

            migrationBuilder.DropForeignKey(
                name: "FK_bordereaux_profiles_program_carriers_ProgramCarrierId",
                table: "bordereaux_profiles");

            migrationBuilder.DropIndex(
                name: "ix_bordereaux_profiles_program_carrier_scope",
                table: "bordereaux_profiles");

            migrationBuilder.DropIndex(
                name: "ix_bordereaux_profiles_program_lob_scope",
                table: "bordereaux_profiles");

            migrationBuilder.DropIndex(
                name: "ix_bordereaux_profiles_program_state_scope",
                table: "bordereaux_profiles");

            migrationBuilder.DropCheckConstraint(
                name: "ck_bordereaux_profile_program_scope_canonical",
                table: "bordereaux_profiles");

            migrationBuilder.DropColumn(
                name: "ProgramCarrierId",
                table: "bordereaux_profiles");

            migrationBuilder.DropColumn(
                name: "ProgramCarrierLineOfBusinessId",
                table: "bordereaux_profiles");

            migrationBuilder.DropColumn(
                name: "ProgramCarrierLobStateId",
                table: "bordereaux_profiles");
        }
    }
}
