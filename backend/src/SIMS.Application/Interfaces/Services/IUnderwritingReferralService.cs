using SIMS.Application.DTOs.UWWriteup;

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
}
