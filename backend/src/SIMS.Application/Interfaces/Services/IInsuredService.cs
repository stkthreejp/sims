using SIMS.Application.Common;
using SIMS.Application.DTOs.Insureds;

namespace SIMS.Application.Interfaces.Services;

public interface IInsuredService
{
    Task<PagedResult<InsuredListItemDto>> GetAllAsync(QueryParameters query);
    Task<Result<InsuredDto>> GetByIdAsync(Guid id);
    Task<Result<InsuredDto>> CreateAsync(InsuredCreateDto dto, Guid createdById);
    Task<Result<InsuredDto>> UpdateAsync(Guid id, InsuredUpdateDto dto);
    Task<Result> DeleteAsync(Guid id);
    Task<InsuredSummaryStatsDto> GetSummaryStatsAsync();
}
