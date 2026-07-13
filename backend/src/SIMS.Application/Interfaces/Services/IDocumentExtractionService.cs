using SIMS.Application.DTOs.DocumentExtraction;
using SIMS.Domain.Entities;

namespace SIMS.Application.Interfaces.Services;

public interface IDocumentExtractionService
{
    /// <summary>
    /// Returns one <see cref="DocumentLobExtraction"/> per detected line of business, or null if no eligible PDFs were found.
    /// Each unknown/generic PDF is scanned for its LOBs first; recognised ACORD types skip that step.
    /// </summary>
    Task<List<DocumentLobExtraction>?> ExtractFromAttachmentsAsync(
        IEnumerable<EmailAttachment> attachments, string? lineOfBusinessHint = null, CancellationToken ct = default);
}
