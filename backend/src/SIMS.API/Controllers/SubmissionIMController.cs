using SIMS.Application.DTOs.Submissions;
using SIMS.Domain.Entities;
using SIMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/submissions/{submissionId:guid}/im")]
[Authorize(Policy = AppPermissions.UnderwritingManage)]
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

        var validation = await ValidateRatingFieldsAsync(dto);
        if (validation is not null) return BadRequest(new { ErrorMessage = validation });

        var e = new SubmissionEquipment
        {
            SubmissionId = submissionId,
            ItemNumber = dto.ItemNumber, Year = dto.Year, Make = dto.Make,
            Model = dto.Model, Description = dto.Description,
            SerialNumber = dto.SerialNumber, Value = dto.Value,
            EquipmentTypeId = dto.EquipmentTypeId,
            TerritoryCode = dto.TerritoryCode,
            Deductible = dto.Deductible,
            SettlementBasis = dto.SettlementBasis,
        };
        _db.SubmissionEquipment.Add(e);
        await _db.SaveChangesAsync();
        await SyncIMCoveragesAsync(submissionId);
        return CreatedAtAction(nameof(GetEquipment), new { submissionId }, MapEquipmentToDto(e));
    }

    [HttpPut("equipment/{id:guid}")]
    public async Task<IActionResult> UpdateEquipment(Guid submissionId, Guid id, [FromBody] SubmissionEquipmentUpdateDto dto)
    {
        var e = await _db.SubmissionEquipment.FirstOrDefaultAsync(x => x.Id == id && x.SubmissionId == submissionId);
        if (e == null) return NotFound();

        var validation = await ValidateRatingFieldsAsync(dto);
        if (validation is not null) return BadRequest(new { ErrorMessage = validation });

        e.ItemNumber = dto.ItemNumber; e.Year = dto.Year; e.Make = dto.Make;
        e.Model = dto.Model; e.Description = dto.Description;
        e.SerialNumber = dto.SerialNumber; e.Value = dto.Value;
        e.EquipmentTypeId = dto.EquipmentTypeId;
        e.TerritoryCode = dto.TerritoryCode;
        e.Deductible = dto.Deductible;
        e.SettlementBasis = dto.SettlementBasis;
        await _db.SaveChangesAsync();
        await SyncIMCoveragesAsync(submissionId);
        return Ok(MapEquipmentToDto(e));
    }

    private async Task SyncIMCoveragesAsync(Guid submissionId)
    {
        var values = await _db.SubmissionEquipment
            .Where(e => e.SubmissionId == submissionId && !e.IsDeleted && e.Value.HasValue)
            .Select(e => e.Value!.Value)
            .ToListAsync();

        var im = await _db.SubmissionIMCoverages.FirstOrDefaultAsync(i => i.SubmissionId == submissionId);
        if (im == null)
        {
            im = new SubmissionIMCoverages { SubmissionId = submissionId, CoinsurancePercentage = 90 };
            _db.SubmissionIMCoverages.Add(im);
        }

        im.ScheduledEquipmentTotalLimit = values.Count > 0 ? values.Sum() : null;
        im.MaximumValueAnyOneItem = values.Count > 0 ? values.Max() : null;
        im.CoinsurancePercentage ??= 90;
        await _db.SaveChangesAsync();
    }

    // Allowed deductible tiers (null is also valid — represents the "10% ACV" tier).
    private static readonly decimal[] AllowedDeductibles = new[] { 2500m, 5000m, 10000m, 25000m };
    private static readonly string[] AllowedSettlementBases = new[] { "ACV", "RCV" };

    private async Task<string?> ValidateRatingFieldsAsync(SubmissionEquipmentCreateDto dto)
    {
        if (dto.EquipmentTypeId.HasValue)
        {
            var exists = await _db.EquipmentTypes.AnyAsync(t => t.Id == dto.EquipmentTypeId.Value);
            if (!exists) return "Equipment type not found.";
        }
        if (dto.Deductible.HasValue && !AllowedDeductibles.Contains(dto.Deductible.Value))
            return $"Deductible must be one of {string.Join(", ", AllowedDeductibles)} or null (10% ACV).";
        if (!string.IsNullOrEmpty(dto.SettlementBasis) && !AllowedSettlementBases.Contains(dto.SettlementBasis))
            return "SettlementBasis must be 'ACV' or 'RCV'.";
        if (!string.IsNullOrEmpty(dto.TerritoryCode))
        {
            var exists = await _db.Territories.AnyAsync(t => t.TerritoryNumber.ToString() == dto.TerritoryCode);
            if (!exists) return "Territory not found.";
        }
        return null;
    }

    [HttpDelete("equipment/{id:guid}")]
    public async Task<IActionResult> DeleteEquipment(Guid submissionId, Guid id)
    {
        var e = await _db.SubmissionEquipment.FirstOrDefaultAsync(x => x.Id == id && x.SubmissionId == submissionId);
        if (e == null) return NotFound();
        e.IsDeleted = true; e.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await SyncIMCoveragesAsync(submissionId);
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
        SerialNumber = e.SerialNumber, Value = e.Value,
        EquipmentTypeId = e.EquipmentTypeId, TerritoryCode = e.TerritoryCode,
        Deductible = e.Deductible, SettlementBasis = e.SettlementBasis,
        CreatedAt = e.CreatedAt,
    };
}
