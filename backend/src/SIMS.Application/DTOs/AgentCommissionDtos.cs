namespace SIMS.Application.DTOs;

public record AgentCommissionDto(
    long Id,
    Guid? ProgramConfigurationId,
    string? ProgramName,
    Guid? CarrierId,
    string? CarrierName,
    string? LineOfBusiness,
    string? LineOfBusinessLabel,
    string? StateCode,
    decimal CommissionRate,
    DateOnly EffectiveDate,
    DateOnly? DisabledDate,
    bool IsActive,
    DateTime CreatedAt
);

public record CreateAgentCommissionRequest(
    Guid? ProgramConfigurationId,
    Guid? CarrierId,
    string? LineOfBusiness,
    string? StateCode,
    decimal CommissionRate,
    DateOnly EffectiveDate
);

public record DisableAgentCommissionRequest(
    DateOnly? DisabledDate
);
