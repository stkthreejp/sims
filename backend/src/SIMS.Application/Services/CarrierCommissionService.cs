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

        if (req.ProgramConfigurationId.HasValue)
        {
            var programExists = await db.Set<ProgramConfiguration>()
                .AnyAsync(p => p.Id == req.ProgramConfigurationId.Value && p.IsActive, ct);
            if (!programExists)
                return Result<CarrierCommissionDto>.Failure("PROGRAM_NOT_FOUND", "Program not found or inactive.");
        }

        var duplicate = await db.Set<CarrierCommission>()
            .AnyAsync(c => c.CarrierId == carrierId
                && c.ProgramConfigurationId == req.ProgramConfigurationId
                && c.LineOfBusiness == req.LineOfBusiness
                && c.EffectiveDate == req.EffectiveDate, ct);

        if (duplicate)
            return Result<CarrierCommissionDto>.Failure("DUPLICATE", "A commission rate with this effective date already exists for this carrier/LOB");

        var entry = new CarrierCommission
        {
            CarrierId = carrierId,
            ProgramConfigurationId = req.ProgramConfigurationId,
            LineOfBusiness = req.LineOfBusiness,
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

    private static CarrierCommissionDto ToDto(CarrierCommission c) => new(
        c.Id,
        c.ProgramConfigurationId,
        c.ProgramConfiguration?.Name,
        c.LineOfBusiness,
        c.LineOfBusiness != null && LobLabels.TryGetValue(c.LineOfBusiness, out var label) ? label : null,
        c.CommissionRate,
        c.SMMRetentionRate,
        c.EffectiveDate,
        c.DisabledDate,
        c.DisabledDate == null || c.DisabledDate > DateOnly.FromDateTime(DateTime.UtcNow),
        c.CreatedAt
    );
}
