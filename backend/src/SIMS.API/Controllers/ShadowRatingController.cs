using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using SIMS.Application.DTOs.Quotes;
using SIMS.Application.Interfaces.Services;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/rating/shadow")]
[Authorize(Roles = "Admin")]
public class ShadowRatingController : ControllerBase
{
    private readonly IShadowRatingService _shadowRating;
    private readonly IConfiguration _config;

    public ShadowRatingController(IShadowRatingService shadowRating, IConfiguration config)
    {
        _shadowRating = shadowRating;
        _config = config;
    }

    [HttpGet("results")]
    public async Task<IActionResult> GetResults([FromQuery] int days = 30, CancellationToken ct = default)
    {
        if (days < 1 || days > 365) days = 30;
        var results = await _shadowRating.GetResultsAsync(days, ct);
        return Ok(new
        {
            ShadowModeEnabled = _config.GetValue<bool>("Rating:ShadowMode"),
            Days = days,
            Results = results,
            OutlierCount = results.Count(r => r.IsOutlier),
            TotalResults = results.Count,
        });
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var enabled = _config.GetValue<bool>("Rating:ShadowMode");
        return Ok(new { ShadowModeEnabled = enabled });
    }
}
