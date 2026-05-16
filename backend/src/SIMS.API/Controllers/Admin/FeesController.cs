using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIMS.Application.DTOs.Accounting;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities.Accounting;
using System.Security.Claims;

namespace SIMS.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/fees")]
[Authorize(Policy = AppPermissions.AdminSystemManage)]
public class FeesController : ControllerBase
{
    private readonly IFeeAdminService _svc;
    private readonly DbContext _db;
    public FeesController(IFeeAdminService svc, DbContext db)
    {
        _svc = svc;
        _db = db;
    }

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // --- Fee Definitions ---

    [HttpGet("ledger-accounts")]
    public async Task<IActionResult> GetLedgerAccounts(CancellationToken ct)
    {
        var accounts = await _db.Set<LedgerAccount>()
            .Where(a => a.TenantId == 1 && a.IsActive)
            .OrderBy(a => a.InternalCode)
            .Select(a => new LedgerAccountOptionDto(a.Id, a.InternalCode, a.ExternalLabel, a.AccountType))
            .ToListAsync(ct);

        return Ok(accounts);
    }

    [HttpGet("definitions")]
    public async Task<IActionResult> GetDefinitions(CancellationToken ct)
        => Ok(await _svc.GetDefinitionsAsync(ct));

    [HttpGet("definitions/{id:long}")]
    public async Task<IActionResult> GetDefinition(long id, CancellationToken ct)
    {
        var r = await _svc.GetDefinitionAsync(id, ct);
        return r.IsSuccess ? Ok(r.Value) : NotFound(new { r.ErrorMessage });
    }

    [HttpPost("definitions")]
    public async Task<IActionResult> CreateDefinition([FromBody] CreateFeeDefinitionRequest req, CancellationToken ct)
    {
        var r = await _svc.CreateDefinitionAsync(req, ct);
        if (!r.IsSuccess) return BadRequest(new { r.ErrorCode, r.ErrorMessage });
        return CreatedAtAction(nameof(GetDefinition), new { id = r.Value!.Id }, r.Value);
    }

    // --- Fee Rule Versions ---

    [HttpGet("definitions/{feeDefinitionId:long}/versions")]
    public async Task<IActionResult> GetVersions(long feeDefinitionId, CancellationToken ct)
        => Ok(await _svc.GetVersionsAsync(feeDefinitionId, ct));

    [HttpGet("versions/{id:long}")]
    public async Task<IActionResult> GetVersion(long id, CancellationToken ct)
    {
        var r = await _svc.GetVersionAsync(id, ct);
        return r.IsSuccess ? Ok(r.Value) : NotFound(new { r.ErrorMessage });
    }

    [HttpPost("versions")]
    public async Task<IActionResult> CreateVersion([FromBody] CreateFeeRuleVersionRequest req, CancellationToken ct)
    {
        var r = await _svc.CreateVersionAsync(UserId, req, ct);
        if (!r.IsSuccess) return BadRequest(new { r.ErrorCode, r.ErrorMessage });
        return CreatedAtAction(nameof(GetVersion), new { id = r.Value!.Id }, r.Value);
    }

    [HttpPost("versions/{id:long}/new-version")]
    public async Task<IActionResult> NewVersionFromExisting(long id, [FromBody] CreateFeeRuleVersionRequest req, CancellationToken ct)
    {
        var r = await _svc.NewVersionFromExistingAsync(UserId, id, req, ct);
        if (!r.IsSuccess) return BadRequest(new { r.ErrorCode, r.ErrorMessage });
        return CreatedAtAction(nameof(GetVersion), new { id = r.Value!.Id }, r.Value);
    }

    [HttpPost("versions/{id:long}/disable")]
    public async Task<IActionResult> DisableVersion(long id, [FromBody] DisableVersionRequest req, CancellationToken ct)
    {
        var r = await _svc.DisableVersionAsync(UserId, id, req.DisabledDate, req.Notes, ct);
        return r.IsSuccess ? NoContent() : BadRequest(new { r.ErrorCode, r.ErrorMessage });
    }

    // --- State Taxability ---

    [HttpPut("definitions/{feeDefinitionId:long}/state-taxability")]
    public async Task<IActionResult> SetStateTaxability(long feeDefinitionId, [FromBody] SetStateTaxabilityRequest req, CancellationToken ct)
    {
        var r = await _svc.SetStateTaxabilityAsync(feeDefinitionId, req, ct);
        return r.IsSuccess ? NoContent() : BadRequest(new { r.ErrorCode, r.ErrorMessage });
    }

    // --- Audit Log ---

    [HttpGet("versions/{id:long}/audit-log")]
    [Authorize(Policy = AppPermissions.AdminSystemManage)]
    public async Task<IActionResult> GetAuditLog(long id, CancellationToken ct)
        => Ok(await _svc.GetAuditLogAsync(id, ct));
}

public record DisableVersionRequest(DateOnly DisabledDate, string? Notes);
