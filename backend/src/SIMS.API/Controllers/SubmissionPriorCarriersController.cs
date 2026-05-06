using SIMS.Application.DTOs.Submissions;
using SIMS.Domain.Entities;
using SIMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/submissions/{submissionId:guid}/prior-carriers")]
[Authorize(Policy = AppPermissions.UnderwritingManage)]
public class SubmissionPriorCarriersController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public SubmissionPriorCarriersController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll(Guid submissionId)
    {
        var carriers = await _db.SubmissionPriorCarriers
            .Where(p => p.SubmissionId == submissionId)
            .OrderBy(p => p.CreatedAt)
            .Select(p => MapToDto(p))
            .ToListAsync();
        return Ok(carriers);
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid submissionId, [FromBody] SubmissionPriorCarrierCreateDto dto)
    {
        if (!await _db.Submissions.AnyAsync(s => s.Id == submissionId))
            return NotFound(new { ErrorMessage = "Submission not found." });

        var carrier = new SubmissionPriorCarrier
        {
            SubmissionId = submissionId,
            LineOfBusiness = dto.LineOfBusiness,
            CarrierName = dto.CarrierName,
            PolicyNumber = dto.PolicyNumber,
            ExpirationDate = dto.ExpirationDate,
            Premium = dto.Premium,
        };
        _db.SubmissionPriorCarriers.Add(carrier);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAll), new { submissionId }, MapToDto(carrier));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid submissionId, Guid id, [FromBody] SubmissionPriorCarrierUpdateDto dto)
    {
        var carrier = await _db.SubmissionPriorCarriers.FirstOrDefaultAsync(p => p.Id == id && p.SubmissionId == submissionId);
        if (carrier == null) return NotFound();

        carrier.LineOfBusiness = dto.LineOfBusiness;
        carrier.CarrierName = dto.CarrierName;
        carrier.PolicyNumber = dto.PolicyNumber;
        carrier.ExpirationDate = dto.ExpirationDate;
        carrier.Premium = dto.Premium;
        await _db.SaveChangesAsync();
        return Ok(MapToDto(carrier));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid submissionId, Guid id)
    {
        var carrier = await _db.SubmissionPriorCarriers.FirstOrDefaultAsync(p => p.Id == id && p.SubmissionId == submissionId);
        if (carrier == null) return NotFound();
        carrier.IsDeleted = true;
        carrier.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static SubmissionPriorCarrierDto MapToDto(SubmissionPriorCarrier p) => new()
    {
        Id = p.Id,
        SubmissionId = p.SubmissionId,
        LineOfBusiness = p.LineOfBusiness,
        CarrierName = p.CarrierName,
        PolicyNumber = p.PolicyNumber,
        ExpirationDate = p.ExpirationDate,
        Premium = p.Premium,
        CreatedAt = p.CreatedAt,
    };
}
