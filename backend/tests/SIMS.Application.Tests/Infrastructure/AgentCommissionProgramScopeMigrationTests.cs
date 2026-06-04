using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using SIMS.Infrastructure.Migrations;
using Xunit;

namespace SIMS.Application.Tests.Infrastructure;

public class AgentCommissionProgramScopeMigrationTests
{
    [Fact]
    public void AddAgentCommissionProgramScopeRefs_AddsShapeCheckConstraint()
    {
        var check = UpOperations()
            .OfType<AddCheckConstraintOperation>()
            .Single(o => o.Name == "ck_agent_commission_program_scope_canonical");

        Assert.Contains("\"ProgramConfigurationId\" IS NULL", check.Sql);
        Assert.Contains("\"ProgramCarrierId\" IS NULL", check.Sql);
        Assert.Contains("\"ProgramCarrierLineOfBusinessId\" IS NULL", check.Sql);
        Assert.Contains("\"ProgramCarrierLobStateId\" IS NULL", check.Sql);
        Assert.Contains("\"CarrierId\" IS NULL", check.Sql);
        Assert.Contains("\"CarrierId\" IS NOT NULL", check.Sql);
        Assert.Contains("\"StateCode\" IS NOT NULL", check.Sql);
        Assert.Contains("\"ProgramCarrierLobStateId\" IS NOT NULL", check.Sql);
    }

    [Fact]
    public void AddAgentCommissionProgramScopeRefs_NormalizesAndBackfillsEffectiveProgramPaths()
    {
        var sql = UpSql();

        Assert.Contains("SET \"LineOfBusiness\" = NULLIF(TRIM(\"LineOfBusiness\"), '')", sql);
        Assert.Contains("SET \"StateCode\" = NULLIF(UPPER(TRIM(\"StateCode\")), '')", sql);
        Assert.Contains("SET \"ProgramCarrierId\" = pc.\"Id\"", sql);
        Assert.Contains("SET \"ProgramCarrierLineOfBusinessId\" = pcl.\"Id\"", sql);
        Assert.Contains("SET \"ProgramCarrierLobStateId\" = pcs.\"Id\"", sql);
        Assert.Contains("pc.\"EffectiveDate\" <= c.\"EffectiveDate\"", sql);
        Assert.Contains("pcl.\"EffectiveDate\" <= c.\"EffectiveDate\"", sql);
        Assert.Contains("pcs.\"EffectiveDate\" <= c.\"EffectiveDate\"", sql);
    }

    [Fact]
    public void AddAgentCommissionProgramScopeRefs_PreflightsUnsupportedAndUnresolvedProgramPaths()
    {
        var sql = UpSql();

        Assert.Contains("unsupported LineOfBusiness value", sql);
        Assert.Contains("inactive or deleted Program", sql);
        Assert.Contains("Program-scoped agent commissions cannot skip carrier or LOB levels before state", sql);
        Assert.Contains("Program/Carrier agent commission has no matching active ProgramCarrier path", sql);
        Assert.Contains("Program/Carrier/LOB agent commission has no matching active ProgramCarrierLineOfBusiness path", sql);
        Assert.Contains("Program/Carrier/LOB/State agent commission has no matching active ProgramCarrierLobState path", sql);
    }

    [Fact]
    public void AddAgentCommissionProgramScopeRefs_CreatesCanonicalValidationTriggers()
    {
        var sql = UpSql();

        Assert.Contains("CREATE OR REPLACE FUNCTION validate_agent_commission_program_scope()", sql);
        Assert.Contains("CREATE TRIGGER trg_validate_agent_commission_program_scope", sql);
        Assert.Contains("Agent commission ProgramCarrierId does not match ProgramConfigurationId, CarrierId, and EffectiveDate.", sql);
        Assert.Contains("Agent commission ProgramCarrierLineOfBusinessId does not match Program, Carrier, LineOfBusiness, and EffectiveDate.", sql);
        Assert.Contains("Agent commission ProgramCarrierLobStateId does not match Program, Carrier, LineOfBusiness, StateCode, and EffectiveDate.", sql);
    }

    [Fact]
    public void AddAgentCommissionProgramScopeRefs_CreatesReverseTriggersForProgramSetupIdentityChanges()
    {
        var sql = UpSql();

        Assert.Contains("CREATE OR REPLACE FUNCTION validate_existing_agent_commission_program_scopes()", sql);
        Assert.Contains("trg_validate_agent_commissions_after_program_carrier_change", sql);
        Assert.Contains("trg_validate_agent_commissions_after_program_lob_change", sql);
        Assert.Contains("trg_validate_agent_commissions_after_program_state_change", sql);
        Assert.Contains("Program setup change would invalidate existing agent commission ProgramCarrierId.", sql);
        Assert.Contains("Program setup change would invalidate existing agent commission ProgramCarrierLineOfBusinessId.", sql);
        Assert.Contains("Program setup change would invalidate existing agent commission ProgramCarrierLobStateId.", sql);
    }

    private static IReadOnlyList<MigrationOperation> UpOperations()
    {
        var migration = new AddAgentCommissionProgramScopeRefs();
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");

        typeof(AddAgentCommissionProgramScopeRefs)
            .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);

        return builder.Operations;
    }

    private static string UpSql() =>
        string.Join("\n\n", UpOperations().OfType<SqlOperation>().Select(o => o.Sql));
}
