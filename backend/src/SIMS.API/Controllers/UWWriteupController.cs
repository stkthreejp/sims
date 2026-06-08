using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIMS.Application.DTOs.UWWriteup;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Security;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/quotes/{quoteId:guid}/writeup")]
[Authorize]
public class UWWriteupController : ControllerBase
{
    private readonly IUWWriteupService _service;

    public UWWriteupController(IUWWriteupService service) => _service = service;

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private UserAccessScope CurrentAccess => User.ToBusinessDataAccessScope();

    [HttpGet]
    public async Task<IActionResult> Get(Guid quoteId, CancellationToken ct)
    {
        try
        {
            var dto = await _service.GetOrCreateAsync(quoteId, CurrentUserId, CurrentAccess, ct);
            return Ok(dto);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPut]
    public async Task<IActionResult> Save(Guid quoteId, [FromBody] SaveWriteupDto dto, CancellationToken ct)
    {
        try
        {
            var result = await _service.SaveAsync(quoteId, dto, CurrentUserId, CurrentAccess, ct);
            return Ok(result);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost("submit")]
    public async Task<IActionResult> Submit(Guid quoteId, [FromBody] SubmitWriteupDto dto, CancellationToken ct)
    {
        try
        {
            var result = await _service.SubmitAsync(quoteId, dto, CurrentUserId, CurrentAccess, ct);
            return Ok(result);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost("approve")]
    [Authorize(Policy = AppPermissions.UnderwritingManage)]
    public async Task<IActionResult> Approve(Guid quoteId, CancellationToken ct)
    {
        try
        {
            var result = await _service.ApproveAsync(quoteId, CurrentUserId, CurrentAccess, ct);
            return Ok(result);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
}
