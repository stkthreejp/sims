namespace SIMS.Application.DTOs;

public record AgentCommissionDto(
    long Id,
    Guid? ProgramConfigurationId,
    string? ProgramName,
    string? LineOfBusiness,
    string? LineOfBusinessLabel,
    decimal CommissionRate,
    DateOnly EffectiveDate,
    DateOnly? DisabledDate,
    bool IsActive,
    DateTime CreatedAt
);

public record CreateAgentCommissionRequest(
    Guid? ProgramConfigurationId,
    string? LineOfBusiness,
    decimal CommissionRate,
    DateOnly EffectiveDate
);

public record DisableAgentCommissionRequest(
    DateOnly? DisabledDate
);
