using IMS.Domain.Enums;

namespace IMS.Application.DTOs.DocumentTemplates;

public class DocumentTemplateDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TemplateEntityType EntityType { get; set; }
    public string HtmlContent { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class DocumentTemplateListItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TemplateEntityType EntityType { get; set; }
    public bool IsActive { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

public class DocumentTemplateCreateDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TemplateEntityType EntityType { get; set; }
    public string HtmlContent { get; set; } = string.Empty;
}

public class DocumentTemplateUpdateDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TemplateEntityType EntityType { get; set; }
    public string HtmlContent { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
