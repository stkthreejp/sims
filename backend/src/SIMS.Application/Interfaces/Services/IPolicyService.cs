using SIMS.Application.Common;
using SIMS.Application.DTOs.Policies;
using SIMS.Application.DTOs.Quotes;

namespace SIMS.Application.Interfaces.Services;

public interface IPolicyService
{
    Task<PagedResult<PolicyListItemDto>> GetAllAsync(QueryParameters query);
    Task<IEnumerable<PolicyListItemDto>> GetByInsuredAsync(Guid insuredId);
    Task<Result<PolicyDto>> GetByIdAsync(Guid id);

    Task<Result<PolicyTransactionDto>> AddEndorsementAsync(Guid policyId, CreateEndorsementDto dto, Guid userId);
    Task<Result<PolicyTransactionDto>> IssueEndorsementAsync(Guid policyId, Guid txnId, IssueEndorsementDto dto, Guid userId);

    Task<Result<QuoteDto>> CreateRenewalQuoteAsync(Guid policyId, Guid userId);

    Task<Result<PolicyDto>> NonRenewAsync(Guid policyId, NonRenewPolicyDto dto, Guid userId);
}
