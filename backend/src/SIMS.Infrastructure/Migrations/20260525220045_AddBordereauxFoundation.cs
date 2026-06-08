using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBordereauxFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bordereaux_profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProgramConfigurationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CarrierId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineOfBusiness = table.Column<int>(type: "integer", nullable: true),
                    StateCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    ReportType = table.Column<int>(type: "integer", nullable: false),
                    Frequency = table.Column<int>(type: "integer", nullable: false),
                    OutputFormat = table.Column<int>(type: "integer", nullable: false),
                    DateBasis = table.Column<int>(type: "integer", nullable: false),
                    RequiresAccountCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    RequiredTabsJson = table.Column<string>(type: "jsonb", nullable: false),
                    RequiredColumnsJson = table.Column<string>(type: "jsonb", nullable: false),
                    MappingRulesJson = table.Column<string>(type: "jsonb", nullable: false),
                    StaticValuesJson = table.Column<string>(type: "jsonb", nullable: false),
                    ValidationRulesJson = table.Column<string>(type: "jsonb", nullable: false),
                    IncludedTransactionTypesJson = table.Column<string>(type: "jsonb", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bordereaux_profiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bordereaux_profiles_carriers_CarrierId",
                        column: x => x.CarrierId,
                        principalTable: "carriers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bordereaux_profiles_program_configurations_ProgramConfigura~",
                        column: x => x.ProgramConfigurationId,
                        principalTable: "program_configurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bordereaux_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BordereauxProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReconciliationStatus = table.Column<int>(type: "integer", nullable: false),
                    GeneratedById = table.Column<Guid>(type: "uuid", nullable: true),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LondonBordereauxBlobPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LondonBordereauxFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    LondonBordereauxContentType = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    AccountCurrentBlobPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AccountCurrentFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    AccountCurrentContentType = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    BordereauxRowCount = table.Column<int>(type: "integer", nullable: false),
                    AccountCurrentRowCount = table.Column<int>(type: "integer", nullable: false),
                    DetailRowCountsJson = table.Column<string>(type: "jsonb", nullable: false),
                    ValidationSummaryJson = table.Column<string>(type: "jsonb", nullable: false),
                    ReconciliationSummaryJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bordereaux_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bordereaux_runs_bordereaux_profiles_BordereauxProfileId",
                        column: x => x.BordereauxProfileId,
                        principalTable: "bordereaux_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_bordereaux_runs_users_GeneratedById",
                        column: x => x.GeneratedById,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bordereaux_profiles_CarrierId",
                table: "bordereaux_profiles",
                column: "CarrierId");

            migrationBuilder.CreateIndex(
                name: "IX_bordereaux_profiles_IsActive",
                table: "bordereaux_profiles",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_bordereaux_profiles_ProgramConfigurationId_CarrierId_Report~",
                table: "bordereaux_profiles",
                columns: new[] { "ProgramConfigurationId", "CarrierId", "ReportType", "LineOfBusiness", "StateCode", "IsActive" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bordereaux_runs_BordereauxProfileId_PeriodStart_PeriodEnd",
                table: "bordereaux_runs",
                columns: new[] { "BordereauxProfileId", "PeriodStart", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_bordereaux_runs_GeneratedById",
                table: "bordereaux_runs",
                column: "GeneratedById");

            migrationBuilder.CreateIndex(
                name: "IX_bordereaux_runs_ReconciliationStatus",
                table: "bordereaux_runs",
                column: "ReconciliationStatus");

            migrationBuilder.CreateIndex(
                name: "IX_bordereaux_runs_Status",
                table: "bordereaux_runs",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bordereaux_runs");

            migrationBuilder.DropTable(
                name: "bordereaux_profiles");
        }
    }
}
