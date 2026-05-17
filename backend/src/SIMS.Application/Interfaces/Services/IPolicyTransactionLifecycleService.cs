using SIMS.Application.Common;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;

namespace SIMS.Application.Interfaces.Services;

public interface IPolicyTransactionLifecycleService
{
    Task<Result> RecordCreatedAsync(PolicyTransaction transaction, Guid userId, string? notes = null);
    Task<Result> TransitionAsync(PolicyTransaction transaction, PolicyTransactionStatus toStatus, Guid userId, string? notes = null);
}
