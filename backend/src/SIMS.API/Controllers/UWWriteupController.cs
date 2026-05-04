using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIMS.Application.DTOs.UWWriteup;
using SIMS.Application.Interfaces.Services;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/quotes/{quoteId:guid}/writeup")]
[Authorize]
public class UWWriteupController : ControllerBase
{
    private readonly IUWWriteupService _service;

    public UWWriteupController(IUWWriteupService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get(Guid quoteId, CancellationToken ct)
    {
        var userId = GetUserId();
        var dto = await _service.GetOrCreateAsync(quoteId, userId, ct);
        return Ok(dto);
    }

    [HttpPut]
    public async Task<IActionResult> Save(Guid quoteId, [FromBody] SaveWriteupDto dto, CancellationToken ct)
    {
        var result = await _service.SaveAsync(quoteId, dto, ct);
        return Ok(result);
    }

    [HttpPost("submit")]
    public async Task<IActionResult> Submit(Guid quoteId, [FromBody] SubmitWriteupDto dto, CancellationToken ct)
    {
        var userId = GetUserId();
        var result = await _service.SubmitAsync(quoteId, dto, userId, ct);
        return Ok(result);
    }

    [HttpPost("approve")]
    [Authorize(Roles = "Admin,Underwriter")]
    public async Task<IActionResult> Approve(Guid quoteId, CancellationToken ct)
    {
        var userId = GetUserId();
        var result = await _service.ApproveAsync(quoteId, userId, ct);
        return Ok(result);
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }
}
