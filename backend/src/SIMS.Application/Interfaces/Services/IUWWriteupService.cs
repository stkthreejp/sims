using SIMS.Application.DTOs.UWWriteup;
using SIMS.Application.Security;

namespace SIMS.Application.Interfaces.Services;

public interface IUWWriteupService
{
    Task<UWWriteupDto> GetOrCreateAsync(Guid quoteId, Guid userId, UserAccessScope access, CancellationToken ct = default);
    Task<UWWriteupDto> SaveAsync(Guid quoteId, SaveWriteupDto dto, Guid userId, UserAccessScope access, CancellationToken ct = default);
    Task<UWWriteupDto> SubmitAsync(Guid quoteId, SubmitWriteupDto dto, Guid userId, UserAccessScope access, CancellationToken ct = default);
    Task<UWWriteupDto> ApproveAsync(Guid quoteId, Guid userId, UserAccessScope access, CancellationToken ct = default);
}
