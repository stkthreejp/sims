using IMS.Application.Common;
using IMS.Application.DTOs.Quotes;
using IMS.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace IMS.API.Controllers;

[ApiController]
[Route("api/v1/quotes")]
[Authorize]
public class QuotesController : ControllerBase
{
    private readonly IQuoteService _quoteService;

    public QuotesController(IQuoteService quoteService) => _quoteService = quoteService;

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] QueryParameters query)
        => Ok(await _quoteService.GetAllAsync(query));

    [HttpGet("policies")]
    public async Task<IActionResult> GetAllPolicies([FromQuery] QueryParameters query)
        => Ok(await _quoteService.GetAllPoliciesAsync(query));

    [HttpGet("by-submission/{submissionId:guid}")]
    public async Task<IActionResult> GetBySubmission(Guid submissionId)
        => Ok(await _quoteService.GetBySubmissionAsync(submissionId));

    [HttpGet("bound-by-insured/{insuredId:guid}")]
    public async Task<IActionResult> GetBoundByInsured(Guid insuredId)
        => Ok(await _quoteService.GetBoundByInsuredAsync(insuredId));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _quoteService.GetByIdAsync(id);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { result.ErrorMessage });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] QuoteCreateDto dto)
    {
        var result = await _quoteService.CreateAsync(dto, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { result.ErrorCode, result.ErrorMessage });
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] QuoteUpdateDto dto)
    {
        var result = await _quoteService.UpdateAsync(id, dto);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("{id:guid}/bind")]
    public async Task<IActionResult> Bind(Guid id, [FromBody] QuoteBindDto dto)
    {
        var result = await _quoteService.BindAsync(id, dto, CurrentUserId);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _quoteService.DeleteAsync(id);
        return result.IsSuccess ? NoContent() : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }
}
