using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using SIMS.Infrastructure.Migrations;
using Xunit;

namespace SIMS.Application.Tests.Infrastructure;

public class SurplusLinesStateSetupProgramScopeMigrationTests
{
    [Fact]
    public void AddSurplusLinesStateSetupProgramScopeRef_AddsStateScopeCheckConstraint()
    {
        var check = UpOperations()
            .OfType<AddCheckConstraintOperation>()
            .Single(o => o.Name == "ck_surplus_lines_state_setup_program_scope_canonical");

        Assert.Contains("\"ProgramConfigurationId\" IS NULL", check.Sql);
        Assert.Contains("\"ProgramCarrierLobStateId\" IS NULL", check.Sql);
        Assert.Contains("\"CarrierId\" IS NOT NULL", check.Sql);
        Assert.Contains("\"LineOfBusiness\" IS NOT NULL", check.Sql);
        Assert.Contains("\"ProgramCarrierLobStateId\" IS NOT NULL", check.Sql);
    }

    [Fact]
    public void AddSurplusLinesStateSetupProgramScopeRef_NormalizesAndBackfillsActiveProgramStatePaths()
    {
        var sql = UpSql();

        Assert.Contains("SET \"StateCode\" = UPPER(TRIM(\"StateCode\"))", sql);
        Assert.Contains("SET \"ProgramCarrierLobStateId\" = pcs.\"Id\"", sql);
        Assert.Contains("pc.\"EffectiveDate\" <= sls.\"EffectiveDate\"", sql);
        Assert.Contains("pcl.\"EffectiveDate\" <= sls.\"EffectiveDate\"", sql);
        Assert.Contains("pcs.\"EffectiveDate\" <= sls.\"EffectiveDate\"", sql);
    }

    [Fact]
    public void AddSurplusLinesStateSetupProgramScopeRef_PreflightsUnresolvedProgramPaths()
    {
        var sql = UpSql();

        Assert.Contains("inactive or deleted Program", sql);
        Assert.Contains("requires Program, Carrier, LOB, and State", sql);
        Assert.Contains("Program/Carrier/LOB/State setup has no matching active ProgramCarrierLobState path", sql);
    }

    [Fact]
    public void AddSurplusLinesStateSetupProgramScopeRef_CreatesCanonicalValidationTriggers()
    {
        var sql = UpSql();

        Assert.Contains("CREATE OR REPLACE FUNCTION validate_surplus_lines_state_setup_program_scope()", sql);
        Assert.Contains("CREATE TRIGGER trg_validate_surplus_lines_state_setup_program_scope", sql);
        Assert.Contains("Surplus lines setup ProgramCarrierLobStateId does not match Program, Carrier, LineOfBusiness, StateCode, and EffectiveDate.", sql);
    }

    [Fact]
    public void AddSurplusLinesStateSetupProgramScopeRef_CreatesReverseTriggersForProgramSetupIdentityChanges()
    {
        var sql = UpSql();

        Assert.Contains("CREATE OR REPLACE FUNCTION validate_existing_surplus_lines_state_setup_program_scopes()", sql);
        Assert.Contains("trg_validate_surplus_lines_setups_after_program_carrier_change", sql);
        Assert.Contains("trg_validate_surplus_lines_setups_after_program_lob_change", sql);
        Assert.Contains("trg_validate_surplus_lines_setups_after_program_state_change", sql);
        Assert.Contains("Program setup change would invalidate existing surplus lines setup ProgramCarrierLobStateId.", sql);
    }

    private static IReadOnlyList<MigrationOperation> UpOperations()
    {
        var migration = new AddSurplusLinesStateSetupProgramScopeRef();
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");

        typeof(AddSurplusLinesStateSetupProgramScopeRef)
            .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);

        return builder.Operations;
    }

    private static string UpSql() =>
        string.Join("\n\n", UpOperations().OfType<SqlOperation>().Select(o => o.Sql));
}
