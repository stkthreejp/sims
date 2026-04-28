using IMS.Application.Common;
using IMS.Application.DTOs.InboundEmails;
using IMS.Application.DTOs.Submissions;

namespace IMS.Application.Interfaces.Services;

public interface IInboundEmailService
{
    Task<IEnumerable<InboundEmailListItemDto>> GetUnprocessedAsync();
    Task<Result<InboundEmailDto>> GetByIdAsync(Guid id);
    Task<Result<CreateSubmissionFromEmailResponse>> CreateSubmissionFromEmailAsync(Guid emailId, Guid currentUserId, Guid? insuredId = null, List<Guid>? attachmentIds = null);
    Task<Result<string>> ReExtractAsync(Guid emailId, Guid currentUserId);
}
