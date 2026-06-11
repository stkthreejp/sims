using Microsoft.EntityFrameworkCore;
using SIMS.Application.Common;
using SIMS.Application.DTOs.Claims;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Security;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;

namespace SIMS.Application.Services;

public class ClaimsService : IClaimsService
{
    // Backstop against runaway/malicious import payloads; real feeds are far smaller.
    public const int MaxImportRows = 20_000;

    private readonly DbContext _db;

    public ClaimsService(DbContext db) => _db = db;

    public async Task<IReadOnlyList<ClaimListItemDto>> GetClaimsAsync(
        UserAccessScope access,
        Guid? policyId = null,
        Guid? insuredId = null,
        ClaimStatus? status = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken ct = default)
    {
        var query = _db.Set<Claim>().AsNoTracking()
            .Where(c => !c.IsDeleted)
            .ForAccessScope(access);

        if (policyId.HasValue) query = query.Where(c => c.PolicyId == policyId.Value);
        if (insuredId.HasValue) query = query.Where(c => c.InsuredId == insuredId.Value);
        if (status.HasValue) query = query.Where(c => c.Status == status.Value);
        if (fromDate.HasValue) query = query.Where(c => c.DateOfLoss >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(c => c.DateOfLoss <= toDate.Value);

        return await query
            .OrderByDescending(c => c.DateOfLoss)
            .Select(c => ToListItem(c))
            .ToListAsync(ct);
    }

    public async Task<Result<ClaimDto>> GetClaimAsync(Guid id, UserAccessScope access, CancellationToken ct = default)
    {
        var claim = await _db.Set<Claim>().AsNoTracking()
            .Where(c => c.Id == id && !c.IsDeleted)
            .ForAccessScope(access)
            .FirstOrDefaultAsync(ct);
        if (claim is null)
            return Result<ClaimDto>.Failure("CLAIM_NOT_FOUND", $"Claim {id} not found.");
        return Result<ClaimDto>.Success(ToDto(claim));
    }

    public async Task<Result<ClaimDto>> CreateClaimAsync(UpsertClaimRequest request, Guid createdById, UserAccessScope access, CancellationToken ct = default)
    {
        // Resolve policy/insured names when a PolicyId is provided
        string? policyNumber = null;
        Guid? insuredId = null;
        string? insuredName = null;

        if (request.PolicyId.HasValue)
        {
            var policy = await _db.Set<Policy>().AsNoTracking()
                .Include(p => p.Submission).ThenInclude(s => s.Insured)
                .Where(p => p.Id == request.PolicyId.Value && !p.IsDeleted)
                .ForAccessScope(access)
                .FirstOrDefaultAsync(ct);
            if (policy is null)
                return Result<ClaimDto>.Failure("POLICY_NOT_FOUND", $"Policy {request.PolicyId} not found.");
            policyNumber = policy.PolicyNumber;
            insuredId = policy.Submission.InsuredId;
            insuredName = policy.Submission.Insured.DisplayName;
        }
        else if (!access.CanAccessAllBusinessData)
        {
            // Unlinked claims are outside the per-user ownership model.
            return Result<ClaimDto>.Failure(BusinessDataAccess.AccessDeniedCode,
                "Claims without a linked policy require full business-data access.");
        }

        // Unique check: SourcePolicyReference + ClaimNumber
        var srcRef = request.SourcePolicyReference ?? policyNumber ?? string.Empty;
        var dupe = await _db.Set<Claim>()
            .AnyAsync(c => c.SourcePolicyReference == srcRef && c.ClaimNumber == request.ClaimNumber && !c.IsDeleted, ct);
        if (dupe)
            return Result<ClaimDto>.Failure("DUPLICATE_CLAIM", $"Claim {request.ClaimNumber} already exists for policy reference '{srcRef}'.");

        var claim = new Claim
        {
            PolicyId = request.PolicyId,
            PolicyNumber = policyNumber ?? request.SourcePolicyReference,
            InsuredId = insuredId,
            InsuredName = insuredName,
            ClaimNumber = request.ClaimNumber,
            CarrierClaimNumber = request.CarrierClaimNumber,
            SourcePolicyReference = srcRef,
            Account = request.Account,
            CarrierName = request.CarrierName,
            DateOfLoss = request.DateOfLoss,
            ReportDate = request.ReportDate,
            ClosedDate = request.ClosedDate,
            Status = request.Status,
            CoverageType = request.CoverageType,
            ClaimTypeDesc = request.ClaimTypeDesc,
            LossCause = request.LossCause,
            Description = request.Description,
            RiskState = request.RiskState,
            AccidentState = request.AccidentState,
            ClaimantName = request.ClaimantName,
            AdjusterName = request.AdjusterName,
            TpaName = request.TpaName,
            TpaClaimNumber = request.TpaClaimNumber,
            Paid = request.Paid,
            Reserved = request.Reserved,
            Expense = request.Expense,
            Recovery = request.Recovery,
            Incurred = request.Paid + request.Reserved + request.Expense,
            LastValuationDate = request.LastValuationDate,
            IsManualEntry = true,
            Notes = request.Notes,
            UpdatedById = createdById,
        };

        _db.Set<Claim>().Add(claim);
        UpsertValuation(claim, claim.LastValuationDate, importBatchId: null);
        await _db.SaveChangesAsync(ct);
        return Result<ClaimDto>.Success(ToDto(claim));
    }

    public async Task<Result<ClaimDto>> UpdateClaimAsync(Guid id, UpsertClaimRequest request, Guid updatedById, UserAccessScope access, CancellationToken ct = default)
    {
        var claim = await _db.Set<Claim>()
            .Include(c => c.Valuations)
            .Where(c => c.Id == id && !c.IsDeleted)
            .ForAccessScope(access)
            .FirstOrDefaultAsync(ct);
        if (claim is null)
            return Result<ClaimDto>.Failure("CLAIM_NOT_FOUND", $"Claim {id} not found.");

        claim.CarrierClaimNumber = request.CarrierClaimNumber;
        claim.Account = request.Account;
        claim.CarrierName = request.CarrierName;
        claim.DateOfLoss = request.DateOfLoss;
        claim.ReportDate = request.ReportDate;
        claim.ClosedDate = request.ClosedDate;
        claim.Status = request.Status;
        claim.CoverageType = request.CoverageType;
        claim.ClaimTypeDesc = request.ClaimTypeDesc;
        claim.LossCause = request.LossCause;
        claim.Description = request.Description;
        claim.RiskState = request.RiskState;
        claim.AccidentState = request.AccidentState;
        claim.ClaimantName = request.ClaimantName;
        claim.AdjusterName = request.AdjusterName;
        claim.TpaName = request.TpaName;
        claim.TpaClaimNumber = request.TpaClaimNumber;
        claim.Notes = request.Notes;

        // Financials on imported claims are owned by the carrier/TPA feed;
        // manual edits may only touch descriptive fields.
        if (claim.IsManualEntry)
        {
            claim.Paid = request.Paid;
            claim.Reserved = request.Reserved;
            claim.Expense = request.Expense;
            claim.Recovery = request.Recovery;
            claim.Incurred = request.Paid + request.Reserved + request.Expense;
            claim.LastValuationDate = request.LastValuationDate;
            UpsertValuation(claim, claim.LastValuationDate, importBatchId: null);
        }

        claim.UpdatedById = updatedById;
        claim.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Result<ClaimDto>.Success(ToDto(claim));
    }

    public async Task<Result<ClaimImportBatchDto>> ImportClaimsAsync(ImportClaimsRequest request, Guid importedById, CancellationToken ct = default)
    {
        if (request.Rows.Count > MaxImportRows)
            return Result<ClaimImportBatchDto>.Failure("IMPORT_TOO_LARGE",
                $"Import contains {request.Rows.Count} rows; the maximum per batch is {MaxImportRows}. Split the file and retry.");

        var batch = new ClaimImportBatch
        {
            FileName = request.FileName,
            CarrierName = request.CarrierName,
            TpaName = request.TpaName,
            ValuationDate = request.ValuationDate,
            RecordCount = request.Rows.Count,
            Status = "Processing",
            ImportedById = importedById,
        };
        _db.Set<ClaimImportBatch>().Add(batch);
        await _db.SaveChangesAsync(ct);

        // Try to match policies by carrier policy number for linked claims
        var policyRefs = request.Rows
            .Where(r => !string.IsNullOrWhiteSpace(r.CarrierPolicyNum))
            .Select(r => r.CarrierPolicyNum!.Trim())
            .Distinct()
            .ToList();

        var policies = await _db.Set<Policy>().AsNoTracking()
            .Include(p => p.Submission).ThenInclude(s => s.Insured)
            .Where(p => policyRefs.Contains(p.PolicyNumber) && !p.IsDeleted)
            .ToListAsync(ct);
        var policyByNumber = policies.GroupBy(p => p.PolicyNumber)
            .ToDictionary(g => g.Key, g => g.First());

        // One batched lookup for all existing claims this file could touch
        var srcRefs = request.Rows
            .Select(r => r.CarrierPolicyNum?.Trim() ?? string.Empty)
            .Distinct()
            .ToList();
        var existingClaims = await _db.Set<Claim>()
            .Include(c => c.Valuations)
            .Where(c => srcRefs.Contains(c.SourcePolicyReference!) && !c.IsDeleted)
            .ToListAsync(ct);
        var existingByKey = existingClaims
            .GroupBy(c => (c.SourcePolicyReference ?? string.Empty, c.ClaimNumber))
            .ToDictionary(g => g.Key, g => g.First());

        var errors = new List<string>();
        int created = 0, updated = 0, skipped = 0;

        for (int i = 0; i < request.Rows.Count; i++)
        {
            var row = request.Rows[i];
            var rowNum = i + 1;

            if (string.IsNullOrWhiteSpace(row.ClaimNumber))
            {
                errors.Add($"Row {rowNum}: missing ClaimNumber");
                skipped++;
                continue;
            }

            if (!DateOnly.TryParse(row.DateOfClaim, out var dateOfLoss))
            {
                errors.Add($"Row {rowNum}: invalid DateOfClaim '{row.DateOfClaim}'");
                skipped++;
                continue;
            }

            var reportDate = DateOnly.TryParse(row.DateReported, out var rd) ? rd : dateOfLoss;
            var valuationDate = DateOnly.TryParse(row.ValueDate, out var vd) ? vd : request.ValuationDate;

            var rawStatus = row.ClaimStatusDesc?.Trim().ToLowerInvariant();
            var status = rawStatus switch
            {
                "closed" => ClaimStatus.Closed,
                "open" => ClaimStatus.Open,
                "denied" => ClaimStatus.Denied,
                "subrogation" => ClaimStatus.Subrogation,
                "withdrawn" => ClaimStatus.Withdrawn,
                _ => ClaimStatus.Open,
            };
            var closedDate = status == ClaimStatus.Closed ? (DateOnly?)valuationDate : null;

            var srcRef = row.CarrierPolicyNum?.Trim() ?? string.Empty;
            var claimNum = row.ClaimNumber.Trim();

            policyByNumber.TryGetValue(srcRef, out var matchedPolicy);

            // Loss-run column semantics: Paid = loss paid, Reserved = loss O/S,
            // Expense = ALAE (paid + O/S). The old mapping folded expense into
            // paid/reserved and hardcoded Expense = 0.
            var paid = row.TotalLossPaid ?? 0m;
            var reserved = row.TotalOsLoss ?? 0m;
            var expense = (row.TotalExpPaid ?? 0m) + (row.TotalOsExp ?? 0m);
            var recovery = row.TotalRecovery ?? 0m;
            var incurred = row.TotalIncurred ?? (paid + reserved + expense);

            existingByKey.TryGetValue((srcRef, claimNum), out var existing);

            if (existing is null)
            {
                var claim = new Claim
                {
                    PolicyId = matchedPolicy?.Id,
                    PolicyNumber = matchedPolicy?.PolicyNumber,
                    InsuredId = matchedPolicy?.Submission?.InsuredId,
                    InsuredName = matchedPolicy?.Submission?.Insured?.DisplayName ?? row.NamedInsured,
                    ClaimNumber = claimNum,
                    SourcePolicyReference = srcRef,
                    Account = row.Account,
                    CarrierName = row.CarrierName ?? request.CarrierName,
                    DateOfLoss = dateOfLoss,
                    ReportDate = reportDate,
                    ClosedDate = closedDate,
                    Status = status,
                    CoverageType = row.Lob,
                    ClaimTypeDesc = row.ClaimTypeDesc,
                    LossCause = row.AccidentCauseDesc,
                    Description = row.AccidentDescription,
                    RiskState = row.RiskState,
                    AccidentState = row.AccidentState,
                    ClaimantName = row.ClaimantName,
                    AdjusterName = row.AdjusterName,
                    TpaName = request.TpaName,
                    Paid = paid,
                    Reserved = reserved,
                    Expense = expense,
                    Recovery = recovery,
                    Incurred = incurred,
                    LastValuationDate = valuationDate,
                    ImportBatchId = batch.Id,
                    IsManualEntry = false,
                };
                _db.Set<Claim>().Add(claim);
                UpsertValuation(claim, valuationDate, batch.Id);
                existingByKey[(srcRef, claimNum)] = claim;
                created++;
            }
            else if (existing.IsManualEntry)
            {
                // Never let a feed silently clobber a manually entered claim.
                errors.Add($"Row {rowNum}: claim {claimNum} for '{srcRef}' exists as a manual entry; resolve manually");
                skipped++;
            }
            else
            {
                // Snapshot is always recorded; current values only move forward —
                // an older file must not regress newer valuations.
                UpsertValuation(existing, valuationDate, batch.Id, status, paid, reserved, expense, recovery, incurred);

                if (valuationDate >= existing.LastValuationDate)
                {
                    existing.Status = status;
                    existing.ClosedDate = closedDate;
                    existing.AdjusterName = row.AdjusterName ?? existing.AdjusterName;
                    existing.ClaimTypeDesc = row.ClaimTypeDesc ?? existing.ClaimTypeDesc;
                    existing.LossCause = row.AccidentCauseDesc ?? existing.LossCause;
                    existing.Description = row.AccidentDescription ?? existing.Description;
                    existing.Paid = paid;
                    existing.Reserved = reserved;
                    existing.Expense = expense;
                    existing.Recovery = recovery;
                    existing.Incurred = incurred;
                    existing.LastValuationDate = valuationDate;
                    existing.ImportBatchId = batch.Id;
                    if (matchedPolicy is not null && existing.PolicyId is null)
                    {
                        existing.PolicyId = matchedPolicy.Id;
                        existing.PolicyNumber = matchedPolicy.PolicyNumber;
                        existing.InsuredId = matchedPolicy.Submission?.InsuredId;
                        existing.InsuredName = matchedPolicy.Submission?.Insured?.DisplayName;
                    }
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                updated++;
            }
        }

        batch.CreatedCount = created;
        batch.UpdatedCount = updated;
        batch.SkippedCount = skipped;
        batch.ErrorCount = errors.Count;
        batch.Status = errors.Count > 0 && created + updated == 0 ? "Failed" : "Complete";
        if (errors.Count > 0)
            batch.ErrorSummaryJson = System.Text.Json.JsonSerializer.Serialize(errors.Take(100).ToList());

        await _db.SaveChangesAsync(ct);

        var importedBy = await _db.Set<User>().AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == importedById, ct);

        return Result<ClaimImportBatchDto>.Success(ToBatchDto(batch, importedBy?.UserName ?? string.Empty));
    }

    public async Task<IReadOnlyList<ClaimImportBatchDto>> GetImportBatchesAsync(CancellationToken ct = default)
    {
        return await _db.Set<ClaimImportBatch>().AsNoTracking()
            .Include(b => b.ImportedBy)
            .Where(b => !b.IsDeleted)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => ToBatchDto(b, b.ImportedBy.UserName ?? string.Empty))
            .ToListAsync(ct);
    }

    public async Task<Result<LossRunDto>> GetLossRunAsync(Guid? insuredId, Guid? policyId, DateOnly asOfDate, UserAccessScope access, CancellationToken ct = default)
    {
        if (!insuredId.HasValue && !policyId.HasValue)
            return Result<LossRunDto>.Failure("MISSING_FILTER", "insuredId or policyId is required.");

        // The requested target must itself be accessible — a foreign id is a
        // scope violation, not an empty result.
        if (policyId.HasValue)
        {
            var policyVisible = await _db.Set<Policy>().AsNoTracking()
                .Where(p => p.Id == policyId.Value && !p.IsDeleted)
                .ForAccessScope(access)
                .AnyAsync(ct);
            if (!policyVisible)
                return Result<LossRunDto>.Failure(BusinessDataAccess.AccessDeniedCode, BusinessDataAccess.AccessDeniedMessage);
        }
        else if (insuredId.HasValue && !access.CanAccessAllBusinessData)
        {
            var insuredVisible = await _db.Set<Policy>().AsNoTracking()
                .Where(p => p.Submission.InsuredId == insuredId.Value && !p.IsDeleted)
                .ForAccessScope(access)
                .AnyAsync(ct);
            if (!insuredVisible)
                return Result<LossRunDto>.Failure(BusinessDataAccess.AccessDeniedCode, BusinessDataAccess.AccessDeniedMessage);
        }

        // Claims known as of the valuation date
        var query = _db.Set<Claim>().AsNoTracking()
            .Where(c => !c.IsDeleted && c.DateOfLoss <= asOfDate && c.ReportDate <= asOfDate)
            .ForAccessScope(access);
        if (insuredId.HasValue) query = query.Where(c => c.InsuredId == insuredId.Value);
        if (policyId.HasValue) query = query.Where(c => c.PolicyId == policyId.Value);

        var matched = await query.OrderByDescending(c => c.DateOfLoss).ToListAsync(ct);

        // Value each claim as of the date: latest snapshot <= asOf; claims with
        // no snapshot fall back to current values when their valuation predates
        // asOf, otherwise they were not yet valued and are excluded.
        var claimIds = matched.Select(c => c.Id).ToList();
        var valuations = await _db.Set<ClaimValuation>().AsNoTracking()
            .Where(v => claimIds.Contains(v.ClaimId) && v.ValuationDate <= asOfDate && !v.IsDeleted)
            .ToListAsync(ct);
        var latestByClaim = valuations
            .GroupBy(v => v.ClaimId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(v => v.ValuationDate).First());
        var hasAnyValuation = (await _db.Set<ClaimValuation>().AsNoTracking()
            .Where(v => claimIds.Contains(v.ClaimId) && !v.IsDeleted)
            .Select(v => v.ClaimId)
            .Distinct()
            .ToListAsync(ct)).ToHashSet();

        var claims = new List<ClaimListItemDto>();
        foreach (var c in matched)
        {
            if (latestByClaim.TryGetValue(c.Id, out var snap))
            {
                var item = ToListItem(c);
                item.Status = snap.Status;
                item.Paid = snap.Paid;
                item.Reserved = snap.Reserved;
                item.Expense = snap.Expense;
                item.Recovery = snap.Recovery;
                item.Incurred = snap.Incurred;
                item.LastValuationDate = snap.ValuationDate;
                claims.Add(item);
            }
            else if (!hasAnyValuation.Contains(c.Id) && c.LastValuationDate <= asOfDate)
            {
                claims.Add(ToListItem(c));
            }
            // else: claim has snapshots but none <= asOf — not yet valued as of that date
        }

        string? insuredName = null;
        string? policyNumber = null;
        string? account = null;

        if (insuredId.HasValue)
            insuredName = (await _db.Set<Insured>().AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == insuredId.Value, ct))?.DisplayName;
        if (policyId.HasValue)
            policyNumber = (await _db.Set<Policy>().AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == policyId.Value, ct))?.PolicyNumber;

        if (claims.Count > 0)
            account = claims[0].Account;

        return Result<LossRunDto>.Success(new LossRunDto
        {
            AsOfDate = asOfDate,
            InsuredId = insuredId,
            InsuredName = insuredName,
            PolicyId = policyId,
            PolicyNumber = policyNumber,
            Account = account,
            ClaimCount = claims.Count,
            OpenCount = claims.Count(c => c.Status == ClaimStatus.Open || c.Status == ClaimStatus.Reopened),
            ClosedCount = claims.Count(c => c.Status == ClaimStatus.Closed),
            TotalPaid = claims.Sum(c => c.Paid),
            TotalReserved = claims.Sum(c => c.Reserved),
            TotalExpense = claims.Sum(c => c.Expense),
            TotalIncurred = claims.Sum(c => c.Incurred),
            Claims = claims,
        });
    }

    // ── Valuation snapshots ─────────────────────────────────────────────────────

    private void UpsertValuation(Claim claim, DateOnly valuationDate, Guid? importBatchId)
        => UpsertValuation(claim, valuationDate, importBatchId,
            claim.Status, claim.Paid, claim.Reserved, claim.Expense, claim.Recovery, claim.Incurred);

    private void UpsertValuation(
        Claim claim, DateOnly valuationDate, Guid? importBatchId,
        ClaimStatus status, decimal paid, decimal reserved, decimal expense, decimal recovery, decimal incurred)
    {
        var existing = claim.Valuations.FirstOrDefault(v => v.ValuationDate == valuationDate && !v.IsDeleted);
        if (existing is null)
        {
            existing = new ClaimValuation { Claim = claim, ClaimId = claim.Id, ValuationDate = valuationDate };
            claim.Valuations.Add(existing);
            _db.Set<ClaimValuation>().Add(existing);
        }
        existing.Status = status;
        existing.Paid = paid;
        existing.Reserved = reserved;
        existing.Expense = expense;
        existing.Recovery = recovery;
        existing.Incurred = incurred;
        existing.ImportBatchId = importBatchId;
        existing.UpdatedAt = DateTime.UtcNow;
    }

    // ── Projections ────────────────────────────────────────────────────────────

    private static ClaimListItemDto ToListItem(Claim c) => new()
    {
        Id = c.Id,
        ClaimNumber = c.ClaimNumber,
        CarrierClaimNumber = c.CarrierClaimNumber,
        PolicyId = c.PolicyId,
        PolicyNumber = c.PolicyNumber,
        InsuredId = c.InsuredId,
        InsuredName = c.InsuredName,
        SourcePolicyReference = c.SourcePolicyReference,
        Account = c.Account,
        CarrierName = c.CarrierName,
        DateOfLoss = c.DateOfLoss,
        ReportDate = c.ReportDate,
        ClosedDate = c.ClosedDate,
        Status = c.Status,
        CoverageType = c.CoverageType,
        ClaimTypeDesc = c.ClaimTypeDesc,
        LossCause = c.LossCause,
        TpaName = c.TpaName,
        ClaimantName = c.ClaimantName,
        AdjusterName = c.AdjusterName,
        Paid = c.Paid,
        Reserved = c.Reserved,
        Expense = c.Expense,
        Recovery = c.Recovery,
        Incurred = c.Incurred,
        LastValuationDate = c.LastValuationDate,
        IsManualEntry = c.IsManualEntry,
        CreatedAt = c.CreatedAt,
    };

    private static ClaimDto ToDto(Claim c) => new()
    {
        Id = c.Id,
        ClaimNumber = c.ClaimNumber,
        CarrierClaimNumber = c.CarrierClaimNumber,
        PolicyId = c.PolicyId,
        PolicyNumber = c.PolicyNumber,
        InsuredId = c.InsuredId,
        InsuredName = c.InsuredName,
        SourcePolicyReference = c.SourcePolicyReference,
        Account = c.Account,
        CarrierName = c.CarrierName,
        DateOfLoss = c.DateOfLoss,
        ReportDate = c.ReportDate,
        ClosedDate = c.ClosedDate,
        Status = c.Status,
        CoverageType = c.CoverageType,
        ClaimTypeDesc = c.ClaimTypeDesc,
        LossCause = c.LossCause,
        Description = c.Description,
        RiskState = c.RiskState,
        AccidentState = c.AccidentState,
        ClaimantName = c.ClaimantName,
        AdjusterName = c.AdjusterName,
        TpaName = c.TpaName,
        TpaClaimNumber = c.TpaClaimNumber,
        Paid = c.Paid,
        Reserved = c.Reserved,
        Expense = c.Expense,
        Recovery = c.Recovery,
        Incurred = c.Incurred,
        LastValuationDate = c.LastValuationDate,
        IsManualEntry = c.IsManualEntry,
        Notes = c.Notes,
        ImportBatchId = c.ImportBatchId,
        CreatedAt = c.CreatedAt,
    };

    private static ClaimImportBatchDto ToBatchDto(ClaimImportBatch b, string importedByName) => new()
    {
        Id = b.Id,
        FileName = b.FileName,
        CarrierName = b.CarrierName,
        TpaName = b.TpaName,
        ValuationDate = b.ValuationDate,
        RecordCount = b.RecordCount,
        CreatedCount = b.CreatedCount,
        UpdatedCount = b.UpdatedCount,
        SkippedCount = b.SkippedCount,
        ErrorCount = b.ErrorCount,
        Status = b.Status,
        ErrorSummaryJson = b.ErrorSummaryJson,
        ImportedByName = importedByName,
        CreatedAt = b.CreatedAt,
    };
}
