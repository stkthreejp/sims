using IMS.Application.DTOs.Submissions;
using IMS.Domain.Entities;
using IMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IMS.API.Controllers;

[ApiController]
[Route("api/v1/submissions/{submissionId:guid}/vehicles")]
[Authorize]
public class SubmissionVehiclesController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public SubmissionVehiclesController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll(Guid submissionId)
    {
        var vehicles = await _db.SubmissionVehicles
            .Where(v => v.SubmissionId == submissionId)
            .OrderBy(v => v.UnitNumber)
            .Select(v => MapToDto(v))
            .ToListAsync();
        return Ok(vehicles);
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid submissionId, [FromBody] SubmissionVehicleCreateDto dto)
    {
        if (!await _db.Submissions.AnyAsync(s => s.Id == submissionId))
            return NotFound(new { ErrorMessage = "Submission not found." });

        var vehicle = new SubmissionVehicle
        {
            SubmissionId = submissionId,
            UnitNumber = dto.UnitNumber,
            Year = dto.Year,
            Make = dto.Make,
            Model = dto.Model,
            Vin = dto.Vin,
            Gvw = dto.Gvw,
            VehicleClass = dto.VehicleClass,
            GaragingZip = dto.GaragingZip,
            Radius = dto.Radius,
        };
        _db.SubmissionVehicles.Add(vehicle);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAll), new { submissionId }, MapToDto(vehicle));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid submissionId, Guid id, [FromBody] SubmissionVehicleUpdateDto dto)
    {
        var vehicle = await _db.SubmissionVehicles.FirstOrDefaultAsync(v => v.Id == id && v.SubmissionId == submissionId);
        if (vehicle == null) return NotFound();

        vehicle.UnitNumber = dto.UnitNumber;
        vehicle.Year = dto.Year;
        vehicle.Make = dto.Make;
        vehicle.Model = dto.Model;
        vehicle.Vin = dto.Vin;
        vehicle.Gvw = dto.Gvw;
        vehicle.VehicleClass = dto.VehicleClass;
        vehicle.GaragingZip = dto.GaragingZip;
        vehicle.Radius = dto.Radius;
        await _db.SaveChangesAsync();
        return Ok(MapToDto(vehicle));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid submissionId, Guid id)
    {
        var vehicle = await _db.SubmissionVehicles.FirstOrDefaultAsync(v => v.Id == id && v.SubmissionId == submissionId);
        if (vehicle == null) return NotFound();
        vehicle.IsDeleted = true;
        vehicle.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static SubmissionVehicleDto MapToDto(SubmissionVehicle v) => new()
    {
        Id = v.Id,
        SubmissionId = v.SubmissionId,
        UnitNumber = v.UnitNumber,
        Year = v.Year,
        Make = v.Make,
        Model = v.Model,
        Vin = v.Vin,
        Gvw = v.Gvw,
        VehicleClass = v.VehicleClass,
        GaragingZip = v.GaragingZip,
        Radius = v.Radius,
        CreatedAt = v.CreatedAt,
    };
}
