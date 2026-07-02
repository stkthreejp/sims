using Microsoft.EntityFrameworkCore;
using SIMS.Application.Common;
using SIMS.Application.DTOs.Rating;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Entities.Rating;
using SIMS.Domain.Enums;

namespace SIMS.Application.Services;

public class CarrierRatingAssignmentService : ICarrierRatingAssignmentService
{
    private static readonly PolicyLineOfBusiness[] ActiveLobs =
    [
        PolicyLineOfBusiness.GeneralLiability,
        PolicyLineOfBusiness.InlandMarine,
        PolicyLineOfBusiness.AutoLiability,
        PolicyLineOfBusiness.AutoPhysicalDamage,
    ];

    private static readonly Dictionary<PolicyLineOfBusiness, string> LobLabels = new()
    {
        [PolicyLineOfBusiness.GeneralLiability]   = "General Liability",
        [PolicyLineOfBusiness.InlandMarine]        = "Inland Marine",
        [PolicyLineOfBusiness.AutoLiability]       = "Auto Liability",
        [PolicyLineOfBusiness.AutoPhysicalDamage]  = "Auto Physical Damage",
    };

    private readonly IServiceProvider _sp;
    private DbContext Db => (DbContext)_sp.GetService(typeof(DbContext))!;

    public CarrierRatingAssignmentService(IServiceProvider sp) => _sp = sp;

    public async Task<IReadOnlyList<CarrierRatingAssignmentDto>> GetAllAsync(Guid? carrierId, CancellationToken ct = default)
    {
        var query = Db.Set<CarrierRatingAssignment>()
            .Where(a => !a.IsDeleted)
            .Include(a => a.ProgramConfiguration)
            .Include(a => a.Carrier)
            .Include(a => a.RatingPlanVersion)
                .ThenInclude(v => v.RatingPlan)
            .AsQueryable();

        if (carrierId.HasValue)
            query = query.Where(a => a.CarrierId == carrierId.Value);

        var rows = await query
            .OrderBy(a => a.Carrier.Name)
            .ThenBy(a => a.ProgramConfiguration == null ? string.Empty : a.ProgramConfiguration.Name)
            .ThenBy(a => a.LineOfBusiness)
            .ToListAsync(ct);

        return rows.Select(ToDto).ToList();
    }

    public async Task<Result<CarrierRatingAssignmentDto>> CreateAsync(
        CarrierRatingAssignmentCreateDto dto, CancellationToken ct = default)
    {
        if (!ActiveLobs.Contains(dto.LineOfBusiness))
            return Result<CarrierRatingAssignmentDto>.Failure("INVALID_LOB", $"{dto.LineOfBusiness} is not an active line of business.");

        var db = Db;

        var carrier = await db.Set<Domain.Entities.Carrier>().FindAsync(new object[] { dto.CarrierId }, ct);
        if (carrier == null || carrier.IsDeleted)
            return Result<CarrierRatingAssignmentDto>.Failure("CARRIER_NOT_FOUND", "Carrier not found.");

        var version = await db.Set<RatingPlanVersion>()
            .Include(v => v.RatingPlan)
            .FirstOrDefaultAsync(v => v.Id == dto.RatingPlanVersionId && !v.IsDeleted, ct);

        if (version == null)
            return Result<CarrierRatingAssignmentDto>.Failure("VERSION_NOT_FOUND", "Rating plan version not found.");

        if (version.RatingPlan.LineOfBusiness != dto.LineOfBusiness)
            return Result<CarrierRatingAssignmentDto>.Failure("LOB_MISMATCH",
                $"Rating plan version is for {version.RatingPlan.LineOfBusiness}, not {dto.LineOfBusiness}.");

        ProgramConfiguration? program = null;
        Guid? programCarrierLineOfBusinessId = null;
        if (dto.ProgramConfigurationId.HasValue)
        {
            program = await db.Set<ProgramConfiguration>()
                .FirstOrDefaultAsync(p => p.Id == dto.ProgramConfigurationId.Value && p.IsActive && !p.IsDeleted, ct);
            if (program == null)
                return Result<CarrierRatingAssignmentDto>.Failure("PROGRAM_NOT_FOUND", "Program not found or inactive.");

            programCarrierLineOfBusinessId = await ResolveProgramCarrierLobPathAsync(
                dto.ProgramConfigurationId.Value,
                dto.CarrierId,
                dto.LineOfBusiness,
                version.EffectiveDate,
                version.ExpirationDate,
                ct);
            if (!programCarrierLineOfBusinessId.HasValue)
                return Result<CarrierRatingAssignmentDto>.Failure("INVALID_PROGRAM_SETUP_PATH",
                    "Selected carrier and line of business are not active for this program.");
        }

        var exists = await db.Set<CarrierRatingAssignment>()
            .AnyAsync(a => a.ProgramConfigurationId == dto.ProgramConfigurationId
                && a.CarrierId == dto.CarrierId
                && a.LineOfBusiness == dto.LineOfBusiness
                && !a.IsDeleted, ct);

        if (exists)
            return Result<CarrierRatingAssignmentDto>.Failure("DUPLICATE",
                "This carrier already has a rating plan assigned for that program and line of business.");

        var assignment = new CarrierRatingAssignment
        {
            ProgramConfigurationId = dto.ProgramConfigurationId,
            CarrierId = dto.CarrierId,
            LineOfBusiness = dto.LineOfBusiness,
            ProgramCarrierLineOfBusinessId = programCarrierLineOfBusinessId,
            RatingPlanVersionId = dto.RatingPlanVersionId,
        };

        db.Set<CarrierRatingAssignment>().Add(assignment);
        await db.SaveChangesAsync(ct);

        assignment.ProgramConfiguration = program;
        assignment.Carrier = carrier;
        assignment.RatingPlanVersion = version;

        return Result<CarrierRatingAssignmentDto>.Success(ToDto(assignment));
    }

    public async Task<Result<CarrierRatingAssignmentDto>> UpdateAsync(
        Guid id, CarrierRatingAssignmentUpdateDto dto, CancellationToken ct = default)
    {
        var db = Db;

        var assignment = await db.Set<CarrierRatingAssignment>()
            .Include(a => a.Carrier)
            .Include(a => a.RatingPlanVersion).ThenInclude(v => v.RatingPlan)
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted, ct);

        if (assignment == null)
            return Result<CarrierRatingAssignmentDto>.Failure("NOT_FOUND", "Assignment not found.");

        var version = await db.Set<RatingPlanVersion>()
            .Include(v => v.RatingPlan)
            .FirstOrDefaultAsync(v => v.Id == dto.RatingPlanVersionId && !v.IsDeleted, ct);

        if (version == null)
            return Result<CarrierRatingAssignmentDto>.Failure("VERSION_NOT_FOUND", "Rating plan version not found.");

        if (version.RatingPlan.LineOfBusiness != assignment.LineOfBusiness)
            return Result<CarrierRatingAssignmentDto>.Failure("LOB_MISMATCH",
                $"Rating plan version is for {version.RatingPlan.LineOfBusiness}, not {assignment.LineOfBusiness}.");

        assignment.RatingPlanVersionId = dto.RatingPlanVersionId;
        assignment.RatingPlanVersion = version;
        assignment.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Result<CarrierRatingAssignmentDto>.Success(ToDto(assignment));
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var db = Db;

        var assignment = await db.Set<CarrierRatingAssignment>()
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted, ct);

        if (assignment == null)
            return Result<bool>.Failure("NOT_FOUND", "Assignment not found.");

        var hasBoundQuotes = await db.Set<QuoteRatingSnapshot>()
            .AnyAsync(s => s.RatingPlanVersionId == assignment.RatingPlanVersionId && s.IsBoundSnapshot, ct);

        if (hasBoundQuotes)
            return Result<bool>.Failure("HAS_BOUND_QUOTES",
                "Cannot remove this assignment — bound quotes reference the assigned rating plan version.");

        assignment.IsDeleted = true;
        assignment.DeletedAt = DateTime.UtcNow;
        assignment.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }

    public async Task<CarrierRatingAssignmentDto?> GetActiveAssignmentAsync(
        Guid carrierId,
        PolicyLineOfBusiness lineOfBusiness,
        Guid? programConfigurationId = null,
        CancellationToken ct = default)
    {
        var assignment = await Db.Set<CarrierRatingAssignment>()
            .Where(a => !a.IsDeleted
                && a.CarrierId == carrierId
                && a.LineOfBusiness == lineOfBusiness
                && (a.ProgramConfigurationId == programConfigurationId || a.ProgramConfigurationId == null))
            .Include(a => a.ProgramConfiguration)
            .Include(a => a.Carrier)
            .Include(a => a.RatingPlanVersion)
                .ThenInclude(v => v.RatingPlan)
            .OrderByDescending(a => a.ProgramConfigurationId == programConfigurationId ? 1 : 0)
            .FirstOrDefaultAsync(ct);

        return assignment == null ? null : ToDto(assignment);
    }

    public async Task<IReadOnlyList<RatingPlanVersionPickerDto>> GetActiveVersionsForLobAsync(
        PolicyLineOfBusiness lob, CancellationToken ct = default)
    {
        var rows = await Db.Set<RatingPlanVersion>()
            .Include(v => v.RatingPlan)
            .Where(v => !v.IsDeleted && v.Status == PlanStatus.Active && v.RatingPlan.LineOfBusiness == lob)
            .OrderBy(v => v.RatingPlan.Name)
            .ThenByDescending(v => v.VersionNumber)
            .ToListAsync(ct);

        return rows.Select(v => new RatingPlanVersionPickerDto
        {
            Id = v.Id,
            PlanName = v.RatingPlan.Name,
            VersionNumber = v.VersionNumber,
            EffectiveDate = v.EffectiveDate,
            Lob = v.RatingPlan.LineOfBusiness,
        }).ToList();
    }

    // The rating version's effective range and the program path's effective range
    // must OVERLAP — a version live since January is valid for a program line that
    // starts in August. (Point-in-time at the version's effective date wrongly
    // rejected any program set up after the rates' inception.)
    private async Task<Guid?> ResolveProgramCarrierLobPathAsync(
        Guid programConfigurationId,
        Guid carrierId,
        PolicyLineOfBusiness lineOfBusiness,
        DateOnly versionEffectiveDate,
        DateOnly? versionExpirationDate,
        CancellationToken ct)
    {
        return await Db.Set<ProgramCarrierLineOfBusiness>()
            .Where(l =>
                l.LineOfBusiness == lineOfBusiness &&
                l.IsActive &&
                !l.IsDeleted &&
                (versionExpirationDate == null || l.EffectiveDate <= versionExpirationDate) &&
                (l.ExpirationDate == null || l.ExpirationDate >= versionEffectiveDate) &&
                l.ProgramCarrier.IsActive &&
                !l.ProgramCarrier.IsDeleted &&
                l.ProgramCarrier.CarrierId == carrierId &&
                l.ProgramCarrier.ProgramConfigurationId == programConfigurationId &&
                (versionExpirationDate == null || l.ProgramCarrier.EffectiveDate <= versionExpirationDate) &&
                (l.ProgramCarrier.ExpirationDate == null || l.ProgramCarrier.ExpirationDate >= versionEffectiveDate))
            .Select(l => (Guid?)l.Id)
            .FirstOrDefaultAsync(ct);
    }

    private static CarrierRatingAssignmentDto ToDto(CarrierRatingAssignment a) => new()
    {
        Id = a.Id,
        ProgramConfigurationId = a.ProgramConfigurationId,
        ProgramName = a.ProgramConfiguration?.Name,
        CarrierId = a.CarrierId,
        CarrierName = a.Carrier.Name,
        LineOfBusiness = a.LineOfBusiness,
        LineOfBusinessLabel = LobLabels.GetValueOrDefault(a.LineOfBusiness, a.LineOfBusiness.ToString()),
        ProgramCarrierLineOfBusinessId = a.ProgramCarrierLineOfBusinessId,
        RatingPlanVersionId = a.RatingPlanVersionId,
        PlanName = a.RatingPlanVersion.RatingPlan.Name,
        VersionNumber = a.RatingPlanVersion.VersionNumber,
        EffectiveDate = a.RatingPlanVersion.EffectiveDate,
    };
}
