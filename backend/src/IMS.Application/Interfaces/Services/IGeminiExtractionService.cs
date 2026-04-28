using IMS.Application.DTOs.Gemini;
using IMS.Domain.Entities;

namespace IMS.Application.Interfaces.Services;

public interface IGeminiExtractionService
{
    Task<GeminiExtractionResult?> ExtractFromAttachmentsAsync(
        IEnumerable<EmailAttachment> attachments, CancellationToken ct = default);
}
