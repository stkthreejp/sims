using Microsoft.AspNetCore.Authorization;
using SIMS.API.Controllers;
using SIMS.Application.Security;
using Xunit;

namespace SIMS.Application.Tests.Controllers;

public class AuthorityApprovalHighRiskControllerTests
{
    [Fact]
    public void RatingPromotion_AllowsManagersToReachAuthorityApprovalGate()
    {
        var policy = typeof(RatingPlanVersionsController)
            .GetMethod(nameof(RatingPlanVersionsController.Promote))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single()
            .Policy;

        Assert.Equal(AppPermissions.RatingManage, policy);
    }
}
