using Microsoft.EntityFrameworkCore;
using SIMS.Application.Services;
using SIMS.Domain.Entities;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class AiModelSettingsServiceTests
{
    [Fact]
    public async Task EnsureDefaultsAsync_CreatesApprovedModelsAndUseCaseSettings()
    {
        await using var db = CreateDb();
        var service = new AiModelSettingsService(db);

        await service.EnsureDefaultsAsync();

        var models = await db.AiModelRegistry.ToListAsync();
        Assert.Contains(models, m => m.Provider == "Anthropic" && m.ModelId == "claude-sonnet-4-20250514" && m.Active);
        Assert.Contains(models, m => m.Provider == "GoogleDocumentAI" && m.ModelId == "FORM_PARSER_PROCESSOR" && m.Active);

        var settings = await db.AiUseCaseModelSettings.ToListAsync();
        Assert.Contains(settings, s => s.UseCase == "RiskScoring" && s.AiModel.Provider == "Anthropic");
        Assert.Contains(settings, s => s.UseCase == "ReferralJudgment" && s.AiModel.Provider == "Anthropic");
        Assert.Contains(settings, s => s.UseCase == "NarrativeDrafting" && s.AiModel.Provider == "Anthropic");
        Assert.Contains(settings, s => s.UseCase == "BatchTriage" && s.AiModel.Provider == "Anthropic");
        Assert.Contains(settings, s => s.UseCase == "DocumentExtraction" && s.AiModel.Provider == "GoogleDocumentAI");
    }

    [Fact]
    public async Task UpdateUseCaseModelAsync_RequiresAChangeReason()
    {
        await using var db = CreateDb();
        var service = new AiModelSettingsService(db);
        await service.EnsureDefaultsAsync();
        var openAi = AddOpenAiRiskModel(db);
        await db.SaveChangesAsync();

        var result = await service.UpdateUseCaseModelAsync("RiskScoring", openAi.Id, Guid.NewGuid(), " ");

        Assert.False(result.IsSuccess);
        Assert.Equal("CHANGE_REASON_REQUIRED", result.ErrorCode);
    }

    [Fact]
    public async Task UpdateUseCaseModelAsync_UpdatesSettingAndWritesAuditLog()
    {
        await using var db = CreateDb();
        var service = new AiModelSettingsService(db);
        await service.EnsureDefaultsAsync();
        var openAi = AddOpenAiRiskModel(db);
        await db.SaveChangesAsync();
        var userId = Guid.NewGuid();

        var result = await service.UpdateUseCaseModelAsync("RiskScoring", openAi.Id, userId, "Evaluate OpenAI on new submissions");

        Assert.True(result.IsSuccess);
        Assert.Equal(openAi.Id, result.Value!.Model.Id);
        Assert.Equal("OpenAI", result.Value.Model.Provider);

        var setting = await db.AiUseCaseModelSettings.SingleAsync(s => s.UseCase == "RiskScoring");
        Assert.Equal(openAi.Id, setting.AiModelRegistryId);
        Assert.Equal(userId, setting.UpdatedByUserId);

        var audit = await db.AiModelSettingAuditLogs.SingleAsync();
        Assert.Equal("RiskScoring", audit.UseCase);
        Assert.Equal(openAi.Id, audit.NewAiModelRegistryId);
        Assert.Equal(userId, audit.ChangedByUserId);
        Assert.Equal("Evaluate OpenAI on new submissions", audit.ChangeReason);
    }

    private static AiModelRegistry AddOpenAiRiskModel(AiModelSettingsTestDbContext db)
    {
        var model = new AiModelRegistry
        {
            Provider = "OpenAI",
            ModelId = "gpt-approved-default",
            DisplayName = "OpenAI Approved Default",
            Active = true,
            AllowedUseCases = ["RiskScoring", "ReferralJudgment", "NarrativeDrafting", "BatchTriage"],
            DefaultUseCases = [],
            CostNotes = "Comparison model"
        };
        db.AiModelRegistry.Add(model);
        return model;
    }

    private static AiModelSettingsTestDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AiModelSettingsTestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AiModelSettingsTestDbContext(options);
    }

    private sealed class AiModelSettingsTestDbContext : DbContext
    {
        public AiModelSettingsTestDbContext(DbContextOptions<AiModelSettingsTestDbContext> options) : base(options)
        {
        }

        public DbSet<AiModelRegistry> AiModelRegistry => Set<AiModelRegistry>();
        public DbSet<AiUseCaseModelSetting> AiUseCaseModelSettings => Set<AiUseCaseModelSetting>();
        public DbSet<AiModelSettingAuditLog> AiModelSettingAuditLogs => Set<AiModelSettingAuditLog>();
    }
}
