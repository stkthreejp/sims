using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities;

public class DocumentTemplate : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TemplateEntityType EntityType { get; set; }
    public DocumentTemplateKind Kind { get; set; } = DocumentTemplateKind.Document;
    public string HtmlContent { get; set; } = string.Empty;
    public string? SubjectTemplate { get; set; }
    public string? EmailBodyHtml { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;
}
