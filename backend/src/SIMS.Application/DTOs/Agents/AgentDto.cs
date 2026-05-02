namespace SIMS.Application.DTOs.Agents;

// ─── Response DTOs ────────────────────────────────────────────────────────────

public class AgentContactDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public string? Title { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsPrimary { get; set; }
}

public class AgentLocationDto
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Phone { get; set; }
    public bool IsPrimary { get; set; }
    public List<AgentContactDto> Contacts { get; set; } = new();
}

public class AgentDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? AgencyName { get; set; }
    public string? LicenseNumber { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<AgentLocationDto> Locations { get; set; } = new();
}

public class AgentListItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? AgencyName { get; set; }
    public string? LicenseNumber { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; }
    public string? PrimaryCity { get; set; }
    public string? PrimaryState { get; set; }
    public int LocationCount { get; set; }
    public int ContactCount { get; set; }
}

// ─── Input DTOs ───────────────────────────────────────────────────────────────

public class AgentContactInputDto
{
    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public string? Title { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsPrimary { get; set; }
}

public class AgentLocationInputDto
{
    public string? Name { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Phone { get; set; }
    public bool IsPrimary { get; set; }
    public List<AgentContactInputDto> Contacts { get; set; } = new();
}

public class AgentCreateDto
{
    public string Name { get; set; } = string.Empty;
    public string? AgencyName { get; set; }
    public string? LicenseNumber { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
}

public class AgentUpdateDto : AgentCreateDto
{
    public bool IsActive { get; set; } = true;
}
