using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPolicyFormDocumentTemplateRef : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DocumentTemplateId",
                table: "policy_form_templates",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_policy_form_templates_DocumentTemplateId",
                table: "policy_form_templates",
                column: "DocumentTemplateId");

            migrationBuilder.AddForeignKey(
                name: "FK_policy_form_templates_DocumentTemplates_DocumentTemplateId",
                table: "policy_form_templates",
                column: "DocumentTemplateId",
                principalTable: "DocumentTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_policy_form_templates_DocumentTemplates_DocumentTemplateId",
                table: "policy_form_templates");

            migrationBuilder.DropIndex(
                name: "IX_policy_form_templates_DocumentTemplateId",
                table: "policy_form_templates");

            migrationBuilder.DropColumn(
                name: "DocumentTemplateId",
                table: "policy_form_templates");
        }
    }
}
