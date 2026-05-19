using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIMS.Application.DTOs.Ai;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Security;
using System.Security.Claims;

namespace SIMS.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/ai-settings")]
[Authorize(Policy = AppPermissions.AdminSystemManage)]
public class AiModelSettingsController : ControllerBase
{
    private readonly IAiModelSettingsService _service;

    public AiModelSettingsController(IAiModelSettingsService service) => _service = service;

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("models")]
    public async Task<IActionResult> GetModels(CancellationToken ct)
        => Ok(await _service.GetModelsAsync(ct));

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
        => Ok(await _service.GetSettingsAsync(ct));

    [HttpPut("settings/{useCase}")]
    public async Task<IActionResult> UpdateSetting(
        string useCase,
        [FromBody] UpdateAiUseCaseModelSettingRequest request,
        CancellationToken ct)
    {
        var result = await _service.UpdateUseCaseModelAsync(
            useCase,
            request.AiModelRegistryId,
            UserId,
            request.ChangeReason,
            request.PromptVersion,
            ct);

        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpGet("audit-log")]
    public async Task<IActionResult> GetAuditLog(CancellationToken ct)
        => Ok(await _service.GetAuditLogAsync(ct));
}
