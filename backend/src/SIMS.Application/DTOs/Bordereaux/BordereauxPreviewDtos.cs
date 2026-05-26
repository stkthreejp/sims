using SIMS.Domain.Enums;

namespace SIMS.Application.DTOs.Bordereaux;

public record BordereauxPremiumPreviewDto(
    Guid ProfileId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    IReadOnlyList<BordereauxPremiumPreviewRowDto> Rows,
    decimal GrossPremiumTotal,
    decimal GrossCommissionTotal,
    decimal FeesTotal,
    decimal NetDueCarrierTotal);

public record BordereauxPremiumPreviewRowDto(
    Guid PolicyId,
    Guid PolicyTransactionId,
    long InvoiceId,
    string PolicyNumber,
    string TransactionNumber,
    TransactionType TransactionType,
    DateOnly ReportingDate,
    DateOnly TransactionEffectiveDate,
    DateOnly BilledDate,
    DateOnly? ExpirationDate,
    string InsuredName,
    string InsuredState,
    Guid? ProgramConfigurationId,
    string? ProgramName,
    Guid CarrierId,
    string CarrierName,
    PolicyLineOfBusiness LineOfBusiness,
    decimal GrossPremium,
    decimal GrossCommission,
    decimal Fees,
    decimal TotalAmount,
    decimal NetDueCarrier,
    string InvoiceNumber,
    string InsuredAddress,
    string InsuredPostcode,
    string InsuredCounty,
    DateOnly? PolicyIssuanceDate,
    string IndustrialSector,
    string NewRenewalIndicator);
