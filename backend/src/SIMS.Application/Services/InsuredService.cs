using SIMS.Application.Common;
using SIMS.Application.DTOs.Insureds;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace SIMS.Application.Services;

public class InsuredService : IInsuredService
{
    private readonly IServiceProvider _serviceProvider;

    public InsuredService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    // DbContext is injected via IServiceProvider to avoid circular dependency issues
    private Microsoft.EntityFrameworkCore.DbContext GetDbContext() =>
        (Microsoft.EntityFrameworkCore.DbContext)_serviceProvider.GetService(typeof(Microsoft.EntityFrameworkCore.DbContext))!;

    public async Task<PagedResult<InsuredListItemDto>> GetAllAsync(QueryParameters query)
    {
        var db = GetDbContext();
        var q = db.Set<Insured>()
            .Where(i => !i.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.ToLower();
            q = q.Where(i =>
                (i.FirstName != null && i.FirstName.ToLower().Contains(search)) ||
                (i.LastName != null && i.LastName.ToLower().Contains(search)) ||
                (i.CompanyName != null && i.CompanyName.ToLower().Contains(search)) ||
                (i.Email != null && i.Email.ToLower().Contains(search)));
        }

        var total = await q.CountAsync();

        q = query.SortDir.ToLower() == "asc"
            ? q.OrderBy(i => i.CreatedAt)
            : q.OrderByDescending(i => i.CreatedAt);

        var items = await q
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Include(i => i.Submissions.Where(s => !s.IsDeleted))
            .ToListAsync();

        return new PagedResult<InsuredListItemDto>
        {
            Items = items.Select(i => new InsuredListItemDto
            {
                Id = i.Id,
                InsuredType = i.InsuredType,
                DisplayName = i.DisplayName,
                Email = i.Email,
                Phone = i.Phone,
                City = i.City,
                State = i.State,
                IsActive = i.IsActive,
                PolicyCount = i.Submissions.Count,
                CreatedAt = i.CreatedAt
            }),
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<Result<InsuredDto>> GetByIdAsync(Guid id)
    {
        var db = GetDbContext();
        var insured = await db.Set<Insured>()
            .Include(i => i.Submissions.Where(s => !s.IsDeleted))
            .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);

        if (insured == null)
            return Result<InsuredDto>.Failure("NOT_FOUND", "Insured not found.");

        return Result<InsuredDto>.Success(MapToDto(insured));
    }

    public async Task<Result<InsuredDto>> CreateAsync(InsuredCreateDto dto, Guid createdById)
    {
        var db = GetDbContext();
        var insured = new Insured
        {
            InsuredType = dto.InsuredType,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            DateOfBirth = dto.DateOfBirth,
            CompanyName = dto.CompanyName,
            UsDotNumber = NormalizeDotNumber(dto.UsDotNumber),
            TaxId = dto.TaxId,
            Email = dto.Email,
            Phone = dto.Phone,
            PhoneAlt = dto.PhoneAlt,
            AddressLine1 = dto.AddressLine1,
            AddressLine2 = dto.AddressLine2,
            City = dto.City,
            State = dto.State,
            ZipCode = dto.ZipCode,
            County = dto.County,
            CreatedById = createdById,
        };
        await ApplyGeocodeAsync(insured, dto);

        db.Set<Insured>().Add(insured);
        await db.SaveChangesAsync();

        return Result<InsuredDto>.Success(MapToDto(insured));
    }

    public async Task<Result<InsuredDto>> UpdateAsync(Guid id, InsuredUpdateDto dto)
    {
        var db = GetDbContext();
        var insured = await db.Set<Insured>().FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
        if (insured == null)
            return Result<InsuredDto>.Failure("NOT_FOUND", "Insured not found.");

        var addressChanged =
            !string.Equals(insured.AddressLine1, dto.AddressLine1, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(insured.AddressLine2 ?? string.Empty, dto.AddressLine2 ?? string.Empty, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(insured.City, dto.City, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(insured.State, dto.State, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(insured.ZipCode, dto.ZipCode, StringComparison.OrdinalIgnoreCase);

        insured.InsuredType = dto.InsuredType;
        insured.FirstName = dto.FirstName;
        insured.LastName = dto.LastName;
        insured.DateOfBirth = dto.DateOfBirth;
        insured.CompanyName = dto.CompanyName;
        insured.UsDotNumber = NormalizeDotNumber(dto.UsDotNumber);
        insured.TaxId = dto.TaxId;
        insured.Email = dto.Email;
        insured.Phone = dto.Phone;
        insured.PhoneAlt = dto.PhoneAlt;
        insured.AddressLine1 = dto.AddressLine1;
        insured.AddressLine2 = dto.AddressLine2;
        insured.City = dto.City;
        insured.State = dto.State;
        insured.ZipCode = dto.ZipCode;
        insured.County = dto.County;
        insured.IsActive = dto.IsActive;
        insured.UpdatedAt = DateTime.UtcNow;
        if (addressChanged || dto.Latitude.HasValue || dto.Longitude.HasValue || !insured.Latitude.HasValue || !insured.Longitude.HasValue)
            await ApplyGeocodeAsync(insured, dto, clearOnMiss: addressChanged);

        await db.SaveChangesAsync();

        return Result<InsuredDto>.Success(MapToDto(insured));
    }

    public async Task<Result> DeleteAsync(Guid id)
    {
        var db = GetDbContext();
        var insured = await db.Set<Insured>().FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
        if (insured == null)
            return Result.Failure("NOT_FOUND", "Insured not found.");

        insured.IsDeleted = true;
        insured.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Result.Success();
    }

    private static InsuredDto MapToDto(Insured i) => new()
    {
        Id = i.Id,
        InsuredType = i.InsuredType,
        DisplayName = i.DisplayName,
        FirstName = i.FirstName,
        LastName = i.LastName,
        DateOfBirth = i.DateOfBirth,
        CompanyName = i.CompanyName,
        UsDotNumber = i.UsDotNumber,
        TaxId = i.TaxId,
        Email = i.Email,
        Phone = i.Phone,
        PhoneAlt = i.PhoneAlt,
        AddressLine1 = i.AddressLine1,
        AddressLine2 = i.AddressLine2,
        City = i.City,
        State = i.State,
        ZipCode = i.ZipCode,
        County = i.County,
        Latitude = i.Latitude,
        Longitude = i.Longitude,
        GeocodePrecision = i.GeocodePrecision,
        GeocodeProvider = i.GeocodeProvider,
        GooglePlaceId = i.GooglePlaceId,
        GeocodedAt = i.GeocodedAt,
        IsActive = i.IsActive,
        CreatedAt = i.CreatedAt,
        PolicyCount = i.Submissions?.Count ?? 0
    };

    private static string? NormalizeDotNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = new string(value.Where(char.IsDigit).ToArray());
        return string.IsNullOrWhiteSpace(digits) ? null : digits;
    }

    private async Task ApplyGeocodeAsync(Insured insured, InsuredCreateDto dto, bool clearOnMiss = false)
    {
        if (dto.Latitude.HasValue && dto.Longitude.HasValue)
        {
            insured.Latitude = dto.Latitude.Value;
            insured.Longitude = dto.Longitude.Value;
            insured.GeocodePrecision = dto.GeocodePrecision;
            insured.GeocodeProvider = string.IsNullOrWhiteSpace(dto.GeocodeProvider) ? "GooglePlaces" : dto.GeocodeProvider;
            insured.GooglePlaceId = dto.GooglePlaceId;
            insured.GeocodedAt = DateTime.UtcNow;
            return;
        }

        var geocoder = _serviceProvider.GetService<IGeocodingService>();
        var result = geocoder == null
            ? null
            : await geocoder.GeocodeAsync(new GeocodeRequest(dto.AddressLine1, dto.AddressLine2, dto.City, dto.State, dto.ZipCode));
        if (result != null)
        {
            insured.Latitude = result.Latitude;
            insured.Longitude = result.Longitude;
            insured.GeocodePrecision = result.Precision;
            insured.GeocodeProvider = result.Provider;
            insured.GooglePlaceId = result.GooglePlaceId;
            insured.GeocodedAt = DateTime.UtcNow;
            return;
        }

        if (!clearOnMiss)
            return;

        insured.Latitude = null;
        insured.Longitude = null;
        insured.GeocodePrecision = null;
        insured.GeocodeProvider = null;
        insured.GooglePlaceId = null;
        insured.GeocodedAt = null;
    }
}
