using Microsoft.EntityFrameworkCore;
using SIMS.Application.DTOs.UWWriteup;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;

namespace SIMS.Application.Services;

public class UnderwritingReferralService : IUnderwritingReferralService
{
    private static readonly AppetiteRule[] Rules =
    [
        new("ReferralLossRatioOver55", "Loss ratio over 55%", p => p.ReferralLossRatioOver55),
        new("ReferralPieceOver500k", "Single piece over 500k", p => p.ReferralPieceOver500k),
        new("ReferralTivOver2mil", "Total TIV over 2M", p => p.ReferralTivOver2mil),
        new("ReferralLossOver400k", "Loss over 400k", p => p.ReferralLossOver400k),
        new("ReferralRateReduction", "Rate reduction", p => p.ReferralRateReduction),
        new("ReferralLossOver50k", "Loss over 50k", p => p.ReferralLossOver50k),
        new("ReferralFmcsaConditional", "Conditional FMCSA rating", p => p.ReferralFmcsaConditional),
        new("ReferralBasicOverThreshold", "BASIC over threshold", p => p.ReferralBasicOverThreshold),
        new("ReferralScheduleCreditOver20", "Schedule credit over 20%", p => p.ReferralScheduleCreditOver20),
        new("ReferralPremiumOver100k", "Premium over 100k", p => p.ReferralPremiumOver100k),
        new("ReferralOwnerOperatorOver30", "Owner-operator over 30%", p => p.ReferralOwnerOperatorOver30),
        new("ReferralUnitOverCap", "Unit over cap", p => p.ReferralUnitOverCap),
        new("ReferralPowerUnitsOrPremium", "Power units or premium threshold", p => p.ReferralPowerUnitsOrPremium),
        new("ReferralTivLocationThreshold", "Location TIV threshold", p => p.ReferralTivLocationThreshold),
        new("ReferralTornadoHail", "Tornado or hail exposure", p => p.ReferralTornadoHail),
        new("ReferralCoastalApd", "Coastal APD exposure", p => p.ReferralCoastalApd),
        new("ReferralCreditScoreLow", "Low credit score", p => p.ReferralCreditScoreLow),
        new("ReferralGlUwCreditOver20", "GL UW credit over 20%", p => p.ReferralGlUwCreditOver20),
        new("ReferralGlRevenueBelowThreshold", "GL revenue below threshold", p => p.ReferralGlRevenueBelowThreshold),
        new("ReferralSawmillOps", "Sawmill operations", p => p.ReferralSawmillOps),
        new("ReferralResidentialWork", "Residential work", p => p.ReferralResidentialWork),
        new("ReferralBurningExposure", "Burning exposure", p => p.ReferralBurningExposure),
        new("ReferralPayrollChangeOver25", "Payroll change over 25%", p => p.ReferralPayrollChangeOver25),
        new("ReferralSubcontractorControls", "Subcontractor controls", p => p.ReferralSubcontractorControls),
    ];

    private readonly DbContext _db;

    public UnderwritingReferralService(DbContext db)
    {
        _db = db;
    }

    public async Task SyncFromWriteupAsync(
        Guid quoteId,
        Guid userId,
        IMWriteupPayload payload,
        CancellationToken ct = default)
    {
        var quote = await _db.Set<Quote>()
            .Where(q => q.Id == quoteId)
            .Select(q => new { q.Id, q.SubmissionId })
            .FirstOrDefaultAsync(ct);

        if (quote == null)
            throw new InvalidOperationException("Quote not found.");

        var triggeredRules = Rules.Where(rule => rule.IsTriggered(payload)).ToList();
        var existingResults = await _db.Set<UnderwritingAppetiteResult>()
            .Where(r => r.QuoteId == quoteId)
            .ToListAsync(ct);
        _db.RemoveRange(existingResults);

        foreach (var rule in triggeredRules)
        {
            _db.Set<UnderwritingAppetiteResult>().Add(new UnderwritingAppetiteResult
            {
                SubmissionId = quote.SubmissionId,
                QuoteId = quote.Id,
                RuleCode = rule.Code,
                RuleName = rule.Name,
                Triggered = true,
                ReferralRequired = true,
                Explanation = $"{rule.Name} requires underwriting referral.",
                EvaluatedById = userId,
                EvaluatedAt = DateTime.UtcNow,
            });
        }

        var existingOpenReferrals = await _db.Set<UnderwritingReferral>()
            .Where(r => r.QuoteId == quoteId && r.Status == UnderwritingReferralStatus.Open)
            .ToListAsync(ct);
        var triggeredCodes = triggeredRules.Select(r => r.Code).ToHashSet(StringComparer.Ordinal);

        foreach (var obsolete in existingOpenReferrals.Where(r => !triggeredCodes.Contains(r.ReferralType)))
        {
            obsolete.IsDeleted = true;
            obsolete.DeletedAt = DateTime.UtcNow;
            obsolete.UpdatedAt = DateTime.UtcNow;
        }

        foreach (var rule in triggeredRules)
        {
            if (existingOpenReferrals.Any(r => r.ReferralType == rule.Code))
                continue;

            _db.Set<UnderwritingReferral>().Add(new UnderwritingReferral
            {
                SubmissionId = quote.SubmissionId,
                QuoteId = quote.Id,
                ReferralType = rule.Code,
                Status = UnderwritingReferralStatus.Open,
                Required = true,
                Reason = $"{rule.Name} requires underwriting referral.",
                RequestedById = userId,
                RequestedAt = DateTime.UtcNow,
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    public Task<bool> HasOpenRequiredReferralsAsync(Guid submissionId, CancellationToken ct = default)
        => _db.Set<UnderwritingReferral>().AnyAsync(r =>
            r.SubmissionId == submissionId &&
            r.Required &&
            r.Status == UnderwritingReferralStatus.Open,
            ct);

    private sealed record AppetiteRule(string Code, string Name, Func<IMWriteupPayload, bool> IsTriggered);
}
