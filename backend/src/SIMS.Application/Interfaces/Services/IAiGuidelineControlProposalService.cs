using SIMS.Application.Common;
using SIMS.Application.DTOs.Underwriting;

namespace SIMS.Application.Interfaces.Services;

public interface IAiGuidelineControlProposalService
{
    Task<Result<AiGuidelineControlProposalResult>> ProposeFromTextAsync(
        AiGuidelineControlProposalRequest request,
        Guid userId,
        CancellationToken ct = default);

    Task<Result<AiGuidelineControlProposalResult>> ProposeFromAttachmentAsync(
        AiGuidelineControlProposalFromAttachmentRequest request,
        Guid userId,
        CancellationToken ct = default);
}
