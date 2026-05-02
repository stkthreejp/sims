using IMS.Application.Common;
using IMS.Application.DTOs.Submissions;
using IMS.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace IMS.API.Controllers;

public class SetLinesOfBusinessRequest
{
    public List<string> LinesOfBusiness { get; set; } = [];
}

[ApiController]
[Route("api/v1/submissions")]
[Authorize]
public class SubmissionsController : ControllerBase
{
    private readonly ISubmissionService _submissionService;

    public SubmissionsController(ISubmissionService submissionService) => _submissionService = submissionService;

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] QueryParameters query)
        => Ok(await _submissionService.GetAllAsync(query));

    [HttpGet("by-insured/{insuredId:guid}")]
    public async Task<IActionResult> GetByInsured(Guid insuredId)
        => Ok(await _submissionService.GetByInsuredAsync(insuredId));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _submissionService.GetByIdAsync(id);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { result.ErrorMessage });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SubmissionCreateDto dto)
    {
        var result = await _submissionService.CreateAsync(dto, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { result.ErrorCode, result.ErrorMessage });
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SubmissionUpdateDto dto)
    {
        var result = await _submissionService.UpdateAsync(id, dto);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPatch("{id:guid}/lines-of-business")]
    public async Task<IActionResult> SetLinesOfBusiness(Guid id, [FromBody] SetLinesOfBusinessRequest request)
    {
        var result = await _submissionService.SetLinesOfBusinessAsync(id, request.LinesOfBusiness);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _submissionService.DeleteAsync(id);
        return result.IsSuccess ? NoContent() : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }
}
