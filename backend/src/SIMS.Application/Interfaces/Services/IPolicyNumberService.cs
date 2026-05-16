using SIMS.Application.Common;
using SIMS.Domain.Entities;

namespace SIMS.Application.Interfaces.Services;

public interface IPolicyNumberService
{
    Task<Result<PolicyNumberGenerationResult>> GenerateForBindAsync(Quote quote, Guid assignedById);
}

public sealed record PolicyNumberGenerationResult(
    string PolicyNumber,
    string BasePolicyNumber,
    int TermNumber,
    Guid? SequenceId,
    Guid? AssignmentId,
    long? SequenceValue);
