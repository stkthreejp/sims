using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using SIMS.Infrastructure.Migrations;
using Xunit;

namespace SIMS.Application.Tests.Infrastructure;

public class BordereauxProfileProgramScopeMigrationTests
{
    [Fact]
    public void AddBordereauxProfileProgramScopeRefs_AddsShapeCheckConstraint()
    {
        var check = UpOperations()
            .OfType<AddCheckConstraintOperation>()
            .Single(o => o.Name == "ck_bordereaux_profile_program_scope_canonical");

        Assert.Contains("\"ProgramCarrierId\" IS NOT NULL", check.Sql);
        Assert.Contains("\"ProgramCarrierLineOfBusinessId\" IS NOT NULL", check.Sql);
        Assert.Contains("\"ProgramCarrierLobStateId\" IS NOT NULL", check.Sql);
    }

    [Fact]
    public void AddBordereauxProfileProgramScopeRefs_NormalizesAndBackfillsCurrentProgramPaths()
    {
        var sql = UpSql();

        Assert.Contains("SET \"StateCode\" = UPPER(TRIM(\"StateCode\"))", sql);
        Assert.Contains("SET \"ProgramCarrierId\" = pc.\"Id\"", sql);
        Assert.Contains("SET \"ProgramCarrierLineOfBusinessId\" = pcl.\"Id\"", sql);
        Assert.Contains("SET \"ProgramCarrierLobStateId\" = pcs.\"Id\"", sql);
        Assert.Contains("pc.\"EffectiveDate\" <= CURRENT_DATE", sql);
        Assert.Contains("pcl.\"EffectiveDate\" <= CURRENT_DATE", sql);
        Assert.Contains("pcs.\"EffectiveDate\" <= CURRENT_DATE", sql);
    }

    [Fact]
    public void AddBordereauxProfileProgramScopeRefs_PreflightsUnresolvedProgramPaths()
    {
        var sql = UpSql();

        Assert.Contains("inactive or deleted Program", sql);
        Assert.Contains("cannot skip LOB before state", sql);
        Assert.Contains("Program/Carrier profile has no matching active ProgramCarrier path", sql);
        Assert.Contains("Program/Carrier/LOB profile has no matching active ProgramCarrierLineOfBusiness path", sql);
        Assert.Contains("Program/Carrier/LOB/State profile has no matching active ProgramCarrierLobState path", sql);
    }

    [Fact]
    public void AddBordereauxProfileProgramScopeRefs_CreatesCanonicalValidationTriggers()
    {
        var sql = UpSql();

        Assert.Contains("CREATE OR REPLACE FUNCTION validate_bordereaux_profile_program_scope()", sql);
        Assert.Contains("CREATE TRIGGER trg_validate_bordereaux_profile_program_scope", sql);
        Assert.Contains("Bordereaux profile ProgramCarrierId does not match ProgramConfigurationId and CarrierId.", sql);
        Assert.Contains("Bordereaux profile ProgramCarrierLineOfBusinessId does not match Program, Carrier, and LineOfBusiness.", sql);
        Assert.Contains("Bordereaux profile ProgramCarrierLobStateId does not match Program, Carrier, LineOfBusiness, and StateCode.", sql);
    }

    [Fact]
    public void AddBordereauxProfileProgramScopeRefs_CreatesReverseTriggersForProgramSetupIdentityChanges()
    {
        var sql = UpSql();

        Assert.Contains("CREATE OR REPLACE FUNCTION validate_existing_bordereaux_profile_program_scopes()", sql);
        Assert.Contains("trg_validate_bordereaux_profiles_after_program_carrier_change", sql);
        Assert.Contains("trg_validate_bordereaux_profiles_after_program_lob_change", sql);
        Assert.Contains("trg_validate_bordereaux_profiles_after_program_state_change", sql);
        Assert.Contains("Program setup change would invalidate existing bordereaux profile ProgramCarrierId.", sql);
        Assert.Contains("Program setup change would invalidate existing bordereaux profile ProgramCarrierLineOfBusinessId.", sql);
        Assert.Contains("Program setup change would invalidate existing bordereaux profile ProgramCarrierLobStateId.", sql);
    }

    private static IReadOnlyList<MigrationOperation> UpOperations()
    {
        var migration = new AddBordereauxProfileProgramScopeRefs();
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");

        typeof(AddBordereauxProfileProgramScopeRefs)
            .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);

        return builder.Operations;
    }

    private static string UpSql() =>
        string.Join("\n\n", UpOperations().OfType<SqlOperation>().Select(o => o.Sql));
}
