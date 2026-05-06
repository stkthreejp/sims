using SIMS.Application.DTOs.Submissions;
using SIMS.Domain.Entities;
using SIMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/submissions/{submissionId:guid}/supplemental")]
[Authorize(Policy = AppPermissions.UnderwritingManage)]
public class SubmissionSupplementalController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public SubmissionSupplementalController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Get(Guid submissionId)
    {
        var s = await _db.SubmissionSupplementals.FirstOrDefaultAsync(x => x.SubmissionId == submissionId);
        if (s == null) return Ok(null);
        return Ok(MapToDto(s));
    }

    [HttpPut]
    public async Task<IActionResult> Upsert(Guid submissionId, [FromBody] SubmissionSupplementalUpsertDto dto)
    {
        if (!await _db.Submissions.AnyAsync(s => s.Id == submissionId))
            return NotFound(new { ErrorMessage = "Submission not found." });

        var s = await _db.SubmissionSupplementals.FirstOrDefaultAsync(x => x.SubmissionId == submissionId);
        if (s == null)
        {
            s = new SubmissionSupplemental { SubmissionId = submissionId };
            _db.SubmissionSupplementals.Add(s);
        }

        s.CommoditiesHauled = JsonSerializer.Serialize(dto.CommoditiesHauled);
        s.TerminalLocations = JsonSerializer.Serialize(dto.TerminalLocations);
        s.SafetyProgramInPlace = dto.SafetyProgramInPlace;
        s.FilingsRequired = JsonSerializer.Serialize(dto.FilingsRequired);
        s.OwnerOperator = dto.OwnerOperator;

        await _db.SaveChangesAsync();
        return Ok(MapToDto(s));
    }

    private static SubmissionSupplementalDto MapToDto(SubmissionSupplemental s) => new()
    {
        Id = s.Id,
        SubmissionId = s.SubmissionId,
        CommoditiesHauled = Deserialize(s.CommoditiesHauled),
        TerminalLocations = Deserialize(s.TerminalLocations),
        SafetyProgramInPlace = s.SafetyProgramInPlace,
        FilingsRequired = Deserialize(s.FilingsRequired),
        OwnerOperator = s.OwnerOperator,
        UpdatedAt = s.UpdatedAt,
    };

    private static List<string> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch { return []; }
    }
}
