namespace SIMS.Application.DTOs.Quotes;

using SIMS.Domain.Enums;

public record QuoteChecklistItemDto(
    Guid Id,
    Guid QuoteId,
    UnderwritingControlStage Stage,
    string TriggerKey,
    string Label,
    bool IsBlocker,
    int SortOrder,
    bool IsCompleted,
    string CompletionSource,
    Guid? CompletedById,
    string? CompletedByName,
    DateTime? CompletedAt
);

public record QuoteChecklistToggleDto(bool IsCompleted);
