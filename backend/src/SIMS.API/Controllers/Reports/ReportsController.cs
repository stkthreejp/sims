using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIMS.Application.Interfaces.Services;

namespace SIMS.API.Controllers.Reports;

[ApiController]
[Route("api/v1/reports")]
[Authorize(Policy = AppPermissions.ReportsView)]
public class ReportsController : ControllerBase
{
    private readonly IReportService _svc;
    public ReportsController(IReportService svc) => _svc = svc;

    [HttpGet("accounting/trust-reconciliation")]
    public async Task<IActionResult> GetTrustReconciliation(
        [FromQuery] DateOnly? asOf, CancellationToken ct)
        => Ok(await _svc.GetTrustReconciliationAsync(asOf, ct));

    [HttpGet("accounting/carrier-payable-aging")]
    public async Task<IActionResult> GetCarrierPayableAging(CancellationToken ct)
        => Ok(await _svc.GetCarrierPayableAgingAsync(ct));

    [HttpGet("accounting/sl-tax-aging")]
    public async Task<IActionResult> GetSlTaxAging(CancellationToken ct)
        => Ok(await _svc.GetSlTaxAgingAsync(ct));

    [HttpGet("accounting/broker-ar-aging")]
    public async Task<IActionResult> GetBrokerArAging(CancellationToken ct)
        => Ok(await _svc.GetBrokerArAgingAsync(ct));

    [HttpGet("accounting/commission-summary")]
    public async Task<IActionResult> GetCommissionSummary(
        [FromQuery] int months = 12, CancellationToken ct = default)
        => Ok(await _svc.GetCommissionSummaryAsync(months, ct));

    [HttpGet("accounting/invoice-totals-by-policy-transaction")]
    public async Task<IActionResult> GetInvoiceTotalsByPolicyTransaction(CancellationToken ct)
        => Ok(await _svc.GetInvoiceTotalsByPolicyTransactionAsync(ct));

    [HttpGet("accounting/invoice-totals-by-program")]
    public async Task<IActionResult> GetInvoiceTotalsByProgram(
        [FromQuery] Guid? programId, CancellationToken ct)
        => Ok(await _svc.GetInvoiceTotalsByProgramAsync(programId, ct));

    [HttpGet("operations/post-bind-follow-up")]
    public async Task<IActionResult> GetPostBindFollowUp(CancellationToken ct)
        => Ok(await _svc.GetPostBindFollowUpAsync(ct));

    [HttpGet("operations/manager-queue")]
    public async Task<IActionResult> GetManagerQueue(CancellationToken ct)
        => Ok(await _svc.GetManagerQueueAsync(ct));

    [HttpGet("operations/unassigned-program-cleanup")]
    public async Task<IActionResult> GetUnassignedProgramCleanup(CancellationToken ct)
        => Ok(await _svc.GetUnassignedProgramCleanupAsync(ct));

    [HttpGet("operations/authority-approvals")]
    public async Task<IActionResult> GetAuthorityApprovalActivity(CancellationToken ct)
        => Ok(await _svc.GetAuthorityApprovalActivityAsync(ct));

    [HttpGet("operations/decline-reasons")]
    public async Task<IActionResult> GetDeclineReasonReport(CancellationToken ct)
        => Ok(await _svc.GetDeclineReasonReportAsync(ct));

    [HttpGet("operations/clearance-overrides")]
    public async Task<IActionResult> GetClearanceOverrideReport(CancellationToken ct)
        => Ok(await _svc.GetClearanceOverrideReportAsync(ct));

    [HttpGet("production/renewals-upcoming")]
    public async Task<IActionResult> GetRenewalsUpcoming(
        [FromQuery] int daysAhead = 90, CancellationToken ct = default)
        => Ok(await _svc.GetRenewalsUpcomingAsync(daysAhead, ct));

    [HttpGet("production/bound-by-period")]
    public async Task<IActionResult> GetBoundByPeriod(
        [FromQuery] DateOnly? dateFrom, [FromQuery] DateOnly? dateTo, CancellationToken ct = default)
        => Ok(await _svc.GetBoundByPeriodAsync(dateFrom, dateTo, ct));

    [HttpGet("production/hit-ratio-by-carrier")]
    public async Task<IActionResult> GetHitRatioByCarrier(
        [FromQuery] DateOnly? dateFrom, [FromQuery] DateOnly? dateTo, CancellationToken ct = default)
        => Ok(await _svc.GetHitRatioByCarrierAsync(dateFrom, dateTo, ct));

    [HttpGet("production/written-premium")]
    public async Task<IActionResult> GetWrittenPremium(
        [FromQuery] DateOnly? dateFrom, [FromQuery] DateOnly? dateTo, CancellationToken ct = default)
        => Ok(await _svc.GetWrittenPremiumAsync(dateFrom, dateTo, ct));

    [HttpGet("production/submission-pipeline")]
    public async Task<IActionResult> GetSubmissionPipeline(
        [FromQuery] DateOnly? dateFrom, [FromQuery] DateOnly? dateTo, CancellationToken ct = default)
        => Ok(await _svc.GetSubmissionPipelineAsync(dateFrom, dateTo, ct));

    [HttpGet("production/uw-workload")]
    public async Task<IActionResult> GetUwWorkload(CancellationToken ct)
        => Ok(await _svc.GetUwWorkloadAsync(ct));
}
