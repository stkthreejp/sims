using SIMS.Domain.Enums;

namespace SIMS.Application.DTOs.Underwriting;

public record ProgramConfigurationDto(
    Guid Id,
    string Name,
    string Code,
    bool IsActive,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<ProgramCarrierDto> Carriers);

public record ProgramCarrierDto(
    Guid Id,
    Guid ProgramConfigurationId,
    Guid CarrierId,
    string CarrierName,
    bool IsActive,
    DateOnly EffectiveDate,
    DateOnly? ExpirationDate,
    string? Notes,
    IReadOnlyList<ProgramCarrierLineOfBusinessDto> LinesOfBusiness);

public record ProgramCarrierLineOfBusinessDto(
    Guid Id,
    Guid ProgramCarrierId,
    PolicyLineOfBusiness LineOfBusiness,
    string LineOfBusinessLabel,
    bool IsActive,
    DateOnly EffectiveDate,
    DateOnly? ExpirationDate,
    string? Notes,
    string? BillingMode,
    int? PaymentTermsDays,
    IReadOnlyList<ProgramCarrierLobStateDto> States);

public record ProgramCarrierLobStateDto(
    Guid Id,
    Guid ProgramCarrierLineOfBusinessId,
    string StateCode,
    bool IsActive,
    DateOnly EffectiveDate,
    DateOnly? ExpirationDate,
    string? Notes);

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

public record UpsertProgramCarrierRequest(
    Guid CarrierId,
    bool IsActive,
    DateOnly EffectiveDate,
    DateOnly? ExpirationDate,
    string? Notes);

public record UpsertProgramCarrierLineOfBusinessRequest(
    PolicyLineOfBusiness LineOfBusiness,
    bool IsActive,
    DateOnly EffectiveDate,
    DateOnly? ExpirationDate,
    string? Notes,
    string? BillingMode = null,
    int? PaymentTermsDays = null);

public record UpsertProgramCarrierLobStateRequest(
    string StateCode,
    bool IsActive,
    DateOnly EffectiveDate,
    DateOnly? ExpirationDate,
    string? Notes);

public record CopyProgramCarrierLobStateRequest(
    string SourceStateCode,
    string TargetStateCode);
