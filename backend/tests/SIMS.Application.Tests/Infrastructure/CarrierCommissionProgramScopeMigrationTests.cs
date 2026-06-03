using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using SIMS.Infrastructure.Migrations;
using Xunit;

namespace SIMS.Application.Tests.Infrastructure;

public class CarrierCommissionProgramScopeMigrationTests
{
    [Fact]
    public void AddCarrierCommissionProgramScopeRefs_AddsShapeCheckConstraint()
    {
        var check = UpOperations()
            .OfType<AddCheckConstraintOperation>()
            .Single(o => o.Name == "ck_carrier_commission_program_scope_canonical");

        Assert.Contains("\"ProgramConfigurationId\" IS NULL", check.Sql);
        Assert.Contains("\"ProgramCarrierId\" IS NULL", check.Sql);
        Assert.Contains("\"ProgramCarrierLineOfBusinessId\" IS NULL", check.Sql);
        Assert.Contains("\"LineOfBusiness\" IS NULL", check.Sql);
        Assert.Contains("\"ProgramCarrierId\" IS NOT NULL", check.Sql);
        Assert.Contains("\"LineOfBusiness\" IS NOT NULL", check.Sql);
        Assert.Contains("\"ProgramCarrierLineOfBusinessId\" IS NOT NULL", check.Sql);
    }

    [Fact]
    public void AddCarrierCommissionProgramScopeRefs_NormalizesAndBackfillsEffectiveProgramPaths()
    {
        var sql = UpSql();

        Assert.Contains("SET \"LineOfBusiness\" = NULLIF(TRIM(\"LineOfBusiness\"), '')", sql);
        Assert.Contains("SET \"ProgramCarrierId\" = pc.\"Id\"", sql);
        Assert.Contains("SET \"ProgramCarrierLineOfBusinessId\" = pcl.\"Id\"", sql);
        Assert.Contains("pc.\"EffectiveDate\" <= c.\"EffectiveDate\"", sql);
        Assert.Contains("pcl.\"EffectiveDate\" <= c.\"EffectiveDate\"", sql);
    }

    [Fact]
    public void AddCarrierCommissionProgramScopeRefs_PreflightsUnsupportedAndUnresolvedProgramPaths()
    {
        var sql = UpSql();

        Assert.Contains("unsupported LineOfBusiness value", sql);
        Assert.Contains("inactive or deleted Program", sql);
        Assert.Contains("Program/Carrier commission has no matching active ProgramCarrier path", sql);
        Assert.Contains("Program/Carrier/LOB commission has no matching active ProgramCarrierLineOfBusiness path", sql);
    }

    [Fact]
    public void AddCarrierCommissionProgramScopeRefs_CreatesCanonicalValidationTriggers()
    {
        var sql = UpSql();

        Assert.Contains("CREATE OR REPLACE FUNCTION validate_carrier_commission_program_scope()", sql);
        Assert.Contains("CREATE TRIGGER trg_validate_carrier_commission_program_scope", sql);
        Assert.Contains("Carrier commission ProgramCarrierId does not match ProgramConfigurationId, CarrierId, and EffectiveDate.", sql);
        Assert.Contains("Carrier commission ProgramCarrierLineOfBusinessId does not match Program, Carrier, LineOfBusiness, and EffectiveDate.", sql);
    }

    [Fact]
    public void AddCarrierCommissionProgramScopeRefs_CreatesReverseTriggersForProgramSetupIdentityChanges()
    {
        var sql = UpSql();

        Assert.Contains("CREATE OR REPLACE FUNCTION validate_existing_carrier_commission_program_scopes()", sql);
        Assert.Contains("trg_validate_carrier_commissions_after_program_carrier_change", sql);
        Assert.Contains("trg_validate_carrier_commissions_after_program_lob_change", sql);
        Assert.Contains("Program setup change would invalidate existing carrier commission ProgramCarrierId.", sql);
        Assert.Contains("Program setup change would invalidate existing carrier commission ProgramCarrierLineOfBusinessId.", sql);
    }

    private static IReadOnlyList<MigrationOperation> UpOperations()
    {
        var migration = new AddCarrierCommissionProgramScopeRefs();
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");

        typeof(AddCarrierCommissionProgramScopeRefs)
            .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);

        return builder.Operations;
    }

    private static string UpSql() =>
        string.Join("\n\n", UpOperations().OfType<SqlOperation>().Select(o => o.Sql));
}
