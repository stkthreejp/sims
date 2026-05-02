using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities;

public class DocumentTemplate : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TemplateEntityType EntityType { get; set; }
    public string HtmlContent { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public Guid CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;
}
