using SIMS.Application.Common;
using SIMS.Application.DTOs;

namespace SIMS.Application.Interfaces.Services;

public interface IAgentCommissionService
{
    Task<IReadOnlyList<AgentCommissionDto>> GetAllAsync(Guid agentId, CancellationToken ct = default);
    Task<Result<AgentCommissionDto>> CreateAsync(Guid agentId, CreateAgentCommissionRequest req, Guid userId, CancellationToken ct = default);
    Task<Result<AgentCommissionDto>> DisableAsync(long id, DateOnly? disabledDate, CancellationToken ct = default);

    // Used by InvoicingService at invoice time
    Task<decimal?> GetActiveRateAsync(Guid agentId, string? lineOfBusiness, DateOnly asOfDate, CancellationToken ct = default);
}
