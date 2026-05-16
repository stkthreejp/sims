using SIMS.Application.Common;
using SIMS.Application.DTOs.Accounting;
using SIMS.Application.DTOs.Quotes;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Security;
using SIMS.Domain.Entities;
using SIMS.Domain.Entities.Rating;
using SIMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace SIMS.Application.Services;

public class QuoteService : IQuoteService
{
    private readonly IServiceProvider _sp;
    private readonly IWorkflowEngineService _workflowEngine;
    private readonly ICarrierCommissionService _carrierCommissions;
    private readonly IAgentCommissionService _agentCommissions;
    private readonly IQuoteChecklistService _checklist;
    private readonly IPolicyNumberService _policyNumbers;

    private Microsoft.EntityFrameworkCore.DbContext Db =>
        (Microsoft.EntityFrameworkCore.DbContext)_sp.GetService(typeof(Microsoft.EntityFrameworkCore.DbContext))!;

    public QuoteService(
        IServiceProvider sp,
        IWorkflowEngineService workflowEngine,
        ICarrierCommissionService carrierCommissions,
        IAgentCommissionService agentCommissions,
        IQuoteChecklistService checklist,
        IPolicyNumberService policyNumbers)
    {
        _sp = sp;
        _workflowEngine = workflowEngine;
        _carrierCommissions = carrierCommissions;
        _agentCommissions = agentCommissions;
        _checklist = checklist;
        _policyNumbers = policyNumbers;
    }

    public async Task<PagedResult<QuoteListItemDto>> GetAllAsync(QueryParameters query, UserAccessScope access)
    {
        var q = Db.Set<Quote>()
            .Include(qt => qt.Submission).ThenInclude(s => s.Insured)
            .Include(qt => qt.Carrier)
            .Where(qt => !qt.IsDeleted)
            .ForAccessScope(access)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.ToLower();
            q = q.Where(qt =>
                qt.QuoteNumber.ToLower().Contains(search) ||
                (qt.PolicyNumber != null && qt.PolicyNumber.ToLower().Contains(search)) ||
                qt.Submission.SubmissionNumber.ToLower().Contains(search) ||
                qt.Carrier.Name.ToLower().Contains(search));
        }

        var total = await q.CountAsync();

        q = query.SortDir.ToLower() == "asc"
            ? q.OrderBy(qt => qt.CreatedAt)
            : q.OrderByDescending(qt => qt.CreatedAt);

        var items = await q
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return new PagedResult<QuoteListItemDto>
        {
            Items = items.Select(MapToListItemDto),
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<IEnumerable<QuoteListItemDto>> GetBySubmissionAsync(Guid submissionId, UserAccessScope access)
    {
        var quotes = await Db.Set<Quote>()
            .Include(qt => qt.Submission).ThenInclude(s => s.Insured)
            .Include(qt => qt.Carrier)
            .Where(qt => qt.SubmissionId == submissionId && !qt.IsDeleted)
            .ForAccessScope(access)
            .OrderByDescending(qt => qt.CreatedAt)
            .ToListAsync();

        return quotes.Select(MapToListItemDto);
    }

    public async Task<IEnumerable<QuoteListItemDto>> GetBoundByInsuredAsync(Guid insuredId)
    {
        var quotes = await Db.Set<Quote>()
            .Include(qt => qt.Submission).ThenInclude(s => s.Insured)
            .Include(qt => qt.Carrier)
            .Where(qt => qt.Submission.InsuredId == insuredId && qt.Status == QuoteStatus.Bound && !qt.IsDeleted)
            .OrderByDescending(qt => qt.BoundDate)
            .ToListAsync();

        return quotes.Select(MapToListItemDto);
    }

    public async Task<Result<QuoteDto>> GetByIdAsync(Guid id, UserAccessScope access)
    {
        var quote = await Db.Set<Quote>()
            .Include(qt => qt.Submission).ThenInclude(s => s.Insured)
            .Include(qt => qt.Carrier)
            .Where(qt => qt.Id == id && !qt.IsDeleted)
            .ForAccessScope(access)
            .FirstOrDefaultAsync();

        return quote == null
            ? Result<QuoteDto>.Failure("NOT_FOUND", "Quote not found.")
            : Result<QuoteDto>.Success(await MapToDtoWithPolicyAsync(quote));
    }

    public async Task<Result<QuoteDto>> CreateAsync(QuoteCreateDto dto, Guid createdById)
    {
        var submission = await Db.Set<Submission>()
            .FirstOrDefaultAsync(s => s.Id == dto.SubmissionId && !s.IsDeleted);
        if (submission == null)
            return Result<QuoteDto>.Failure("INVALID_SUBMISSION", "Submission not found.");

        var carrier = await Db.Set<Carrier>()
            .FirstOrDefaultAsync(c => c.Id == dto.CarrierId && !c.IsDeleted && c.IsActive);
        if (carrier == null)
            return Result<QuoteDto>.Failure("INVALID_CARRIER", "Carrier not found or inactive.");

        // Look up commission rates from setup tables
        var asOfDate = dto.EffectiveDate;
        var lobKey = dto.LineOfBusiness.ToString();

        var carrierRates = await _carrierCommissions.GetActiveRatesAsync(dto.CarrierId, lobKey, asOfDate);
        var agentRate = submission.AgentId.HasValue
            ? await _agentCommissions.GetActiveRateAsync(submission.AgentId.Value, lobKey, asOfDate)
            : null;

        var quoteNumber = await GenerateQuoteNumberAsync();
        var total = dto.PremiumAmount + dto.TaxesAndFees;

        var quote = new Quote
        {
            QuoteNumber = quoteNumber,
            SubmissionId = dto.SubmissionId,
            CarrierId = dto.CarrierId,
            LineOfBusiness = dto.LineOfBusiness,
            EffectiveDate = dto.EffectiveDate,
            ExpirationDate = dto.ExpirationDate,
            PremiumAmount = dto.PremiumAmount,
            TaxesAndFees = dto.TaxesAndFees,
            TotalPremium = total,
            CarrierCommissionRate = carrierRates?.CommissionRate ?? 0,
            SMMRetentionRate = carrierRates?.SMMRetentionRate ?? 0,
            AgentCommissionRate = agentRate ?? 0,
            CompanyId = dto.CompanyId,
            ProducerId = dto.ProducerId ?? submission.ProducerId,
            IsFilingState = dto.IsFilingState,
            CoverageDescription = dto.CoverageDescription,
            Deductible = dto.Deductible,
            Limit = dto.Limit,
            UninsuredMotoristLimit = dto.UninsuredMotoristLimit,
            MedicalPaymentsLimit = dto.MedicalPaymentsLimit,
            CreatedById = createdById
        };

        Db.Set<Quote>().Add(quote);
        await Db.SaveChangesAsync();

        await _checklist.SeedDefaultsAsync(quote.Id, quote.LineOfBusiness);

        await Db.Entry(quote).Reference(qt => qt.Submission).LoadAsync();
        await Db.Entry(quote).Reference(qt => qt.Carrier).LoadAsync();

        await _workflowEngine.FireEventAsync(
            "quote.created",
            TaskEntityType.Policy,
            quote.Id,
            BuildQuoteContext(quote));

        return Result<QuoteDto>.Success(MapToDto(quote));
    }

    public async Task<Result<QuoteDto>> UpdateAsync(Guid id, QuoteUpdateDto dto, UserAccessScope access)
    {
        var quote = await Db.Set<Quote>()
            .Include(qt => qt.Submission)
            .Include(qt => qt.Carrier)
            .Where(qt => qt.Id == id && !qt.IsDeleted)
            .ForAccessScope(access)
            .FirstOrDefaultAsync();
        if (quote == null) return Result<QuoteDto>.Failure("NOT_FOUND", "Quote not found.");
        if (quote.Status == QuoteStatus.Bound)
            return Result<QuoteDto>.Failure("ALREADY_BOUND", "Cannot edit a bound policy.");

        var previousStatus = quote.Status;
        var lobChanged = quote.CarrierId != dto.CarrierId || quote.LineOfBusiness != dto.LineOfBusiness;

        quote.CarrierId = dto.CarrierId;
        quote.LineOfBusiness = dto.LineOfBusiness;
        quote.EffectiveDate = dto.EffectiveDate;
        quote.ExpirationDate = dto.ExpirationDate;
        quote.PremiumAmount = dto.PremiumAmount;
        quote.TaxesAndFees = dto.TaxesAndFees;
        quote.TotalPremium = dto.PremiumAmount + dto.TaxesAndFees;
        quote.CoverageDescription = dto.CoverageDescription;
        quote.Deductible = dto.Deductible;
        quote.Limit = dto.Limit;
        quote.UninsuredMotoristLimit = dto.UninsuredMotoristLimit;
        quote.MedicalPaymentsLimit = dto.MedicalPaymentsLimit;
        quote.Status = dto.Status;
        quote.UpdatedAt = DateTime.UtcNow;

        // Re-look up commission rates if carrier or LOB changed
        if (lobChanged)
        {
            var lobKey = dto.LineOfBusiness.ToString();
            var asOfDate = dto.EffectiveDate;
            var carrierRates = await _carrierCommissions.GetActiveRatesAsync(dto.CarrierId, lobKey, asOfDate);
            quote.CarrierCommissionRate = carrierRates?.CommissionRate ?? 0;
            quote.SMMRetentionRate = carrierRates?.SMMRetentionRate ?? 0;

            var submission = await Db.Set<Submission>().FirstOrDefaultAsync(s => s.Id == quote.SubmissionId);
            if (submission?.AgentId.HasValue == true)
            {
                var agentRate = await _agentCommissions.GetActiveRateAsync(submission.AgentId.Value, lobKey, asOfDate);
                quote.AgentCommissionRate = agentRate ?? 0;
            }
        }

        await Db.SaveChangesAsync();

        if (quote.CarrierId != dto.CarrierId)
            await Db.Entry(quote).Reference(qt => qt.Carrier).LoadAsync();

        if (dto.Status != previousStatus)
        {
            var eventName = dto.Status switch
            {
                QuoteStatus.Submitted => "quote.status.submitted",
                QuoteStatus.Quoted    => "quote.status.quoted",
                QuoteStatus.Declined  => "quote.status.declined",
                QuoteStatus.Cancelled => "quote.status.cancelled",
                QuoteStatus.Expired   => "quote.status.expired",
                _                     => null
            };

            if (eventName != null)
                await _workflowEngine.FireEventAsync(
                    eventName,
                    TaskEntityType.Policy,
                    quote.Id,
                    BuildQuoteContext(quote));
        }

        return Result<QuoteDto>.Success(MapToDto(quote));
    }

    public async Task<Result<QuoteDto>> BindAsync(Guid id, QuoteBindDto dto, UserAccessScope access)
    {
        var quote = await Db.Set<Quote>()
            .Include(qt => qt.Submission).ThenInclude(s => s.Insured)
            .Include(qt => qt.Submission).ThenInclude(s => s.Locations)
            .Include(qt => qt.Submission).ThenInclude(s => s.Vehicles)
            .Include(qt => qt.Carrier)
            .Where(qt => qt.Id == id && !qt.IsDeleted)
            .ForAccessScope(access)
            .FirstOrDefaultAsync();
        if (quote == null) return Result<QuoteDto>.Failure("NOT_FOUND", "Quote not found.");
        if (quote.Status == QuoteStatus.Bound)
            return Result<QuoteDto>.Failure("ALREADY_BOUND", "Quote is already bound.");
        if (!await HasIncludedPolicyFormsAsync(quote.Id))
            return Result<QuoteDto>.Failure("POLICY_FORMS_REQUIRED", "Select the policy forms for this quote before binding.");

        await using var dbTransaction = await Db.Database.BeginTransactionAsync();

        var policyNumberResult = await _policyNumbers.GenerateForBindAsync(quote, access.UserId);
        if (!policyNumberResult.IsSuccess || policyNumberResult.Value == null)
            return Result<QuoteDto>.Failure(policyNumberResult.ErrorCode ?? "POLICY_NUMBER_ERROR", policyNumberResult.ErrorMessage ?? "Policy number could not be assigned.");
        var policyNumber = policyNumberResult.Value;

        quote.Status = QuoteStatus.Bound;
        quote.PolicyNumber = policyNumber.PolicyNumber;
        quote.BoundDate = dto.BoundDate;
        quote.EffectiveDate = dto.EffectiveDate;
        quote.ExpirationDate = dto.ExpirationDate;
        quote.UpdatedAt = DateTime.UtcNow;

        // Lock the latest rating snapshot, if one exists. Once locked, the rating
        // engine won't replace it, the UI shows it as read-only, and any future
        // re-rating produces a NEW snapshot rather than mutating this one.
        var latestSnapshot = await Db.Set<QuoteRatingSnapshot>()
            .Where(s => s.QuoteId == quote.Id && !s.IsDeleted)
            .OrderByDescending(s => s.RatedAt)
            .FirstOrDefaultAsync();
        if (latestSnapshot != null)
        {
            latestSnapshot.IsBoundSnapshot = true;
            latestSnapshot.UpdatedAt = DateTime.UtcNow;
        }

        // Create the Policy record
        var policy = new Policy
        {
            PolicyNumber = policyNumber.PolicyNumber,
            BasePolicyNumber = policyNumber.BasePolicyNumber,
            PolicyTermNumber = policyNumber.TermNumber,
            PolicyNumberSequenceId = policyNumber.SequenceId,
            PolicyNumberAssignmentId = policyNumber.AssignmentId,
            SubmissionId = quote.SubmissionId,
            BoundQuoteId = quote.Id,
            CarrierId = quote.CarrierId,
            LineOfBusiness = quote.LineOfBusiness,
            EffectiveDate = dto.EffectiveDate,
            ExpirationDate = dto.ExpirationDate,
            PremiumAmount = quote.PremiumAmount,
            TaxesAndFees = quote.TaxesAndFees,
            TotalPremium = quote.TotalPremium,
            Status = PolicyStatus.Active,
            BoundDate = dto.BoundDate,
        };
        Db.Set<Policy>().Add(policy);

        // Update submission status if all quotes are now bound
        var submission = quote.Submission;
        var otherQuotes = await Db.Set<Quote>()
            .Where(qt => qt.SubmissionId == quote.SubmissionId && !qt.IsDeleted && qt.Id != id)
            .ToListAsync();
        if (submission != null && otherQuotes.All(qt => qt.Status == QuoteStatus.Bound))
            submission.Status = SubmissionStatus.Bound;

        await Db.SaveChangesAsync();

        if (policyNumber.SequenceId.HasValue)
        {
            var usage = await Db.Set<PolicyNumberSequenceUsage>()
                .FirstOrDefaultAsync(u => u.QuoteId == quote.Id && u.FullPolicyNumber == policy.PolicyNumber);
            if (usage != null)
            {
                usage.PolicyId = policy.Id;
                await Db.SaveChangesAsync();
            }
        }

        // NewBusiness transaction
        var txnNumber = await GenerateTransactionNumberAsync();
        var transaction = new PolicyTransaction
        {
            PolicyId = policy.Id,
            TransactionType = TransactionType.NewBusiness,
            Status = PolicyTransactionStatus.Issued,
            TransactionNumber = txnNumber,
            EffectiveDate = dto.EffectiveDate,
            PremiumChange = quote.TotalPremium,
            NewTotalPremium = quote.TotalPremium,
            ProcessedById = access.UserId,
            ProcessedAt = DateTime.UtcNow,
        };
        Db.Set<PolicyTransaction>().Add(transaction);
        await Db.SaveChangesAsync();

        // Auto-create invoice
        var invoicing = (IInvoicingService)_sp.GetService(typeof(IInvoicingService))!;
        var invoiceReq = new CreateInvoiceRequest(
            EffectiveDate: dto.EffectiveDate,
            GrossPremium: quote.PremiumAmount,
            StateCode: quote.Submission?.Insured?.State ?? "",
            IsEndorsement: false,
            IsFilingState: quote.IsFilingState,
            CarrierId: quote.CarrierId,
            CompanyId: quote.CompanyId,
            ProducerId: quote.ProducerId,
            LineOfBusiness: quote.LineOfBusiness.ToString(),
            City: null,
            LicenseType: null,
            LocationCount: quote.Submission?.Locations?.Count(l => !l.IsDeleted) ?? 1,
            VehicleCount: quote.Submission?.Vehicles?.Count(v => !v.IsDeleted) ?? 1,
            PolicyTransactionId: transaction.Id
        );
        var invoiceResult = await invoicing.BindAsync(invoiceReq, access.UserId);
        if (!invoiceResult.IsSuccess)
            return Result<QuoteDto>.Failure(invoiceResult.ErrorCode ?? "INVOICE_FAILED", invoiceResult.ErrorMessage ?? "Invoice could not be created.");

        await dbTransaction.CommitAsync();

        await _workflowEngine.FireEventAsync(
            "quote.status.bound",
            TaskEntityType.Policy,
            quote.Id,
            BuildQuoteContext(quote));

        var dtoResult = MapToDto(quote);
        dtoResult.BoundPolicyId = policy.Id;
        return Result<QuoteDto>.Success(dtoResult);
    }

    public async Task<Result<InvoicePreviewDto>> GetInvoicePreviewAsync(Guid id, UserAccessScope access)
    {
        var quote = await Db.Set<Quote>()
            .Include(qt => qt.Submission).ThenInclude(s => s.Insured)
            .Include(qt => qt.Submission).ThenInclude(s => s.Locations)
            .Include(qt => qt.Submission).ThenInclude(s => s.Vehicles)
            .Where(qt => qt.Id == id && !qt.IsDeleted)
            .ForAccessScope(access)
            .FirstOrDefaultAsync();

        if (quote == null)
            return Result<InvoicePreviewDto>.Failure("NOT_FOUND", "Quote not found.");

        var feeCalc = (IFeeCalculationService)_sp.GetService(typeof(IFeeCalculationService))!;
        var ctx = new PolicyContext(
            EffectiveDate: quote.EffectiveDate,
            GrossPremium: quote.PremiumAmount,
            StateCode: quote.Submission?.Insured?.State ?? "",
            IsEndorsement: false,
            IsFilingState: quote.IsFilingState,
            CarrierId: quote.CarrierId,
            CompanyId: quote.CompanyId,
            ProducerId: quote.ProducerId,
            LineOfBusiness: quote.LineOfBusiness.ToString(),
            City: null,
            LicenseType: null,
            LocationCount: quote.Submission?.Locations?.Count(l => !l.IsDeleted) ?? 1,
            VehicleCount: quote.Submission?.Vehicles?.Count(v => !v.IsDeleted) ?? 1);

        var calcResult = await feeCalc.CalculateAsync(ctx);
        var lines = calcResult.Lines
            .Select(l => new InvoiceLineDto(
                Id: 0,
                FeeCode: l.FeeCode,
                FeeDisplayName: l.FeeDisplayName,
                FeeCategory: l.FeeCategory,
                Amount: l.Amount,
                IsTaxable: l.IsTaxable,
                AccountCode: "",
                AccountLabel: ""))
            .ToList();

        var totalFees = lines.Sum(l => l.Amount);
        return Result<InvoicePreviewDto>.Success(new InvoicePreviewDto(
            GrossPremium: quote.PremiumAmount,
            TotalFees: totalFees,
            TotalAmount: quote.PremiumAmount + totalFees,
            Lines: lines));
    }

    public async Task<Result<QuoteDto>> ApplyCommissionOverrideAsync(
        Guid id, CommissionOverrideRequest req, UserAccessScope access)
    {
        var quote = await Db.Set<Quote>()
            .Include(qt => qt.Submission)
            .Include(qt => qt.Carrier)
            .Where(qt => qt.Id == id && !qt.IsDeleted)
            .ForAccessScope(access)
            .FirstOrDefaultAsync();
        if (quote == null) return Result<QuoteDto>.Failure("NOT_FOUND", "Quote not found.");
        if (quote.Status == QuoteStatus.Bound)
            return Result<QuoteDto>.Failure("ALREADY_BOUND", "Cannot modify commission on a bound policy.");

        if (req.GivebackAmount == null && req.NewAgentRate == null)
            return Result<QuoteDto>.Failure("INVALID_REQUEST", "Provide either a giveback amount or a new agent rate.");
        if (req.GivebackAmount != null && req.NewAgentRate != null)
            return Result<QuoteDto>.Failure("INVALID_REQUEST", "Provide either a giveback amount or a new agent rate, not both.");

        var oldPremium = quote.PremiumAmount;
        var oldAgentDollars = Math.Round(oldPremium * quote.AgentCommissionRate, 4);
        var oldCarrierDollars = Math.Round(oldPremium * quote.CarrierCommissionRate, 4);
        var oldSMMDollars = Math.Round(oldPremium * quote.SMMRetentionRate, 4);

        decimal givebackAmount;
        if (req.GivebackAmount.HasValue)
        {
            givebackAmount = req.GivebackAmount.Value;
        }
        else
        {
            // Apply new rate to old premium to determine new agent dollar amount
            var newAgentDollars = Math.Round(oldPremium * req.NewAgentRate!.Value, 4);
            givebackAmount = oldAgentDollars - newAgentDollars;
        }

        if (givebackAmount <= 0)
            return Result<QuoteDto>.Failure("INVALID_GIVEBACK", "Giveback amount must reduce the agent commission.");
        if (givebackAmount >= oldAgentDollars)
            return Result<QuoteDto>.Failure("INVALID_GIVEBACK", "Giveback amount cannot exceed the agent's full commission.");

        var newPremium = oldPremium - givebackAmount;
        if (newPremium <= 0)
            return Result<QuoteDto>.Failure("INVALID_GIVEBACK", "Resulting premium would be zero or negative.");

        // Recalculate all rates on the new premium (dollar amounts for carrier + SMM unchanged)
        var newCarrierRate = newPremium > 0 ? Math.Round(oldCarrierDollars / newPremium, 6) : 0;
        var newSMMRate = newPremium > 0 ? Math.Round(oldSMMDollars / newPremium, 6) : 0;
        var newAgentRate = newPremium > 0 ? Math.Round((oldAgentDollars - givebackAmount) / newPremium, 6) : 0;

        // Update premium on the quote
        quote.PremiumAmount = Math.Round(newPremium, 2);
        quote.TotalPremium = Math.Round(newPremium + quote.TaxesAndFees, 2);

        // Lock the override rates
        quote.CommissionOverrideCarrierRate = newCarrierRate;
        quote.CommissionOverrideSMMRate = newSMMRate;
        quote.CommissionOverrideAgentRate = newAgentRate;
        quote.CommissionOverrideBy = access.UserId;
        quote.CommissionOverrideAt = DateTime.UtcNow;
        quote.UpdatedAt = DateTime.UtcNow;

        await Db.SaveChangesAsync();

        return Result<QuoteDto>.Success(MapToDto(quote));
    }

    public async Task<Result> DeleteAsync(Guid id, UserAccessScope access)
    {
        var quote = await Db.Set<Quote>()
            .Where(qt => qt.Id == id && !qt.IsDeleted)
            .ForAccessScope(access)
            .FirstOrDefaultAsync();
        if (quote == null) return Result.Failure("NOT_FOUND", "Quote not found.");
        if (quote.Status == QuoteStatus.Bound)
            return Result.Failure("BOUND_POLICY", "Cannot delete a bound policy.");

        quote.IsDeleted = true;
        quote.DeletedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync();
        return Result.Success();
    }

    private static Dictionary<string, object> BuildQuoteContext(Quote qt)
    {
        var ctx = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["Status"]       = qt.Status.ToString(),
            ["SubmissionId"] = qt.SubmissionId,
            ["CarrierId"]    = qt.CarrierId,
        };
        ctx["EffectiveDate"]  = qt.EffectiveDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        ctx["ExpirationDate"] = qt.ExpirationDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        if (qt.BoundDate.HasValue)
            ctx["BoundDate"] = qt.BoundDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        if (qt.Submission != null)
        {
            ctx["UnderwriterId"] = qt.Submission.UnderwriterId;
            if (qt.Submission.AssistantUWId.HasValue)
                ctx["AssistantUWId"] = qt.Submission.AssistantUWId.Value;
        }
        return ctx;
    }

    private async Task<string> GenerateQuoteNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"QTE-{year}-";
        var count = await Db.Set<Quote>()
            .IgnoreQueryFilters()
            .CountAsync(q => q.QuoteNumber.StartsWith(prefix));
        return $"{prefix}{(count + 1):D4}";
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

    private static QuoteListItemDto MapToListItemDto(Quote qt) => new()
    {
        Id = qt.Id,
        QuoteNumber = qt.QuoteNumber,
        SubmissionId = qt.SubmissionId,
        SubmissionNumber = qt.Submission?.SubmissionNumber ?? "",
        InsuredName = qt.Submission?.Insured?.DisplayName ?? "",
        CarrierName = qt.Carrier?.Name ?? "",
        LineOfBusiness = qt.LineOfBusiness,
        Status = qt.Status,
        PolicyNumber = qt.PolicyNumber,
        EffectiveDate = qt.EffectiveDate,
        ExpirationDate = qt.ExpirationDate,
        TotalPremium = qt.TotalPremium,
        HasCommissionOverride = qt.HasCommissionOverride,
        CreatedAt = qt.CreatedAt
    };

    private async Task<QuoteDto> MapToDtoWithPolicyAsync(Quote qt)
    {
        var dto = MapToDto(qt);
        if (qt.Status == QuoteStatus.Bound || qt.PolicyNumber != null)
        {
            dto.BoundPolicyId = await Db.Set<Policy>()
                .Where(p => p.BoundQuoteId == qt.Id && !p.IsDeleted)
                .OrderByDescending(p => p.BoundDate)
                .Select(p => (Guid?)p.Id)
                .FirstOrDefaultAsync();
        }

        return dto;
    }

    private async Task<bool> HasIncludedPolicyFormsAsync(Guid quoteId)
        => await Db.Set<QuotePolicyFormSelection>()
            .AnyAsync(f => f.QuoteId == quoteId && f.IsIncluded && !f.IsDeleted);

    private static QuoteDto MapToDto(Quote qt)
    {
        var premium = qt.PremiumAmount;
        var effectiveCarrier = qt.EffectiveCarrierRate;
        var effectiveSMM = qt.EffectiveSMMRate;
        var effectiveAgent = qt.EffectiveAgentRate;

        return new QuoteDto
        {
            Id = qt.Id,
            QuoteNumber = qt.QuoteNumber,
            SubmissionId = qt.SubmissionId,
            SubmissionNumber = qt.Submission?.SubmissionNumber ?? "",
            InsuredId = qt.Submission?.InsuredId ?? Guid.Empty,
            InsuredName = qt.Submission?.Insured?.DisplayName ?? "",
            CarrierId = qt.CarrierId,
            CarrierName = qt.Carrier?.Name ?? "",
            LineOfBusiness = qt.LineOfBusiness,
            Status = qt.Status,
            PolicyNumber = qt.PolicyNumber,
            BoundDate = qt.BoundDate,
            IssuedDate = qt.IssuedDate,
            CancelledDate = qt.CancelledDate,
            EffectiveDate = qt.EffectiveDate,
            ExpirationDate = qt.ExpirationDate,
            PremiumAmount = qt.PremiumAmount,
            TaxesAndFees = qt.TaxesAndFees,
            TotalPremium = qt.TotalPremium,
            CarrierCommissionRate = qt.CarrierCommissionRate,
            SMMRetentionRate = qt.SMMRetentionRate,
            AgentCommissionRate = qt.AgentCommissionRate,
            CarrierCommissionAmount = Math.Round(premium * qt.CarrierCommissionRate, 2),
            SMMRetentionAmount = Math.Round(premium * qt.SMMRetentionRate, 2),
            AgentCommissionAmount = Math.Round(premium * qt.AgentCommissionRate, 2),
            CommissionOverride = qt.HasCommissionOverride ? new CommissionOverrideDto
            {
                CarrierRate = qt.CommissionOverrideCarrierRate!.Value,
                SMMRate = qt.CommissionOverrideSMMRate!.Value,
                AgentRate = qt.CommissionOverrideAgentRate!.Value,
                OverrideBy = qt.CommissionOverrideBy!.Value,
                OverrideAt = qt.CommissionOverrideAt!.Value,
                CarrierCommissionAmount = Math.Round(premium * effectiveCarrier, 2),
                SMMRetentionAmount = Math.Round(premium * effectiveSMM, 2),
                AgentCommissionAmount = Math.Round(premium * effectiveAgent, 2),
            } : null,
            CompanyId = qt.CompanyId,
            ProducerId = qt.ProducerId,
            IsFilingState = qt.IsFilingState,
            CoverageDescription = qt.CoverageDescription,
            Deductible = qt.Deductible,
            Limit = qt.Limit,
            UninsuredMotoristLimit = qt.UninsuredMotoristLimit,
            MedicalPaymentsLimit = qt.MedicalPaymentsLimit,
            CreatedAt = qt.CreatedAt
        };
    }
}
