namespace SIMS.Application.DTOs.Underwriting;

public record ProgramConfigurationDto(
    Guid Id,
    string Name,
    string Code,
    bool IsActive,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreateProgramConfigurationRequest(
    string Name,
    string Code,
    bool IsActive,
    string? Notes);

public record UpdateProgramConfigurationRequest(
    string Name,
    string Code,
    bool IsActive,
    string? Notes);
