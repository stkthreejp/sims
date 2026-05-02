using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Accounting_AgentCommissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AgentCommissionAmount",
                table: "invoices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "agent_commissions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineOfBusiness = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CommissionRate = table.Column<decimal>(type: "numeric(8,6)", nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DisabledDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_commissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_agent_commissions_agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agent_commissions_AgentId_DisabledDate",
                table: "agent_commissions",
                columns: new[] { "AgentId", "DisabledDate" });

            migrationBuilder.CreateIndex(
                name: "IX_agent_commissions_AgentId_LineOfBusiness_EffectiveDate",
                table: "agent_commissions",
                columns: new[] { "AgentId", "LineOfBusiness", "EffectiveDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_commissions");

            migrationBuilder.DropColumn(
                name: "AgentCommissionAmount",
                table: "invoices");
        }
    }
}
