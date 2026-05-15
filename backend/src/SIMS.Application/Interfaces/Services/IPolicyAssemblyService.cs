using SIMS.Application.Common;
using SIMS.Application.Interfaces.Services;

namespace SIMS.Application.Interfaces.Services;

public interface IPolicyAssemblyService
{
    Task<Result<GeneratedDocumentDto>> AssembleAndFileAsync(Guid policyId, Guid userId, bool isPreview = false);
}
