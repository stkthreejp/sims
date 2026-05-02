using System.Security.Claims;
using SIMS.Application.DTOs.Auth;
using SIMS.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService) => _authService = authService;

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var result = await _authService.LoginAsync(dto, ip);
        return result.IsSuccess ? Ok(result.Value) : Unauthorized(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("microsoft")]
    public async Task<IActionResult> LoginWithMicrosoft([FromBody] MicrosoftLoginRequestDto dto)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var result = await _authService.LoginWithMicrosoftAsync(dto, ip);
        return result.IsSuccess ? Ok(result.Value) : Unauthorized(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDto dto)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var result = await _authService.RefreshTokenAsync(dto.RefreshToken, ip);
        return result.IsSuccess ? Ok(result.Value) : Unauthorized(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequestDto dto)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        await _authService.LogoutAsync(dto.RefreshToken, ip);
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
}
