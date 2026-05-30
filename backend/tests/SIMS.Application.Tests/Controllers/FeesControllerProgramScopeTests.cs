using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIMS.API.Controllers.Admin;
using SIMS.Application.DTOs.Accounting;
using SIMS.Application.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Entities.Accounting;
using SIMS.Domain.Enums;
using SIMS.Infrastructure.Data;
using Xunit;

namespace SIMS.Application.Tests.Controllers;

public class FeesControllerProgramScopeTests
{
    [Fact]
    public async Task CreateVersion_ReturnsBadRequestShapeForInvalidProgramScope()
    {
        await using var db = CreateDb();
        var program = new ProgramConfiguration { Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        var carrier = new Carrier { Name = "BRACE", IsActive = true };
        var fee = BuildFee("MGA", "MGA Fee", "PolicyFee", 100);
        db.AddRange(program, carrier, fee);
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var request = ValidRequest(fee.Id) with
        {
            ProgramConfigurationId = program.Id,
            CarrierId = carrier.Id,
            LineOfBusiness = PolicyLineOfBusiness.GeneralLiability.ToString(),
            StateCode = "TX"
        };

        var result = await controller.CreateVersion(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("PROGRAM_SCOPE_PATH_NOT_FOUND", GetStringProperty(badRequest.Value!, "ErrorCode"));
        Assert.False(string.IsNullOrWhiteSpace(GetStringProperty(badRequest.Value!, "ErrorMessage")));
    }

    [Fact]
    public async Task CreateVersion_ReturnsNormalizedProgramScopeForValidPath()
    {
        await using var db = CreateDb();
        var program = new ProgramConfiguration { Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        var carrier = new Carrier { Name = "BRACE", IsActive = true };
        var fee = BuildFee("SL_TAX", "Surplus Lines Tax", "Tax", 10);
        db.AddRange(program, carrier, fee);
        await db.SaveChangesAsync();

        var programCarrier = new ProgramCarrier
        {
            ProgramConfigurationId = program.Id,
            CarrierId = carrier.Id,
            IsActive = true,
            EffectiveDate = new DateOnly(2026, 1, 1)
        };
        db.Add(programCarrier);
        await db.SaveChangesAsync();

        var programLob = new ProgramCarrierLineOfBusiness
        {
            ProgramCarrierId = programCarrier.Id,
            LineOfBusiness = PolicyLineOfBusiness.GeneralLiability,
            IsActive = true,
            EffectiveDate = new DateOnly(2026, 1, 1)
        };
        db.Add(programLob);
        await db.SaveChangesAsync();

        db.Add(new ProgramCarrierLobState
        {
            ProgramCarrierLineOfBusinessId = programLob.Id,
            StateCode = "TX",
            IsActive = true,
            EffectiveDate = new DateOnly(2026, 1, 1)
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var request = ValidRequest(fee.Id) with
        {
            ProgramConfigurationId = program.Id,
            CarrierId = carrier.Id,
            LineOfBusiness = "generalliability",
            StateCode = " tx "
        };

        var result = await controller.CreateVersion(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var dto = Assert.IsType<FeeRuleVersionDto>(created.Value);
        Assert.Equal(PolicyLineOfBusiness.GeneralLiability.ToString(), dto.LineOfBusiness);
        Assert.Equal("TX", dto.StateCode);
    }

    private static FeesController CreateController(ApplicationDbContext db)
    {
        var controller = new FeesController(new FeeAdminService(new TestServiceProvider(db)), db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
                    ]))
                }
            }
        };

        return controller;
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static FeeDefinition BuildFee(string code, string displayName, string category, int order) =>
        new()
        {
            Code = code,
            DisplayName = displayName,
            FeeCategory = category,
            IsTaxable = false,
            CalculationOrder = order,
            LedgerAccountId = 1
        };

    private static string? GetStringProperty(object payload, string name) =>
        payload.GetType().GetProperty(name)?.GetValue(payload)?.ToString();

    private static CreateFeeRuleVersionRequest ValidRequest(long feeDefinitionId) =>
        new(
            FeeDefinitionId: feeDefinitionId,
            ProgramConfigurationId: null,
            CarrierId: null,
            CompanyId: null,
            ProducerId: null,
            LineOfBusiness: null,
            StateCode: "TX",
            City: null,
            LicenseType: null,
            EffectiveDate: new DateOnly(2026, 1, 1),
            CalcType: "Percent",
            FlatAmount: null,
            PercentRate: 0.0485m,
            PercentOfNet: false,
            MinimumAmount: null,
            MaxPercent: null,
            MaxAmount: null,
            Commissionable: false,
            InstallmentBehavior: "PerInstallment",
            SplitByParticipation: false,
            FullyEarned: false,
            FullyEarnedDays: null,
            ExcludeTerrorism: false,
            MultiplyByLocations: false,
            MultiplyByVehicles: false,
            SendToAccounting: true,
            ApplyOnlyOnce: false,
            MandatoryCharge: true,
            ApplyAutomatically: true,
            ApplyWhenPackagePolicyOnly: false,
            DoNotApplyWhenPackagePolicyOnly: false,
            ApplyToChildLines: false,
            OnlyAppliesToIssuanceState: true,
            AppliesToFlatCancellations: false,
            PremiumMinThreshold: null,
            PremiumMaxThreshold: null,
            PremiumThresholdBasis: null,
            StateCountMin: null,
            StateCountMax: null,
            RoundingMode: "NearestCent",
            ExcludeWhenNotFiling: false,
            ExcludeOnEndorsements: false,
            ExcludeOnRenewal: false,
            ExcludeOnOriginalBinder: false,
            ExcludeOnMultiCarrierPolicy: false,
            PayHomeState: false,
            ExcludedPolicyTransactionTypes: null,
            PayableRouting: "NotPayable",
            PayablePayeeId: null,
            MasterPayeeWhenHomeState: false,
            Notes: null,
            PremiumBrackets: []);

    private sealed class TestServiceProvider(DbContext db) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(DbContext) ? db : null;
    }
}
