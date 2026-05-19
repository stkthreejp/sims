using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiModelSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_model_registry",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ModelId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    AllowedUseCases = table.Column<string[]>(type: "text[]", nullable: false),
                    DefaultUseCases = table.Column<string[]>(type: "text[]", nullable: false),
                    CostNotes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RetirementReviewDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_model_registry", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ai_model_setting_audit_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UseCase = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PreviousAiModelRegistryId = table.Column<Guid>(type: "uuid", nullable: true),
                    NewAiModelRegistryId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousPromptVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    NewPromptVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangeReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_model_setting_audit_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ai_use_case_model_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UseCase = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    AiModelRegistryId = table.Column<Guid>(type: "uuid", nullable: false),
                    PromptVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_use_case_model_settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_use_case_model_settings_ai_model_registry_AiModelRegistr~",
                        column: x => x.AiModelRegistryId,
                        principalTable: "ai_model_registry",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_model_registry_Provider_ModelId",
                table: "ai_model_registry",
                columns: new[] { "Provider", "ModelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_model_setting_audit_logs_CreatedAt",
                table: "ai_model_setting_audit_logs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ai_model_setting_audit_logs_UseCase",
                table: "ai_model_setting_audit_logs",
                column: "UseCase");

            migrationBuilder.CreateIndex(
                name: "IX_ai_use_case_model_settings_AiModelRegistryId",
                table: "ai_use_case_model_settings",
                column: "AiModelRegistryId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_use_case_model_settings_UseCase",
                table: "ai_use_case_model_settings",
                column: "UseCase",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_model_setting_audit_logs");

            migrationBuilder.DropTable(
                name: "ai_use_case_model_settings");

            migrationBuilder.DropTable(
                name: "ai_model_registry");
        }
    }
}
