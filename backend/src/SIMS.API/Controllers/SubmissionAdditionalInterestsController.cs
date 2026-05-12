using SIMS.Application.DTOs.Submissions;
using SIMS.Domain.Entities;
using SIMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/submissions/{submissionId:guid}/additional-interests")]
[Authorize(Policy = AppPermissions.UnderwritingManage)]
public class SubmissionAdditionalInterestsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public SubmissionAdditionalInterestsController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll(Guid submissionId)
    {
        var rows = await _db.SubmissionAdditionalInterests
            .Where(a => a.SubmissionId == submissionId)
            .OrderBy(a => a.LineOfBusiness)
            .ThenBy(a => a.Name)
            .Select(a => MapToDto(a))
            .ToListAsync();

        return Ok(rows);
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid submissionId, [FromBody] SubmissionAdditionalInterestCreateDto dto)
    {
        if (!await _db.Submissions.AnyAsync(s => s.Id == submissionId))
            return NotFound(new { ErrorMessage = "Submission not found." });

        var validation = Validate(dto);
        if (validation is not null) return BadRequest(new { ErrorMessage = validation });

        var row = new SubmissionAdditionalInterest { SubmissionId = submissionId };
        Apply(row, dto);

        _db.SubmissionAdditionalInterests.Add(row);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAll), new { submissionId }, MapToDto(row));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid submissionId, Guid id, [FromBody] SubmissionAdditionalInterestUpdateDto dto)
    {
        var row = await _db.SubmissionAdditionalInterests
            .FirstOrDefaultAsync(a => a.Id == id && a.SubmissionId == submissionId);
        if (row == null) return NotFound();

        var validation = Validate(dto);
        if (validation is not null) return BadRequest(new { ErrorMessage = validation });

        Apply(row, dto);
        await _db.SaveChangesAsync();
        return Ok(MapToDto(row));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid submissionId, Guid id)
    {
        var row = await _db.SubmissionAdditionalInterests
            .FirstOrDefaultAsync(a => a.Id == id && a.SubmissionId == submissionId);
        if (row == null) return NotFound();

        row.IsDeleted = true;
        row.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static string? Validate(SubmissionAdditionalInterestCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return "Name is required.";

        if (!dto.AdditionalInsured && !dto.LossPayee && !dto.WaiverOfSubrogation && !dto.PrimaryNonContributory)
            return "Select at least one requested interest.";

        if (dto.AppliesToType == Domain.Enums.AdditionalInterestAppliesToType.ScheduledItems &&
            string.IsNullOrWhiteSpace(dto.ScheduledItemNumbers))
            return "Scheduled item numbers are required when the interest applies to scheduled items.";

        return null;
    }

    private static void Apply(SubmissionAdditionalInterest row, SubmissionAdditionalInterestCreateDto dto)
    {
        row.LineOfBusiness = dto.LineOfBusiness;
        row.Name = dto.Name.Trim();
        row.AddressLine1 = dto.AddressLine1?.Trim();
        row.AddressLine2 = dto.AddressLine2?.Trim();
        row.City = dto.City?.Trim();
        row.State = dto.State?.Trim().ToUpperInvariant();
        row.ZipCode = dto.ZipCode?.Trim();
        row.Email = dto.Email?.Trim();
        row.Phone = dto.Phone?.Trim();
        row.AppliesToType = dto.AppliesToType;
        row.ScheduledItemNumbers = dto.ScheduledItemNumbers?.Trim();
        row.AdditionalInsured = dto.AdditionalInsured;
        row.LossPayee = dto.LossPayee;
        row.WaiverOfSubrogation = dto.WaiverOfSubrogation;
        row.PrimaryNonContributory = dto.PrimaryNonContributory;
        row.Notes = dto.Notes?.Trim();
    }

    private static SubmissionAdditionalInterestDto MapToDto(SubmissionAdditionalInterest a) => new()
    {
        Id = a.Id,
        SubmissionId = a.SubmissionId,
        LineOfBusiness = a.LineOfBusiness,
        Name = a.Name,
        AddressLine1 = a.AddressLine1,
        AddressLine2 = a.AddressLine2,
        City = a.City,
        State = a.State,
        ZipCode = a.ZipCode,
        Email = a.Email,
        Phone = a.Phone,
        AppliesToType = a.AppliesToType,
        ScheduledItemNumbers = a.ScheduledItemNumbers,
        AdditionalInsured = a.AdditionalInsured,
        LossPayee = a.LossPayee,
        WaiverOfSubrogation = a.WaiverOfSubrogation,
        PrimaryNonContributory = a.PrimaryNonContributory,
        Notes = a.Notes,
        CreatedAt = a.CreatedAt,
    };
}
