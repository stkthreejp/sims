using IMS.Application.Common;
using IMS.Application.DTOs.Tasks;

namespace IMS.Application.Interfaces.Services;

public interface IWorkflowTemplateService
{
    Task<IEnumerable<WorkflowTemplateListItemDto>> GetAllAsync();
    Task<Result<WorkflowTemplateDto>> GetByIdAsync(Guid id);
    Task<Result<WorkflowTemplateDto>> CreateAsync(WorkflowTemplateCreateDto dto);
    Task<Result<WorkflowTemplateDto>> UpdateAsync(Guid id, WorkflowTemplateUpdateDto dto);
    Task<Result> DeleteAsync(Guid id);
    Task<Result<WorkflowTemplateDto>> SetStepsAsync(Guid id, List<WorkflowStepUpsertDto> steps);
}
