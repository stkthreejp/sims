using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBordereauxRunAuditSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProfileSnapshotJson",
                table: "bordereaux_runs",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<int>(
                name: "RunNumber",
                table: "bordereaux_runs",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "SourceRowsSnapshotJson",
                table: "bordereaux_runs",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateIndex(
                name: "IX_bordereaux_runs_BordereauxProfileId_PeriodStart_PeriodEnd_R~",
                table: "bordereaux_runs",
                columns: new[] { "BordereauxProfileId", "PeriodStart", "PeriodEnd", "RunNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_bordereaux_runs_BordereauxProfileId_PeriodStart_PeriodEnd_R~",
                table: "bordereaux_runs");

            migrationBuilder.DropColumn(
                name: "ProfileSnapshotJson",
                table: "bordereaux_runs");

            migrationBuilder.DropColumn(
                name: "RunNumber",
                table: "bordereaux_runs");

            migrationBuilder.DropColumn(
                name: "SourceRowsSnapshotJson",
                table: "bordereaux_runs");
        }
    }
}
