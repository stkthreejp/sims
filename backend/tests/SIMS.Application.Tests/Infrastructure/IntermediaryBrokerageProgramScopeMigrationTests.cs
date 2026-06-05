using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using SIMS.Infrastructure.Migrations;
using Xunit;

namespace SIMS.Application.Tests.Infrastructure;

public class IntermediaryBrokerageProgramScopeMigrationTests
{
    [Fact]
    public void AddIntermediaryBrokerageProgramScopeRefs_AddsShapeCheckConstraint()
    {
        var check = UpOperations()
            .OfType<AddCheckConstraintOperation>()
            .Single(o => o.Name == "ck_intermediary_brokerage_program_scope_canonical");

        Assert.Contains("\"LineOfBusiness\" IS NULL", check.Sql);
        Assert.Contains("\"ProgramCarrierId\" IS NOT NULL", check.Sql);
        Assert.Contains("\"ProgramCarrierLineOfBusinessId\" IS NULL", check.Sql);
        Assert.Contains("\"LineOfBusiness\" IS NOT NULL", check.Sql);
        Assert.Contains("\"ProgramCarrierId\" IS NULL", check.Sql);
        Assert.Contains("\"ProgramCarrierLineOfBusinessId\" IS NOT NULL", check.Sql);
    }

    [Fact]
    public void AddIntermediaryBrokerageProgramScopeRefs_BackfillsEffectiveProgramPaths()
    {
        var sql = UpSql();

        Assert.Contains("SET \"ProgramCarrierId\" = pc.\"Id\"", sql);
        Assert.Contains("SET \"ProgramCarrierLineOfBusinessId\" = pcl.\"Id\"", sql);
        Assert.Contains("pc.\"EffectiveDate\" <= s.\"EffectiveDate\"", sql);
        Assert.Contains("pcl.\"EffectiveDate\" <= s.\"EffectiveDate\"", sql);
        Assert.Contains("Program/Carrier brokerage setup has no matching active ProgramCarrier path", sql);
        Assert.Contains("Program/Carrier/LOB brokerage setup has no matching active ProgramCarrierLineOfBusiness path", sql);
    }

    [Fact]
    public void AddIntermediaryBrokerageProgramScopeRefs_CreatesCanonicalValidationTriggers()
    {
        var sql = UpSql();

        Assert.Contains("CREATE OR REPLACE FUNCTION validate_intermediary_brokerage_program_scope()", sql);
        Assert.Contains("CREATE TRIGGER trg_validate_intermediary_brokerage_program_scope", sql);
        Assert.Contains("Intermediary brokerage ProgramCarrierId does not match ProgramConfigurationId, CarrierId, and EffectiveDate.", sql);
        Assert.Contains("Intermediary brokerage ProgramCarrierLineOfBusinessId does not match Program, Carrier, LineOfBusiness, and EffectiveDate.", sql);
    }

    [Fact]
    public void AddIntermediaryBrokerageProgramScopeRefs_CreatesReverseTriggersForProgramSetupIdentityChanges()
    {
        var sql = UpSql();

        Assert.Contains("CREATE OR REPLACE FUNCTION validate_existing_intermediary_brokerage_program_scopes()", sql);
        Assert.Contains("trg_validate_intermediary_brokerage_after_program_carrier_change", sql);
        Assert.Contains("trg_validate_intermediary_brokerage_after_program_lob_change", sql);
        Assert.Contains("Program setup change would invalidate existing intermediary brokerage ProgramCarrierId.", sql);
        Assert.Contains("Program setup change would invalidate existing intermediary brokerage ProgramCarrierLineOfBusinessId.", sql);
    }

    private static IReadOnlyList<MigrationOperation> UpOperations()
    {
        var migration = new AddIntermediaryBrokerageProgramScopeRefs();
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");

        typeof(AddIntermediaryBrokerageProgramScopeRefs)
            .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);

        return builder.Operations;
    }

    private static string UpSql() =>
        string.Join("\n\n", UpOperations().OfType<SqlOperation>().Select(o => o.Sql));
}
