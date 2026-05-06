using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIMS.Application.DTOs.Rating;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Rating;
using SIMS.Domain.Entities;
using SIMS.Domain.Entities.Rating;
using SIMS.Domain.Enums;
using SIMS.Infrastructure.Data;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/rating-plan-versions")]
[Authorize(Policy = AppPermissions.RatingManage)]
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
    [Authorize(Policy = AppPermissions.RatingAdmin)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var v = await _db.RatingPlanVersions
            .Where(v => v.Id == id && !v.IsDeleted)
            .Include(v => v.RatingPlan)
            .Include(v => v.PromotedBy)
            .FirstOrDefaultAsync(ct);

        if (v == null) return NotFound();

        var impactPreviewAt = await _db.RatingPlanVersionImpactPreviews
            .Where(p => p.RatingPlanVersionId == id && !p.IsDeleted)
            .OrderByDescending(p => p.ComputedAt)
            .Select(p => (DateTime?)p.ComputedAt)
            .FirstOrDefaultAsync(ct);

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
            CreatedById = v.CreatedById,
            LastEditedById = v.LastEditedById,
            ImpactPreviewComputedAt = impactPreviewAt,
        });
    }

    // ─── Factor tables ────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/factors")]
    [Authorize(Policy = AppPermissions.RatingAdmin)]
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
    [Authorize(Policy = AppPermissions.RatingAdmin)]
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
    [Authorize(Policy = AppPermissions.RatingAdmin)]
    public async Task<IActionResult> Promote(Guid id, CancellationToken ct)
    {
        var version = await _db.RatingPlanVersions
            .Include(v => v.RatingPlan)
            .Include(v => v.FactorTables).ThenInclude(t => t.Rows)
            .FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted, ct);

        if (version == null) return NotFound();

        if (version.Status != PlanStatus.Draft)
            return Conflict(new { ErrorCode = "NOT_DRAFT", ErrorMessage = "Only Draft versions can be promoted." });

        var currentUserId = CurrentUserId;
        if ((version.CreatedById.HasValue && version.CreatedById == currentUserId) ||
            (version.LastEditedById.HasValue && version.LastEditedById == currentUserId))
            return StatusCode(403, new { ErrorCode = "MAKER_CHECKER", ErrorMessage = "You edited this draft — a different admin must promote it." });

        if (!version.FactorTables.Any(t => t.Rows.Any()))
            return Conflict(new { ErrorCode = "NO_FACTORS", ErrorMessage = "Version must have at least one factor table with rows before promoting." });

        var hasPreview = await _db.RatingPlanVersionImpactPreviews
            .AnyAsync(p => p.RatingPlanVersionId == id && !p.IsDeleted, ct);
        if (!hasPreview)
            return Conflict(new { ErrorCode = "NO_IMPACT_PREVIEW", ErrorMessage = "Run an impact preview before promoting this version." });

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
        version.PromotedById = currentUserId;
        version.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Ok(new { versionId = version.Id, status = version.Status.ToString() });
    }

    // ─── Retire ───────────────────────────────────────────────────────────────

    [HttpPost("{id:guid}/retire")]
    [Authorize(Policy = AppPermissions.RatingAdmin)]
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

    // ─── Update draft metadata ────────────────────────────────────────────────

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AppPermissions.RatingAdmin)]
    public async Task<IActionResult> UpdateMeta(Guid id, [FromBody] UpdateVersionMetaDto dto, CancellationToken ct)
    {
        var version = await _db.RatingPlanVersions.FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted, ct);
        if (version == null) return NotFound();
        if (version.Status != PlanStatus.Draft)
            return Conflict(new { ErrorCode = "NOT_DRAFT", ErrorMessage = "Only Draft versions can be edited." });

        version.EffectiveDate = dto.EffectiveDate;
        version.Notes = dto.Notes;
        version.ScheduleMin = dto.ScheduleMin;
        version.ScheduleMax = dto.ScheduleMax;
        version.MinimumPremium = dto.MinimumPremium;
        version.LastEditedById = CurrentUserId;
        version.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Ok(new { versionId = version.Id });
    }

    // ─── Bulk-replace factor table rows ──────────────────────────────────────

    [HttpPut("{id:guid}/factors/{tableCode}")]
    [Authorize(Policy = AppPermissions.RatingAdmin)]
    public async Task<IActionResult> UpdateFactorTable(Guid id, string tableCode, [FromBody] UpdateFactorTableDto dto, CancellationToken ct)
    {
        var version = await _db.RatingPlanVersions.FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted, ct);
        if (version == null) return NotFound();
        if (version.Status != PlanStatus.Draft)
            return Conflict(new { ErrorCode = "NOT_DRAFT", ErrorMessage = "Only Draft versions can be edited." });

        var table = await _db.FactorTables
            .Include(t => t.Rows)
            .FirstOrDefaultAsync(t => t.RatingPlanVersionId == id && t.Code == tableCode, ct);
        if (table == null) return NotFound(new { ErrorCode = "TABLE_NOT_FOUND", ErrorMessage = $"Factor table '{tableCode}' not found." });

        _db.FactorRows.RemoveRange(table.Rows);

        foreach (var r in dto.Rows)
        {
            table.Rows.Add(new FactorRow
            {
                DimensionValues = r.DimensionValues,
                Factor = r.Factor,
            });
        }

        version.LastEditedById = CurrentUserId;
        version.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Ok(new { tableCode, rowCount = dto.Rows.Count });
    }

    // ─── CSV import ───────────────────────────────────────────────────────────

    [HttpPost("{id:guid}/import-csv")]
    [Authorize(Policy = AppPermissions.RatingAdmin)]
    public async Task<IActionResult> ImportCsv(Guid id, [FromForm] IFormFile file, CancellationToken ct)
    {
        var version = await _db.RatingPlanVersions
            .Include(v => v.FactorTables).ThenInclude(t => t.Rows)
            .FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted, ct);
        if (version == null) return NotFound();
        if (version.Status != PlanStatus.Draft)
            return Conflict(new { ErrorCode = "NOT_DRAFT", ErrorMessage = "Only Draft versions can be edited." });

        using var reader = new System.IO.StreamReader(file.OpenReadStream());
        var lines = new List<string>();
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line != null) lines.Add(line);
        }

        if (lines.Count < 2)
            return BadRequest(new { ErrorCode = "EMPTY_CSV", ErrorMessage = "CSV must have a header row and at least one data row." });

        var headers = lines[0].Split(',').Select(h => h.Trim()).ToArray();
        var tableCodeIdx = Array.IndexOf(headers, "table_code");
        var factorIdx = Array.IndexOf(headers, "factor");

        if (tableCodeIdx < 0 || factorIdx < 0)
            return BadRequest(new { ErrorCode = "INVALID_CSV", ErrorMessage = "CSV must have 'table_code' and 'factor' columns." });

        var grouped = new Dictionary<string, List<string[]>>(StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();

        for (int i = 1; i < lines.Count; i++)
        {
            var parts = lines[i].Split(',').Select(p => p.Trim()).ToArray();
            if (parts.Length < headers.Length) { warnings.Add($"Row {i + 1}: too few columns, skipped."); continue; }
            var tableCode = parts[tableCodeIdx];
            if (string.IsNullOrWhiteSpace(tableCode)) continue;
            if (!grouped.ContainsKey(tableCode)) grouped[tableCode] = new List<string[]>();
            grouped[tableCode].Add(parts);
        }

        var result = new CsvImportResultDto { Warnings = warnings };

        foreach (var (tableCode, rows) in grouped)
        {
            var table = version.FactorTables.FirstOrDefault(t =>
                string.Equals(t.Code, tableCode, StringComparison.OrdinalIgnoreCase));
            if (table == null) { warnings.Add($"Table '{tableCode}' not found in this version — skipped."); continue; }

            var dimIndices = table.DimensionNames
                .Select(d => (Name: d, Idx: Array.FindIndex(headers, h => string.Equals(h, d, StringComparison.OrdinalIgnoreCase))))
                .ToList();

            if (dimIndices.Any(d => d.Idx < 0))
            {
                warnings.Add($"Table '{tableCode}': missing dimension columns — skipped.");
                continue;
            }

            _db.FactorRows.RemoveRange(table.Rows);
            table.Rows.Clear();

            foreach (var parts in rows)
            {
                if (!decimal.TryParse(parts[factorIdx], out var factor))
                {
                    warnings.Add($"Table '{tableCode}': unparseable factor '{parts[factorIdx]}' — row skipped.");
                    continue;
                }
                var dims = dimIndices.ToDictionary(d => d.Name, d => parts[d.Idx]);
                table.Rows.Add(new FactorRow { DimensionValues = dims, Factor = factor });
            }

            result.TablesUpdated.Add(tableCode);
            result.RowCountByTable[tableCode] = table.Rows.Count;
        }

        version.LastEditedById = CurrentUserId;
        version.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Ok(result);
    }

    // ─── Impact preview ───────────────────────────────────────────────────────

    [HttpPost("{id:guid}/preview-impact")]
    [Authorize(Policy = AppPermissions.RatingAdmin)]
    public async Task<IActionResult> ComputeImpactPreview(Guid id, CancellationToken ct)
    {
        var version = await _db.RatingPlanVersions
            .Where(v => v.Id == id && !v.IsDeleted)
            .Include(v => v.RatingPlan)
            .Include(v => v.FactorTables).ThenInclude(t => t.Rows)
            .FirstOrDefaultAsync(ct);

        if (version == null) return NotFound();
        if (version.Status != PlanStatus.Draft)
            return Conflict(new { ErrorCode = "NOT_DRAFT", ErrorMessage = "Impact preview is only for Draft versions." });

        if (!version.FactorTables.Any(t => t.Rows.Any()))
            return Conflict(new { ErrorCode = "NO_FACTORS", ErrorMessage = "Version must have factor tables to preview impact." });

        var baseRateTable = version.FactorTables.FirstOrDefault(t => t.Code == "BASE_RATE");
        var dedTable = version.FactorTables.FirstOrDefault(t => t.Code == "DEDUCTIBLE_FACTOR");

        if (baseRateTable == null || dedTable == null)
            return Conflict(new { ErrorCode = "MISSING_FACTORS", ErrorMessage = "Version must have BASE_RATE and DEDUCTIBLE_FACTOR tables." });

        var lob = version.RatingPlan.LineOfBusiness;

        // Get IDs of open rated quotes for this LOB
        var openRatedQuoteIds = await _db.QuoteRatingSnapshots
            .Where(s => !s.IsDeleted
                && !s.Quote.IsDeleted
                && s.Quote.LineOfBusiness == lob
                && s.Quote.Status != QuoteStatus.Bound
                && s.Quote.Status != QuoteStatus.Cancelled
                && s.Quote.Status != QuoteStatus.Declined
                && s.Quote.Status != QuoteStatus.Expired)
            .Select(s => s.QuoteId)
            .Distinct()
            .ToListAsync(ct);

        var allSnapshots = await _db.QuoteRatingSnapshots
            .Where(s => openRatedQuoteIds.Contains(s.QuoteId) && !s.IsDeleted)
            .Include(s => s.Quote)
                .ThenInclude(q => q.Submission)
                    .ThenInclude(sub => sub.Equipment)
                        .ThenInclude(e => e.EquipmentType)
            .Include(s => s.Quote)
                .ThenInclude(q => q.Submission)
                    .ThenInclude(sub => sub.Insured)
            .ToListAsync(ct);

        var ratedQuotes = allSnapshots
            .GroupBy(s => s.QuoteId)
            .Select(g => g.OrderByDescending(s => s.RatedAt).First())
            .ToList();

        var movers = new List<(string QuoteNumber, string InsuredName, decimal OldPrem, decimal NewPrem, decimal DeltaPct)>();
        int quotesUp = 0, quotesDown = 0, quotesFlat = 0;
        decimal totalOld = 0, totalNew = 0;

        var effectiveYear = DateTime.UtcNow.Year;

        foreach (var snapshot in ratedQuotes)
        {
            var equipment = snapshot.Quote.Submission.Equipment
                .Where(e => e.EquipmentTypeId.HasValue && e.Value.HasValue && e.EquipmentType != null)
                .ToList();
            if (!equipment.Any()) continue;

            var inputs = equipment.Select(e => new ImV1Formula.EquipmentInput(
                e.EquipmentType!.TypeNumber, e.Year, e.Value!.Value, e.Deductible)).ToList();

            ImV1Formula.RatingResult rateResult;
            try
            {
                rateResult = ImV1Formula.Rate(baseRateTable, dedTable, inputs, effectiveYear, snapshot.ScheduleModifier, version.MinimumPremium);
            }
            catch { continue; }

            var oldPrem = snapshot.ManualPremium;
            var newPrem = rateResult.ManualPremium;
            var deltaPct = oldPrem == 0 ? 0 : Math.Round((newPrem - oldPrem) / oldPrem * 100, 2);

            totalOld += oldPrem;
            totalNew += newPrem;

            if (deltaPct > 0.5m) quotesUp++;
            else if (deltaPct < -0.5m) quotesDown++;
            else quotesFlat++;

            var insured = snapshot.Quote.Submission.Insured;
            var insuredName = insured?.CompanyName ?? $"{insured?.FirstName} {insured?.LastName}".Trim();
            movers.Add((snapshot.Quote.QuoteNumber, insuredName.Length > 0 ? insuredName : "Unknown", oldPrem, newPrem, deltaPct));
        }

        var topMovers = movers
            .OrderByDescending(m => Math.Abs(m.DeltaPct))
            .Take(10)
            .Select(m => new TopMoverDto
            {
                QuoteId = Guid.Empty,
                QuoteNumber = m.QuoteNumber,
                InsuredName = m.InsuredName,
                CurrentPremium = m.OldPrem,
                NewPremium = m.NewPrem,
                DeltaPct = m.DeltaPct,
            })
            .ToList();

        var totalDeltaPct = totalOld == 0 ? 0 : Math.Round((totalNew - totalOld) / totalOld * 100, 2);

        var buckets = new[]
        {
            ("<-20%", movers.Count(m => m.DeltaPct < -20)),
            ("-20% to -10%", movers.Count(m => m.DeltaPct >= -20 && m.DeltaPct < -10)),
            ("-10% to -5%", movers.Count(m => m.DeltaPct >= -10 && m.DeltaPct < -5)),
            ("-5% to 0%", movers.Count(m => m.DeltaPct >= -5 && m.DeltaPct < -0.5m)),
            ("Flat", movers.Count(m => m.DeltaPct >= -0.5m && m.DeltaPct <= 0.5m)),
            ("+0% to +5%", movers.Count(m => m.DeltaPct > 0.5m && m.DeltaPct <= 5)),
            ("+5% to +10%", movers.Count(m => m.DeltaPct > 5 && m.DeltaPct <= 10)),
            ("+10% to +20%", movers.Count(m => m.DeltaPct > 10 && m.DeltaPct <= 20)),
            (">+20%", movers.Count(m => m.DeltaPct > 20)),
        };

        var previewData = new
        {
            distributionBuckets = buckets.Select(b => new { rangeLabel = b.Item1, count = b.Item2 }),
            topMovers = topMovers.Select(t => new
            {
                quoteId = t.QuoteId,
                quoteNumber = t.QuoteNumber,
                insuredName = t.InsuredName,
                currentPremium = t.CurrentPremium,
                newPremium = t.NewPremium,
                deltaPct = t.DeltaPct,
            }),
        };

        var existingPreview = await _db.RatingPlanVersionImpactPreviews
            .FirstOrDefaultAsync(p => p.RatingPlanVersionId == id && !p.IsDeleted, ct);
        if (existingPreview != null)
        {
            _db.RatingPlanVersionImpactPreviews.Remove(existingPreview);
        }

        var preview = new RatingPlanVersionImpactPreview
        {
            RatingPlanVersionId = id,
            ComputedAt = DateTime.UtcNow,
            ComputedById = CurrentUserId,
            QuoteCount = movers.Count,
            TotalCurrentPremium = totalOld,
            TotalNewPremium = totalNew,
            TotalDeltaPct = totalDeltaPct,
            QuotesUp = quotesUp,
            QuotesDown = quotesDown,
            QuotesFlat = quotesFlat,
            PreviewJson = JsonSerializer.Serialize(previewData),
        };

        _db.RatingPlanVersionImpactPreviews.Add(preview);
        await _db.SaveChangesAsync(ct);

        return Ok(MapPreviewToDto(preview));
    }

    [HttpGet("{id:guid}/preview-impact")]
    [Authorize(Policy = AppPermissions.RatingAdmin)]
    public async Task<IActionResult> GetImpactPreview(Guid id, CancellationToken ct)
    {
        var preview = await _db.RatingPlanVersionImpactPreviews
            .Where(p => p.RatingPlanVersionId == id && !p.IsDeleted)
            .OrderByDescending(p => p.ComputedAt)
            .FirstOrDefaultAsync(ct);

        if (preview == null) return NotFound();
        return Ok(MapPreviewToDto(preview));
    }

    private static RatingImpactPreviewDto MapPreviewToDto(RatingPlanVersionImpactPreview p)
    {
        var inner = JsonSerializer.Deserialize<ImpactPreviewInner>(p.PreviewJson) ?? new ImpactPreviewInner();
        return new RatingImpactPreviewDto
        {
            ComputedAt = p.ComputedAt,
            QuoteCount = p.QuoteCount,
            TotalCurrentPremium = p.TotalCurrentPremium,
            TotalNewPremium = p.TotalNewPremium,
            TotalDeltaPct = p.TotalDeltaPct,
            QuotesUp = p.QuotesUp,
            QuotesDown = p.QuotesDown,
            QuotesFlat = p.QuotesFlat,
            DistributionBuckets = inner.DistributionBuckets
                .Select(b => new DistributionBucketDto { RangeLabel = b.RangeLabel, Count = b.Count })
                .ToList(),
            TopMovers = inner.TopMovers
                .Select(t => new TopMoverDto
                {
                    QuoteId = t.QuoteId,
                    QuoteNumber = t.QuoteNumber,
                    InsuredName = t.InsuredName,
                    CurrentPremium = t.CurrentPremium,
                    NewPremium = t.NewPremium,
                    DeltaPct = t.DeltaPct,
                })
                .ToList(),
        };
    }

    private record ImpactPreviewInner(
        List<BucketRecord> DistributionBuckets,
        List<MoverRecord> TopMovers)
    {
        public ImpactPreviewInner() : this([], []) { }
    }

    private record BucketRecord(string RangeLabel, int Count);
    private record MoverRecord(Guid QuoteId, string QuoteNumber, string InsuredName,
        decimal CurrentPremium, decimal NewPremium, decimal DeltaPct);
}
