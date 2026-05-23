using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIMS.Application.DTOs.Accounting;
using SIMS.Application.DTOs.Underwriting;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Enums;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace SIMS.API.Controllers.Billing;

[ApiController]
[Route("api/v1/billing/void")]
[Authorize(Policy = AppPermissions.AccountingManage)]
public class VoidController : ControllerBase
{
    private readonly IVoidService _svc;
    private readonly IAuthorityApprovalService _authorityApproval;

    public VoidController(IVoidService svc, IAuthorityApprovalService authorityApproval)
    {
        _svc = svc;
        _authorityApproval = authorityApproval;
    }

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsAdmin => User.IsInRole("Admin");

    [HttpPost("receipts/{id:long}")]
    public async Task<IActionResult> VoidReceipt(long id, [FromBody] VoidRequest req, CancellationToken ct)
    {
        var authority = await RequireVoidAuthorityAsync("receipt", id, "Receipt void", ct);
        if (!authority.Allowed)
            return AuthorityRequired(authority);

        var r = await _svc.VoidReceiptAsync(id, req.Reason, UserId, IsAdmin || authority.Allowed, ct);
        return r.Success ? Ok(r) : BadRequest(new { r.ErrorCode, r.ErrorMessage });
    }

    [HttpPost("cash-applications/{id:long}")]
    public async Task<IActionResult> VoidCashApplication(long id, [FromBody] VoidRequest req, CancellationToken ct)
    {
        var authority = await RequireVoidAuthorityAsync("cash-application", id, "Cash application void", ct);
        if (!authority.Allowed)
            return AuthorityRequired(authority);

        var r = await _svc.VoidCashApplicationAsync(id, req.Reason, UserId, IsAdmin || authority.Allowed, ct);
        return r.Success ? Ok(r) : BadRequest(new { r.ErrorCode, r.ErrorMessage });
    }

    [HttpPost("invoices/{id:long}")]
    public async Task<IActionResult> VoidInvoice(long id, [FromBody] VoidRequest req, CancellationToken ct)
    {
        var authority = await RequireVoidAuthorityAsync("invoice", id, "Invoice void", ct);
        if (!authority.Allowed)
            return AuthorityRequired(authority);

        var r = await _svc.VoidInvoiceAsync(id, req.Reason, UserId, IsAdmin || authority.Allowed, ct);
        return r.Success ? Ok(r) : BadRequest(new { r.ErrorCode, r.ErrorMessage });
    }

    [HttpPost("disbursements/{id:long}")]
    public async Task<IActionResult> VoidDisbursement(long id, [FromBody] VoidRequest req, CancellationToken ct)
    {
        var authority = await RequireVoidAuthorityAsync("disbursement", id, "Disbursement void", ct);
        if (!authority.Allowed)
            return AuthorityRequired(authority);

        var r = await _svc.VoidDisbursementAsync(id, req.Reason, UserId, IsAdmin || authority.Allowed, ct);
        return r.Success ? Ok(r) : BadRequest(new { r.ErrorCode, r.ErrorMessage });
    }

    private async Task<AuthorityApprovalEvaluationDto> RequireVoidAuthorityAsync(
        string voidType,
        long id,
        string actionLabel,
        CancellationToken ct)
    {
        var actionCode = $"accounting.void.{voidType}";
        return await _authorityApproval.EvaluateAsync(
            new AuthorityApprovalEvaluationRequest(
                AuthorityApprovalTargetType.AccountingAction,
                StableAccountingTargetId(actionCode, id),
                actionCode,
                actionLabel,
                AppPermissions.AccountingAdmin,
                "AccountingVoid",
                $"{actionLabel} requires accounting admin approval.",
                null,
                null),
            User.PermissionNames(),
            UserId,
            ct);
    }

    private ObjectResult AuthorityRequired(AuthorityApprovalEvaluationDto authority) =>
        StatusCode(403, new { ErrorCode = "AUTHORITY_APPROVAL_REQUIRED", ErrorMessage = authority.Message, authority.ApprovalRequestId });

    private static Guid StableAccountingTargetId(string actionCode, long id)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{actionCode}:{id}"));
        return new Guid(bytes.Take(16).ToArray());
    }
}
