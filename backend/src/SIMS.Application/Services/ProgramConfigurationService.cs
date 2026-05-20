using Microsoft.EntityFrameworkCore;
using SIMS.Application.Common;
using SIMS.Application.DTOs.Underwriting;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;

namespace SIMS.Application.Services;

public class ProgramConfigurationService : IProgramConfigurationService
{
    private readonly DbContext _db;

    public ProgramConfigurationService(DbContext db) => _db = db;

    public async Task<IReadOnlyList<ProgramConfigurationDto>> GetAsync(bool includeInactive = false, CancellationToken ct = default)
    {
        var query = _db.Set<ProgramConfiguration>()
            .Include(p => p.Carrier)
            .AsQueryable();

        if (!includeInactive)
            query = query.Where(p => p.IsActive);

        var programs = await query
            .OrderBy(p => p.Name)
            .ThenBy(p => p.Carrier == null ? "" : p.Carrier.Name)
            .ThenBy(p => p.LineOfBusiness)
            .ThenBy(p => p.StateCode)
            .ToListAsync(ct);

        return programs.Select(Map).ToList();
    }

    public async Task<Result<ProgramConfigurationDto>> CreateAsync(CreateProgramConfigurationRequest request, CancellationToken ct = default)
    {
        var validation = await ValidateAsync(null, request.Name, request.Code, request.CarrierId, request.StateCode, ct);
        if (validation is not null)
            return Result<ProgramConfigurationDto>.Failure(validation.Value.Code, validation.Value.Message);

        var program = new ProgramConfiguration
        {
            Name = request.Name.Trim(),
            Code = NormalizeCode(request.Code),
            CarrierId = request.CarrierId,
            LineOfBusiness = request.LineOfBusiness,
            StateCode = NormalizeStateCode(request.StateCode),
            IsActive = request.IsActive,
            Notes = TrimToNull(request.Notes)
        };

        _db.Set<ProgramConfiguration>().Add(program);
        await _db.SaveChangesAsync(ct);

        program.Carrier = request.CarrierId.HasValue
            ? await _db.Set<Carrier>().FindAsync([request.CarrierId.Value], ct)
            : null;

        return Result<ProgramConfigurationDto>.Success(Map(program));
    }

    public async Task<Result<ProgramConfigurationDto>> UpdateAsync(Guid id, UpdateProgramConfigurationRequest request, CancellationToken ct = default)
    {
        var program = await _db.Set<ProgramConfiguration>()
            .Include(p => p.Carrier)
            .SingleOrDefaultAsync(p => p.Id == id, ct);

        if (program is null)
            return Result<ProgramConfigurationDto>.Failure("PROGRAM_NOT_FOUND", "Program was not found.");

        var validation = await ValidateAsync(id, request.Name, request.Code, request.CarrierId, request.StateCode, ct);
        if (validation is not null)
            return Result<ProgramConfigurationDto>.Failure(validation.Value.Code, validation.Value.Message);

        program.Name = request.Name.Trim();
        program.Code = NormalizeCode(request.Code);
        program.CarrierId = request.CarrierId;
        program.LineOfBusiness = request.LineOfBusiness;
        program.StateCode = NormalizeStateCode(request.StateCode);
        program.IsActive = request.IsActive;
        program.Notes = TrimToNull(request.Notes);

        await _db.SaveChangesAsync(ct);
        if (program.CarrierId.HasValue)
            program.Carrier = await _db.Set<Carrier>().FindAsync([program.CarrierId.Value], ct);
        else
            program.Carrier = null;

        return Result<ProgramConfigurationDto>.Success(Map(program));
    }

    private async Task<(string Code, string Message)?> ValidateAsync(Guid? existingId, string name, string code, Guid? carrierId, string stateCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ("PROGRAM_NAME_REQUIRED", "Program name is required.");
        if (string.IsNullOrWhiteSpace(code))
            return ("PROGRAM_CODE_REQUIRED", "Program code is required.");

        var normalizedState = NormalizeStateCode(stateCode);
        if (normalizedState != "ALL" && normalizedState.Length != 2)
            return ("STATE_INVALID", "State must be ALL or a two-letter state code.");

        if (carrierId.HasValue && !await _db.Set<Carrier>().AnyAsync(c => c.Id == carrierId.Value, ct))
            return ("CARRIER_NOT_FOUND", "Company was not found.");

        var normalizedCode = NormalizeCode(code);
        var duplicateCode = await _db.Set<ProgramConfiguration>()
            .AnyAsync(p => p.Code == normalizedCode && (!existingId.HasValue || p.Id != existingId.Value), ct);
        if (duplicateCode)
            return ("PROGRAM_CODE_DUPLICATE", "Program code is already in use.");

        return null;
    }

    internal static string NormalizeStateCode(string? stateCode)
    {
        if (string.IsNullOrWhiteSpace(stateCode))
            return "ALL";
        var trimmed = stateCode.Trim().ToUpperInvariant();
        return trimmed is "ALL" or "*" ? "ALL" : trimmed;
    }

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();
    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ProgramConfigurationDto Map(ProgramConfiguration program) =>
        new(
            program.Id,
            program.Name,
            program.Code,
            program.CarrierId,
            program.Carrier?.Name,
            program.LineOfBusiness,
            program.StateCode,
            program.IsActive,
            program.Notes,
            program.CreatedAt,
            program.UpdatedAt);
}
