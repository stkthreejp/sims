namespace SIMS.Application.DTOs.Agents;

// ─── Compliance docs ──────────────────────────────────────────────────────────

public class AgentComplianceDocDto
{
    public Guid Id { get; set; }
    public string DocType { get; set; } = string.Empty;
    public DateOnly? ExpirationDate { get; set; }
    public string? LicenseState { get; set; }
    public DateOnly? ExecutedDate { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = string.Empty; // Current | ExpiringSoon | Expired | Missing
}

public class AgentComplianceDocUpsertDto
{
    public DateOnly? ExpirationDate { get; set; }
    public string? LicenseState { get; set; }
    public DateOnly? ExecutedDate { get; set; }
    public string? Notes { get; set; }
}

public class AgentComplianceStatusDto
{
    public bool IsQuoteReady { get; set; }
    public List<string> MissingOrExpired { get; set; } = new();
    public List<AgentComplianceDocDto> Docs { get; set; } = new();
}

// ─── Contact log ─────────────────────────────────────────────────────────────

public class AgentContactLogDto
{
    public Guid Id { get; set; }
    public DateOnly LogDate { get; set; }
    public string LogType { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string Notes { get; set; } = string.Empty;
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class AgentContactLogCreateDto
{
    public DateOnly LogDate { get; set; }
    public string LogType { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string Notes { get; set; } = string.Empty;
}

// ─── KPIs ────────────────────────────────────────────────────────────────────

public class AgentKpiDto
{
    public decimal BoundPremiumLast12Months { get; set; }
    public decimal? BoundPremiumPrior12Months { get; set; }
    public int QuotesIssuedLast12Months { get; set; }
    public int QuotesBoundLast12Months { get; set; }
    public decimal? HitRatio { get; set; } // null when no quotes issued
}

// ─── Summary stats (list page) ───────────────────────────────────────────────

public class AgentSummaryStatsDto
{
    public int TotalAgents { get; set; }
    public int MissingComplianceDocs { get; set; }
    public int EOExpiringSoon { get; set; }
    public int LicensesExpiringSoon { get; set; }
}

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
