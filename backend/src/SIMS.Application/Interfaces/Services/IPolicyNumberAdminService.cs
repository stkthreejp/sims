using SIMS.Application.Common;
using SIMS.Application.DTOs.PolicyNumbers;

namespace SIMS.Application.Interfaces.Services;

public interface IPolicyNumberAdminService
{
    Task<IReadOnlyList<PolicyNumberSequenceDto>> GetSequencesAsync(bool includeInactive);
    Task<Result<PolicyNumberSequenceDto>> CreateSequenceAsync(PolicyNumberSequenceUpsertDto dto);
    Task<Result<PolicyNumberSequenceDto>> UpdateSequenceAsync(Guid id, PolicyNumberSequenceUpsertDto dto);
    Task<Result> DeleteSequenceAsync(Guid id);
    Task<IReadOnlyList<PolicyNumberAssignmentDto>> GetAssignmentsAsync(bool includeInactive);
    Task<Result<PolicyNumberAssignmentDto>> CreateAssignmentAsync(PolicyNumberAssignmentUpsertDto dto);
    Task<Result<PolicyNumberAssignmentDto>> UpdateAssignmentAsync(Guid id, PolicyNumberAssignmentUpsertDto dto);
    Task<Result> DeleteAssignmentAsync(Guid id);
    PolicyNumberPreviewDto Preview(PolicyNumberPreviewRequestDto dto);
}
