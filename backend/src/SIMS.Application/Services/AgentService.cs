using SIMS.Application.Common;
using SIMS.Application.DTOs.Agents;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
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
