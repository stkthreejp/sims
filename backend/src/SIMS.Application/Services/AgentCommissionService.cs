using Microsoft.EntityFrameworkCore;
using SIMS.Application.Common;
using SIMS.Application.DTOs;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;

namespace SIMS.Application.Services;

public class AgentCommissionService : IAgentCommissionService
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

    public AgentCommissionService(IServiceProvider sp) => _sp = sp;

    public async Task<IReadOnlyList<AgentCommissionDto>> GetAllAsync(Guid agentId, CancellationToken ct = default)
    {
        var rows = await Db.Set<AgentCommission>()
            .Where(c => c.AgentId == agentId)
            .OrderBy(c => c.LineOfBusiness)
            .ThenByDescending(c => c.EffectiveDate)
            .ToListAsync(ct);

        return rows.Select(ToDto).ToList();
    }

    public async Task<Result<AgentCommissionDto>> CreateAsync(
        Guid agentId, CreateAgentCommissionRequest req, Guid userId, CancellationToken ct = default)
    {
        if (req.CommissionRate < 0 || req.CommissionRate > 1)
            return Result<AgentCommissionDto>.Failure("INVALID_RATE", "Commission rate must be between 0 and 1 (e.g. 0.15 for 15%)");

        var db = Db;

        var duplicate = await db.Set<AgentCommission>()
            .AnyAsync(c => c.AgentId == agentId
                && c.LineOfBusiness == req.LineOfBusiness
                && c.EffectiveDate == req.EffectiveDate, ct);

        if (duplicate)
            return Result<AgentCommissionDto>.Failure("DUPLICATE", "A commission rate with this effective date already exists for this agent/LOB");

        var entry = new AgentCommission
        {
            AgentId = agentId,
            LineOfBusiness = req.LineOfBusiness,
            CommissionRate = req.CommissionRate,
            EffectiveDate = req.EffectiveDate,
            CreatedBy = userId,
        };

        db.Set<AgentCommission>().Add(entry);
        await db.SaveChangesAsync(ct);

        return Result<AgentCommissionDto>.Success(ToDto(entry));
    }

    public async Task<Result<AgentCommissionDto>> DisableAsync(
        long id, DateOnly? disabledDate, CancellationToken ct = default)
    {
        var db = Db;
        var entry = await db.Set<AgentCommission>().FindAsync(new object[] { id }, ct);
        if (entry == null)
            return Result<AgentCommissionDto>.Failure("NOT_FOUND", "Commission rate not found");

        if (entry.DisabledDate.HasValue)
            return Result<AgentCommissionDto>.Failure("ALREADY_DISABLED", "This rate is already disabled");

        entry.DisabledDate = disabledDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        await db.SaveChangesAsync(ct);

        return Result<AgentCommissionDto>.Success(ToDto(entry));
    }

    public async Task<decimal?> GetActiveRateAsync(
        Guid agentId, string? lineOfBusiness, DateOnly asOfDate, CancellationToken ct = default)
    {
        var candidates = await Db.Set<AgentCommission>()
            .Where(c => c.AgentId == agentId
                && (c.LineOfBusiness == lineOfBusiness || c.LineOfBusiness == null)
                && c.EffectiveDate <= asOfDate
                && (c.DisabledDate == null || c.DisabledDate > asOfDate))
            .ToListAsync(ct);

        var specific = candidates
            .Where(c => c.LineOfBusiness == lineOfBusiness)
            .OrderByDescending(c => c.EffectiveDate)
            .FirstOrDefault();

        if (specific != null) return specific.CommissionRate;

        return candidates
            .Where(c => c.LineOfBusiness == null)
            .OrderByDescending(c => c.EffectiveDate)
            .FirstOrDefault()?.CommissionRate;
    }

    private static AgentCommissionDto ToDto(AgentCommission c) => new(
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
