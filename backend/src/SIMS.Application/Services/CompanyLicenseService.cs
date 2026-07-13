using Microsoft.EntityFrameworkCore;
using SIMS.Application.Common;
using SIMS.Application.DTOs.CompanyLicenses;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;

namespace SIMS.Application.Services;

public class CompanyLicenseService : ICompanyLicenseService
{
    private readonly DbContext _db;

    public CompanyLicenseService(DbContext db) => _db = db;

    public async Task<IReadOnlyList<CompanyLicenseDto>> GetAllAsync(bool includeInactive, CancellationToken ct = default)
    {
        var query = _db.Set<CompanyLicense>().AsQueryable();
        if (!includeInactive)
            query = query.Where(l => l.IsActive);
        var list = await query.OrderBy(l => l.HolderName).ThenBy(l => l.LicenseState).ToListAsync(ct);
        return list.Select(Map).ToList();
    }

    public async Task<Result<CompanyLicenseDto>> CreateAsync(UpsertCompanyLicenseRequest req, CancellationToken ct = default)
    {
        var validation = Validate(req);
        if (validation is not null)
            return Result<CompanyLicenseDto>.Failure(validation.Value.Code, validation.Value.Message);

        var license = new CompanyLicense();
        Apply(license, req);
        _db.Set<CompanyLicense>().Add(license);
        await _db.SaveChangesAsync(ct);
        return Result<CompanyLicenseDto>.Success(Map(license));
    }

    public async Task<Result<CompanyLicenseDto>> UpdateAsync(Guid id, UpsertCompanyLicenseRequest req, CancellationToken ct = default)
    {
        var validation = Validate(req);
        if (validation is not null)
            return Result<CompanyLicenseDto>.Failure(validation.Value.Code, validation.Value.Message);

        var license = await _db.Set<CompanyLicense>().FirstOrDefaultAsync(l => l.Id == id, ct);
        if (license is null)
            return Result<CompanyLicenseDto>.Failure("NOT_FOUND", "Company license not found.");

        Apply(license, req);
        license.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result<CompanyLicenseDto>.Success(Map(license));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var license = await _db.Set<CompanyLicense>().FirstOrDefaultAsync(l => l.Id == id, ct);
        if (license is null)
            return Result.Failure("NOT_FOUND", "Company license not found.");

        license.IsDeleted = true;
        license.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private const int MaxImportRows = 1000;

    public async Task<Result<ImportCompanyLicensesResult>> ImportAsync(IReadOnlyList<UpsertCompanyLicenseRequest> rows, CancellationToken ct = default)
    {
        if (rows.Count == 0)
            return Result<ImportCompanyLicensesResult>.Failure("EMPTY_IMPORT", "No rows to import.");
        if (rows.Count > MaxImportRows)
            return Result<ImportCompanyLicensesResult>.Failure("TOO_MANY_ROWS", $"Import contains {rows.Count} rows; the maximum per file is {MaxImportRows}.");

        // Existing (holder + number + state) keys so a re-import skips duplicates instead of doubling up.
        var existing = (await _db.Set<CompanyLicense>()
                .Select(l => new { l.HolderName, l.LicenseNumber, l.LicenseState })
                .ToListAsync(ct))
            .Select(l => Key(l.HolderName, l.LicenseNumber, l.LicenseState))
            .ToHashSet();

        var errors = new List<CompanyLicenseImportError>();
        var created = 0;
        var skipped = 0;
        for (var i = 0; i < rows.Count; i++)
        {
            var req = rows[i];
            var validation = Validate(req);
            if (validation is not null)
            {
                errors.Add(new CompanyLicenseImportError(i + 1, validation.Value.Message));
                continue;
            }

            var key = Key(req.HolderName, req.LicenseNumber, req.LicenseState);
            if (!existing.Add(key))
            {
                skipped++;
                continue;
            }

            var license = new CompanyLicense();
            Apply(license, req);
            _db.Set<CompanyLicense>().Add(license);
            created++;
        }

        await _db.SaveChangesAsync(ct);
        return Result<ImportCompanyLicensesResult>.Success(new ImportCompanyLicensesResult(created, skipped, errors));
    }

    private static string Key(string holder, string number, string state) =>
        $"{holder.Trim().ToUpperInvariant()}|{number.Trim().ToUpperInvariant()}|{state.Trim().ToUpperInvariant()}";

    private static (string Code, string Message)? Validate(UpsertCompanyLicenseRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.HolderName))
            return ("VALIDATION", "License holder name is required.");
        if (string.IsNullOrWhiteSpace(req.LicenseNumber))
            return ("VALIDATION", "License number is required.");
        if (string.IsNullOrWhiteSpace(req.LicenseState) || req.LicenseState.Trim().Length != 2)
            return ("VALIDATION", "License state must be a two-letter code.");
        if (string.IsNullOrWhiteSpace(req.LicenseType))
            return ("VALIDATION", "License type is required.");
        if (req.EffectiveDate.HasValue && req.ExpirationDate.HasValue && req.ExpirationDate < req.EffectiveDate)
            return ("VALIDATION", "Expiration date must be on or after the effective date.");
        return null;
    }

    private static void Apply(CompanyLicense l, UpsertCompanyLicenseRequest req)
    {
        l.HolderName = req.HolderName.Trim();
        l.LicenseNumber = req.LicenseNumber.Trim();
        l.LicenseState = req.LicenseState.Trim().ToUpperInvariant();
        l.LicenseType = req.LicenseType.Trim();
        l.EffectiveDate = req.EffectiveDate;
        l.ExpirationDate = req.ExpirationDate;
        l.AddressLine1 = req.AddressLine1?.Trim();
        l.AddressLine2 = req.AddressLine2?.Trim();
        l.City = req.City?.Trim();
        l.State = string.IsNullOrWhiteSpace(req.State) ? null : req.State.Trim().ToUpperInvariant();
        l.ZipCode = req.ZipCode?.Trim();
        l.Country = string.IsNullOrWhiteSpace(req.Country) ? "USA" : req.Country.Trim();
        l.IsActive = req.IsActive;
        l.Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim();
    }

    private static CompanyLicenseDto Map(CompanyLicense l) => new(
        l.Id, l.HolderName, l.LicenseNumber, l.LicenseState, l.LicenseType,
        l.EffectiveDate, l.ExpirationDate, l.AddressLine1, l.AddressLine2, l.City, l.State, l.ZipCode, l.Country,
        l.IsActive, l.Notes);
}
