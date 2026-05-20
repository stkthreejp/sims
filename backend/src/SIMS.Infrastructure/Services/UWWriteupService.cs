using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SIMS.Application.DTOs.UWWriteup;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;
using SIMS.Infrastructure.Data;

namespace SIMS.Infrastructure.Services;

public class UWWriteupService : IUWWriteupService
{
    private readonly ApplicationDbContext _db;
    private readonly IUnderwritingReferralService _referrals;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public UWWriteupService(ApplicationDbContext db, IUnderwritingReferralService referrals)
    {
        _db = db;
        _referrals = referrals;
    }

    public async Task<UWWriteupDto> GetOrCreateAsync(Guid quoteId, Guid userId, CancellationToken ct = default)
    {
        var writeup = await _db.QuoteUWWriteups
            .Include(w => w.SubmittedBy)
            .Include(w => w.ApprovedBy)
            .Include(w => w.Conditions.Where(c => !c.IsDeleted))
            .FirstOrDefaultAsync(w => w.QuoteId == quoteId && !w.IsDeleted, ct);

        if (writeup is null)
        {
            writeup = new QuoteUWWriteup { QuoteId = quoteId };
            _db.QuoteUWWriteups.Add(writeup);
            await _db.SaveChangesAsync(ct);
        }

        return await BuildDtoAsync(writeup, quoteId, userId, ct);
    }

    public async Task<UWWriteupDto> SaveAsync(Guid quoteId, SaveWriteupDto dto, CancellationToken ct = default)
    {
        var writeup = await _db.QuoteUWWriteups
            .Include(w => w.Conditions)
            .FirstOrDefaultAsync(w => w.QuoteId == quoteId && !w.IsDeleted, ct)
            ?? throw new InvalidOperationException("Writeup not found.");

        if (writeup.Status != UWWriteupStatus.Draft)
            throw new InvalidOperationException("Only draft writeups can be edited.");

        writeup.PayloadJson = JsonSerializer.Serialize(dto.Payload, _json);
        writeup.UpdatedAt = DateTime.UtcNow;

        // Sync conditions — remove deleted, upsert existing/new
        var existingIds = writeup.Conditions.Select(c => c.Id).ToHashSet();
        var incomingIds = dto.Conditions.Where(c => c.Id.HasValue).Select(c => c.Id!.Value).ToHashSet();

        foreach (var c in writeup.Conditions.Where(c => !incomingIds.Contains(c.Id)))
            _db.QuoteUWWriteupConditions.Remove(c);

        foreach (var incoming in dto.Conditions)
        {
            if (incoming.Id.HasValue && existingIds.Contains(incoming.Id.Value))
            {
                var existing = writeup.Conditions.First(c => c.Id == incoming.Id.Value);
                existing.Text = incoming.Text;
                existing.Required = incoming.Required;
                existing.Satisfied = incoming.Satisfied;
                existing.SortOrder = incoming.SortOrder;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _db.QuoteUWWriteupConditions.Add(new QuoteUWWriteupCondition
                {
                    WriteupId = writeup.Id,
                    Text = incoming.Text,
                    Required = incoming.Required,
                    Satisfied = incoming.Satisfied,
                    SortOrder = incoming.SortOrder,
                });
            }
        }

        await _db.SaveChangesAsync(ct);

        // Reload for fresh nav props
        await _db.Entry(writeup).Collection(w => w.Conditions).LoadAsync(ct);
        return await BuildDtoAsync(writeup, quoteId, Guid.Empty, ct);
    }

    public async Task<UWWriteupDto> SubmitAsync(Guid quoteId, SubmitWriteupDto dto, Guid userId, CancellationToken ct = default)
    {
        var writeup = await _db.QuoteUWWriteups
            .Include(w => w.Conditions)
            .FirstOrDefaultAsync(w => w.QuoteId == quoteId && !w.IsDeleted, ct)
            ?? throw new InvalidOperationException("Writeup not found.");

        if (writeup.Status != UWWriteupStatus.Draft)
            throw new InvalidOperationException("Already submitted.");

        if (!Enum.TryParse<UWWriteupDecision>(dto.Decision, out var decision))
            throw new InvalidOperationException($"Invalid decision: {dto.Decision}");

        // Snapshot auto-computed fields into payload so the audit record is self-contained
        var currentPayload = JsonSerializer.Deserialize<IMWriteupPayload>(writeup.PayloadJson, _json) ?? new();
        var equipmentSummary = await ComputeEquipmentSummaryAsync(quoteId, ct);
        currentPayload.ReferralPieceOver500k = equipmentSummary.LargestUnitTiv > 500_000m || currentPayload.ReferralPieceOver500k;
        currentPayload.ReferralTivOver2mil = equipmentSummary.TotalTiv > 2_000_000m || currentPayload.ReferralTivOver2mil;

        writeup.PayloadJson = JsonSerializer.Serialize(currentPayload, _json);
        writeup.Status = UWWriteupStatus.Submitted;
        writeup.Decision = decision;
        writeup.SubmittedAt = DateTime.UtcNow;
        writeup.SubmittedById = userId;
        writeup.UpdatedAt = DateTime.UtcNow;

        if (decision == UWWriteupDecision.Approve)
        {
            writeup.Status = UWWriteupStatus.Approved;
            writeup.ApprovedAt = DateTime.UtcNow;
            writeup.ApprovedById = userId;
        }

        await _db.SaveChangesAsync(ct);
        await _referrals.SyncFromWriteupAsync(quoteId, userId, currentPayload, ct);
        await _db.Entry(writeup).Reference(w => w.SubmittedBy).LoadAsync(ct);
        await _db.Entry(writeup).Reference(w => w.ApprovedBy).LoadAsync(ct);
        return await BuildDtoAsync(writeup, quoteId, userId, ct);
    }

    public async Task<UWWriteupDto> ApproveAsync(Guid quoteId, Guid userId, CancellationToken ct = default)
    {
        var writeup = await _db.QuoteUWWriteups
            .Include(w => w.Conditions)
            .FirstOrDefaultAsync(w => w.QuoteId == quoteId && !w.IsDeleted, ct)
            ?? throw new InvalidOperationException("Writeup not found.");

        if (writeup.Status != UWWriteupStatus.Submitted)
            throw new InvalidOperationException("Only submitted writeups can be approved.");

        writeup.Status = UWWriteupStatus.Approved;
        writeup.ApprovedAt = DateTime.UtcNow;
        writeup.ApprovedById = userId;
        writeup.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _db.Entry(writeup).Reference(w => w.ApprovedBy).LoadAsync(ct);
        return await BuildDtoAsync(writeup, quoteId, userId, ct);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task<UWWriteupDto> BuildDtoAsync(QuoteUWWriteup writeup, Guid quoteId, Guid userId, CancellationToken ct)
    {
        var quote = await _db.Quotes
            .Include(q => q.Submission)
                .ThenInclude(s => s.Insured)
            .Include(q => q.Submission)
                .ThenInclude(s => s.Agent)
            .Include(q => q.Submission)
                .ThenInclude(s => s.Underwriter)
            .Include(q => q.Submission)
                .ThenInclude(s => s.AssistantUW)
            .Include(q => q.Submission)
                .ThenInclude(s => s.PriorCarriers)
            .Include(q => q.Submission)
                .ThenInclude(s => s.Equipment)
                    .ThenInclude(e => e.EquipmentType)
            .FirstOrDefaultAsync(q => q.Id == quoteId, ct)
            ?? throw new InvalidOperationException("Quote not found.");

        var sub = quote.Submission;
        var insured = sub.Insured;
        var payload = JsonSerializer.Deserialize<IMWriteupPayload>(writeup.PayloadJson, _json) ?? new();
        var equipment = await ComputeEquipmentSummaryAsync(quoteId, ct);

        // Determine New vs Renewal — has a prior bound quote on same insured+LOB?
        var hasPriorPolicy = await _db.Quotes
            .AnyAsync(q => q.Submission.InsuredId == insured.Id
                        && q.LineOfBusiness == quote.LineOfBusiness
                        && q.Status == QuoteStatus.Bound
                        && q.Id != quoteId, ct);

        var address = string.Join(", ", new[]
        {
            insured.AddressLine1,
            insured.AddressLine2,
            insured.City,
            insured.State,
            insured.ZipCode,
        }.Where(s => !string.IsNullOrWhiteSpace(s)));

        return new UWWriteupDto
        {
            Id = writeup.Id,
            QuoteId = quoteId,
            Status = writeup.Status.ToString(),
            Decision = writeup.Decision?.ToString(),
            SchemaVersion = writeup.SchemaVersion,
            SubmittedAt = writeup.SubmittedAt,
            SubmittedByName = writeup.SubmittedBy is null ? null
                : $"{writeup.SubmittedBy.FirstName} {writeup.SubmittedBy.LastName}".Trim(),
            ApprovedAt = writeup.ApprovedAt,
            ApprovedByName = writeup.ApprovedBy is null ? null
                : $"{writeup.ApprovedBy.FirstName} {writeup.ApprovedBy.LastName}".Trim(),

            UWName = $"{sub.Underwriter.FirstName} {sub.Underwriter.LastName}".Trim(),
            AssistantUWName = sub.AssistantUW is null ? null
                : $"{sub.AssistantUW.FirstName} {sub.AssistantUW.LastName}".Trim(),
            AgentName = sub.Agent?.Name ?? string.Empty,
            InsuredName = insured.DisplayName,
            Lob = quote.LineOfBusiness.ToString(),
            PolicyType = hasPriorPolicy ? "Renewal" : "New",
            EffectiveDate = quote.EffectiveDate.ToString("MM/dd/yyyy"),
            OperationType = insured.OperationType,
            NewVenture = insured.YearsInBusiness.HasValue && insured.YearsInBusiness.Value < 1,
            YearsInBusiness = insured.YearsInBusiness,
            CreditScore = insured.CreditScore,
            Website = insured.Website,
            Address = address,
            PriorCarriers = sub.PriorCarriers.Select(p => new PriorCarrierSummaryDto
            {
                CarrierName = p.CarrierName,
                PolicyNumber = p.PolicyNumber,
                ExpirationDate = p.ExpirationDate?.ToString("MM/dd/yyyy"),
                PremiumAmount = p.Premium,
            }).ToList(),

            Equipment = equipment,
            AutoReferralPieceOver500k = equipment.LargestUnitTiv > 500_000m,
            AutoReferralTivOver2mil = equipment.TotalTiv > 2_000_000m,

            Payload = payload,
            Conditions = writeup.Conditions
                .OrderBy(c => c.SortOrder)
                .Select(c => new WriteupConditionDto
                {
                    Id = c.Id,
                    Text = c.Text,
                    Required = c.Required,
                    Satisfied = c.Satisfied,
                    SortOrder = c.SortOrder,
                }).ToList(),
        };
    }

    private async Task<EquipmentSummaryDto> ComputeEquipmentSummaryAsync(Guid quoteId, CancellationToken ct)
    {
        var items = await _db.SubmissionEquipment
            .Include(e => e.EquipmentType)
            .Where(e => e.Submission.Quotes.Any(q => q.Id == quoteId) && !e.IsDeleted)
            .ToListAsync(ct);

        static string Classify(string? name) => name?.ToLowerInvariant() switch
        {
            string n when n.Contains("cutter") || n.Contains("chain saw") || n.Contains("chainsaw") || n.Contains("saw") => "Cutter",
            string n when n.Contains("skidder") => "Skidder",
            string n when n.Contains("loader") => "Loader",
            string n when n.Contains("dozer") || n.Contains("bulldozer") => "Dozer",
            _ => "Other",
        };

        return new EquipmentSummaryDto
        {
            TotalTiv = items.Sum(e => e.Value ?? 0),
            LargestUnitTiv = items.Any() ? items.Max(e => e.Value ?? 0) : 0,
            TotalCount = items.Count,
            CountCutter = items.Count(e => Classify(e.EquipmentType?.Name) == "Cutter"),
            CountSkidder = items.Count(e => Classify(e.EquipmentType?.Name) == "Skidder"),
            CountLoader = items.Count(e => Classify(e.EquipmentType?.Name) == "Loader"),
            CountDozer = items.Count(e => Classify(e.EquipmentType?.Name) == "Dozer"),
            CountOther = items.Count(e => Classify(e.EquipmentType?.Name) == "Other"),
        };
    }
}
