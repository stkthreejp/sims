using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class APD_VehicleColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // All rating table columns were already created as snake_case by prior raw-SQL migrations.
            // Only the new APD vehicle columns need to be added here.
            migrationBuilder.Sql(@"
                ALTER TABLE submission_vehicles
                    ADD COLUMN IF NOT EXISTS apd_vehicle_class      integer,
                    ADD COLUMN IF NOT EXISTS apd_road_type          integer,
                    ADD COLUMN IF NOT EXISTS apd_annual_miles       integer,
                    ADD COLUMN IF NOT EXISTS apd_operation_code     integer,
                    ADD COLUMN IF NOT EXISTS apd_state              varchar(2),
                    ADD COLUMN IF NOT EXISTS apd_stated_value       numeric(18,2),
                    ADD COLUMN IF NOT EXISTS apd_comp_deductible    numeric(18,2),
                    ADD COLUMN IF NOT EXISTS apd_coll_deductible    numeric(18,2),
                    ADD COLUMN IF NOT EXISTS apd_driver_age_code    integer,
                    ADD COLUMN IF NOT EXISTS apd_driver_points_code integer,
                    ADD COLUMN IF NOT EXISTS apd_driver_exp_mod     numeric(5,2);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE submission_vehicles
                    DROP COLUMN IF EXISTS apd_vehicle_class,
                    DROP COLUMN IF EXISTS apd_road_type,
                    DROP COLUMN IF EXISTS apd_annual_miles,
                    DROP COLUMN IF EXISTS apd_operation_code,
                    DROP COLUMN IF EXISTS apd_state,
                    DROP COLUMN IF EXISTS apd_stated_value,
                    DROP COLUMN IF EXISTS apd_comp_deductible,
                    DROP COLUMN IF EXISTS apd_coll_deductible,
                    DROP COLUMN IF EXISTS apd_driver_age_code,
                    DROP COLUMN IF EXISTS apd_driver_points_code,
                    DROP COLUMN IF EXISTS apd_driver_exp_mod;
            ");
        }
    }
}
