using IMS.Application.Common;
using IMS.Application.DTOs.Submissions;

namespace IMS.Application.Interfaces.Services;

public interface ISubmissionService
{
    Task<PagedResult<SubmissionListItemDto>> GetAllAsync(QueryParameters query);
    Task<IEnumerable<SubmissionListItemDto>> GetByInsuredAsync(Guid insuredId);
    Task<Result<SubmissionDto>> GetByIdAsync(Guid id);
    Task<Result<SubmissionDto>> CreateAsync(SubmissionCreateDto dto, Guid createdById);
    Task<Result<SubmissionDto>> UpdateAsync(Guid id, SubmissionUpdateDto dto);
    Task<Result<SubmissionDto>> SetLinesOfBusinessAsync(Guid id, List<string> lobs);
    Task<Result> DeleteAsync(Guid id);
}
