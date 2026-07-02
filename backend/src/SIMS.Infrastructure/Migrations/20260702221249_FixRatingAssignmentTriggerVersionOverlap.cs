using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <summary>
    /// Companion to the service-layer fix "allow rating assignment when program path
    /// starts after version inception" (F3): the DB trigger
    /// validate_carrier_rating_assignment_program_scope still enforced the old
    /// point-in-time rule (path active AS OF the version's effective date), so the
    /// service approved the assignment and the trigger then raised P0001 → 500.
    /// Replace the function with range-OVERLAP semantics, mirroring
    /// CarrierRatingAssignmentService.ResolveProgramCarrierLobPathAsync.
    /// </summary>
    public partial class FixRatingAssignmentTriggerVersionOverlap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION validate_carrier_rating_assignment_program_scope()
                RETURNS trigger AS $$
                DECLARE
                    version_effective_date date;
                    version_expiration_date date;
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

                    SELECT v.effective_date, v.expiration_date
                    INTO version_effective_date, version_expiration_date
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
                          AND (version_expiration_date IS NULL OR pc."EffectiveDate" <= version_expiration_date)
                          AND (pc."ExpirationDate" IS NULL OR pc."ExpirationDate" >= version_effective_date)
                          AND (version_expiration_date IS NULL OR pcl."EffectiveDate" <= version_expiration_date)
                          AND (pcl."ExpirationDate" IS NULL OR pcl."ExpirationDate" >= version_effective_date)
                    ) THEN
                        RAISE EXCEPTION 'Carrier rating assignment ProgramCarrierLineOfBusinessId does not match Program, Carrier, LineOfBusiness within the rating version effective range.';
                    END IF;

                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
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
                """);
        }
    }
}
