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

    private static readonly string[] PayeeTypes = ["Carrier", "TaxFilingService", "PremiumFinance", "Broker", "Other"];

    [HttpGet("payees")]
    public async Task<IActionResult> GetPayees([FromQuery] bool includeInactive = false, CancellationToken ct = default)
    {
        var payees = await _db.Set<Payee>()
            .Where(p => p.TenantId == 1 && (includeInactive || p.IsActive))
            .OrderBy(p => p.Name)
            .Select(p => new PayeeOptionDto(p.Id, p.Name, p.PayeeType, p.IsActive))
            .ToListAsync(ct);

        return Ok(payees);
    }

    [HttpPost("payees")]
    public async Task<IActionResult> CreatePayee([FromBody] UpsertPayeeRequest req, CancellationToken ct)
    {
        var name = req.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new { ErrorCode = "VALIDATION", ErrorMessage = "Payee name is required." });
        if (!PayeeTypes.Contains(req.PayeeType))
            return BadRequest(new { ErrorCode = "VALIDATION", ErrorMessage = $"Payee type must be one of: {string.Join(", ", PayeeTypes)}." });

        var duplicate = await _db.Set<Payee>()
            .AnyAsync(p => p.TenantId == 1 && p.Name.ToLower() == name.ToLower(), ct);
        if (duplicate)
            return BadRequest(new { ErrorCode = "DUPLICATE_NAME", ErrorMessage = $"A payee named '{name}' already exists." });

        var payee = new Payee
        {
            Name = name,
            PayeeType = req.PayeeType,
            ExternalReference = string.IsNullOrWhiteSpace(req.ExternalReference) ? null : req.ExternalReference.Trim(),
            IsActive = req.IsActive,
        };
        _db.Set<Payee>().Add(payee);
        await _db.SaveChangesAsync(ct);

        return Ok(new PayeeOptionDto(payee.Id, payee.Name, payee.PayeeType, payee.IsActive));
    }

    [HttpPut("payees/{id:long}")]
    public async Task<IActionResult> UpdatePayee(long id, [FromBody] UpsertPayeeRequest req, CancellationToken ct)
    {
        var payee = await _db.Set<Payee>().FirstOrDefaultAsync(p => p.Id == id && p.TenantId == 1, ct);
        if (payee is null)
            return NotFound(new { ErrorCode = "NOT_FOUND", ErrorMessage = "Payee not found." });

        var name = req.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new { ErrorCode = "VALIDATION", ErrorMessage = "Payee name is required." });
        if (!PayeeTypes.Contains(req.PayeeType))
            return BadRequest(new { ErrorCode = "VALIDATION", ErrorMessage = $"Payee type must be one of: {string.Join(", ", PayeeTypes)}." });

        var duplicate = await _db.Set<Payee>()
            .AnyAsync(p => p.TenantId == 1 && p.Id != id && p.Name.ToLower() == name.ToLower(), ct);
        if (duplicate)
            return BadRequest(new { ErrorCode = "DUPLICATE_NAME", ErrorMessage = $"A payee named '{name}' already exists." });

        payee.Name = name;
        payee.PayeeType = req.PayeeType;
        payee.ExternalReference = string.IsNullOrWhiteSpace(req.ExternalReference) ? null : req.ExternalReference.Trim();
        payee.IsActive = req.IsActive;
        await _db.SaveChangesAsync(ct);

        return Ok(new PayeeOptionDto(payee.Id, payee.Name, payee.PayeeType, payee.IsActive));
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
