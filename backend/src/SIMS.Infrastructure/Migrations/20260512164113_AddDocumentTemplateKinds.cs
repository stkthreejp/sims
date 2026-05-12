using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentTemplateKinds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmailBodyHtml",
                table: "DocumentTemplates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "DocumentTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SubjectTemplate",
                table: "DocumentTemplates",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailBodyHtml",
                table: "DocumentTemplates");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "DocumentTemplates");

            migrationBuilder.DropColumn(
                name: "SubjectTemplate",
                table: "DocumentTemplates");
        }
    }
}
