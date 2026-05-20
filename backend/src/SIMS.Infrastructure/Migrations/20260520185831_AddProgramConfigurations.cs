using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramConfigurations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProgramId",
                table: "underwriting_guideline_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProgramId",
                table: "underwriting_guideline_controls",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "program_configurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    CarrierId = table.Column<Guid>(type: "uuid", nullable: true),
                    LineOfBusiness = table.Column<int>(type: "integer", nullable: false),
                    StateCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_program_configurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_program_configurations_carriers_CarrierId",
                        column: x => x.CarrierId,
                        principalTable: "carriers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_underwriting_guideline_documents_ProgramId_Version",
                table: "underwriting_guideline_documents",
                columns: new[] { "ProgramId", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_underwriting_guideline_controls_ProgramId",
                table: "underwriting_guideline_controls",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_underwriting_guideline_controls_Status_ProgramId",
                table: "underwriting_guideline_controls",
                columns: new[] { "Status", "ProgramId" });

            migrationBuilder.CreateIndex(
                name: "IX_program_configurations_CarrierId_LineOfBusiness_StateCode_I~",
                table: "program_configurations",
                columns: new[] { "CarrierId", "LineOfBusiness", "StateCode", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_program_configurations_Code",
                table: "program_configurations",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_underwriting_guideline_controls_program_configurations_Prog~",
                table: "underwriting_guideline_controls",
                column: "ProgramId",
                principalTable: "program_configurations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_underwriting_guideline_documents_program_configurations_Pro~",
                table: "underwriting_guideline_documents",
                column: "ProgramId",
                principalTable: "program_configurations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_underwriting_guideline_controls_program_configurations_Prog~",
                table: "underwriting_guideline_controls");

            migrationBuilder.DropForeignKey(
                name: "FK_underwriting_guideline_documents_program_configurations_Pro~",
                table: "underwriting_guideline_documents");

            migrationBuilder.DropTable(
                name: "program_configurations");

            migrationBuilder.DropIndex(
                name: "IX_underwriting_guideline_documents_ProgramId_Version",
                table: "underwriting_guideline_documents");

            migrationBuilder.DropIndex(
                name: "IX_underwriting_guideline_controls_ProgramId",
                table: "underwriting_guideline_controls");

            migrationBuilder.DropIndex(
                name: "IX_underwriting_guideline_controls_Status_ProgramId",
                table: "underwriting_guideline_controls");

            migrationBuilder.DropColumn(
                name: "ProgramId",
                table: "underwriting_guideline_documents");

            migrationBuilder.DropColumn(
                name: "ProgramId",
                table: "underwriting_guideline_controls");
        }
    }
}
