using Microsoft.EntityFrameworkCore;
using SIMS.Application.Common;
using SIMS.Application.DTOs;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;

namespace SIMS.Application.Services;

public class CarrierCommissionService : ICarrierCommissionService
{
    private readonly IServiceProvider _sp;
    private DbContext Db => (DbContext)_sp.GetService(typeof(DbContext))!;

    private static readonly Dictionary<string, string> LobLabels = new()
    {
        ["GeneralLiability"] = "General Liability",
        ["Property"] = "Property",
        ["CommercialAuto"] = "Commercial Auto",
        ["BusinessOwners"] = "Business Owners",
        ["WorkersCompensation"] = "Workers Compensation",
        ["ProfessionalLiability"] = "Professional Liability",
        ["Umbrella"] = "Umbrella",
        ["Cyber"] = "Cyber",
        ["ExcessLiability"] = "Excess Liability",
        ["Other"] = "Other",
    };

    public CarrierCommissionService(IServiceProvider sp) => _sp = sp;

    public async Task<IReadOnlyList<CarrierCommissionDto>> GetAllAsync(Guid carrierId, CancellationToken ct = default)
    {
        var rows = await Db.Set<CarrierCommission>()
            .Include(c => c.ProgramConfiguration)
            .Where(c => c.CarrierId == carrierId)
            .OrderBy(c => c.ProgramConfiguration == null ? string.Empty : c.ProgramConfiguration.Name)
            .ThenBy(c => c.LineOfBusiness)
            .ThenByDescending(c => c.EffectiveDate)
            .ToListAsync(ct);

        return rows.Select(ToDto).ToList();
    }

    public async Task<Result<CarrierCommissionDto>> CreateAsync(
        Guid carrierId, CreateCarrierCommissionRequest req, Guid userId, CancellationToken ct = default)
    {
        if (req.CommissionRate < 0 || req.CommissionRate > 1)
            return Result<CarrierCommissionDto>.Failure("INVALID_RATE", "Commission rate must be between 0 and 1 (e.g. 0.15 for 15%)");

        if (req.SMMRetentionRate < 0 || req.SMMRetentionRate > req.CommissionRate)
            return Result<CarrierCommissionDto>.Failure("INVALID_SMM_RATE", "SMM retention rate must be between 0 and the total commission rate");

        var db = Db;

        var scope = await ResolveProgramScopeAsync(req.ProgramConfigurationId, carrierId, req.LineOfBusiness, req.EffectiveDate, ct);
        if (!scope.IsSuccess)
            return Result<CarrierCommissionDto>.Failure(scope.ErrorCode!, scope.ErrorMessage!);

        var duplicate = await db.Set<CarrierCommission>()
            .AnyAsync(c => c.CarrierId == carrierId
                && c.ProgramConfigurationId == req.ProgramConfigurationId
                && c.LineOfBusiness == scope.Value!.LineOfBusiness
                && c.EffectiveDate == req.EffectiveDate, ct);

        if (duplicate)
            return Result<CarrierCommissionDto>.Failure("DUPLICATE", "A commission rate with this effective date already exists for this carrier/LOB");

        var entry = new CarrierCommission
        {
            CarrierId = carrierId,
            ProgramConfigurationId = req.ProgramConfigurationId,
            LineOfBusiness = scope.Value!.LineOfBusiness,
            ProgramCarrierId = scope.Value.ProgramCarrierId,
            ProgramCarrierLineOfBusinessId = scope.Value.ProgramCarrierLineOfBusinessId,
            CommissionRate = req.CommissionRate,
            SMMRetentionRate = req.SMMRetentionRate,
            EffectiveDate = req.EffectiveDate,
            CreatedBy = userId,
        };

        db.Set<CarrierCommission>().Add(entry);
        await db.SaveChangesAsync(ct);

        return Result<CarrierCommissionDto>.Success(ToDto(entry));
    }

    public async Task<Result<CarrierCommissionDto>> DisableAsync(
        long id, DateOnly? disabledDate, CancellationToken ct = default)
    {
        var db = Db;
        var entry = await db.Set<CarrierCommission>().FindAsync(new object[] { id }, ct);
        if (entry == null)
            return Result<CarrierCommissionDto>.Failure("NOT_FOUND", "Commission rate not found");

        if (entry.DisabledDate.HasValue)
            return Result<CarrierCommissionDto>.Failure("ALREADY_DISABLED", "This rate is already disabled");

        entry.DisabledDate = disabledDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        await db.SaveChangesAsync(ct);

        return Result<CarrierCommissionDto>.Success(ToDto(entry));
    }

    public async Task<CarrierCommissionRates?> GetActiveRatesAsync(
        Guid carrierId, string? lineOfBusiness, DateOnly asOfDate, Guid? programConfigurationId = null, CancellationToken ct = default)
    {
        var candidates = await Db.Set<CarrierCommission>()
            .Where(c => c.CarrierId == carrierId
                && (c.ProgramConfigurationId == programConfigurationId || c.ProgramConfigurationId == null)
                && (c.LineOfBusiness == lineOfBusiness || c.LineOfBusiness == null)
                && c.EffectiveDate <= asOfDate
                && (c.DisabledDate == null || c.DisabledDate > asOfDate))
            .ToListAsync(ct);

        var specific = candidates
            .OrderByDescending(c => c.ProgramConfigurationId == programConfigurationId ? 1 : 0)
            .ThenByDescending(c => c.LineOfBusiness == lineOfBusiness ? 1 : 0)
            .ThenByDescending(c => c.EffectiveDate)
            .FirstOrDefault();

        if (specific != null)
            return new CarrierCommissionRates(specific.CommissionRate, specific.SMMRetentionRate);
        return null;
    }

    private async Task<Result<ResolvedCarrierCommissionProgramScope>> ResolveProgramScopeAsync(
        Guid? programConfigurationId,
        Guid carrierId,
        string? lineOfBusiness,
        DateOnly effectiveDate,
        CancellationToken ct)
    {
        var normalizedLineOfBusiness = string.IsNullOrWhiteSpace(lineOfBusiness) ? null : lineOfBusiness.Trim();
        if (!programConfigurationId.HasValue)
            return Result<ResolvedCarrierCommissionProgramScope>.Success(new(null, null, normalizedLineOfBusiness));

        var programId = programConfigurationId.Value;
        var programExists = await Db.Set<ProgramConfiguration>()
            .AnyAsync(p => p.Id == programId && p.IsActive && !p.IsDeleted, ct);
        if (!programExists)
            return Result<ResolvedCarrierCommissionProgramScope>.Failure("PROGRAM_NOT_FOUND", "Program not found or inactive.");

        if (normalizedLineOfBusiness == null)
        {
            var programCarrierId = await Db.Set<ProgramCarrier>()
                .Where(c =>
                    c.ProgramConfigurationId == programId &&
                    c.CarrierId == carrierId &&
                    c.IsActive &&
                    !c.IsDeleted &&
                    c.EffectiveDate <= effectiveDate &&
                    (c.ExpirationDate == null || c.ExpirationDate >= effectiveDate))
                .Select(c => (Guid?)c.Id)
                .FirstOrDefaultAsync(ct);

            return programCarrierId.HasValue
                ? Result<ResolvedCarrierCommissionProgramScope>.Success(new(programCarrierId.Value, null, null))
                : Result<ResolvedCarrierCommissionProgramScope>.Failure("INVALID_PROGRAM_SETUP_PATH",
                    "Selected carrier is not active for this program.");
        }

        if (!Enum.TryParse<PolicyLineOfBusiness>(normalizedLineOfBusiness, out var lob))
            return Result<ResolvedCarrierCommissionProgramScope>.Failure("INVALID_PROGRAM_SETUP_PATH",
                "Selected carrier and line of business are not active for this program.");

        var programLobId = await Db.Set<ProgramCarrierLineOfBusiness>()
            .Where(l =>
                l.LineOfBusiness == lob &&
                l.IsActive &&
                !l.IsDeleted &&
                l.EffectiveDate <= effectiveDate &&
                (l.ExpirationDate == null || l.ExpirationDate >= effectiveDate) &&
                l.ProgramCarrier.IsActive &&
                !l.ProgramCarrier.IsDeleted &&
                l.ProgramCarrier.CarrierId == carrierId &&
                l.ProgramCarrier.ProgramConfigurationId == programId &&
                l.ProgramCarrier.EffectiveDate <= effectiveDate &&
                (l.ProgramCarrier.ExpirationDate == null || l.ProgramCarrier.ExpirationDate >= effectiveDate))
            .Select(l => (Guid?)l.Id)
            .FirstOrDefaultAsync(ct);

        return programLobId.HasValue
            ? Result<ResolvedCarrierCommissionProgramScope>.Success(new(null, programLobId.Value, normalizedLineOfBusiness))
            : Result<ResolvedCarrierCommissionProgramScope>.Failure("INVALID_PROGRAM_SETUP_PATH",
                "Selected carrier and line of business are not active for this program.");
    }

    private static CarrierCommissionDto ToDto(CarrierCommission c) => new(
        c.Id,
        c.ProgramConfigurationId,
        c.ProgramConfiguration?.Name,
        c.LineOfBusiness,
        c.LineOfBusiness != null && LobLabels.TryGetValue(c.LineOfBusiness, out var label) ? label : null,
        c.ProgramCarrierId,
        c.ProgramCarrierLineOfBusinessId,
        c.CommissionRate,
        c.SMMRetentionRate,
        c.EffectiveDate,
        c.DisabledDate,
        c.DisabledDate == null || c.DisabledDate > DateOnly.FromDateTime(DateTime.UtcNow),
        c.CreatedAt
    );
}

internal sealed record ResolvedCarrierCommissionProgramScope(
    Guid? ProgramCarrierId,
    Guid? ProgramCarrierLineOfBusinessId,
    string? LineOfBusiness);
