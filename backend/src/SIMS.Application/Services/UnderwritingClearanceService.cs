using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SIMS.Application.DTOs.Underwriting;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;

namespace SIMS.Application.Services;

public class UnderwritingClearanceService : IUnderwritingClearanceService
{
    private static readonly SubmissionStatus[] OpenSubmissionStatuses =
    [
        SubmissionStatus.New,
        SubmissionStatus.InProgress,
        SubmissionStatus.Quoted,
    ];

    private readonly DbContext _db;

    public UnderwritingClearanceService(DbContext db)
    {
        _db = db;
    }

    public async Task<UnderwritingClearanceEvaluationDto?> GetLatestSubmissionAsync(
        Guid submissionId,
        CancellationToken ct = default)
    {
        var exists = await _db.Set<Submission>().AnyAsync(s => s.Id == submissionId, ct);
        if (!exists)
            return null;

        var results = await _db.Set<UnderwritingClearanceResult>()
            .Where(r => r.SubmissionId == submissionId)
            .OrderBy(r => r.CheckType)
            .Select(r => new UnderwritingClearanceResultDto
            {
                CheckType = r.CheckType,
                Status = r.Status,
                MatchedRecordId = r.MatchedRecordId,
                MatchedRecordLabel = r.MatchedRecordLabel,
                Explanation = r.Explanation,
                IsOverridden = r.IsOverridden,
                OverriddenById = r.OverriddenById,
                OverriddenAt = r.OverriddenAt,
                OverrideReason = r.OverrideReason,
            })
            .ToListAsync(ct);

        return new UnderwritingClearanceEvaluationDto
        {
            SubmissionId = submissionId,
            OverallStatus = ResolveOverallStatus(results),
            Results = results,
        };
    }

    public async Task<UnderwritingClearanceEvaluationDto> EvaluateSubmissionAsync(
        Guid submissionId,
        Guid reviewerId,
        CancellationToken ct = default)
    {
        var submission = await _db.Set<Submission>()
            .Include(s => s.Insured)
            .FirstOrDefaultAsync(s => s.Id == submissionId, ct);

        if (submission == null)
            throw new InvalidOperationException($"Submission {submissionId} was not found.");

        var lobs = ParseLinesOfBusiness(submission.LinesOfBusiness);
        var results = new List<UnderwritingClearanceResultDto>();

        var duplicate = await FindDuplicateSubmissionAsync(submission, lobs, ct);
        if (duplicate != null)
        {
            results.Add(new UnderwritingClearanceResultDto
            {
                CheckType = UnderwritingClearanceCheckType.DuplicateSubmission,
                Status = UnderwritingClearanceStatus.Warning,
                MatchedRecordId = duplicate.Id,
                MatchedRecordLabel = duplicate.SubmissionNumber,
                Explanation = $"Potential duplicate open submission {duplicate.SubmissionNumber} for the same insured, LOB, and effective date window.",
            });
        }

        var overlappingPolicy = await FindActivePolicyOverlapAsync(submission, lobs, ct);
        if (overlappingPolicy != null)
        {
            results.Add(new UnderwritingClearanceResultDto
            {
                CheckType = UnderwritingClearanceCheckType.ActivePolicyOverlap,
                Status = UnderwritingClearanceStatus.Blocked,
                MatchedRecordId = overlappingPolicy.Id,
                MatchedRecordLabel = overlappingPolicy.PolicyNumber,
                Explanation = $"Active policy {overlappingPolicy.PolicyNumber} overlaps the requested effective period for this insured and LOB.",
            });
        }

        await SaveResultsAsync(submission, reviewerId, results, ct);

        return (await GetLatestSubmissionAsync(submission.Id, ct))!;
    }

    public async Task<UnderwritingClearanceEvaluationDto> OverrideSubmissionAsync(
        Guid submissionId,
        Guid overriddenById,
        string reason,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A clearance override reason is required.");

        var results = await _db.Set<UnderwritingClearanceResult>()
            .Where(r => r.SubmissionId == submissionId)
            .OrderBy(r => r.CheckType)
            .ToListAsync(ct);

        if (results.Count == 0)
            throw new InvalidOperationException("Evaluate clearance before overriding it.");

        var blocked = results.Where(r => r.Status == UnderwritingClearanceStatus.Blocked).ToList();
        if (blocked.Count == 0)
            throw new InvalidOperationException("Only blocked clearance results can be overridden.");

        var now = DateTime.UtcNow;
        foreach (var result in blocked)
        {
            result.IsOverridden = true;
            result.OverriddenById = overriddenById;
            result.OverriddenAt = now;
            result.OverrideReason = reason.Trim();
            result.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(ct);

        var dtos = results.Select(r => new UnderwritingClearanceResultDto
        {
            CheckType = r.CheckType,
            Status = r.Status,
            MatchedRecordId = r.MatchedRecordId,
            MatchedRecordLabel = r.MatchedRecordLabel,
            Explanation = r.Explanation,
            IsOverridden = r.IsOverridden,
            OverriddenById = r.OverriddenById,
            OverriddenAt = r.OverriddenAt,
            OverrideReason = r.OverrideReason,
        }).ToList();

        return new UnderwritingClearanceEvaluationDto
        {
            SubmissionId = submissionId,
            OverallStatus = ResolveOverallStatus(dtos),
            Results = dtos,
        };
    }

    private async Task SaveResultsAsync(
        Submission submission,
        Guid reviewerId,
        IReadOnlyCollection<UnderwritingClearanceResultDto> results,
        CancellationToken ct)
    {
        var existing = await _db.Set<UnderwritingClearanceResult>()
            .Where(r => r.SubmissionId == submission.Id)
            .ToListAsync(ct);
        var overrides = existing
            .Where(r => r.IsOverridden)
            .ToDictionary(r => r.CheckType);
        _db.RemoveRange(existing);

        foreach (var result in results)
        {
            overrides.TryGetValue(result.CheckType, out var previous);
            _db.Set<UnderwritingClearanceResult>().Add(new UnderwritingClearanceResult
            {
                SubmissionId = submission.Id,
                CheckType = result.CheckType,
                Status = result.Status,
                MatchedRecordId = result.MatchedRecordId,
                MatchedRecordLabel = result.MatchedRecordLabel,
                Explanation = result.Explanation,
                ReviewedById = reviewerId,
                ReviewedAt = DateTime.UtcNow,
                SnapshotJson = BuildSnapshotJson(submission),
                IsOverridden = previous != null,
                OverriddenById = previous?.OverriddenById,
                OverriddenAt = previous?.OverriddenAt,
                OverrideReason = previous?.OverrideReason,
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task<Submission?> FindDuplicateSubmissionAsync(
        Submission submission,
        IReadOnlySet<PolicyLineOfBusiness> lobs,
        CancellationToken ct)
    {
        if (!submission.EffectiveDate.HasValue)
            return null;

        var windowStart = submission.EffectiveDate.Value.AddDays(-30);
        var windowEnd = submission.EffectiveDate.Value.AddDays(30);

        var candidates = await _db.Set<Submission>()
            .Where(s =>
                s.Id != submission.Id &&
                s.InsuredId == submission.InsuredId &&
                s.EffectiveDate.HasValue &&
                s.EffectiveDate.Value >= windowStart &&
                s.EffectiveDate.Value <= windowEnd &&
                OpenSubmissionStatuses.Contains(s.Status))
            .OrderBy(s => s.EffectiveDate)
            .ToListAsync(ct);

        return candidates.FirstOrDefault(s => HasLobOverlap(lobs, ParseLinesOfBusiness(s.LinesOfBusiness)));
    }

    private async Task<Policy?> FindActivePolicyOverlapAsync(
        Submission submission,
        IReadOnlySet<PolicyLineOfBusiness> lobs,
        CancellationToken ct)
    {
        if (!submission.EffectiveDate.HasValue || !submission.ExpirationDate.HasValue)
            return null;

        var candidates = await _db.Set<Policy>()
            .Include(p => p.Submission)
            .Where(p =>
                p.Submission.InsuredId == submission.InsuredId &&
                p.Status == PolicyStatus.Active &&
                p.EffectiveDate < submission.ExpirationDate.Value &&
                p.ExpirationDate > submission.EffectiveDate.Value)
            .OrderBy(p => p.EffectiveDate)
            .ToListAsync(ct);

        return candidates.FirstOrDefault(p => lobs.Count == 0 || lobs.Contains(p.LineOfBusiness));
    }

    private static UnderwritingClearanceStatus ResolveOverallStatus(
        IReadOnlyCollection<UnderwritingClearanceResultDto> results)
    {
        if (results.Any(r => r.Status == UnderwritingClearanceStatus.Blocked && !r.IsOverridden))
            return UnderwritingClearanceStatus.Blocked;

        if (results.Any(r => r.Status == UnderwritingClearanceStatus.Warning))
            return UnderwritingClearanceStatus.Warning;

        if (results.Any(r => r.Status == UnderwritingClearanceStatus.Blocked && r.IsOverridden))
            return UnderwritingClearanceStatus.Warning;

        return UnderwritingClearanceStatus.Clear;
    }

    private static bool HasLobOverlap(
        IReadOnlySet<PolicyLineOfBusiness> target,
        IReadOnlySet<PolicyLineOfBusiness> candidate)
    {
        if (target.Count == 0 || candidate.Count == 0)
            return true;

        return target.Overlaps(candidate);
    }

    private static string BuildSnapshotJson(Submission submission)
        => JsonSerializer.Serialize(new
        {
            submission.SubmissionNumber,
            submission.InsuredId,
            submission.EffectiveDate,
            submission.ExpirationDate,
            submission.LinesOfBusiness,
            submission.Status,
        });

    private static IReadOnlySet<PolicyLineOfBusiness> ParseLinesOfBusiness(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new HashSet<PolicyLineOfBusiness>();

        try
        {
            var names = JsonSerializer.Deserialize<string[]>(value) ?? [];
            return names
                .Select(name => Enum.TryParse<PolicyLineOfBusiness>(name, out var lob) ? lob : (PolicyLineOfBusiness?)null)
                .Where(lob => lob.HasValue)
                .Select(lob => lob!.Value)
                .ToHashSet();
        }
        catch (JsonException)
        {
            return new HashSet<PolicyLineOfBusiness>();
        }
    }
}
