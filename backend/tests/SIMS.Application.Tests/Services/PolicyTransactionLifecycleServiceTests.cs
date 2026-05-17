using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SIMS.Application.Common;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;
using SIMS.Infrastructure.Data;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class PolicyTransactionLifecycleServiceTests
{
    [Fact]
    public void StatusDefinitions_CoverEveryPolicyTransactionStatus()
    {
        var definitions = PolicyTransactionLifecycleService.StatusDefinitions;

        foreach (var status in Enum.GetValues<PolicyTransactionStatus>())
        {
            var definition = Assert.Single(definitions.Where(d => d.Status == status));
            Assert.False(string.IsNullOrWhiteSpace(definition.Label));
            Assert.False(string.IsNullOrWhiteSpace(definition.Owner));
            Assert.False(string.IsNullOrWhiteSpace(definition.Meaning));
        }
    }

    [Fact]
    public void CanTransition_AllowsSubmittedToIssuedAndBlocksIssuedToSubmitted()
    {
        Assert.True(PolicyTransactionLifecycleService.CanTransition(
            PolicyTransactionStatus.Submitted,
            PolicyTransactionStatus.Issued));

        Assert.False(PolicyTransactionLifecycleService.CanTransition(
            PolicyTransactionStatus.Issued,
            PolicyTransactionStatus.Submitted));
    }

    [Fact]
    public async Task TransitionAsync_FailsLoudlyForIllegalTransitions()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var transaction = CreateTransaction(userId, PolicyTransactionStatus.Issued);
        db.AddRange(CreateUser(userId), transaction);
        await db.SaveChangesAsync();
        var workflow = new RecordingWorkflowEngineService();
        var lifecycle = new PolicyTransactionLifecycleService(db, workflow);

        var result = await lifecycle.TransitionAsync(
            transaction,
            PolicyTransactionStatus.Submitted,
            userId,
            "cannot reopen issued transaction");

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_TRANSACTION_STATUS_TRANSITION", result.ErrorCode);
        Assert.Equal(PolicyTransactionStatus.Issued, transaction.Status);
        Assert.Empty(await db.Set<PolicyTransactionStatusHistory>().ToListAsync());
        Assert.Empty(workflow.Events);
    }

    [Fact]
    public async Task TransitionAsync_RecordsHistoryAndFiresStatusEvent()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var transaction = CreateTransaction(userId, PolicyTransactionStatus.Submitted);
        db.AddRange(CreateUser(userId), transaction);
        await db.SaveChangesAsync();
        var workflow = new RecordingWorkflowEngineService();
        var lifecycle = new PolicyTransactionLifecycleService(db, workflow);

        var result = await lifecycle.TransitionAsync(
            transaction,
            PolicyTransactionStatus.Issued,
            userId,
            "issue endorsement");

        Assert.True(result.IsSuccess);
        Assert.Equal(PolicyTransactionStatus.Issued, transaction.Status);
        var history = await db.Set<PolicyTransactionStatusHistory>().SingleAsync();
        Assert.Equal(transaction.Id, history.PolicyTransactionId);
        Assert.Equal(PolicyTransactionStatus.Submitted, history.FromStatus);
        Assert.Equal(PolicyTransactionStatus.Issued, history.ToStatus);
        Assert.Equal("policy.transaction.issued", history.EventName);
        Assert.Equal(userId, history.ChangedById);
        Assert.Equal("issue endorsement", history.Notes);
        Assert.Contains(workflow.Events, e => e.EventName == "policy.transaction.issued" && e.EntityId == transaction.Id);
    }

    [Fact]
    public async Task RecordCreatedAsync_RecordsCreatedAndStatusEvents()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var transaction = CreateTransaction(userId, PolicyTransactionStatus.Submitted);
        db.AddRange(CreateUser(userId), transaction);
        await db.SaveChangesAsync();
        var workflow = new RecordingWorkflowEngineService();
        var lifecycle = new PolicyTransactionLifecycleService(db, workflow);

        var result = await lifecycle.RecordCreatedAsync(transaction, userId, "created endorsement");

        Assert.True(result.IsSuccess);
        var history = await db.Set<PolicyTransactionStatusHistory>()
            .OrderBy(h => h.ChangedAt)
            .ToListAsync();
        Assert.Equal(2, history.Count);
        Assert.Null(history[0].FromStatus);
        Assert.Equal(PolicyTransactionStatus.Submitted, history[0].ToStatus);
        Assert.Equal("policy.transaction.created", history[0].EventName);
        Assert.Equal("policy.transaction.submitted", history[1].EventName);
        Assert.Contains(workflow.Events, e => e.EventName == "policy.transaction.created");
        Assert.Contains(workflow.Events, e => e.EventName == "policy.transaction.submitted");
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static PolicyTransaction CreateTransaction(Guid userId, PolicyTransactionStatus status) => new()
    {
        Id = Guid.NewGuid(),
        PolicyId = Guid.NewGuid(),
        TransactionType = TransactionType.Endorsement,
        Status = status,
        TransactionNumber = $"TXN-{Guid.NewGuid():N}",
        EffectiveDate = new DateOnly(2026, 1, 1),
        ProcessedById = userId,
        ProcessedAt = DateTime.UtcNow,
    };

    private static User CreateUser(Guid userId) => new()
    {
        Id = userId,
        UserName = "lifecycle@sims.test",
        Email = "lifecycle@sims.test",
        FirstName = "Lifecycle",
        LastName = "User",
    };

    private sealed class RecordingWorkflowEngineService : IWorkflowEngineService
    {
        public List<(string EventName, Guid EntityId)> Events { get; } = [];

        public Task FireEventAsync(string eventName, TaskEntityType entityType, Guid entityId, Dictionary<string, object> context)
        {
            Events.Add((eventName, entityId));
            return Task.CompletedTask;
        }

        public Task FireStepCompletedAsync(Guid completedStepId, TaskEntityType entityType, Guid entityId, Dictionary<string, object> context)
            => Task.CompletedTask;
    }
}
