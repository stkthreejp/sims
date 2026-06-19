using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Accounting_ReplaceQboWithXero : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pending_qbo_syncs");

            migrationBuilder.DropTable(
                name: "qbo_oauth_tokens");

            migrationBuilder.CreateTable(
                name: "pending_journal_syncs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    RollupId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextRetryAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pending_journal_syncs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pending_journal_syncs_journal_entry_rollups_RollupId",
                        column: x => x.RollupId,
                        principalTable: "journal_entry_rollups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "xero_oauth_tokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    XeroTenantId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AccessToken = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    AccessTokenExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_xero_oauth_tokens", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pending_journal_syncs_next_retry",
                table: "pending_journal_syncs",
                column: "NextRetryAt");

            migrationBuilder.CreateIndex(
                name: "IX_pending_journal_syncs_RollupId",
                table: "pending_journal_syncs",
                column: "RollupId");

            migrationBuilder.CreateIndex(
                name: "ix_pending_journal_syncs_status",
                table: "pending_journal_syncs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ix_xero_oauth_tokens_tenant",
                table: "xero_oauth_tokens",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pending_journal_syncs");

            migrationBuilder.DropTable(
                name: "xero_oauth_tokens");

            migrationBuilder.CreateTable(
                name: "pending_qbo_syncs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RollupId = table.Column<long>(type: "bigint", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    NextRetryAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pending_qbo_syncs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pending_qbo_syncs_journal_entry_rollups_RollupId",
                        column: x => x.RollupId,
                        principalTable: "journal_entry_rollups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "qbo_oauth_tokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccessToken = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    AccessTokenExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RealmId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RefreshToken = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RefreshTokenExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qbo_oauth_tokens", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pending_qbo_syncs_next_retry",
                table: "pending_qbo_syncs",
                column: "NextRetryAt");

            migrationBuilder.CreateIndex(
                name: "IX_pending_qbo_syncs_RollupId",
                table: "pending_qbo_syncs",
                column: "RollupId");

            migrationBuilder.CreateIndex(
                name: "ix_pending_qbo_syncs_status",
                table: "pending_qbo_syncs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ix_qbo_oauth_tokens_tenant_realm",
                table: "qbo_oauth_tokens",
                columns: new[] { "TenantId", "RealmId" },
                unique: true);
        }
    }
}
