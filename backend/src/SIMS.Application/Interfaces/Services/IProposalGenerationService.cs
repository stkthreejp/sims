using SIMS.Application.Common;
using SIMS.Application.DTOs.Attachments;

namespace SIMS.Application.Interfaces.Services;

public interface IProposalGenerationService
{
    Task<Result<string>> GenerateInlandMarineHtmlAsync(Guid quoteId);
    Task<Result<GeneratedDocumentDto>> SaveInlandMarineHtmlAsync(Guid quoteId, Guid userId);
    Task<Result<GeneratedDocumentDto>> SaveInlandMarinePdfAsync(Guid quoteId, Guid userId);
    Task<Result<ProposalSendDraftDto>> CreateInlandMarineSendDraftAsync(Guid quoteId, Guid userId);
}

public sealed record ProposalSendDraftDto(
    GeneratedDocumentDto GeneratedDocument,
    Guid CommunicationId);
