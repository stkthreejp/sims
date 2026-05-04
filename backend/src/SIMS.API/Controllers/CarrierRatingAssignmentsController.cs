using SIMS.Application.DTOs.Rating;
using SIMS.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/carrier-rating-assignments")]
[Authorize(Roles = "Admin,Underwriter")]
public class CarrierRatingAssignmentsController : ControllerBase
{
    private readonly ICarrierRatingAssignmentService _svc;

    public CarrierRatingAssignmentsController(ICarrierRatingAssignmentService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? carrierId, CancellationToken ct)
        => Ok(await _svc.GetAllAsync(carrierId, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CarrierRatingAssignmentCreateDto dto, CancellationToken ct)
    {
        var result = await _svc.CreateAsync(dto, ct);
        if (!result.IsSuccess) return BadRequest(new { result.ErrorCode, result.ErrorMessage });
        return Ok(result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CarrierRatingAssignmentUpdateDto dto, CancellationToken ct)
    {
        var result = await _svc.UpdateAsync(id, dto, ct);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "NOT_FOUND") return NotFound(new { result.ErrorCode, result.ErrorMessage });
            return BadRequest(new { result.ErrorCode, result.ErrorMessage });
        }
        return Ok(result.Value);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _svc.DeleteAsync(id, ct);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "NOT_FOUND") return NotFound(new { result.ErrorCode, result.ErrorMessage });
            if (result.ErrorCode == "HAS_BOUND_QUOTES") return Conflict(new { result.ErrorCode, result.ErrorMessage });
            return BadRequest(new { result.ErrorCode, result.ErrorMessage });
        }
        return NoContent();
    }
}
