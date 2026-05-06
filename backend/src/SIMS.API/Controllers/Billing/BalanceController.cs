using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIMS.Domain.Entities.Accounting;

namespace SIMS.API.Controllers.Billing;

[ApiController]
[Route("api/v1/billing/balance")]
[Authorize(Policy = AppPermissions.AccountingManage)]
public class BalanceController : ControllerBase
{
    private readonly DbContext _db;
    public BalanceController(DbContext db) => _db = db;

    [HttpGet("trust")]
    public async Task<IActionResult> GetTrustBalance(CancellationToken ct)
    {
        var account = await _db.Set<LedgerAccount>()
            .FirstOrDefaultAsync(a => a.InternalCode == "1100" && a.TenantId == 1, ct);

        if (account == null)
            return NotFound(new { ErrorMessage = "Trust account (1100) not found" });

        var balance = await _db.Set<LedgerTransaction>()
            .Where(t => t.AccountId == account.Id && t.TenantId == 1 && t.PostingStatus == "Posted")
            .SumAsync(t => t.Debit - t.Credit, ct);

        return Ok(new { balance, accountLabel = account.ExternalLabel });
    }
}
