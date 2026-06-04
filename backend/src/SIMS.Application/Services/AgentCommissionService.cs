using Microsoft.EntityFrameworkCore;
using SIMS.Application.Common;
using SIMS.Application.DTOs;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;

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
            .Include(c => c.ProgramConfiguration)
            .Include(c => c.Carrier)
            .Where(c => c.AgentId == agentId)
            .OrderBy(c => c.ProgramConfiguration == null ? string.Empty : c.ProgramConfiguration.Name)
            .ThenBy(c => c.Carrier == null ? string.Empty : c.Carrier.Name)
            .ThenBy(c => c.LineOfBusiness)
            .ThenBy(c => c.StateCode)
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
        var scope = await ResolveProgramScopeAsync(
            req.ProgramConfigurationId,
            req.CarrierId,
            req.LineOfBusiness,
            req.StateCode,
            req.EffectiveDate,
            ct);
        if (!scope.IsSuccess)
            return Result<AgentCommissionDto>.Failure(scope.ErrorCode!, scope.ErrorMessage!);

        if (scope.Value!.StateCode != null && (!scope.Value.CarrierId.HasValue || string.IsNullOrWhiteSpace(scope.Value.LineOfBusiness)))
            return Result<AgentCommissionDto>.Failure("INVALID_STATE_SCOPE", "State-specific agent commission rates require a carrier and line of business.");

        Carrier? carrier = null;
        if (scope.Value.CarrierId.HasValue)
        {
            carrier = await db.Set<Carrier>()
                .FirstOrDefaultAsync(c => c.Id == scope.Value.CarrierId.Value && c.IsActive && !c.IsDeleted, ct);
            if (carrier == null)
                return Result<AgentCommissionDto>.Failure("CARRIER_NOT_FOUND", "Carrier not found or inactive.");
        }

        var duplicate = await db.Set<AgentCommission>()
            .AnyAsync(c => c.AgentId == agentId
                && c.ProgramConfigurationId == req.ProgramConfigurationId
                && c.CarrierId == scope.Value.CarrierId
                && c.LineOfBusiness == scope.Value.LineOfBusiness
                && c.StateCode == scope.Value.StateCode
                && c.EffectiveDate == req.EffectiveDate, ct);

        if (duplicate)
            return Result<AgentCommissionDto>.Failure("DUPLICATE", "A commission rate with this effective date already exists for this agent/LOB");

        var entry = new AgentCommission
        {
            AgentId = agentId,
            ProgramConfigurationId = req.ProgramConfigurationId,
            CarrierId = scope.Value.CarrierId,
            LineOfBusiness = scope.Value.LineOfBusiness,
            StateCode = scope.Value.StateCode,
            ProgramCarrierId = scope.Value.ProgramCarrierId,
            ProgramCarrierLineOfBusinessId = scope.Value.ProgramCarrierLineOfBusinessId,
            ProgramCarrierLobStateId = scope.Value.ProgramCarrierLobStateId,
            CommissionRate = req.CommissionRate,
            EffectiveDate = req.EffectiveDate,
            CreatedBy = userId,
        };

        db.Set<AgentCommission>().Add(entry);
        await db.SaveChangesAsync(ct);

        entry.Carrier = carrier;
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
        Guid agentId,
        string? lineOfBusiness,
        DateOnly asOfDate,
        Guid? programConfigurationId = null,
        Guid? carrierId = null,
        string? stateCode = null,
        CancellationToken ct = default)
    {
        var normalizedStateCode = NormalizeStateCode(stateCode);
        var candidates = await Db.Set<AgentCommission>()
            .Where(c => c.AgentId == agentId
                && (c.ProgramConfigurationId == programConfigurationId || c.ProgramConfigurationId == null)
                && (c.CarrierId == carrierId || c.CarrierId == null)
                && (c.LineOfBusiness == lineOfBusiness || c.LineOfBusiness == null)
                && (c.StateCode == normalizedStateCode || c.StateCode == null)
                && c.EffectiveDate <= asOfDate
                && (c.DisabledDate == null || c.DisabledDate > asOfDate))
            .ToListAsync(ct);

        return candidates
            .OrderByDescending(c => c.ProgramConfigurationId == programConfigurationId ? 1 : 0)
            .ThenByDescending(c => c.CarrierId == carrierId ? 1 : 0)
            .ThenByDescending(c => c.LineOfBusiness == lineOfBusiness ? 1 : 0)
            .ThenByDescending(c => c.StateCode == normalizedStateCode ? 1 : 0)
            .ThenByDescending(c => c.EffectiveDate)
            .FirstOrDefault()?.CommissionRate;
    }

    private async Task<Result<ResolvedAgentCommissionProgramScope>> ResolveProgramScopeAsync(
        Guid? programConfigurationId,
        Guid? carrierId,
        string? lineOfBusiness,
        string? stateCode,
        DateOnly effectiveDate,
        CancellationToken ct)
    {
        var normalizedLineOfBusiness = string.IsNullOrWhiteSpace(lineOfBusiness) ? null : lineOfBusiness.Trim();
        var normalizedStateCode = NormalizeStateCode(stateCode);
        if (!programConfigurationId.HasValue)
        {
            return Result<ResolvedAgentCommissionProgramScope>.Success(new(
                carrierId,
                normalizedLineOfBusiness,
                normalizedStateCode,
                null,
                null,
                null));
        }

        var programId = programConfigurationId.Value;
        var programExists = await Db.Set<ProgramConfiguration>()
            .AnyAsync(p => p.Id == programId && p.IsActive && !p.IsDeleted, ct);
        if (!programExists)
            return Result<ResolvedAgentCommissionProgramScope>.Failure("PROGRAM_NOT_FOUND", "Program not found or inactive.");

        if (normalizedStateCode != null && (!carrierId.HasValue || normalizedLineOfBusiness == null))
            return Result<ResolvedAgentCommissionProgramScope>.Failure("INVALID_STATE_SCOPE", "State-specific agent commission rates require a carrier and line of business.");

        if (normalizedLineOfBusiness == null)
        {
            if (!carrierId.HasValue)
            {
                return Result<ResolvedAgentCommissionProgramScope>.Success(new(
                    null,
                    null,
                    null,
                    null,
                    null,
                    null));
            }

            var programCarrierId = await Db.Set<ProgramCarrier>()
                .Where(c =>
                    c.ProgramConfigurationId == programId &&
                    c.CarrierId == carrierId.Value &&
                    c.IsActive &&
                    !c.IsDeleted &&
                    c.EffectiveDate <= effectiveDate &&
                    (c.ExpirationDate == null || c.ExpirationDate >= effectiveDate))
                .Select(c => (Guid?)c.Id)
                .FirstOrDefaultAsync(ct);

            return programCarrierId.HasValue
                ? Result<ResolvedAgentCommissionProgramScope>.Success(new(carrierId, null, null, programCarrierId.Value, null, null))
                : Result<ResolvedAgentCommissionProgramScope>.Failure("INVALID_PROGRAM_SETUP_PATH",
                    "Selected carrier is not active for this program.");
        }

        if (!carrierId.HasValue)
            return Result<ResolvedAgentCommissionProgramScope>.Failure("INVALID_PROGRAM_SETUP_PATH",
                "Program-scoped agent commission rates require a carrier when line of business is selected.");

        if (!Enum.TryParse<PolicyLineOfBusiness>(normalizedLineOfBusiness, out var lob))
            return Result<ResolvedAgentCommissionProgramScope>.Failure("INVALID_PROGRAM_SETUP_PATH",
                "Selected carrier, line of business, and state are not active for this program.");

        var query = Db.Set<ProgramCarrierLineOfBusiness>()
            .Where(l =>
                l.LineOfBusiness == lob &&
                l.IsActive &&
                !l.IsDeleted &&
                l.EffectiveDate <= effectiveDate &&
                (l.ExpirationDate == null || l.ExpirationDate >= effectiveDate) &&
                l.ProgramCarrier.IsActive &&
                !l.ProgramCarrier.IsDeleted &&
                l.ProgramCarrier.ProgramConfigurationId == programConfigurationId &&
                l.ProgramCarrier.CarrierId == carrierId.Value &&
                l.ProgramCarrier.EffectiveDate <= effectiveDate &&
                (l.ProgramCarrier.ExpirationDate == null || l.ProgramCarrier.ExpirationDate >= effectiveDate));

        if (normalizedStateCode != null)
        {
            var programStateId = await query
                .SelectMany(l => l.States)
                .Where(s =>
                s.StateCode == normalizedStateCode &&
                s.IsActive &&
                !s.IsDeleted &&
                s.EffectiveDate <= effectiveDate &&
                (s.ExpirationDate == null || s.ExpirationDate >= effectiveDate))
                .Select(s => (Guid?)s.Id)
                .FirstOrDefaultAsync(ct);

            return programStateId.HasValue
                ? Result<ResolvedAgentCommissionProgramScope>.Success(new(carrierId, normalizedLineOfBusiness, normalizedStateCode, null, null, programStateId.Value))
                : Result<ResolvedAgentCommissionProgramScope>.Failure("INVALID_PROGRAM_SETUP_PATH",
                    "Selected carrier, line of business, and state are not active for this program.");
        }

        var programLobId = await query
            .Select(l => (Guid?)l.Id)
            .FirstOrDefaultAsync(ct);

        return programLobId.HasValue
            ? Result<ResolvedAgentCommissionProgramScope>.Success(new(carrierId, normalizedLineOfBusiness, null, null, programLobId.Value, null))
            : Result<ResolvedAgentCommissionProgramScope>.Failure("INVALID_PROGRAM_SETUP_PATH",
                "Selected carrier and line of business are not active for this program.");
    }

    private static string? NormalizeStateCode(string? stateCode)
    {
        var trimmed = stateCode?.Trim().ToUpperInvariant();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static AgentCommissionDto ToDto(AgentCommission c) => new(
        c.Id,
        c.ProgramConfigurationId,
        c.ProgramConfiguration?.Name,
        c.CarrierId,
        c.Carrier?.Name,
        c.LineOfBusiness,
        c.LineOfBusiness != null && LobLabels.TryGetValue(c.LineOfBusiness, out var label) ? label : null,
        c.StateCode,
        c.ProgramCarrierId,
        c.ProgramCarrierLineOfBusinessId,
        c.ProgramCarrierLobStateId,
        c.CommissionRate,
        c.EffectiveDate,
        c.DisabledDate,
        c.DisabledDate == null || c.DisabledDate > DateOnly.FromDateTime(DateTime.UtcNow),
        c.CreatedAt
    );
}

internal sealed record ResolvedAgentCommissionProgramScope(
    Guid? CarrierId,
    string? LineOfBusiness,
    string? StateCode,
    Guid? ProgramCarrierId,
    Guid? ProgramCarrierLineOfBusinessId,
    Guid? ProgramCarrierLobStateId);
