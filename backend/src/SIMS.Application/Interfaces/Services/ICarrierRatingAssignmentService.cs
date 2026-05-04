using SIMS.Application.Common;
using SIMS.Application.DTOs.Rating;
using SIMS.Domain.Enums;

namespace SIMS.Application.Interfaces.Services;

public interface ICarrierRatingAssignmentService
{
    Task<IReadOnlyList<CarrierRatingAssignmentDto>> GetAllAsync(Guid? carrierId, CancellationToken ct = default);
    Task<Result<CarrierRatingAssignmentDto>> CreateAsync(CarrierRatingAssignmentCreateDto dto, CancellationToken ct = default);
    Task<Result<CarrierRatingAssignmentDto>> UpdateAsync(Guid id, CarrierRatingAssignmentUpdateDto dto, CancellationToken ct = default);
    Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<RatingPlanVersionPickerDto>> GetActiveVersionsForLobAsync(PolicyLineOfBusiness lob, CancellationToken ct = default);
}
