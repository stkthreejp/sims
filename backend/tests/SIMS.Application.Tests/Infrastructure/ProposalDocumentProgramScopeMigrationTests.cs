using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using SIMS.Infrastructure.Migrations;
using Xunit;

namespace SIMS.Application.Tests.Infrastructure;

public class ProposalDocumentProgramScopeMigrationTests
{
    [Fact]
    public void AddProposalDocumentProgramScopeRefs_AddsShapeCheckConstraints()
    {
        var checks = UpOperations()
            .OfType<AddCheckConstraintOperation>()
            .ToDictionary(o => o.Name, o => o.Sql);

        var scopeCheck = checks["ck_proposal_document_program_scope_canonical"];
        Assert.Contains("\"ProgramConfigurationId\" IS NULL", scopeCheck);
        Assert.Contains("\"ProgramCarrierLineOfBusinessId\" IS NULL", scopeCheck);
        Assert.Contains("\"ProgramCarrierLobStateId\" IS NULL", scopeCheck);
        Assert.Contains("\"State\" IS NULL", scopeCheck);
        Assert.Contains("\"ProgramCarrierLineOfBusinessId\" IS NOT NULL", scopeCheck);
        Assert.Contains("\"State\" IS NOT NULL", scopeCheck);
        Assert.Contains("\"ProgramCarrierLobStateId\" IS NOT NULL", scopeCheck);

        Assert.Contains("\"Role\" <> 1 OR \"State\" IS NOT NULL", checks["ck_proposal_document_state_notice_requires_state"]);
    }

    [Fact]
    public void AddProposalDocumentProgramScopeRefs_NormalizesAndBackfillsEffectiveProgramPaths()
    {
        var sql = UpSql();

        Assert.Contains("SET \"State\" = NULLIF(UPPER(TRIM(\"State\")), '')", sql);
        Assert.Contains("SET \"ProgramCarrierLineOfBusinessId\" = pcl.\"Id\"", sql);
        Assert.Contains("SET \"ProgramCarrierLobStateId\" = pcs.\"Id\"", sql);
        Assert.Contains("COALESCE(p.\"EffectiveDate\", CURRENT_DATE)", sql);
    }

    [Fact]
    public void AddProposalDocumentProgramScopeRefs_PreflightsUnresolvedProgramPaths()
    {
        var sql = UpSql();

        Assert.Contains("StateNotice rows require a state", sql);
        Assert.Contains("inactive or deleted Program", sql);
        Assert.Contains("Program/Carrier/LOB setup has no matching active ProgramCarrierLineOfBusiness path", sql);
        Assert.Contains("Program/Carrier/LOB/State setup has no matching active ProgramCarrierLobState path", sql);
    }

    [Fact]
    public void AddProposalDocumentProgramScopeRefs_CreatesCanonicalValidationTriggers()
    {
        var sql = UpSql();

        Assert.Contains("CREATE OR REPLACE FUNCTION validate_proposal_document_program_scope()", sql);
        Assert.Contains("CREATE TRIGGER trg_validate_proposal_document_program_scope", sql);
        Assert.Contains("StateNotice proposal document setup requires State.", sql);
        Assert.Contains("Proposal document ProgramCarrierLineOfBusinessId does not match Program, Carrier, LineOfBusiness, and EffectiveDate.", sql);
        Assert.Contains("Proposal document ProgramCarrierLobStateId does not match Program, Carrier, LineOfBusiness, State, and EffectiveDate.", sql);
    }

    [Fact]
    public void AddProposalDocumentProgramScopeRefs_CreatesReverseTriggersForProgramSetupIdentityChanges()
    {
        var sql = UpSql();

        Assert.Contains("CREATE OR REPLACE FUNCTION validate_existing_proposal_document_program_scopes()", sql);
        Assert.Contains("trg_validate_proposal_documents_after_program_carrier_change", sql);
        Assert.Contains("trg_validate_proposal_documents_after_program_lob_change", sql);
        Assert.Contains("trg_validate_proposal_documents_after_program_state_change", sql);
        Assert.Contains("Program setup change would invalidate existing proposal document ProgramCarrierLineOfBusinessId.", sql);
        Assert.Contains("Program setup change would invalidate existing proposal document ProgramCarrierLobStateId.", sql);
    }

    private static IReadOnlyList<MigrationOperation> UpOperations()
    {
        var migration = new AddProposalDocumentProgramScopeRefs();
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");

        typeof(AddProposalDocumentProgramScopeRefs)
            .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);

        return builder.Operations;
    }

    private static string UpSql() =>
        string.Join("\n\n", UpOperations().OfType<SqlOperation>().Select(o => o.Sql));
}
