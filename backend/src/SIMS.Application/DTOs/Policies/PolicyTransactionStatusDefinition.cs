using SIMS.Domain.Enums;

namespace SIMS.Application.DTOs.Policies;

public sealed record PolicyTransactionStatusDefinition(
    PolicyTransactionStatus Status,
    string Label,
    string Owner,
    string Meaning,
    bool IsTerminal);
