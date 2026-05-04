using SIMS.Application.DTOs.UWWriteup;

namespace SIMS.Application.Interfaces.Services;

public interface IUWWriteupService
{
    Task<UWWriteupDto> GetOrCreateAsync(Guid quoteId, Guid userId, CancellationToken ct = default);
    Task<UWWriteupDto> SaveAsync(Guid quoteId, SaveWriteupDto dto, CancellationToken ct = default);
    Task<UWWriteupDto> SubmitAsync(Guid quoteId, SubmitWriteupDto dto, Guid userId, CancellationToken ct = default);
    Task<UWWriteupDto> ApproveAsync(Guid quoteId, Guid userId, CancellationToken ct = default);
}
