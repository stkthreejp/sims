using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIMS.Application.Interfaces.Services;
using SIMS.Infrastructure.Data;

namespace SIMS.API.Controllers.Billing;

[ApiController]
[Route("api/v1/billing/xero")]
[Authorize(Policy = AppPermissions.AccountingManage)]
public class XeroController : ControllerBase
{
    private readonly IXeroTokenService _tokens;
    private readonly IXeroApiClient _api;
    private readonly ApplicationDbContext _db;

    public XeroController(IXeroTokenService tokens, IXeroApiClient api, ApplicationDbContext db)
    {
        _tokens = tokens;
        _api = api;
        _db = db;
    }

    /// <summary>Returns Xero connection status and the pending sync retry queue.</summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var connected = await _tokens.IsConnectedAsync(ct);

        var pending = await _db.PendingJournalSyncs
            .Include(p => p.Rollup)
            .Where(p => p.TenantId == 1 && p.Status != "Done" && p.Rollup!.DriverType == "Xero")
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                p.Id,
                p.RollupId,
                Period = $"{p.Rollup!.PeriodYear}-{p.Rollup.PeriodMonth:D2}",
                p.Status,
                p.AttemptCount,
                p.NextRetryAt,
                p.LastError,
                p.CreatedAt,
            })
            .ToListAsync(ct);

        return Ok(new { connected, pending });
    }

    /// <summary>Returns the Xero chart of accounts for GL mapping setup.</summary>
    [HttpGet("accounts")]
    [Authorize(Policy = AppPermissions.AccountingAdmin)]
    public async Task<IActionResult> GetAccounts(CancellationToken ct)
    {
        if (!await _tokens.IsConnectedAsync(ct))
            return BadRequest(new { ErrorCode = "XERO_NOT_CONNECTED", ErrorMessage = "Xero is not connected." });

        try
        {
            var accounts = await _api.GetChartOfAccountsAsync(ct);
            return Ok(accounts);
        }
        catch (Exception ex)
        {
            return BadRequest(new { ErrorCode = "XERO_ERROR", ErrorMessage = ex.Message });
        }
    }
}
