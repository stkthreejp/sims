using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIMS.API.Services;
using SIMS.Domain.Entities;
using SIMS.Infrastructure.Data;
using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/legal-requirements")]
[Authorize]
public class LegalRequirementsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IOpenLawsClient _openLawsClient;
    private readonly ILogger<LegalRequirementsController> _logger;

    public LegalRequirementsController(ApplicationDbContext db, IOpenLawsClient openLawsClient, ILogger<LegalRequirementsController> logger)
    {
        _db = db;
        _openLawsClient = openLawsClient;
        _logger = logger;
    }

    private Guid? CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
        ? userId
        : null;

    private string CurrentUserName => User.FindFirstValue(ClaimTypes.Name)
        ?? User.FindFirstValue("name")
        ?? "Unknown";

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LegalRequirementSectionDto>>> GetSections(
        [FromQuery] string? state,
        [FromQuery] string? action,
        [FromQuery] string? category,
        [FromQuery] string? search)
    {
        var query = _db.LegalRequirementSections.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(state))
            query = query.Where(r => r.State == state);

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(r => r.Action == action);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(r => r.Category == category);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(r =>
                EF.Functions.ILike(r.Topic, pattern) ||
                EF.Functions.ILike(r.Category, pattern) ||
                EF.Functions.ILike(r.RequirementText, pattern));
        }

        var sections = await query
            .OrderBy(r => r.State)
            .ThenBy(r => r.SortOrder)
            .Select(r => new LegalRequirementSectionDto(
                r.Id,
                r.State,
                r.LineOfBusiness,
                r.Action,
                r.Category,
                r.Topic,
                r.RequirementText,
                r.Citations,
                r.SourceName,
                r.SourceDocument,
                r.SourceCreatedAt,
                r.ReviewStatus,
                r.LastVerifiedAt,
                r.SortOrder))
            .ToListAsync();

        return Ok(sections);
    }

    [HttpGet("summary")]
    public async Task<ActionResult<LegalRequirementsSummaryDto>> GetSummary()
    {
        var rows = await _db.LegalRequirementSections
            .AsNoTracking()
            .Select(r => new { r.State, r.Action, r.Category, r.ReviewStatus, r.SourceName, r.SourceDocument, r.SourceCreatedAt })
            .ToListAsync();

        var summary = new LegalRequirementsSummaryDto(
            rows.Select(r => r.State).Distinct().Order().ToArray(),
            rows.Select(r => r.Action).Distinct().Order().ToArray(),
            rows.Select(r => r.Category).Distinct().Order().ToArray(),
            rows.Count,
            rows.GroupBy(r => r.State).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()),
            rows.GroupBy(r => r.ReviewStatus).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()),
            await _db.LegalTrackedSources.AsNoTracking().CountAsync(),
            await _db.LegalSourceScanRuns.AsNoTracking().CountAsync(),
            await _db.LegalSourceScanResults.AsNoTracking().CountAsync(r => r.ReviewStatus == "Pending"),
            await _db.LegalRequirementChangeLogs.AsNoTracking().CountAsync(),
            rows.FirstOrDefault()?.SourceName ?? "Oden Online",
            rows.FirstOrDefault()?.SourceDocument ?? "COMMERCIAL INSURANCE - CANCELLATION - P&C",
            rows.FirstOrDefault()?.SourceCreatedAt);

        return Ok(summary);
    }

    [HttpGet("sources")]
    public async Task<ActionResult<IReadOnlyList<LegalTrackedSourceDto>>> GetSources([FromQuery] string? state)
    {
        var query = _db.LegalTrackedSources.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(state))
            query = query.Where(s => s.State == state || s.State == "All");

        var sources = await query
            .OrderBy(s => s.State == "All" ? string.Empty : s.State)
            .ThenBy(s => s.Name)
            .Select(s => new LegalTrackedSourceDto(
                s.Id,
                s.State,
                s.Name,
                s.SourceType,
                s.Url,
                !string.IsNullOrWhiteSpace(s.ApiKey),
                s.IsEnabled,
                s.ScanCadence,
                s.LastCheckedAt,
                s.LastChangedAt,
                s.LastStatus,
                s.LastErrorMessage,
                s.Notes))
            .ToListAsync();

        return Ok(sources);
    }

    [HttpPost("sources")]
    [Authorize(Policy = AppPermissions.AdminSystemManage)]
    public async Task<ActionResult<LegalTrackedSourceDto>> CreateSource([FromBody] LegalTrackedSourceUpsertDto dto)
    {
        var validationError = ValidateSource(dto);
        if (validationError != null)
            return BadRequest(new { errorMessage = validationError });

        var state = dto.State.Trim();
        var name = dto.Name.Trim();
        var sourceType = dto.SourceType.Trim();

        var exists = await _db.LegalTrackedSources.AnyAsync(s =>
            s.State == state &&
            s.Name == name &&
            s.SourceType == sourceType);

        if (exists)
            return BadRequest(new { errorMessage = "A tracked source with the same state, name, and type already exists." });

        var source = new LegalTrackedSource
        {
            State = state,
            Name = name,
            SourceType = sourceType,
            Url = TrimToNull(dto.Url),
            ApiKey = TrimToNull(dto.ApiKey),
            IsEnabled = dto.IsEnabled,
            ScanCadence = dto.ScanCadence.Trim(),
            LastStatus = "NotChecked",
            Notes = TrimToNull(dto.Notes)
        };

        _db.LegalTrackedSources.Add(source);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetSources), new { state = source.State }, ToDto(source));
    }

    [HttpPut("sources/{sourceId:guid}")]
    [Authorize(Policy = AppPermissions.AdminSystemManage)]
    public async Task<ActionResult<LegalTrackedSourceDto>> UpdateSource(Guid sourceId, [FromBody] LegalTrackedSourceUpsertDto dto)
    {
        var source = await _db.LegalTrackedSources.FirstOrDefaultAsync(s => s.Id == sourceId);
        if (source == null)
            return NotFound();

        var validationError = ValidateSource(dto);
        if (validationError != null)
            return BadRequest(new { errorMessage = validationError });

        var state = dto.State.Trim();
        var name = dto.Name.Trim();
        var sourceType = dto.SourceType.Trim();

        var exists = await _db.LegalTrackedSources.AnyAsync(s =>
            s.Id != sourceId &&
            s.State == state &&
            s.Name == name &&
            s.SourceType == sourceType);

        if (exists)
            return BadRequest(new { errorMessage = "A tracked source with the same state, name, and type already exists." });

        source.State = state;
        source.Name = name;
        source.SourceType = sourceType;
        source.Url = TrimToNull(dto.Url);
        var apiKey = TrimToNull(dto.ApiKey);
        if (apiKey != null)
            source.ApiKey = apiKey;
        source.IsEnabled = dto.IsEnabled;
        source.ScanCadence = dto.ScanCadence.Trim();
        source.Notes = TrimToNull(dto.Notes);

        await _db.SaveChangesAsync();

        return Ok(ToDto(source));
    }

    [HttpPost("sources/{sourceId:guid}/scan")]
    [Authorize(Policy = AppPermissions.AdminSystemManage)]
    public async Task<ActionResult<LegalSourceScanRunDto>> ScanSource(Guid sourceId)
    {
        var source = await _db.LegalTrackedSources.FirstOrDefaultAsync(s => s.Id == sourceId);
        if (source == null)
            return NotFound();

        if (!source.IsEnabled)
            return BadRequest(new { errorMessage = "Tracked source is disabled." });

        if (source.SourceType.Equals("OpenLaw API", StringComparison.OrdinalIgnoreCase) ||
            source.SourceType.Equals("OpenLaws API", StringComparison.OrdinalIgnoreCase))
            return await ScanOpenLawsSource(source);

        var now = DateTime.UtcNow;
        var run = new LegalSourceScanRun
        {
            SourceName = source.Name,
            SourceType = source.SourceType,
            Status = "Completed",
            StartedAt = now,
            CompletedAt = now,
            ResultsFound = 0,
            PossibleChanges = 0,
            StartedById = CurrentUserId,
            StartedByName = CurrentUserName
        };

        source.LastCheckedAt = now;
        source.LastStatus = "Completed";
        source.LastErrorMessage = null;

        _db.LegalSourceScanRuns.Add(run);
        await _db.SaveChangesAsync();

        return Ok(new LegalSourceScanRunDto(
            run.Id,
            run.SourceName,
            run.SourceType,
            run.Status,
            run.StartedAt,
            run.CompletedAt,
            run.ResultsFound,
            run.PossibleChanges,
            run.ErrorMessage,
            run.StartedByName));
    }

    private async Task<ActionResult<LegalSourceScanRunDto>> ScanOpenLawsSource(LegalTrackedSource source)
    {
        if (string.IsNullOrWhiteSpace(source.ApiKey))
            return BadRequest(new { errorMessage = "OpenLaws API key is required before this source can be checked." });

        var now = DateTime.UtcNow;
        var run = new LegalSourceScanRun
        {
            SourceName = source.Name,
            SourceType = source.SourceType,
            Status = "Completed",
            StartedAt = now,
            StartedById = CurrentUserId,
            StartedByName = CurrentUserName
        };

        _db.LegalSourceScanRuns.Add(run);
        source.LastCheckedAt = now;

        try
        {
            var jurisdictions = await OpenLawsJurisdictionsAsync(source.State);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var findings = new List<LegalSourceScanResult>();

            foreach (var jurisdiction in jurisdictions)
            {
                foreach (var scanQuery in OpenLawsScanQueries())
                {
                    var results = await _openLawsClient.SearchAsync(
                        new OpenLawsSearchRequest(
                            source.Url ?? "https://api.openlaws.us",
                            source.ApiKey,
                            jurisdiction.Key,
                            scanQuery.Query,
                            source.State == "All" ? 3 : 5),
                        HttpContext.RequestAborted);

                    foreach (var result in results)
                    {
                        var key = $"{result.Jurisdiction}|{result.LawKey}|{result.Path}";
                        if (!seen.Add(key))
                            continue;

                        findings.Add(new LegalSourceScanResult
                        {
                            ScanRun = run,
                            State = jurisdiction.State,
                            Category = scanQuery.Category,
                            Topic = Truncate(result.DisplayName, 160),
                            MatchStatus = "PossibleChange",
                            SourceUrl = Truncate(result.WebUrl ?? string.Empty, 1000),
                            SourceCitation = Truncate(OpenLawsCitation(result), 300),
                            SourceText = result.Text,
                            SuggestedRequirementText = result.Text,
                            ConfidenceScore = 0.65m,
                            ReviewStatus = "Pending"
                        });
                    }
                }
            }

            _db.LegalSourceScanResults.AddRange(findings);
            run.ResultsFound = findings.Count;
            run.PossibleChanges = findings.Count;
            run.CompletedAt = DateTime.UtcNow;
            source.LastStatus = "Completed";
            source.LastErrorMessage = null;
            if (findings.Count > 0)
                source.LastChangedAt = run.CompletedAt;
        }
        catch (Exception ex) when (ex is OpenLawsException or HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "OpenLaws scan failed for source {SourceId} ({SourceName})", source.Id, source.Name);
            run.Status = "Failed";
            run.CompletedAt = DateTime.UtcNow;
            run.ErrorMessage = ex.Message;
            source.LastStatus = "Failed";
            source.LastErrorMessage = ex.Message;
        }

        await _db.SaveChangesAsync();

        return Ok(new LegalSourceScanRunDto(
            run.Id,
            run.SourceName,
            run.SourceType,
            run.Status,
            run.StartedAt,
            run.CompletedAt,
            run.ResultsFound,
            run.PossibleChanges,
            run.ErrorMessage,
            run.StartedByName));
    }

    [HttpGet("scan-runs")]
    public async Task<ActionResult<IReadOnlyList<LegalSourceScanRunDto>>> GetScanRuns()
    {
        var runs = await _db.LegalSourceScanRuns
            .AsNoTracking()
            .OrderByDescending(r => r.StartedAt)
            .Take(50)
            .Select(r => new LegalSourceScanRunDto(
                r.Id,
                r.SourceName,
                r.SourceType,
                r.Status,
                r.StartedAt,
                r.CompletedAt,
                r.ResultsFound,
                r.PossibleChanges,
                r.ErrorMessage,
                r.StartedByName))
            .ToListAsync();

        return Ok(runs);
    }

    [HttpGet("scan-results")]
    public async Task<ActionResult<IReadOnlyList<LegalSourceScanResultDto>>> GetScanResults(
        [FromQuery] string? reviewStatus,
        [FromQuery] string? state,
        [FromQuery] Guid? scanRunId)
    {
        var query = _db.LegalSourceScanResults
            .AsNoTracking()
            .Include(r => r.ScanRun)
            .Include(r => r.RequirementSection)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(reviewStatus))
            query = query.Where(r => r.ReviewStatus == reviewStatus);

        if (!string.IsNullOrWhiteSpace(state))
            query = query.Where(r => r.State == state);

        if (scanRunId.HasValue)
            query = query.Where(r => r.ScanRunId == scanRunId.Value);

        var results = await query
            .OrderByDescending(r => r.CreatedAt)
            .Take(scanRunId.HasValue ? 500 : 100)
            .Select(r => new LegalSourceScanResultDto(
                r.Id,
                r.ScanRunId,
                r.ScanRun.SourceName,
                r.RequirementSectionId,
                r.State,
                r.Category,
                r.Topic,
                r.RequirementSection != null ? r.RequirementSection.RequirementText : null,
                r.RequirementSection != null ? r.RequirementSection.Citations : Array.Empty<string>(),
                r.MatchStatus,
                r.SourceUrl,
                r.SourceCitation,
                r.SourceText,
                r.SuggestedRequirementText,
                r.ConfidenceScore,
                r.ReviewStatus,
                r.ReviewedByName,
                r.ReviewedAt,
                r.CreatedAt))
            .ToListAsync();

        return Ok(results);
    }

    [HttpGet("change-log")]
    public async Task<ActionResult<IReadOnlyList<LegalRequirementChangeLogDto>>> GetChangeLog(
        [FromQuery] Guid? requirementSectionId,
        [FromQuery] string? state)
    {
        var query = _db.LegalRequirementChangeLogs
            .AsNoTracking()
            .Include(l => l.RequirementSection)
            .AsQueryable();

        if (requirementSectionId.HasValue)
            query = query.Where(l => l.RequirementSectionId == requirementSectionId.Value);

        if (!string.IsNullOrWhiteSpace(state))
            query = query.Where(l => l.RequirementSection.State == state);

        var logs = await query
            .OrderByDescending(l => l.ChangedAt)
            .Take(100)
            .Select(l => new LegalRequirementChangeLogDto(
                l.Id,
                l.RequirementSectionId,
                l.RequirementSection.State,
                l.RequirementSection.Category,
                l.RequirementSection.Topic,
                l.ScanResultId,
                l.ChangeType,
                l.FieldName,
                l.OldValue,
                l.NewValue,
                l.Comment,
                l.ChangedByName,
                l.ChangedAt))
            .ToListAsync();

        return Ok(logs);
    }

    [HttpPost("imports/oden")]
    [Authorize(Policy = AppPermissions.AdminSystemManage)]
    [RequestSizeLimit(10_000_000)]
    public async Task<ActionResult<LegalSourceScanRunDto>> ImportOden([FromForm] IFormFile file)
    {
        if (file.Length == 0)
            return BadRequest(new { errorMessage = "Oden export file is required." });

        using var reader = new StreamReader(file.OpenReadStream());
        var html = await reader.ReadToEndAsync();
        var importedSections = OdenChartParser.Parse(html);

        if (importedSections.Count == 0)
            return BadRequest(new { errorMessage = "No requirement sections were found in the Oden export." });

        var importedAction = importedSections.Select(s => s.Action).Distinct().SingleOrDefault() ?? "Cancellation";

        var run = new LegalSourceScanRun
        {
            SourceName = OdenSourceName(importedAction),
            SourceType = "Manual HTML Export",
            Status = "Completed",
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            ResultsFound = importedSections.Count,
            StartedById = CurrentUserId,
            StartedByName = CurrentUserName
        };

        _db.LegalSourceScanRuns.Add(run);

        var existingSections = await _db.LegalRequirementSections.ToListAsync();
        var possibleChanges = 0;

        foreach (var section in importedSections)
        {
            var existing = existingSections.FirstOrDefault(r =>
                r.State == section.State &&
                r.LineOfBusiness == section.LineOfBusiness &&
                r.Action == section.Action &&
                r.Category == section.Category &&
                r.Topic == section.Topic);

            var matchStatus = "NoChange";
            var reviewStatus = "Reviewed";
            string? suggestedText = null;

            if (existing == null)
            {
                matchStatus = "NewRequirement";
                reviewStatus = "Pending";
                suggestedText = section.RequirementText;
                possibleChanges++;
            }
            else if (!RequirementTextEquals(existing.RequirementText, section.RequirementText) ||
                     !existing.Citations.Order().SequenceEqual(section.Citations.Order()))
            {
                matchStatus = "PossibleChange";
                reviewStatus = "Pending";
                suggestedText = section.RequirementText;
                possibleChanges++;
            }

            _db.LegalSourceScanResults.Add(new LegalSourceScanResult
            {
                ScanRun = run,
                RequirementSectionId = existing?.Id,
                State = section.State,
                Category = section.Category,
                Topic = section.Topic,
                MatchStatus = matchStatus,
                SourceUrl = string.Empty,
                SourceCitation = string.Join("; ", section.Citations),
                SourceText = section.RequirementText,
                SuggestedRequirementText = suggestedText,
                ConfidenceScore = matchStatus == "NoChange" ? 1m : 0.85m,
                ReviewStatus = reviewStatus,
                ReviewedById = matchStatus == "NoChange" ? CurrentUserId : null,
                ReviewedByName = matchStatus == "NoChange" ? CurrentUserName : null,
                ReviewedAt = matchStatus == "NoChange" ? DateTime.UtcNow : null
            });
        }

        run.PossibleChanges = possibleChanges;
        var odenSource = await _db.LegalTrackedSources.FirstOrDefaultAsync(s =>
            s.Name == OdenSourceName(importedAction) &&
            s.SourceType == "Oden Export");

        if (odenSource != null)
        {
            odenSource.LastCheckedAt = DateTime.UtcNow;
            odenSource.LastStatus = "Completed";
            odenSource.LastErrorMessage = null;
            if (possibleChanges > 0)
                odenSource.LastChangedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        return Ok(new LegalSourceScanRunDto(
            run.Id,
            run.SourceName,
            run.SourceType,
            run.Status,
            run.StartedAt,
            run.CompletedAt,
            run.ResultsFound,
            run.PossibleChanges,
            run.ErrorMessage,
            run.StartedByName));
    }

    [HttpPost("scan-runs/simulate-change")]
    [Authorize(Policy = AppPermissions.AdminSystemManage)]
    public async Task<ActionResult<LegalSourceScanResultDto>> SimulateChange()
    {
        var requirement = await _db.LegalRequirementSections
            .OrderBy(r => r.State)
            .ThenBy(r => r.SortOrder)
            .FirstOrDefaultAsync(r => r.Category == "NOTICE REQUIREMENTS");

        if (requirement == null)
            return BadRequest(new { errorMessage = "No notice requirement is available to simulate a change." });

        var run = new LegalSourceScanRun
        {
            SourceName = "Simulation",
            SourceType = "Admin Test",
            Status = "Completed",
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            ResultsFound = 1,
            PossibleChanges = 1,
            StartedById = CurrentUserId,
            StartedByName = CurrentUserName
        };

        var result = new LegalSourceScanResult
        {
            ScanRun = run,
            RequirementSection = requirement,
            State = requirement.State,
            Category = requirement.Category,
            Topic = requirement.Topic,
            MatchStatus = "PossibleChange",
            SourceUrl = string.Empty,
            SourceCitation = string.Join("; ", requirement.Citations),
            SourceText = $"{requirement.RequirementText} [SIMULATED REVIEW CHANGE - do not approve unless testing.]",
            SuggestedRequirementText = $"{requirement.RequirementText} [SIMULATED REVIEW CHANGE - do not approve unless testing.]",
            ConfidenceScore = 0.5m,
            ReviewStatus = "Pending"
        };

        _db.LegalSourceScanRuns.Add(run);
        _db.LegalSourceScanResults.Add(result);
        await _db.SaveChangesAsync();

        return Ok(new LegalSourceScanResultDto(
            result.Id,
            run.Id,
            run.SourceName,
            requirement.Id,
            result.State,
            result.Category,
            result.Topic,
            requirement.RequirementText,
            requirement.Citations,
            result.MatchStatus,
            result.SourceUrl,
            result.SourceCitation,
            result.SourceText,
            result.SuggestedRequirementText,
            result.ConfidenceScore,
            result.ReviewStatus,
            result.ReviewedByName,
            result.ReviewedAt,
            result.CreatedAt));
    }

    [HttpPost("scan-results/{scanResultId:guid}/approve")]
    [Authorize(Policy = AppPermissions.AdminSystemManage)]
    public async Task<IActionResult> ApproveScanResult(Guid scanResultId, [FromBody] LegalScanResultReviewDto dto)
    {
        var result = await _db.LegalSourceScanResults
            .Include(r => r.ScanRun)
            .Include(r => r.RequirementSection)
            .FirstOrDefaultAsync(r => r.Id == scanResultId);

        if (result == null)
            return NotFound();

        if (result.ReviewStatus != "Pending")
            return BadRequest(new { errorMessage = "Scan result has already been reviewed." });

        if (result.RequirementSection == null)
        {
            var action = InferAction(result.ScanRun.SourceName, result.SourceText);
            var newSection = new LegalRequirementSection
            {
                State = result.State,
                LineOfBusiness = "Commercial P&C",
                Action = action,
                Category = result.Category,
                Topic = result.Topic,
                RequirementText = result.SuggestedRequirementText ?? result.SourceText,
                Citations = SplitCitations(result.SourceCitation),
                SourceName = OdenSourceName(action),
                SourceDocument = OdenSourceDocument(action),
                SourceCreatedAt = DateTime.UtcNow,
                ReviewStatus = "Approved",
                LastVerifiedAt = DateTime.UtcNow,
                SortOrder = await NextSortOrderAsync(result.State)
            };

            _db.LegalRequirementSections.Add(newSection);
            result.RequirementSection = newSection;

            _db.LegalRequirementChangeLogs.Add(new LegalRequirementChangeLog
            {
                RequirementSection = newSection,
                ScanResult = result,
                ChangeType = "Create",
                FieldName = "RequirementText",
                OldValue = null,
                NewValue = newSection.RequirementText,
                Comment = dto.Comment,
                ChangedById = CurrentUserId,
                ChangedByName = CurrentUserName,
                ChangedAt = DateTime.UtcNow
            });
        }
        else
        {
            var section = result.RequirementSection;
            var newText = result.SuggestedRequirementText ?? result.SourceText;
            var newCitations = SplitCitations(result.SourceCitation);

            if (!RequirementTextEquals(section.RequirementText, newText))
            {
                _db.LegalRequirementChangeLogs.Add(new LegalRequirementChangeLog
                {
                    RequirementSection = section,
                    ScanResult = result,
                    ChangeType = "Update",
                    FieldName = "RequirementText",
                    OldValue = section.RequirementText,
                    NewValue = newText,
                    Comment = dto.Comment,
                    ChangedById = CurrentUserId,
                    ChangedByName = CurrentUserName,
                    ChangedAt = DateTime.UtcNow
                });
                section.RequirementText = newText;
            }

            if (!section.Citations.Order().SequenceEqual(newCitations.Order()))
            {
                _db.LegalRequirementChangeLogs.Add(new LegalRequirementChangeLog
                {
                    RequirementSection = section,
                    ScanResult = result,
                    ChangeType = "Update",
                    FieldName = "Citations",
                    OldValue = string.Join("; ", section.Citations),
                    NewValue = string.Join("; ", newCitations),
                    Comment = dto.Comment,
                    ChangedById = CurrentUserId,
                    ChangedByName = CurrentUserName,
                    ChangedAt = DateTime.UtcNow
                });
                section.Citations = newCitations;
            }

            section.ReviewStatus = "Approved";
            section.LastVerifiedAt = DateTime.UtcNow;
        }

        result.ReviewStatus = "Approved";
        result.ReviewedById = CurrentUserId;
        result.ReviewedByName = CurrentUserName;
        result.ReviewedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("scan-results/{scanResultId:guid}/reject")]
    [Authorize(Policy = AppPermissions.AdminSystemManage)]
    public async Task<IActionResult> RejectScanResult(Guid scanResultId, [FromBody] LegalScanResultReviewDto dto)
    {
        var result = await _db.LegalSourceScanResults.FirstOrDefaultAsync(r => r.Id == scanResultId);
        if (result == null)
            return NotFound();

        if (result.ReviewStatus != "Pending")
            return BadRequest(new { errorMessage = "Scan result has already been reviewed." });

        result.ReviewStatus = "Rejected";
        result.ReviewedById = CurrentUserId;
        result.ReviewedByName = string.IsNullOrWhiteSpace(dto.Comment)
            ? CurrentUserName
            : $"{CurrentUserName}: {dto.Comment}";
        result.ReviewedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<int> NextSortOrderAsync(string state)
    {
        var max = await _db.LegalRequirementSections
            .Where(r => r.State == state)
            .MaxAsync(r => (int?)r.SortOrder);

        return (max ?? 0) + 1;
    }

    private async Task<IReadOnlyList<OpenLawsJurisdiction>> OpenLawsJurisdictionsAsync(string sourceState)
    {
        if (!sourceState.Equals("All", StringComparison.OrdinalIgnoreCase))
            return [new OpenLawsJurisdiction(sourceState, ToOpenLawsJurisdictionKey(sourceState))];

        var states = await _db.LegalRequirementSections
            .AsNoTracking()
            .Select(r => r.State)
            .Where(s => s != "All")
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync();

        return states
            .Select(state => new OpenLawsJurisdiction(state, ToOpenLawsJurisdictionKey(state)))
            .ToList();
    }

    private static string ToOpenLawsJurisdictionKey(string state)
    {
        var trimmed = state.Trim();
        if (Regex.IsMatch(trimmed, "^[A-Za-z]{2,3}$"))
            return trimmed.ToUpperInvariant();

        if (StateAbbreviations.TryGetValue(trimmed, out var abbreviation))
            return abbreviation;

        throw new OpenLawsException($"State '{state}' could not be mapped to an OpenLaws jurisdiction key.");
    }

    private static readonly Dictionary<string, string> StateAbbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Alabama"] = "AL",
        ["Alaska"] = "AK",
        ["Arizona"] = "AZ",
        ["Arkansas"] = "AR",
        ["California"] = "CA",
        ["Colorado"] = "CO",
        ["Connecticut"] = "CT",
        ["Delaware"] = "DE",
        ["District of Columbia"] = "DC",
        ["Florida"] = "FL",
        ["Georgia"] = "GA",
        ["Hawaii"] = "HI",
        ["Idaho"] = "ID",
        ["Illinois"] = "IL",
        ["Indiana"] = "IN",
        ["Iowa"] = "IA",
        ["Kansas"] = "KS",
        ["Kentucky"] = "KY",
        ["Louisiana"] = "LA",
        ["Maine"] = "ME",
        ["Maryland"] = "MD",
        ["Massachusetts"] = "MA",
        ["Michigan"] = "MI",
        ["Minnesota"] = "MN",
        ["Mississippi"] = "MS",
        ["Missouri"] = "MO",
        ["Montana"] = "MT",
        ["Nebraska"] = "NE",
        ["Nevada"] = "NV",
        ["New Hampshire"] = "NH",
        ["New Jersey"] = "NJ",
        ["New Mexico"] = "NM",
        ["New York"] = "NY",
        ["North Carolina"] = "NC",
        ["North Dakota"] = "ND",
        ["Ohio"] = "OH",
        ["Oklahoma"] = "OK",
        ["Oregon"] = "OR",
        ["Pennsylvania"] = "PA",
        ["Rhode Island"] = "RI",
        ["South Carolina"] = "SC",
        ["South Dakota"] = "SD",
        ["Tennessee"] = "TN",
        ["Texas"] = "TX",
        ["Utah"] = "UT",
        ["Vermont"] = "VT",
        ["Virginia"] = "VA",
        ["Washington"] = "WA",
        ["West Virginia"] = "WV",
        ["Wisconsin"] = "WI",
        ["Wyoming"] = "WY"
    };

    private static IReadOnlyList<OpenLawsScanQuery> OpenLawsScanQueries() =>
    [
        new("commercial insurance cancellation notice", "NOTICE REQUIREMENTS"),
        new("commercial insurance nonrenewal notice", "NOTICE REQUIREMENTS")
    ];

    private static string OpenLawsCitation(OpenLawsSearchResult result)
    {
        var identifier = string.IsNullOrWhiteSpace(result.Identifier) ? result.Path : result.Identifier;
        return string.IsNullOrWhiteSpace(identifier)
            ? result.LawKey
            : $"{result.LawKey} {identifier}".Trim();
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static bool RequirementTextEquals(string left, string right)
    {
        return string.Equals(NormalizeWhitespace(left), NormalizeWhitespace(right), StringComparison.Ordinal);
    }

    private static string NormalizeWhitespace(string value)
    {
        return Regex.Replace(value.Trim(), @"\s+", " ");
    }

    private static string[] SplitCitations(string value)
    {
        return value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string InferAction(string sourceName, string sourceText)
    {
        if (sourceName.Contains("Nonrenewal", StringComparison.OrdinalIgnoreCase) ||
            sourceName.Contains("Non-Renewal", StringComparison.OrdinalIgnoreCase) ||
            sourceText.Contains("nonrenewal", StringComparison.OrdinalIgnoreCase) ||
            sourceText.Contains("non-renewal", StringComparison.OrdinalIgnoreCase))
            return "NonRenewal";

        return "Cancellation";
    }

    private static string OdenSourceName(string action) =>
        action == "NonRenewal" ? "Oden Online Nonrenewal Chart" : "Oden Online Cancellation Chart";

    private static string OdenSourceDocument(string action) =>
        action == "NonRenewal" ? "COMMERCIAL INSURANCE - NONRENEWAL - P&C" : "COMMERCIAL INSURANCE - CANCELLATION - P&C";

    private static string? ValidateSource(LegalTrackedSourceUpsertDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.State))
            return "State is required.";
        if (string.IsNullOrWhiteSpace(dto.Name))
            return "Source name is required.";
        if (string.IsNullOrWhiteSpace(dto.SourceType))
            return "Source type is required.";
        if (string.IsNullOrWhiteSpace(dto.ScanCadence))
            return "Scan cadence is required.";
        if (!string.IsNullOrWhiteSpace(dto.Url) &&
            !Uri.TryCreate(dto.Url.Trim(), UriKind.Absolute, out _))
            return "URL must be a valid absolute URL.";

        return null;
    }

    private static string? TrimToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static LegalTrackedSourceDto ToDto(LegalTrackedSource source)
    {
        return new LegalTrackedSourceDto(
            source.Id,
            source.State,
            source.Name,
            source.SourceType,
            source.Url,
            !string.IsNullOrWhiteSpace(source.ApiKey),
            source.IsEnabled,
            source.ScanCadence,
            source.LastCheckedAt,
            source.LastChangedAt,
            source.LastStatus,
            source.LastErrorMessage,
            source.Notes);
    }

    private sealed record OpenLawsScanQuery(string Query, string Category);
    private sealed record OpenLawsJurisdiction(string State, string Key);
}

public sealed record LegalRequirementSectionDto(
    Guid Id,
    string State,
    string LineOfBusiness,
    string Action,
    string Category,
    string Topic,
    string RequirementText,
    string[] Citations,
    string SourceName,
    string SourceDocument,
    DateTime SourceCreatedAt,
    string ReviewStatus,
    DateTime LastVerifiedAt,
    int SortOrder);

public sealed record LegalRequirementsSummaryDto(
    string[] States,
    string[] Actions,
    string[] Categories,
    int SectionCount,
    Dictionary<string, int> SectionsByState,
    Dictionary<string, int> SectionsByReviewStatus,
    int TrackedSourceCount,
    int ScanRunCount,
    int PendingScanResultCount,
    int ChangeLogCount,
    string SourceName,
    string SourceDocument,
    DateTime? SourceCreatedAt);

public sealed record LegalTrackedSourceDto(
    Guid Id,
    string State,
    string Name,
    string SourceType,
    string? Url,
    bool HasApiKey,
    bool IsEnabled,
    string ScanCadence,
    DateTime? LastCheckedAt,
    DateTime? LastChangedAt,
    string LastStatus,
    string? LastErrorMessage,
    string? Notes);

public sealed record LegalTrackedSourceUpsertDto(
    string State,
    string Name,
    string SourceType,
    string? Url,
    string? ApiKey,
    bool IsEnabled,
    string ScanCadence,
    string? Notes);

public sealed record LegalSourceScanRunDto(
    Guid Id,
    string SourceName,
    string SourceType,
    string Status,
    DateTime StartedAt,
    DateTime? CompletedAt,
    int ResultsFound,
    int PossibleChanges,
    string? ErrorMessage,
    string? StartedByName);

public sealed record LegalSourceScanResultDto(
    Guid Id,
    Guid ScanRunId,
    string SourceName,
    Guid? RequirementSectionId,
    string State,
    string Category,
    string Topic,
    string? CurrentRequirementText,
    string[] CurrentCitations,
    string MatchStatus,
    string SourceUrl,
    string SourceCitation,
    string SourceText,
    string? SuggestedRequirementText,
    decimal? ConfidenceScore,
    string ReviewStatus,
    string? ReviewedByName,
    DateTime? ReviewedAt,
    DateTime CreatedAt);

public sealed record LegalRequirementChangeLogDto(
    Guid Id,
    Guid RequirementSectionId,
    string State,
    string Category,
    string Topic,
    Guid? ScanResultId,
    string ChangeType,
    string FieldName,
    string? OldValue,
    string? NewValue,
    string? Comment,
    string ChangedByName,
    DateTime ChangedAt);

public sealed record LegalScanResultReviewDto(string? Comment);

internal sealed record ParsedOdenRequirementSection(
    string State,
    string LineOfBusiness,
    string Action,
    string Category,
    string Topic,
    string RequirementText,
    string[] Citations,
    int SortOrder);

internal static partial class OdenChartParser
{
    private static readonly HashSet<string> StateNames =
    [
        "Alabama", "Arkansas", "Florida", "Georgia", "Louisiana", "Maryland", "Mississippi",
        "North Carolina", "Oklahoma", "Pennsylvania", "South Carolina", "Tennessee", "Texas", "Virginia"
    ];

    private static readonly string[] CategoryHeadings =
    [
        "SPECIFIC POLICY TYPE OR COVERAGE REQUIREMENTS",
        "REGULATION OF POLICY TYPES",
        "INSURER REQUIREMENTS",
        "NOTICE REQUIREMENTS",
        "DEFINITIONS",
        "REASONS"
    ];

    private static readonly string[] KnownTopics =
    [
        "Additional Information", "Liability Immunity", "Penalty for Noncompliance", "Return of Unearned Premium",
        "Notification to Mortgagee or Lienholder", "Notification to State Authority", "Proof of Notice", "Time Period",
        "Acceptable Reasons", "General Requirements", "Prohibited Reasons", "Exempt Policy Types",
        "Policy Types Regulated by Insurance Code", "Policy Types Regulated by Other Codes or Plans",
        "Automobile - For Hire", "Automobile", "Motor Carrier", "Surplus Lines", "Business Entity",
        "Commercial Building", "Domestic Violence", "Living Unit", "Mine Subsidence",
        "Miscellaneous Casualty Insurance", "Personal Injury Liability Insurance", "Property Damage Liability Insurance", "Residence"
    ];

    public static IReadOnlyList<ParsedOdenRequirementSection> Parse(string html)
    {
        var rows = ExtractRows(html);
        var sections = new List<ParsedOdenRequirementSection>();
        var currentState = string.Empty;
        var sortOrder = 0;
        var action = DetectAction(rows);

        foreach (var text in rows)
        {
            if (string.IsNullOrWhiteSpace(text) ||
                text.StartsWith("COMMERCIAL INSURANCE", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("IMPORTANT NOTE", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("TABLE OF CONTENTS", StringComparison.OrdinalIgnoreCase))
                continue;

            if (StateNames.Contains(text))
            {
                currentState = text;
                sortOrder = 0;
                continue;
            }

            if (string.IsNullOrWhiteSpace(currentState))
                continue;

            var (category, topic) = SplitHeading(text);
            if (CategoryHeadings.Contains(text))
                continue;

            var body = StripHeading(text, category, topic);
            sortOrder++;
            sections.Add(new ParsedOdenRequirementSection(
                currentState,
                "Commercial P&C",
                action,
                category,
                topic,
                body,
                ExtractCitations(body),
                sortOrder));
        }

        return sections;
    }

    private static string DetectAction(IEnumerable<string> rows)
    {
        return rows.Any(r =>
            r.Contains("NONRENEWAL", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("NON-RENEWAL", StringComparison.OrdinalIgnoreCase))
            ? "NonRenewal"
            : "Cancellation";
    }

    private static List<string> ExtractRows(string html)
    {
        var rows = new List<string>();
        foreach (Match rowMatch in Regex.Matches(html, @"<tr\b[^>]*>(.*?)</tr>", RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            var cellMatch = Regex.Match(rowMatch.Groups[1].Value, @"<(?:td|th)\b[^>]*>(.*?)</(?:td|th)>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!cellMatch.Success)
                continue;

            var withoutTags = Regex.Replace(cellMatch.Groups[1].Value, "<.*?>", " ");
            rows.Add(Normalize(WebUtility.HtmlDecode(withoutTags).Replace("\ufffd", string.Empty)));
        }

        return rows;
    }

    private static (string Category, string Topic) SplitHeading(string text)
    {
        foreach (var category in CategoryHeadings)
        {
            if (text == category)
                return (category, category.ToTitleCase());

            var prefix = $"{category} : ";
            if (text.StartsWith(prefix, StringComparison.Ordinal))
            {
                var rest = text[prefix.Length..];
                var topic = KnownTopics.FirstOrDefault(t => rest == t || rest.StartsWith($"{t} ", StringComparison.Ordinal));
                return (category, topic ?? rest);
            }

            if (text.StartsWith($"{category} ", StringComparison.Ordinal))
                return (category, category.ToTitleCase());
        }

        return ("OTHER", "Other");
    }

    private static string StripHeading(string text, string category, string topic)
    {
        foreach (var candidate in new[] { $"{category} : {topic}", $"{category} {topic}", category })
        {
            if (text.StartsWith(candidate, StringComparison.Ordinal))
                return Normalize(text[candidate.Length..]);
        }

        return text;
    }

    private static string[] ExtractCitations(string text)
    {
        return Regex.Matches(text, @"\[([^\]]+)\]")
            .Select(m => Normalize(m.Groups[1].Value))
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .ToArray();
    }

    private static string Normalize(string value)
    {
        return Regex.Replace(value.Trim(), @"\s+", " ");
    }

    private static string ToTitleCase(this string value)
    {
        return string.Join(" ", value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Length == 0 ? w : char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant()));
    }

}
