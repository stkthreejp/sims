using SIMS.Application.Common;
using SIMS.Application.DTOs.Carriers;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace SIMS.Application.Services;

public class CarrierService : ICarrierService
{
    private readonly IServiceProvider _serviceProvider;

    public CarrierService(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    private DbContext Db =>
        (DbContext)_serviceProvider.GetService(typeof(DbContext))!;

    // ─── Core CRUD ────────────────────────────────────────────────────────────

    public async Task<IEnumerable<CarrierListItemDto>> GetAllAsync(bool activeOnly = false)
    {
        IQueryable<Carrier> q = Db.Set<Carrier>()
            .Include(c => c.LinesOfBusiness)
            .Include(c => c.Contacts);

        if (activeOnly) q = q.Where(c => c.IsActive);

        var carriers = await q.OrderBy(c => c.Name).ToListAsync();
        return carriers.Select(MapToListItem);
    }

    public async Task<Result<CarrierDto>> GetByIdAsync(Guid id)
    {
        var carrier = await Db.Set<Carrier>()
            .Include(c => c.LinesOfBusiness)
            .Include(c => c.Contacts)
            .FirstOrDefaultAsync(c => c.Id == id);

        return carrier == null
            ? Result<CarrierDto>.Failure("NOT_FOUND", "Carrier not found.")
            : Result<CarrierDto>.Success(MapToDto(carrier));
    }

    public async Task<Result<CarrierDto>> CreateAsync(CarrierCreateDto dto)
    {
        if (await Db.Set<Carrier>().AnyAsync(c => c.Name == dto.Name))
            return Result<CarrierDto>.Failure("DUPLICATE_NAME", "A carrier with this name already exists.");

        var carrier = new Carrier
        {
            Name = dto.Name.Trim(),
            Naic = dto.Naic?.Trim(),
            AmBestRating = dto.AmBestRating?.Trim(),
            AddressLine1 = dto.AddressLine1,
            AddressLine2 = dto.AddressLine2,
            City = dto.City,
            State = dto.State,
            ZipCode = dto.ZipCode,
            Website = dto.Website,
            DefaultCurrencyCode = NormalizeCurrency(dto.DefaultCurrencyCode),
            IsActive = true,
        };

        foreach (var lob in dto.LinesOfBusiness.Distinct())
            carrier.LinesOfBusiness.Add(new CarrierLineOfBusiness { CarrierId = carrier.Id, LineOfBusiness = lob });

        Db.Set<Carrier>().Add(carrier);
        await Db.SaveChangesAsync();

        return Result<CarrierDto>.Success(MapToDto(carrier));
    }

    public async Task<Result<CarrierDto>> UpdateAsync(Guid id, CarrierUpdateDto dto)
    {
        var carrier = await Db.Set<Carrier>()
            .Include(c => c.LinesOfBusiness)
            .Include(c => c.Contacts)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (carrier == null)
            return Result<CarrierDto>.Failure("NOT_FOUND", "Carrier not found.");

        if (await Db.Set<Carrier>().AnyAsync(c => c.Name == dto.Name && c.Id != id))
            return Result<CarrierDto>.Failure("DUPLICATE_NAME", "Another carrier already has this name.");

        carrier.Name = dto.Name.Trim();
        carrier.Naic = dto.Naic?.Trim();
        carrier.AmBestRating = dto.AmBestRating?.Trim();
        carrier.AddressLine1 = dto.AddressLine1;
        carrier.AddressLine2 = dto.AddressLine2;
        carrier.City = dto.City;
        carrier.State = dto.State;
        carrier.ZipCode = dto.ZipCode;
        carrier.Website = dto.Website;
        carrier.DefaultCurrencyCode = NormalizeCurrency(dto.DefaultCurrencyCode);
        carrier.IsActive = dto.IsActive;
        carrier.UpdatedAt = DateTime.UtcNow;

        // Replace LOBs
        Db.Set<CarrierLineOfBusiness>().RemoveRange(carrier.LinesOfBusiness.ToList());
        foreach (var lob in dto.LinesOfBusiness.Distinct())
            Db.Set<CarrierLineOfBusiness>().Add(new CarrierLineOfBusiness { CarrierId = carrier.Id, LineOfBusiness = lob });

        await Db.SaveChangesAsync();

        return Result<CarrierDto>.Success(MapToDto(carrier));
    }

    public async Task<Result> DeleteAsync(Guid id)
    {
        var carrier = await Db.Set<Carrier>().FirstOrDefaultAsync(c => c.Id == id);
        if (carrier == null)
            return Result.Failure("NOT_FOUND", "Carrier not found.");

        var hasQuotes = await Db.Set<Quote>().AnyAsync(q => q.CarrierId == id);
        if (hasQuotes)
            return Result.Failure("HAS_QUOTES", "Cannot delete a carrier that has quotes. Deactivate it instead.");

        carrier.IsDeleted = true;
        carrier.DeletedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync();
        return Result.Success();
    }

    // ─── Contacts ─────────────────────────────────────────────────────────────

    public async Task<Result<CarrierContactDto>> AddContactAsync(Guid carrierId, CarrierContactInputDto dto)
    {
        var carrier = await Db.Set<Carrier>().FirstOrDefaultAsync(c => c.Id == carrierId);
        if (carrier == null)
            return Result<CarrierContactDto>.Failure("NOT_FOUND", "Carrier not found.");

        if (dto.IsPrimary)
            await ClearPrimaryContacts(carrierId);

        var contact = MapContactInput(dto);
        contact.CarrierId = carrierId;

        Db.Set<CarrierContact>().Add(contact);
        await Db.SaveChangesAsync();

        return Result<CarrierContactDto>.Success(MapContactToDto(contact));
    }

    public async Task<Result<CarrierContactDto>> UpdateContactAsync(Guid carrierId, Guid contactId, CarrierContactInputDto dto)
    {
        var contact = await Db.Set<CarrierContact>()
            .FirstOrDefaultAsync(c => c.Id == contactId && c.CarrierId == carrierId);

        if (contact == null)
            return Result<CarrierContactDto>.Failure("NOT_FOUND", "Contact not found.");

        if (dto.IsPrimary && !contact.IsPrimary)
            await ClearPrimaryContacts(carrierId);

        contact.FirstName = dto.FirstName.Trim();
        contact.LastName = dto.LastName?.Trim();
        contact.Title = dto.Title?.Trim();
        contact.Email = dto.Email?.Trim();
        contact.Phone = dto.Phone?.Trim();
        contact.IsPrimary = dto.IsPrimary;
        contact.UpdatedAt = DateTime.UtcNow;

        await Db.SaveChangesAsync();
        return Result<CarrierContactDto>.Success(MapContactToDto(contact));
    }

    public async Task<Result> DeleteContactAsync(Guid carrierId, Guid contactId)
    {
        var contact = await Db.Set<CarrierContact>()
            .FirstOrDefaultAsync(c => c.Id == contactId && c.CarrierId == carrierId);

        if (contact == null)
            return Result.Failure("NOT_FOUND", "Contact not found.");

        contact.IsDeleted = true;
        contact.DeletedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync();
        return Result.Success();
    }

    // ─── KPIs and Summary ────────────────────────────────────────────────────

    public async Task<CarrierSummaryStatsDto> GetSummaryStatsAsync()
    {
        var totalCarriers = await Db.Set<Carrier>().CountAsync(c => c.IsActive);

        var activeLobCount = await Db.Set<CarrierLineOfBusiness>()
            .CountAsync(l => l.Carrier.IsActive);

        var pendingReviews = await Db.Set<UnderwritingGuidelineControl>()
            .CountAsync(c => c.Status == UnderwritingControlStatus.AiSuggested
                          || c.Status == UnderwritingControlStatus.Draft);

        return new CarrierSummaryStatsDto
        {
            TotalCarriers = totalCarriers,
            ActiveLobCount = activeLobCount,
            PendingGuidelineReviews = pendingReviews,
        };
    }

    public async Task<CarrierKpiDto> GetKpiAsync(Guid carrierId)
    {
        var last12Start = DateTime.UtcNow.Date.AddYears(-1);

        var boundPremium = await Db.Set<Policy>()
            .Where(p => p.CarrierId == carrierId && p.CreatedAt >= last12Start)
            .SumAsync(p => (decimal?)p.TotalPremium) ?? 0m;

        var activeLobCount = await Db.Set<CarrierLineOfBusiness>()
            .CountAsync(l => l.CarrierId == carrierId);

        return new CarrierKpiDto
        {
            BoundPremiumLast12Months = boundPremium,
            ActiveLobCount = activeLobCount,
        };
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private async Task ClearPrimaryContacts(Guid carrierId)
    {
        var primaries = await Db.Set<CarrierContact>()
            .Where(c => c.CarrierId == carrierId && c.IsPrimary)
            .ToListAsync();
        foreach (var c in primaries) c.IsPrimary = false;
    }

    private static CarrierContact MapContactInput(CarrierContactInputDto dto) => new()
    {
        FirstName = dto.FirstName.Trim(),
        LastName = dto.LastName?.Trim(),
        Title = dto.Title?.Trim(),
        Email = dto.Email?.Trim(),
        Phone = dto.Phone?.Trim(),
        IsPrimary = dto.IsPrimary,
    };

    private static CarrierContactDto MapContactToDto(CarrierContact c) => new()
    {
        Id = c.Id,
        FirstName = c.FirstName,
        LastName = c.LastName,
        Title = c.Title,
        Email = c.Email,
        Phone = c.Phone,
        IsPrimary = c.IsPrimary,
    };

    private static CarrierDto MapToDto(Carrier c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        Naic = c.Naic,
        AmBestRating = c.AmBestRating,
        AddressLine1 = c.AddressLine1,
        AddressLine2 = c.AddressLine2,
        City = c.City,
        State = c.State,
        ZipCode = c.ZipCode,
        Website = c.Website,
        DefaultCurrencyCode = c.DefaultCurrencyCode,
        IsActive = c.IsActive,
        LinesOfBusiness = c.LinesOfBusiness.Select(x => x.LineOfBusiness).ToList(),
        Contacts = c.Contacts.OrderByDescending(x => x.IsPrimary).ThenBy(x => x.LastName).ThenBy(x => x.FirstName).Select(MapContactToDto).ToList(),
        CreatedAt = c.CreatedAt,
    };

    private static CarrierListItemDto MapToListItem(Carrier c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        Naic = c.Naic,
        AmBestRating = c.AmBestRating,
        City = c.City,
        State = c.State,
        IsActive = c.IsActive,
        LinesOfBusiness = c.LinesOfBusiness.Select(x => x.LineOfBusiness).ToList(),
        ContactCount = c.Contacts.Count,
    };

    private static string NormalizeCurrency(string? value)
        => string.IsNullOrWhiteSpace(value) ? "USD" : value.Trim().ToUpperInvariant();
}
