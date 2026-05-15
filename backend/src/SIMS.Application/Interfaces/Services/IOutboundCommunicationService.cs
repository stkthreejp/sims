using SIMS.Application.Common;
using SIMS.Application.DTOs.OutboundCommunications;
using SIMS.Domain.Enums;

namespace SIMS.Application.Interfaces.Services;

public interface IOutboundCommunicationService
{
    Task<IEnumerable<OutboundCommunicationListItemDto>> GetForEntityAsync(OutboundCommunicationEntityType entityType, Guid entityId);
    Task<Result<OutboundCommunicationDto>> GetByIdAsync(Guid id);
    Task<Result<OutboundCommunicationDto>> CreateDraftAsync(OutboundCommunicationCreateDto dto, Guid createdById);
    Task<Result<OutboundCommunicationDto>> UpdateDraftAsync(Guid id, OutboundCommunicationUpdateDto dto);
    Task<Result<OutboundCommunicationDto>> UpdateStatusAsync(Guid id, OutboundCommunicationStatusUpdateDto dto, Guid userId);
    Task<Result<OutboundCommunicationDto>> SendAsync(Guid id, Guid userId);
}
