using SIMS.Application.DTOs.Submissions;
using SIMS.Domain.Entities;
using SIMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/submissions/{submissionId:guid}/locations")]
[Authorize(Policy = AppPermissions.UnderwritingManage)]
public class SubmissionLocationsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public SubmissionLocationsController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll(Guid submissionId)
    {
        var locations = await _db.SubmissionLocations
            .Where(l => l.SubmissionId == submissionId)
            .OrderByDescending(l => l.IsPrimary)
            .ThenBy(l => l.LocationNumber)
            .Select(l => MapToDto(l))
            .ToListAsync();
        return Ok(locations);
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid submissionId, [FromBody] SubmissionLocationCreateDto dto)
    {
        if (!await _db.Submissions.AnyAsync(s => s.Id == submissionId))
            return NotFound(new { ErrorMessage = "Submission not found." });

        var shouldBePrimary = dto.IsPrimary || !await _db.SubmissionLocations.AnyAsync(l => l.SubmissionId == submissionId && l.IsPrimary);
        if (shouldBePrimary)
            await ClearPrimaryLocationsAsync(submissionId);

        var location = new SubmissionLocation
        {
            SubmissionId = submissionId,
            LocationNumber = dto.LocationNumber,
            Address = dto.Address.Trim(),
            City = TrimToNull(dto.City),
            State = NormalizeCode(dto.State),
            County = TrimToNull(dto.County),
            ZipCode = TrimToNull(dto.ZipCode),
            Country = NormalizeCode(dto.Country),
            IsPrimary = shouldBePrimary,
        };
        _db.SubmissionLocations.Add(location);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAll), new { submissionId }, MapToDto(location));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid submissionId, Guid id, [FromBody] SubmissionLocationUpdateDto dto)
    {
        var location = await _db.SubmissionLocations.FirstOrDefaultAsync(l => l.Id == id && l.SubmissionId == submissionId);
        if (location == null) return NotFound();
        var shouldBePrimary = dto.IsPrimary || !await _db.SubmissionLocations.AnyAsync(l => l.SubmissionId == submissionId && l.Id != id && l.IsPrimary);
        if (shouldBePrimary)
            await ClearPrimaryLocationsAsync(submissionId, id);

        location.LocationNumber = dto.LocationNumber;
        location.Address = dto.Address.Trim();
        location.City = TrimToNull(dto.City);
        location.State = NormalizeCode(dto.State);
        location.County = TrimToNull(dto.County);
        location.ZipCode = TrimToNull(dto.ZipCode);
        location.Country = NormalizeCode(dto.Country);
        location.IsPrimary = shouldBePrimary;
        await _db.SaveChangesAsync();
        return Ok(MapToDto(location));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid submissionId, Guid id)
    {
        var location = await _db.SubmissionLocations.FirstOrDefaultAsync(l => l.Id == id && l.SubmissionId == submissionId);
        if (location == null) return NotFound();
        var wasPrimary = location.IsPrimary;
        location.IsDeleted = true;
        location.DeletedAt = DateTime.UtcNow;
        location.IsPrimary = false;

        if (wasPrimary)
        {
            var replacement = await _db.SubmissionLocations
                .Where(l => l.SubmissionId == submissionId && l.Id != id)
                .OrderBy(l => l.LocationNumber)
                .FirstOrDefaultAsync();
            if (replacement != null)
                replacement.IsPrimary = true;
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static SubmissionLocationDto MapToDto(SubmissionLocation l) => new()
    {
        Id = l.Id,
        SubmissionId = l.SubmissionId,
        LocationNumber = l.LocationNumber,
        Address = l.Address,
        City = l.City,
        State = l.State,
        County = l.County,
        ZipCode = l.ZipCode,
        Country = l.Country,
        IsPrimary = l.IsPrimary,
        CreatedAt = l.CreatedAt,
    };

    private async Task ClearPrimaryLocationsAsync(Guid submissionId, Guid? exceptId = null)
    {
        var query = _db.SubmissionLocations.Where(l => l.SubmissionId == submissionId && l.IsPrimary);
        if (exceptId.HasValue)
            query = query.Where(l => l.Id != exceptId.Value);

        var currentPrimaries = await query.ToListAsync();
        foreach (var primary in currentPrimaries)
            primary.IsPrimary = false;
    }

    private static string? TrimToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string? NormalizeCode(string? value) => TrimToNull(value)?.ToUpperInvariant();
}
