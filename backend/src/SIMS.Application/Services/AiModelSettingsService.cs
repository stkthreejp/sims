using Microsoft.EntityFrameworkCore;
using SIMS.Application.Common;
using SIMS.Application.DTOs.Ai;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;

namespace SIMS.Application.Services;

public class AiModelSettingsService : IAiModelSettingsService
{
    public const string DocumentExtraction = "DocumentExtraction";
    public const string RiskScoring = "RiskScoring";
    public const string ReferralJudgment = "ReferralJudgment";
    public const string NarrativeDrafting = "NarrativeDrafting";
    public const string BatchTriage = "BatchTriage";

    private const string DefaultPromptVersion = "smm-underwriter-v1";
    private readonly DbContext _db;

    public AiModelSettingsService(DbContext db) => _db = db;

    public async Task EnsureDefaultsAsync(CancellationToken ct = default)
    {
        var docModel = await EnsureModelAsync(
            provider: "GoogleDocumentAI",
            modelId: "FORM_PARSER_PROCESSOR",
            displayName: "Google Document AI Form Parser",
            allowedUseCases: [DocumentExtraction],
            defaultUseCases: [DocumentExtraction],
            costNotes: "Uses configured Document AI processor.",
            ct);

        var sonnet = await EnsureModelAsync(
            provider: "Anthropic",
            modelId: "claude-sonnet-4-20250514",
            displayName: "Claude Sonnet 4",
            allowedUseCases: [RiskScoring, ReferralJudgment, NarrativeDrafting, BatchTriage],
            defaultUseCases: [RiskScoring, ReferralJudgment, NarrativeDrafting, BatchTriage],
            costNotes: "Recommended default while the SMM Underwriter skill is maintained in Claude.",
            ct);

        await EnsureSettingAsync(DocumentExtraction, docModel.Id, "document-ai-form-parser", ct);
        foreach (var useCase in new[] { RiskScoring, ReferralJudgment, NarrativeDrafting, BatchTriage })
            await EnsureSettingAsync(useCase, sonnet.Id, DefaultPromptVersion, ct);

        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AiModelRegistryDto>> GetModelsAsync(CancellationToken ct = default)
    {
        await EnsureDefaultsAsync(ct);
        var models = await _db.Set<AiModelRegistry>()
            .OrderBy(m => m.Provider)
            .ThenBy(m => m.DisplayName)
            .ToListAsync(ct);
        return models.Select(MapModel).ToList();
    }

    public async Task<IReadOnlyList<AiUseCaseModelSettingDto>> GetSettingsAsync(CancellationToken ct = default)
    {
        await EnsureDefaultsAsync(ct);
        var settings = await _db.Set<AiUseCaseModelSetting>()
            .Include(s => s.AiModel)
            .OrderBy(s => s.UseCase)
            .ToListAsync(ct);
        return settings.Select(MapSetting).ToList();
    }

    public async Task<IReadOnlyList<AiModelSettingAuditLogDto>> GetAuditLogAsync(CancellationToken ct = default)
    {
        var rows = await _db.Set<AiModelSettingAuditLog>()
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(ct);
        return rows.Select(MapAudit).ToList();
    }

    public async Task<Result<AiUseCaseModelSettingDto>> UpdateUseCaseModelAsync(
        string useCase,
        Guid aiModelRegistryId,
        Guid userId,
        string changeReason,
        string? promptVersion = null,
        CancellationToken ct = default)
    {
        await EnsureDefaultsAsync(ct);

        if (string.IsNullOrWhiteSpace(changeReason))
            return Result<AiUseCaseModelSettingDto>.Failure("CHANGE_REASON_REQUIRED", "A change reason is required.");

        var model = await _db.Set<AiModelRegistry>().FindAsync([aiModelRegistryId], ct);
        if (model is null || model.IsDeleted)
            return Result<AiUseCaseModelSettingDto>.Failure("MODEL_NOT_FOUND", "AI model was not found.");

        if (!model.Active)
            return Result<AiUseCaseModelSettingDto>.Failure("MODEL_INACTIVE", "AI model is inactive.");

        if (!model.AllowedUseCases.Contains(useCase))
            return Result<AiUseCaseModelSettingDto>.Failure("MODEL_USE_CASE_NOT_ALLOWED", "AI model is not approved for this use case.");

        var setting = await _db.Set<AiUseCaseModelSetting>()
            .Include(s => s.AiModel)
            .SingleOrDefaultAsync(s => s.UseCase == useCase, ct);

        if (setting is null)
        {
            setting = new AiUseCaseModelSetting { UseCase = useCase };
            _db.Set<AiUseCaseModelSetting>().Add(setting);
        }

        Guid? previousModelId = setting.AiModelRegistryId == Guid.Empty ? null : setting.AiModelRegistryId;
        var previousPromptVersion = setting.PromptVersion;

        setting.AiModelRegistryId = aiModelRegistryId;
        setting.PromptVersion = string.IsNullOrWhiteSpace(promptVersion) ? DefaultPromptVersion : promptVersion.Trim();
        setting.UpdatedByUserId = userId;

        _db.Set<AiModelSettingAuditLog>().Add(new AiModelSettingAuditLog
        {
            UseCase = useCase,
            PreviousAiModelRegistryId = previousModelId,
            NewAiModelRegistryId = aiModelRegistryId,
            PreviousPromptVersion = previousPromptVersion,
            NewPromptVersion = setting.PromptVersion,
            ChangedByUserId = userId,
            ChangeReason = changeReason.Trim()
        });

        await _db.SaveChangesAsync(ct);
        setting.AiModel = model;
        return Result<AiUseCaseModelSettingDto>.Success(MapSetting(setting));
    }

    private async Task<AiModelRegistry> EnsureModelAsync(
        string provider,
        string modelId,
        string displayName,
        string[] allowedUseCases,
        string[] defaultUseCases,
        string costNotes,
        CancellationToken ct)
    {
        var existing = await _db.Set<AiModelRegistry>()
            .SingleOrDefaultAsync(m => m.Provider == provider && m.ModelId == modelId, ct);
        if (existing is not null)
            return existing;

        var model = new AiModelRegistry
        {
            Provider = provider,
            ModelId = modelId,
            DisplayName = displayName,
            Active = true,
            AllowedUseCases = allowedUseCases,
            DefaultUseCases = defaultUseCases,
            CostNotes = costNotes
        };
        _db.Set<AiModelRegistry>().Add(model);
        return model;
    }

    private async Task EnsureSettingAsync(string useCase, Guid modelId, string promptVersion, CancellationToken ct)
    {
        var exists = await _db.Set<AiUseCaseModelSetting>().AnyAsync(s => s.UseCase == useCase, ct);
        if (exists)
            return;

        _db.Set<AiUseCaseModelSetting>().Add(new AiUseCaseModelSetting
        {
            UseCase = useCase,
            AiModelRegistryId = modelId,
            PromptVersion = promptVersion
        });
    }

    private static AiModelRegistryDto MapModel(AiModelRegistry model) =>
        new(
            model.Id,
            model.Provider,
            model.ModelId,
            model.DisplayName,
            model.Active,
            model.AllowedUseCases,
            model.DefaultUseCases,
            model.CostNotes,
            model.RetirementReviewDate);

    private static AiUseCaseModelSettingDto MapSetting(AiUseCaseModelSetting setting) =>
        new(
            setting.UseCase,
            MapModel(setting.AiModel),
            setting.PromptVersion,
            setting.UpdatedByUserId,
            setting.UpdatedAt);

    private static AiModelSettingAuditLogDto MapAudit(AiModelSettingAuditLog log) =>
        new(
            log.Id,
            log.UseCase,
            log.PreviousAiModelRegistryId,
            log.NewAiModelRegistryId,
            log.PreviousPromptVersion,
            log.NewPromptVersion,
            log.ChangedByUserId,
            log.ChangeReason,
            log.CreatedAt);
}
