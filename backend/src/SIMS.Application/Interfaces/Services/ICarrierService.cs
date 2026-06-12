using SIMS.Application.Common;
using SIMS.Application.DTOs.Carriers;

namespace SIMS.Application.Interfaces.Services;

public interface ICarrierService
{
    Task<IEnumerable<CarrierListItemDto>> GetAllAsync(bool activeOnly = false);
    Task<Result<CarrierDto>> GetByIdAsync(Guid id);
    Task<Result<CarrierDto>> CreateAsync(CarrierCreateDto dto);
    Task<Result<CarrierDto>> UpdateAsync(Guid id, CarrierUpdateDto dto);
    Task<Result> DeleteAsync(Guid id);

    // Contacts
    Task<Result<CarrierContactDto>> AddContactAsync(Guid carrierId, CarrierContactInputDto dto);
    Task<Result<CarrierContactDto>> UpdateContactAsync(Guid carrierId, Guid contactId, CarrierContactInputDto dto);
    Task<Result> DeleteContactAsync(Guid carrierId, Guid contactId);

    // KPIs and summary
    Task<CarrierSummaryStatsDto> GetSummaryStatsAsync();
    Task<CarrierKpiDto> GetKpiAsync(Guid carrierId);
}
