namespace SIMS.Application.DTOs.Ai;

public record AiModelRegistryDto(
    Guid Id,
    string Provider,
    string ModelId,
    string DisplayName,
    bool Active,
    IReadOnlyList<string> AllowedUseCases,
    IReadOnlyList<string> DefaultUseCases,
    string? CostNotes,
    DateOnly? RetirementReviewDate);

public record AiUseCaseModelSettingDto(
    string UseCase,
    AiModelRegistryDto Model,
    string PromptVersion,
    Guid? UpdatedByUserId,
    DateTime UpdatedAt);

public record AiModelSettingAuditLogDto(
    Guid Id,
    string UseCase,
    Guid? PreviousAiModelRegistryId,
    Guid NewAiModelRegistryId,
    string? PreviousPromptVersion,
    string NewPromptVersion,
    Guid ChangedByUserId,
    string ChangeReason,
    DateTime ChangedAt);

public record UpdateAiUseCaseModelSettingRequest(
    Guid AiModelRegistryId,
    string PromptVersion,
    string ChangeReason);
