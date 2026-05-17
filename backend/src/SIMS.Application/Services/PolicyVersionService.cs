using Microsoft.EntityFrameworkCore;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Entities.Rating;
using System.Text.Json;

namespace SIMS.Application.Services;

public class PolicyVersionService : IPolicyVersionService
{
    private readonly DbContext _db;

    public PolicyVersionService(DbContext db)
    {
        _db = db;
    }

    public async Task<PolicyVersion> EnsureCurrentVersionAsync(Policy policy, Guid userId, CancellationToken ct = default)
    {
        var current = await GetCurrentVersionAsync(policy.Id, ct);
        if (current != null)
            return current;

        return await CreatePolicyVersionAsync(policy, null, null, userId, ct);
    }

    public async Task<PolicyVersion> CreateVersionAsync(
        Policy policy,
        PolicyTransaction transaction,
        PolicyVersion? priorVersion,
        Guid userId,
        CancellationToken ct = default)
    {
        var version = await CreatePolicyVersionAsync(policy, transaction.Id, priorVersion, userId, ct);
        transaction.PriorPolicyVersionId = priorVersion?.Id;
        transaction.ResultingPolicyVersionId = version.Id;
        transaction.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return version;
    }

    private async Task<PolicyVersion> CreatePolicyVersionAsync(
        Policy policy,
        Guid? transactionId,
        PolicyVersion? priorVersion,
        Guid userId,
        CancellationToken ct)
    {
        var nextVersionNumber = priorVersion?.VersionNumber + 1 ?? 1;
        var ratingSnapshotId = await GetRatingSnapshotIdAsync(policy.BoundQuoteId, ct);
        var version = new PolicyVersion
        {
            PolicyId = policy.Id,
            VersionNumber = nextVersionNumber,
            CreatedByPolicyTransactionId = transactionId,
            PriorPolicyVersionId = priorVersion?.Id,
            EffectiveDate = policy.EffectiveDate,
            ExpirationDate = policy.ExpirationDate,
            Status = policy.Status,
            PremiumAmount = policy.PremiumAmount,
            TaxesAndFees = policy.TaxesAndFees,
            TotalPremium = policy.TotalPremium,
            CoverageSnapshotJson = await BuildCoverageSnapshotJsonAsync(policy, ct),
            ExposureSnapshotJson = await BuildExposureSnapshotJsonAsync(policy, ct),
            RatingSnapshotId = ratingSnapshotId,
            CreatedById = userId,
        };

        _db.Set<PolicyVersion>().Add(version);
        await _db.SaveChangesAsync(ct);
        return version;
    }

    private Task<PolicyVersion?> GetCurrentVersionAsync(Guid policyId, CancellationToken ct)
        => _db.Set<PolicyVersion>()
            .Where(v => v.PolicyId == policyId)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct);

    private Task<Guid?> GetRatingSnapshotIdAsync(Guid quoteId, CancellationToken ct)
        => _db.Set<QuoteRatingSnapshot>()
            .Where(s => s.QuoteId == quoteId && !s.IsDeleted)
            .OrderByDescending(s => s.IsBoundSnapshot)
            .ThenByDescending(s => s.RatedAt)
            .Select(s => (Guid?)s.Id)
            .FirstOrDefaultAsync(ct);

    private async Task<string> BuildCoverageSnapshotJsonAsync(Policy policy, CancellationToken ct)
    {
        var quote = policy.BoundQuote;
        if (quote == null)
        {
            quote = await _db.Set<Quote>()
                .AsNoTracking()
                .FirstOrDefaultAsync(q => q.Id == policy.BoundQuoteId, ct);
        }

        return JsonSerializer.Serialize(new
        {
            policy.LineOfBusiness,
            quote?.CoverageDescription,
            quote?.Deductible,
            quote?.Limit,
            quote?.UninsuredMotoristLimit,
            quote?.MedicalPaymentsLimit,
            CarrierCommissionRate = quote?.EffectiveCarrierRate,
            SMMRetentionRate = quote?.EffectiveSMMRate,
            AgentCommissionRate = quote?.EffectiveAgentRate,
        });
    }

    private async Task<string> BuildExposureSnapshotJsonAsync(Policy policy, CancellationToken ct)
    {
        var submissionId = policy.SubmissionId;
        var includedFormCount = await _db.Set<QuotePolicyFormSelection>()
            .CountAsync(f => f.QuoteId == policy.BoundQuoteId && f.IsIncluded && !f.IsDeleted, ct);

        return JsonSerializer.Serialize(new
        {
            LocationCount = await _db.Set<SubmissionLocation>().CountAsync(l => l.SubmissionId == submissionId && !l.IsDeleted, ct),
            DriverCount = await _db.Set<SubmissionDriver>().CountAsync(d => d.SubmissionId == submissionId && !d.IsDeleted, ct),
            VehicleCount = await _db.Set<SubmissionVehicle>().CountAsync(v => v.SubmissionId == submissionId && !v.IsDeleted, ct),
            EquipmentCount = await _db.Set<SubmissionEquipment>().CountAsync(e => e.SubmissionId == submissionId && !e.IsDeleted, ct),
            AdditionalInterestCount = await _db.Set<SubmissionAdditionalInterest>().CountAsync(i => i.SubmissionId == submissionId && !i.IsDeleted, ct),
            IncludedFormCount = includedFormCount,
        });
    }
}
