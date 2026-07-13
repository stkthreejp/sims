using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIMS.Application.DTOs.CompanyLicenses;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Security;

namespace SIMS.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/company-licenses")]
[Authorize(Policy = AppPermissions.AdminSystemManage)]
public class CompanyLicensesController : ControllerBase
{
    private readonly ICompanyLicenseService _service;

    public CompanyLicensesController(ICompanyLicenseService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => Ok(await _service.GetAllAsync(includeInactive, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertCompanyLicenseRequest req, CancellationToken ct)
    {
        var result = await _service.CreateAsync(req, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpsertCompanyLicenseRequest req, CancellationToken ct)
    {
        var result = await _service.UpdateAsync(id, req, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _service.DeleteAsync(id, ct);
        return result.IsSuccess ? NoContent() : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("import")]
    public async Task<IActionResult> Import([FromBody] ImportCompanyLicensesRequest req, CancellationToken ct)
    {
        var result = await _service.ImportAsync(req.Rows ?? [], ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }
}
