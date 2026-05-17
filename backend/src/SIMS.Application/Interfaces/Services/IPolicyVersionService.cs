using SIMS.Domain.Entities;

namespace SIMS.Application.Interfaces.Services;

public interface IPolicyVersionService
{
    Task<PolicyVersion> EnsureCurrentVersionAsync(Policy policy, Guid userId, CancellationToken ct = default);
    Task<PolicyVersion> CreateVersionAsync(Policy policy, PolicyTransaction transaction, PolicyVersion? priorVersion, Guid userId, CancellationToken ct = default);
}
