using IMS.Application.Common;
using IMS.Application.DTOs.DocumentTemplates;
using IMS.Domain.Enums;

namespace IMS.Application.Interfaces.Services;

public interface IDocumentTemplateService
{
    Task<IEnumerable<DocumentTemplateListItemDto>> GetAllAsync(TemplateEntityType? entityType = null, bool includeInactive = false);
    Task<Result<DocumentTemplateDto>> GetByIdAsync(Guid id);
    Task<Result<DocumentTemplateDto>> CreateAsync(DocumentTemplateCreateDto dto, Guid createdById);
    Task<Result<DocumentTemplateDto>> UpdateAsync(Guid id, DocumentTemplateUpdateDto dto);
    Task<Result> DeleteAsync(Guid id);
}
