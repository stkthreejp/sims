using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class APD_Seed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                DECLARE
                    v_plan_id          uuid;
                    v_version_id       uuid;
                    v_ft_comp_base_id  uuid;
                    v_ft_coll_base_id  uuid;
                    v_ft_mileage_id    uuid;
                    v_ft_driver_id     uuid;
                    v_ft_operation_id  uuid;
                    v_ft_state_id      uuid;
                    v_ft_comp_ded_id   uuid;
                    v_ft_coll_ded_id   uuid;
                    v_carrier_id       uuid;
                    v_now timestamp with time zone := now();
                BEGIN
                    -- ================================================================
                    -- PART 1: Rating Plan + Version (APD v1, LOB=12)
                    -- ================================================================
                    SELECT id INTO v_plan_id FROM rating_plans
                    WHERE line_of_business = 12 AND name = 'Auto Physical Damage v1';
                    IF v_plan_id IS NULL THEN
                        v_plan_id := gen_random_uuid();
                        INSERT INTO rating_plans (id, line_of_business, name, formula_key, status, created_at, updated_at, is_deleted)
                        VALUES (v_plan_id, 12, 'Auto Physical Damage v1', 'APD_v1', 2, v_now, v_now, false);
                    END IF;

                    SELECT id INTO v_version_id FROM rating_plan_versions
                    WHERE rating_plan_id = v_plan_id AND version_number = 1;
                    IF v_version_id IS NULL THEN
                        v_version_id := gen_random_uuid();
                        INSERT INTO rating_plan_versions (id, rating_plan_id, version_number, effective_date, status, schedule_min, schedule_max, created_at, updated_at, is_deleted)
                        VALUES (v_version_id, v_plan_id, 1, '2026-01-01', 2, 0.7500, 1.2500, v_now, v_now, false);
                    END IF;

                    -- ================================================================
                    -- PART 2: COMP_BASE_RATE  (RatePer100=2)
                    -- Dimensions: vehicle_class (1-4), value_bracket (1-8)
                    -- Source: London APD Rater Final Version v23.xlsx, TABLE 1.F
                    -- ================================================================
                    SELECT id INTO v_ft_comp_base_id FROM factor_tables
                    WHERE rating_plan_version_id = v_version_id AND code = 'COMP_BASE_RATE';
                    IF v_ft_comp_base_id IS NULL THEN
                        v_ft_comp_base_id := gen_random_uuid();
                        INSERT INTO factor_tables (id, rating_plan_version_id, code, dimension_names, value_semantics, created_at, updated_at, is_deleted)
                        VALUES (v_ft_comp_base_id, v_version_id, 'COMP_BASE_RATE', '[""vehicle_class"",""value_bracket""]', 2, v_now, v_now, false);

                        INSERT INTO factor_rows (id, factor_table_id, dimension_values, factor, created_at, updated_at, is_deleted) VALUES
                            -- Vehicle class 1 (Light/Med)
                            (gen_random_uuid(), v_ft_comp_base_id, '{""vehicle_class"":""1"",""value_bracket"":""1""}', 1.381380, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_base_id, '{""vehicle_class"":""1"",""value_bracket"":""2""}', 1.259940, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_base_id, '{""vehicle_class"":""1"",""value_bracket"":""3""}', 1.123320, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_base_id, '{""vehicle_class"":""1"",""value_bracket"":""4""}', 1.062600, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_base_id, '{""vehicle_class"":""1"",""value_bracket"":""5""}', 1.001880, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_base_id, '{""vehicle_class"":""1"",""value_bracket"":""6""}', 0.941160, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_base_id, '{""vehicle_class"":""1"",""value_bracket"":""7""}', 0.865260, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_base_id, '{""vehicle_class"":""1"",""value_bracket"":""8""}', 0.804540, v_now, v_now, false),
                            -- Vehicle class 2 (Heavy/XHeavy)
                            (gen_random_uuid(), v_ft_comp_base_id, '{""vehicle_class"":""2"",""value_bracket"":""1""}', 2.368080, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_base_id, '{""vehicle_class"":""2"",""value_bracket"":""2""}', 2.155560, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_base_id, '{""vehicle_class"":""2"",""value_bracket"":""3""}', 1.912680, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_base_id, '{""vehicle_class"":""2"",""value_bracket"":""4""}', 1.821600, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_base_id, '{""vehicle_class"":""2"",""value_bracket"":""5""}', 1.715340, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_base_id, '{""vehicle_class"":""2"",""value_bracket"":""6""}', 1.609080, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_base_id, '{""vehicle_class"":""2"",""value_bracket"":""7""}', 1.472460, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_base_id, '{""vehicle_class"":""2"",""value_bracket"":""8""}', 1.381380, v_now, v_now, false),
                            -- Vehicle class 3 (Truck Tractor)
                            (gen_random_uuid(), v_ft_comp_base_id, '{""vehicle_class"":""3"",""value_bracket"":""1""}', 2.307360, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_base_id, '{""vehicle_class"":""3"",""value_bracket"":""2""}', 2.110020, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_base_id, '{""vehicle_class"":""3"",""value_bracket"":""3""}', 1.867140, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_base_id, '{""vehicle_class"":""3"",""value_bracket"":""4""}', 1.670000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_base_id, '{""vehicle_class"":""3"",""value_bracket"":""5""}', 1.780000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_base_id, '{""vehicle_class"":""3"",""value_bracket"":""6""}', 1.870000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_base_id, '{""vehicle_class"":""3"",""value_bracket"":""7""}', 2.110000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_base_id, '{""vehicle_class"":""3"",""value_bracket"":""8""}', 2.310000, v_now, v_now, false),
                            -- Vehicle class 4 (Trailer)
                            (gen_random_uuid(), v_ft_comp_base_id, '{""vehicle_class"":""4"",""value_bracket"":""1""}', 0.971520, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_base_id, '{""vehicle_class"":""4"",""value_bracket"":""2""}', 0.941160, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_base_id, '{""vehicle_class"":""4"",""value_bracket"":""3""}', 0.880440, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_base_id, '{""vehicle_class"":""4"",""value_bracket"":""4""}', 0.819720, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_base_id, '{""vehicle_class"":""4"",""value_bracket"":""5""}', 0.789360, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_base_id, '{""vehicle_class"":""4"",""value_bracket"":""6""}', 0.743820, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_base_id, '{""vehicle_class"":""4"",""value_bracket"":""7""}', 0.698280, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_base_id, '{""vehicle_class"":""4"",""value_bracket"":""8""}', 0.637560, v_now, v_now, false);
                    END IF;

                    -- ================================================================
                    -- PART 3: COLL_BASE_RATE  (RatePer100=2)
                    -- Dimensions: vehicle_class (1-4), value_bracket (1-8)
                    -- Source: London APD Rater Final Version v23.xlsx, TABLE 1.E
                    -- ================================================================
                    SELECT id INTO v_ft_coll_base_id FROM factor_tables
                    WHERE rating_plan_version_id = v_version_id AND code = 'COLL_BASE_RATE';
                    IF v_ft_coll_base_id IS NULL THEN
                        v_ft_coll_base_id := gen_random_uuid();
                        INSERT INTO factor_tables (id, rating_plan_version_id, code, dimension_names, value_semantics, created_at, updated_at, is_deleted)
                        VALUES (v_ft_coll_base_id, v_version_id, 'COLL_BASE_RATE', '[""vehicle_class"",""value_bracket""]', 2, v_now, v_now, false);

                        INSERT INTO factor_rows (id, factor_table_id, dimension_values, factor, created_at, updated_at, is_deleted) VALUES
                            -- Vehicle class 1 (Light/Med)
                            (gen_random_uuid(), v_ft_coll_base_id, '{""vehicle_class"":""1"",""value_bracket"":""1""}', 4.174500, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_base_id, '{""vehicle_class"":""1"",""value_bracket"":""2""}', 3.870900, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_base_id, '{""vehicle_class"":""1"",""value_bracket"":""3""}', 3.749460, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_base_id, '{""vehicle_class"":""1"",""value_bracket"":""4""}', 3.536940, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_base_id, '{""vehicle_class"":""1"",""value_bracket"":""5""}', 3.278880, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_base_id, '{""vehicle_class"":""1"",""value_bracket"":""6""}', 3.020820, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_base_id, '{""vehicle_class"":""1"",""value_bracket"":""7""}', 2.732400, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_base_id, '{""vehicle_class"":""1"",""value_bracket"":""8""}', 2.398440, v_now, v_now, false),
                            -- Vehicle class 2 (Heavy/XHeavy)
                            (gen_random_uuid(), v_ft_coll_base_id, '{""vehicle_class"":""2"",""value_bracket"":""1""}', 3.946800, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_base_id, '{""vehicle_class"":""2"",""value_bracket"":""2""}', 3.658380, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_base_id, '{""vehicle_class"":""2"",""value_bracket"":""3""}', 3.536940, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_base_id, '{""vehicle_class"":""2"",""value_bracket"":""4""}', 3.339600, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_base_id, '{""vehicle_class"":""2"",""value_bracket"":""5""}', 3.096720, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_base_id, '{""vehicle_class"":""2"",""value_bracket"":""6""}', 2.853840, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_base_id, '{""vehicle_class"":""2"",""value_bracket"":""7""}', 2.580600, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_base_id, '{""vehicle_class"":""2"",""value_bracket"":""8""}', 2.261820, v_now, v_now, false),
                            -- Vehicle class 3 (Truck Tractor)
                            (gen_random_uuid(), v_ft_coll_base_id, '{""vehicle_class"":""3"",""value_bracket"":""1""}', 4.872780, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_base_id, '{""vehicle_class"":""3"",""value_bracket"":""2""}', 4.523640, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_base_id, '{""vehicle_class"":""3"",""value_bracket"":""3""}', 4.371840, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_base_id, '{""vehicle_class"":""3"",""value_bracket"":""4""}', 3.830000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_base_id, '{""vehicle_class"":""3"",""value_bracket"":""5""}', 4.130000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_base_id, '{""vehicle_class"":""3"",""value_bracket"":""6""}', 4.370000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_base_id, '{""vehicle_class"":""3"",""value_bracket"":""7""}', 4.520000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_base_id, '{""vehicle_class"":""3"",""value_bracket"":""8""}', 4.870000, v_now, v_now, false),
                            -- Vehicle class 4 (Trailer)
                            (gen_random_uuid(), v_ft_coll_base_id, '{""vehicle_class"":""4"",""value_bracket"":""1""}', 2.125200, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_base_id, '{""vehicle_class"":""4"",""value_bracket"":""2""}', 2.034120, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_base_id, '{""vehicle_class"":""4"",""value_bracket"":""3""}', 1.927860, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_base_id, '{""vehicle_class"":""4"",""value_bracket"":""4""}', 1.791240, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_base_id, '{""vehicle_class"":""4"",""value_bracket"":""5""}', 1.700160, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_base_id, '{""vehicle_class"":""4"",""value_bracket"":""6""}', 1.609080, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_base_id, '{""vehicle_class"":""4"",""value_bracket"":""7""}', 1.487640, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_base_id, '{""vehicle_class"":""4"",""value_bracket"":""8""}', 1.396560, v_now, v_now, false);
                    END IF;

                    -- ================================================================
                    -- PART 4: MILEAGE_FACTOR  (Multiplier=1)
                    -- Dimensions: road_type (1-5), mileage_class (10/11/12/13/20)
                    -- Note: road_type=5, mileage_class=20 is N/A — not inserted.
                    -- Source: London APD Rater Final Version v23.xlsx, TABLE 1.H
                    -- ================================================================
                    SELECT id INTO v_ft_mileage_id FROM factor_tables
                    WHERE rating_plan_version_id = v_version_id AND code = 'MILEAGE_FACTOR';
                    IF v_ft_mileage_id IS NULL THEN
                        v_ft_mileage_id := gen_random_uuid();
                        INSERT INTO factor_tables (id, rating_plan_version_id, code, dimension_names, value_semantics, created_at, updated_at, is_deleted)
                        VALUES (v_ft_mileage_id, v_version_id, 'MILEAGE_FACTOR', '[""road_type"",""mileage_class""]', 1, v_now, v_now, false);

                        INSERT INTO factor_rows (id, factor_table_id, dimension_values, factor, created_at, updated_at, is_deleted) VALUES
                            -- Road type 1 (State Hwy Rural)
                            (gen_random_uuid(), v_ft_mileage_id, '{""road_type"":""1"",""mileage_class"":""10""}', 0.473000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_mileage_id, '{""road_type"":""1"",""mileage_class"":""11""}', 0.683000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_mileage_id, '{""road_type"":""1"",""mileage_class"":""12""}', 0.950000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_mileage_id, '{""road_type"":""1"",""mileage_class"":""13""}', 1.143089, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_mileage_id, '{""road_type"":""1"",""mileage_class"":""20""}', 1.402381, v_now, v_now, false),
                            -- Road type 2 (Surface Rural)
                            (gen_random_uuid(), v_ft_mileage_id, '{""road_type"":""2"",""mileage_class"":""10""}', 0.450000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_mileage_id, '{""road_type"":""2"",""mileage_class"":""11""}', 0.692000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_mileage_id, '{""road_type"":""2"",""mileage_class"":""12""}', 0.948897, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_mileage_id, '{""road_type"":""2"",""mileage_class"":""13""}', 1.167364, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_mileage_id, '{""road_type"":""2"",""mileage_class"":""20""}', 1.395761, v_now, v_now, false),
                            -- Road type 3 (State Hwy Suburban)
                            (gen_random_uuid(), v_ft_mileage_id, '{""road_type"":""3"",""mileage_class"":""10""}', 0.495000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_mileage_id, '{""road_type"":""3"",""mileage_class"":""11""}', 0.733000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_mileage_id, '{""road_type"":""3"",""mileage_class"":""12""}', 1.019512, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_mileage_id, '{""road_type"":""3"",""mileage_class"":""13""}', 1.254530, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_mileage_id, '{""road_type"":""3"",""mileage_class"":""20""}', 1.535889, v_now, v_now, false),
                            -- Road type 4 (Surface Suburban)
                            (gen_random_uuid(), v_ft_mileage_id, '{""road_type"":""4"",""mileage_class"":""10""}', 0.540000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_mileage_id, '{""road_type"":""4"",""mileage_class"":""11""}', 0.809000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_mileage_id, '{""road_type"":""4"",""mileage_class"":""12""}', 1.112195, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_mileage_id, '{""road_type"":""4"",""mileage_class"":""13""}', 1.338386, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_mileage_id, '{""road_type"":""4"",""mileage_class"":""20""}', 1.602091, v_now, v_now, false),
                            -- Road type 5 (Off Road) — mileage_class 20 is N/A, not inserted
                            (gen_random_uuid(), v_ft_mileage_id, '{""road_type"":""5"",""mileage_class"":""10""}', 0.404000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_mileage_id, '{""road_type"":""5"",""mileage_class"":""11""}', 0.525000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_mileage_id, '{""road_type"":""5"",""mileage_class"":""12""}', 0.646000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_mileage_id, '{""road_type"":""5"",""mileage_class"":""13""}', 0.759000, v_now, v_now, false);
                    END IF;

                    -- ================================================================
                    -- PART 5: DRIVER_FACTOR  (Multiplier=1)
                    -- Dimensions: driver_age (0-8), driver_points (0-5)
                    -- N/A cells are not inserted (lookup failure = ineligible combo).
                    -- Source: London APD Rater Final Version v23.xlsx, Driver Table
                    -- ================================================================
                    SELECT id INTO v_ft_driver_id FROM factor_tables
                    WHERE rating_plan_version_id = v_version_id AND code = 'DRIVER_FACTOR';
                    IF v_ft_driver_id IS NULL THEN
                        v_ft_driver_id := gen_random_uuid();
                        INSERT INTO factor_tables (id, rating_plan_version_id, code, dimension_names, value_semantics, created_at, updated_at, is_deleted)
                        VALUES (v_ft_driver_id, v_version_id, 'DRIVER_FACTOR', '[""driver_age"",""driver_points""]', 1, v_now, v_now, false);

                        INSERT INTO factor_rows (id, factor_table_id, dimension_values, factor, created_at, updated_at, is_deleted) VALUES
                            -- age=0 (<21): pts 0-2 valid, 3-4 N/A, 5 (fleet) N/A
                            (gen_random_uuid(), v_ft_driver_id, '{""driver_age"":""0"",""driver_points"":""0""}', 1.600000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_driver_id, '{""driver_age"":""0"",""driver_points"":""1""}', 2.500000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_driver_id, '{""driver_age"":""0"",""driver_points"":""2""}', 3.500000, v_now, v_now, false),
                            -- age=1 (21-24): pts 0-2 valid
                            (gen_random_uuid(), v_ft_driver_id, '{""driver_age"":""1"",""driver_points"":""0""}', 1.350000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_driver_id, '{""driver_age"":""1"",""driver_points"":""1""}', 1.600000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_driver_id, '{""driver_age"":""1"",""driver_points"":""2""}', 2.000000, v_now, v_now, false),
                            -- age=2 (25-29): pts 0-3 valid
                            (gen_random_uuid(), v_ft_driver_id, '{""driver_age"":""2"",""driver_points"":""0""}', 1.120000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_driver_id, '{""driver_age"":""2"",""driver_points"":""1""}', 1.300000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_driver_id, '{""driver_age"":""2"",""driver_points"":""2""}', 1.500000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_driver_id, '{""driver_age"":""2"",""driver_points"":""3""}', 1.700000, v_now, v_now, false),
                            -- age=3 (30-39): all 6 codes valid (incl fleet=5)
                            (gen_random_uuid(), v_ft_driver_id, '{""driver_age"":""3"",""driver_points"":""0""}', 1.050000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_driver_id, '{""driver_age"":""3"",""driver_points"":""1""}', 1.180000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_driver_id, '{""driver_age"":""3"",""driver_points"":""2""}', 1.350000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_driver_id, '{""driver_age"":""3"",""driver_points"":""3""}', 1.530000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_driver_id, '{""driver_age"":""3"",""driver_points"":""4""}', 1.700000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_driver_id, '{""driver_age"":""3"",""driver_points"":""5""}', 1.050000, v_now, v_now, false),
                            -- age=4 (40-49): all 6 codes valid
                            (gen_random_uuid(), v_ft_driver_id, '{""driver_age"":""4"",""driver_points"":""0""}', 0.920000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_driver_id, '{""driver_age"":""4"",""driver_points"":""1""}', 1.080000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_driver_id, '{""driver_age"":""4"",""driver_points"":""2""}', 1.230000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_driver_id, '{""driver_age"":""4"",""driver_points"":""3""}', 1.370000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_driver_id, '{""driver_age"":""4"",""driver_points"":""4""}', 1.530000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_driver_id, '{""driver_age"":""4"",""driver_points"":""5""}', 0.940000, v_now, v_now, false),
                            -- age=5 (50-65): all 6 codes valid
                            (gen_random_uuid(), v_ft_driver_id, '{""driver_age"":""5"",""driver_points"":""0""}', 0.860000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_driver_id, '{""driver_age"":""5"",""driver_points"":""1""}', 1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_driver_id, '{""driver_age"":""5"",""driver_points"":""2""}', 1.120000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_driver_id, '{""driver_age"":""5"",""driver_points"":""3""}', 1.270000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_driver_id, '{""driver_age"":""5"",""driver_points"":""4""}', 1.430000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_driver_id, '{""driver_age"":""5"",""driver_points"":""5""}', 0.900000, v_now, v_now, false),
                            -- age=6 (66-72): all 6 codes valid
                            (gen_random_uuid(), v_ft_driver_id, '{""driver_age"":""6"",""driver_points"":""0""}', 0.840000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_driver_id, '{""driver_age"":""6"",""driver_points"":""1""}', 0.980000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_driver_id, '{""driver_age"":""6"",""driver_points"":""2""}', 1.100000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_driver_id, '{""driver_age"":""6"",""driver_points"":""3""}', 1.250000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_driver_id, '{""driver_age"":""6"",""driver_points"":""4""}', 1.400000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_driver_id, '{""driver_age"":""6"",""driver_points"":""5""}', 0.920000, v_now, v_now, false),
                            -- age=7 (>72): pts 0-3 valid, 4 N/A, 5 (fleet) valid
                            (gen_random_uuid(), v_ft_driver_id, '{""driver_age"":""7"",""driver_points"":""0""}', 0.970000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_driver_id, '{""driver_age"":""7"",""driver_points"":""1""}', 1.120000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_driver_id, '{""driver_age"":""7"",""driver_points"":""2""}', 1.270000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_driver_id, '{""driver_age"":""7"",""driver_points"":""3""}', 1.450000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_driver_id, '{""driver_age"":""7"",""driver_points"":""5""}', 1.050000, v_now, v_now, false),
                            -- age=8 (Non-Fleet Unassigned): only pts=0 and pts=5 (fleet) valid
                            (gen_random_uuid(), v_ft_driver_id, '{""driver_age"":""8"",""driver_points"":""0""}', 1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_driver_id, '{""driver_age"":""8"",""driver_points"":""5""}', 1.000000, v_now, v_now, false);
                    END IF;

                    -- ================================================================
                    -- PART 6: OPERATION_FACTOR  (Multiplier=1)
                    -- Dimensions: operation_code (91/92/99), vehicle_class (1-4)
                    -- Source: London APD Rater Final Version v23.xlsx, Operation Table
                    -- ================================================================
                    SELECT id INTO v_ft_operation_id FROM factor_tables
                    WHERE rating_plan_version_id = v_version_id AND code = 'OPERATION_FACTOR';
                    IF v_ft_operation_id IS NULL THEN
                        v_ft_operation_id := gen_random_uuid();
                        INSERT INTO factor_tables (id, rating_plan_version_id, code, dimension_names, value_semantics, created_at, updated_at, is_deleted)
                        VALUES (v_ft_operation_id, v_version_id, 'OPERATION_FACTOR', '[""operation_code"",""vehicle_class""]', 1, v_now, v_now, false);

                        INSERT INTO factor_rows (id, factor_table_id, dimension_values, factor, created_at, updated_at, is_deleted) VALUES
                            (gen_random_uuid(), v_ft_operation_id, '{""operation_code"":""91"",""vehicle_class"":""1""}', 0.900000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_operation_id, '{""operation_code"":""91"",""vehicle_class"":""2""}', 1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_operation_id, '{""operation_code"":""91"",""vehicle_class"":""3""}', 1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_operation_id, '{""operation_code"":""91"",""vehicle_class"":""4""}', 0.900000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_operation_id, '{""operation_code"":""92"",""vehicle_class"":""1""}', 1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_operation_id, '{""operation_code"":""92"",""vehicle_class"":""2""}', 1.050000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_operation_id, '{""operation_code"":""92"",""vehicle_class"":""3""}', 1.050000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_operation_id, '{""operation_code"":""92"",""vehicle_class"":""4""}', 1.050000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_operation_id, '{""operation_code"":""99"",""vehicle_class"":""1""}', 1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_operation_id, '{""operation_code"":""99"",""vehicle_class"":""2""}', 1.500000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_operation_id, '{""operation_code"":""99"",""vehicle_class"":""3""}', 1.500000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_operation_id, '{""operation_code"":""99"",""vehicle_class"":""4""}', 1.000000, v_now, v_now, false);
                    END IF;

                    -- ================================================================
                    -- PART 7: STATE_FACTOR  (Multiplier=1)
                    -- Dimensions: state (2-char), operation_code (91/92/99)
                    -- Source: London APD Rater Final Version v23.xlsx, State Table
                    -- ================================================================
                    SELECT id INTO v_ft_state_id FROM factor_tables
                    WHERE rating_plan_version_id = v_version_id AND code = 'STATE_FACTOR';
                    IF v_ft_state_id IS NULL THEN
                        v_ft_state_id := gen_random_uuid();
                        INSERT INTO factor_tables (id, rating_plan_version_id, code, dimension_names, value_semantics, created_at, updated_at, is_deleted)
                        VALUES (v_ft_state_id, v_version_id, 'STATE_FACTOR', '[""state"",""operation_code""]', 1, v_now, v_now, false);

                        INSERT INTO factor_rows (id, factor_table_id, dimension_values, factor, created_at, updated_at, is_deleted) VALUES
                            (gen_random_uuid(), v_ft_state_id, '{""state"":""AL"",""operation_code"":""91""}', 1.142538, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_state_id, '{""state"":""AL"",""operation_code"":""92""}', 1.101494, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_state_id, '{""state"":""AL"",""operation_code"":""99""}', 1.101494, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_state_id, '{""state"":""AR"",""operation_code"":""91""}', 1.114023, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_state_id, '{""state"":""AR"",""operation_code"":""92""}', 1.164084, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_state_id, '{""state"":""AR"",""operation_code"":""99""}', 1.164084, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_state_id, '{""state"":""FL"",""operation_code"":""91""}', 0.973431, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_state_id, '{""state"":""FL"",""operation_code"":""92""}', 1.164552, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_state_id, '{""state"":""FL"",""operation_code"":""99""}', 1.164552, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_state_id, '{""state"":""GA"",""operation_code"":""91""}', 1.020533, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_state_id, '{""state"":""GA"",""operation_code"":""92""}', 1.277740, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_state_id, '{""state"":""GA"",""operation_code"":""99""}', 1.277740, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_state_id, '{""state"":""LA"",""operation_code"":""91""}', 1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_state_id, '{""state"":""LA"",""operation_code"":""92""}', 1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_state_id, '{""state"":""LA"",""operation_code"":""99""}', 1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_state_id, '{""state"":""MD"",""operation_code"":""91""}', 1.372716, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_state_id, '{""state"":""MD"",""operation_code"":""92""}', 1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_state_id, '{""state"":""MD"",""operation_code"":""99""}', 1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_state_id, '{""state"":""MS"",""operation_code"":""91""}', 1.095859, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_state_id, '{""state"":""MS"",""operation_code"":""92""}', 1.024641, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_state_id, '{""state"":""MS"",""operation_code"":""99""}', 1.024641, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_state_id, '{""state"":""NC"",""operation_code"":""91""}', 0.959046, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_state_id, '{""state"":""NC"",""operation_code"":""92""}', 1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_state_id, '{""state"":""NC"",""operation_code"":""99""}', 1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_state_id, '{""state"":""OK"",""operation_code"":""91""}', 1.184413, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_state_id, '{""state"":""OK"",""operation_code"":""92""}', 0.970690, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_state_id, '{""state"":""OK"",""operation_code"":""99""}', 0.970690, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_state_id, '{""state"":""PA"",""operation_code"":""91""}', 0.907974, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_state_id, '{""state"":""PA"",""operation_code"":""92""}', 1.073094, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_state_id, '{""state"":""PA"",""operation_code"":""99""}', 1.073094, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_state_id, '{""state"":""SC"",""operation_code"":""91""}', 0.937645, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_state_id, '{""state"":""SC"",""operation_code"":""92""}', 1.059514, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_state_id, '{""state"":""SC"",""operation_code"":""99""}', 1.059514, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_state_id, '{""state"":""TN"",""operation_code"":""91""}', 1.163589, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_state_id, '{""state"":""TN"",""operation_code"":""92""}', 1.193610, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_state_id, '{""state"":""TN"",""operation_code"":""99""}', 1.193610, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_state_id, '{""state"":""TX"",""operation_code"":""91""}', 1.102550, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_state_id, '{""state"":""TX"",""operation_code"":""92""}', 1.093065, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_state_id, '{""state"":""TX"",""operation_code"":""99""}', 1.093065, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_state_id, '{""state"":""VA"",""operation_code"":""91""}', 1.305073, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_state_id, '{""state"":""VA"",""operation_code"":""92""}', 1.414958, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_state_id, '{""state"":""VA"",""operation_code"":""99""}', 1.414958, v_now, v_now, false);
                    END IF;

                    -- ================================================================
                    -- PART 8: COMP_DED_FACTOR  (Multiplier=1)
                    -- Dimensions: deductible (500/1000/2000/2500/5000/10000/25000), value_bracket (1-8)
                    -- Source: London APD Rater Final Version v23.xlsx, TABLE 4.E
                    -- ================================================================
                    SELECT id INTO v_ft_comp_ded_id FROM factor_tables
                    WHERE rating_plan_version_id = v_version_id AND code = 'COMP_DED_FACTOR';
                    IF v_ft_comp_ded_id IS NULL THEN
                        v_ft_comp_ded_id := gen_random_uuid();
                        INSERT INTO factor_tables (id, rating_plan_version_id, code, dimension_names, value_semantics, created_at, updated_at, is_deleted)
                        VALUES (v_ft_comp_ded_id, v_version_id, 'COMP_DED_FACTOR', '[""deductible"",""value_bracket""]', 1, v_now, v_now, false);

                        INSERT INTO factor_rows (id, factor_table_id, dimension_values, factor, created_at, updated_at, is_deleted) VALUES
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""500"",""value_bracket"":""1""}',   1.070000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""500"",""value_bracket"":""2""}',   1.060000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""500"",""value_bracket"":""3""}',   1.050000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""500"",""value_bracket"":""4""}',   1.040000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""500"",""value_bracket"":""5""}',   1.030000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""500"",""value_bracket"":""6""}',   1.020000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""500"",""value_bracket"":""7""}',   1.010000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""500"",""value_bracket"":""8""}',   1.010000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""1000"",""value_bracket"":""1""}',  1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""1000"",""value_bracket"":""2""}',  1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""1000"",""value_bracket"":""3""}',  1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""1000"",""value_bracket"":""4""}',  1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""1000"",""value_bracket"":""5""}',  1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""1000"",""value_bracket"":""6""}',  1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""1000"",""value_bracket"":""7""}',  1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""1000"",""value_bracket"":""8""}',  1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""2000"",""value_bracket"":""1""}',  1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""2000"",""value_bracket"":""2""}',  1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""2000"",""value_bracket"":""3""}',  1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""2000"",""value_bracket"":""4""}',  1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""2000"",""value_bracket"":""5""}',  1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""2000"",""value_bracket"":""6""}',  1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""2000"",""value_bracket"":""7""}',  1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""2000"",""value_bracket"":""8""}',  1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""2500"",""value_bracket"":""1""}',  0.950000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""2500"",""value_bracket"":""2""}',  0.930000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""2500"",""value_bracket"":""3""}',  0.910000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""2500"",""value_bracket"":""4""}',  0.890000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""2500"",""value_bracket"":""5""}',  0.860000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""2500"",""value_bracket"":""6""}',  0.860000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""2500"",""value_bracket"":""7""}',  0.860000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""2500"",""value_bracket"":""8""}',  0.860000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""5000"",""value_bracket"":""1""}',  0.900000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""5000"",""value_bracket"":""2""}',  0.900000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""5000"",""value_bracket"":""3""}',  0.900000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""5000"",""value_bracket"":""4""}',  0.870000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""5000"",""value_bracket"":""5""}',  0.840000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""5000"",""value_bracket"":""6""}',  0.840000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""5000"",""value_bracket"":""7""}',  0.840000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""5000"",""value_bracket"":""8""}',  0.840000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""10000"",""value_bracket"":""1""}', 0.900000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""10000"",""value_bracket"":""2""}', 0.900000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""10000"",""value_bracket"":""3""}', 0.900000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""10000"",""value_bracket"":""4""}', 0.870000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""10000"",""value_bracket"":""5""}', 0.840000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""10000"",""value_bracket"":""6""}', 0.840000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""10000"",""value_bracket"":""7""}', 0.840000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""10000"",""value_bracket"":""8""}', 0.840000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""25000"",""value_bracket"":""1""}', 0.900000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""25000"",""value_bracket"":""2""}', 0.900000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""25000"",""value_bracket"":""3""}', 0.900000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""25000"",""value_bracket"":""4""}', 0.870000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""25000"",""value_bracket"":""5""}', 0.840000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""25000"",""value_bracket"":""6""}', 0.840000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""25000"",""value_bracket"":""7""}', 0.840000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_comp_ded_id, '{""deductible"":""25000"",""value_bracket"":""8""}', 0.840000, v_now, v_now, false);
                    END IF;

                    -- ================================================================
                    -- PART 9: COLL_DED_FACTOR  (Multiplier=1)
                    -- Dimensions: deductible (500/1000/2000/2500/5000/10000/25000), value_bracket (1-8)
                    -- Source: London APD Rater Final Version v23.xlsx, TABLE 4.D
                    -- ================================================================
                    SELECT id INTO v_ft_coll_ded_id FROM factor_tables
                    WHERE rating_plan_version_id = v_version_id AND code = 'COLL_DED_FACTOR';
                    IF v_ft_coll_ded_id IS NULL THEN
                        v_ft_coll_ded_id := gen_random_uuid();
                        INSERT INTO factor_tables (id, rating_plan_version_id, code, dimension_names, value_semantics, created_at, updated_at, is_deleted)
                        VALUES (v_ft_coll_ded_id, v_version_id, 'COLL_DED_FACTOR', '[""deductible"",""value_bracket""]', 1, v_now, v_now, false);

                        INSERT INTO factor_rows (id, factor_table_id, dimension_values, factor, created_at, updated_at, is_deleted) VALUES
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""500"",""value_bracket"":""1""}',   1.100000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""500"",""value_bracket"":""2""}',   1.080000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""500"",""value_bracket"":""3""}',   1.060000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""500"",""value_bracket"":""4""}',   1.050000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""500"",""value_bracket"":""5""}',   1.040000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""500"",""value_bracket"":""6""}',   1.040000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""500"",""value_bracket"":""7""}',   1.030000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""500"",""value_bracket"":""8""}',   1.020000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""1000"",""value_bracket"":""1""}',  1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""1000"",""value_bracket"":""2""}',  1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""1000"",""value_bracket"":""3""}',  1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""1000"",""value_bracket"":""4""}',  1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""1000"",""value_bracket"":""5""}',  1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""1000"",""value_bracket"":""6""}',  1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""1000"",""value_bracket"":""7""}',  1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""1000"",""value_bracket"":""8""}',  1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""2000"",""value_bracket"":""1""}',  1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""2000"",""value_bracket"":""2""}',  1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""2000"",""value_bracket"":""3""}',  1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""2000"",""value_bracket"":""4""}',  1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""2000"",""value_bracket"":""5""}',  1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""2000"",""value_bracket"":""6""}',  1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""2000"",""value_bracket"":""7""}',  1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""2000"",""value_bracket"":""8""}',  1.000000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""2500"",""value_bracket"":""1""}',  0.950000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""2500"",""value_bracket"":""2""}',  0.940000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""2500"",""value_bracket"":""3""}',  0.930000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""2500"",""value_bracket"":""4""}',  0.920000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""2500"",""value_bracket"":""5""}',  0.900000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""2500"",""value_bracket"":""6""}',  0.900000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""2500"",""value_bracket"":""7""}',  0.900000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""2500"",""value_bracket"":""8""}',  0.900000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""5000"",""value_bracket"":""1""}',  0.900000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""5000"",""value_bracket"":""2""}',  0.900000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""5000"",""value_bracket"":""3""}',  0.900000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""5000"",""value_bracket"":""4""}',  0.900000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""5000"",""value_bracket"":""5""}',  0.850000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""5000"",""value_bracket"":""6""}',  0.850000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""5000"",""value_bracket"":""7""}',  0.850000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""5000"",""value_bracket"":""8""}',  0.850000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""10000"",""value_bracket"":""1""}', 0.900000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""10000"",""value_bracket"":""2""}', 0.900000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""10000"",""value_bracket"":""3""}', 0.900000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""10000"",""value_bracket"":""4""}', 0.900000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""10000"",""value_bracket"":""5""}', 0.850000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""10000"",""value_bracket"":""6""}', 0.850000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""10000"",""value_bracket"":""7""}', 0.850000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""10000"",""value_bracket"":""8""}', 0.850000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""25000"",""value_bracket"":""1""}', 0.900000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""25000"",""value_bracket"":""2""}', 0.900000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""25000"",""value_bracket"":""3""}', 0.900000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""25000"",""value_bracket"":""4""}', 0.900000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""25000"",""value_bracket"":""5""}', 0.850000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""25000"",""value_bracket"":""6""}', 0.850000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""25000"",""value_bracket"":""7""}', 0.850000, v_now, v_now, false),
                            (gen_random_uuid(), v_ft_coll_ded_id, '{""deductible"":""25000"",""value_bracket"":""8""}', 0.850000, v_now, v_now, false);
                    END IF;

                    -- ================================================================
                    -- PART 10: Carrier Rating Assignment (Beazley + APD=12)
                    -- ================================================================
                    EXECUTE format(
                        'SELECT %I FROM carriers WHERE lower(%I::text) LIKE $1 LIMIT 1',
                        (SELECT column_name FROM information_schema.columns WHERE table_name = 'carriers' AND lower(column_name) = 'id'   LIMIT 1),
                        (SELECT column_name FROM information_schema.columns WHERE table_name = 'carriers' AND lower(column_name) = 'name' LIMIT 1)
                    ) INTO v_carrier_id USING '%beazley%';

                    IF v_carrier_id IS NOT NULL
                       AND NOT EXISTS (SELECT 1 FROM carrier_rating_assignments
                                       WHERE carrier_id = v_carrier_id AND line_of_business = 12) THEN
                        INSERT INTO carrier_rating_assignments (id, carrier_id, line_of_business, rating_plan_version_id, created_at, updated_at, is_deleted)
                        VALUES (gen_random_uuid(), v_carrier_id, 12, v_version_id, v_now, v_now, false);
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
                    WHERE rp.formula_key = 'APD_v1'
                );
                DELETE FROM factor_rows WHERE factor_table_id IN (
                    SELECT ft.id FROM factor_tables ft
                    JOIN rating_plan_versions rpv ON rpv.id = ft.rating_plan_version_id
                    JOIN rating_plans rp ON rp.id = rpv.rating_plan_id WHERE rp.formula_key = 'APD_v1'
                );
                DELETE FROM factor_tables WHERE rating_plan_version_id IN (
                    SELECT rpv.id FROM rating_plan_versions rpv
                    JOIN rating_plans rp ON rp.id = rpv.rating_plan_id WHERE rp.formula_key = 'APD_v1'
                );
                DELETE FROM rating_plan_versions WHERE rating_plan_id IN (SELECT id FROM rating_plans WHERE formula_key = 'APD_v1');
                DELETE FROM rating_plans WHERE formula_key = 'APD_v1';
            ");
        }
    }
}
