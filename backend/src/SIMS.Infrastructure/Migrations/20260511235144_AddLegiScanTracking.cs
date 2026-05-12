using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLegiScanTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "legiscan_tracked_bills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BillId = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    BillNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ChangeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: true),
                    StatusDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Stance = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RawBillJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_legiscan_tracked_bills", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_legiscan_tracked_bills_BillId",
                table: "legiscan_tracked_bills",
                column: "BillId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_legiscan_tracked_bills_ChangeHash",
                table: "legiscan_tracked_bills",
                column: "ChangeHash");

            migrationBuilder.CreateIndex(
                name: "IX_legiscan_tracked_bills_IsActive",
                table: "legiscan_tracked_bills",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_legiscan_tracked_bills_State_BillNumber",
                table: "legiscan_tracked_bills",
                columns: new[] { "State", "BillNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "legiscan_tracked_bills");
        }
    }
}
