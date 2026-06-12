using SIMS.Application.Common;
using SIMS.Application.DTOs.Agents;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace SIMS.Application.Services;

public class AgentService : IAgentService
{
    private readonly IServiceProvider _sp;
    private Microsoft.EntityFrameworkCore.DbContext Db =>
        (Microsoft.EntityFrameworkCore.DbContext)_sp.GetService(typeof(Microsoft.EntityFrameworkCore.DbContext))!;

    public AgentService(IServiceProvider sp) => _sp = sp;

    // ─── Core CRUD ────────────────────────────────────────────────────────────

    public async Task<IEnumerable<AgentListItemDto>> GetAllAsync(bool activeOnly = false)
    {
        IQueryable<Agent> q = Db.Set<Agent>()
            .Include(a => a.Locations).ThenInclude(l => l.Contacts);

        if (activeOnly) q = q.Where(a => a.IsActive);

        var agents = await q.OrderBy(a => a.Name).ToListAsync();

        return agents.Select(a =>
        {
            var primary = a.Locations.FirstOrDefault(l => l.IsPrimary) ?? a.Locations.FirstOrDefault();
            return new AgentListItemDto
            {
                Id = a.Id,
                Name = a.Name,
                AgencyName = a.AgencyName,
                LicenseNumber = a.LicenseNumber,
                Email = a.Email,
                IsActive = a.IsActive,
                PrimaryCity = primary?.City,
                PrimaryState = primary?.State,
                LocationCount = a.Locations.Count,
                ContactCount = a.Locations.Sum(l => l.Contacts.Count),
            };
        });
    }

    public async Task<Result<AgentDto>> GetByIdAsync(Guid id)
    {
        var agent = await Db.Set<Agent>()
            .Include(a => a.Locations).ThenInclude(l => l.Contacts)
            .FirstOrDefaultAsync(a => a.Id == id);

        return agent == null
            ? Result<AgentDto>.Failure("NOT_FOUND", "Agent not found.")
            : Result<AgentDto>.Success(MapToDto(agent));
    }

    public async Task<Result<AgentDto>> CreateAsync(AgentCreateDto dto)
    {
        var agent = new Agent
        {
            Name = dto.Name.Trim(),
            AgencyName = dto.AgencyName?.Trim(),
            LicenseNumber = dto.LicenseNumber?.Trim(),
            Email = dto.Email?.Trim(),
            Phone = dto.Phone?.Trim(),
        };
        Db.Set<Agent>().Add(agent);
        await Db.SaveChangesAsync();
        return Result<AgentDto>.Success(MapToDto(agent));
    }

    public async Task<Result<AgentDto>> UpdateAsync(Guid id, AgentUpdateDto dto)
    {
        var agent = await Db.Set<Agent>()
            .Include(a => a.Locations).ThenInclude(l => l.Contacts)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (agent == null) return Result<AgentDto>.Failure("NOT_FOUND", "Agent not found.");

        agent.Name = dto.Name.Trim();
        agent.AgencyName = dto.AgencyName?.Trim();
        agent.LicenseNumber = dto.LicenseNumber?.Trim();
        agent.Email = dto.Email?.Trim();
        agent.Phone = dto.Phone?.Trim();
        agent.IsActive = dto.IsActive;
        agent.UpdatedAt = DateTime.UtcNow;

        await Db.SaveChangesAsync();
        return Result<AgentDto>.Success(MapToDto(agent));
    }

    public async Task<Result> DeleteAsync(Guid id)
    {
        var agent = await Db.Set<Agent>()
            .Include(a => a.Submissions)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (agent == null) return Result.Failure("NOT_FOUND", "Agent not found.");
        if (agent.Submissions.Any())
            return Result.Failure("HAS_SUBMISSIONS", "Cannot delete an agent with existing submissions.");

        agent.IsDeleted = true;
        agent.DeletedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync();
        return Result.Success();
    }

    // ─── Locations ────────────────────────────────────────────────────────────

    public async Task<Result<AgentLocationDto>> AddLocationAsync(Guid agentId, AgentLocationInputDto dto)
    {
        var agent = await Db.Set<Agent>().FirstOrDefaultAsync(a => a.Id == agentId);
        if (agent == null) return Result<AgentLocationDto>.Failure("NOT_FOUND", "Agent not found.");

        // If this is set as primary, clear existing primary
        if (dto.IsPrimary)
            await ClearPrimaryLocations(agentId);

        var location = new AgentLocation
        {
            AgentId = agentId,
            Name = dto.Name?.Trim(),
            AddressLine1 = dto.AddressLine1?.Trim(),
            AddressLine2 = dto.AddressLine2?.Trim(),
            City = dto.City?.Trim(),
            State = dto.State?.Trim(),
            ZipCode = dto.ZipCode?.Trim(),
            Phone = dto.Phone?.Trim(),
            IsPrimary = dto.IsPrimary,
        };

        foreach (var c in dto.Contacts)
            location.Contacts.Add(MapContactInput(c));

        Db.Set<AgentLocation>().Add(location);
        await Db.SaveChangesAsync();

        return Result<AgentLocationDto>.Success(MapLocationToDto(location));
    }

    public async Task<Result<AgentLocationDto>> UpdateLocationAsync(Guid agentId, Guid locationId, AgentLocationInputDto dto)
    {
        var location = await Db.Set<AgentLocation>()
            .Include(l => l.Contacts)
            .FirstOrDefaultAsync(l => l.Id == locationId && l.AgentId == agentId);

        if (location == null) return Result<AgentLocationDto>.Failure("NOT_FOUND", "Location not found.");

        if (dto.IsPrimary && !location.IsPrimary)
            await ClearPrimaryLocations(agentId);

        location.Name = dto.Name?.Trim();
        location.AddressLine1 = dto.AddressLine1?.Trim();
        location.AddressLine2 = dto.AddressLine2?.Trim();
        location.City = dto.City?.Trim();
        location.State = dto.State?.Trim();
        location.ZipCode = dto.ZipCode?.Trim();
        location.Phone = dto.Phone?.Trim();
        location.IsPrimary = dto.IsPrimary;
        location.UpdatedAt = DateTime.UtcNow;

        await Db.SaveChangesAsync();
        return Result<AgentLocationDto>.Success(MapLocationToDto(location));
    }

    public async Task<Result> DeleteLocationAsync(Guid agentId, Guid locationId)
    {
        var location = await Db.Set<AgentLocation>()
            .FirstOrDefaultAsync(l => l.Id == locationId && l.AgentId == agentId);

        if (location == null) return Result.Failure("NOT_FOUND", "Location not found.");

        location.IsDeleted = true;
        location.DeletedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync();
        return Result.Success();
    }

    // ─── Contacts ─────────────────────────────────────────────────────────────

    public async Task<Result<AgentContactDto>> AddContactAsync(Guid agentId, Guid locationId, AgentContactInputDto dto)
    {
        var location = await Db.Set<AgentLocation>()
            .FirstOrDefaultAsync(l => l.Id == locationId && l.AgentId == agentId);

        if (location == null) return Result<AgentContactDto>.Failure("NOT_FOUND", "Location not found.");

        if (dto.IsPrimary)
            await ClearPrimaryContacts(locationId);

        var contact = MapContactInput(dto);
        contact.AgentLocationId = locationId;

        Db.Set<AgentContact>().Add(contact);
        await Db.SaveChangesAsync();

        return Result<AgentContactDto>.Success(MapContactToDto(contact));
    }

    public async Task<Result<AgentContactDto>> UpdateContactAsync(Guid agentId, Guid locationId, Guid contactId, AgentContactInputDto dto)
    {
        var contact = await Db.Set<AgentContact>()
            .FirstOrDefaultAsync(c => c.Id == contactId && c.AgentLocationId == locationId);

        if (contact == null) return Result<AgentContactDto>.Failure("NOT_FOUND", "Contact not found.");

        // Verify the location belongs to this agent
        var locationExists = await Db.Set<AgentLocation>()
            .AnyAsync(l => l.Id == locationId && l.AgentId == agentId);
        if (!locationExists) return Result<AgentContactDto>.Failure("NOT_FOUND", "Location not found.");

        if (dto.IsPrimary && !contact.IsPrimary)
            await ClearPrimaryContacts(locationId);

        contact.FirstName = dto.FirstName.Trim();
        contact.LastName = dto.LastName?.Trim();
        contact.Title = dto.Title?.Trim();
        contact.Email = dto.Email?.Trim();
        contact.Phone = dto.Phone?.Trim();
        contact.IsPrimary = dto.IsPrimary;
        contact.UpdatedAt = DateTime.UtcNow;

        await Db.SaveChangesAsync();
        return Result<AgentContactDto>.Success(MapContactToDto(contact));
    }

    public async Task<Result> DeleteContactAsync(Guid agentId, Guid locationId, Guid contactId)
    {
        var contact = await Db.Set<AgentContact>()
            .FirstOrDefaultAsync(c => c.Id == contactId && c.AgentLocationId == locationId);

        if (contact == null) return Result.Failure("NOT_FOUND", "Contact not found.");

        contact.IsDeleted = true;
        contact.DeletedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync();
        return Result.Success();
    }

    // ─── Compliance Docs ─────────────────────────────────────────────────────

    public async Task<AgentComplianceStatusDto> GetComplianceStatusAsync(Guid agentId)
    {
        var docs = await Db.Set<AgentComplianceDoc>()
            .Where(d => d.AgentId == agentId)
            .ToListAsync();

        var docDtos = Enum.GetValues<AgentComplianceDocType>()
            .Select(type =>
            {
                var doc = docs.FirstOrDefault(d => d.DocType == type);
                return doc == null
                    ? new AgentComplianceDocDto { DocType = type.ToString(), Status = "Missing" }
                    : MapComplianceDocToDto(doc);
            }).ToList();

        var missingOrExpired = docDtos
            .Where(d => d.Status is "Missing" or "Expired")
            .Select(d => d.DocType)
            .ToList();

        return new AgentComplianceStatusDto
        {
            IsQuoteReady = missingOrExpired.Count == 0,
            MissingOrExpired = missingOrExpired,
            Docs = docDtos,
        };
    }

    public async Task<Result<AgentComplianceDocDto>> UpsertComplianceDocAsync(Guid agentId, string docType, AgentComplianceDocUpsertDto dto)
    {
        if (!Enum.TryParse<AgentComplianceDocType>(docType, true, out var docTypeEnum))
            return Result<AgentComplianceDocDto>.Failure("INVALID_DOC_TYPE", $"Unknown doc type: {docType}");

        var existing = await Db.Set<AgentComplianceDoc>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.AgentId == agentId && d.DocType == docTypeEnum);

        if (existing == null)
        {
            existing = new AgentComplianceDoc { AgentId = agentId, DocType = docTypeEnum };
            Db.Set<AgentComplianceDoc>().Add(existing);
        }
        else
        {
            existing.IsDeleted = false;
            existing.DeletedAt = null;
        }

        existing.ExpirationDate = dto.ExpirationDate;
        existing.LicenseState = dto.LicenseState?.Trim();
        existing.ExecutedDate = dto.ExecutedDate;
        existing.Notes = dto.Notes?.Trim();
        existing.UpdatedAt = DateTime.UtcNow;

        await Db.SaveChangesAsync();
        return Result<AgentComplianceDocDto>.Success(MapComplianceDocToDto(existing));
    }

    public async Task<Result> DeleteComplianceDocAsync(Guid agentId, string docType)
    {
        if (!Enum.TryParse<AgentComplianceDocType>(docType, true, out var docTypeEnum))
            return Result.Failure("INVALID_DOC_TYPE", $"Unknown doc type: {docType}");

        var doc = await Db.Set<AgentComplianceDoc>()
            .FirstOrDefaultAsync(d => d.AgentId == agentId && d.DocType == docTypeEnum);

        if (doc == null) return Result.Failure("NOT_FOUND", "Compliance doc not found.");

        doc.IsDeleted = true;
        doc.DeletedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync();
        return Result.Success();
    }

    // ─── Contact Log ─────────────────────────────────────────────────────────

    public async Task<IEnumerable<AgentContactLogDto>> GetContactLogsAsync(Guid agentId)
    {
        var logs = await Db.Set<AgentContactLog>()
            .Where(l => l.AgentId == agentId)
            .OrderByDescending(l => l.LogDate)
            .ThenByDescending(l => l.CreatedAt)
            .ToListAsync();

        return logs.Select(MapContactLogToDto);
    }

    public async Task<Result<AgentContactLogDto>> CreateContactLogAsync(Guid agentId, AgentContactLogCreateDto dto, Guid userId)
    {
        var agentExists = await Db.Set<Agent>().AnyAsync(a => a.Id == agentId);
        if (!agentExists) return Result<AgentContactLogDto>.Failure("NOT_FOUND", "Agent not found.");

        if (!Enum.TryParse<AgentContactLogType>(dto.LogType, true, out var logTypeEnum))
            return Result<AgentContactLogDto>.Failure("INVALID_LOG_TYPE", $"Unknown log type: {dto.LogType}");

        var log = new AgentContactLog
        {
            AgentId = agentId,
            LogDate = dto.LogDate,
            LogType = logTypeEnum,
            ContactName = dto.ContactName?.Trim(),
            Notes = dto.Notes.Trim(),
            CreatedBy = userId,
        };

        Db.Set<AgentContactLog>().Add(log);
        await Db.SaveChangesAsync();
        return Result<AgentContactLogDto>.Success(MapContactLogToDto(log));
    }

    public async Task<Result> DeleteContactLogAsync(Guid agentId, Guid logId)
    {
        var log = await Db.Set<AgentContactLog>()
            .FirstOrDefaultAsync(l => l.Id == logId && l.AgentId == agentId);

        if (log == null) return Result.Failure("NOT_FOUND", "Contact log entry not found.");

        log.IsDeleted = true;
        log.DeletedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync();
        return Result.Success();
    }

    // ─── KPIs ────────────────────────────────────────────────────────────────

    public async Task<AgentKpiDto> GetKpiAsync(Guid agentId)
    {
        var now = DateTime.UtcNow.Date;
        var last12Start = now.AddYears(-1);
        var prior12Start = now.AddYears(-2);

        var premiums = await Db.Set<Policy>()
            .Where(p => p.Submission.AgentId == agentId && p.CreatedAt >= prior12Start)
            .Select(p => new { p.TotalPremium, p.CreatedAt })
            .ToListAsync();

        var premiumLast12 = premiums
            .Where(p => p.CreatedAt >= last12Start)
            .Sum(p => p.TotalPremium);

        var premiumPrior12 = premiums
            .Where(p => p.CreatedAt < last12Start)
            .Sum(p => p.TotalPremium);

        var quotes = await Db.Set<Quote>()
            .Where(q => q.Submission.AgentId == agentId
                && q.Status != QuoteStatus.Draft
                && q.CreatedAt >= last12Start)
            .Select(q => new { q.Status })
            .ToListAsync();

        var issued = quotes.Count;
        var bound = quotes.Count(q => q.Status == QuoteStatus.Bound);

        return new AgentKpiDto
        {
            BoundPremiumLast12Months = premiumLast12,
            BoundPremiumPrior12Months = premiumPrior12,
            QuotesIssuedLast12Months = issued,
            QuotesBoundLast12Months = bound,
            HitRatio = issued > 0 ? Math.Round((decimal)bound / issued * 100, 1) : null,
        };
    }

    // ─── Summary Stats ────────────────────────────────────────────────────────

    public async Task<AgentSummaryStatsDto> GetSummaryStatsAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var in30Days = today.AddDays(30);

        var activeAgentIds = await Db.Set<Agent>()
            .Where(a => a.IsActive)
            .Select(a => a.Id)
            .ToListAsync();

        var complianceDocs = await Db.Set<AgentComplianceDoc>()
            .Where(d => activeAgentIds.Contains(d.AgentId))
            .Select(d => new { d.AgentId, d.DocType, d.ExpirationDate })
            .ToListAsync();

        var docsByAgent = complianceDocs
            .GroupBy(d => d.AgentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var allTypes = Enum.GetValues<AgentComplianceDocType>();
        int missingCompliance = 0, eoExpiring = 0, licensesExpiring = 0;

        foreach (var agentId in activeAgentIds)
        {
            var agentDocs = docsByAgent.TryGetValue(agentId, out var docs) ? docs : new();

            bool isReady = allTypes.All(type =>
            {
                var doc = agentDocs.FirstOrDefault(d => d.DocType == type);
                return doc != null && (doc.ExpirationDate == null || doc.ExpirationDate >= today);
            });
            if (!isReady) missingCompliance++;

            var eo = agentDocs.FirstOrDefault(d => d.DocType == AgentComplianceDocType.EOCertificate);
            if (eo?.ExpirationDate is { } eoDue && eoDue >= today && eoDue <= in30Days)
                eoExpiring++;

            if (agentDocs.Any(d => d.DocType == AgentComplianceDocType.StateLicense
                    && d.ExpirationDate is { } lic && lic >= today && lic <= in30Days))
                licensesExpiring++;
        }

        return new AgentSummaryStatsDto
        {
            TotalAgents = activeAgentIds.Count,
            MissingComplianceDocs = missingCompliance,
            EOExpiringSoon = eoExpiring,
            LicensesExpiringSoon = licensesExpiring,
        };
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private async Task ClearPrimaryLocations(Guid agentId)
    {
        var primaries = await Db.Set<AgentLocation>()
            .Where(l => l.AgentId == agentId && l.IsPrimary)
            .ToListAsync();
        foreach (var l in primaries) l.IsPrimary = false;
    }

    private async Task ClearPrimaryContacts(Guid locationId)
    {
        var primaries = await Db.Set<AgentContact>()
            .Where(c => c.AgentLocationId == locationId && c.IsPrimary)
            .ToListAsync();
        foreach (var c in primaries) c.IsPrimary = false;
    }

    private static AgentContact MapContactInput(AgentContactInputDto dto) => new()
    {
        FirstName = dto.FirstName.Trim(),
        LastName = dto.LastName?.Trim(),
        Title = dto.Title?.Trim(),
        Email = dto.Email?.Trim(),
        Phone = dto.Phone?.Trim(),
        IsPrimary = dto.IsPrimary,
    };

    private static AgentContactDto MapContactToDto(AgentContact c) => new()
    {
        Id = c.Id,
        FirstName = c.FirstName,
        LastName = c.LastName,
        Title = c.Title,
        Email = c.Email,
        Phone = c.Phone,
        IsPrimary = c.IsPrimary,
    };

    private static AgentLocationDto MapLocationToDto(AgentLocation l) => new()
    {
        Id = l.Id,
        Name = l.Name,
        AddressLine1 = l.AddressLine1,
        AddressLine2 = l.AddressLine2,
        City = l.City,
        State = l.State,
        ZipCode = l.ZipCode,
        Phone = l.Phone,
        IsPrimary = l.IsPrimary,
        Contacts = l.Contacts.Select(MapContactToDto).ToList(),
    };

    private static string ComputeComplianceStatus(DateOnly? expirationDate)
    {
        if (expirationDate == null) return "Current";
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        if (expirationDate < today) return "Expired";
        if (expirationDate <= today.AddDays(30)) return "ExpiringSoon";
        return "Current";
    }

    private static AgentComplianceDocDto MapComplianceDocToDto(AgentComplianceDoc d) => new()
    {
        Id = d.Id,
        DocType = d.DocType.ToString(),
        ExpirationDate = d.ExpirationDate,
        LicenseState = d.LicenseState,
        ExecutedDate = d.ExecutedDate,
        Notes = d.Notes,
        Status = ComputeComplianceStatus(d.ExpirationDate),
    };

    private static AgentContactLogDto MapContactLogToDto(AgentContactLog l) => new()
    {
        Id = l.Id,
        LogDate = l.LogDate,
        LogType = l.LogType.ToString(),
        ContactName = l.ContactName,
        Notes = l.Notes,
        CreatedByName = string.Empty,
        CreatedAt = l.CreatedAt,
    };

    private static AgentDto MapToDto(Agent a) => new()
    {
        Id = a.Id,
        Name = a.Name,
        AgencyName = a.AgencyName,
        LicenseNumber = a.LicenseNumber,
        Email = a.Email,
        Phone = a.Phone,
        IsActive = a.IsActive,
        CreatedAt = a.CreatedAt,
        Locations = a.Locations.OrderByDescending(l => l.IsPrimary).ThenBy(l => l.Name).Select(MapLocationToDto).ToList(),
    };
}
