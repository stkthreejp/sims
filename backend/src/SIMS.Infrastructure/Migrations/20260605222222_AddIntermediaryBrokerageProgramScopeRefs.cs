using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIntermediaryBrokerageProgramScopeRefs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProgramCarrierId",
                table: "intermediary_program_carrier_lob_setups",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProgramCarrierLineOfBusinessId",
                table: "intermediary_program_carrier_lob_setups",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM intermediary_program_carrier_lob_setups s
                        WHERE NOT s."IsDeleted"
                          AND NOT EXISTS (
                              SELECT 1
                              FROM program_configurations p
                              WHERE p."Id" = s."ProgramConfigurationId"
                                AND p."IsActive" = TRUE
                                AND p."IsDeleted" = FALSE
                          )
                    ) THEN
                        RAISE EXCEPTION 'Cannot add intermediary brokerage Program SOT constraint: at least one setup references an inactive or deleted Program.';
                    END IF;
                END $$;

                UPDATE intermediary_program_carrier_lob_setups s
                SET "ProgramCarrierId" = pc."Id"
                FROM program_carriers pc
                WHERE s."LineOfBusiness" IS NULL
                  AND pc."ProgramConfigurationId" = s."ProgramConfigurationId"
                  AND pc."CarrierId" = s."CarrierId"
                  AND pc."IsActive" = TRUE
                  AND pc."IsDeleted" = FALSE
                  AND pc."EffectiveDate" <= s."EffectiveDate"
                  AND (pc."ExpirationDate" IS NULL OR pc."ExpirationDate" >= s."EffectiveDate");

                UPDATE intermediary_program_carrier_lob_setups s
                SET "ProgramCarrierLineOfBusinessId" = pcl."Id"
                FROM program_carrier_lines_of_business pcl
                INNER JOIN program_carriers pc ON pc."Id" = pcl."ProgramCarrierId"
                WHERE s."LineOfBusiness" IS NOT NULL
                  AND pc."ProgramConfigurationId" = s."ProgramConfigurationId"
                  AND pc."CarrierId" = s."CarrierId"
                  AND pcl."LineOfBusiness" = s."LineOfBusiness"
                  AND pc."IsActive" = TRUE
                  AND pc."IsDeleted" = FALSE
                  AND pcl."IsActive" = TRUE
                  AND pcl."IsDeleted" = FALSE
                  AND pc."EffectiveDate" <= s."EffectiveDate"
                  AND (pc."ExpirationDate" IS NULL OR pc."ExpirationDate" >= s."EffectiveDate")
                  AND pcl."EffectiveDate" <= s."EffectiveDate"
                  AND (pcl."ExpirationDate" IS NULL OR pcl."ExpirationDate" >= s."EffectiveDate");

                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM intermediary_program_carrier_lob_setups s
                        WHERE NOT s."IsDeleted"
                          AND s."LineOfBusiness" IS NULL
                          AND s."ProgramCarrierId" IS NULL
                    ) THEN
                        RAISE EXCEPTION 'Cannot add intermediary brokerage Program SOT constraint: a Program/Carrier brokerage setup has no matching active ProgramCarrier path.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM intermediary_program_carrier_lob_setups s
                        WHERE NOT s."IsDeleted"
                          AND s."LineOfBusiness" IS NOT NULL
                          AND s."ProgramCarrierLineOfBusinessId" IS NULL
                    ) THEN
                        RAISE EXCEPTION 'Cannot add intermediary brokerage Program SOT constraint: a Program/Carrier/LOB brokerage setup has no matching active ProgramCarrierLineOfBusiness path.';
                    END IF;
                END $$;

                CREATE OR REPLACE FUNCTION validate_intermediary_brokerage_program_scope()
                RETURNS trigger AS $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM program_configurations p
                        WHERE p."Id" = NEW."ProgramConfigurationId"
                          AND p."IsActive" = TRUE
                          AND p."IsDeleted" = FALSE
                    ) THEN
                        RAISE EXCEPTION 'Intermediary brokerage ProgramConfigurationId is not active.';
                    END IF;

                    IF NEW."LineOfBusiness" IS NULL THEN
                        IF NEW."ProgramCarrierId" IS NULL OR NEW."ProgramCarrierLineOfBusinessId" IS NOT NULL THEN
                            RAISE EXCEPTION 'Program all-lines intermediary brokerage setup requires ProgramCarrierId only.';
                        END IF;

                        IF NOT EXISTS (
                            SELECT 1
                            FROM program_carriers pc
                            WHERE pc."Id" = NEW."ProgramCarrierId"
                              AND pc."ProgramConfigurationId" = NEW."ProgramConfigurationId"
                              AND pc."CarrierId" = NEW."CarrierId"
                              AND pc."IsActive" = TRUE
                              AND pc."IsDeleted" = FALSE
                              AND pc."EffectiveDate" <= NEW."EffectiveDate"
                              AND (pc."ExpirationDate" IS NULL OR pc."ExpirationDate" >= NEW."EffectiveDate")
                        ) THEN
                            RAISE EXCEPTION 'Intermediary brokerage ProgramCarrierId does not match ProgramConfigurationId, CarrierId, and EffectiveDate.';
                        END IF;

                        RETURN NEW;
                    END IF;

                    IF NEW."ProgramCarrierLineOfBusinessId" IS NULL OR NEW."ProgramCarrierId" IS NOT NULL THEN
                        RAISE EXCEPTION 'Program LOB intermediary brokerage setup requires ProgramCarrierLineOfBusinessId only.';
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
                          AND pc."EffectiveDate" <= NEW."EffectiveDate"
                          AND (pc."ExpirationDate" IS NULL OR pc."ExpirationDate" >= NEW."EffectiveDate")
                          AND pcl."EffectiveDate" <= NEW."EffectiveDate"
                          AND (pcl."ExpirationDate" IS NULL OR pcl."ExpirationDate" >= NEW."EffectiveDate")
                    ) THEN
                        RAISE EXCEPTION 'Intermediary brokerage ProgramCarrierLineOfBusinessId does not match Program, Carrier, LineOfBusiness, and EffectiveDate.';
                    END IF;

                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_validate_intermediary_brokerage_program_scope
                BEFORE INSERT OR UPDATE OF "ProgramConfigurationId", "CarrierId", "LineOfBusiness", "EffectiveDate", "ProgramCarrierId", "ProgramCarrierLineOfBusinessId"
                ON intermediary_program_carrier_lob_setups
                FOR EACH ROW
                EXECUTE FUNCTION validate_intermediary_brokerage_program_scope();

                CREATE OR REPLACE FUNCTION validate_existing_intermediary_brokerage_program_scopes()
                RETURNS trigger AS $$
                BEGIN
                    IF TG_TABLE_NAME = 'program_carriers' THEN
                        IF EXISTS (
                            SELECT 1
                            FROM intermediary_program_carrier_lob_setups s
                            WHERE s."ProgramCarrierId" = NEW."Id"
                              AND (s."ProgramConfigurationId" <> NEW."ProgramConfigurationId" OR s."CarrierId" <> NEW."CarrierId")
                        ) THEN
                            RAISE EXCEPTION 'Program setup change would invalidate existing intermediary brokerage ProgramCarrierId.';
                        END IF;

                        IF EXISTS (
                            SELECT 1
                            FROM intermediary_program_carrier_lob_setups s
                            INNER JOIN program_carrier_lines_of_business pcl ON pcl."Id" = s."ProgramCarrierLineOfBusinessId"
                            WHERE pcl."ProgramCarrierId" = NEW."Id"
                              AND (s."ProgramConfigurationId" <> NEW."ProgramConfigurationId" OR s."CarrierId" <> NEW."CarrierId")
                        ) THEN
                            RAISE EXCEPTION 'Program setup change would invalidate existing intermediary brokerage ProgramCarrierLineOfBusinessId.';
                        END IF;
                    END IF;

                    IF TG_TABLE_NAME = 'program_carrier_lines_of_business' THEN
                        IF EXISTS (
                            SELECT 1
                            FROM intermediary_program_carrier_lob_setups s
                            INNER JOIN program_carriers pc ON pc."Id" = NEW."ProgramCarrierId"
                            WHERE s."ProgramCarrierLineOfBusinessId" = NEW."Id"
                              AND (
                                  s."LineOfBusiness" <> NEW."LineOfBusiness"
                                  OR s."ProgramConfigurationId" <> pc."ProgramConfigurationId"
                                  OR s."CarrierId" <> pc."CarrierId"
                              )
                        ) THEN
                            RAISE EXCEPTION 'Program setup change would invalidate existing intermediary brokerage ProgramCarrierLineOfBusinessId.';
                        END IF;
                    END IF;

                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_validate_intermediary_brokerage_after_program_carrier_change
                AFTER UPDATE OF "ProgramConfigurationId", "CarrierId"
                ON program_carriers
                FOR EACH ROW
                EXECUTE FUNCTION validate_existing_intermediary_brokerage_program_scopes();

                CREATE TRIGGER trg_validate_intermediary_brokerage_after_program_lob_change
                AFTER UPDATE OF "ProgramCarrierId", "LineOfBusiness"
                ON program_carrier_lines_of_business
                FOR EACH ROW
                EXECUTE FUNCTION validate_existing_intermediary_brokerage_program_scopes();
                """);

            migrationBuilder.CreateIndex(
                name: "ix_intermediary_brokerage_program_carrier_scope",
                table: "intermediary_program_carrier_lob_setups",
                column: "ProgramCarrierId");

            migrationBuilder.CreateIndex(
                name: "ix_intermediary_brokerage_program_lob_scope",
                table: "intermediary_program_carrier_lob_setups",
                column: "ProgramCarrierLineOfBusinessId");

            migrationBuilder.AddCheckConstraint(
                name: "ck_intermediary_brokerage_program_scope_canonical",
                table: "intermediary_program_carrier_lob_setups",
                sql: "(\n    \"LineOfBusiness\" IS NULL\n    AND \"ProgramCarrierId\" IS NOT NULL\n    AND \"ProgramCarrierLineOfBusinessId\" IS NULL\n)\nOR (\n    \"LineOfBusiness\" IS NOT NULL\n    AND \"ProgramCarrierId\" IS NULL\n    AND \"ProgramCarrierLineOfBusinessId\" IS NOT NULL\n)");

            migrationBuilder.AddForeignKey(
                name: "FK_intermediary_program_carrier_lob_setups_program_carrier_lin~",
                table: "intermediary_program_carrier_lob_setups",
                column: "ProgramCarrierLineOfBusinessId",
                principalTable: "program_carrier_lines_of_business",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_intermediary_program_carrier_lob_setups_program_carriers_Pr~",
                table: "intermediary_program_carrier_lob_setups",
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
                DROP TRIGGER IF EXISTS trg_validate_intermediary_brokerage_after_program_lob_change ON program_carrier_lines_of_business;
                DROP TRIGGER IF EXISTS trg_validate_intermediary_brokerage_after_program_carrier_change ON program_carriers;
                DROP TRIGGER IF EXISTS trg_validate_intermediary_brokerage_program_scope ON intermediary_program_carrier_lob_setups;
                DROP FUNCTION IF EXISTS validate_existing_intermediary_brokerage_program_scopes();
                DROP FUNCTION IF EXISTS validate_intermediary_brokerage_program_scope();
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_intermediary_program_carrier_lob_setups_program_carrier_lin~",
                table: "intermediary_program_carrier_lob_setups");

            migrationBuilder.DropForeignKey(
                name: "FK_intermediary_program_carrier_lob_setups_program_carriers_Pr~",
                table: "intermediary_program_carrier_lob_setups");

            migrationBuilder.DropIndex(
                name: "ix_intermediary_brokerage_program_carrier_scope",
                table: "intermediary_program_carrier_lob_setups");

            migrationBuilder.DropIndex(
                name: "ix_intermediary_brokerage_program_lob_scope",
                table: "intermediary_program_carrier_lob_setups");

            migrationBuilder.DropCheckConstraint(
                name: "ck_intermediary_brokerage_program_scope_canonical",
                table: "intermediary_program_carrier_lob_setups");

            migrationBuilder.DropColumn(
                name: "ProgramCarrierId",
                table: "intermediary_program_carrier_lob_setups");

            migrationBuilder.DropColumn(
                name: "ProgramCarrierLineOfBusinessId",
                table: "intermediary_program_carrier_lob_setups");
        }
    }
}
