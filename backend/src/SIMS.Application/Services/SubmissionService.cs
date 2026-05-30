using System.Text.Json;
using SIMS.Application.Common;
using SIMS.Application.DTOs.Submissions;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Security;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace SIMS.Application.Services;

public class SubmissionService : ISubmissionService
{
    private readonly IServiceProvider _sp;
    private readonly IWorkflowEngineService _workflowEngine;
    private Microsoft.EntityFrameworkCore.DbContext Db =>
        (Microsoft.EntityFrameworkCore.DbContext)_sp.GetService(typeof(Microsoft.EntityFrameworkCore.DbContext))!;

    public SubmissionService(IServiceProvider sp, IWorkflowEngineService workflowEngine)
    {
        _sp = sp;
        _workflowEngine = workflowEngine;
    }

    public async Task<PagedResult<SubmissionListItemDto>> GetAllAsync(QueryParameters query, UserAccessScope access)
    {
        var q = Db.Set<Submission>()
            .Include(s => s.Insured)
            .Include(s => s.Agent)
            .Include(s => s.Underwriter)
            .Include(s => s.Quotes.Where(qt => !qt.IsDeleted))
            .Where(s => !s.IsDeleted)
            .ForAccessScope(access)
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

    public async Task<IEnumerable<SubmissionListItemDto>> GetByInsuredAsync(Guid insuredId, UserAccessScope access)
    {
        var submissions = await Db.Set<Submission>()
            .Include(s => s.Agent)
            .Include(s => s.Underwriter)
            .Include(s => s.Quotes.Where(qt => !qt.IsDeleted))
            .Where(s => s.InsuredId == insuredId && !s.IsDeleted)
            .ForAccessScope(access)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return submissions.Select(MapToListItemDto);
    }

    public async Task<Result<SubmissionDto>> GetByIdAsync(Guid id, UserAccessScope access)
    {
        var submission = await Db.Set<Submission>()
            .Include(s => s.Insured)
            .Include(s => s.Agent)
            .Include(s => s.Underwriter)
            .Include(s => s.AssistantUW)
            .Include(s => s.Quotes.Where(qt => !qt.IsDeleted))
            .Where(s => s.Id == id && !s.IsDeleted)
            .ForAccessScope(access)
            .FirstOrDefaultAsync();

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
            DescriptionOfOperations = dto.DescriptionOfOperations,
            LinesOfBusiness = dto.LinesOfBusiness.Count > 0
                ? JsonSerializer.Serialize(dto.LinesOfBusiness.Distinct().ToList())
                : null,
            RenewingPolicyId = dto.RenewingPolicyId,
            CreatedById = createdById
        };

        Db.Set<Submission>().Add(submission);
        await Db.SaveChangesAsync();

        await Db.Entry(submission).Reference(s => s.Insured).LoadAsync();
        await Db.Entry(submission).Reference(s => s.Agent).LoadAsync();
        await Db.Entry(submission).Reference(s => s.Underwriter).LoadAsync();

        await _workflowEngine.FireEventAsync(
            "submission.created",
            TaskEntityType.Submission,
            submission.Id,
            BuildSubmissionContext(submission));

        return Result<SubmissionDto>.Success(MapToDto(submission));
    }

    public async Task<Result<SubmissionDto>> UpdateAsync(Guid id, SubmissionUpdateDto dto, UserAccessScope access)
    {
        var submission = await Db.Set<Submission>()
            .Include(s => s.Insured)
            .Include(s => s.Agent)
            .Include(s => s.Underwriter)
            .Include(s => s.AssistantUW)
            .Include(s => s.Quotes.Where(qt => !qt.IsDeleted))
            .Where(s => s.Id == id && !s.IsDeleted)
            .ForAccessScope(access)
            .FirstOrDefaultAsync();

        if (submission == null) return Result<SubmissionDto>.Failure("NOT_FOUND", "Submission not found.");

        var previousStatus = submission.Status;

        submission.AgentId = dto.AgentId;
        submission.UnderwriterId = dto.UnderwriterId;
        submission.AssistantUWId = dto.AssistantUWId;
        submission.EffectiveDate = dto.EffectiveDate;
        submission.ExpirationDate = dto.ExpirationDate;
        submission.DescriptionOfOperations = dto.DescriptionOfOperations;
        submission.Status = dto.Status;
        submission.LinesOfBusiness = dto.LinesOfBusiness.Count > 0
            ? JsonSerializer.Serialize(dto.LinesOfBusiness.Distinct().ToList())
            : null;
        submission.UpdatedAt = DateTime.UtcNow;

        await Db.SaveChangesAsync();

        // Reload nav props that may have changed
        await Db.Entry(submission).Reference(s => s.Agent).LoadAsync();
        await Db.Entry(submission).Reference(s => s.Underwriter).LoadAsync();
        await Db.Entry(submission).Reference(s => s.AssistantUW).LoadAsync();

        // Fire workflow event on status transitions
        if (dto.Status != previousStatus)
        {
            var eventName = dto.Status switch
            {
                SubmissionStatus.InProgress => "submission.status.inprogress",
                SubmissionStatus.Quoted     => "submission.status.quoted",
                SubmissionStatus.Bound      => "submission.status.bound",
                SubmissionStatus.Declined   => "submission.status.declined",
                SubmissionStatus.Withdrawn  => "submission.status.withdrawn",
                _                           => null
            };

            if (eventName != null)
                await _workflowEngine.FireEventAsync(
                    eventName,
                    TaskEntityType.Submission,
                    submission.Id,
                    BuildSubmissionContext(submission));
        }

        return Result<SubmissionDto>.Success(MapToDto(submission));
    }

    public async Task<Result<SubmissionDto>> SetLinesOfBusinessAsync(Guid id, List<string> lobs, UserAccessScope access)
    {
        var submission = await Db.Set<Submission>()
            .Include(s => s.Insured)
            .Include(s => s.Agent)
            .Include(s => s.Underwriter)
            .Include(s => s.AssistantUW)
            .Include(s => s.Quotes.Where(qt => !qt.IsDeleted))
            .Where(s => s.Id == id && !s.IsDeleted)
            .ForAccessScope(access)
            .FirstOrDefaultAsync();

        if (submission == null)
            return Result<SubmissionDto>.Failure("NOT_FOUND", "Submission not found.");

        submission.LinesOfBusiness = lobs.Count > 0
            ? JsonSerializer.Serialize(lobs.Distinct().ToList())
            : null;
        submission.UpdatedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync();

        return Result<SubmissionDto>.Success(MapToDto(submission));
    }

    public async Task<Result> DeleteAsync(Guid id, UserAccessScope access)
    {
        var submission = await Db.Set<Submission>()
            .Include(s => s.Quotes)
            .Where(s => s.Id == id && !s.IsDeleted)
            .ForAccessScope(access)
            .FirstOrDefaultAsync();
        if (submission == null) return Result.Failure("NOT_FOUND", "Submission not found.");

        if (submission.Quotes.Any(q => q.Status == QuoteStatus.Bound))
            return Result.Failure("HAS_BOUND_QUOTES", "Cannot delete a submission with bound policies.");

        submission.IsDeleted = true;
        submission.DeletedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync();
        return Result.Success();
    }

    private static Dictionary<string, object> BuildSubmissionContext(Submission s)
    {
        var ctx = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["UnderwriterId"] = s.UnderwriterId,
            ["Status"]        = s.Status.ToString(),
        };
        if (s.AssistantUWId.HasValue) ctx["AssistantUWId"] = s.AssistantUWId.Value;
        if (s.AgentId.HasValue)       ctx["AgentId"]       = s.AgentId.Value;
        if (s.EffectiveDate.HasValue)  ctx["EffectiveDate"] = s.EffectiveDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        if (s.ExpirationDate.HasValue) ctx["ExpirationDate"] = s.ExpirationDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        return ctx;
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
        AgencyName = s.Agent?.AgencyName,
        UnderwriterName = s.Underwriter?.FullName ?? "",
        EffectiveDate = s.EffectiveDate,
        Status = s.Status,
        LinesOfBusiness = string.IsNullOrWhiteSpace(s.LinesOfBusiness)
            ? []
            : JsonSerializer.Deserialize<List<string>>(s.LinesOfBusiness) ?? [],
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
        DescriptionOfOperations = s.DescriptionOfOperations,
        LinesOfBusiness = string.IsNullOrWhiteSpace(s.LinesOfBusiness)
            ? []
            : JsonSerializer.Deserialize<List<string>>(s.LinesOfBusiness) ?? [],
        RenewingPolicyId = s.RenewingPolicyId,
        QuoteCount = s.Quotes?.Count ?? 0,
        CreatedAt = s.CreatedAt
    };
}
