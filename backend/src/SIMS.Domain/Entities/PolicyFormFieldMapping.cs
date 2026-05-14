namespace SIMS.Domain.Entities;

public class PolicyFormFieldMapping : BaseEntity
{
    public Guid PolicyFormTemplateId { get; set; }
    public string PdfFieldName { get; set; } = string.Empty;
    public string DataPath { get; set; } = string.Empty;
    public string? Format { get; set; }

    public PolicyFormTemplate PolicyFormTemplate { get; set; } = null!;
}
