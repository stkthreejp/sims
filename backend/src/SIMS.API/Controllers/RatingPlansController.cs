using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIMS.Application.DTOs.Rating;
using SIMS.Domain.Entities.Rating;
using SIMS.Domain.Enums;
using SIMS.Infrastructure.Data;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/rating-plans")]
[Authorize(Policy = AppPermissions.RatingAdmin)]
public class RatingPlansController : ControllerBase
{
    private static readonly Dictionary<PolicyLineOfBusiness, string> LobLabels = new()
    {
        [PolicyLineOfBusiness.GeneralLiability]  = "General Liability",
        [PolicyLineOfBusiness.InlandMarine]       = "Inland Marine",
        [PolicyLineOfBusiness.AutoLiability]      = "Auto Liability",
        [PolicyLineOfBusiness.AutoPhysicalDamage] = "Auto Physical Damage",
    };

    private readonly ApplicationDbContext _db;
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public RatingPlansController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var plans = await _db.RatingPlans
            .Where(p => !p.IsDeleted)
            .Select(p => new
            {
                p.Id,
                p.LineOfBusiness,
                p.Name,
                p.FormulaKey,
                p.Status,
                ActiveVersionId = p.Versions
                    .Where(v => !v.IsDeleted && v.Status == PlanStatus.Active)
                    .OrderByDescending(v => v.VersionNumber)
                    .Select(v => (Guid?)v.Id)
                    .FirstOrDefault(),
                ActiveVersionNumber = p.Versions
                    .Where(v => !v.IsDeleted && v.Status == PlanStatus.Active)
                    .OrderByDescending(v => v.VersionNumber)
                    .Select(v => (int?)v.VersionNumber)
                    .FirstOrDefault(),
                ActiveEffectiveDate = p.Versions
                    .Where(v => !v.IsDeleted && v.Status == PlanStatus.Active)
                    .OrderByDescending(v => v.VersionNumber)
                    .Select(v => (DateOnly?)v.EffectiveDate)
                    .FirstOrDefault(),
                VersionCount = p.Versions.Count(v => !v.IsDeleted),
            })
            .OrderBy(p => p.LineOfBusiness)
            .ToListAsync(ct);

        // Assignment counts require joining through versions; fetch separately
        var versionIdsByPlan = await _db.RatingPlanVersions
            .Where(v => !v.IsDeleted)
            .Select(v => new { v.Id, v.RatingPlanId })
            .ToListAsync(ct);

        var assignmentCounts = await _db.CarrierRatingAssignments
            .Where(a => !a.IsDeleted)
            .GroupBy(a => a.RatingPlanVersionId)
            .Select(g => new { VersionId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var planIdToAssignmentCount = versionIdsByPlan
            .Join(assignmentCounts, v => v.Id, a => a.VersionId, (v, a) => new { v.RatingPlanId, a.Count })
            .GroupBy(x => x.RatingPlanId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Count));

        var result = plans.Select(p => new RatingPlanListItemDto
        {
            Id = p.Id,
            Lob = p.LineOfBusiness,
            LobLabel = LobLabels.GetValueOrDefault(p.LineOfBusiness, p.LineOfBusiness.ToString()),
            Name = p.Name,
            FormulaKey = p.FormulaKey,
            Status = p.Status,
            ActiveVersionId = p.ActiveVersionId,
            ActiveVersionNumber = p.ActiveVersionNumber,
            ActiveEffectiveDate = p.ActiveEffectiveDate,
            VersionCount = p.VersionCount,
            AssignedCarrierCount = planIdToAssignmentCount.GetValueOrDefault(p.Id, 0),
        });

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var plan = await _db.RatingPlans
            .Where(p => p.Id == id && !p.IsDeleted)
            .Select(p => new
            {
                p.Id, p.LineOfBusiness, p.Name, p.FormulaKey, p.Status,
                Versions = p.Versions
                    .Where(v => !v.IsDeleted)
                    .OrderByDescending(v => v.VersionNumber)
                    .Select(v => new
                    {
                        v.Id, v.VersionNumber, v.Status,
                        v.EffectiveDate, v.ExpirationDate, v.Notes,
                        v.PromotedAt, v.PromotedById,
                        v.CreatedById, v.LastEditedById,
                        PromotedByName = v.PromotedBy == null ? null : v.PromotedBy.FirstName + " " + v.PromotedBy.LastName,
                        AssignedCarrierCount = _db.CarrierRatingAssignments
                            .Count(a => a.RatingPlanVersionId == v.Id && !a.IsDeleted),
                    })
                    .ToList(),
            })
            .FirstOrDefaultAsync(ct);

        if (plan == null) return NotFound();

        var assignments = await _db.CarrierRatingAssignments
            .Where(a => !a.IsDeleted && a.RatingPlanVersion.RatingPlanId == id)
            .Select(a => new PlanCarrierAssignmentDto
            {
                AssignmentId = a.Id,
                CarrierId = a.CarrierId,
                CarrierName = a.Carrier.Name,
                VersionId = a.RatingPlanVersionId,
                VersionNumber = a.RatingPlanVersion.VersionNumber,
            })
            .OrderBy(a => a.CarrierName)
            .ToListAsync(ct);

        var dto = new RatingPlanDetailDto
        {
            Id = plan.Id,
            Lob = plan.LineOfBusiness,
            LobLabel = LobLabels.GetValueOrDefault(plan.LineOfBusiness, plan.LineOfBusiness.ToString()),
            Name = plan.Name,
            FormulaKey = plan.FormulaKey,
            Status = plan.Status,
            Versions = plan.Versions.Select(v => new RatingPlanVersionSummaryDto
            {
                Id = v.Id,
                VersionNumber = v.VersionNumber,
                Status = v.Status,
                EffectiveDate = v.EffectiveDate,
                ExpirationDate = v.ExpirationDate,
                Notes = v.Notes,
                PromotedAt = v.PromotedAt,
                PromotedByName = v.PromotedByName,
                AssignedCarrierCount = v.AssignedCarrierCount,
                CreatedById = v.CreatedById,
                LastEditedById = v.LastEditedById,
            }).ToList(),
            Assignments = assignments,
        };

        return Ok(dto);
    }

    [HttpPost("{planId:guid}/versions")]
    public async Task<IActionResult> CreateVersion(Guid planId, [FromBody] CreateRatingPlanVersionDto dto, CancellationToken ct)
    {
        var plan = await _db.RatingPlans.FirstOrDefaultAsync(p => p.Id == planId && !p.IsDeleted, ct);
        if (plan == null) return NotFound();

        var hasDraft = await _db.RatingPlanVersions.AnyAsync(
            v => v.RatingPlanId == planId && !v.IsDeleted && v.Status == PlanStatus.Draft, ct);
        if (hasDraft)
            return Conflict(new { ErrorCode = "DRAFT_EXISTS", ErrorMessage = "This plan already has a Draft version. Complete or retire it first." });

        var nextNumber = await _db.RatingPlanVersions
            .Where(v => v.RatingPlanId == planId && !v.IsDeleted)
            .MaxAsync(v => (int?)v.VersionNumber, ct) ?? 0;
        nextNumber++;

        var version = new RatingPlanVersion
        {
            RatingPlanId = planId,
            VersionNumber = nextNumber,
            EffectiveDate = dto.EffectiveDate,
            Status = PlanStatus.Draft,
            Notes = dto.Notes,
            CreatedById = CurrentUserId,
        };

        if (dto.CloneFromVersionId.HasValue)
        {
            var source = await _db.RatingPlanVersions
                .Where(v => v.Id == dto.CloneFromVersionId && !v.IsDeleted && v.RatingPlanId == planId)
                .Include(v => v.FactorTables).ThenInclude(t => t.Rows)
                .Include(v => v.EligibilityRules)
                .FirstOrDefaultAsync(ct);

            if (source == null)
                return BadRequest(new { ErrorCode = "INVALID_CLONE_SOURCE", ErrorMessage = "Clone source version not found." });

            version.ScheduleMin = source.ScheduleMin;
            version.ScheduleMax = source.ScheduleMax;
            version.MinimumPremium = source.MinimumPremium;

            foreach (var srcTable in source.FactorTables)
            {
                var newTable = new FactorTable
                {
                    Code = srcTable.Code,
                    DimensionNames = srcTable.DimensionNames.ToArray(),
                    ValueSemantics = srcTable.ValueSemantics,
                };
                foreach (var r in srcTable.Rows)
                {
                    newTable.Rows.Add(new FactorRow
                    {
                        DimensionValues = new Dictionary<string, string>(r.DimensionValues),
                        Factor = r.Factor,
                    });
                }
                version.FactorTables.Add(newTable);
            }

            foreach (var rule in source.EligibilityRules)
            {
                version.EligibilityRules.Add(new EligibilityRule
                {
                    EquipmentTypeId = rule.EquipmentTypeId,
                    Accepted = rule.Accepted,
                });
            }
        }

        _db.RatingPlanVersions.Add(version);
        await _db.SaveChangesAsync(ct);

        return Ok(new { versionId = version.Id, versionNumber = version.VersionNumber });
    }
}
