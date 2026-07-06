using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities;

public class PolicyFormTemplate : BaseEntity
{
    public string FormNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? EditionDate { get; set; }
    public DocumentType DocumentType { get; set; } = DocumentType.PolicyForm;
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public string? StoragePath { get; set; }
    public bool IsFillable { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    // F16: instead of an uploaded binary, a form may be an authored Document Library template
    // (HTML + merge fields), rendered into the packet by PolicyAssemblyService.
    public Guid? DocumentTemplateId { get; set; }
    public DocumentTemplate? DocumentTemplate { get; set; }

    public ICollection<PolicyFormFieldMapping> FieldMappings { get; set; } = new List<PolicyFormFieldMapping>();
}
