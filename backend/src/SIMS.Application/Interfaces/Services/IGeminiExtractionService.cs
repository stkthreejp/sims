using SIMS.Application.DTOs.Gemini;
using SIMS.Domain.Entities;

namespace SIMS.Application.Interfaces.Services;

public interface IGeminiExtractionService
{
    /// <summary>
    /// Returns one <see cref="GeminiLobExtraction"/> per detected line of business, or null if no eligible PDFs were found.
    /// Each unknown/generic PDF is scanned for its LOBs first; recognised ACORD types skip that step.
    /// </summary>
    Task<List<GeminiLobExtraction>?> ExtractFromAttachmentsAsync(
        IEnumerable<EmailAttachment> attachments, string? lineOfBusinessHint = null, CancellationToken ct = default);
}
