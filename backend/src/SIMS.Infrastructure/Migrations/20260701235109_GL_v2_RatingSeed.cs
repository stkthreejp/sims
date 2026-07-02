using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GL_v2_RatingSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
DECLARE
    v_plan_id    uuid;
    v_version_id uuid;
    v_ft         uuid;
    v_now timestamp with time zone := now();
BEGIN
    SELECT id INTO v_plan_id FROM rating_plans WHERE line_of_business = 1 AND formula_key = 'GL_v2';
    IF v_plan_id IS NULL THEN
        v_plan_id := gen_random_uuid();
        INSERT INTO rating_plans (id, line_of_business, name, formula_key, status, created_at, updated_at, is_deleted)
        VALUES (v_plan_id, 1, 'General Liability v2 (Longleaf, multi-state)', 'GL_v2', 2, v_now, v_now, false);
    END IF;

    SELECT id INTO v_version_id FROM rating_plan_versions WHERE rating_plan_id = v_plan_id AND version_number = 1;
    IF v_version_id IS NULL THEN
        v_version_id := gen_random_uuid();
        INSERT INTO rating_plan_versions (id, rating_plan_id, version_number, effective_date, status, schedule_min, schedule_max, created_at, updated_at, is_deleted)
        VALUES (v_version_id, v_plan_id, 1, '2026-01-01', 2, 0.8000, 1.2000, v_now, v_now, false);
    ELSE
        DELETE FROM factor_rows fr USING factor_tables ft WHERE fr.factor_table_id = ft.id AND ft.rating_plan_version_id = v_version_id;
        DELETE FROM factor_tables WHERE rating_plan_version_id = v_version_id;
    END IF;

    v_ft := gen_random_uuid();
    INSERT INTO factor_tables (id, rating_plan_version_id, code, dimension_names, value_semantics, created_at, updated_at, is_deleted)
    VALUES (v_ft, v_version_id, 'GL_CLASS', '[""class_code""]', 1, v_now, v_now, false);
    INSERT INTO factor_rows (id, factor_table_id, dimension_values, factor, created_at, updated_at, is_deleted) VALUES
      (gen_random_uuid(), v_ft, '{""class_code"":""97111"",""description"":""Logging and Lumbering"",""premium_basis"":""Payroll / $1,000"",""has_pco"":""false"",""po_tier"":""PO_T2"",""pco_tier"":""PCO_TA"",""divisor"":""1000""}', 1000, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""99793"",""description"":""Truckers - Common or Contract Carriers"",""premium_basis"":""Payroll / $1,000"",""has_pco"":""false"",""po_tier"":""PO_T3"",""pco_tier"":""PCO_TA"",""divisor"":""1000""}', 1000, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""43822"",""description"":""Forestry Services"",""premium_basis"":""Payroll / $1,000"",""has_pco"":""false"",""po_tier"":""PO_T2"",""pco_tier"":""PCO_TA"",""divisor"":""1000""}', 1000, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""49451"",""description"":""Vacant Land - Other than Not-For-Profit"",""premium_basis"":""Each Acre"",""has_pco"":""false"",""po_tier"":""PO_T2"",""pco_tier"":""PCO_TA"",""divisor"":""1""}', 1, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61226"",""description"":""Buildings/Premises - Office NOC"",""premium_basis"":""Area / 1,000 sq ft"",""has_pco"":""false"",""po_tier"":""PO_T2"",""pco_tier"":""PCO_TA"",""divisor"":""1000""}', 1000, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61224"",""description"":""Buildings/Premises - Office (Emp. Only)"",""premium_basis"":""Area / 1,000 sq ft"",""has_pco"":""false"",""po_tier"":""PO_T2"",""pco_tier"":""PCO_TA"",""divisor"":""1000""}', 1000, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""91581"",""description"":""Sub-contracted Work (not buildings)"",""premium_basis"":""Total Cost / $1,000"",""has_pco"":""true"",""po_tier"":""PO_T3"",""pco_tier"":""PCO_TB"",""divisor"":""1000""}', 1000, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""91590"",""description"":""Contractors Permanent Yard"",""premium_basis"":""Payroll / $1,000"",""has_pco"":""false"",""po_tier"":""PO_T3"",""pco_tier"":""PCO_TA"",""divisor"":""1000""}', 1000, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""94007"",""description"":""Excavation"",""premium_basis"":""Payroll / $1,000"",""has_pco"":""true"",""po_tier"":""PO_T2"",""pco_tier"":""PCO_TB"",""divisor"":""1000""}', 1000, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""95410"",""description"":""Grading of Land"",""premium_basis"":""Payroll / $1,000"",""has_pco"":""true"",""po_tier"":""PO_T2"",""pco_tier"":""PCO_TB"",""divisor"":""1000""}', 1000, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""58873"",""description"":""Saw Mills or Planing Mills"",""premium_basis"":""Gross Sales / $1,000"",""has_pco"":""true"",""po_tier"":""PO_T3"",""pco_tier"":""PCO_TB"",""divisor"":""1000""}', 1000, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""59738"",""description"":""Tie, Post or Pole Yard"",""premium_basis"":""Gross Sales / $1,000"",""has_pco"":""true"",""po_tier"":""PO_T3"",""pco_tier"":""PCO_TB"",""divisor"":""1000""}', 1000, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""45819"",""description"":""Lumberyards"",""premium_basis"":""Gross Sales / $1,000"",""has_pco"":""true"",""po_tier"":""PO_T2"",""pco_tier"":""PCO_TB"",""divisor"":""1000""}', 1000, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61212"",""description"":""Buildings/Premises - Bank/Office - Mercantile/Mfg (LRO)"",""premium_basis"":""Area / 1,000 sq ft"",""has_pco"":""false"",""po_tier"":""PO_T2"",""pco_tier"":""PCO_TA"",""divisor"":""1000""}', 1000, v_now, v_now, false);

    v_ft := gen_random_uuid();
    INSERT INTO factor_tables (id, rating_plan_version_id, code, dimension_names, value_semantics, created_at, updated_at, is_deleted)
    VALUES (v_ft, v_version_id, 'GL_LOSS_COST_334', '[""class_code"",""state""]', 2, v_now, v_now, false);
    INSERT INTO factor_rows (id, factor_table_id, dimension_values, factor, created_at, updated_at, is_deleted) VALUES
      (gen_random_uuid(), v_ft, '{""class_code"":""97111"",""state"":""AL""}', 5.64, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""97111"",""state"":""AR""}', 4.69, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""97111"",""state"":""FL""}', 8.09, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""97111"",""state"":""GA""}', 5.06, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""97111"",""state"":""LA""}', 24.1, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""97111"",""state"":""MS""}', 7.06, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""97111"",""state"":""OK""}', 4.41, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""97111"",""state"":""SC""}', 7.23, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""97111"",""state"":""TN""}', 4.41, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""97111"",""state"":""TX""}', 5.3, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""97111"",""state"":""PA""}', 5.06, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""97111"",""state"":""MD""}', 5.06, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""97111"",""state"":""VA""}', 5.06, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""97111"",""state"":""NC""}', 5.06, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""97111"",""state"":""KY""}', 5.06, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""99793"",""state"":""AL""}', 3.23, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""99793"",""state"":""AR""}', 2.9, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""99793"",""state"":""FL""}', 2.9, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""99793"",""state"":""GA""}', 2.9, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""99793"",""state"":""LA""}', 2.9, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""99793"",""state"":""MS""}', 2.9, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""99793"",""state"":""OK""}', 2.52, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""99793"",""state"":""SC""}', 4.14, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""99793"",""state"":""TN""}', 2.52, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""99793"",""state"":""TX""}', 3.03, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""99793"",""state"":""PA""}', 2.9, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""99793"",""state"":""MD""}', 2.9, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""99793"",""state"":""VA""}', 2.9, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""99793"",""state"":""NC""}', 2.9, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""99793"",""state"":""KY""}', 2.9, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""43822"",""state"":""AL""}', 6.28, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""43822"",""state"":""AR""}', 3.54, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""43822"",""state"":""FL""}', 6.43, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""43822"",""state"":""GA""}', 4.18, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""43822"",""state"":""LA""}', 9.66, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""43822"",""state"":""MS""}', 5.06, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""43822"",""state"":""OK""}', 2.69, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""43822"",""state"":""SC""}', 5.81, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""43822"",""state"":""TN""}', 3.32, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""43822"",""state"":""TX""}', 2.76, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""43822"",""state"":""PA""}', 4.18, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""43822"",""state"":""MD""}', 4.18, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""43822"",""state"":""VA""}', 4.18, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""43822"",""state"":""NC""}', 4.18, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""43822"",""state"":""KY""}', 4.18, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""49451"",""state"":""AL""}', 0.12, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""49451"",""state"":""AR""}', 0.37, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""49451"",""state"":""FL""}', 7.63, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""49451"",""state"":""LA""}', 1.26, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""49451"",""state"":""MS""}', 0.71, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61226"",""state"":""AL""}', 120.0, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61226"",""state"":""AR""}', 38.8, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61226"",""state"":""FL""}', 157.0, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61226"",""state"":""GA""}', 134.0, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61226"",""state"":""LA""}', 100.0, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61226"",""state"":""MS""}', 57.5, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61226"",""state"":""OK""}', 35.6, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61226"",""state"":""SC""}', 154.0, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61226"",""state"":""TN""}', 50.4, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61226"",""state"":""TX""}', 47.7, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61226"",""state"":""PA""}', 134.0, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61226"",""state"":""MD""}', 134.0, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61226"",""state"":""VA""}', 134.0, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61226"",""state"":""NC""}', 134.0, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61226"",""state"":""KY""}', 134.0, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61224"",""state"":""AL""}', 51.8, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61224"",""state"":""AR""}', 21.1, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61224"",""state"":""FL""}', 85.7, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61224"",""state"":""GA""}', 57.9, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61224"",""state"":""LA""}', 54.4, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61224"",""state"":""MS""}', 31.3, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61224"",""state"":""OK""}', 15.3, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61224"",""state"":""SC""}', 65.5, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61224"",""state"":""TN""}', 21.6, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61224"",""state"":""TX""}', 20.4, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61224"",""state"":""PA""}', 57.9, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61224"",""state"":""MD""}', 57.9, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61224"",""state"":""VA""}', 57.9, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61224"",""state"":""NC""}', 57.9, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61224"",""state"":""KY""}', 57.9, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""91581"",""state"":""AL""}', 0.1, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""91590"",""state"":""AL""}', 3.67, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""91590"",""state"":""AR""}', 2.36, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""91590"",""state"":""FL""}', 4.08, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""91590"",""state"":""GA""}', 3.29, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""91590"",""state"":""LA""}', 12.1, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""91590"",""state"":""MS""}', 3.56, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""91590"",""state"":""OK""}', 2.87, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""91590"",""state"":""SC""}', 4.72, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""91590"",""state"":""TN""}', 2.87, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""91590"",""state"":""TX""}', 3.46, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""91590"",""state"":""PA""}', 3.29, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""91590"",""state"":""MD""}', 3.29, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""91590"",""state"":""VA""}', 3.29, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""91590"",""state"":""NC""}', 3.29, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""91590"",""state"":""KY""}', 3.29, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""94007"",""state"":""AL""}', 11.7, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""94007"",""state"":""AR""}', 10.5, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""94007"",""state"":""FL""}', 10.5, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""94007"",""state"":""GA""}', 10.5, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""94007"",""state"":""LA""}', 10.5, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""94007"",""state"":""MS""}', 10.5, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""94007"",""state"":""OK""}', 9.15, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""94007"",""state"":""SC""}', 15.1, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""94007"",""state"":""TN""}', 9.13, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""94007"",""state"":""TX""}', 10.9, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""94007"",""state"":""PA""}', 10.5, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""94007"",""state"":""MD""}', 10.5, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""94007"",""state"":""VA""}', 10.5, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""94007"",""state"":""NC""}', 10.5, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""94007"",""state"":""KY""}', 10.5, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""95410"",""state"":""AL""}', 4.71, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""95410"",""state"":""AR""}', 4.23, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""95410"",""state"":""FL""}', 4.23, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""95410"",""state"":""GA""}', 4.23, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""95410"",""state"":""LA""}', 4.23, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""95410"",""state"":""MS""}', 4.23, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""95410"",""state"":""OK""}', 3.68, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""95410"",""state"":""SC""}', 6.06, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""95410"",""state"":""TN""}', 3.68, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""95410"",""state"":""TX""}', 4.43, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""95410"",""state"":""PA""}', 4.23, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""95410"",""state"":""MD""}', 4.23, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""95410"",""state"":""VA""}', 4.23, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""95410"",""state"":""NC""}', 4.23, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""95410"",""state"":""KY""}', 4.23, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""58873"",""state"":""AL""}', 0.107, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""58873"",""state"":""AR""}', 0.139, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""58873"",""state"":""FL""}', 0.23, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""58873"",""state"":""GA""}', 0.161, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""58873"",""state"":""LA""}', 0.5, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""58873"",""state"":""MS""}', 0.109, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""58873"",""state"":""OK""}', 0.108, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""58873"",""state"":""SC""}', 0.134, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""58873"",""state"":""TN""}', 0.074, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""58873"",""state"":""TX""}', 0.15, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""58873"",""state"":""PA""}', 0.161, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""58873"",""state"":""MD""}', 0.161, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""58873"",""state"":""VA""}', 0.161, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""58873"",""state"":""NC""}', 0.161, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""58873"",""state"":""KY""}', 0.161, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""59738"",""state"":""AL""}', 0.125, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""59738"",""state"":""AR""}', 0.141, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""59738"",""state"":""FL""}', 0.215, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""59738"",""state"":""GA""}', 0.141, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""59738"",""state"":""LA""}', 0.51, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""59738"",""state"":""MS""}', 0.089, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""59738"",""state"":""OK""}', 0.157, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""59738"",""state"":""SC""}', 0.139, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""59738"",""state"":""TN""}', 0.071, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""59738"",""state"":""TX""}', 0.15, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""59738"",""state"":""PA""}', 0.141, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""59738"",""state"":""MD""}', 0.141, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""59738"",""state"":""VA""}', 0.141, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""59738"",""state"":""NC""}', 0.141, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""59738"",""state"":""KY""}', 0.141, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""45819"",""state"":""AL""}', 0.102, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""45819"",""state"":""AR""}', 0.126, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""45819"",""state"":""FL""}', 0.31, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""45819"",""state"":""GA""}', 0.13, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""45819"",""state"":""LA""}', 0.28, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""45819"",""state"":""MS""}', 0.169, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""45819"",""state"":""OK""}', 0.087, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""45819"",""state"":""SC""}', 0.185, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""45819"",""state"":""TN""}', 0.082, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""45819"",""state"":""TX""}', 0.088, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""45819"",""state"":""PA""}', 0.13, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""45819"",""state"":""MD""}', 0.13, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""45819"",""state"":""VA""}', 0.13, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""45819"",""state"":""NC""}', 0.13, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""45819"",""state"":""KY""}', 0.13, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61212"",""state"":""AL""}', 33.1, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61212"",""state"":""AR""}', 37.0, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61212"",""state"":""FL""}', 37.0, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61212"",""state"":""GA""}', 37.0, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61212"",""state"":""LA""}', 37.0, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61212"",""state"":""MS""}', 37.0, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61212"",""state"":""OK""}', 9.77, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61212"",""state"":""SC""}', 41.8, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61212"",""state"":""TN""}', 13.8, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61212"",""state"":""TX""}', 13.0, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61212"",""state"":""PA""}', 37.0, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61212"",""state"":""MD""}', 37.0, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61212"",""state"":""VA""}', 37.0, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61212"",""state"":""NC""}', 37.0, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""61212"",""state"":""KY""}', 37.0, v_now, v_now, false);

    v_ft := gen_random_uuid();
    INSERT INTO factor_tables (id, rating_plan_version_id, code, dimension_names, value_semantics, created_at, updated_at, is_deleted)
    VALUES (v_ft, v_version_id, 'GL_LOSS_COST_336', '[""class_code"",""state""]', 2, v_now, v_now, false);
    INSERT INTO factor_rows (id, factor_table_id, dimension_values, factor, created_at, updated_at, is_deleted) VALUES
      (gen_random_uuid(), v_ft, '{""class_code"":""91581"",""state"":""AL""}', 0.1, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""94007"",""state"":""AL""}', 4.83, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""94007"",""state"":""AR""}', 3.62, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""94007"",""state"":""FL""}', 3.62, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""94007"",""state"":""GA""}', 3.62, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""94007"",""state"":""LA""}', 3.62, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""94007"",""state"":""MS""}', 3.62, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""94007"",""state"":""OK""}', 3.23, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""94007"",""state"":""SC""}', 10.7, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""94007"",""state"":""TN""}', 3.7, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""94007"",""state"":""TX""}', 4.94, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""94007"",""state"":""PA""}', 3.62, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""94007"",""state"":""MD""}', 3.62, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""94007"",""state"":""VA""}', 3.62, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""94007"",""state"":""NC""}', 3.62, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""94007"",""state"":""KY""}', 3.62, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""95410"",""state"":""AL""}', 2.71, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""95410"",""state"":""AR""}', 2.04, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""95410"",""state"":""FL""}', 2.04, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""95410"",""state"":""GA""}', 2.04, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""95410"",""state"":""LA""}', 2.04, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""95410"",""state"":""MS""}', 2.04, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""95410"",""state"":""OK""}', 1.81, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""95410"",""state"":""SC""}', 6.03, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""95410"",""state"":""TN""}', 2.08, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""95410"",""state"":""TX""}', 2.78, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""95410"",""state"":""PA""}', 2.04, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""95410"",""state"":""MD""}', 2.04, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""95410"",""state"":""VA""}', 2.04, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""95410"",""state"":""NC""}', 2.04, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""95410"",""state"":""KY""}', 2.04, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""58873"",""state"":""AL""}', 0.021, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""58873"",""state"":""AR""}', 0.023, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""58873"",""state"":""FL""}', 0.021, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""58873"",""state"":""GA""}', 0.021, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""58873"",""state"":""LA""}', 0.021, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""58873"",""state"":""MS""}', 0.021, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""58873"",""state"":""OK""}', 0.021, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""58873"",""state"":""SC""}', 0.023, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""58873"",""state"":""TN""}', 0.021, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""58873"",""state"":""TX""}', 0.019, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""58873"",""state"":""PA""}', 0.021, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""58873"",""state"":""MD""}', 0.021, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""58873"",""state"":""VA""}', 0.021, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""58873"",""state"":""NC""}', 0.021, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""58873"",""state"":""KY""}', 0.021, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""59738"",""state"":""AL""}', 0.044, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""59738"",""state"":""AR""}', 0.048, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""59738"",""state"":""FL""}', 0.044, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""59738"",""state"":""GA""}', 0.044, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""59738"",""state"":""LA""}', 0.044, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""59738"",""state"":""MS""}', 0.044, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""59738"",""state"":""OK""}', 0.044, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""59738"",""state"":""SC""}', 0.048, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""59738"",""state"":""TN""}', 0.044, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""59738"",""state"":""TX""}', 0.04, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""59738"",""state"":""PA""}', 0.044, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""59738"",""state"":""MD""}', 0.044, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""59738"",""state"":""VA""}', 0.044, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""59738"",""state"":""NC""}', 0.044, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""59738"",""state"":""KY""}', 0.044, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""45819"",""state"":""AL""}', 0.08, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""45819"",""state"":""AR""}', 0.06, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""45819"",""state"":""FL""}', 0.187, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""45819"",""state"":""GA""}', 0.064, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""45819"",""state"":""LA""}', 0.093, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""45819"",""state"":""MS""}', 0.058, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""45819"",""state"":""OK""}', 0.037, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""45819"",""state"":""SC""}', 0.138, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""45819"",""state"":""TN""}', 0.056, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""45819"",""state"":""TX""}', 0.065, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""45819"",""state"":""PA""}', 0.064, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""45819"",""state"":""MD""}', 0.064, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""45819"",""state"":""VA""}', 0.064, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""45819"",""state"":""NC""}', 0.064, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""class_code"":""45819"",""state"":""KY""}', 0.064, v_now, v_now, false);

    v_ft := gen_random_uuid();
    INSERT INTO factor_tables (id, rating_plan_version_id, code, dimension_names, value_semantics, created_at, updated_at, is_deleted)
    VALUES (v_ft, v_version_id, 'GL_ILF', '[""limit"",""tier""]', 1, v_now, v_now, false);
    INSERT INTO factor_rows (id, factor_table_id, dimension_values, factor, created_at, updated_at, is_deleted) VALUES
      (gen_random_uuid(), v_ft, '{""limit"":""100000"",""tier"":""PO_T1""}', 1.0, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""limit"":""100000"",""tier"":""PO_T2""}', 1.0, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""limit"":""100000"",""tier"":""PO_T3""}', 1.0, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""limit"":""100000"",""tier"":""PCO_TA""}', 1.0, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""limit"":""100000"",""tier"":""PCO_TB""}', 1.0, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""limit"":""100000"",""tier"":""PCO_TC""}', 1.0, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""limit"":""300000"",""tier"":""PO_T1""}', 1.37, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""limit"":""300000"",""tier"":""PO_T2""}', 1.38, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""limit"":""300000"",""tier"":""PO_T3""}', 1.36, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""limit"":""300000"",""tier"":""PCO_TA""}', 1.24, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""limit"":""300000"",""tier"":""PCO_TB""}', 1.27, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""limit"":""300000"",""tier"":""PCO_TC""}', 1.33, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""limit"":""500000"",""tier"":""PO_T1""}', 1.54, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""limit"":""500000"",""tier"":""PO_T2""}', 1.58, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""limit"":""500000"",""tier"":""PO_T3""}', 1.57, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""limit"":""500000"",""tier"":""PCO_TA""}', 1.34, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""limit"":""500000"",""tier"":""PCO_TB""}', 1.4, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""limit"":""500000"",""tier"":""PCO_TC""}', 1.54, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""limit"":""1000000"",""tier"":""PO_T1""}', 1.76, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""limit"":""1000000"",""tier"":""PO_T2""}', 1.87, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""limit"":""1000000"",""tier"":""PO_T3""}', 1.89, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""limit"":""1000000"",""tier"":""PCO_TA""}', 1.46, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""limit"":""1000000"",""tier"":""PCO_TB""}', 1.57, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""limit"":""1000000"",""tier"":""PCO_TC""}', 1.85, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""limit"":""2000000"",""tier"":""PO_T1""}', 1.97, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""limit"":""2000000"",""tier"":""PO_T2""}', 2.17, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""limit"":""2000000"",""tier"":""PO_T3""}', 2.28, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""limit"":""2000000"",""tier"":""PCO_TA""}', 1.61, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""limit"":""2000000"",""tier"":""PCO_TB""}', 1.75, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""limit"":""2000000"",""tier"":""PCO_TC""}', 2.21, v_now, v_now, false);

    v_ft := gen_random_uuid();
    INSERT INTO factor_tables (id, rating_plan_version_id, code, dimension_names, value_semantics, created_at, updated_at, is_deleted)
    VALUES (v_ft, v_version_id, 'GL_PARAMS', '[""key""]', 1, v_now, v_now, false);
    INSERT INTO factor_rows (id, factor_table_id, dimension_values, factor, created_at, updated_at, is_deleted) VALUES
      (gen_random_uuid(), v_ft, '{""key"":""LCM""}', 1.65, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""key"":""TRIA_RATE""}', 0.025, v_now, v_now, false);

    v_ft := gen_random_uuid();
    INSERT INTO factor_tables (id, rating_plan_version_id, code, dimension_names, value_semantics, created_at, updated_at, is_deleted)
    VALUES (v_ft, v_version_id, 'GL_LL_ENDORSEMENT', '[""limit"",""kind""]', 1, v_now, v_now, false);
    INSERT INTO factor_rows (id, factor_table_id, dimension_values, factor, created_at, updated_at, is_deleted) VALUES
      (gen_random_uuid(), v_ft, '{""limit"":""100000"",""kind"":""min""}', 250.0, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""limit"":""100000"",""kind"":""pct""}', 0.04, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""limit"":""250000"",""kind"":""min""}', 350.0, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""limit"":""250000"",""kind"":""pct""}', 0.06, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""limit"":""500000"",""kind"":""min""}', 600.0, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""limit"":""500000"",""kind"":""pct""}', 0.08, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""limit"":""1000000"",""kind"":""min""}', 750.0, v_now, v_now, false),
      (gen_random_uuid(), v_ft, '{""limit"":""1000000"",""kind"":""pct""}', 0.1, v_now, v_now, false);

    -- Supersede GL_v1: repoint all GL assignments to GL_v2, retire the GL_v1 plan.
    UPDATE carrier_rating_assignments SET rating_plan_version_id = v_version_id, updated_at = v_now WHERE line_of_business = 1;
    UPDATE rating_plans SET status = 3, updated_at = v_now WHERE line_of_business = 1 AND formula_key = 'GL_v1';

    -- Global GL additional-interest rates (carrier_id NULL = all GL carriers).
    IF NOT EXISTS (SELECT 1 FROM carrier_additional_interest_rates WHERE carrier_id IS NULL AND line_of_business = 1 AND is_deleted = false) THEN
        INSERT INTO carrier_additional_interest_rates (id, carrier_id, line_of_business, coverage_type, charge_method, per_interest_amount, blanket_amount, minimum_charge, maximum_charge, state, effective_date, expiration_date, is_active, created_at, updated_at, is_deleted)
        VALUES
          (gen_random_uuid(), NULL, 1, 0, 2, 50, NULL, NULL, NULL, NULL, NULL, NULL, true, v_now, v_now, false),
          (gen_random_uuid(), NULL, 1, 0, 3, NULL, 250, NULL, NULL, NULL, NULL, NULL, true, v_now, v_now, false),
          (gen_random_uuid(), NULL, 1, 2, 2, 50, NULL, NULL, NULL, NULL, NULL, NULL, true, v_now, v_now, false),
          (gen_random_uuid(), NULL, 1, 2, 3, NULL, 250, NULL, NULL, NULL, NULL, NULL, true, v_now, v_now, false),
          (gen_random_uuid(), NULL, 1, 3, 3, NULL, 250, NULL, NULL, NULL, NULL, NULL, true, v_now, v_now, false);
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
    v_v1_version uuid;
    v_now timestamp with time zone := now();
BEGIN
    SELECT id INTO v_plan_id FROM rating_plans WHERE line_of_business = 1 AND formula_key = 'GL_v2';
    IF v_plan_id IS NOT NULL THEN
        SELECT id INTO v_version_id FROM rating_plan_versions WHERE rating_plan_id = v_plan_id AND version_number = 1;
        UPDATE rating_plans SET status = 2, updated_at = v_now WHERE line_of_business = 1 AND formula_key = 'GL_v1';
        SELECT rpv.id INTO v_v1_version FROM rating_plan_versions rpv JOIN rating_plans rp ON rp.id = rpv.rating_plan_id
        WHERE rp.line_of_business = 1 AND rp.formula_key = 'GL_v1' AND rpv.version_number = 1;
        IF v_v1_version IS NOT NULL THEN
            UPDATE carrier_rating_assignments SET rating_plan_version_id = v_v1_version, updated_at = v_now WHERE rating_plan_version_id = v_version_id;
        END IF;
        DELETE FROM factor_rows fr USING factor_tables ft WHERE fr.factor_table_id = ft.id AND ft.rating_plan_version_id = v_version_id;
        DELETE FROM factor_tables WHERE rating_plan_version_id = v_version_id;
        DELETE FROM rating_plan_versions WHERE rating_plan_id = v_plan_id;
        DELETE FROM rating_plans WHERE id = v_plan_id;
    END IF;
    DELETE FROM carrier_additional_interest_rates WHERE carrier_id IS NULL AND line_of_business = 1;
END $$;
");
        }
    }
}
