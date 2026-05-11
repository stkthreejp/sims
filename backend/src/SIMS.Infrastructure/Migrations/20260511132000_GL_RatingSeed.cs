using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GL_RatingSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                DECLARE
                    v_plan_id    uuid;
                    v_version_id uuid;
                    v_carrier_id uuid;
                    v_now timestamp with time zone := now();
                BEGIN
                    -- ================================================================
                    -- PART 1: Rating Plan + Version (GL v1, LOB=1)
                    -- ================================================================
                    SELECT id INTO v_plan_id FROM rating_plans
                    WHERE line_of_business = 1 AND formula_key = 'GL_v1';

                    IF v_plan_id IS NULL THEN
                        v_plan_id := gen_random_uuid();
                        INSERT INTO rating_plans
                            (id, line_of_business, name, formula_key, status, created_at, updated_at, is_deleted)
                        VALUES
                            (v_plan_id, 1, 'General Liability v1', 'GL_v1', 2, v_now, v_now, false);
                    END IF;

                    SELECT id INTO v_version_id FROM rating_plan_versions
                    WHERE rating_plan_id = v_plan_id AND version_number = 1;

                    IF v_version_id IS NULL THEN
                        v_version_id := gen_random_uuid();
                        INSERT INTO rating_plan_versions
                            (id, rating_plan_id, version_number, effective_date, status,
                             schedule_min, schedule_max, created_at, updated_at, is_deleted)
                        VALUES
                            (v_version_id, v_plan_id, 1, '2026-01-01', 2,
                             0.8000, 1.2000, v_now, v_now, false);
                    END IF;

                    -- ================================================================
                    -- PART 2: Carrier Assignment
                    -- Update existing GL (LOB=1) assignments to use GL_v1.
                    -- If none exist, try to find a carrier named like Brace/Lloyd.
                    -- ================================================================
                    UPDATE carrier_rating_assignments
                    SET rating_plan_version_id = v_version_id,
                        updated_at             = v_now
                    WHERE line_of_business = 1;

                    -- If no assignment was updated, try to create one for Brace/Lloyd carrier
                    IF NOT FOUND THEN
                        EXECUTE format(
                            'SELECT %I FROM carriers WHERE (lower(%I::text) LIKE $1 OR lower(%I::text) LIKE $2) LIMIT 1',
                            (SELECT column_name FROM information_schema.columns WHERE table_name = 'carriers' AND lower(column_name) = 'id'   LIMIT 1),
                            (SELECT column_name FROM information_schema.columns WHERE table_name = 'carriers' AND lower(column_name) = 'name' LIMIT 1),
                            (SELECT column_name FROM information_schema.columns WHERE table_name = 'carriers' AND lower(column_name) = 'name' LIMIT 1)
                        ) INTO v_carrier_id USING '%brace%', '%lloyd%';

                        IF v_carrier_id IS NOT NULL THEN
                            INSERT INTO carrier_rating_assignments
                                (id, carrier_id, line_of_business, rating_plan_version_id, created_at, updated_at, is_deleted)
                            VALUES
                                (gen_random_uuid(), v_carrier_id, 1, v_version_id, v_now, v_now, false);
                        END IF;
                    END IF;

                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                DECLARE
                    v_plan_id    uuid;
                    v_version_id uuid;
                BEGIN
                    SELECT id INTO v_plan_id FROM rating_plans WHERE formula_key = 'GL_v1';
                    IF v_plan_id IS NOT NULL THEN
                        SELECT id INTO v_version_id FROM rating_plan_versions WHERE rating_plan_id = v_plan_id;
                        DELETE FROM carrier_rating_assignments WHERE rating_plan_version_id = v_version_id;
                        DELETE FROM factor_rows   fr USING factor_tables ft WHERE fr.factor_table_id = ft.id AND ft.rating_plan_version_id = v_version_id;
                        DELETE FROM factor_tables WHERE rating_plan_version_id = v_version_id;
                        DELETE FROM rating_plan_versions WHERE rating_plan_id = v_plan_id;
                        DELETE FROM rating_plans WHERE id = v_plan_id;
                    END IF;
                END $$;
            ");
        }
    }
}
