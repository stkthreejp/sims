using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPolicyFormsLibrary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "policy_form_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FormNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    EditionDate = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DocumentType = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StoragePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsFillable = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_policy_form_templates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "policy_package_configurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CarrierId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineOfBusiness = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_policy_package_configurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_policy_package_configurations_carriers_CarrierId",
                        column: x => x.CarrierId,
                        principalTable: "carriers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "policy_form_field_mappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyFormTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    PdfFieldName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    DataPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Format = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_policy_form_field_mappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_policy_form_field_mappings_policy_form_templates_PolicyForm~",
                        column: x => x.PolicyFormTemplateId,
                        principalTable: "policy_form_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "policy_package_forms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyPackageConfigurationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyFormTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    SequenceOrder = table.Column<int>(type: "integer", nullable: false),
                    FormType = table.Column<int>(type: "integer", nullable: false),
                    TriggerConditionJson = table.Column<string>(type: "jsonb", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_policy_package_forms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_policy_package_forms_policy_form_templates_PolicyFormTempla~",
                        column: x => x.PolicyFormTemplateId,
                        principalTable: "policy_form_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_policy_package_forms_policy_package_configurations_PolicyPa~",
                        column: x => x.PolicyPackageConfigurationId,
                        principalTable: "policy_package_configurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_policy_form_field_mappings_PolicyFormTemplateId_PdfFieldNam~",
                table: "policy_form_field_mappings",
                columns: new[] { "PolicyFormTemplateId", "PdfFieldName", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_policy_form_templates_FormNumber_EditionDate_IsDeleted",
                table: "policy_form_templates",
                columns: new[] { "FormNumber", "EditionDate", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_policy_package_configurations_CarrierId_LineOfBusiness_Stat~",
                table: "policy_package_configurations",
                columns: new[] { "CarrierId", "LineOfBusiness", "State", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_policy_package_forms_PolicyFormTemplateId",
                table: "policy_package_forms",
                column: "PolicyFormTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_policy_package_forms_PolicyPackageConfigurationId_SequenceO~",
                table: "policy_package_forms",
                columns: new[] { "PolicyPackageConfigurationId", "SequenceOrder", "IsDeleted" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "policy_form_field_mappings");

            migrationBuilder.DropTable(
                name: "policy_package_forms");

            migrationBuilder.DropTable(
                name: "policy_form_templates");

            migrationBuilder.DropTable(
                name: "policy_package_configurations");
        }
    }
}
