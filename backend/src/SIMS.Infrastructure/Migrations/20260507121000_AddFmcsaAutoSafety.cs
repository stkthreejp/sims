using System;
using SIMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260507121000_AddFmcsaAutoSafety")]
    public partial class AddFmcsaAutoSafety : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "us_dot_number",
                table: "insureds",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "fmcsa_carrier_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    us_dot_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    snapshot_month = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    legal_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    dba_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    physical_address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    state = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    zip_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    power_units = table.Column<int>(type: "integer", nullable: true),
                    driver_count = table.Column<int>(type: "integer", nullable: true),
                    mileage = table.Column<int>(type: "integer", nullable: true),
                    mileage_year = table.Column<int>(type: "integer", nullable: true),
                    operation_classification = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    carrier_operation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    imported_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table => table.PrimaryKey("pk_fmcsa_carrier_snapshots", x => x.id));

            migrationBuilder.CreateTable(
                name: "fmcsa_crashes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    us_dot_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    report_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    crash_date = table.Column<DateOnly>(type: "date", nullable: false),
                    state = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    tow_away = table.Column<bool>(type: "boolean", nullable: false),
                    injury = table.Column<bool>(type: "boolean", nullable: false),
                    fatality = table.Column<bool>(type: "boolean", nullable: false),
                    severity_weight = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    time_weight = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    imported_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table => table.PrimaryKey("pk_fmcsa_crashes", x => x.id));

            migrationBuilder.CreateTable(
                name: "fmcsa_inspections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    us_dot_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    report_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    inspection_date = table.Column<DateOnly>(type: "date", nullable: false),
                    state = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    inspection_level = table.Column<int>(type: "integer", nullable: true),
                    driver_out_of_service = table.Column<bool>(type: "boolean", nullable: false),
                    vehicle_out_of_service = table.Column<bool>(type: "boolean", nullable: false),
                    driver_violation_count = table.Column<int>(type: "integer", nullable: false),
                    vehicle_violation_count = table.Column<int>(type: "integer", nullable: false),
                    imported_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table => table.PrimaryKey("pk_fmcsa_inspections", x => x.id));

            migrationBuilder.CreateTable(
                name: "fmcsa_scoring_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    us_dot_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    snapshot_month = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    methodology_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    generated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table => table.PrimaryKey("pk_fmcsa_scoring_runs", x => x.id));

            migrationBuilder.CreateTable(
                name: "fmcsa_violations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fmcsa_inspection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    us_dot_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    report_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    violation_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    basic = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    violation_group = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    is_out_of_service = table.Column<bool>(type: "boolean", nullable: false),
                    is_driver_disqualifying = table.Column<bool>(type: "boolean", nullable: false),
                    severity_weight = table.Column<int>(type: "integer", nullable: false),
                    time_weight = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    imported_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fmcsa_violations", x => x.id);
                    table.ForeignKey(
                        name: "fk_fmcsa_violations_fmcsa_inspections_fmcsa_inspection_id",
                        column: x => x.fmcsa_inspection_id,
                        principalTable: "fmcsa_inspections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fmcsa_basic_scores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fmcsa_scoring_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    basic = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    measure = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    percentile = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    is_prioritized = table.Column<bool>(type: "boolean", nullable: false),
                    event_count = table.Column<int>(type: "integer", nullable: false),
                    out_of_service_count = table.Column<int>(type: "integer", nullable: false),
                    trend_direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fmcsa_basic_scores", x => x.id);
                    table.ForeignKey(
                        name: "fk_fmcsa_basic_scores_fmcsa_scoring_runs_fmcsa_scoring_run_id",
                        column: x => x.fmcsa_scoring_run_id,
                        principalTable: "fmcsa_scoring_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_fmcsa_basic_scores_fmcsa_scoring_run_id_basic",
                table: "fmcsa_basic_scores",
                columns: new[] { "fmcsa_scoring_run_id", "basic" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fmcsa_carrier_snapshots_us_dot_number_snapshot_month",
                table: "fmcsa_carrier_snapshots",
                columns: new[] { "us_dot_number", "snapshot_month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fmcsa_crashes_us_dot_number_crash_date",
                table: "fmcsa_crashes",
                columns: new[] { "us_dot_number", "crash_date" });

            migrationBuilder.CreateIndex(
                name: "ix_fmcsa_crashes_us_dot_number_report_number",
                table: "fmcsa_crashes",
                columns: new[] { "us_dot_number", "report_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fmcsa_inspections_us_dot_number_inspection_date",
                table: "fmcsa_inspections",
                columns: new[] { "us_dot_number", "inspection_date" });

            migrationBuilder.CreateIndex(
                name: "ix_fmcsa_inspections_us_dot_number_report_number",
                table: "fmcsa_inspections",
                columns: new[] { "us_dot_number", "report_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fmcsa_scoring_runs_us_dot_number_snapshot_month",
                table: "fmcsa_scoring_runs",
                columns: new[] { "us_dot_number", "snapshot_month" });

            migrationBuilder.CreateIndex(
                name: "ix_fmcsa_violations_fmcsa_inspection_id",
                table: "fmcsa_violations",
                column: "fmcsa_inspection_id");

            migrationBuilder.CreateIndex(
                name: "ix_fmcsa_violations_us_dot_number_report_number",
                table: "fmcsa_violations",
                columns: new[] { "us_dot_number", "report_number" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "fmcsa_basic_scores");
            migrationBuilder.DropTable(name: "fmcsa_carrier_snapshots");
            migrationBuilder.DropTable(name: "fmcsa_crashes");
            migrationBuilder.DropTable(name: "fmcsa_violations");
            migrationBuilder.DropTable(name: "fmcsa_scoring_runs");
            migrationBuilder.DropTable(name: "fmcsa_inspections");
            migrationBuilder.DropColumn(name: "us_dot_number", table: "insureds");
        }
    }
}
