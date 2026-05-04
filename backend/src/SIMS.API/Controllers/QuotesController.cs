using SIMS.Application.Common;
using SIMS.Application.DTOs.Quotes;
using SIMS.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/quotes")]
[Authorize]
public class QuotesController : ControllerBase
{
    private readonly IQuoteService _quoteService;
    private readonly IRatingEngineService _ratingEngine;
    private readonly IShadowRatingService _shadowRating;
    private readonly IConfiguration _config;

    public QuotesController(IQuoteService quoteService, IRatingEngineService ratingEngine,
        IShadowRatingService shadowRating, IConfiguration config)
    {
        _quoteService = quoteService;
        _ratingEngine = ratingEngine;
        _shadowRating = shadowRating;
        _config = config;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] QueryParameters query)
        => Ok(await _quoteService.GetAllAsync(query));

    [HttpGet("by-submission/{submissionId:guid}")]
    public async Task<IActionResult> GetBySubmission(Guid submissionId)
        => Ok(await _quoteService.GetBySubmissionAsync(submissionId));

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

    [HttpPost("{id:guid}/rate")]
    public async Task<IActionResult> Rate(Guid id, [FromBody] RateQuoteRequest request)
    {
        var result = await _ratingEngine.RateAsync(id, request, CurrentUserId);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpGet("{id:guid}/rating-snapshot")]
    public async Task<IActionResult> GetRatingSnapshot(Guid id)
    {
        var result = await _ratingEngine.GetLatestSnapshotAsync(id);
        if (!result.IsSuccess && result.ErrorCode == "NOT_FOUND")
            return NotFound(new { result.ErrorCode, result.ErrorMessage });
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("{id:guid}/shadow-rate")]
    [Authorize(Roles = "Admin,Underwriter")]
    public async Task<IActionResult> ShadowRate(Guid id, [FromBody] RateQuoteRequest request)
    {
        if (!_config.GetValue<bool>("Rating:ShadowMode"))
            return Conflict(new { ErrorCode = "SHADOW_MODE_DISABLED", ErrorMessage = "Shadow mode is not enabled." });
        var result = await _shadowRating.ShadowRateAsync(id, request, CurrentUserId);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("{id:guid}/bind")]
    public async Task<IActionResult> Bind(Guid id, [FromBody] QuoteBindDto dto)
    {
        var result = await _quoteService.BindAsync(id, dto, CurrentUserId);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("{id:guid}/commission-override")]
    [Authorize(Roles = "Admin,Underwriter")]
    public async Task<IActionResult> CommissionOverride(Guid id, [FromBody] CommissionOverrideRequest req)
    {
        var result = await _quoteService.ApplyCommissionOverrideAsync(id, req, CurrentUserId);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _quoteService.DeleteAsync(id);
        return result.IsSuccess ? NoContent() : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }
}
