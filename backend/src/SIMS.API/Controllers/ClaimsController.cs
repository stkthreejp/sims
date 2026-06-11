using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIMS.API.Security;
using SIMS.Application.DTOs.Claims;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Security;
using SIMS.Domain.Enums;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/claims")]
[Authorize(Policy = AppPermissions.ClaimsView)]
public class ClaimsController : ControllerBase
{
    private readonly IClaimsService _service;
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)!);
    private UserAccessScope CurrentAccess => User.ToBusinessDataAccessScope();

    public ClaimsController(IClaimsService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] Guid? policyId = null,
        [FromQuery] Guid? insuredId = null,
        [FromQuery] ClaimStatus? status = null,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        CancellationToken ct = default)
        => Ok(await _service.GetClaimsAsync(CurrentAccess, policyId, insuredId, status, fromDate, toDate, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetClaimAsync(id, CurrentAccess, ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost]
    [Authorize(Policy = AppPermissions.ClaimsManage)]
    public async Task<IActionResult> Create([FromBody] UpsertClaimRequest request, CancellationToken ct)
    {
        var result = await _service.CreateClaimAsync(request, CurrentUserId, CurrentAccess, ct);
        if (result.IsSuccess) return Ok(result.Value);
        return result.ErrorCode switch
        {
            "POLICY_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
            BusinessDataAccess.AccessDeniedCode => Forbid(),
            _ => BadRequest(new { result.ErrorCode, result.ErrorMessage }),
        };
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AppPermissions.ClaimsManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpsertClaimRequest request, CancellationToken ct)
    {
        var result = await _service.UpdateClaimAsync(id, request, CurrentUserId, CurrentAccess, ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("import")]
    [Authorize(Policy = AppPermissions.ClaimsManage)]
    public async Task<IActionResult> Import([FromBody] ImportClaimsRequest request, CancellationToken ct)
    {
        var result = await _service.ImportClaimsAsync(request, CurrentUserId, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpGet("import-batches")]
    public async Task<IActionResult> GetImportBatches(CancellationToken ct)
        => Ok(await _service.GetImportBatchesAsync(ct));

    [HttpGet("loss-run")]
    public async Task<IActionResult> GetLossRun(
        [FromQuery] Guid? insuredId = null,
        [FromQuery] Guid? policyId = null,
        [FromQuery] DateOnly? asOfDate = null,
        CancellationToken ct = default)
    {
        var date = asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var result = await _service.GetLossRunAsync(insuredId, policyId, date, CurrentAccess, ct);
        if (result.IsSuccess) return Ok(result.Value);
        return result.ErrorCode == BusinessDataAccess.AccessDeniedCode
            ? Forbid()
            : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpGet("loss-run/csv")]
    public async Task<IActionResult> GetLossRunCsv(
        [FromQuery] Guid? insuredId = null,
        [FromQuery] Guid? policyId = null,
        [FromQuery] DateOnly? asOfDate = null,
        CancellationToken ct = default)
    {
        var date = asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var result = await _service.GetLossRunAsync(insuredId, policyId, date, CurrentAccess, ct);
        if (!result.IsSuccess)
            return result.ErrorCode == BusinessDataAccess.AccessDeniedCode
                ? Forbid()
                : BadRequest(new { result.ErrorCode, result.ErrorMessage });

        var run = result.Value!;
        var sb = new StringBuilder();
        sb.AppendLine($"Loss Run as of {run.AsOfDate:yyyy-MM-dd}");
        sb.AppendLine($"Insured:,{CsvEscape(run.InsuredName)},Policy:,{CsvEscape(run.PolicyNumber)},Account:,{CsvEscape(run.Account)}");
        sb.AppendLine($"Claims:,{run.ClaimCount},Open:,{run.OpenCount},Closed:,{run.ClosedCount}");
        sb.AppendLine($"Total Paid:,{run.TotalPaid:F2},Total Reserved:,{run.TotalReserved:F2},Total Expense:,{run.TotalExpense:F2},Total Incurred:,{run.TotalIncurred:F2}");
        sb.AppendLine();
        sb.AppendLine("ClaimNumber,CarrierClaimNumber,PolicyNumber,InsuredName,DateOfLoss,ReportDate,ClosedDate,Status," +
                      "CoverageType,LossCause,ClaimantName,AdjusterName,Paid,Reserved,Expense,Recovery,Incurred,ValuationDate");

        foreach (var c in run.Claims)
        {
            sb.AppendLine(string.Join(",",
                CsvEscape(c.ClaimNumber),
                CsvEscape(c.CarrierClaimNumber),
                CsvEscape(c.PolicyNumber ?? c.SourcePolicyReference),
                CsvEscape(c.InsuredName),
                c.DateOfLoss.ToString("yyyy-MM-dd"),
                c.ReportDate.ToString("yyyy-MM-dd"),
                c.ClosedDate?.ToString("yyyy-MM-dd") ?? "",
                c.Status,
                CsvEscape(c.CoverageType),
                CsvEscape(c.LossCause),
                CsvEscape(c.ClaimantName),
                CsvEscape(c.AdjusterName),
                c.Paid.ToString("F2"),
                c.Reserved.ToString("F2"),
                c.Expense.ToString("F2"),
                c.Recovery.ToString("F2"),
                c.Incurred.ToString("F2"),
                c.LastValuationDate.ToString("yyyy-MM-dd")));
        }

        var label = run.PolicyNumber ?? run.InsuredName ?? "loss-run";
        var safeLabel = string.Concat(label.Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_')).ToLowerInvariant();
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"loss-run-{safeLabel}-{date:yyyyMMdd}.csv");
    }

    private static string CsvEscape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
