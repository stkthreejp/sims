using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubmissionLobFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_inbound_emails_GraphMessageId",
                table: "inbound_emails");

            migrationBuilder.AddColumn<string>(
                name: "DescriptionOfOperations",
                table: "submissions",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MedicalPaymentsLimit",
                table: "quotes",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UninsuredMotoristLimit",
                table: "quotes",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Dba",
                table: "insureds",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EntityType",
                table: "insureds",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "YearsInBusiness",
                table: "insureds",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "submission_drivers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverNumber = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    LicenseNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LicenseState = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    DateHired = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submission_drivers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_submission_drivers_submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "submission_equipment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemNumber = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: true),
                    Make = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SerialNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submission_equipment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_submission_equipment_submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "submission_gl_classifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationNumber = table.Column<int>(type: "integer", nullable: false),
                    ClassCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PremiumBasis = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Exposure = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submission_gl_classifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_submission_gl_classifications_submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "submission_gl_coverages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    GeneralAggregate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ProductsCompletedOps = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    EachOccurrence = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    PersonalAndAdvInjury = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    DamageToRentedPremises = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MedicalExpense = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    TotalSubcontractorCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submission_gl_coverages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_submission_gl_coverages_submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "submission_im_coverages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduledEquipmentTotalLimit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    UnscheduledEquipmentLimit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MaximumValueAnyOneItem = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Deductible = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CoinsurancePercentage = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submission_im_coverages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_submission_im_coverages_submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "submission_locations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationNumber = table.Column<int>(type: "integer", nullable: false),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ZipCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submission_locations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_submission_locations_submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "submission_prior_carriers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineOfBusiness = table.Column<string>(type: "text", nullable: true),
                    CarrierName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PolicyNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ExpirationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Premium = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submission_prior_carriers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_submission_prior_carriers_submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "submission_supplementals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommoditiesHauled = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TerminalLocations = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SafetyProgramInPlace = table.Column<bool>(type: "boolean", nullable: false),
                    FilingsRequired = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OwnerOperator = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submission_supplementals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_submission_supplementals_submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "submission_vehicles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitNumber = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: true),
                    Make = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Vin = table.Column<string>(type: "character varying(17)", maxLength: 17, nullable: true),
                    Gvw = table.Column<int>(type: "integer", nullable: true),
                    VehicleClass = table.Column<int>(type: "integer", nullable: false),
                    GaragingZip = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Radius = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submission_vehicles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_submission_vehicles_submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_inbound_emails_GraphMessageId",
                table: "inbound_emails",
                column: "GraphMessageId",
                unique: true,
                filter: "\"GraphMessageId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_submission_drivers_SubmissionId",
                table: "submission_drivers",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_submission_equipment_SubmissionId",
                table: "submission_equipment",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_submission_gl_classifications_SubmissionId",
                table: "submission_gl_classifications",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_submission_gl_coverages_SubmissionId",
                table: "submission_gl_coverages",
                column: "SubmissionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_submission_im_coverages_SubmissionId",
                table: "submission_im_coverages",
                column: "SubmissionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_submission_locations_SubmissionId",
                table: "submission_locations",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_submission_prior_carriers_SubmissionId",
                table: "submission_prior_carriers",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_submission_supplementals_SubmissionId",
                table: "submission_supplementals",
                column: "SubmissionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_submission_vehicles_SubmissionId",
                table: "submission_vehicles",
                column: "SubmissionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "submission_drivers");

            migrationBuilder.DropTable(
                name: "submission_equipment");

            migrationBuilder.DropTable(
                name: "submission_gl_classifications");

            migrationBuilder.DropTable(
                name: "submission_gl_coverages");

            migrationBuilder.DropTable(
                name: "submission_im_coverages");

            migrationBuilder.DropTable(
                name: "submission_locations");

            migrationBuilder.DropTable(
                name: "submission_prior_carriers");

            migrationBuilder.DropTable(
                name: "submission_supplementals");

            migrationBuilder.DropTable(
                name: "submission_vehicles");

            migrationBuilder.DropIndex(
                name: "IX_inbound_emails_GraphMessageId",
                table: "inbound_emails");

            migrationBuilder.DropColumn(
                name: "DescriptionOfOperations",
                table: "submissions");

            migrationBuilder.DropColumn(
                name: "MedicalPaymentsLimit",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "UninsuredMotoristLimit",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "Dba",
                table: "insureds");

            migrationBuilder.DropColumn(
                name: "EntityType",
                table: "insureds");

            migrationBuilder.DropColumn(
                name: "YearsInBusiness",
                table: "insureds");

            migrationBuilder.CreateIndex(
                name: "IX_inbound_emails_GraphMessageId",
                table: "inbound_emails",
                column: "GraphMessageId",
                unique: true,
                filter: "\"graph_message_id\" IS NOT NULL");
        }
    }
}
