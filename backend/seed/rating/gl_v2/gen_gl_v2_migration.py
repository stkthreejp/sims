"""GL_v2 rating-seed generator.

Reads gl_rates.json (extracted from SMM_GL_Rater_Finalv11.xlsx) and writes the
full seed SQL into the EF migration 20260701235109_GL_v2_RatingSeed.cs. This is
the script that produced the shipped migration; it is kept here (per the
backend/seed/rating convention) so future rate changes never require
re-extracting the workbook.

FOR A FUTURE RATE CHANGE (until the post-UAT rate workbench exists):
  1. Edit gl_rates.json with the new values.
  2. Scaffold a NEW empty migration (dotnet ef migrations add GL_v2_Rates_<n> ...).
  3. Copy this script, point `mig` at the new migration file, and adapt the SQL:
     insert a NEW rating_plan_versions row (version_number = next), attach the
     factor tables to that new version id, and repoint carrier_rating_assignments
     to it. NEVER mutate a version that has rated/bound quotes.
  4. dotnet build + dotnet test, commit, deploy (migration runs on API startup).

Note: the fixture snapshot in backend/tests/SIMS.Application.Rating.Tests/
Fixtures/GL_v2/rate_data.json pins the FORMULA logic against the frozen v11
workbook values — live rate changes do NOT require fixture updates.

Column-casing warning: rating tables (rating_plans, factor_tables, ...) are
snake_case, but carrier_additional_interest_rates uses quoted PascalCase
columns. Check ApplicationDbContextModelSnapshot.cs before writing raw SQL.
"""
import json
from pathlib import Path

HERE = Path(__file__).resolve().parent
raw = json.load(open(HERE / "gl_rates.json"))
mig = HERE.parents[2] / "src" / "SIMS.Infrastructure" / "Migrations" / "20260701235109_GL_v2_RatingSeed.cs"

def clean(s):
    return (s or "").replace('–','-').replace('—','-').replace('’',"'").encode('ascii','ignore').decode()
def obj(d):
    inner=",".join('"%s":"%s"' % (k, clean(str(v))) for k,v in d.items())
    return ("{"+inner+"}").replace("'","''")
def fnum(x):
    return repr(float(x))

def table(code, dims, semantics, rows):
    s=[]
    s.append("    v_ft := gen_random_uuid();")
    dn="["+",".join('"%s"'%d for d in dims)+"]"
    s.append("    INSERT INTO factor_tables (id, rating_plan_version_id, code, dimension_names, value_semantics, created_at, updated_at, is_deleted)")
    s.append("    VALUES (v_ft, v_version_id, '%s', '%s', %d, v_now, v_now, false);" % (code, dn, semantics))
    s.append("    INSERT INTO factor_rows (id, factor_table_id, dimension_values, factor, created_at, updated_at, is_deleted) VALUES")
    vals=["      (gen_random_uuid(), v_ft, '%s', %s, v_now, v_now, false)" % (j,f) for (j,f) in rows]
    s.append(",\n".join(vals)+";")
    return "\n".join(s)

classes=raw["classes"]
cls_rows=[(obj({"class_code":c["code"],"description":c["desc"],"premium_basis":c["basis"],
    "has_pco":"true" if c["has_pco"] else "false","po_tier":c["po_tier"] or "","pco_tier":c["pco_tier"] or "","divisor":c["divisor"]}), c["divisor"]) for c in classes]
t_class=table("GL_CLASS",["class_code"],1,cls_rows)
lc334_rows=[(obj({"class_code":code,"state":st}), fnum(v)) for code,sv in raw["lc334"].items() for st,v in sv.items()]
t334=table("GL_LOSS_COST_334",["class_code","state"],2,lc334_rows)
lc336_rows=[(obj({"class_code":code,"state":st}), fnum(v)) for code,sv in raw["lc336"].items() for st,v in sv.items()]
t336=table("GL_LOSS_COST_336",["class_code","state"],2,lc336_rows)
ilf_rows=[(obj({"limit":int(r["limit"]),"tier":tier}), fnum(r[tier])) for r in raw["ilf"] for tier in ["PO_T1","PO_T2","PO_T3","PCO_TA","PCO_TB","PCO_TC"] if r[tier] is not None]
t_ilf=table("GL_ILF",["limit","tier"],1,ilf_rows)
p=raw["params"]
par_rows=[(obj({"key":"LCM"}), fnum(p["lcm"])),(obj({"key":"TRIA_RATE"}), fnum(p["tria"]))]
t_par=table("GL_PARAMS",["key"],1,par_rows)
ll_rows=[]
for r in raw["ll_endorsement"]:
    ll_rows.append((obj({"limit":int(r["limit"]),"kind":"min"}), fnum(r["min"])))
    ll_rows.append((obj({"limit":int(r["limit"]),"kind":"pct"}), fnum(r["pct"])))
t_ll=table("GL_LL_ENDORSEMENT",["limit","kind"],1,ll_rows)

up = "\n".join([
"",
"DO $$",
"DECLARE",
"    v_plan_id    uuid;",
"    v_version_id uuid;",
"    v_ft         uuid;",
"    v_now timestamp with time zone := now();",
"BEGIN",
"    SELECT id INTO v_plan_id FROM rating_plans WHERE line_of_business = 1 AND formula_key = 'GL_v2';",
"    IF v_plan_id IS NULL THEN",
"        v_plan_id := gen_random_uuid();",
"        INSERT INTO rating_plans (id, line_of_business, name, formula_key, status, created_at, updated_at, is_deleted)",
"        VALUES (v_plan_id, 1, 'General Liability v2 (Longleaf, multi-state)', 'GL_v2', 2, v_now, v_now, false);",
"    END IF;",
"",
"    SELECT id INTO v_version_id FROM rating_plan_versions WHERE rating_plan_id = v_plan_id AND version_number = 1;",
"    IF v_version_id IS NULL THEN",
"        v_version_id := gen_random_uuid();",
"        INSERT INTO rating_plan_versions (id, rating_plan_id, version_number, effective_date, status, schedule_min, schedule_max, created_at, updated_at, is_deleted)",
"        VALUES (v_version_id, v_plan_id, 1, '2026-01-01', 2, 0.8000, 1.2000, v_now, v_now, false);",
"    ELSE",
"        DELETE FROM factor_rows fr USING factor_tables ft WHERE fr.factor_table_id = ft.id AND ft.rating_plan_version_id = v_version_id;",
"        DELETE FROM factor_tables WHERE rating_plan_version_id = v_version_id;",
"    END IF;",
"",
t_class,"",t334,"",t336,"",t_ilf,"",t_par,"",t_ll,"",
"    -- Supersede GL_v1: repoint all GL assignments to GL_v2, retire the GL_v1 plan.",
"    UPDATE carrier_rating_assignments SET rating_plan_version_id = v_version_id, updated_at = v_now WHERE line_of_business = 1;",
"    UPDATE rating_plans SET status = 3, updated_at = v_now WHERE line_of_business = 1 AND formula_key = 'GL_v1';",
"",
"    -- Global GL additional-interest rates (carrier_id NULL = all GL carriers).",
'    IF NOT EXISTS (SELECT 1 FROM carrier_additional_interest_rates WHERE "CarrierId" IS NULL AND "LineOfBusiness" = 1 AND "IsDeleted" = false) THEN',
'        INSERT INTO carrier_additional_interest_rates ("Id", "CarrierId", "LineOfBusiness", "CoverageType", "ChargeMethod", "PerInterestAmount", "BlanketAmount", "MinimumCharge", "MaximumCharge", "State", "EffectiveDate", "ExpirationDate", "IsActive", "CreatedAt", "UpdatedAt", "IsDeleted")',
"        VALUES",
"          (gen_random_uuid(), NULL, 1, 0, 2, 50, NULL, NULL, NULL, NULL, NULL, NULL, true, v_now, v_now, false),",
"          (gen_random_uuid(), NULL, 1, 0, 3, NULL, 250, NULL, NULL, NULL, NULL, NULL, true, v_now, v_now, false),",
"          (gen_random_uuid(), NULL, 1, 2, 2, 50, NULL, NULL, NULL, NULL, NULL, NULL, true, v_now, v_now, false),",
"          (gen_random_uuid(), NULL, 1, 2, 3, NULL, 250, NULL, NULL, NULL, NULL, NULL, true, v_now, v_now, false),",
"          (gen_random_uuid(), NULL, 1, 3, 3, NULL, 250, NULL, NULL, NULL, NULL, NULL, true, v_now, v_now, false);",
"    END IF;",
"END $$;",
""])

down = "\n".join([
"",
"DO $$",
"DECLARE",
"    v_plan_id    uuid;",
"    v_version_id uuid;",
"    v_v1_version uuid;",
"    v_now timestamp with time zone := now();",
"BEGIN",
"    SELECT id INTO v_plan_id FROM rating_plans WHERE line_of_business = 1 AND formula_key = 'GL_v2';",
"    IF v_plan_id IS NOT NULL THEN",
"        SELECT id INTO v_version_id FROM rating_plan_versions WHERE rating_plan_id = v_plan_id AND version_number = 1;",
"        UPDATE rating_plans SET status = 2, updated_at = v_now WHERE line_of_business = 1 AND formula_key = 'GL_v1';",
"        SELECT rpv.id INTO v_v1_version FROM rating_plan_versions rpv JOIN rating_plans rp ON rp.id = rpv.rating_plan_id",
"        WHERE rp.line_of_business = 1 AND rp.formula_key = 'GL_v1' AND rpv.version_number = 1;",
"        IF v_v1_version IS NOT NULL THEN",
"            UPDATE carrier_rating_assignments SET rating_plan_version_id = v_v1_version, updated_at = v_now WHERE rating_plan_version_id = v_version_id;",
"        END IF;",
"        DELETE FROM factor_rows fr USING factor_tables ft WHERE fr.factor_table_id = ft.id AND ft.rating_plan_version_id = v_version_id;",
"        DELETE FROM factor_tables WHERE rating_plan_version_id = v_version_id;",
"        DELETE FROM rating_plan_versions WHERE rating_plan_id = v_plan_id;",
"        DELETE FROM rating_plans WHERE id = v_plan_id;",
"    END IF;",
'    DELETE FROM carrier_additional_interest_rates WHERE "CarrierId" IS NULL AND "LineOfBusiness" = 1;',
"END $$;",
""])

def esc(s): return s.replace('"','""')
cs = (
'using Microsoft.EntityFrameworkCore.Migrations;\n\n'
'#nullable disable\n\n'
'namespace SIMS.Infrastructure.Migrations\n{\n'
'    /// <inheritdoc />\n'
'    public partial class GL_v2_RatingSeed : Migration\n    {\n'
'        /// <inheritdoc />\n'
'        protected override void Up(MigrationBuilder migrationBuilder)\n        {\n'
'            migrationBuilder.Sql(@"'+esc(up)+'");\n'
'        }\n\n'
'        /// <inheritdoc />\n'
'        protected override void Down(MigrationBuilder migrationBuilder)\n        {\n'
'            migrationBuilder.Sql(@"'+esc(down)+'");\n'
'        }\n    }\n}\n'
)
open(mig,"w",encoding="utf-8").write(cs)
print("wrote %s" % mig)
print("rows: GL_CLASS=%d LC334=%d LC336=%d ILF=%d PARAMS=%d LL=%d" % (len(cls_rows),len(lc334_rows),len(lc336_rows),len(ilf_rows),len(par_rows),len(ll_rows)))
