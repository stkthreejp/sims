using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using SIMS.Application.DTOs.PolicyForms;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Enums;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/policy-forms")]
[Authorize(Policy = AppPermissions.UnderwritingManage)]
public class PolicyFormsController : ControllerBase
{
    private readonly IPolicyFormService _service;
    private readonly IPolicyAssemblyService _assembly;

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public PolicyFormsController(IPolicyFormService service, IPolicyAssemblyService assembly)
    {
        _service = service;
        _assembly = assembly;
    }

    [HttpGet("templates")]
    public async Task<IActionResult> GetTemplates([FromQuery] bool includeInactive = false)
        => Ok(await _service.GetTemplatesAsync(includeInactive));

    [HttpGet("templates/{id:guid}")]
    public async Task<IActionResult> GetTemplate(Guid id)
    {
        var result = await _service.GetTemplateAsync(id);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("templates")]
    public async Task<IActionResult> CreateTemplate([FromBody] PolicyFormTemplateUpsertDto dto)
    {
        var result = await _service.CreateTemplateAsync(dto);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetTemplate), new { id = result.Value!.Id }, result.Value)
            : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPut("templates/{id:guid}")]
    public async Task<IActionResult> UpdateTemplate(Guid id, [FromBody] PolicyFormTemplateUpsertDto dto)
    {
        var result = await _service.UpdateTemplateAsync(id, dto);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("templates/{id:guid}/file")]
    [RequestSizeLimit(52_428_800)]
    public async Task<IActionResult> UploadTemplateFile(Guid id, IFormFile file)
    {
        var result = await _service.UploadTemplateFileAsync(id, file);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpGet("templates/{id:guid}/download-url")]
    public async Task<IActionResult> GetTemplateDownloadUrl(Guid id)
    {
        var result = await _service.GetTemplateDownloadUrlAsync(id);
        return result.IsSuccess ? Ok(new { url = result.Value }) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("templates/{id:guid}/test-merge")]
    public async Task<IActionResult> TestMergeTemplate(Guid id, [FromBody] PolicyFormTestMergeDto dto)
    {
        var result = await _assembly.TestMergeTemplateAsync(id, dto.PolicyId, CurrentUserId);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpDelete("templates/{id:guid}")]
    public async Task<IActionResult> DeleteTemplate(Guid id)
    {
        var result = await _service.DeleteTemplateAsync(id);
        return result.IsSuccess ? NoContent() : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPut("templates/{id:guid}/mappings")]
    public async Task<IActionResult> ReplaceMappings(Guid id, [FromBody] List<PolicyFormFieldMappingUpsertDto> mappings)
    {
        var result = await _service.ReplaceMappingsAsync(id, mappings);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpGet("tags")]
    public async Task<IActionResult> GetDocumentTags()
        => Ok(await _service.GetDocumentTagsAsync());

    [HttpGet("packages")]
    public async Task<IActionResult> GetPackages(
        [FromQuery] Guid? carrierId = null,
        [FromQuery] PolicyLineOfBusiness? lineOfBusiness = null,
        [FromQuery] string? state = null,
        [FromQuery] bool includeInactive = false)
        => Ok(await _service.GetPackagesAsync(carrierId, lineOfBusiness, state, includeInactive));

    [HttpGet("packages/{id:guid}")]
    public async Task<IActionResult> GetPackage(Guid id)
    {
        var result = await _service.GetPackageAsync(id);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("packages")]
    public async Task<IActionResult> CreatePackage([FromBody] PolicyPackageConfigurationUpsertDto dto)
    {
        var result = await _service.CreatePackageAsync(dto);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetPackage), new { id = result.Value!.Id }, result.Value)
            : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPut("packages/{id:guid}")]
    public async Task<IActionResult> UpdatePackage(Guid id, [FromBody] PolicyPackageConfigurationUpsertDto dto)
    {
        var result = await _service.UpdatePackageAsync(id, dto);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpDelete("packages/{id:guid}")]
    public async Task<IActionResult> DeletePackage(Guid id)
    {
        var result = await _service.DeletePackageAsync(id);
        return result.IsSuccess ? NoContent() : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPut("packages/{id:guid}/forms")]
    public async Task<IActionResult> ReplacePackageForms(Guid id, [FromBody] List<PolicyPackageFormUpsertDto> forms)
    {
        var result = await _service.ReplacePackageFormsAsync(id, forms);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }
}
