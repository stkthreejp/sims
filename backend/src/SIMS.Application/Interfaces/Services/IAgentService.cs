using SIMS.Application.Common;
using SIMS.Application.DTOs.Agents;

namespace SIMS.Application.Interfaces.Services;

public interface IAgentService
{
    Task<IEnumerable<AgentListItemDto>> GetAllAsync(bool activeOnly = false);
    Task<Result<AgentDto>> GetByIdAsync(Guid id);
    Task<Result<AgentDto>> CreateAsync(AgentCreateDto dto);
    Task<Result<AgentDto>> UpdateAsync(Guid id, AgentUpdateDto dto);
    Task<Result> DeleteAsync(Guid id);

    // Locations
    Task<Result<AgentLocationDto>> AddLocationAsync(Guid agentId, AgentLocationInputDto dto);
    Task<Result<AgentLocationDto>> UpdateLocationAsync(Guid agentId, Guid locationId, AgentLocationInputDto dto);
    Task<Result> DeleteLocationAsync(Guid agentId, Guid locationId);

    // Contacts
    Task<Result<AgentContactDto>> AddContactAsync(Guid agentId, Guid locationId, AgentContactInputDto dto);
    Task<Result<AgentContactDto>> UpdateContactAsync(Guid agentId, Guid locationId, Guid contactId, AgentContactInputDto dto);
    Task<Result> DeleteContactAsync(Guid agentId, Guid locationId, Guid contactId);

    // Compliance docs
    Task<AgentComplianceStatusDto> GetComplianceStatusAsync(Guid agentId);
    Task<Result<AgentComplianceDocDto>> UpsertComplianceDocAsync(Guid agentId, string docType, AgentComplianceDocUpsertDto dto);
    Task<Result> DeleteComplianceDocAsync(Guid agentId, string docType);

    // Contact log
    Task<IEnumerable<AgentContactLogDto>> GetContactLogsAsync(Guid agentId);
    Task<Result<AgentContactLogDto>> CreateContactLogAsync(Guid agentId, AgentContactLogCreateDto dto, Guid userId);
    Task<Result> DeleteContactLogAsync(Guid agentId, Guid logId);

    // KPIs and summary
    Task<AgentKpiDto> GetKpiAsync(Guid agentId);
    Task<AgentSummaryStatsDto> GetSummaryStatsAsync();
}
