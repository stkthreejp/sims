using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIntermediaryAndRiskLocationBdxMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ZipCode",
                table: "submission_locations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "submission_locations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "submission_locations",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "County",
                table: "submission_locations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPrimary",
                table: "submission_locations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "submission_locations",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "intermediaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ReferenceNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    AddressLine1 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AddressLine2 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ZipCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Country = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    BankName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BankAccountName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BankAccountLast4 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    BankRoutingNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    BankSwiftCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    BankInstructions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intermediaries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "intermediary_program_carrier_lob_setups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IntermediaryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramConfigurationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CarrierId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineOfBusiness = table.Column<int>(type: "integer", nullable: true),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpirationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    BrokerageRate = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    CreatePayable = table.Column<bool>(type: "boolean", nullable: false),
                    PayablePayeeId = table.Column<long>(type: "bigint", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intermediary_program_carrier_lob_setups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_intermediary_program_carrier_lob_setups_carriers_CarrierId",
                        column: x => x.CarrierId,
                        principalTable: "carriers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_intermediary_program_carrier_lob_setups_intermediaries_Inte~",
                        column: x => x.IntermediaryId,
                        principalTable: "intermediaries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_intermediary_program_carrier_lob_setups_payees_PayablePayee~",
                        column: x => x.PayablePayeeId,
                        principalTable: "payees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_intermediary_program_carrier_lob_setups_program_configurati~",
                        column: x => x.ProgramConfigurationId,
                        principalTable: "program_configurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_intermediaries_IsActive",
                table: "intermediaries",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_intermediaries_Name",
                table: "intermediaries",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_intermediary_program_carrier_lob_setups_CarrierId",
                table: "intermediary_program_carrier_lob_setups",
                column: "CarrierId");

            migrationBuilder.CreateIndex(
                name: "IX_intermediary_program_carrier_lob_setups_IntermediaryId",
                table: "intermediary_program_carrier_lob_setups",
                column: "IntermediaryId");

            migrationBuilder.CreateIndex(
                name: "IX_intermediary_program_carrier_lob_setups_PayablePayeeId",
                table: "intermediary_program_carrier_lob_setups",
                column: "PayablePayeeId");

            migrationBuilder.CreateIndex(
                name: "ix_intermediary_setup_lookup",
                table: "intermediary_program_carrier_lob_setups",
                columns: new[] { "ProgramConfigurationId", "CarrierId", "LineOfBusiness", "EffectiveDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "intermediary_program_carrier_lob_setups");

            migrationBuilder.DropTable(
                name: "intermediaries");

            migrationBuilder.DropColumn(
                name: "City",
                table: "submission_locations");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "submission_locations");

            migrationBuilder.DropColumn(
                name: "County",
                table: "submission_locations");

            migrationBuilder.DropColumn(
                name: "IsPrimary",
                table: "submission_locations");

            migrationBuilder.DropColumn(
                name: "State",
                table: "submission_locations");

            migrationBuilder.AlterColumn<string>(
                name: "ZipCode",
                table: "submission_locations",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);
        }
    }
}
