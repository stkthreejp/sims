using System.Security.Claims;
using SIMS.Application.Common;
using SIMS.Application.DTOs.Auth;
using SIMS.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
[AllowAnonymous]
public class AuthController : ControllerBase
{
    private const string RefreshTokenCookieName = "sims-refresh";
    private readonly IAuthService _authService;
    private readonly IConfiguration _config;
    private readonly IHostEnvironment _environment;

    public AuthController(IAuthService authService, IConfiguration config, IHostEnvironment environment)
    {
        _authService = authService;
        _config = config;
        _environment = environment;
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var result = await _authService.LoginAsync(dto, ip);
        return ToLoginResponse(result);
    }

    [HttpPost("microsoft")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> LoginWithMicrosoft([FromBody] MicrosoftLoginRequestDto dto)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var result = await _authService.LoginWithMicrosoftAsync(dto, ip);
        return ToLoginResponse(result);
    }

    [HttpPost("refresh")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Refresh([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RefreshTokenRequestDto? dto)
    {
        var refreshToken = Request.Cookies[RefreshTokenCookieName] ?? dto?.RefreshToken;
        if (string.IsNullOrWhiteSpace(refreshToken))
            return Unauthorized(new { ErrorCode = "INVALID_TOKEN", ErrorMessage = "Refresh token is required." });

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var result = await _authService.RefreshTokenAsync(refreshToken, ip);
        return ToLoginResponse(result);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RefreshTokenRequestDto? dto)
    {
        var refreshToken = Request.Cookies[RefreshTokenCookieName] ?? dto?.RefreshToken;
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (!string.IsNullOrWhiteSpace(refreshToken))
            await _authService.LogoutAsync(refreshToken, ip);
        Response.Cookies.Delete(RefreshTokenCookieName, BuildExpiredRefreshCookieOptions());
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _authService.GetCurrentUserAsync(userId);
        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }

    [HttpPut("me/password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _authService.ChangePasswordAsync(userId, dto);
        return result.IsSuccess ? NoContent() : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    private IActionResult ToLoginResponse(Result<LoginResponseDto> result)
    {
        if (!result.IsSuccess)
            return Unauthorized(new { result.ErrorCode, result.ErrorMessage });

        var value = result.Value!;
        if (!string.IsNullOrWhiteSpace(value.RefreshToken))
        {
            Response.Cookies.Append(RefreshTokenCookieName, value.RefreshToken, BuildRefreshCookieOptions());
            value.RefreshToken = null;
        }

        return Ok(value);
    }

    private CookieOptions BuildRefreshCookieOptions()
    {
        var refreshDays = int.TryParse(_config["Jwt:RefreshTokenExpiryDays"], out var parsed) ? parsed : 7;
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = !_environment.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Path = "/api/v1/auth",
            MaxAge = TimeSpan.FromDays(refreshDays),
        };
    }

    private CookieOptions BuildExpiredRefreshCookieOptions()
    {
        var options = BuildRefreshCookieOptions();
        options.Expires = DateTimeOffset.UnixEpoch;
        return options;
    }
}
