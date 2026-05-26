using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSurplusLinesStateSetup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "surplus_lines_state_setups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StateCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    ProgramConfigurationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CarrierId = table.Column<Guid>(type: "uuid", nullable: true),
                    LineOfBusiness = table.Column<int>(type: "integer", nullable: true),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpirationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    FilingRequired = table.Column<bool>(type: "boolean", nullable: false),
                    LicenseHolderType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FilingBrokerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LicenseNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LicenseState = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    BrokerAddressLine1 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BrokerAddressLine2 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BrokerCity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BrokerState = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    BrokerZipCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BrokerCountry = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    StampingWording = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RequiredNoticeText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PaperworkNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FilingNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SurplusLinesTaxFeeDefinitionId = table.Column<long>(type: "bigint", nullable: true),
                    StampingFeeDefinitionId = table.Column<long>(type: "bigint", nullable: true),
                    FilingFeeDefinitionId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_surplus_lines_state_setups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_surplus_lines_state_setups_carriers_CarrierId",
                        column: x => x.CarrierId,
                        principalTable: "carriers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_surplus_lines_state_setups_fee_definitions_FilingFeeDefinit~",
                        column: x => x.FilingFeeDefinitionId,
                        principalTable: "fee_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_surplus_lines_state_setups_fee_definitions_StampingFeeDefin~",
                        column: x => x.StampingFeeDefinitionId,
                        principalTable: "fee_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_surplus_lines_state_setups_fee_definitions_SurplusLinesTaxF~",
                        column: x => x.SurplusLinesTaxFeeDefinitionId,
                        principalTable: "fee_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_surplus_lines_state_setups_program_configurations_ProgramCo~",
                        column: x => x.ProgramConfigurationId,
                        principalTable: "program_configurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_surplus_lines_state_setup_lookup",
                table: "surplus_lines_state_setups",
                columns: new[] { "StateCode", "ProgramConfigurationId", "CarrierId", "LineOfBusiness", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_surplus_lines_state_setups_CarrierId",
                table: "surplus_lines_state_setups",
                column: "CarrierId");

            migrationBuilder.CreateIndex(
                name: "IX_surplus_lines_state_setups_FilingFeeDefinitionId",
                table: "surplus_lines_state_setups",
                column: "FilingFeeDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_surplus_lines_state_setups_ProgramConfigurationId",
                table: "surplus_lines_state_setups",
                column: "ProgramConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_surplus_lines_state_setups_StampingFeeDefinitionId",
                table: "surplus_lines_state_setups",
                column: "StampingFeeDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_surplus_lines_state_setups_SurplusLinesTaxFeeDefinitionId",
                table: "surplus_lines_state_setups",
                column: "SurplusLinesTaxFeeDefinitionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "surplus_lines_state_setups");
        }
    }
}
