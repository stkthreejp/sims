using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIMS.Application.DTOs.Rating;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities.Rating;
using SIMS.Domain.Enums;
using SIMS.Infrastructure.Data;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/rating-plan-versions")]
[Authorize(Roles = "Admin,Underwriter")]
public class RatingPlanVersionsController : ControllerBase
{
    private static readonly Dictionary<PolicyLineOfBusiness, string> LobLabels = new()
    {
        [PolicyLineOfBusiness.GeneralLiability]  = "General Liability",
        [PolicyLineOfBusiness.InlandMarine]       = "Inland Marine",
        [PolicyLineOfBusiness.AutoLiability]      = "Auto Liability",
        [PolicyLineOfBusiness.AutoPhysicalDamage] = "Auto Physical Damage",
    };

    private readonly ICarrierRatingAssignmentService _assignmentSvc;
    private readonly ApplicationDbContext _db;
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public RatingPlanVersionsController(ICarrierRatingAssignmentService assignmentSvc, ApplicationDbContext db)
    {
        _assignmentSvc = assignmentSvc;
        _db = db;
    }

    // ─── Picker (used by CarrierDetailPage assignment modal) ─────────────────

    [HttpGet]
    public async Task<IActionResult> GetForLob([FromQuery] PolicyLineOfBusiness lob, CancellationToken ct)
        => Ok(await _assignmentSvc.GetActiveVersionsForLobAsync(lob, ct));

    // ─── Version detail ───────────────────────────────────────────────────────

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var v = await _db.RatingPlanVersions
            .Where(v => v.Id == id && !v.IsDeleted)
            .Include(v => v.RatingPlan)
            .Include(v => v.PromotedBy)
            .FirstOrDefaultAsync(ct);

        if (v == null) return NotFound();

        return Ok(new RatingPlanVersionDetailDto
        {
            Id = v.Id,
            RatingPlanId = v.RatingPlanId,
            PlanName = v.RatingPlan.Name,
            Lob = v.RatingPlan.LineOfBusiness,
            LobLabel = LobLabels.GetValueOrDefault(v.RatingPlan.LineOfBusiness, v.RatingPlan.LineOfBusiness.ToString()),
            VersionNumber = v.VersionNumber,
            Status = v.Status,
            EffectiveDate = v.EffectiveDate,
            ExpirationDate = v.ExpirationDate,
            ScheduleMin = v.ScheduleMin,
            ScheduleMax = v.ScheduleMax,
            MinimumPremium = v.MinimumPremium,
            Notes = v.Notes,
            PromotedAt = v.PromotedAt,
            PromotedByName = v.PromotedBy?.FullName,
            PromotedById = v.PromotedById,
        });
    }

    // ─── Factor tables ────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/factors")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetFactors(Guid id, CancellationToken ct)
    {
        var exists = await _db.RatingPlanVersions.AnyAsync(v => v.Id == id && !v.IsDeleted, ct);
        if (!exists) return NotFound();

        var tables = await _db.FactorTables
            .Where(t => t.RatingPlanVersionId == id)
            .Include(t => t.Rows)
            .OrderBy(t => t.Code)
            .ToListAsync(ct);

        var result = tables.Select(t => new FactorTableDto
        {
            Id = t.Id,
            Code = t.Code,
            DimensionNames = t.DimensionNames,
            ValueSemantics = t.ValueSemantics,
            Rows = t.Rows
                .OrderBy(r => r.DimensionValues.Values.FirstOrDefault())
                .Select(r => new FactorRowDto
                {
                    Id = r.Id,
                    DimensionValues = r.DimensionValues,
                    Factor = r.Factor,
                })
                .ToList(),
        });

        return Ok(result);
    }

    // ─── Eligibility rules ────────────────────────────────────────────────────

    [HttpGet("{id:guid}/eligibility-rules")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetEligibilityRules(Guid id, CancellationToken ct)
    {
        var exists = await _db.RatingPlanVersions.AnyAsync(v => v.Id == id && !v.IsDeleted, ct);
        if (!exists) return NotFound();

        var rules = await _db.EligibilityRules
            .Where(r => r.RatingPlanVersionId == id)
            .Include(r => r.EquipmentType)
            .OrderBy(r => r.EquipmentType.TypeNumber)
            .Select(r => new EligibilityRuleDto
            {
                Id = r.Id,
                EquipmentTypeId = r.EquipmentTypeId,
                EquipmentTypeName = r.EquipmentType.Name,
                TypeNumber = r.EquipmentType.TypeNumber,
                Accepted = r.Accepted,
            })
            .ToListAsync(ct);

        return Ok(rules);
    }

    // ─── Promote ─────────────────────────────────────────────────────────────

    [HttpPost("{id:guid}/promote")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Promote(Guid id, CancellationToken ct)
    {
        var version = await _db.RatingPlanVersions
            .Include(v => v.RatingPlan)
            .Include(v => v.FactorTables).ThenInclude(t => t.Rows)
            .FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted, ct);

        if (version == null) return NotFound();

        if (version.Status != PlanStatus.Draft)
            return Conflict(new { ErrorCode = "NOT_DRAFT", ErrorMessage = "Only Draft versions can be promoted." });

        if (!version.FactorTables.Any(t => t.Rows.Any()))
            return Conflict(new { ErrorCode = "NO_FACTORS", ErrorMessage = "Version must have at least one factor table with rows before promoting." });

        if (version.EffectiveDate < DateOnly.FromDateTime(DateTime.UtcNow))
            return Conflict(new { ErrorCode = "PAST_EFFECTIVE_DATE", ErrorMessage = "Cannot promote a version with an effective date in the past." });

        // No time-travel: no other Active version of the same plan with a later effective date
        var laterActive = await _db.RatingPlanVersions.AnyAsync(
            v => v.RatingPlanId == version.RatingPlanId
                && v.Id != id
                && !v.IsDeleted
                && v.Status == PlanStatus.Active
                && v.EffectiveDate > version.EffectiveDate, ct);

        if (laterActive)
            return Conflict(new { ErrorCode = "TIME_TRAVEL", ErrorMessage = "Another active version exists with a later effective date. Retire it first." });

        // Expire the current active version (if any)
        var currentActive = await _db.RatingPlanVersions.FirstOrDefaultAsync(
            v => v.RatingPlanId == version.RatingPlanId
                && v.Id != id
                && !v.IsDeleted
                && v.Status == PlanStatus.Active, ct);

        if (currentActive != null)
        {
            currentActive.ExpirationDate = version.EffectiveDate.AddDays(-1);
            currentActive.UpdatedAt = DateTime.UtcNow;
        }

        version.Status = PlanStatus.Active;
        version.PromotedAt = DateTime.UtcNow;
        version.PromotedById = CurrentUserId;
        version.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Ok(new { versionId = version.Id, status = version.Status.ToString() });
    }

    // ─── Retire ───────────────────────────────────────────────────────────────

    [HttpPost("{id:guid}/retire")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Retire(Guid id, CancellationToken ct)
    {
        var version = await _db.RatingPlanVersions
            .FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted, ct);

        if (version == null) return NotFound();

        var hasBoundQuotes = await _db.QuoteRatingSnapshots
            .AnyAsync(s => s.RatingPlanVersionId == id && s.IsBoundSnapshot, ct);

        if (hasBoundQuotes)
            return Conflict(new { ErrorCode = "HAS_BOUND_QUOTES", ErrorMessage = "Cannot retire this version — bound quotes reference it." });

        version.Status = PlanStatus.Retired;
        version.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Ok(new { versionId = version.Id, status = version.Status.ToString() });
    }
}
