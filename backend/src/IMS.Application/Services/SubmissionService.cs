using System.Text.Json;
using IMS.Application.Common;
using IMS.Application.DTOs.Submissions;
using IMS.Application.Interfaces.Services;
using IMS.Domain.Entities;
using IMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace IMS.Application.Services;

public class SubmissionService : ISubmissionService
{
    private readonly IServiceProvider _sp;
    private Microsoft.EntityFrameworkCore.DbContext Db =>
        (Microsoft.EntityFrameworkCore.DbContext)_sp.GetService(typeof(Microsoft.EntityFrameworkCore.DbContext))!;

    public SubmissionService(IServiceProvider sp) => _sp = sp;

    public async Task<PagedResult<SubmissionListItemDto>> GetAllAsync(QueryParameters query)
    {
        var q = Db.Set<Submission>()
            .Include(s => s.Insured)
            .Include(s => s.Agent)
            .Include(s => s.Underwriter)
            .Include(s => s.Quotes.Where(qt => !qt.IsDeleted))
            .Where(s => !s.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.ToLower();
            q = q.Where(s =>
                s.SubmissionNumber.ToLower().Contains(search) ||
                s.Insured.FirstName!.ToLower().Contains(search) ||
                s.Insured.LastName!.ToLower().Contains(search) ||
                s.Insured.CompanyName!.ToLower().Contains(search));
        }

        var total = await q.CountAsync();

        q = query.SortDir.ToLower() == "asc"
            ? q.OrderBy(s => s.CreatedAt)
            : q.OrderByDescending(s => s.CreatedAt);

        var items = await q
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return new PagedResult<SubmissionListItemDto>
        {
            Items = items.Select(MapToListItemDto),
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<IEnumerable<SubmissionListItemDto>> GetByInsuredAsync(Guid insuredId)
    {
        var submissions = await Db.Set<Submission>()
            .Include(s => s.Agent)
            .Include(s => s.Underwriter)
            .Include(s => s.Quotes.Where(qt => !qt.IsDeleted))
            .Where(s => s.InsuredId == insuredId && !s.IsDeleted)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return submissions.Select(MapToListItemDto);
    }

    public async Task<Result<SubmissionDto>> GetByIdAsync(Guid id)
    {
        var submission = await Db.Set<Submission>()
            .Include(s => s.Insured)
            .Include(s => s.Agent)
            .Include(s => s.Underwriter)
            .Include(s => s.AssistantUW)
            .Include(s => s.Quotes.Where(qt => !qt.IsDeleted))
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);

        return submission == null
            ? Result<SubmissionDto>.Failure("NOT_FOUND", "Submission not found.")
            : Result<SubmissionDto>.Success(MapToDto(submission));
    }

    public async Task<Result<SubmissionDto>> CreateAsync(SubmissionCreateDto dto, Guid createdById)
    {
        var number = await GenerateSubmissionNumberAsync();

        var submission = new Submission
        {
            SubmissionNumber = number,
            InsuredId = dto.InsuredId,
            AgentId = dto.AgentId,
            UnderwriterId = dto.UnderwriterId,
            AssistantUWId = dto.AssistantUWId,
            EffectiveDate = dto.EffectiveDate,
            ExpirationDate = dto.ExpirationDate,
            CreatedById = createdById
        };

        Db.Set<Submission>().Add(submission);
        await Db.SaveChangesAsync();

        await Db.Entry(submission).Reference(s => s.Insured).LoadAsync();
        await Db.Entry(submission).Reference(s => s.Agent).LoadAsync();
        await Db.Entry(submission).Reference(s => s.Underwriter).LoadAsync();

        return Result<SubmissionDto>.Success(MapToDto(submission));
    }

    public async Task<Result<SubmissionDto>> UpdateAsync(Guid id, SubmissionUpdateDto dto)
    {
        var submission = await Db.Set<Submission>()
            .Include(s => s.Insured)
            .Include(s => s.Agent)
            .Include(s => s.Underwriter)
            .Include(s => s.AssistantUW)
            .Include(s => s.Quotes.Where(qt => !qt.IsDeleted))
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);

        if (submission == null) return Result<SubmissionDto>.Failure("NOT_FOUND", "Submission not found.");

        submission.AgentId = dto.AgentId;
        submission.UnderwriterId = dto.UnderwriterId;
        submission.AssistantUWId = dto.AssistantUWId;
        submission.EffectiveDate = dto.EffectiveDate;
        submission.ExpirationDate = dto.ExpirationDate;
        submission.Status = dto.Status;
        submission.UpdatedAt = DateTime.UtcNow;

        await Db.SaveChangesAsync();

        // Reload nav props that may have changed
        await Db.Entry(submission).Reference(s => s.Agent).LoadAsync();
        await Db.Entry(submission).Reference(s => s.Underwriter).LoadAsync();
        await Db.Entry(submission).Reference(s => s.AssistantUW).LoadAsync();

        return Result<SubmissionDto>.Success(MapToDto(submission));
    }

    public async Task<Result> DeleteAsync(Guid id)
    {
        var submission = await Db.Set<Submission>()
            .Include(s => s.Quotes)
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
        if (submission == null) return Result.Failure("NOT_FOUND", "Submission not found.");

        if (submission.Quotes.Any(q => q.Status == QuoteStatus.Bound))
            return Result.Failure("HAS_BOUND_QUOTES", "Cannot delete a submission with bound policies.");

        submission.IsDeleted = true;
        submission.DeletedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync();
        return Result.Success();
    }

    private async Task<string> GenerateSubmissionNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"SUB-{year}-";
        var count = await Db.Set<Submission>()
            .IgnoreQueryFilters()
            .CountAsync(s => s.SubmissionNumber.StartsWith(prefix));
        return $"{prefix}{(count + 1):D4}";
    }

    private static SubmissionListItemDto MapToListItemDto(Submission s) => new()
    {
        Id = s.Id,
        SubmissionNumber = s.SubmissionNumber,
        InsuredId = s.InsuredId,
        InsuredName = s.Insured?.DisplayName ?? "",
        AgentName = s.Agent?.Name,
        UnderwriterName = s.Underwriter?.FullName ?? "",
        EffectiveDate = s.EffectiveDate,
        Status = s.Status,
        QuoteCount = s.Quotes?.Count ?? 0,
        CreatedAt = s.CreatedAt
    };

    private static SubmissionDto MapToDto(Submission s) => new()
    {
        Id = s.Id,
        SubmissionNumber = s.SubmissionNumber,
        InsuredId = s.InsuredId,
        InsuredName = s.Insured?.DisplayName ?? "",
        AgentId = s.AgentId,
        AgentName = s.Agent?.Name,
        AgencyName = s.Agent?.AgencyName,
        UnderwriterId = s.UnderwriterId,
        UnderwriterName = s.Underwriter?.FullName ?? "",
        AssistantUWId = s.AssistantUWId,
        AssistantUWName = s.AssistantUW?.FullName,
        EffectiveDate = s.EffectiveDate,
        ExpirationDate = s.ExpirationDate,
        Status = s.Status,
        LinesOfBusiness = s.LinesOfBusiness != null
            ? JsonSerializer.Deserialize<List<string>>(s.LinesOfBusiness) ?? []
            : [],
        QuoteCount = s.Quotes?.Count ?? 0,
        CreatedAt = s.CreatedAt
    };
}
