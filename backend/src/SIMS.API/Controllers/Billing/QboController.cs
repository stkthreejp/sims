using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIMS.Application.Interfaces.Services;
using SIMS.Infrastructure.Data;

namespace SIMS.API.Controllers.Billing;

[ApiController]
[Route("api/v1/billing/qbo")]
[Authorize(Roles = "Admin,Underwriter")]
public class QboController : ControllerBase
{
    private readonly IQboTokenService _tokens;
    private readonly IQboApiClient _api;
    private readonly ApplicationDbContext _db;

    public QboController(IQboTokenService tokens, IQboApiClient api, ApplicationDbContext db)
    {
        _tokens = tokens;
        _api = api;
        _db = db;
    }

    /// <summary>Returns QBO connection status and pending retry queue.</summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var connected = await _tokens.IsConnectedAsync(ct);

        var pending = await _db.PendingQboSyncs
            .Include(p => p.Rollup)
            .Where(p => p.TenantId == 1 && p.Status != "Done")
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

    /// <summary>Returns QBO chart of accounts for GL mapping setup.</summary>
    [HttpGet("accounts")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAccounts(CancellationToken ct)
    {
        if (!await _tokens.IsConnectedAsync(ct))
            return BadRequest(new { ErrorCode = "QBO_NOT_CONNECTED", ErrorMessage = "QBO is not connected." });

        try
        {
            var accounts = await _api.GetChartOfAccountsAsync(ct);
            return Ok(accounts);
        }
        catch (Exception ex)
        {
            return BadRequest(new { ErrorCode = "QBO_ERROR", ErrorMessage = ex.Message });
        }
    }
}
