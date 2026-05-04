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
[Authorize(Roles = "Admin")]
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
}
