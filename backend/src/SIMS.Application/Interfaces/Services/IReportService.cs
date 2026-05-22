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
    Task<InvoiceTotalsByProgramDto> GetInvoiceTotalsByProgramAsync(CancellationToken ct = default);
}
