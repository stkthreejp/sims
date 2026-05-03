using Microsoft.EntityFrameworkCore;
using SIMS.Application.Common;
using SIMS.Application.DTOs.Accounting;
using SIMS.Application.DTOs.Policies;
using SIMS.Application.DTOs.Quotes;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;

namespace SIMS.Application.Services;

public class PolicyService : IPolicyService
{
    private readonly IServiceProvider _sp;
    private readonly IInvoicingService _invoicing;

    private DbContext Db => (DbContext)_sp.GetService(typeof(DbContext))!;

    public PolicyService(IServiceProvider sp, IInvoicingService invoicing)
    {
        _sp = sp;
        _invoicing = invoicing;
    }

    public async Task<PagedResult<PolicyListItemDto>> GetAllAsync(QueryParameters query)
    {
        var q = Db.Set<Policy>()
            .Include(p => p.Submission).ThenInclude(s => s.Insured)
            .Include(p => p.Carrier)
            .Where(p => !p.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.ToLower();
            q = q.Where(p =>
                p.PolicyNumber.ToLower().Contains(s) ||
                p.Submission.Insured.DisplayName.ToLower().Contains(s) ||
                p.Carrier.Name.ToLower().Contains(s));
        }

        var total = await q.CountAsync();

        q = query.SortDir.ToLower() == "asc"
            ? q.OrderBy(p => p.BoundDate)
            : q.OrderByDescending(p => p.BoundDate);

        var items = await q
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return new PagedResult<PolicyListItemDto>
        {
            Items = items.Select(MapToListItemDto),
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<IEnumerable<PolicyListItemDto>> GetByInsuredAsync(Guid insuredId)
    {
        var items = await Db.Set<Policy>()
            .Include(p => p.Submission).ThenInclude(s => s.Insured)
            .Include(p => p.Carrier)
            .Where(p => p.Submission.InsuredId == insuredId && !p.IsDeleted)
            .OrderByDescending(p => p.BoundDate)
            .ToListAsync();

        return items.Select(MapToListItemDto);
    }

    public async Task<Result<PolicyDto>> GetByIdAsync(Guid id)
    {
        var policy = await Db.Set<Policy>()
            .Include(p => p.Submission).ThenInclude(s => s.Insured)
            .Include(p => p.Carrier)
            .Include(p => p.BoundQuote)
            .Include(p => p.Transactions).ThenInclude(t => t.ProcessedBy)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        return policy == null
            ? Result<PolicyDto>.Failure("NOT_FOUND", "Policy not found.")
            : Result<PolicyDto>.Success(MapToDto(policy));
    }

    public async Task<Result<PolicyTransactionDto>> AddEndorsementAsync(
        Guid policyId, CreateEndorsementDto dto, Guid userId)
    {
        var policy = await Db.Set<Policy>()
            .FirstOrDefaultAsync(p => p.Id == policyId && !p.IsDeleted);
        if (policy == null) return Result<PolicyTransactionDto>.Failure("NOT_FOUND", "Policy not found.");
        if (policy.Status != PolicyStatus.Active)
            return Result<PolicyTransactionDto>.Failure("INVALID_STATUS", "Only active policies can be endorsed.");

        var txnNumber = await GenerateTransactionNumberAsync();
        var txn = new PolicyTransaction
        {
            PolicyId = policyId,
            TransactionType = TransactionType.Endorsement,
            Status = PolicyTransactionStatus.Pending,
            TransactionNumber = txnNumber,
            EffectiveDate = dto.EffectiveDate,
            PremiumChange = dto.PremiumChange,
            NewTotalPremium = policy.TotalPremium + dto.PremiumChange,
            EndorsementDescription = dto.EndorsementDescription,
            Notes = dto.Notes,
            ProcessedById = userId,
            ProcessedAt = DateTime.UtcNow,
        };

        Db.Set<PolicyTransaction>().Add(txn);
        await Db.SaveChangesAsync();
        await Db.Entry(txn).Reference(t => t.ProcessedBy).LoadAsync();

        return Result<PolicyTransactionDto>.Success(MapToTransactionDto(txn));
    }

    public async Task<Result<PolicyTransactionDto>> IssueEndorsementAsync(
        Guid policyId, Guid txnId, IssueEndorsementDto dto, Guid userId)
    {
        var txn = await Db.Set<PolicyTransaction>()
            .Include(t => t.Policy).ThenInclude(p => p.BoundQuote)
            .Include(t => t.Policy).ThenInclude(p => p.Submission).ThenInclude(s => s.Insured)
            .Include(t => t.Policy).ThenInclude(p => p.Submission).ThenInclude(s => s.Locations)
            .Include(t => t.Policy).ThenInclude(p => p.Submission).ThenInclude(s => s.Vehicles)
            .Include(t => t.ProcessedBy)
            .FirstOrDefaultAsync(t => t.Id == txnId && t.PolicyId == policyId);

        if (txn == null) return Result<PolicyTransactionDto>.Failure("NOT_FOUND", "Endorsement not found.");
        if (txn.TransactionType != TransactionType.Endorsement)
            return Result<PolicyTransactionDto>.Failure("INVALID_TYPE", "Transaction is not an endorsement.");
        if (txn.Status != PolicyTransactionStatus.Pending)
            return Result<PolicyTransactionDto>.Failure("ALREADY_ISSUED", "Endorsement is already issued.");

        if (dto.EffectiveDate.HasValue) txn.EffectiveDate = dto.EffectiveDate.Value;
        if (dto.PremiumChange.HasValue)
        {
            txn.PremiumChange = dto.PremiumChange.Value;
            txn.NewTotalPremium = txn.Policy.TotalPremium + dto.PremiumChange.Value;
        }

        txn.Status = PolicyTransactionStatus.Issued;
        txn.Policy.TotalPremium = txn.NewTotalPremium;
        await Db.SaveChangesAsync();

        // Auto-create invoice
        var quote = txn.Policy.BoundQuote;
        var submission = txn.Policy.Submission;
        if (quote != null && submission != null)
        {
            var req = new CreateInvoiceRequest(
                EffectiveDate: txn.EffectiveDate,
                GrossPremium: txn.PremiumChange,
                StateCode: submission.Insured?.State ?? "",
                IsEndorsement: true,
                IsFilingState: quote.IsFilingState,
                CompanyId: quote.CompanyId,
                ProducerId: quote.ProducerId,
                LineOfBusiness: quote.LineOfBusiness.ToString(),
                City: null,
                LicenseType: null,
                LocationCount: submission.Locations?.Count(l => !l.IsDeleted) ?? 1,
                VehicleCount: submission.Vehicles?.Count(v => !v.IsDeleted) ?? 1,
                PolicyTransactionId: txn.Id
            );
            await _invoicing.BindAsync(req, userId);
        }

        return Result<PolicyTransactionDto>.Success(MapToTransactionDto(txn));
    }

    public async Task<Result<QuoteDto>> CreateRenewalQuoteAsync(Guid policyId, Guid userId)
    {
        var policy = await Db.Set<Policy>()
            .Include(p => p.BoundQuote)
            .Include(p => p.Submission)
            .FirstOrDefaultAsync(p => p.Id == policyId && !p.IsDeleted);
        if (policy == null) return Result<QuoteDto>.Failure("NOT_FOUND", "Policy not found.");
        if (policy.Status != PolicyStatus.Active)
            return Result<QuoteDto>.Failure("INVALID_STATUS", "Only active policies can be renewed.");

        var quoteService = (IQuoteService)_sp.GetService(typeof(IQuoteService))!;
        var source = policy.BoundQuote;

        var renewalDto = new QuoteCreateDto
        {
            SubmissionId = policy.SubmissionId,
            CarrierId = policy.CarrierId,
            LineOfBusiness = policy.LineOfBusiness,
            EffectiveDate = policy.ExpirationDate,
            ExpirationDate = policy.ExpirationDate.AddYears(1),
            PremiumAmount = policy.PremiumAmount,
            TaxesAndFees = policy.TaxesAndFees,
            CompanyId = source?.CompanyId,
            ProducerId = source?.ProducerId,
            IsFilingState = source?.IsFilingState ?? false,
            CoverageDescription = source?.CoverageDescription,
            Deductible = source?.Deductible,
            Limit = source?.Limit,
            UninsuredMotoristLimit = source?.UninsuredMotoristLimit,
            MedicalPaymentsLimit = source?.MedicalPaymentsLimit,
        };

        return await quoteService.CreateAsync(renewalDto, userId);
    }

    public async Task<Result<PolicyDto>> NonRenewAsync(Guid policyId, NonRenewPolicyDto dto, Guid userId)
    {
        var policy = await Db.Set<Policy>()
            .Include(p => p.Submission).ThenInclude(s => s.Insured)
            .Include(p => p.Carrier)
            .Include(p => p.BoundQuote)
            .Include(p => p.Transactions).ThenInclude(t => t.ProcessedBy)
            .FirstOrDefaultAsync(p => p.Id == policyId && !p.IsDeleted);
        if (policy == null) return Result<PolicyDto>.Failure("NOT_FOUND", "Policy not found.");
        if (policy.Status != PolicyStatus.Active)
            return Result<PolicyDto>.Failure("INVALID_STATUS", "Only active policies can be non-renewed.");

        policy.Status = PolicyStatus.NonRenewed;
        policy.NonRenewedDate = dto.NonRenewedDate;
        policy.UpdatedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync();

        return Result<PolicyDto>.Success(MapToDto(policy));
    }

    private async Task<string> GenerateTransactionNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"TXN-{year}-";
        var count = await Db.Set<PolicyTransaction>()
            .IgnoreQueryFilters()
            .CountAsync(t => t.TransactionNumber.StartsWith(prefix));
        return $"{prefix}{(count + 1):D5}";
    }

    private static PolicyListItemDto MapToListItemDto(Policy p) => new()
    {
        Id = p.Id,
        PolicyNumber = p.PolicyNumber,
        SubmissionId = p.SubmissionId,
        InsuredName = p.Submission?.Insured?.DisplayName ?? "",
        CarrierName = p.Carrier?.Name ?? "",
        LineOfBusiness = p.LineOfBusiness,
        EffectiveDate = p.EffectiveDate,
        ExpirationDate = p.ExpirationDate,
        TotalPremium = p.TotalPremium,
        Status = p.Status,
        BoundDate = p.BoundDate,
        CreatedAt = p.CreatedAt,
    };

    private static PolicyDto MapToDto(Policy p)
    {
        var quote = p.BoundQuote;
        var premium = quote?.PremiumAmount ?? p.PremiumAmount;
        var effectiveCarrier = quote?.EffectiveCarrierRate ?? 0;
        var effectiveSMM = quote?.EffectiveSMMRate ?? 0;
        var effectiveAgent = quote?.EffectiveAgentRate ?? 0;

        return new PolicyDto
        {
            Id = p.Id,
            PolicyNumber = p.PolicyNumber,
            SubmissionId = p.SubmissionId,
            SubmissionNumber = p.Submission?.SubmissionNumber ?? "",
            InsuredId = p.Submission?.InsuredId ?? Guid.Empty,
            InsuredName = p.Submission?.Insured?.DisplayName ?? "",
            CarrierId = p.CarrierId,
            CarrierName = p.Carrier?.Name ?? "",
            LineOfBusiness = p.LineOfBusiness,
            EffectiveDate = p.EffectiveDate,
            ExpirationDate = p.ExpirationDate,
            PremiumAmount = p.PremiumAmount,
            TaxesAndFees = p.TaxesAndFees,
            TotalPremium = p.TotalPremium,
            Status = p.Status,
            BoundDate = p.BoundDate,
            IssuedDate = p.IssuedDate,
            CancelledDate = p.CancelledDate,
            NonRenewedDate = p.NonRenewedDate,
            BoundQuoteId = p.BoundQuoteId,
            CarrierCommissionRate = effectiveCarrier,
            SMMRetentionRate = effectiveSMM,
            AgentCommissionRate = effectiveAgent,
            CarrierCommissionAmount = Math.Round(premium * effectiveCarrier, 2),
            SMMRetentionAmount = Math.Round(premium * effectiveSMM, 2),
            AgentCommissionAmount = Math.Round(premium * effectiveAgent, 2),
            CoverageDescription = quote?.CoverageDescription,
            Deductible = quote?.Deductible,
            Limit = quote?.Limit,
            UninsuredMotoristLimit = quote?.UninsuredMotoristLimit,
            MedicalPaymentsLimit = quote?.MedicalPaymentsLimit,
            Transactions = p.Transactions
                .OrderBy(t => t.ProcessedAt)
                .Select(MapToTransactionDto)
                .ToList(),
            CreatedAt = p.CreatedAt,
        };
    }

    private static PolicyTransactionDto MapToTransactionDto(PolicyTransaction t) => new()
    {
        Id = t.Id,
        PolicyId = t.PolicyId,
        TransactionType = t.TransactionType,
        Status = t.Status,
        TransactionNumber = t.TransactionNumber,
        EffectiveDate = t.EffectiveDate,
        EndorsementDescription = t.EndorsementDescription,
        PriorPolicyId = t.PriorPolicyId,
        CancellationReason = t.CancellationReason,
        CancellationMethod = t.CancellationMethod,
        PremiumChange = t.PremiumChange,
        NewTotalPremium = t.NewTotalPremium,
        ProcessedByName = t.ProcessedBy != null
            ? $"{t.ProcessedBy.FirstName} {t.ProcessedBy.LastName}".Trim()
            : "",
        ProcessedAt = t.ProcessedAt,
        Notes = t.Notes,
    };
}
