using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuotePolicyFormSelections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "quote_policy_form_selections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyFormTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    SequenceOrder = table.Column<int>(type: "integer", nullable: false),
                    FormType = table.Column<int>(type: "integer", nullable: false),
                    IsIncluded = table.Column<bool>(type: "boolean", nullable: false),
                    IsSystemGenerated = table.Column<bool>(type: "boolean", nullable: false),
                    TriggerConditionJson = table.Column<string>(type: "jsonb", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quote_policy_form_selections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_quote_policy_form_selections_policy_form_templates_PolicyFo~",
                        column: x => x.PolicyFormTemplateId,
                        principalTable: "policy_form_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_quote_policy_form_selections_quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "quotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_quote_policy_form_selections_PolicyFormTemplateId",
                table: "quote_policy_form_selections",
                column: "PolicyFormTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_quote_policy_form_selections_QuoteId_PolicyFormTemplateId_I~",
                table: "quote_policy_form_selections",
                columns: new[] { "QuoteId", "PolicyFormTemplateId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_quote_policy_form_selections_QuoteId_SequenceOrder_IsDeleted",
                table: "quote_policy_form_selections",
                columns: new[] { "QuoteId", "SequenceOrder", "IsDeleted" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quote_policy_form_selections");
        }
    }
}
