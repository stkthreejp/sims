using SIMS.Application.DTOs.Submissions;
using SIMS.Domain.Entities;
using SIMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/submissions/{submissionId:guid}/drivers")]
[Authorize]
public class SubmissionDriversController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public SubmissionDriversController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll(Guid submissionId)
    {
        var drivers = await _db.SubmissionDrivers
            .Where(d => d.SubmissionId == submissionId)
            .OrderBy(d => d.DriverNumber)
            .Select(d => MapToDto(d))
            .ToListAsync();
        return Ok(drivers);
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid submissionId, [FromBody] SubmissionDriverCreateDto dto)
    {
        if (!await _db.Submissions.AnyAsync(s => s.Id == submissionId))
            return NotFound(new { ErrorMessage = "Submission not found." });

        var driver = new SubmissionDriver
        {
            SubmissionId = submissionId,
            DriverNumber = dto.DriverNumber,
            Name = dto.Name,
            DateOfBirth = dto.DateOfBirth,
            LicenseNumber = dto.LicenseNumber,
            LicenseState = dto.LicenseState,
            DateHired = dto.DateHired,
        };
        _db.SubmissionDrivers.Add(driver);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAll), new { submissionId }, MapToDto(driver));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid submissionId, Guid id, [FromBody] SubmissionDriverUpdateDto dto)
    {
        var driver = await _db.SubmissionDrivers.FirstOrDefaultAsync(d => d.Id == id && d.SubmissionId == submissionId);
        if (driver == null) return NotFound();

        driver.DriverNumber = dto.DriverNumber;
        driver.Name = dto.Name;
        driver.DateOfBirth = dto.DateOfBirth;
        driver.LicenseNumber = dto.LicenseNumber;
        driver.LicenseState = dto.LicenseState;
        driver.DateHired = dto.DateHired;
        await _db.SaveChangesAsync();
        return Ok(MapToDto(driver));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid submissionId, Guid id)
    {
        var driver = await _db.SubmissionDrivers.FirstOrDefaultAsync(d => d.Id == id && d.SubmissionId == submissionId);
        if (driver == null) return NotFound();
        driver.IsDeleted = true;
        driver.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static SubmissionDriverDto MapToDto(SubmissionDriver d) => new()
    {
        Id = d.Id,
        SubmissionId = d.SubmissionId,
        DriverNumber = d.DriverNumber,
        Name = d.Name,
        DateOfBirth = d.DateOfBirth,
        LicenseNumber = d.LicenseNumber,
        LicenseState = d.LicenseState,
        DateHired = d.DateHired,
        CreatedAt = d.CreatedAt,
    };
}
