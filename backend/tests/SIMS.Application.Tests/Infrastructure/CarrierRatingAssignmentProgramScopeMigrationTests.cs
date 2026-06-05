using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using SIMS.Infrastructure.Migrations;
using Xunit;

namespace SIMS.Application.Tests.Infrastructure;

public class CarrierRatingAssignmentProgramScopeMigrationTests
{
    [Fact]
    public void AddCarrierRatingAssignmentProgramScopeRefs_AddsShapeCheckConstraint()
    {
        var check = UpOperations()
            .OfType<AddCheckConstraintOperation>()
            .Single(o => o.Name == "ck_carrier_rating_assignment_program_scope_canonical");

        Assert.Contains("program_configuration_id IS NULL", check.Sql);
        Assert.Contains("program_carrier_line_of_business_id IS NULL", check.Sql);
        Assert.Contains("program_configuration_id IS NOT NULL", check.Sql);
        Assert.Contains("program_carrier_line_of_business_id IS NOT NULL", check.Sql);
    }

    [Fact]
    public void AddCarrierRatingAssignmentProgramScopeRefs_BackfillsEffectiveProgramLobPaths()
    {
        var sql = UpSql();

        Assert.Contains("SET program_carrier_line_of_business_id = (", sql);
        Assert.Contains("SELECT pcl.\"Id\"", sql);
        Assert.Contains("pc.\"EffectiveDate\" <= v.effective_date", sql);
        Assert.Contains("pcl.\"EffectiveDate\" <= v.effective_date", sql);
        Assert.Contains("Program/Carrier/LOB rating assignment has no matching active ProgramCarrierLineOfBusiness path", sql);
    }

    [Fact]
    public void AddCarrierRatingAssignmentProgramScopeRefs_CreatesCanonicalValidationTriggers()
    {
        var sql = UpSql();

        Assert.Contains("CREATE OR REPLACE FUNCTION validate_carrier_rating_assignment_program_scope()", sql);
        Assert.Contains("CREATE TRIGGER trg_validate_carrier_rating_assignment_program_scope", sql);
        Assert.Contains("Carrier rating assignment ProgramCarrierLineOfBusinessId does not match Program, Carrier, LineOfBusiness, and version EffectiveDate.", sql);
    }

    [Fact]
    public void AddCarrierRatingAssignmentProgramScopeRefs_CreatesReverseTriggersForProgramSetupIdentityChanges()
    {
        var sql = UpSql();

        Assert.Contains("CREATE OR REPLACE FUNCTION validate_existing_carrier_rating_assignment_program_scopes()", sql);
        Assert.Contains("trg_validate_carrier_rating_assignments_after_program_carrier_change", sql);
        Assert.Contains("trg_validate_carrier_rating_assignments_after_program_lob_change", sql);
        Assert.Contains("Program setup change would invalidate existing carrier rating assignment ProgramCarrierLineOfBusinessId.", sql);
    }

    private static IReadOnlyList<MigrationOperation> UpOperations()
    {
        var migration = new AddCarrierRatingAssignmentProgramScopeRefs();
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");

        typeof(AddCarrierRatingAssignmentProgramScopeRefs)
            .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);

        return builder.Operations;
    }

    private static string UpSql() =>
        string.Join("\n\n", UpOperations().OfType<SqlOperation>().Select(o => o.Sql));
}
