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
            .OrderBy(l => l.LocationNumber)
            .Select(l => MapToDto(l))
            .ToListAsync();
        return Ok(locations);
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid submissionId, [FromBody] SubmissionLocationCreateDto dto)
    {
        if (!await _db.Submissions.AnyAsync(s => s.Id == submissionId))
            return NotFound(new { ErrorMessage = "Submission not found." });

        var location = new SubmissionLocation
        {
            SubmissionId = submissionId,
            LocationNumber = dto.LocationNumber,
            Address = dto.Address,
            ZipCode = dto.ZipCode,
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
        location.LocationNumber = dto.LocationNumber;
        location.Address = dto.Address;
        location.ZipCode = dto.ZipCode;
        await _db.SaveChangesAsync();
        return Ok(MapToDto(location));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid submissionId, Guid id)
    {
        var location = await _db.SubmissionLocations.FirstOrDefaultAsync(l => l.Id == id && l.SubmissionId == submissionId);
        if (location == null) return NotFound();
        location.IsDeleted = true;
        location.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static SubmissionLocationDto MapToDto(SubmissionLocation l) => new()
    {
        Id = l.Id,
        SubmissionId = l.SubmissionId,
        LocationNumber = l.LocationNumber,
        Address = l.Address,
        ZipCode = l.ZipCode,
        CreatedAt = l.CreatedAt,
    };
}
