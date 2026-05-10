namespace SIMS.Application.DTOs.Quotes;

public record QuoteChecklistItemDto(
    Guid Id,
    Guid QuoteId,
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
