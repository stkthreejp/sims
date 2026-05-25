using SIMS.Domain.Enums;

namespace SIMS.Application.DTOs.Bordereaux;

public record BordereauxProfileDto(
    Guid Id,
    string Name,
    Guid ProgramConfigurationId,
    string ProgramName,
    Guid CarrierId,
    string CarrierName,
    PolicyLineOfBusiness? LineOfBusiness,
    string? StateCode,
    BordereauxReportType ReportType,
    BordereauxFrequency Frequency,
    BordereauxOutputFormat OutputFormat,
    BordereauxDateBasis DateBasis,
    bool RequiresAccountCurrent,
    bool IsActive,
    string RequiredTabsJson,
    string RequiredColumnsJson,
    string MappingRulesJson,
    string StaticValuesJson,
    string ValidationRulesJson,
    string IncludedTransactionTypesJson,
    string? Notes);

public record UpsertBordereauxProfileRequest(
    string Name,
    Guid ProgramConfigurationId,
    Guid CarrierId,
    PolicyLineOfBusiness? LineOfBusiness,
    string? StateCode,
    BordereauxReportType ReportType,
    BordereauxFrequency Frequency,
    BordereauxOutputFormat OutputFormat,
    BordereauxDateBasis DateBasis,
    bool RequiresAccountCurrent,
    bool IsActive,
    string RequiredTabsJson,
    string RequiredColumnsJson,
    string MappingRulesJson,
    string StaticValuesJson,
    string ValidationRulesJson,
    string IncludedTransactionTypesJson,
    string? Notes);
