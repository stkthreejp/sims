using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIMS.Application.DTOs.ProposalDocuments;
using SIMS.Application.Interfaces.Services;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/proposal-document-configurations")]
[Authorize(Policy = AppPermissions.UnderwritingManage)]
public class ProposalDocumentConfigurationsController : ControllerBase
{
    private readonly IProposalDocumentConfigurationService _service;

    public ProposalDocumentConfigurationsController(IProposalDocumentConfigurationService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => Ok(await _service.GetAllAsync(includeInactive, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertProposalDocumentConfigurationRequest request, CancellationToken ct)
    {
        var result = await _service.CreateAsync(request, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpsertProposalDocumentConfigurationRequest request, CancellationToken ct)
    {
        var result = await _service.UpdateAsync(id, request, ct);
        if (!result.IsSuccess && result.ErrorCode == "NOT_FOUND")
            return NotFound(new { result.ErrorCode, result.ErrorMessage });

        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _service.DeleteAsync(id, ct);
        if (!result.IsSuccess && result.ErrorCode == "NOT_FOUND")
            return NotFound(new { result.ErrorCode, result.ErrorMessage });

        return result.IsSuccess ? NoContent() : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpGet("quotes/{quoteId:guid}/selection")]
    [Authorize(Policy = AppPermissions.PoliciesView)]
    public async Task<IActionResult> ResolveForQuote(Guid quoteId, CancellationToken ct)
    {
        var result = await _service.ResolveForQuoteAsync(quoteId, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }
}
