using Microsoft.EntityFrameworkCore;
using SIMS.Application.DTOs.Underwriting;
using SIMS.Application.Services;
using SIMS.Domain.Enums;
using SIMS.Infrastructure.Data;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class AuthorityApprovalServiceTests
{
    [Fact]
    public async Task EvaluateAsync_AllowsActionWhenUserHasRequiredPermission()
    {
        await using var db = CreateDb();
        var service = new AuthorityApprovalService(db);

        var result = await service.EvaluateAsync(
            Request("commission.override", "underwriting.authority.approve"),
            ["underwriting.authority.approve"],
            Guid.NewGuid());

        Assert.True(result.Allowed);
        Assert.False(result.RequiresApproval);
        Assert.Null(result.ApprovalRequestId);
    }

    [Fact]
    public async Task EvaluateAsync_CreatesPendingApprovalWhenUserLacksRequiredPermission()
    {
        await using var db = CreateDb();
        var service = new AuthorityApprovalService(db);
        var userId = Guid.NewGuid();

        var result = await service.EvaluateAsync(
            Request("commission.override", "underwriting.authority.approve"),
            Array.Empty<string>(),
            userId);

        Assert.False(result.Allowed);
        Assert.True(result.RequiresApproval);
        Assert.NotNull(result.ApprovalRequestId);
        Assert.Equal("Approval required for Commission override.", result.Message);

        var request = await db.AuthorityApprovalRequests.SingleAsync();
        Assert.Equal(AuthorityApprovalStatus.Pending, request.Status);
        Assert.Equal(userId, request.RequestedById);
        Assert.Equal("commission.override", request.ActionCode);
        Assert.Equal("underwriting.authority.approve", request.RequiredPermission);
    }

    [Fact]
    public async Task EvaluateAsync_ReusesOpenApprovalForSameTargetActionAndType()
    {
        await using var db = CreateDb();
        var service = new AuthorityApprovalService(db);
        var request = Request("endorsement.issue", "policies.endorse");

        var first = await service.EvaluateAsync(request, Array.Empty<string>(), Guid.NewGuid());
        var second = await service.EvaluateAsync(request, Array.Empty<string>(), Guid.NewGuid());

        Assert.Equal(first.ApprovalRequestId, second.ApprovalRequestId);
        Assert.Equal(1, await db.AuthorityApprovalRequests.CountAsync());
    }

    [Fact]
    public async Task EvaluateAsync_AllowsActionWhenMatchingApprovalWasApproved()
    {
        await using var db = CreateDb();
        var service = new AuthorityApprovalService(db);
        var request = Request("accounting.void", "accounting.admin");

        var blocked = await service.EvaluateAsync(request, Array.Empty<string>(), Guid.NewGuid());
        await service.DecideAsync(blocked.ApprovalRequestId!.Value, AuthorityApprovalStatus.Approved, Guid.NewGuid(), "Approved after review");

        var result = await service.EvaluateAsync(request, Array.Empty<string>(), Guid.NewGuid());

        Assert.True(result.Allowed);
        Assert.False(result.RequiresApproval);
        Assert.Equal(blocked.ApprovalRequestId, result.ApprovalRequestId);
    }

    private static AuthorityApprovalEvaluationRequest Request(string actionCode, string requiredPermission) => new(
        AuthorityApprovalTargetType.PolicyTransaction,
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        actionCode,
        "Commission override",
        requiredPermission,
        "AuthorityException",
        "Action exceeds user authority.",
        null,
        null);

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
