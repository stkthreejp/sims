using SIMS.Application.Common;
using SIMS.Application.DTOs.Attachments;
using SIMS.Application.DTOs.Policies;
using SIMS.Application.DTOs.Quotes;
using SIMS.Application.Security;

namespace SIMS.Application.Interfaces.Services;

public interface IPolicyService
{
    Task<PagedResult<PolicyListItemDto>> GetAllAsync(QueryParameters query, UserAccessScope access);
    Task<IEnumerable<PolicyListItemDto>> GetByInsuredAsync(Guid insuredId, UserAccessScope access);
    Task<Result<PolicyDto>> GetByIdAsync(Guid id, UserAccessScope access);
    Task<Result<PolicyTransactionArtifactsDto>> GetTransactionArtifactsAsync(Guid policyId, Guid transactionId, UserAccessScope access);
    Task<Result<PolicyIssuancePacketDto>> GetIssuancePacketAsync(Guid policyId, UserAccessScope access);
    Task<Result<GeneratedDocumentDto>> GenerateIssuancePacketPreviewAsync(Guid policyId, UserAccessScope access);
    Task<Result<PolicyDto>> IssueAsync(Guid policyId, IssuePolicyDto dto, UserAccessScope access);
    Task<Result<VoidTestBindResultDto>> VoidTestBindAsync(Guid policyId, VoidTestBindDto dto, UserAccessScope access, bool isAdmin);

    Task<Result<PolicyTransactionDto>> AddEndorsementAsync(Guid policyId, CreateEndorsementDto dto, UserAccessScope access);
    Task<Result<PolicyTransactionDto>> IssueEndorsementAsync(Guid policyId, Guid txnId, IssueEndorsementDto dto, UserAccessScope access);

    Task<Result<QuoteDto>> CreateRenewalQuoteAsync(Guid policyId, UserAccessScope access);

    Task<Result<PolicyDto>> CancelAsync(Guid policyId, CancelPolicyDto dto, UserAccessScope access);
    Task<Result<PolicyTransactionDto>> IssueCancellationNoticeAsync(Guid policyId, IssueCancellationNoticeDto dto, UserAccessScope access);
    Task<Result<PolicyDto>> CompleteCancellationAsync(Guid policyId, Guid transactionId, CompleteCancellationDto dto, UserAccessScope access);
    Task<Result<PolicyDto>> ReinstateAsync(Guid policyId, ReinstatePolicyDto dto, UserAccessScope access);
    Task<Result<PolicyDto>> NonRenewAsync(Guid policyId, NonRenewPolicyDto dto, UserAccessScope access);
    Task<Result<PolicyDto>> CompleteNonRenewalAsync(Guid policyId, Guid transactionId, CompleteNonRenewalDto dto, UserAccessScope access);
    Task<Result<LegalComplianceGuidanceDto>> GetCancellationGuidanceAsync(Guid policyId, UserAccessScope access);
    Task<Result<LegalComplianceGuidanceDto>> GetNonRenewalGuidanceAsync(Guid policyId, UserAccessScope access);
}
