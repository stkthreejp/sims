using Microsoft.AspNetCore.Authorization;
using SIMS.API.Controllers;
using SIMS.Application.Security;
using Xunit;

namespace SIMS.Application.Tests.Controllers;

public class UnderwritingControlEnforcementControllerTests
{
    [Fact]
    public void ReadAndEvaluateEndpoints_DoNotRequireUnderwritingManage()
    {
        var controllerAuthorize = typeof(UnderwritingControlEnforcementController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Null(controllerAuthorize.Policy);
        Assert.Null(GetMethodPolicy(nameof(UnderwritingControlEnforcementController.GetForTarget)));
        Assert.Null(GetMethodPolicy(nameof(UnderwritingControlEnforcementController.EvaluateQuote)));
        Assert.Null(GetMethodPolicy(nameof(UnderwritingControlEnforcementController.EvaluatePolicy)));
    }

    [Fact]
    public void OverrideEndpoint_StillRequiresOverridePermission()
    {
        Assert.Equal(
            AppPermissions.UnderwritingClearanceOverride,
            GetMethodPolicy(nameof(UnderwritingControlEnforcementController.Override)));
    }

    private static string? GetMethodPolicy(string methodName) =>
        typeof(UnderwritingControlEnforcementController)
            .GetMethod(methodName)!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault()
            ?.Policy;
}
