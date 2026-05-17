using Microsoft.EntityFrameworkCore;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Entities.Rating;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SIMS.Application.Services;

public class PolicyVersionService : IPolicyVersionService
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

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
        }, SnapshotJsonOptions);
    }

    private async Task<string> BuildExposureSnapshotJsonAsync(Policy policy, CancellationToken ct)
    {
        var submissionId = policy.SubmissionId;
        var locations = await _db.Set<SubmissionLocation>()
            .Where(l => l.SubmissionId == submissionId && !l.IsDeleted)
            .OrderBy(l => l.LocationNumber)
            .Select(l => new
            {
                l.Id,
                l.LocationNumber,
                l.Address,
                l.ZipCode,
            })
            .ToListAsync(ct);

        var drivers = await _db.Set<SubmissionDriver>()
            .Where(d => d.SubmissionId == submissionId && !d.IsDeleted)
            .OrderBy(d => d.DriverNumber)
            .Select(d => new
            {
                d.Id,
                d.DriverNumber,
                d.Name,
                d.DateOfBirth,
                d.LicenseNumber,
                d.LicenseState,
                d.DateHired,
            })
            .ToListAsync(ct);

        var vehicles = await _db.Set<SubmissionVehicle>()
            .Where(v => v.SubmissionId == submissionId && !v.IsDeleted)
            .OrderBy(v => v.UnitNumber)
            .Select(v => new
            {
                v.Id,
                v.UnitNumber,
                v.Year,
                v.Make,
                v.Model,
                v.Vin,
                v.Gvw,
                v.VehicleClass,
                v.GaragingZip,
                v.Radius,
                v.ApdVehicleClass,
                v.ApdRoadType,
                v.ApdAnnualMiles,
                v.ApdOperationCode,
                v.ApdState,
                v.ApdStatedValue,
                v.ApdCompDeductible,
                v.ApdCollDeductible,
            })
            .ToListAsync(ct);

        var equipment = await _db.Set<SubmissionEquipment>()
            .Where(e => e.SubmissionId == submissionId && !e.IsDeleted)
            .OrderBy(e => e.ItemNumber)
            .Select(e => new
            {
                e.Id,
                e.ItemNumber,
                e.Year,
                e.Make,
                e.Model,
                e.Description,
                e.SerialNumber,
                e.Value,
                e.EquipmentTypeId,
                e.TerritoryCode,
                e.Deductible,
                e.SettlementBasis,
            })
            .ToListAsync(ct);

        var additionalInterests = await _db.Set<SubmissionAdditionalInterest>()
            .Where(i => i.SubmissionId == submissionId && !i.IsDeleted)
            .OrderBy(i => i.Name)
            .Select(i => new
            {
                i.Id,
                i.LineOfBusiness,
                i.Name,
                i.AddressLine1,
                i.AddressLine2,
                i.City,
                i.State,
                i.ZipCode,
                i.Email,
                i.Phone,
                i.AppliesToType,
                i.ScheduledItemNumbers,
                i.AdditionalInsured,
                i.LossPayee,
                i.WaiverOfSubrogation,
                i.PrimaryNonContributory,
                i.Notes,
            })
            .ToListAsync(ct);

        var blanketAdditionalInterests = await _db.Set<SubmissionAdditionalInterestBlanket>()
            .Where(i => i.SubmissionId == submissionId && !i.IsDeleted)
            .OrderBy(i => i.LineOfBusiness)
            .Select(i => new
            {
                i.Id,
                i.LineOfBusiness,
                i.AdditionalInsured,
                i.WaiverOfSubrogation,
                i.PrimaryNonContributory,
            })
            .ToListAsync(ct);

        var policyForms = await _db.Set<QuotePolicyFormSelection>()
            .Where(f => f.QuoteId == policy.BoundQuoteId && f.IsIncluded && !f.IsDeleted)
            .OrderBy(f => f.SequenceOrder)
            .Select(f => new
            {
                f.Id,
                f.SequenceOrder,
                f.FormType,
                f.IsSystemGenerated,
                f.PolicyFormTemplate.FormNumber,
                FormName = f.PolicyFormTemplate.Name,
                f.PolicyFormTemplate.EditionDate,
                f.PolicyFormTemplate.DocumentType,
                f.PolicyFormTemplate.FileName,
            })
            .ToListAsync(ct);

        return JsonSerializer.Serialize(new
        {
            LocationCount = locations.Count,
            DriverCount = drivers.Count,
            VehicleCount = vehicles.Count,
            EquipmentCount = equipment.Count,
            AdditionalInterestCount = additionalInterests.Count,
            BlanketAdditionalInterestCount = blanketAdditionalInterests.Count,
            IncludedFormCount = policyForms.Count,
            Locations = locations,
            Drivers = drivers,
            Vehicles = vehicles,
            Equipment = equipment,
            AdditionalInterests = additionalInterests,
            BlanketAdditionalInterests = blanketAdditionalInterests,
            PolicyForms = policyForms,
        }, SnapshotJsonOptions);
    }
}
