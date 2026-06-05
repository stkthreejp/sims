using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCarrierRatingAssignmentProgramScopeRefs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "program_carrier_line_of_business_id",
                table: "carrier_rating_assignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE carrier_rating_assignments a
                SET program_carrier_line_of_business_id = pcl."Id"
                FROM program_carrier_lines_of_business pcl
                INNER JOIN program_carriers pc ON pc."Id" = pcl."ProgramCarrierId"
                INNER JOIN rating_plan_versions v ON TRUE
                WHERE a.program_configuration_id IS NOT NULL
                  AND v.id = a.rating_plan_version_id
                  AND pc."ProgramConfigurationId" = a.program_configuration_id
                  AND pc."CarrierId" = a.carrier_id
                  AND pcl."LineOfBusiness" = a.line_of_business
                  AND pc."IsActive" = TRUE
                  AND pc."IsDeleted" = FALSE
                  AND pcl."IsActive" = TRUE
                  AND pcl."IsDeleted" = FALSE
                  AND pc."EffectiveDate" <= v.effective_date
                  AND (pc."ExpirationDate" IS NULL OR pc."ExpirationDate" >= v.effective_date)
                  AND pcl."EffectiveDate" <= v.effective_date
                  AND (pcl."ExpirationDate" IS NULL OR pcl."ExpirationDate" >= v.effective_date);

                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM carrier_rating_assignments a
                        WHERE a.program_configuration_id IS NOT NULL
                          AND NOT EXISTS (
                              SELECT 1
                              FROM program_configurations p
                              WHERE p."Id" = a.program_configuration_id
                                AND p."IsActive" = TRUE
                                AND p."IsDeleted" = FALSE
                          )
                    ) THEN
                        RAISE EXCEPTION 'Cannot add carrier rating assignment Program SOT constraint: at least one assignment references an inactive or deleted Program.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM carrier_rating_assignments a
                        WHERE a.program_configuration_id IS NOT NULL
                          AND a.program_carrier_line_of_business_id IS NULL
                    ) THEN
                        RAISE EXCEPTION 'Cannot add carrier rating assignment Program SOT constraint: a Program/Carrier/LOB rating assignment has no matching active ProgramCarrierLineOfBusiness path.';
                    END IF;
                END $$;

                CREATE OR REPLACE FUNCTION validate_carrier_rating_assignment_program_scope()
                RETURNS trigger AS $$
                DECLARE
                    version_effective_date date;
                BEGIN
                    IF NEW.program_configuration_id IS NULL THEN
                        IF NEW.program_carrier_line_of_business_id IS NOT NULL THEN
                            RAISE EXCEPTION 'Carrier rating assignment without ProgramConfigurationId cannot reference Program setup scope ids.';
                        END IF;

                        RETURN NEW;
                    END IF;

                    IF NEW.program_carrier_line_of_business_id IS NULL THEN
                        RAISE EXCEPTION 'Program carrier rating assignment requires ProgramCarrierLineOfBusinessId.';
                    END IF;

                    SELECT v.effective_date
                    INTO version_effective_date
                    FROM rating_plan_versions v
                    WHERE v.id = NEW.rating_plan_version_id;

                    IF version_effective_date IS NULL THEN
                        RAISE EXCEPTION 'Carrier rating assignment rating plan version is missing.';
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1
                        FROM program_configurations p
                        WHERE p."Id" = NEW.program_configuration_id
                          AND p."IsActive" = TRUE
                          AND p."IsDeleted" = FALSE
                    ) THEN
                        RAISE EXCEPTION 'Carrier rating assignment ProgramConfigurationId is not active.';
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1
                        FROM program_carrier_lines_of_business pcl
                        INNER JOIN program_carriers pc ON pc."Id" = pcl."ProgramCarrierId"
                        WHERE pcl."Id" = NEW.program_carrier_line_of_business_id
                          AND pc."ProgramConfigurationId" = NEW.program_configuration_id
                          AND pc."CarrierId" = NEW.carrier_id
                          AND pcl."LineOfBusiness" = NEW.line_of_business
                          AND pc."IsActive" = TRUE
                          AND pc."IsDeleted" = FALSE
                          AND pcl."IsActive" = TRUE
                          AND pcl."IsDeleted" = FALSE
                          AND pc."EffectiveDate" <= version_effective_date
                          AND (pc."ExpirationDate" IS NULL OR pc."ExpirationDate" >= version_effective_date)
                          AND pcl."EffectiveDate" <= version_effective_date
                          AND (pcl."ExpirationDate" IS NULL OR pcl."ExpirationDate" >= version_effective_date)
                    ) THEN
                        RAISE EXCEPTION 'Carrier rating assignment ProgramCarrierLineOfBusinessId does not match Program, Carrier, LineOfBusiness, and version EffectiveDate.';
                    END IF;

                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_validate_carrier_rating_assignment_program_scope
                BEFORE INSERT OR UPDATE OF program_configuration_id, carrier_id, line_of_business, rating_plan_version_id, program_carrier_line_of_business_id
                ON carrier_rating_assignments
                FOR EACH ROW
                EXECUTE FUNCTION validate_carrier_rating_assignment_program_scope();

                CREATE OR REPLACE FUNCTION validate_existing_carrier_rating_assignment_program_scopes()
                RETURNS trigger AS $$
                BEGIN
                    IF TG_TABLE_NAME = 'program_carriers' THEN
                        IF EXISTS (
                            SELECT 1
                            FROM carrier_rating_assignments a
                            INNER JOIN program_carrier_lines_of_business pcl ON pcl."Id" = a.program_carrier_line_of_business_id
                            WHERE pcl."ProgramCarrierId" = NEW."Id"
                              AND (a.program_configuration_id <> NEW."ProgramConfigurationId" OR a.carrier_id <> NEW."CarrierId")
                        ) THEN
                            RAISE EXCEPTION 'Program setup change would invalidate existing carrier rating assignment ProgramCarrierLineOfBusinessId.';
                        END IF;
                    END IF;

                    IF TG_TABLE_NAME = 'program_carrier_lines_of_business' THEN
                        IF EXISTS (
                            SELECT 1
                            FROM carrier_rating_assignments a
                            INNER JOIN program_carriers pc ON pc."Id" = NEW."ProgramCarrierId"
                            WHERE a.program_carrier_line_of_business_id = NEW."Id"
                              AND (
                                  a.line_of_business <> NEW."LineOfBusiness"
                                  OR a.program_configuration_id <> pc."ProgramConfigurationId"
                                  OR a.carrier_id <> pc."CarrierId"
                              )
                        ) THEN
                            RAISE EXCEPTION 'Program setup change would invalidate existing carrier rating assignment ProgramCarrierLineOfBusinessId.';
                        END IF;
                    END IF;

                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_validate_carrier_rating_assignments_after_program_carrier_change
                AFTER UPDATE OF "ProgramConfigurationId", "CarrierId"
                ON program_carriers
                FOR EACH ROW
                EXECUTE FUNCTION validate_existing_carrier_rating_assignment_program_scopes();

                CREATE TRIGGER trg_validate_carrier_rating_assignments_after_program_lob_change
                AFTER UPDATE OF "ProgramCarrierId", "LineOfBusiness"
                ON program_carrier_lines_of_business
                FOR EACH ROW
                EXECUTE FUNCTION validate_existing_carrier_rating_assignment_program_scopes();
                """);

            migrationBuilder.CreateIndex(
                name: "ix_carrier_rating_assignment_program_lob_scope",
                table: "carrier_rating_assignments",
                column: "program_carrier_line_of_business_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_carrier_rating_assignment_program_scope_canonical",
                table: "carrier_rating_assignments",
                sql: "(\n    program_configuration_id IS NULL\n    AND program_carrier_line_of_business_id IS NULL\n)\nOR (\n    program_configuration_id IS NOT NULL\n    AND program_carrier_line_of_business_id IS NOT NULL\n)");

            migrationBuilder.AddForeignKey(
                name: "FK_carrier_rating_assignments_program_carrier_lines_of_busines~",
                table: "carrier_rating_assignments",
                column: "program_carrier_line_of_business_id",
                principalTable: "program_carrier_lines_of_business",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS trg_validate_carrier_rating_assignments_after_program_lob_change ON program_carrier_lines_of_business;
                DROP TRIGGER IF EXISTS trg_validate_carrier_rating_assignments_after_program_carrier_change ON program_carriers;
                DROP TRIGGER IF EXISTS trg_validate_carrier_rating_assignment_program_scope ON carrier_rating_assignments;
                DROP FUNCTION IF EXISTS validate_existing_carrier_rating_assignment_program_scopes();
                DROP FUNCTION IF EXISTS validate_carrier_rating_assignment_program_scope();
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_carrier_rating_assignments_program_carrier_lines_of_busines~",
                table: "carrier_rating_assignments");

            migrationBuilder.DropIndex(
                name: "ix_carrier_rating_assignment_program_lob_scope",
                table: "carrier_rating_assignments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_carrier_rating_assignment_program_scope_canonical",
                table: "carrier_rating_assignments");

            migrationBuilder.DropColumn(
                name: "program_carrier_line_of_business_id",
                table: "carrier_rating_assignments");
        }
    }
}
