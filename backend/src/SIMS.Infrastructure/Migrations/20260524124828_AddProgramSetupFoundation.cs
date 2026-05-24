using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramSetupFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProgramConfigurationId",
                table: "fee_rule_versions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "program_carriers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramConfigurationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CarrierId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpirationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_program_carriers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_program_carriers_carriers_CarrierId",
                        column: x => x.CarrierId,
                        principalTable: "carriers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_program_carriers_program_configurations_ProgramConfiguratio~",
                        column: x => x.ProgramConfigurationId,
                        principalTable: "program_configurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "program_carrier_lines_of_business",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramCarrierId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineOfBusiness = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpirationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_program_carrier_lines_of_business", x => x.Id);
                    table.ForeignKey(
                        name: "FK_program_carrier_lines_of_business_program_carriers_ProgramC~",
                        column: x => x.ProgramCarrierId,
                        principalTable: "program_carriers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "program_carrier_lob_states",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramCarrierLineOfBusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    StateCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpirationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_program_carrier_lob_states", x => x.Id);
                    table.ForeignKey(
                        name: "FK_program_carrier_lob_states_program_carrier_lines_of_busines~",
                        column: x => x.ProgramCarrierLineOfBusinessId,
                        principalTable: "program_carrier_lines_of_business",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_fee_rule_program_carrier_lob_lookup",
                table: "fee_rule_versions",
                columns: new[] { "FeeDefinitionId", "ProgramConfigurationId", "CarrierId", "LineOfBusiness", "StateCode", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_fee_rule_versions_ProgramConfigurationId",
                table: "fee_rule_versions",
                column: "ProgramConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_program_carrier_lines_of_business_IsActive",
                table: "program_carrier_lines_of_business",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_program_carrier_lines_of_business_ProgramCarrierId_LineOfBu~",
                table: "program_carrier_lines_of_business",
                columns: new[] { "ProgramCarrierId", "LineOfBusiness" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_program_carrier_lob_states_IsActive",
                table: "program_carrier_lob_states",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_program_carrier_lob_states_ProgramCarrierLineOfBusinessId_S~",
                table: "program_carrier_lob_states",
                columns: new[] { "ProgramCarrierLineOfBusinessId", "StateCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_program_carriers_CarrierId",
                table: "program_carriers",
                column: "CarrierId");

            migrationBuilder.CreateIndex(
                name: "IX_program_carriers_IsActive",
                table: "program_carriers",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_program_carriers_ProgramConfigurationId_CarrierId",
                table: "program_carriers",
                columns: new[] { "ProgramConfigurationId", "CarrierId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_fee_rule_versions_program_configurations_ProgramConfigurati~",
                table: "fee_rule_versions",
                column: "ProgramConfigurationId",
                principalTable: "program_configurations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_fee_rule_versions_program_configurations_ProgramConfigurati~",
                table: "fee_rule_versions");

            migrationBuilder.DropTable(
                name: "program_carrier_lob_states");

            migrationBuilder.DropTable(
                name: "program_carrier_lines_of_business");

            migrationBuilder.DropTable(
                name: "program_carriers");

            migrationBuilder.DropIndex(
                name: "ix_fee_rule_program_carrier_lob_lookup",
                table: "fee_rule_versions");

            migrationBuilder.DropIndex(
                name: "IX_fee_rule_versions_ProgramConfigurationId",
                table: "fee_rule_versions");

            migrationBuilder.DropColumn(
                name: "ProgramConfigurationId",
                table: "fee_rule_versions");
        }
    }
}
