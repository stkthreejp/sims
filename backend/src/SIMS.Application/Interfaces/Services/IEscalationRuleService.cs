using SIMS.Application.Common;
using SIMS.Application.DTOs.Tasks;

namespace SIMS.Application.Interfaces.Services;

public interface IEscalationRuleService
{
    Task<IEnumerable<EscalationRuleDto>> GetAllAsync();
    Task<Result<EscalationRuleDto>> GetByIdAsync(Guid id);
    Task<Result<EscalationRuleDto>> CreateAsync(EscalationRuleCreateDto dto);
    Task<Result<EscalationRuleDto>> UpdateAsync(Guid id, EscalationRuleUpdateDto dto);
    Task<Result> DeleteAsync(Guid id);
}
