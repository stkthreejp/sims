using SIMS.Application.Common;
using SIMS.Application.DTOs.InboundEmails;
using SIMS.Application.DTOs.Submissions;

namespace SIMS.Application.Interfaces.Services;

public interface IInboundEmailService
{
    Task<IEnumerable<InboundEmailListItemDto>> GetUnprocessedAsync();
    Task<Result<InboundEmailDto>> GetByIdAsync(Guid id);
    Task<Result<CreateSubmissionFromEmailResponse>> CreateSubmissionFromEmailAsync(Guid emailId, Guid currentUserId, Guid? insuredId = null, List<Guid>? attachmentIds = null, string? lineOfBusiness = null);
    Task<Result<string>> ReExtractAsync(Guid emailId, Guid currentUserId, string? lineOfBusiness = null);
}
