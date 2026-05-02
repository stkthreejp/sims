namespace SIMS.Application.DTOs;

public record AgentCommissionDto(
    long Id,
    string? LineOfBusiness,
    string? LineOfBusinessLabel,
    decimal CommissionRate,
    DateOnly EffectiveDate,
    DateOnly? DisabledDate,
    bool IsActive,
    DateTime CreatedAt
);

public record CreateAgentCommissionRequest(
    string? LineOfBusiness,
    decimal CommissionRate,
    DateOnly EffectiveDate
);

public record DisableAgentCommissionRequest(
    DateOnly? DisabledDate
);
