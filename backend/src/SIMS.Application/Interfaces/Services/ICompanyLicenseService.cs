using SIMS.Application.Common;
using SIMS.Application.DTOs.CompanyLicenses;

namespace SIMS.Application.Interfaces.Services;

public interface ICompanyLicenseService
{
    Task<IReadOnlyList<CompanyLicenseDto>> GetAllAsync(bool includeInactive, CancellationToken ct = default);
    Task<Result<CompanyLicenseDto>> CreateAsync(UpsertCompanyLicenseRequest req, CancellationToken ct = default);
    Task<Result<CompanyLicenseDto>> UpdateAsync(Guid id, UpsertCompanyLicenseRequest req, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}
