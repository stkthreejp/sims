using SIMS.Application.Common;
using SIMS.Application.DTOs.DocumentTemplates;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace SIMS.Application.Services;

public class DocumentTemplateService : IDocumentTemplateService
{
    private readonly IServiceProvider _sp;
    public DocumentTemplateService(IServiceProvider sp) => _sp = sp;

    private DbContext Db => (DbContext)_sp.GetService(typeof(DbContext))!;

    public async Task<IEnumerable<DocumentTemplateListItemDto>> GetAllAsync(
        TemplateEntityType? entityType = null,
        bool includeInactive = false,
        DocumentTemplateKind? kind = null)
    {
        IQueryable<DocumentTemplate> q = Db.Set<DocumentTemplate>()
            .Include(t => t.CreatedBy);

        if (entityType.HasValue)
            q = q.Where(t => t.EntityType == entityType.Value);

        if (kind.HasValue)
            q = q.Where(t => t.Kind == kind.Value);

        if (!includeInactive)
            q = q.Where(t => t.IsActive);

        var templates = await q.OrderBy(t => t.EntityType).ThenBy(t => t.Name).ToListAsync();

        return templates.Select(t => new DocumentTemplateListItemDto
        {
            Id = t.Id,
            Name = t.Name,
            Description = t.Description,
            EntityType = t.EntityType,
            Kind = t.Kind,
            IsActive = t.IsActive,
            CreatedByName = t.CreatedBy?.FullName ?? string.Empty,
            UpdatedAt = t.UpdatedAt,
        });
    }

    public async Task<Result<DocumentTemplateDto>> GetByIdAsync(Guid id)
    {
        var template = await Db.Set<DocumentTemplate>()
            .Include(t => t.CreatedBy)
            .FirstOrDefaultAsync(t => t.Id == id);

        return template == null
            ? Result<DocumentTemplateDto>.Failure("NOT_FOUND", "Template not found.")
            : Result<DocumentTemplateDto>.Success(MapToDto(template));
    }

    public async Task<Result<DocumentTemplateDto>> CreateAsync(DocumentTemplateCreateDto dto, Guid createdById)
    {
        var template = new DocumentTemplate
        {
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim(),
            EntityType = dto.EntityType,
            Kind = dto.Kind,
            HtmlContent = dto.HtmlContent,
            SubjectTemplate = dto.SubjectTemplate?.Trim(),
            EmailBodyHtml = dto.EmailBodyHtml,
            IsActive = true,
            CreatedById = createdById,
        };

        Db.Set<DocumentTemplate>().Add(template);
        await Db.SaveChangesAsync();

        // Reload with navigation
        return await GetByIdAsync(template.Id);
    }

    public async Task<Result<DocumentTemplateDto>> UpdateAsync(Guid id, DocumentTemplateUpdateDto dto)
    {
        var template = await Db.Set<DocumentTemplate>().FindAsync(id);
        if (template == null)
            return Result<DocumentTemplateDto>.Failure("NOT_FOUND", "Template not found.");

        template.Name = dto.Name.Trim();
        template.Description = dto.Description?.Trim();
        template.EntityType = dto.EntityType;
        template.Kind = dto.Kind;
        template.HtmlContent = dto.HtmlContent;
        template.SubjectTemplate = dto.SubjectTemplate?.Trim();
        template.EmailBodyHtml = dto.EmailBodyHtml;
        template.IsActive = dto.IsActive;

        await Db.SaveChangesAsync();
        return await GetByIdAsync(template.Id);
    }

    public async Task<Result> DeleteAsync(Guid id)
    {
        var template = await Db.Set<DocumentTemplate>().FindAsync(id);
        if (template == null)
            return Result.Failure("NOT_FOUND", "Template not found.");

        template.IsDeleted = true;
        template.DeletedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync();
        return Result.Success();
    }

    private static DocumentTemplateDto MapToDto(DocumentTemplate t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        Description = t.Description,
        EntityType = t.EntityType,
        Kind = t.Kind,
        HtmlContent = t.HtmlContent,
        SubjectTemplate = t.SubjectTemplate,
        EmailBodyHtml = t.EmailBodyHtml,
        IsActive = t.IsActive,
        CreatedByName = t.CreatedBy?.FullName ?? string.Empty,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt,
    };
}
