using SIMS.Domain.Entities.Accounting;
using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities;

public class SurplusLinesStateSetup : BaseEntity
{
    public string StateCode { get; set; } = string.Empty;
    public Guid? ProgramConfigurationId { get; set; }
    public Guid? CarrierId { get; set; }
    public PolicyLineOfBusiness? LineOfBusiness { get; set; }
    public Guid? ProgramCarrierLobStateId { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public DateOnly? ExpirationDate { get; set; }
    public bool IsActive { get; set; } = true;
    public bool FilingRequired { get; set; }
    public Guid? CompanyLicenseId { get; set; }
    public string LicenseHolderType { get; set; } = string.Empty;
    public string FilingBrokerName { get; set; } = string.Empty;
    public string LicenseNumber { get; set; } = string.Empty;
    public string LicenseState { get; set; } = string.Empty;
    public string BrokerAddressLine1 { get; set; } = string.Empty;
    public string? BrokerAddressLine2 { get; set; }
    public string BrokerCity { get; set; } = string.Empty;
    public string BrokerState { get; set; } = string.Empty;
    public string BrokerZipCode { get; set; } = string.Empty;
    public string BrokerCountry { get; set; } = "USA";
    public string? StampingWording { get; set; }
    public string? RequiredNoticeText { get; set; }
    public string? PaperworkNotes { get; set; }
    public string? FilingNotes { get; set; }
    public long? SurplusLinesTaxFeeDefinitionId { get; set; }
    public long? StampingFeeDefinitionId { get; set; }
    public long? FilingFeeDefinitionId { get; set; }
    public long? StatePayeeId { get; set; }
    public long? FilingPayeeId { get; set; }
    public bool CreateFilingPayable { get; set; }
    public int? FilingPaymentTermsDays { get; set; }
    public string? FilingFrequency { get; set; }
    public int? FilingDueDayOfMonth { get; set; }
    public string? FilingMethod { get; set; }
    public string? FilingPortalUrl { get; set; }
    public string RequiredFilingFormsJson { get; set; } = "[]";
    public bool DiligentSearchRequired { get; set; }
    public string? DiligentSearchNotes { get; set; }
    public bool AffidavitRequired { get; set; }
    public string? AffidavitNotes { get; set; }

    public ProgramConfiguration? ProgramConfiguration { get; set; }
    public Carrier? Carrier { get; set; }
    public ProgramCarrierLobState? ProgramCarrierLobState { get; set; }
    public FeeDefinition? SurplusLinesTaxFeeDefinition { get; set; }
    public FeeDefinition? StampingFeeDefinition { get; set; }
    public FeeDefinition? FilingFeeDefinition { get; set; }
    public Payee? StatePayee { get; set; }
    public Payee? FilingPayee { get; set; }
    public CompanyLicense? CompanyLicense { get; set; }
}
