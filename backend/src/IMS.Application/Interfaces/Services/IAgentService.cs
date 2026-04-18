using IMS.Application.Common;
using IMS.Application.DTOs.Agents;

namespace IMS.Application.Interfaces.Services;

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
}
