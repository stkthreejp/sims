using SIMS.Application.DTOs.UWWriteup;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;

namespace SIMS.Application.Interfaces.Services;

public interface IUnderwritingReferralService
{
    Task SyncFromWriteupAsync(
        Guid quoteId,
        Guid userId,
        IMWriteupPayload payload,
        CancellationToken ct = default);

    Task<bool> HasOpenRequiredReferralsAsync(
        Guid submissionId,
        CancellationToken ct = default);

    Task<UnderwritingReferral> DecideAsync(
        Guid referralId,
        UnderwritingReferralStatus decision,
        Guid decisionById,
        string? notes,
        CancellationToken ct = default);
}
