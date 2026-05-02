using SIMS.Application.Common;
using SIMS.Domain.Enums;

namespace SIMS.Application.Interfaces.Services;

public interface IDocumentGenerationService
{
    /// <summary>
    /// Fills a template with entity data, converts to PDF, stores in blob storage,
    /// and returns a signed download URL.
    /// </summary>
    Task<Result<string>> GenerateAsync(Guid templateId, TemplateEntityType entityType, Guid entityId);
}
