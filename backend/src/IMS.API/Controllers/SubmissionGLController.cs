using IMS.Application.DTOs.Submissions;
using IMS.Domain.Entities;
using IMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IMS.API.Controllers;

[ApiController]
[Route("api/v1/submissions/{submissionId:guid}/gl")]
[Authorize]
public class SubmissionGLController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public SubmissionGLController(ApplicationDbContext db) => _db = db;

    [HttpGet("coverages")]
    public async Task<IActionResult> GetCoverages(Guid submissionId)
    {
        var gl = await _db.SubmissionGLCoverages
            .FirstOrDefaultAsync(g => g.SubmissionId == submissionId);
        if (gl == null) return Ok(null);
        return Ok(MapCoveragesToDto(gl));
    }

    [HttpPut("coverages")]
    public async Task<IActionResult> UpsertCoverages(Guid submissionId, [FromBody] SubmissionGLCoveragesUpsertDto dto)
    {
        if (!await _db.Submissions.AnyAsync(s => s.Id == submissionId))
            return NotFound(new { ErrorMessage = "Submission not found." });

        var gl = await _db.SubmissionGLCoverages
            .FirstOrDefaultAsync(g => g.SubmissionId == submissionId);

        if (gl == null) { gl = new SubmissionGLCoverages { SubmissionId = submissionId }; _db.SubmissionGLCoverages.Add(gl); }

        gl.GeneralAggregate = dto.GeneralAggregate;
        gl.ProductsCompletedOps = dto.ProductsCompletedOps;
        gl.EachOccurrence = dto.EachOccurrence;
        gl.PersonalAndAdvInjury = dto.PersonalAndAdvInjury;
        gl.DamageToRentedPremises = dto.DamageToRentedPremises;
        gl.MedicalExpense = dto.MedicalExpense;
        gl.TotalSubcontractorCost = dto.TotalSubcontractorCost;
        await _db.SaveChangesAsync();
        return Ok(MapCoveragesToDto(gl));
    }

    [HttpGet("classifications")]
    public async Task<IActionResult> GetClassifications(Guid submissionId)
    {
        var list = await _db.SubmissionGLClassifications
            .Where(c => c.SubmissionId == submissionId)
            .OrderBy(c => c.LocationNumber)
            .Select(c => MapClassificationToDto(c))
            .ToListAsync();
        return Ok(list);
    }

    [HttpPost("classifications")]
    public async Task<IActionResult> CreateClassification(Guid submissionId, [FromBody] SubmissionGLClassificationCreateDto dto)
    {
        if (!await _db.Submissions.AnyAsync(s => s.Id == submissionId))
            return NotFound(new { ErrorMessage = "Submission not found." });

        var c = new SubmissionGLClassification
        {
            SubmissionId = submissionId,
            LocationNumber = dto.LocationNumber,
            ClassCode = dto.ClassCode,
            Description = dto.Description,
            PremiumBasis = dto.PremiumBasis,
            Exposure = dto.Exposure,
        };
        _db.SubmissionGLClassifications.Add(c);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetClassifications), new { submissionId }, MapClassificationToDto(c));
    }

    [HttpPut("classifications/{id:guid}")]
    public async Task<IActionResult> UpdateClassification(Guid submissionId, Guid id, [FromBody] SubmissionGLClassificationUpdateDto dto)
    {
        var c = await _db.SubmissionGLClassifications.FirstOrDefaultAsync(x => x.Id == id && x.SubmissionId == submissionId);
        if (c == null) return NotFound();
        c.LocationNumber = dto.LocationNumber; c.ClassCode = dto.ClassCode; c.Description = dto.Description;
        c.PremiumBasis = dto.PremiumBasis; c.Exposure = dto.Exposure;
        await _db.SaveChangesAsync();
        return Ok(MapClassificationToDto(c));
    }

    [HttpDelete("classifications/{id:guid}")]
    public async Task<IActionResult> DeleteClassification(Guid submissionId, Guid id)
    {
        var c = await _db.SubmissionGLClassifications.FirstOrDefaultAsync(x => x.Id == id && x.SubmissionId == submissionId);
        if (c == null) return NotFound();
        c.IsDeleted = true; c.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static SubmissionGLCoveragesDto MapCoveragesToDto(SubmissionGLCoverages g) => new()
    {
        Id = g.Id, SubmissionId = g.SubmissionId,
        GeneralAggregate = g.GeneralAggregate, ProductsCompletedOps = g.ProductsCompletedOps,
        EachOccurrence = g.EachOccurrence, PersonalAndAdvInjury = g.PersonalAndAdvInjury,
        DamageToRentedPremises = g.DamageToRentedPremises, MedicalExpense = g.MedicalExpense,
        TotalSubcontractorCost = g.TotalSubcontractorCost,
        UpdatedAt = g.UpdatedAt,
    };

    private static SubmissionGLClassificationDto MapClassificationToDto(SubmissionGLClassification c) => new()
    {
        Id = c.Id, SubmissionId = c.SubmissionId, LocationNumber = c.LocationNumber,
        ClassCode = c.ClassCode, Description = c.Description,
        PremiumBasis = c.PremiumBasis, Exposure = c.Exposure, CreatedAt = c.CreatedAt,
    };
}
