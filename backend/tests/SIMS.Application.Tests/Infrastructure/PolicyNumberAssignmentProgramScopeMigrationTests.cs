using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using SIMS.Infrastructure.Migrations;
using Xunit;

namespace SIMS.Application.Tests.Infrastructure;

public class PolicyNumberAssignmentProgramScopeMigrationTests
{
    [Fact]
    public void AddPolicyNumberAssignmentProgramScopeRefs_AddsShapeCheckConstraint()
    {
        var check = UpOperations()
            .OfType<AddCheckConstraintOperation>()
            .Single(o => o.Name == "ck_policy_number_assignment_program_scope_canonical");

        Assert.Contains("\"ProgramConfigurationId\" IS NULL", check.Sql);
        Assert.Contains("\"ProgramCarrierLineOfBusinessId\" IS NULL", check.Sql);
        Assert.Contains("\"ProgramCarrierLobStateId\" IS NULL", check.Sql);
        Assert.Contains("\"State\" IS NULL", check.Sql);
        Assert.Contains("\"ProgramCarrierLineOfBusinessId\" IS NOT NULL", check.Sql);
        Assert.Contains("\"State\" IS NOT NULL", check.Sql);
        Assert.Contains("\"ProgramCarrierLobStateId\" IS NOT NULL", check.Sql);
    }

    [Fact]
    public void AddPolicyNumberAssignmentProgramScopeRefs_NormalizesAndBackfillsCurrentProgramPaths()
    {
        var sql = UpSql();

        Assert.Contains("SET \"State\" = NULLIF(UPPER(TRIM(\"State\")), '')", sql);
        Assert.Contains("SET \"ProgramCarrierLineOfBusinessId\" = pcl.\"Id\"", sql);
        Assert.Contains("SET \"ProgramCarrierLobStateId\" = pcs.\"Id\"", sql);
        Assert.Contains("pc.\"EffectiveDate\" <= CURRENT_DATE", sql);
        Assert.Contains("pcl.\"EffectiveDate\" <= CURRENT_DATE", sql);
        Assert.Contains("pcs.\"EffectiveDate\" <= CURRENT_DATE", sql);
    }

    [Fact]
    public void AddPolicyNumberAssignmentProgramScopeRefs_PreflightsUnresolvedProgramPaths()
    {
        var sql = UpSql();

        Assert.Contains("inactive or deleted Program", sql);
        Assert.Contains("Program/Carrier/LOB assignment has no matching active ProgramCarrierLineOfBusiness path", sql);
        Assert.Contains("Program/Carrier/LOB/State assignment has no matching active ProgramCarrierLobState path", sql);
    }

    [Fact]
    public void AddPolicyNumberAssignmentProgramScopeRefs_CreatesCanonicalValidationTriggers()
    {
        var sql = UpSql();

        Assert.Contains("CREATE OR REPLACE FUNCTION validate_policy_number_assignment_program_scope()", sql);
        Assert.Contains("CREATE TRIGGER trg_validate_policy_number_assignment_program_scope", sql);
        Assert.Contains("Policy number assignment ProgramCarrierLineOfBusinessId does not match Program, Carrier, and LineOfBusiness.", sql);
        Assert.Contains("Policy number assignment ProgramCarrierLobStateId does not match Program, Carrier, LineOfBusiness, and State.", sql);
    }

    [Fact]
    public void AddPolicyNumberAssignmentProgramScopeRefs_CreatesReverseTriggersForProgramSetupIdentityChanges()
    {
        var sql = UpSql();

        Assert.Contains("CREATE OR REPLACE FUNCTION validate_existing_policy_number_assignment_program_scopes()", sql);
        Assert.Contains("trg_validate_policy_number_assignments_after_program_carrier_change", sql);
        Assert.Contains("trg_validate_policy_number_assignments_after_program_lob_change", sql);
        Assert.Contains("trg_validate_policy_number_assignments_after_program_state_change", sql);
        Assert.Contains("Program setup change would invalidate existing policy number assignment ProgramCarrierLineOfBusinessId.", sql);
        Assert.Contains("Program setup change would invalidate existing policy number assignment ProgramCarrierLobStateId.", sql);
    }

    private static IReadOnlyList<MigrationOperation> UpOperations()
    {
        var migration = new AddPolicyNumberAssignmentProgramScopeRefs();
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");

        typeof(AddPolicyNumberAssignmentProgramScopeRefs)
            .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);

        return builder.Operations;
    }

    private static string UpSql() =>
        string.Join("\n\n", UpOperations().OfType<SqlOperation>().Select(o => o.Sql));
}
