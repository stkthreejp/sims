using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Enums;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/rating/shadow")]
[Authorize(Policy = AppPermissions.RatingAdmin)]
public class ShadowRatingController : ControllerBase
{
    private readonly IShadowRatingService _shadowRating;

    public ShadowRatingController(IShadowRatingService shadowRating)
        => _shadowRating = shadowRating;

    [HttpGet("results")]
    public async Task<IActionResult> GetResults([FromQuery] int days = 30, CancellationToken ct = default)
    {
        if (days < 1 || days > 365) days = 30;
        var results = await _shadowRating.GetResultsAsync(days, ct);
        var settings = await _shadowRating.GetShadowSettingsAsync(ct);
        return Ok(new
        {
            Settings = settings,
            Days = days,
            Results = results,
            OutlierCount = results.Count(r => r.IsOutlier),
            TotalResults = results.Count,
        });
    }

    [HttpGet("settings")]
    [Authorize]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
    {
        var settings = await _shadowRating.GetShadowSettingsAsync(ct);
        return Ok(settings);
    }

    [HttpPut("settings/{lob}")]
    public async Task<IActionResult> UpdateLobSetting(string lob, [FromBody] UpdateShadowLobDto dto, CancellationToken ct)
    {
        if (!Enum.TryParse<PolicyLineOfBusiness>(lob, true, out var parsedLob))
            return BadRequest(new { ErrorCode = "INVALID_LOB", ErrorMessage = $"Unknown LOB: {lob}" });

        await _shadowRating.SetShadowModeForLobAsync(parsedLob, dto.Enabled, ct);
        var settings = await _shadowRating.GetShadowSettingsAsync(ct);
        return Ok(settings);
    }
}

public class UpdateShadowLobDto
{
    public bool Enabled { get; set; }
}
