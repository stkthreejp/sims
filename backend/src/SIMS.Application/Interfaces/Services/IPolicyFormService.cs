using SIMS.Application.Common;
using SIMS.Application.DTOs.PolicyForms;
using SIMS.Domain.Enums;

namespace SIMS.Application.Interfaces.Services;

public interface IPolicyFormService
{
    Task<IReadOnlyList<PolicyFormTemplateDto>> GetTemplatesAsync(bool includeInactive = false);
    Task<Result<PolicyFormTemplateDto>> GetTemplateAsync(Guid id);
    Task<Result<PolicyFormTemplateDto>> CreateTemplateAsync(PolicyFormTemplateUpsertDto dto);
    Task<Result<PolicyFormTemplateDto>> UpdateTemplateAsync(Guid id, PolicyFormTemplateUpsertDto dto);
    Task<Result> DeleteTemplateAsync(Guid id);
    Task<Result<IReadOnlyList<PolicyFormFieldMappingDto>>> ReplaceMappingsAsync(Guid templateId, IReadOnlyList<PolicyFormFieldMappingUpsertDto> mappings);

    Task<IReadOnlyList<PolicyPackageConfigurationDto>> GetPackagesAsync(Guid? carrierId = null, PolicyLineOfBusiness? lineOfBusiness = null, string? state = null, bool includeInactive = false);
    Task<Result<PolicyPackageConfigurationDto>> GetPackageAsync(Guid id);
    Task<Result<PolicyPackageConfigurationDto>> CreatePackageAsync(PolicyPackageConfigurationUpsertDto dto);
    Task<Result<PolicyPackageConfigurationDto>> UpdatePackageAsync(Guid id, PolicyPackageConfigurationUpsertDto dto);
    Task<Result> DeletePackageAsync(Guid id);
    Task<Result<PolicyPackageConfigurationDto>> ReplacePackageFormsAsync(Guid packageId, IReadOnlyList<PolicyPackageFormUpsertDto> forms);
}
