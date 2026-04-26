using IMS.Application.DTOs.Submissions;
using IMS.Domain.Entities;
using IMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IMS.API.Controllers;

[ApiController]
[Route("api/v1/submissions/{submissionId:guid}/im")]
[Authorize]
public class SubmissionIMController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public SubmissionIMController(ApplicationDbContext db) => _db = db;

    [HttpGet("coverages")]
    public async Task<IActionResult> GetCoverages(Guid submissionId)
    {
        var im = await _db.SubmissionIMCoverages
            .FirstOrDefaultAsync(i => i.SubmissionId == submissionId);
        if (im == null) return Ok(null);
        return Ok(MapCoveragesToDto(im));
    }

    [HttpPut("coverages")]
    public async Task<IActionResult> UpsertCoverages(Guid submissionId, [FromBody] SubmissionIMCoveragesUpsertDto dto)
    {
        if (!await _db.Submissions.AnyAsync(s => s.Id == submissionId))
            return NotFound(new { ErrorMessage = "Submission not found." });

        var im = await _db.SubmissionIMCoverages
            .FirstOrDefaultAsync(i => i.SubmissionId == submissionId);

        if (im == null) { im = new SubmissionIMCoverages { SubmissionId = submissionId }; _db.SubmissionIMCoverages.Add(im); }

        im.ScheduledEquipmentTotalLimit = dto.ScheduledEquipmentTotalLimit;
        im.UnscheduledEquipmentLimit = dto.UnscheduledEquipmentLimit;
        im.MaximumValueAnyOneItem = dto.MaximumValueAnyOneItem;
        im.Deductible = dto.Deductible;
        im.CoinsurancePercentage = dto.CoinsurancePercentage;
        await _db.SaveChangesAsync();
        return Ok(MapCoveragesToDto(im));
    }

    [HttpGet("equipment")]
    public async Task<IActionResult> GetEquipment(Guid submissionId)
    {
        var list = await _db.SubmissionEquipment
            .Where(e => e.SubmissionId == submissionId)
            .OrderBy(e => e.ItemNumber)
            .Select(e => MapEquipmentToDto(e))
            .ToListAsync();
        return Ok(list);
    }

    [HttpPost("equipment")]
    public async Task<IActionResult> CreateEquipment(Guid submissionId, [FromBody] SubmissionEquipmentCreateDto dto)
    {
        if (!await _db.Submissions.AnyAsync(s => s.Id == submissionId))
            return NotFound(new { ErrorMessage = "Submission not found." });

        var e = new SubmissionEquipment
        {
            SubmissionId = submissionId,
            ItemNumber = dto.ItemNumber, Year = dto.Year, Make = dto.Make,
            Model = dto.Model, Description = dto.Description,
            SerialNumber = dto.SerialNumber, Value = dto.Value,
        };
        _db.SubmissionEquipment.Add(e);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetEquipment), new { submissionId }, MapEquipmentToDto(e));
    }

    [HttpPut("equipment/{id:guid}")]
    public async Task<IActionResult> UpdateEquipment(Guid submissionId, Guid id, [FromBody] SubmissionEquipmentUpdateDto dto)
    {
        var e = await _db.SubmissionEquipment.FirstOrDefaultAsync(x => x.Id == id && x.SubmissionId == submissionId);
        if (e == null) return NotFound();
        e.ItemNumber = dto.ItemNumber; e.Year = dto.Year; e.Make = dto.Make;
        e.Model = dto.Model; e.Description = dto.Description;
        e.SerialNumber = dto.SerialNumber; e.Value = dto.Value;
        await _db.SaveChangesAsync();
        return Ok(MapEquipmentToDto(e));
    }

    [HttpDelete("equipment/{id:guid}")]
    public async Task<IActionResult> DeleteEquipment(Guid submissionId, Guid id)
    {
        var e = await _db.SubmissionEquipment.FirstOrDefaultAsync(x => x.Id == id && x.SubmissionId == submissionId);
        if (e == null) return NotFound();
        e.IsDeleted = true; e.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static SubmissionIMCoveragesDto MapCoveragesToDto(SubmissionIMCoverages i) => new()
    {
        Id = i.Id, SubmissionId = i.SubmissionId,
        ScheduledEquipmentTotalLimit = i.ScheduledEquipmentTotalLimit,
        UnscheduledEquipmentLimit = i.UnscheduledEquipmentLimit,
        MaximumValueAnyOneItem = i.MaximumValueAnyOneItem,
        Deductible = i.Deductible, CoinsurancePercentage = i.CoinsurancePercentage,
        UpdatedAt = i.UpdatedAt,
    };

    private static SubmissionEquipmentDto MapEquipmentToDto(SubmissionEquipment e) => new()
    {
        Id = e.Id, SubmissionId = e.SubmissionId, ItemNumber = e.ItemNumber,
        Year = e.Year, Make = e.Make, Model = e.Model, Description = e.Description,
        SerialNumber = e.SerialNumber, Value = e.Value, CreatedAt = e.CreatedAt,
    };
}
