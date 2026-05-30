using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using SIMS.Infrastructure.Migrations;
using Xunit;

namespace SIMS.Application.Tests.Infrastructure;

public class FeeRuleProgramScopeMigrationTests
{
    [Fact]
    public void AddFeeRuleProgramScopeRefs_AddsShapeCheckConstraint()
    {
        var check = UpOperations()
            .OfType<AddCheckConstraintOperation>()
            .Single(o => o.Name == "ck_fee_rule_program_scope_canonical");

        Assert.Contains("\"ProgramConfigurationId\" IS NULL", check.Sql);
        Assert.Contains("\"ProgramCarrierId\" IS NOT NULL", check.Sql);
        Assert.Contains("\"ProgramCarrierLineOfBusinessId\" IS NOT NULL", check.Sql);
        Assert.Contains("\"ProgramCarrierLobStateId\" IS NOT NULL", check.Sql);
    }

    [Fact]
    public void AddFeeRuleProgramScopeRefs_NormalizesStateAndBackfillsUsingEffectiveDates()
    {
        var sql = UpSql();

        Assert.Contains("SET \"StateCode\" = UPPER(TRIM(\"StateCode\"))", sql);
        Assert.Contains("pc.\"EffectiveDate\" <= v.\"EffectiveDate\"", sql);
        Assert.Contains("pcl.\"EffectiveDate\" <= v.\"EffectiveDate\"", sql);
        Assert.Contains("pcs.\"EffectiveDate\" <= v.\"EffectiveDate\"", sql);
        Assert.Contains("pc.\"ExpirationDate\" IS NULL OR pc.\"ExpirationDate\" >= v.\"EffectiveDate\"", sql);
        Assert.Contains("pcl.\"ExpirationDate\" IS NULL OR pcl.\"ExpirationDate\" >= v.\"EffectiveDate\"", sql);
        Assert.Contains("pcs.\"ExpirationDate\" IS NULL OR pcs.\"ExpirationDate\" >= v.\"EffectiveDate\"", sql);
    }

    [Fact]
    public void AddFeeRuleProgramScopeRefs_PreflightsUnsupportedLobsAndUnresolvedProgramPaths()
    {
        var sql = UpSql();

        Assert.Contains("unsupported LineOfBusiness value", sql);
        Assert.Contains("inactive or deleted Program", sql);
        Assert.Contains("Program/Carrier fee rule has no matching active ProgramCarrier path", sql);
        Assert.Contains("Program/Carrier/LOB fee rule has no matching active ProgramCarrierLineOfBusiness path", sql);
        Assert.Contains("Program/Carrier/LOB/State fee rule has no matching active ProgramCarrierLobState path", sql);
        Assert.Contains("cannot skip carrier or LOB levels before state", sql);
    }

    [Fact]
    public void AddFeeRuleProgramScopeRefs_CreatesTriggerForCanonicalPathMismatches()
    {
        var sql = UpSql();

        Assert.Contains("CREATE OR REPLACE FUNCTION validate_fee_rule_program_scope()", sql);
        Assert.Contains("CREATE TRIGGER trg_validate_fee_rule_program_scope", sql);
        Assert.Contains("\"ProgramCarrierLobStateId\", \"EffectiveDate\"", sql);
        Assert.Contains("Fee rule ProgramConfigurationId is not active.", sql);
        Assert.Contains("Fee rule ProgramCarrierId does not match ProgramConfigurationId and CarrierId.", sql);
        Assert.Contains("Fee rule ProgramCarrierLineOfBusinessId does not match Program, Carrier, and LineOfBusiness.", sql);
        Assert.Contains("Fee rule ProgramCarrierLobStateId does not match Program, Carrier, LineOfBusiness, and StateCode.", sql);
        Assert.Contains("pc.\"EffectiveDate\" <= NEW.\"EffectiveDate\"", sql);
        Assert.Contains("pcl.\"EffectiveDate\" <= NEW.\"EffectiveDate\"", sql);
        Assert.Contains("pcs.\"EffectiveDate\" <= NEW.\"EffectiveDate\"", sql);
        Assert.Contains("pc.\"ExpirationDate\" IS NULL OR pc.\"ExpirationDate\" >= NEW.\"EffectiveDate\"", sql);
        Assert.Contains("pcl.\"ExpirationDate\" IS NULL OR pcl.\"ExpirationDate\" >= NEW.\"EffectiveDate\"", sql);
        Assert.Contains("pcs.\"ExpirationDate\" IS NULL OR pcs.\"ExpirationDate\" >= NEW.\"EffectiveDate\"", sql);
        Assert.Contains("pc.\"IsActive\" = TRUE", sql);
        Assert.Contains("pcl.\"IsActive\" = TRUE", sql);
        Assert.Contains("pcs.\"IsActive\" = TRUE", sql);
        Assert.Contains("pc.\"IsDeleted\" = FALSE", sql);
        Assert.Contains("pcl.\"IsDeleted\" = FALSE", sql);
        Assert.Contains("pcs.\"IsDeleted\" = FALSE", sql);
    }

    [Fact]
    public void AddFeeRuleProgramScopeRefs_CreatesReverseTriggersForCanonicalSetupChanges()
    {
        var sql = UpSql();

        Assert.Contains("CREATE OR REPLACE FUNCTION validate_existing_fee_rule_program_scopes()", sql);
        Assert.Contains("trg_validate_fee_rules_after_program_configuration_change", sql);
        Assert.Contains("trg_validate_fee_rules_after_program_carrier_change", sql);
        Assert.Contains("trg_validate_fee_rules_after_program_lob_change", sql);
        Assert.Contains("trg_validate_fee_rules_after_program_state_change", sql);
        Assert.Contains("Program setup change would invalidate existing fee rule ProgramConfigurationId.", sql);
        Assert.Contains("Program setup change would invalidate existing fee rule ProgramCarrierId.", sql);
        Assert.Contains("Program setup change would invalidate existing fee rule ProgramCarrierLineOfBusinessId.", sql);
        Assert.Contains("Program setup change would invalidate existing fee rule ProgramCarrierLobStateId.", sql);
    }

    private static IReadOnlyList<MigrationOperation> UpOperations()
    {
        var migration = new AddFeeRuleProgramScopeRefs();
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");

        typeof(AddFeeRuleProgramScopeRefs)
            .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);

        return builder.Operations;
    }

    private static string UpSql() =>
        string.Join("\n\n", UpOperations().OfType<SqlOperation>().Select(o => o.Sql));
}
