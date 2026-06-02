using SIMS.Domain.Enums;

namespace SIMS.Application.DTOs.ProposalDocuments;

public record ProposalDocumentConfigurationDto(
    Guid Id,
    Guid? ProgramConfigurationId,
    string? ProgramName,
    Guid CarrierId,
    string CarrierName,
    PolicyLineOfBusiness LineOfBusiness,
    string LineOfBusinessLabel,
    string? State,
    Guid? ProgramCarrierLineOfBusinessId,
    Guid? ProgramCarrierLobStateId,
    ProposalDocumentRole Role,
    Guid DocumentTemplateId,
    string DocumentTemplateName,
    int SequenceOrder,
    bool IsActive,
    DateOnly? EffectiveDate,
    DateOnly? ExpirationDate,
    string? Notes);

public record UpsertProposalDocumentConfigurationRequest(
    Guid? ProgramConfigurationId,
    Guid CarrierId,
    PolicyLineOfBusiness LineOfBusiness,
    string? State,
    ProposalDocumentRole Role,
    Guid DocumentTemplateId,
    int SequenceOrder,
    bool IsActive,
    DateOnly? EffectiveDate,
    DateOnly? ExpirationDate,
    string? Notes);

public record ProposalDocumentSelectionDto(
    Guid QuoteId,
    string? State,
    ProposalDocumentSelectionItemDto Proposal,
    IReadOnlyList<ProposalDocumentSelectionItemDto> Notices);

public record ProposalDocumentSelectionItemDto(
    Guid ConfigurationId,
    Guid DocumentTemplateId,
    string DocumentTemplateName,
    ProposalDocumentRole Role,
    string? State,
    int SequenceOrder);
