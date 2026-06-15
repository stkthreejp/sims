using SIMS.Application.DTOs.Accounting;
using SIMS.Application.DTOs.Reports;

namespace SIMS.Application.Interfaces.Services;

public interface IReportService
{
    Task<TrustReconciliationDto> GetTrustReconciliationAsync(DateOnly? asOf = null, CancellationToken ct = default);
    Task<PayableAgingDto> GetCarrierPayableAgingAsync(CancellationToken ct = default);
    Task<PayableAgingDto> GetSlTaxAgingAsync(CancellationToken ct = default);
    Task<BrokerArAgingDto> GetBrokerArAgingAsync(CancellationToken ct = default);
    Task<CommissionSummaryDto> GetCommissionSummaryAsync(int months = 12, CancellationToken ct = default);
    Task<InvoiceTotalsByPolicyTransactionDto> GetInvoiceTotalsByPolicyTransactionAsync(CancellationToken ct = default);
    Task<InvoiceTotalsByProgramDto> GetInvoiceTotalsByProgramAsync(Guid? programId = null, CancellationToken ct = default);
    Task<PostBindFollowUpDto> GetPostBindFollowUpAsync(CancellationToken ct = default);
    Task<ManagerQueueDto> GetManagerQueueAsync(CancellationToken ct = default);
    Task<UnassignedProgramCleanupDto> GetUnassignedProgramCleanupAsync(CancellationToken ct = default);
    Task<AuthorityApprovalActivityDto> GetAuthorityApprovalActivityAsync(CancellationToken ct = default);
    Task<DeclineReasonReportDto> GetDeclineReasonReportAsync(CancellationToken ct = default);
    Task<ClearanceOverrideReportDto> GetClearanceOverrideReportAsync(CancellationToken ct = default);
    Task<RenewalsUpcomingDto> GetRenewalsUpcomingAsync(int daysAhead = 90, CancellationToken ct = default);
    Task<BoundByPeriodDto> GetBoundByPeriodAsync(DateOnly? dateFrom = null, DateOnly? dateTo = null, CancellationToken ct = default);
    Task<HitRatioByCarrierDto> GetHitRatioByCarrierAsync(DateOnly? dateFrom = null, DateOnly? dateTo = null, CancellationToken ct = default);
    Task<WrittenPremiumDto> GetWrittenPremiumAsync(DateOnly? dateFrom = null, DateOnly? dateTo = null, CancellationToken ct = default);
    Task<SubmissionPipelineDto> GetSubmissionPipelineAsync(DateOnly? dateFrom = null, DateOnly? dateTo = null, CancellationToken ct = default);
    Task<UwWorkloadDto> GetUwWorkloadAsync(CancellationToken ct = default);
}
