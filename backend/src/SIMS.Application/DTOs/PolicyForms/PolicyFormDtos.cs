using SIMS.Domain.Enums;

namespace SIMS.Application.DTOs.PolicyForms;

public class PolicyFormTemplateDto
{
    public Guid Id { get; set; }
    public string FormNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? EditionDate { get; set; }
    public DocumentType DocumentType { get; set; }
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public string? StoragePath { get; set; }
    public bool IsFillable { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
    public List<PolicyFormFieldMappingDto> FieldMappings { get; set; } = [];
    public DateTime UpdatedAt { get; set; }
}

public class PolicyFormTemplateUpsertDto
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
}

public class PolicyFormFieldMappingDto
{
    public Guid Id { get; set; }
    public string PdfFieldName { get; set; } = string.Empty;
    public string DataPath { get; set; } = string.Empty;
    public string? Format { get; set; }
}

public class PolicyFormFieldMappingUpsertDto
{
    public string PdfFieldName { get; set; } = string.Empty;
    public string DataPath { get; set; } = string.Empty;
    public string? Format { get; set; }
}

public class DocumentTagDto
{
    public string Tag { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string DataType { get; set; } = "Text";
    public string? DefaultFormat { get; set; }
    public bool IsRepeatable { get; set; }
    public string? RepeatBlock { get; set; }
}

public class PolicyPackageConfigurationDto
{
    public Guid Id { get; set; }
    public Guid CarrierId { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public PolicyLineOfBusiness LineOfBusiness { get; set; }
    public string State { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public List<PolicyPackageFormDto> Forms { get; set; } = [];
    public DateTime UpdatedAt { get; set; }
}

public class PolicyPackageConfigurationUpsertDto
{
    public Guid CarrierId { get; set; }
    public PolicyLineOfBusiness LineOfBusiness { get; set; }
    public string State { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class PolicyPackageFormDto
{
    public Guid Id { get; set; }
    public Guid PolicyFormTemplateId { get; set; }
    public string FormNumber { get; set; } = string.Empty;
    public string FormName { get; set; } = string.Empty;
    public string? EditionDate { get; set; }
    public int SequenceOrder { get; set; }
    public PolicyFormType FormType { get; set; }
    public string? TriggerConditionJson { get; set; }
    public string? Notes { get; set; }
}

public class PolicyPackageFormUpsertDto
{
    public Guid PolicyFormTemplateId { get; set; }
    public int SequenceOrder { get; set; }
    public PolicyFormType FormType { get; set; } = PolicyFormType.Mandatory;
    public string? TriggerConditionJson { get; set; }
    public string? Notes { get; set; }
}
