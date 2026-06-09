using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIMS.Application.DTOs.Bordereaux;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Security;
using SIMS.Domain.Enums;

namespace SIMS.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/bordereaux-profiles")]
[Authorize(Policy = AppPermissions.AccountingAdmin)]
public class BordereauxProfilesController : ControllerBase
{
    private readonly IBordereauxService _service;
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public BordereauxProfilesController(IBordereauxService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] bool includeInactive = false,
        [FromQuery] Guid? programId = null,
        [FromQuery] Guid? carrierId = null,
        [FromQuery] BordereauxReportType? reportType = null,
        [FromQuery] BordereauxOutputFormat? outputFormat = null,
        CancellationToken ct = default)
        => Ok(await _service.GetProfilesAsync(includeInactive, programId, carrierId, reportType, outputFormat, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetProfileAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpGet("{id:guid}/premium-preview")]
    public async Task<IActionResult> GetPremiumPreview(
        Guid id,
        [FromQuery] DateOnly periodStart,
        [FromQuery] DateOnly periodEnd,
        CancellationToken ct)
    {
        var result = await _service.GetPremiumPreviewAsync(id, periodStart, periodEnd, ct);
        if (result.IsSuccess)
            return Ok(result.Value);

        return result.ErrorCode is "PROFILE_NOT_FOUND"
            ? NotFound(new { result.ErrorCode, result.ErrorMessage })
            : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpGet("premium-runs")]
    public async Task<IActionResult> GetPremiumRuns(
        [FromQuery] Guid? profileId = null,
        CancellationToken ct = default)
        => Ok(await _service.GetRunsAsync(profileId, ct));

    [HttpGet("premium-runs/{runId:guid}")]
    public async Task<IActionResult> GetPremiumRun(Guid runId, CancellationToken ct)
    {
        var result = await _service.GetRunAsync(runId, ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("{id:guid}/premium-runs")]
    public async Task<IActionResult> CreatePremiumRunSnapshot(
        Guid id,
        [FromBody] CreatePremiumBordereauxRunRequest request,
        CancellationToken ct)
    {
        var result = await _service.CreatePremiumRunSnapshotAsync(id, request.PeriodStart, request.PeriodEnd, CurrentUserId, ct);
        if (result.IsSuccess)
            return Ok(result.Value);

        return result.ErrorCode is "PROFILE_NOT_FOUND"
            ? NotFound(new { result.ErrorCode, result.ErrorMessage })
            : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("premium-runs/{runId:guid}/reconcile")]
    public async Task<IActionResult> ReconcilePremiumRun(
        Guid runId,
        [FromBody] ReconcileBordereauxRunRequest request,
        CancellationToken ct)
    {
        var result = await _service.ReconcilePremiumRunAsync(runId, request, ct);
        if (result.IsSuccess)
            return Ok(result.Value);

        return result.ErrorCode is "RUN_NOT_FOUND"
            ? NotFound(new { result.ErrorCode, result.ErrorMessage })
            : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("premium-runs/{runId:guid}/export-package")]
    public async Task<IActionResult> GeneratePremiumExportPackage(Guid runId, CancellationToken ct)
    {
        var result = await _service.GeneratePremiumExportPackageAsync(runId, CurrentUserId, ct);
        if (result.IsSuccess)
            return Ok(result.Value);

        return result.ErrorCode is "RUN_NOT_FOUND"
            ? NotFound(new { result.ErrorCode, result.ErrorMessage })
            : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpGet("premium-runs/{runId:guid}/csv")]
    public async Task<IActionResult> GetPremiumRunCsv(Guid runId, CancellationToken ct)
    {
        var result = await _service.GetRunSourceRowsAsync(runId, ct);
        if (!result.IsSuccess)
            return result.ErrorCode is "RUN_NOT_FOUND"
                ? NotFound(new { result.ErrorCode, result.ErrorMessage })
                : BadRequest(new { result.ErrorCode, result.ErrorMessage });

        var sb = new StringBuilder();
        sb.AppendLine("PolicyNumber,TransactionNumber,TransactionType,ReportingDate,InsuredName,InsuredState," +
                      "LineOfBusiness,GrossPremium,GrossCommission,Fees,NetDueCarrier,InvoiceNumber,PolicyIssuanceDate");

        foreach (var row in result.Value!)
        {
            sb.AppendLine(string.Join(",",
                CsvEscape(row.PolicyNumber),
                CsvEscape(row.TransactionNumber),
                row.TransactionType,
                row.ReportingDate.ToString("yyyy-MM-dd"),
                CsvEscape(row.InsuredName),
                CsvEscape(row.InsuredState),
                row.LineOfBusiness,
                row.GrossPremium.ToString("F2"),
                row.GrossCommission.ToString("F2"),
                row.Fees.ToString("F2"),
                row.NetDueCarrier.ToString("F2"),
                CsvEscape(row.InvoiceNumber),
                row.PolicyIssuanceDate?.ToString("yyyy-MM-dd") ?? ""));
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"bordereaux-{runId:N}.csv");
    }

    [HttpGet("premium-runs/{runId:guid}/london-bordereaux/download-url")]
    public Task<IActionResult> GetLondonBordereauxDownloadUrl(Guid runId, CancellationToken ct)
        => GetRunFileDownloadUrl(runId, BordereauxRunFileKind.LondonBordereaux, ct);

    [HttpGet("premium-runs/{runId:guid}/account-current/download-url")]
    public Task<IActionResult> GetAccountCurrentDownloadUrl(Guid runId, CancellationToken ct)
        => GetRunFileDownloadUrl(runId, BordereauxRunFileKind.AccountCurrent, ct);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertBordereauxProfileRequest request, CancellationToken ct)
    {
        var result = await _service.CreateProfileAsync(request, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpsertBordereauxProfileRequest request, CancellationToken ct)
    {
        var result = await _service.UpdateProfileAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    private static string CsvEscape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    private async Task<IActionResult> GetRunFileDownloadUrl(Guid runId, BordereauxRunFileKind fileKind, CancellationToken ct)
    {
        var result = await _service.GetRunFileDownloadUrlAsync(runId, fileKind, ct);
        if (result.IsSuccess)
            return Ok(new { url = result.Value });

        return result.ErrorCode is "RUN_NOT_FOUND"
            ? NotFound(new { result.ErrorCode, result.ErrorMessage })
            : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }
}
