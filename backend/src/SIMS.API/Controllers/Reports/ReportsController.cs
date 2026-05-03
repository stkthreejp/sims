using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIMS.Application.Interfaces.Services;

namespace SIMS.API.Controllers.Reports;

[ApiController]
[Route("api/v1/reports")]
[Authorize(Roles = "Admin,Underwriter")]
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
}
