namespace SIMS.Application.DTOs.CompanyLicenses;

public record CompanyLicenseDto(
    Guid Id,
    string HolderName,
    string LicenseNumber,
    string LicenseState,
    string LicenseType,
    DateOnly? EffectiveDate,
    DateOnly? ExpirationDate,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? State,
    string? ZipCode,
    string Country,
    bool IsActive,
    string? Notes);

public record UpsertCompanyLicenseRequest(
    string HolderName,
    string LicenseNumber,
    string LicenseState,
    string LicenseType,
    DateOnly? EffectiveDate,
    DateOnly? ExpirationDate,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? State,
    string? ZipCode,
    string? Country,
    bool IsActive,
    string? Notes);

public record ImportCompanyLicensesRequest(IReadOnlyList<UpsertCompanyLicenseRequest> Rows);

public record CompanyLicenseImportError(int Row, string Message);

public record ImportCompanyLicensesResult(int Created, int Skipped, IReadOnlyList<CompanyLicenseImportError> Errors);
