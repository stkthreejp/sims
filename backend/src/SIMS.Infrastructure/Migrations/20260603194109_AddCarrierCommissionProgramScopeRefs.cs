using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCarrierCommissionProgramScopeRefs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProgramCarrierId",
                table: "carrier_commissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProgramCarrierLineOfBusinessId",
                table: "carrier_commissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE carrier_commissions
                SET "LineOfBusiness" = NULLIF(TRIM("LineOfBusiness"), '')
                WHERE "LineOfBusiness" IS NOT NULL;

                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM carrier_commissions c
                        WHERE c."ProgramConfigurationId" IS NOT NULL
                          AND NOT EXISTS (
                              SELECT 1
                              FROM program_configurations p
                              WHERE p."Id" = c."ProgramConfigurationId"
                                AND p."IsActive" = TRUE
                                AND p."IsDeleted" = FALSE
                          )
                    ) THEN
                        RAISE EXCEPTION 'Cannot add carrier commission Program SOT constraint: at least one Program-scoped carrier commission references an inactive or deleted Program.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM carrier_commissions c
                        WHERE c."ProgramConfigurationId" IS NOT NULL
                          AND c."LineOfBusiness" IS NOT NULL
                          AND c."LineOfBusiness" NOT IN (
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
                        RAISE EXCEPTION 'Cannot add carrier commission Program SOT constraint: at least one Program-scoped carrier commission has an unsupported LineOfBusiness value.';
                    END IF;
                END $$;

                UPDATE carrier_commissions c
                SET "ProgramCarrierId" = pc."Id"
                FROM program_carriers pc
                WHERE c."ProgramConfigurationId" IS NOT NULL
                  AND c."LineOfBusiness" IS NULL
                  AND pc."ProgramConfigurationId" = c."ProgramConfigurationId"
                  AND pc."CarrierId" = c."CarrierId"
                  AND pc."IsActive" = TRUE
                  AND pc."IsDeleted" = FALSE
                  AND pc."EffectiveDate" <= c."EffectiveDate"
                  AND (pc."ExpirationDate" IS NULL OR pc."ExpirationDate" >= c."EffectiveDate");

                UPDATE carrier_commissions c
                SET "ProgramCarrierLineOfBusinessId" = pcl."Id"
                FROM program_carrier_lines_of_business pcl
                INNER JOIN program_carriers pc ON pc."Id" = pcl."ProgramCarrierId"
                WHERE c."ProgramConfigurationId" IS NOT NULL
                  AND c."LineOfBusiness" IS NOT NULL
                  AND pc."ProgramConfigurationId" = c."ProgramConfigurationId"
                  AND pc."CarrierId" = c."CarrierId"
                  AND pcl."LineOfBusiness" = CASE c."LineOfBusiness"
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
                  AND pc."EffectiveDate" <= c."EffectiveDate"
                  AND (pc."ExpirationDate" IS NULL OR pc."ExpirationDate" >= c."EffectiveDate")
                  AND pcl."EffectiveDate" <= c."EffectiveDate"
                  AND (pcl."ExpirationDate" IS NULL OR pcl."ExpirationDate" >= c."EffectiveDate");

                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM carrier_commissions c
                        WHERE c."ProgramConfigurationId" IS NOT NULL
                          AND c."LineOfBusiness" IS NULL
                          AND c."ProgramCarrierId" IS NULL
                    ) THEN
                        RAISE EXCEPTION 'Cannot add carrier commission Program SOT constraint: a Program/Carrier commission has no matching active ProgramCarrier path.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM carrier_commissions c
                        WHERE c."ProgramConfigurationId" IS NOT NULL
                          AND c."LineOfBusiness" IS NOT NULL
                          AND c."ProgramCarrierLineOfBusinessId" IS NULL
                    ) THEN
                        RAISE EXCEPTION 'Cannot add carrier commission Program SOT constraint: a Program/Carrier/LOB commission has no matching active ProgramCarrierLineOfBusiness path.';
                    END IF;
                END $$;

                CREATE OR REPLACE FUNCTION validate_carrier_commission_program_scope()
                RETURNS trigger AS $$
                BEGIN
                    IF NEW."ProgramConfigurationId" IS NULL THEN
                        IF NEW."ProgramCarrierId" IS NOT NULL OR NEW."ProgramCarrierLineOfBusinessId" IS NOT NULL THEN
                            RAISE EXCEPTION 'Carrier commission without ProgramConfigurationId cannot reference Program setup scope ids.';
                        END IF;

                        RETURN NEW;
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1
                        FROM program_configurations p
                        WHERE p."Id" = NEW."ProgramConfigurationId"
                          AND p."IsActive" = TRUE
                          AND p."IsDeleted" = FALSE
                    ) THEN
                        RAISE EXCEPTION 'Carrier commission ProgramConfigurationId is not active.';
                    END IF;

                    IF NEW."LineOfBusiness" IS NULL THEN
                        IF NEW."ProgramCarrierId" IS NULL OR NEW."ProgramCarrierLineOfBusinessId" IS NOT NULL THEN
                            RAISE EXCEPTION 'Program all-lines carrier commission requires ProgramCarrierId only.';
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
                            RAISE EXCEPTION 'Carrier commission ProgramCarrierId does not match ProgramConfigurationId, CarrierId, and EffectiveDate.';
                        END IF;

                        RETURN NEW;
                    END IF;

                    IF NEW."ProgramCarrierLineOfBusinessId" IS NULL OR NEW."ProgramCarrierId" IS NOT NULL THEN
                        RAISE EXCEPTION 'Program LOB carrier commission requires ProgramCarrierLineOfBusinessId only.';
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1
                        FROM program_carrier_lines_of_business pcl
                        INNER JOIN program_carriers pc ON pc."Id" = pcl."ProgramCarrierId"
                        WHERE pcl."Id" = NEW."ProgramCarrierLineOfBusinessId"
                          AND pc."ProgramConfigurationId" = NEW."ProgramConfigurationId"
                          AND pc."CarrierId" = NEW."CarrierId"
                          AND pcl."LineOfBusiness" = CASE NEW."LineOfBusiness"
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
                          AND pc."EffectiveDate" <= NEW."EffectiveDate"
                          AND (pc."ExpirationDate" IS NULL OR pc."ExpirationDate" >= NEW."EffectiveDate")
                          AND pcl."EffectiveDate" <= NEW."EffectiveDate"
                          AND (pcl."ExpirationDate" IS NULL OR pcl."ExpirationDate" >= NEW."EffectiveDate")
                    ) THEN
                        RAISE EXCEPTION 'Carrier commission ProgramCarrierLineOfBusinessId does not match Program, Carrier, LineOfBusiness, and EffectiveDate.';
                    END IF;

                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_validate_carrier_commission_program_scope
                BEFORE INSERT OR UPDATE OF "ProgramConfigurationId", "CarrierId", "LineOfBusiness", "EffectiveDate", "ProgramCarrierId", "ProgramCarrierLineOfBusinessId"
                ON carrier_commissions
                FOR EACH ROW
                EXECUTE FUNCTION validate_carrier_commission_program_scope();

                CREATE OR REPLACE FUNCTION validate_existing_carrier_commission_program_scopes()
                RETURNS trigger AS $$
                BEGIN
                    IF TG_TABLE_NAME = 'program_carriers' THEN
                        IF EXISTS (
                            SELECT 1
                            FROM carrier_commissions c
                            WHERE c."ProgramCarrierId" = NEW."Id"
                              AND (c."ProgramConfigurationId" <> NEW."ProgramConfigurationId" OR c."CarrierId" <> NEW."CarrierId")
                        ) THEN
                            RAISE EXCEPTION 'Program setup change would invalidate existing carrier commission ProgramCarrierId.';
                        END IF;

                        IF EXISTS (
                            SELECT 1
                            FROM carrier_commissions c
                            INNER JOIN program_carrier_lines_of_business pcl ON pcl."Id" = c."ProgramCarrierLineOfBusinessId"
                            WHERE pcl."ProgramCarrierId" = NEW."Id"
                              AND (c."ProgramConfigurationId" <> NEW."ProgramConfigurationId" OR c."CarrierId" <> NEW."CarrierId")
                        ) THEN
                            RAISE EXCEPTION 'Program setup change would invalidate existing carrier commission ProgramCarrierLineOfBusinessId.';
                        END IF;
                    END IF;

                    IF TG_TABLE_NAME = 'program_carrier_lines_of_business' THEN
                        IF EXISTS (
                            SELECT 1
                            FROM carrier_commissions c
                            INNER JOIN program_carriers pc ON pc."Id" = NEW."ProgramCarrierId"
                            WHERE c."ProgramCarrierLineOfBusinessId" = NEW."Id"
                              AND (
                                  CASE c."LineOfBusiness"
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
                                  END <> NEW."LineOfBusiness"
                                  OR c."ProgramConfigurationId" <> pc."ProgramConfigurationId"
                                  OR c."CarrierId" <> pc."CarrierId"
                              )
                        ) THEN
                            RAISE EXCEPTION 'Program setup change would invalidate existing carrier commission ProgramCarrierLineOfBusinessId.';
                        END IF;
                    END IF;

                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_validate_carrier_commissions_after_program_carrier_change
                AFTER UPDATE OF "ProgramConfigurationId", "CarrierId"
                ON program_carriers
                FOR EACH ROW
                EXECUTE FUNCTION validate_existing_carrier_commission_program_scopes();

                CREATE TRIGGER trg_validate_carrier_commissions_after_program_lob_change
                AFTER UPDATE OF "ProgramCarrierId", "LineOfBusiness"
                ON program_carrier_lines_of_business
                FOR EACH ROW
                EXECUTE FUNCTION validate_existing_carrier_commission_program_scopes();
                """);

            migrationBuilder.CreateIndex(
                name: "ix_carrier_commission_program_carrier_scope",
                table: "carrier_commissions",
                column: "ProgramCarrierId");

            migrationBuilder.CreateIndex(
                name: "ix_carrier_commission_program_lob_scope",
                table: "carrier_commissions",
                column: "ProgramCarrierLineOfBusinessId");

            migrationBuilder.AddCheckConstraint(
                name: "ck_carrier_commission_program_scope_canonical",
                table: "carrier_commissions",
                sql: "(\n    \"ProgramConfigurationId\" IS NULL\n    AND \"ProgramCarrierId\" IS NULL\n    AND \"ProgramCarrierLineOfBusinessId\" IS NULL\n)\nOR (\n    \"ProgramConfigurationId\" IS NOT NULL\n    AND \"LineOfBusiness\" IS NULL\n    AND \"ProgramCarrierId\" IS NOT NULL\n    AND \"ProgramCarrierLineOfBusinessId\" IS NULL\n)\nOR (\n    \"ProgramConfigurationId\" IS NOT NULL\n    AND \"LineOfBusiness\" IS NOT NULL\n    AND \"ProgramCarrierId\" IS NULL\n    AND \"ProgramCarrierLineOfBusinessId\" IS NOT NULL\n)");

            migrationBuilder.AddForeignKey(
                name: "FK_carrier_commissions_program_carrier_lines_of_business_Progr~",
                table: "carrier_commissions",
                column: "ProgramCarrierLineOfBusinessId",
                principalTable: "program_carrier_lines_of_business",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_carrier_commissions_program_carriers_ProgramCarrierId",
                table: "carrier_commissions",
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
                DROP TRIGGER IF EXISTS trg_validate_carrier_commissions_after_program_lob_change ON program_carrier_lines_of_business;
                DROP TRIGGER IF EXISTS trg_validate_carrier_commissions_after_program_carrier_change ON program_carriers;
                DROP TRIGGER IF EXISTS trg_validate_carrier_commission_program_scope ON carrier_commissions;
                DROP FUNCTION IF EXISTS validate_existing_carrier_commission_program_scopes();
                DROP FUNCTION IF EXISTS validate_carrier_commission_program_scope();
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_carrier_commissions_program_carrier_lines_of_business_Progr~",
                table: "carrier_commissions");

            migrationBuilder.DropForeignKey(
                name: "FK_carrier_commissions_program_carriers_ProgramCarrierId",
                table: "carrier_commissions");

            migrationBuilder.DropIndex(
                name: "ix_carrier_commission_program_carrier_scope",
                table: "carrier_commissions");

            migrationBuilder.DropIndex(
                name: "ix_carrier_commission_program_lob_scope",
                table: "carrier_commissions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_carrier_commission_program_scope_canonical",
                table: "carrier_commissions");

            migrationBuilder.DropColumn(
                name: "ProgramCarrierId",
                table: "carrier_commissions");

            migrationBuilder.DropColumn(
                name: "ProgramCarrierLineOfBusinessId",
                table: "carrier_commissions");
        }
    }
}
