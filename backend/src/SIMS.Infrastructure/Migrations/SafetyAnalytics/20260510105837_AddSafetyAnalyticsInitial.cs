using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations.SafetyAnalytics
{
    /// <inheritdoc />
    public partial class AddSafetyAnalyticsInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fmcsa_analytics_import_batches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_month = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    source_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rows_imported = table.Column<int>(type: "integer", nullable: false),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fmcsa_analytics_import_batches", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "fmcsa_basic_peer_measures",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_month = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    us_dot_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    basic = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    official_measure = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    sims_measure = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    inspection_with_violation_count = table.Column<int>(type: "integer", nullable: false),
                    violation_count = table.Column<int>(type: "integer", nullable: false),
                    out_of_service_count = table.Column<int>(type: "integer", nullable: false),
                    weighted_violation_score = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    exposure = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    peer_group_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    peer_rank = table.Column<int>(type: "integer", nullable: true),
                    peer_population = table.Column<int>(type: "integer", nullable: true),
                    sims_percentile = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fmcsa_basic_peer_measures", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "fmcsa_carrier_peer_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_month = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    us_dot_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    legal_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    state = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    power_units = table.Column<int>(type: "integer", nullable: true),
                    driver_count = table.Column<int>(type: "integer", nullable: true),
                    mileage = table.Column<int>(type: "integer", nullable: true),
                    mileage_year = table.Column<int>(type: "integer", nullable: true),
                    inspection_count = table.Column<int>(type: "integer", nullable: false),
                    driver_inspection_count = table.Column<int>(type: "integer", nullable: false),
                    vehicle_inspection_count = table.Column<int>(type: "integer", nullable: false),
                    driver_oos_inspection_count = table.Column<int>(type: "integer", nullable: false),
                    vehicle_oos_inspection_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fmcsa_carrier_peer_snapshots", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fmcsa_analytics_import_batches_snapshot_month_source_name",
                table: "fmcsa_analytics_import_batches",
                columns: new[] { "snapshot_month", "source_name" });

            migrationBuilder.CreateIndex(
                name: "IX_fmcsa_basic_peer_measures_snapshot_month_basic_peer_group_k~",
                table: "fmcsa_basic_peer_measures",
                columns: new[] { "snapshot_month", "basic", "peer_group_key", "sims_measure" });

            migrationBuilder.CreateIndex(
                name: "IX_fmcsa_basic_peer_measures_snapshot_month_us_dot_number_basic",
                table: "fmcsa_basic_peer_measures",
                columns: new[] { "snapshot_month", "us_dot_number", "basic" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fmcsa_carrier_peer_snapshots_snapshot_month_power_units",
                table: "fmcsa_carrier_peer_snapshots",
                columns: new[] { "snapshot_month", "power_units" });

            migrationBuilder.CreateIndex(
                name: "IX_fmcsa_carrier_peer_snapshots_snapshot_month_us_dot_number",
                table: "fmcsa_carrier_peer_snapshots",
                columns: new[] { "snapshot_month", "us_dot_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fmcsa_analytics_import_batches");

            migrationBuilder.DropTable(
                name: "fmcsa_basic_peer_measures");

            migrationBuilder.DropTable(
                name: "fmcsa_carrier_peer_snapshots");
        }
    }
}
