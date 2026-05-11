using SIMS.Application.Common;
using SIMS.Application.DTOs.Attachments;

namespace SIMS.Application.Interfaces.Services;

public interface IAutoSafetyReportService
{
    Task<Result<AttachmentDto>> GenerateQuoteReportAsync(Guid quoteId, Guid userId, CancellationToken ct = default);
}
