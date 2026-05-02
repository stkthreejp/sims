using IMS.Application.Common;
using IMS.Application.DTOs.Quotes;
using IMS.Application.Interfaces.Services;
using IMS.Domain.Entities;
using IMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace IMS.Application.Services;

public class QuoteService : IQuoteService
{
    private readonly IServiceProvider _sp;
    private readonly IWorkflowEngineService _workflowEngine;
    private Microsoft.EntityFrameworkCore.DbContext Db =>
        (Microsoft.EntityFrameworkCore.DbContext)_sp.GetService(typeof(Microsoft.EntityFrameworkCore.DbContext))!;

    public QuoteService(IServiceProvider sp, IWorkflowEngineService workflowEngine)
    {
        _sp = sp;
        _workflowEngine = workflowEngine;
    }

    public async Task<PagedResult<QuoteListItemDto>> GetAllAsync(QueryParameters query)
    {
        var q = Db.Set<Quote>()
            .Include(qt => qt.Submission).ThenInclude(s => s.Insured)
            .Include(qt => qt.Carrier)
            .Where(qt => !qt.IsDeleted)
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

    public async Task<PagedResult<QuoteListItemDto>> GetAllPoliciesAsync(QueryParameters query)
    {
        var q = Db.Set<Quote>()
            .Include(qt => qt.Submission).ThenInclude(s => s.Insured)
            .Include(qt => qt.Carrier)
            .Where(qt => !qt.IsDeleted && qt.Status == QuoteStatus.Bound)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.ToLower();
            q = q.Where(qt =>
                (qt.PolicyNumber != null && qt.PolicyNumber.ToLower().Contains(search)) ||
                qt.Submission.Insured.DisplayName.ToLower().Contains(search) ||
                qt.Carrier.Name.ToLower().Contains(search));
        }

        var total = await q.CountAsync();

        q = query.SortDir.ToLower() == "asc"
            ? q.OrderBy(qt => qt.BoundDate)
            : q.OrderByDescending(qt => qt.BoundDate);

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

    public async Task<IEnumerable<QuoteListItemDto>> GetBySubmissionAsync(Guid submissionId)
    {
        var quotes = await Db.Set<Quote>()
            .Include(qt => qt.Submission).ThenInclude(s => s.Insured)
            .Include(qt => qt.Carrier)
            .Where(qt => qt.SubmissionId == submissionId && !qt.IsDeleted)
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

    public async Task<Result<QuoteDto>> GetByIdAsync(Guid id)
    {
        var quote = await Db.Set<Quote>()
            .Include(qt => qt.Submission).ThenInclude(s => s.Insured)
            .Include(qt => qt.Carrier)
            .FirstOrDefaultAsync(qt => qt.Id == id && !qt.IsDeleted);

        return quote == null
            ? Result<QuoteDto>.Failure("NOT_FOUND", "Quote not found.")
            : Result<QuoteDto>.Success(MapToDto(quote));
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

        var quoteNumber = await GenerateQuoteNumberAsync();
        var total = dto.PremiumAmount + dto.TaxesAndFees;
        var commission = Math.Round(dto.PremiumAmount * dto.CommissionRate, 2);

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
            CommissionRate = dto.CommissionRate,
            CommissionAmount = commission,
            CoverageDescription = dto.CoverageDescription,
            Deductible = dto.Deductible,
            Limit = dto.Limit,
            CreatedById = createdById
        };

        Db.Set<Quote>().Add(quote);
        await Db.SaveChangesAsync();

        await Db.Entry(quote).Reference(qt => qt.Submission).LoadAsync();
        await Db.Entry(quote).Reference(qt => qt.Carrier).LoadAsync();

        await _workflowEngine.FireEventAsync(
            "quote.created",
            TaskEntityType.Policy,
            quote.Id,
            BuildQuoteContext(quote));

        return Result<QuoteDto>.Success(MapToDto(quote));
    }

    public async Task<Result<QuoteDto>> UpdateAsync(Guid id, QuoteUpdateDto dto)
    {
        var quote = await Db.Set<Quote>()
            .Include(qt => qt.Submission)
            .Include(qt => qt.Carrier)
            .FirstOrDefaultAsync(qt => qt.Id == id && !qt.IsDeleted);
        if (quote == null) return Result<QuoteDto>.Failure("NOT_FOUND", "Quote not found.");
        if (quote.Status == QuoteStatus.Bound)
            return Result<QuoteDto>.Failure("ALREADY_BOUND", "Cannot edit a bound policy.");

        var previousStatus = quote.Status;

        quote.CarrierId = dto.CarrierId;
        quote.LineOfBusiness = dto.LineOfBusiness;
        quote.EffectiveDate = dto.EffectiveDate;
        quote.ExpirationDate = dto.ExpirationDate;
        quote.PremiumAmount = dto.PremiumAmount;
        quote.TaxesAndFees = dto.TaxesAndFees;
        quote.TotalPremium = dto.PremiumAmount + dto.TaxesAndFees;
        quote.CommissionRate = dto.CommissionRate;
        quote.CommissionAmount = Math.Round(dto.PremiumAmount * dto.CommissionRate, 2);
        quote.CoverageDescription = dto.CoverageDescription;
        quote.Deductible = dto.Deductible;
        quote.Limit = dto.Limit;
        quote.Status = dto.Status;
        quote.UpdatedAt = DateTime.UtcNow;

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

    public async Task<Result<QuoteDto>> BindAsync(Guid id, QuoteBindDto dto, Guid userId)
    {
        var quote = await Db.Set<Quote>()
            .Include(qt => qt.Submission)
            .Include(qt => qt.Carrier)
            .FirstOrDefaultAsync(qt => qt.Id == id && !qt.IsDeleted);
        if (quote == null) return Result<QuoteDto>.Failure("NOT_FOUND", "Quote not found.");
        if (quote.Status == QuoteStatus.Bound)
            return Result<QuoteDto>.Failure("ALREADY_BOUND", "Quote is already bound.");

        var policyNumber = await GeneratePolicyNumberAsync();

        quote.Status = QuoteStatus.Bound;
        quote.PolicyNumber = policyNumber;
        quote.BoundDate = dto.BoundDate;
        quote.EffectiveDate = dto.EffectiveDate;
        quote.ExpirationDate = dto.ExpirationDate;
        quote.UpdatedAt = DateTime.UtcNow;

        // Update submission status if all quotes are bound
        var submission = await Db.Set<Submission>()
            .Include(s => s.Quotes.Where(qt => !qt.IsDeleted))
            .FirstOrDefaultAsync(s => s.Id == quote.SubmissionId);
        if (submission != null && submission.Quotes.All(qt => qt.Status == QuoteStatus.Bound || qt.Id == id))
            submission.Status = SubmissionStatus.Bound;

        await Db.SaveChangesAsync();

        await _workflowEngine.FireEventAsync(
            "quote.status.bound",
            TaskEntityType.Policy,
            quote.Id,
            BuildQuoteContext(quote));

        return Result<QuoteDto>.Success(MapToDto(quote));
    }

    public async Task<Result> DeleteAsync(Guid id)
    {
        var quote = await Db.Set<Quote>().FirstOrDefaultAsync(qt => qt.Id == id && !qt.IsDeleted);
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

    private async Task<string> GeneratePolicyNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"POL-{year}-";
        var count = await Db.Set<Quote>()
            .IgnoreQueryFilters()
            .CountAsync(q => q.PolicyNumber != null && q.PolicyNumber.StartsWith(prefix));
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
        CreatedAt = qt.CreatedAt
    };

    private static QuoteDto MapToDto(Quote qt) => new()
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
        CommissionRate = qt.CommissionRate,
        CommissionAmount = qt.CommissionAmount,
        CoverageDescription = qt.CoverageDescription,
        Deductible = qt.Deductible,
        Limit = qt.Limit,
        CreatedAt = qt.CreatedAt
    };
}
