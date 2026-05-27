using Microsoft.EntityFrameworkCore;
using SIMS.Application.Common;
using SIMS.Application.DTOs.Intermediaries;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Entities.Accounting;
using SIMS.Domain.Enums;

namespace SIMS.Application.Services;

public class IntermediaryService : IIntermediaryService
{
    private readonly DbContext _db;

    public IntermediaryService(DbContext db) => _db = db;

    public async Task<IReadOnlyList<IntermediaryListItemDto>> GetAsync(bool includeInactive = false, CancellationToken ct = default)
    {
        var query = _db.Set<Intermediary>()
            .Include(i => i.ProgramCarrierLobSetups)
            .AsQueryable();

        if (!includeInactive)
            query = query.Where(i => i.IsActive);

        var intermediaries = await query
            .OrderBy(i => i.Name)
            .ToListAsync(ct);

        return intermediaries.Select(i => new IntermediaryListItemDto(
            i.Id,
            i.Name,
            i.ReferenceNumber,
            i.Email,
            i.Phone,
            i.City,
            i.State,
            i.IsActive,
            i.ProgramCarrierLobSetups.Count,
            i.ProgramCarrierLobSetups.Count(s => s.IsActive))).ToList();
    }

    public async Task<Result<IntermediaryDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var intermediary = await FindIntermediaryAsync(id, ct);
        return intermediary is null
            ? Result<IntermediaryDto>.Failure("INTERMEDIARY_NOT_FOUND", "Intermediary was not found.")
            : Result<IntermediaryDto>.Success(Map(intermediary));
    }

    public async Task<Result<IntermediaryDto>> CreateAsync(CreateIntermediaryRequest request, CancellationToken ct = default)
    {
        var validation = ValidateName(request.Name);
        if (validation is not null)
            return Result<IntermediaryDto>.Failure(validation.Value.Code, validation.Value.Message);

        var intermediary = new Intermediary
        {
            Name = request.Name.Trim(),
            ReferenceNumber = TrimToNull(request.ReferenceNumber),
            Email = TrimToNull(request.Email),
            Phone = TrimToNull(request.Phone),
            AddressLine1 = TrimToNull(request.AddressLine1),
            AddressLine2 = TrimToNull(request.AddressLine2),
            City = TrimToNull(request.City),
            State = NormalizeState(request.State),
            ZipCode = TrimToNull(request.ZipCode),
            Country = NormalizeCountry(request.Country),
            BankName = TrimToNull(request.BankName),
            BankAccountName = TrimToNull(request.BankAccountName),
            BankAccountLast4 = TrimToNull(request.BankAccountLast4),
            BankRoutingNumber = TrimToNull(request.BankRoutingNumber),
            BankSwiftCode = TrimToNull(request.BankSwiftCode),
            BankInstructions = TrimToNull(request.BankInstructions),
            IsActive = request.IsActive,
            Notes = TrimToNull(request.Notes)
        };

        _db.Set<Intermediary>().Add(intermediary);
        await _db.SaveChangesAsync(ct);

        return Result<IntermediaryDto>.Success(Map(intermediary));
    }

    public async Task<Result<IntermediaryDto>> UpdateAsync(Guid id, UpdateIntermediaryRequest request, CancellationToken ct = default)
    {
        var validation = ValidateName(request.Name);
        if (validation is not null)
            return Result<IntermediaryDto>.Failure(validation.Value.Code, validation.Value.Message);

        var intermediary = await FindIntermediaryAsync(id, ct);
        if (intermediary is null)
            return Result<IntermediaryDto>.Failure("INTERMEDIARY_NOT_FOUND", "Intermediary was not found.");

        intermediary.Name = request.Name.Trim();
        intermediary.ReferenceNumber = TrimToNull(request.ReferenceNumber);
        intermediary.Email = TrimToNull(request.Email);
        intermediary.Phone = TrimToNull(request.Phone);
        intermediary.AddressLine1 = TrimToNull(request.AddressLine1);
        intermediary.AddressLine2 = TrimToNull(request.AddressLine2);
        intermediary.City = TrimToNull(request.City);
        intermediary.State = NormalizeState(request.State);
        intermediary.ZipCode = TrimToNull(request.ZipCode);
        intermediary.Country = NormalizeCountry(request.Country);
        intermediary.BankName = TrimToNull(request.BankName);
        intermediary.BankAccountName = TrimToNull(request.BankAccountName);
        intermediary.BankAccountLast4 = TrimToNull(request.BankAccountLast4);
        intermediary.BankRoutingNumber = TrimToNull(request.BankRoutingNumber);
        intermediary.BankSwiftCode = TrimToNull(request.BankSwiftCode);
        intermediary.BankInstructions = TrimToNull(request.BankInstructions);
        intermediary.IsActive = request.IsActive;
        intermediary.Notes = TrimToNull(request.Notes);

        await _db.SaveChangesAsync(ct);
        return Result<IntermediaryDto>.Success(Map(intermediary));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var intermediary = await _db.Set<Intermediary>()
            .Include(i => i.ProgramCarrierLobSetups)
            .SingleOrDefaultAsync(i => i.Id == id, ct);

        if (intermediary is null)
            return Result.Failure("INTERMEDIARY_NOT_FOUND", "Intermediary was not found.");
        if (intermediary.ProgramCarrierLobSetups.Any())
            return Result.Failure("HAS_BROKERAGE_SETUPS", "Cannot delete an intermediary with brokerage setup rows.");

        intermediary.IsDeleted = true;
        intermediary.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<IntermediaryBrokerageSetupDto>> CreateBrokerageSetupAsync(Guid intermediaryId, UpsertIntermediaryBrokerageSetupRequest request, CancellationToken ct = default)
    {
        var intermediary = await _db.Set<Intermediary>().SingleOrDefaultAsync(i => i.Id == intermediaryId, ct);
        if (intermediary is null)
            return Result<IntermediaryBrokerageSetupDto>.Failure("INTERMEDIARY_NOT_FOUND", "Intermediary was not found.");

        var validation = await ValidateSetupRequestAsync(request, ct);
        if (!validation.IsSuccess)
            return Result<IntermediaryBrokerageSetupDto>.Failure(validation.ErrorCode!, validation.ErrorMessage!);

        var refs = validation.Value!;
        var setup = new IntermediaryProgramCarrierLobSetup
        {
            IntermediaryId = intermediaryId,
            ProgramConfigurationId = request.ProgramConfigurationId,
            CarrierId = request.CarrierId,
            LineOfBusiness = request.LineOfBusiness,
            EffectiveDate = request.EffectiveDate,
            ExpirationDate = request.ExpirationDate,
            BrokerageRate = request.BrokerageRate,
            CreatePayable = request.CreatePayable,
            PayablePayeeId = request.CreatePayable ? request.PayablePayeeId : null,
            IsActive = request.IsActive,
            Notes = TrimToNull(request.Notes),
            Intermediary = intermediary,
            ProgramConfiguration = refs.Program,
            Carrier = refs.Carrier,
            PayablePayee = refs.Payee
        };

        _db.Set<IntermediaryProgramCarrierLobSetup>().Add(setup);
        await _db.SaveChangesAsync(ct);

        return Result<IntermediaryBrokerageSetupDto>.Success(Map(setup));
    }

    public async Task<Result<IntermediaryBrokerageSetupDto>> UpdateBrokerageSetupAsync(Guid intermediaryId, Guid setupId, UpsertIntermediaryBrokerageSetupRequest request, CancellationToken ct = default)
    {
        var setup = await _db.Set<IntermediaryProgramCarrierLobSetup>()
            .Include(s => s.ProgramConfiguration)
            .Include(s => s.Carrier)
            .Include(s => s.PayablePayee)
            .SingleOrDefaultAsync(s => s.Id == setupId && s.IntermediaryId == intermediaryId, ct);

        if (setup is null)
            return Result<IntermediaryBrokerageSetupDto>.Failure("BROKERAGE_SETUP_NOT_FOUND", "Brokerage setup row was not found.");

        var validation = await ValidateSetupRequestAsync(request, ct);
        if (!validation.IsSuccess)
            return Result<IntermediaryBrokerageSetupDto>.Failure(validation.ErrorCode!, validation.ErrorMessage!);

        var refs = validation.Value!;
        setup.ProgramConfigurationId = request.ProgramConfigurationId;
        setup.CarrierId = request.CarrierId;
        setup.LineOfBusiness = request.LineOfBusiness;
        setup.EffectiveDate = request.EffectiveDate;
        setup.ExpirationDate = request.ExpirationDate;
        setup.BrokerageRate = request.BrokerageRate;
        setup.CreatePayable = request.CreatePayable;
        setup.PayablePayeeId = request.CreatePayable ? request.PayablePayeeId : null;
        setup.IsActive = request.IsActive;
        setup.Notes = TrimToNull(request.Notes);
        setup.ProgramConfiguration = refs.Program;
        setup.Carrier = refs.Carrier;
        setup.PayablePayee = refs.Payee;

        await _db.SaveChangesAsync(ct);
        return Result<IntermediaryBrokerageSetupDto>.Success(Map(setup));
    }

    public async Task<Result> DeleteBrokerageSetupAsync(Guid intermediaryId, Guid setupId, CancellationToken ct = default)
    {
        var setup = await _db.Set<IntermediaryProgramCarrierLobSetup>()
            .SingleOrDefaultAsync(s => s.Id == setupId && s.IntermediaryId == intermediaryId, ct);

        if (setup is null)
            return Result.Failure("BROKERAGE_SETUP_NOT_FOUND", "Brokerage setup row was not found.");

        setup.IsDeleted = true;
        setup.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<Intermediary?> FindIntermediaryAsync(Guid id, CancellationToken ct) =>
        await _db.Set<Intermediary>()
            .Include(i => i.ProgramCarrierLobSetups)
                .ThenInclude(s => s.ProgramConfiguration)
            .Include(i => i.ProgramCarrierLobSetups)
                .ThenInclude(s => s.Carrier)
            .Include(i => i.ProgramCarrierLobSetups)
                .ThenInclude(s => s.PayablePayee)
            .SingleOrDefaultAsync(i => i.Id == id, ct);

    private async Task<Result<SetupReferences>> ValidateSetupRequestAsync(UpsertIntermediaryBrokerageSetupRequest request, CancellationToken ct)
    {
        if (request.ExpirationDate.HasValue && request.ExpirationDate.Value < request.EffectiveDate)
            return Result<SetupReferences>.Failure("INVALID_DATE_RANGE", "Expiration date cannot be before effective date.");

        if (request.BrokerageRate is < 0m or > 1m)
            return Result<SetupReferences>.Failure("BROKERAGE_RATE_INVALID", "Brokerage rate must be between 0 and 1.");

        if (request.CreatePayable && !request.PayablePayeeId.HasValue)
            return Result<SetupReferences>.Failure("PAYABLE_PAYEE_REQUIRED", "A payable payee is required when direct broker payable is enabled.");

        var program = await _db.Set<ProgramConfiguration>()
            .SingleOrDefaultAsync(p => p.Id == request.ProgramConfigurationId && p.IsActive, ct);
        if (program is null)
            return Result<SetupReferences>.Failure("PROGRAM_NOT_FOUND", "Program was not found or is inactive.");

        var carrier = await _db.Set<Carrier>()
            .SingleOrDefaultAsync(c => c.Id == request.CarrierId && c.IsActive, ct);
        if (carrier is null)
            return Result<SetupReferences>.Failure("CARRIER_NOT_FOUND", "Carrier was not found or is inactive.");

        var pathExists = await ProgramCarrierLobPathExistsAsync(
            request.ProgramConfigurationId,
            request.CarrierId,
            request.LineOfBusiness,
            request.EffectiveDate,
            ct);
        if (!pathExists)
            return Result<SetupReferences>.Failure("INVALID_PROGRAM_SETUP_PATH", "Selected carrier and line of business are not active for this program.");

        Payee? payee = null;
        if (request.CreatePayable && request.PayablePayeeId.HasValue)
        {
            payee = await _db.Set<Payee>()
                .SingleOrDefaultAsync(p => p.Id == request.PayablePayeeId.Value && p.IsActive, ct);
            if (payee is null)
                return Result<SetupReferences>.Failure("PAYABLE_PAYEE_NOT_FOUND", "The selected payable payee was not found or is inactive.");
        }

        return Result<SetupReferences>.Success(new SetupReferences(program, carrier, payee));
    }

    private async Task<bool> ProgramCarrierLobPathExistsAsync(
        Guid programConfigurationId,
        Guid carrierId,
        PolicyLineOfBusiness? lineOfBusiness,
        DateOnly effectiveDate,
        CancellationToken ct)
    {
        if (!lineOfBusiness.HasValue)
        {
            return await _db.Set<ProgramCarrier>()
                .AnyAsync(c =>
                    c.ProgramConfigurationId == programConfigurationId &&
                    c.CarrierId == carrierId &&
                    c.IsActive &&
                    c.EffectiveDate <= effectiveDate &&
                    (c.ExpirationDate == null || c.ExpirationDate >= effectiveDate), ct);
        }

        return await _db.Set<ProgramCarrierLineOfBusiness>()
            .AnyAsync(l =>
                l.LineOfBusiness == lineOfBusiness.Value &&
                l.IsActive &&
                l.EffectiveDate <= effectiveDate &&
                (l.ExpirationDate == null || l.ExpirationDate >= effectiveDate) &&
                l.ProgramCarrier.IsActive &&
                l.ProgramCarrier.CarrierId == carrierId &&
                l.ProgramCarrier.ProgramConfigurationId == programConfigurationId &&
                l.ProgramCarrier.EffectiveDate <= effectiveDate &&
                (l.ProgramCarrier.ExpirationDate == null || l.ProgramCarrier.ExpirationDate >= effectiveDate), ct);
    }

    private static IntermediaryDto Map(Intermediary intermediary) =>
        new(
            intermediary.Id,
            intermediary.Name,
            intermediary.ReferenceNumber,
            intermediary.Email,
            intermediary.Phone,
            intermediary.AddressLine1,
            intermediary.AddressLine2,
            intermediary.City,
            intermediary.State,
            intermediary.ZipCode,
            intermediary.Country,
            intermediary.BankName,
            intermediary.BankAccountName,
            intermediary.BankAccountLast4,
            intermediary.BankRoutingNumber,
            intermediary.BankSwiftCode,
            intermediary.BankInstructions,
            intermediary.IsActive,
            intermediary.Notes,
            intermediary.CreatedAt,
            intermediary.UpdatedAt,
            intermediary.ProgramCarrierLobSetups
                .OrderBy(s => s.ProgramConfiguration.Name)
                .ThenBy(s => s.Carrier.Name)
                .ThenBy(s => s.LineOfBusiness)
                .ThenByDescending(s => s.EffectiveDate)
                .Select(Map)
                .ToList());

    private static IntermediaryBrokerageSetupDto Map(IntermediaryProgramCarrierLobSetup setup) =>
        new(
            setup.Id,
            setup.IntermediaryId,
            setup.ProgramConfigurationId,
            setup.ProgramConfiguration?.Name ?? string.Empty,
            setup.CarrierId,
            setup.Carrier?.Name ?? string.Empty,
            setup.LineOfBusiness,
            GetLobLabel(setup.LineOfBusiness),
            setup.EffectiveDate,
            setup.ExpirationDate,
            setup.BrokerageRate,
            setup.CreatePayable,
            setup.PayablePayeeId,
            setup.PayablePayee?.Name,
            setup.IsActive,
            setup.Notes);

    private static (string Code, string Message)? ValidateName(string name) =>
        string.IsNullOrWhiteSpace(name)
            ? ("INTERMEDIARY_NAME_REQUIRED", "Intermediary name is required.")
            : null;

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeState(string? state) =>
        string.IsNullOrWhiteSpace(state) ? null : state.Trim().ToUpperInvariant();

    private static string? NormalizeCountry(string? country) =>
        string.IsNullOrWhiteSpace(country) ? null : country.Trim().ToUpperInvariant();

    private static string GetLobLabel(PolicyLineOfBusiness? lob) => lob switch
    {
        null => "All Lines",
        PolicyLineOfBusiness.GeneralLiability => "General Liability",
        PolicyLineOfBusiness.InlandMarine => "Inland Marine",
        PolicyLineOfBusiness.AutoLiability => "Auto Liability",
        PolicyLineOfBusiness.AutoPhysicalDamage => "Auto Physical Damage",
        _ => lob.Value.ToString()
    };

    private sealed record SetupReferences(
        ProgramConfiguration Program,
        Carrier Carrier,
        Payee? Payee);
}
