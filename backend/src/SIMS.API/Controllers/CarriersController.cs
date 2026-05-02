using SIMS.Application.DTOs.Carriers;
using SIMS.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/carriers")]
[Authorize]
public class CarriersController : ControllerBase
{
    private readonly ICarrierService _carrierService;

    public CarriersController(ICarrierService carrierService) => _carrierService = carrierService;

    // ─── Core ─────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = false)
        => Ok(await _carrierService.GetAllAsync(activeOnly));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _carrierService.GetByIdAsync(id);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { result.ErrorMessage });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CarrierCreateDto dto)
    {
        var result = await _carrierService.CreateAsync(dto);
        if (!result.IsSuccess) return BadRequest(new { result.ErrorCode, result.ErrorMessage });
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CarrierUpdateDto dto)
    {
        var result = await _carrierService.UpdateAsync(id, dto);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _carrierService.DeleteAsync(id);
        return result.IsSuccess ? NoContent() : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    // ─── Contacts ─────────────────────────────────────────────────────────────

    [HttpPost("{id:guid}/contacts")]
    public async Task<IActionResult> AddContact(Guid id, [FromBody] CarrierContactInputDto dto)
    {
        var result = await _carrierService.AddContactAsync(id, dto);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPut("{id:guid}/contacts/{contactId:guid}")]
    public async Task<IActionResult> UpdateContact(Guid id, Guid contactId, [FromBody] CarrierContactInputDto dto)
    {
        var result = await _carrierService.UpdateContactAsync(id, contactId, dto);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpDelete("{id:guid}/contacts/{contactId:guid}")]
    public async Task<IActionResult> DeleteContact(Guid id, Guid contactId)
    {
        var result = await _carrierService.DeleteContactAsync(id, contactId);
        return result.IsSuccess ? NoContent() : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }
}
