using Microsoft.EntityFrameworkCore;
using SIMS.Application.Common;
using SIMS.Application.DTOs;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;

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
            .Where(c => c.CarrierId == carrierId)
            .OrderBy(c => c.LineOfBusiness)
            .ThenByDescending(c => c.EffectiveDate)
            .ToListAsync(ct);

        return rows.Select(ToDto).ToList();
    }

    public async Task<Result<CarrierCommissionDto>> CreateAsync(
        Guid carrierId, CreateCarrierCommissionRequest req, Guid userId, CancellationToken ct = default)
    {
        if (req.CommissionRate < 0 || req.CommissionRate > 1)
            return Result<CarrierCommissionDto>.Failure("INVALID_RATE", "Commission rate must be between 0 and 1 (e.g. 0.15 for 15%)");

        var db = Db;

        var duplicate = await db.Set<CarrierCommission>()
            .AnyAsync(c => c.CarrierId == carrierId
                && c.LineOfBusiness == req.LineOfBusiness
                && c.EffectiveDate == req.EffectiveDate, ct);

        if (duplicate)
            return Result<CarrierCommissionDto>.Failure("DUPLICATE", "A commission rate with this effective date already exists for this carrier/LOB");

        var entry = new CarrierCommission
        {
            CarrierId = carrierId,
            LineOfBusiness = req.LineOfBusiness,
            CommissionRate = req.CommissionRate,
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

    public async Task<decimal?> GetActiveRateAsync(
        Guid carrierId, string? lineOfBusiness, DateOnly asOfDate, CancellationToken ct = default)
    {
        var candidates = await Db.Set<CarrierCommission>()
            .Where(c => c.CarrierId == carrierId
                && (c.LineOfBusiness == lineOfBusiness || c.LineOfBusiness == null)
                && c.EffectiveDate <= asOfDate
                && (c.DisabledDate == null || c.DisabledDate > asOfDate))
            .ToListAsync(ct);

        // Prefer exact LOB match over null (all-LOB fallback)
        var specific = candidates
            .Where(c => c.LineOfBusiness == lineOfBusiness)
            .OrderByDescending(c => c.EffectiveDate)
            .FirstOrDefault();

        if (specific != null) return specific.CommissionRate;

        var fallback = candidates
            .Where(c => c.LineOfBusiness == null)
            .OrderByDescending(c => c.EffectiveDate)
            .FirstOrDefault();

        return fallback?.CommissionRate;
    }

    private static CarrierCommissionDto ToDto(CarrierCommission c) => new(
        c.Id,
        c.LineOfBusiness,
        c.LineOfBusiness != null && LobLabels.TryGetValue(c.LineOfBusiness, out var label) ? label : null,
        c.CommissionRate,
        c.EffectiveDate,
        c.DisabledDate,
        c.DisabledDate == null || c.DisabledDate > DateOnly.FromDateTime(DateTime.UtcNow),
        c.CreatedAt
    );
}
