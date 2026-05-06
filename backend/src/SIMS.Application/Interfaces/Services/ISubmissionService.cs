using SIMS.Application.Common;
using SIMS.Application.DTOs.Submissions;
using SIMS.Application.Security;

namespace SIMS.Application.Interfaces.Services;

public interface ISubmissionService
{
    Task<PagedResult<SubmissionListItemDto>> GetAllAsync(QueryParameters query, UserAccessScope access);
    Task<IEnumerable<SubmissionListItemDto>> GetByInsuredAsync(Guid insuredId, UserAccessScope access);
    Task<Result<SubmissionDto>> GetByIdAsync(Guid id, UserAccessScope access);
    Task<Result<SubmissionDto>> CreateAsync(SubmissionCreateDto dto, Guid createdById);
    Task<Result<SubmissionDto>> UpdateAsync(Guid id, SubmissionUpdateDto dto, UserAccessScope access);
    Task<Result<SubmissionDto>> SetLinesOfBusinessAsync(Guid id, List<string> lobs, UserAccessScope access);
    Task<Result> DeleteAsync(Guid id, UserAccessScope access);
}
