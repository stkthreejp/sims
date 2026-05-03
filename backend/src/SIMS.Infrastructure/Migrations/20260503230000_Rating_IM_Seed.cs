using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Rating_IM_Seed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                DECLARE
                    v_plan_id uuid;
                    v_version_id uuid;
                    v_ft_base_rate_id uuid;
                    v_ft_deductible_id uuid;
                    v_carrier_id uuid;
                    v_now timestamp with time zone := now();
                BEGIN
                    -- ================================================================
                    -- PART 1: Equipment Types (12 rows)
                    -- ================================================================
                    INSERT INTO equipment_types (id, type_number, name, created_at, updated_at, is_deleted)
                    VALUES
                        (gen_random_uuid(),  1, 'Skidder',       v_now, v_now, false),
                        (gen_random_uuid(),  2, 'Loader',         v_now, v_now, false),
                        (gen_random_uuid(),  3, 'Dozer',          v_now, v_now, false),
                        (gen_random_uuid(),  4, 'Fellerbuncher',  v_now, v_now, false),
                        (gen_random_uuid(),  5, 'Delimber',       v_now, v_now, false),
                        (gen_random_uuid(),  6, 'Chipper',        v_now, v_now, false),
                        (gen_random_uuid(),  7, 'Misc Tools',     v_now, v_now, false),
                        (gen_random_uuid(),  8, 'Yard Equipment', v_now, v_now, false),
                        (gen_random_uuid(),  9, 'Tub Grinder',    v_now, v_now, false),
                        (gen_random_uuid(), 10, 'Excavator',      v_now, v_now, false),
                        (gen_random_uuid(), 11, 'Skid Steer',     v_now, v_now, false),
                        (gen_random_uuid(), 12, 'Grader',         v_now, v_now, false)
                    ON CONFLICT (type_number) DO NOTHING;

                    -- ================================================================
                    -- PART 2: Territories (7 rows)
                    -- ================================================================
                    INSERT INTO territories (id, territory_number, states, modifier, created_at, updated_at, is_deleted)
                    VALUES
                        (gen_random_uuid(), 1, 'AL,AR,FL,GA,LA,MS,OK,SC,TX',                    2.123000, v_now, v_now, false),
                        (gen_random_uuid(), 2, 'KS,KY,MO,NC,NM,TN,VA,WV',                       1.903000, v_now, v_now, false),
                        (gen_random_uuid(), 3, 'CO,CT,DE,IA,IL,IN,MA,MD,ND,NJ,NV,NY,RI,SD,UT', 1.551000, v_now, v_now, false),
                        (gen_random_uuid(), 4, 'MI,MN,WI',                                       1.100000, v_now, v_now, false),
                        (gen_random_uuid(), 5, 'ME,NH,OH,PA,VT',                                 1.397000, v_now, v_now, false),
                        (gen_random_uuid(), 6, 'CA',                                              1.265000, v_now, v_now, false),
                        (gen_random_uuid(), 7, 'AZ,ID,MT,OR,WA,WY',                              1.254000, v_now, v_now, false)
                    ON CONFLICT (territory_number) DO NOTHING;

                    -- ================================================================
                    -- PART 3: Rating Plan + Version (Inland Marine v1)
                    -- ================================================================
                    SELECT id INTO v_plan_id FROM rating_plans
                    WHERE line_of_business = 10 AND name = 'Inland Marine v1';
                    IF v_plan_id IS NULL THEN
                        v_plan_id := gen_random_uuid();
                        INSERT INTO rating_plans (id, line_of_business, name, formula_key, status, created_at, updated_at, is_deleted)
                        VALUES (v_plan_id, 10, 'Inland Marine v1', 'IM_v1', 2, v_now, v_now, false);
                    END IF;

                    SELECT id INTO v_version_id FROM rating_plan_versions
                    WHERE rating_plan_id = v_plan_id AND version_number = 1;
                    IF v_version_id IS NULL THEN
                        v_version_id := gen_random_uuid();
                        INSERT INTO rating_plan_versions (id, rating_plan_id, version_number, effective_date, status, schedule_min, schedule_max, created_at, updated_at, is_deleted)
                        VALUES (v_version_id, v_plan_id, 1, '2026-01-01', 2, 0.7500, 1.2500, v_now, v_now, false);
                    END IF;

                    -- ================================================================
                    -- PART 4: Factor Table — BASE_RATE (RatePer100 = 2)
                    -- Dimensions: equipment_type (int), age_band (string)
                    -- ================================================================
                    SELECT id INTO v_ft_base_rate_id FROM factor_tables
                    WHERE rating_plan_version_id = v_version_id AND code = 'BASE_RATE';
                    IF v_ft_base_rate_id IS NULL THEN
                        v_ft_base_rate_id := gen_random_uuid();
                        INSERT INTO factor_tables (id, rating_plan_version_id, code, dimension_names, value_semantics, created_at, updated_at, is_deleted)
                        VALUES (v_ft_base_rate_id, v_version_id, 'BASE_RATE', '[""equipment_type"",""age_band""]', 2, v_now, v_now, false);

                        INSERT INTO factor_rows (id, factor_table_id, dimension_values, factor, created_at, updated_at, is_deleted)
                        VALUES
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""1"",""age_band"":""1-3""}',  1.140000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""1"",""age_band"":""4-7""}',  1.710000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""1"",""age_band"":""8-11""}', 1.710000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""1"",""age_band"":""12+""}',  2.166000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""2"",""age_band"":""1-3""}',  0.500000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""2"",""age_band"":""4-7""}',  0.500000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""2"",""age_band"":""8-11""}', 0.550000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""2"",""age_band"":""12+""}',  0.600000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""3"",""age_band"":""1-3""}',  0.500000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""3"",""age_band"":""4-7""}',  0.500000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""3"",""age_band"":""8-11""}', 0.550000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""3"",""age_band"":""12+""}',  0.600000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""4"",""age_band"":""1-3""}',  1.140000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""4"",""age_band"":""4-7""}',  1.710000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""4"",""age_band"":""8-11""}', 1.710000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""4"",""age_band"":""12+""}',  2.166000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""5"",""age_band"":""1-3""}',  0.720000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""5"",""age_band"":""4-7""}',  1.080000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""5"",""age_band"":""8-11""}', 1.080000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""5"",""age_band"":""12+""}',  1.296000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""6"",""age_band"":""1-3""}',  1.640000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""6"",""age_band"":""4-7""}',  2.460000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""6"",""age_band"":""8-11""}', 2.460000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""6"",""age_band"":""12+""}',  2.952000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""7"",""age_band"":""1-3""}',  1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""7"",""age_band"":""4-7""}',  1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""7"",""age_band"":""8-11""}', 1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""7"",""age_band"":""12+""}',  1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""8"",""age_band"":""1-3""}',  0.620000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""8"",""age_band"":""4-7""}',  0.650000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""8"",""age_band"":""8-11""}', 0.650000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""8"",""age_band"":""12+""}',  0.700000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""9"",""age_band"":""1-3""}',  1.640000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""9"",""age_band"":""4-7""}',  2.460000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""9"",""age_band"":""8-11""}', 2.460000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""9"",""age_band"":""12+""}',  2.952000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""10"",""age_band"":""1-3""}',  0.520000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""10"",""age_band"":""4-7""}',  0.550000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""10"",""age_band"":""8-11""}', 0.570000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""10"",""age_band"":""12+""}',  0.620000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""11"",""age_band"":""1-3""}',  0.620000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""11"",""age_band"":""4-7""}',  0.650000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""11"",""age_band"":""8-11""}', 0.650000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""11"",""age_band"":""12+""}',  0.700000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""12"",""age_band"":""1-3""}',  0.500000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""12"",""age_band"":""4-7""}',  0.500000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""12"",""age_band"":""8-11""}', 0.550000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_base_rate_id, '{""equipment_type"":""12"",""age_band"":""12+""}',  0.600000, v_now, v_now, false);
                    END IF;

                    -- ================================================================
                    -- PART 5: Factor Table — DEDUCTIBLE_FACTOR (Multiplier = 1)
                    -- Dimensions: equipment_type (int), deductible (string tier)
                    -- ================================================================
                    SELECT id INTO v_ft_deductible_id FROM factor_tables
                    WHERE rating_plan_version_id = v_version_id AND code = 'DEDUCTIBLE_FACTOR';
                    IF v_ft_deductible_id IS NULL THEN
                        v_ft_deductible_id := gen_random_uuid();
                        INSERT INTO factor_tables (id, rating_plan_version_id, code, dimension_names, value_semantics, created_at, updated_at, is_deleted)
                        VALUES (v_ft_deductible_id, v_version_id, 'DEDUCTIBLE_FACTOR', '[""equipment_type"",""deductible""]', 1, v_now, v_now, false);

                        INSERT INTO factor_rows (id, factor_table_id, dimension_values, factor, created_at, updated_at, is_deleted)
                        VALUES
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""1"",""deductible"":""2500""}',    1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""2"",""deductible"":""2500""}',    1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""3"",""deductible"":""2500""}',    1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""4"",""deductible"":""2500""}',    1.020000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""5"",""deductible"":""2500""}',    1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""6"",""deductible"":""2500""}',    0.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""7"",""deductible"":""2500""}',    1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""8"",""deductible"":""2500""}',    1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""9"",""deductible"":""2500""}',    0.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""10"",""deductible"":""2500""}',   1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""11"",""deductible"":""2500""}',   1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""12"",""deductible"":""2500""}',   1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""1"",""deductible"":""5000""}',    0.980000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""2"",""deductible"":""5000""}',    0.980000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""3"",""deductible"":""5000""}',    0.980000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""4"",""deductible"":""5000""}',    1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""5"",""deductible"":""5000""}',    0.980000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""6"",""deductible"":""5000""}',    1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""7"",""deductible"":""5000""}',    0.980000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""8"",""deductible"":""5000""}',    0.980000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""9"",""deductible"":""5000""}',    1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""10"",""deductible"":""5000""}',   0.980000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""11"",""deductible"":""5000""}',   0.980000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""12"",""deductible"":""5000""}',   0.980000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""1"",""deductible"":""10000""}',   0.960000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""2"",""deductible"":""10000""}',   0.960000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""3"",""deductible"":""10000""}',   0.960000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""4"",""deductible"":""10000""}',   0.980000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""5"",""deductible"":""10000""}',   0.960000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""6"",""deductible"":""10000""}',   0.980000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""7"",""deductible"":""10000""}',   0.960000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""8"",""deductible"":""10000""}',   0.960000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""9"",""deductible"":""10000""}',   0.980000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""10"",""deductible"":""10000""}',  0.960000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""11"",""deductible"":""10000""}',  0.960000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""12"",""deductible"":""10000""}',  0.960000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""1"",""deductible"":""25000""}',   0.940000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""2"",""deductible"":""25000""}',   0.940000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""3"",""deductible"":""25000""}',   0.920000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""4"",""deductible"":""25000""}',   0.960000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""5"",""deductible"":""25000""}',   0.940000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""6"",""deductible"":""25000""}',   0.960000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""7"",""deductible"":""25000""}',   0.920000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""8"",""deductible"":""25000""}',   0.920000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""9"",""deductible"":""25000""}',   0.960000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""10"",""deductible"":""25000""}',  0.920000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""11"",""deductible"":""25000""}',  0.920000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""12"",""deductible"":""25000""}',  0.920000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""1"",""deductible"":""10%ACV""}',  0.880000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""2"",""deductible"":""10%ACV""}',  0.880000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""3"",""deductible"":""10%ACV""}',  0.880000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""4"",""deductible"":""10%ACV""}',  0.920000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""5"",""deductible"":""10%ACV""}',  0.880000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""6"",""deductible"":""10%ACV""}',  0.920000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""7"",""deductible"":""10%ACV""}',  0.880000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""8"",""deductible"":""10%ACV""}',  0.880000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""9"",""deductible"":""10%ACV""}',  0.920000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""10"",""deductible"":""10%ACV""}', 0.880000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""11"",""deductible"":""10%ACV""}', 0.880000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_deductible_id, '{""equipment_type"":""12"",""deductible"":""10%ACV""}', 0.880000, v_now, v_now, false);
                    END IF;

                    -- ================================================================
                    -- PART 6: Eligibility Rules (all 12 equipment types accepted)
                    -- ================================================================
                    INSERT INTO eligibility_rules (id, rating_plan_version_id, equipment_type_id, accepted, created_at, updated_at, is_deleted)
                    SELECT gen_random_uuid(), v_version_id, et.id, true, v_now, v_now, false
                    FROM equipment_types et
                    WHERE NOT EXISTS (
                        SELECT 1 FROM eligibility_rules er
                        WHERE er.rating_plan_version_id = v_version_id AND er.equipment_type_id = et.id
                    );

                    -- ================================================================
                    -- PART 7: Carrier Rating Assignment (Beazley + InlandMarine=10)
                    -- ================================================================
                    EXECUTE format(
                        'SELECT %I FROM carriers WHERE lower(%I::text) LIKE $1 LIMIT 1',
                        (SELECT column_name FROM information_schema.columns WHERE table_name = 'carriers' AND lower(column_name) = 'id'   LIMIT 1),
                        (SELECT column_name FROM information_schema.columns WHERE table_name = 'carriers' AND lower(column_name) = 'name' LIMIT 1)
                    ) INTO v_carrier_id USING '%beazley%';

                    IF v_carrier_id IS NOT NULL
                       AND NOT EXISTS (SELECT 1 FROM carrier_rating_assignments
                                       WHERE carrier_id = v_carrier_id AND line_of_business = 10) THEN
                        INSERT INTO carrier_rating_assignments (id, carrier_id, line_of_business, rating_plan_version_id, created_at, updated_at, is_deleted)
                        VALUES (gen_random_uuid(), v_carrier_id, 10, v_version_id, v_now, v_now, false);
                    END IF;
                END$$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM carrier_rating_assignments
                WHERE rating_plan_version_id IN (
                    SELECT rpv.id FROM rating_plan_versions rpv
                    JOIN rating_plans rp ON rp.id = rpv.rating_plan_id
                    WHERE rp.formula_key = 'IM_v1'
                );
                DELETE FROM eligibility_rules WHERE rating_plan_version_id IN (
                    SELECT rpv.id FROM rating_plan_versions rpv
                    JOIN rating_plans rp ON rp.id = rpv.rating_plan_id WHERE rp.formula_key = 'IM_v1'
                );
                DELETE FROM factor_rows WHERE factor_table_id IN (
                    SELECT ft.id FROM factor_tables ft
                    JOIN rating_plan_versions rpv ON rpv.id = ft.rating_plan_version_id
                    JOIN rating_plans rp ON rp.id = rpv.rating_plan_id WHERE rp.formula_key = 'IM_v1'
                );
                DELETE FROM factor_tables WHERE rating_plan_version_id IN (
                    SELECT rpv.id FROM rating_plan_versions rpv
                    JOIN rating_plans rp ON rp.id = rpv.rating_plan_id WHERE rp.formula_key = 'IM_v1'
                );
                DELETE FROM rating_plan_versions WHERE rating_plan_id IN (SELECT id FROM rating_plans WHERE formula_key = 'IM_v1');
                DELETE FROM rating_plans WHERE formula_key = 'IM_v1';
                DELETE FROM equipment_types WHERE type_number BETWEEN 1 AND 12;
                DELETE FROM territories WHERE territory_number BETWEEN 1 AND 7;
            ");
        }
    }
}
