using SIMS.Application.DTOs.DocumentAI;

namespace SIMS.Application.Interfaces.Services;

public interface IDocumentAiExtractionService
{
    Task<DocumentAiExtractionResult> ProcessAsync(
        byte[] content,
        string mimeType,
        string fileName,
        CancellationToken cancellationToken = default);
}
