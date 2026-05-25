using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities.Bordereaux;

public class BordereauxProfile : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public Guid ProgramConfigurationId { get; set; }
    public Guid CarrierId { get; set; }
    public PolicyLineOfBusiness? LineOfBusiness { get; set; }
    public string? StateCode { get; set; }
    public BordereauxReportType ReportType { get; set; } = BordereauxReportType.Premium;
    public BordereauxFrequency Frequency { get; set; } = BordereauxFrequency.Monthly;
    public BordereauxOutputFormat OutputFormat { get; set; } = BordereauxOutputFormat.Xlsx;
    public BordereauxDateBasis DateBasis { get; set; } = BordereauxDateBasis.EffectiveOrBoundDateGreater;
    public bool RequiresAccountCurrent { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public string RequiredTabsJson { get; set; } = "[]";
    public string RequiredColumnsJson { get; set; } = "[]";
    public string MappingRulesJson { get; set; } = "{}";
    public string StaticValuesJson { get; set; } = "{}";
    public string ValidationRulesJson { get; set; } = "{}";
    public string IncludedTransactionTypesJson { get; set; } = "[]";
    public string? Notes { get; set; }

    public ProgramConfiguration ProgramConfiguration { get; set; } = null!;
    public Carrier Carrier { get; set; } = null!;
    public ICollection<BordereauxRun> Runs { get; set; } = new List<BordereauxRun>();
}
