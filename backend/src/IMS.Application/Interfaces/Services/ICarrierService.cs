using IMS.Application.Common;
using IMS.Application.DTOs.Carriers;

namespace IMS.Application.Interfaces.Services;

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
}
