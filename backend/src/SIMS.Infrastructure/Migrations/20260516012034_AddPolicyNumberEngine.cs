using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPolicyNumberEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BasePolicyNumber",
                table: "policies",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PolicyNumberAssignmentId",
                table: "policies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PolicyNumberSequenceId",
                table: "policies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PolicyTermNumber",
                table: "policies",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "WritingCompanyId",
                table: "policies",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "policy_number_sequences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Format = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NextNumber = table.Column<long>(type: "bigint", nullable: false),
                    ResetAnnually = table.Column<bool>(type: "boolean", nullable: false),
                    LastResetYear = table.Column<int>(type: "integer", nullable: true),
                    TermSuffixFormat = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RenewalBehavior = table.Column<int>(type: "integer", nullable: false),
                    AllowManualOverride = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_policy_number_sequences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "policy_number_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyNumberSequenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CarrierId = table.Column<Guid>(type: "uuid", nullable: false),
                    WritingCompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    LineOfBusiness = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_policy_number_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_policy_number_assignments_carriers_CarrierId",
                        column: x => x.CarrierId,
                        principalTable: "carriers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_policy_number_assignments_policy_number_sequences_PolicyNum~",
                        column: x => x.PolicyNumberSequenceId,
                        principalTable: "policy_number_sequences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "policy_number_sequence_usages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyNumberSequenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyNumberAssignmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    QuoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyId = table.Column<Guid>(type: "uuid", nullable: true),
                    BasePolicyNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FullPolicyNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SequenceValue = table.Column<long>(type: "bigint", nullable: false),
                    TermNumber = table.Column<int>(type: "integer", nullable: false),
                    WasManualOverride = table.Column<bool>(type: "boolean", nullable: false),
                    AssignedById = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_policy_number_sequence_usages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_policy_number_sequence_usages_policies_PolicyId",
                        column: x => x.PolicyId,
                        principalTable: "policies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_policy_number_sequence_usages_policy_number_assignments_Pol~",
                        column: x => x.PolicyNumberAssignmentId,
                        principalTable: "policy_number_assignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_policy_number_sequence_usages_policy_number_sequences_Polic~",
                        column: x => x.PolicyNumberSequenceId,
                        principalTable: "policy_number_sequences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_policy_number_sequence_usages_quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "quotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_policy_number_sequence_usages_users_AssignedById",
                        column: x => x.AssignedById,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_policy_number_assignments_CarrierId_WritingCompanyId_LineOf~",
                table: "policy_number_assignments",
                columns: new[] { "CarrierId", "WritingCompanyId", "LineOfBusiness", "State", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_policy_number_assignments_PolicyNumberSequenceId",
                table: "policy_number_assignments",
                column: "PolicyNumberSequenceId");

            migrationBuilder.CreateIndex(
                name: "IX_policy_number_sequence_usages_AssignedById",
                table: "policy_number_sequence_usages",
                column: "AssignedById");

            migrationBuilder.CreateIndex(
                name: "IX_policy_number_sequence_usages_FullPolicyNumber",
                table: "policy_number_sequence_usages",
                column: "FullPolicyNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_policy_number_sequence_usages_PolicyId",
                table: "policy_number_sequence_usages",
                column: "PolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_policy_number_sequence_usages_PolicyNumberAssignmentId",
                table: "policy_number_sequence_usages",
                column: "PolicyNumberAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_policy_number_sequence_usages_PolicyNumberSequenceId",
                table: "policy_number_sequence_usages",
                column: "PolicyNumberSequenceId");

            migrationBuilder.CreateIndex(
                name: "IX_policy_number_sequence_usages_QuoteId",
                table: "policy_number_sequence_usages",
                column: "QuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_policy_number_sequences_Name",
                table: "policy_number_sequences",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "policy_number_sequence_usages");

            migrationBuilder.DropTable(
                name: "policy_number_assignments");

            migrationBuilder.DropTable(
                name: "policy_number_sequences");

            migrationBuilder.DropColumn(
                name: "BasePolicyNumber",
                table: "policies");

            migrationBuilder.DropColumn(
                name: "PolicyNumberAssignmentId",
                table: "policies");

            migrationBuilder.DropColumn(
                name: "PolicyNumberSequenceId",
                table: "policies");

            migrationBuilder.DropColumn(
                name: "PolicyTermNumber",
                table: "policies");

            migrationBuilder.DropColumn(
                name: "WritingCompanyId",
                table: "policies");
        }
    }
}
