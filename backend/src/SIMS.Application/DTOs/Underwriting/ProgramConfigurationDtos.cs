using SIMS.Domain.Enums;

namespace SIMS.Application.DTOs.Underwriting;

public record ProgramConfigurationDto(
    Guid Id,
    string Name,
    string Code,
    Guid? CarrierId,
    string? CarrierName,
    PolicyLineOfBusiness LineOfBusiness,
    string StateCode,
    bool IsActive,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreateProgramConfigurationRequest(
    string Name,
    string Code,
    Guid? CarrierId,
    PolicyLineOfBusiness LineOfBusiness,
    string StateCode,
    bool IsActive,
    string? Notes);

public record UpdateProgramConfigurationRequest(
    string Name,
    string Code,
    Guid? CarrierId,
    PolicyLineOfBusiness LineOfBusiness,
    string StateCode,
    bool IsActive,
    string? Notes);
