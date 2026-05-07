using SIMS.Application.DTOs.Submissions;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;
using SIMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/submissions/{submissionId:guid}/loss-history")]
[Authorize(Policy = AppPermissions.UnderwritingManage)]
public class SubmissionLossHistoryController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public SubmissionLossHistoryController(ApplicationDbContext db) => _db = db;

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(Guid submissionId)
    {
        var years = await GetYearsQuery(submissionId).ToListAsync();
        return Ok(BuildSummary(years));
    }

    [HttpGet("years")]
    public async Task<IActionResult> GetYears(Guid submissionId)
    {
        var years = await GetYearsQuery(submissionId).ToListAsync();
        return Ok(years.Select(MapYearToDto));
    }

    [HttpPost("years")]
    public async Task<IActionResult> CreateYear(Guid submissionId, [FromBody] SubmissionLossYearCreateDto dto)
    {
        if (!await _db.Submissions.AnyAsync(s => s.Id == submissionId))
            return NotFound(new { ErrorMessage = "Submission not found." });

        var year = new SubmissionLossYear { SubmissionId = submissionId };
        ApplyYearDto(year, dto);
        _db.SubmissionLossYears.Add(year);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetYears), new { submissionId }, MapYearToDto(year));
    }

    [HttpPut("years/{yearId:guid}")]
    public async Task<IActionResult> UpdateYear(Guid submissionId, Guid yearId, [FromBody] SubmissionLossYearUpdateDto dto)
    {
        var year = await _db.SubmissionLossYears
            .Include(y => y.Claims)
            .FirstOrDefaultAsync(y => y.Id == yearId && y.SubmissionId == submissionId);
        if (year == null) return NotFound();

        ApplyYearDto(year, dto);
        await _db.SaveChangesAsync();
        return Ok(MapYearToDto(year));
    }

    [HttpDelete("years/{yearId:guid}")]
    public async Task<IActionResult> DeleteYear(Guid submissionId, Guid yearId)
    {
        var year = await _db.SubmissionLossYears
            .Include(y => y.Claims)
            .FirstOrDefaultAsync(y => y.Id == yearId && y.SubmissionId == submissionId);
        if (year == null) return NotFound();

        year.IsDeleted = true;
        year.DeletedAt = DateTime.UtcNow;
        foreach (var claim in year.Claims)
        {
            claim.IsDeleted = true;
            claim.DeletedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("years/{yearId:guid}/claims")]
    public async Task<IActionResult> CreateClaim(Guid submissionId, Guid yearId, [FromBody] SubmissionLossClaimCreateDto dto)
    {
        if (!await _db.SubmissionLossYears.AnyAsync(y => y.Id == yearId && y.SubmissionId == submissionId))
            return NotFound(new { ErrorMessage = "Loss year not found." });

        var claim = new SubmissionLossClaim { SubmissionLossYearId = yearId };
        ApplyClaimDto(claim, dto);
        _db.SubmissionLossClaims.Add(claim);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetYears), new { submissionId }, MapClaimToDto(claim));
    }

    [HttpPut("claims/{claimId:guid}")]
    public async Task<IActionResult> UpdateClaim(Guid submissionId, Guid claimId, [FromBody] SubmissionLossClaimUpdateDto dto)
    {
        var claim = await _db.SubmissionLossClaims
            .Include(c => c.LossYear)
            .FirstOrDefaultAsync(c => c.Id == claimId && c.LossYear.SubmissionId == submissionId);
        if (claim == null) return NotFound();

        ApplyClaimDto(claim, dto);
        await _db.SaveChangesAsync();
        return Ok(MapClaimToDto(claim));
    }

    [HttpDelete("claims/{claimId:guid}")]
    public async Task<IActionResult> DeleteClaim(Guid submissionId, Guid claimId)
    {
        var claim = await _db.SubmissionLossClaims
            .Include(c => c.LossYear)
            .FirstOrDefaultAsync(c => c.Id == claimId && c.LossYear.SubmissionId == submissionId);
        if (claim == null) return NotFound();

        claim.IsDeleted = true;
        claim.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private IQueryable<SubmissionLossYear> GetYearsQuery(Guid submissionId) =>
        _db.SubmissionLossYears
            .Include(y => y.Claims)
            .Where(y => y.SubmissionId == submissionId)
            .OrderByDescending(y => y.PolicyYear)
            .ThenBy(y => y.LineOfBusiness);

    private static void ApplyYearDto(SubmissionLossYear year, SubmissionLossYearCreateDto dto)
    {
        year.PolicyYear = dto.PolicyYear;
        year.LineOfBusiness = dto.LineOfBusiness;
        year.CarrierName = dto.CarrierName;
        year.PolicyNumber = dto.PolicyNumber;
        year.PremiumAmount = dto.PremiumAmount;
        year.PremiumBasis = dto.PremiumBasis;
        year.IsSmmWritten = dto.IsSmmWritten;
        year.Source = dto.Source;
        year.AsOfDate = dto.AsOfDate;
        year.PaidOverride = dto.PaidOverride;
        year.ReservedOverride = dto.ReservedOverride;
        year.ExpenseOverride = dto.ExpenseOverride;
        year.Notes = dto.Notes;
    }

    private static void ApplyClaimDto(SubmissionLossClaim claim, SubmissionLossClaimCreateDto dto)
    {
        claim.DateOfLoss = dto.DateOfLoss;
        claim.ClaimNumber = dto.ClaimNumber;
        claim.Status = dto.Status;
        claim.Description = dto.Description;
        claim.CoverageType = dto.CoverageType;
        claim.Paid = dto.Paid;
        claim.Reserved = dto.Reserved;
        claim.Expense = dto.Expense;
    }

    private static SubmissionLossHistorySummaryDto BuildSummary(List<SubmissionLossYear> years)
    {
        var yearDtos = years.Select(MapYearToDto).ToList();
        var claimDtos = yearDtos.SelectMany(y => y.Claims).ToList();
        var totalPremium = yearDtos.Sum(y => y.PremiumAmount);
        var totalIncurred = yearDtos.Sum(y => y.Incurred);
        var claimCount = claimDtos.Count;

        return new SubmissionLossHistorySummaryDto
        {
            YearCount = yearDtos.Count,
            ClaimCount = claimCount,
            TotalPremium = totalPremium,
            TotalPaid = yearDtos.Sum(y => y.Paid),
            TotalReserved = yearDtos.Sum(y => y.Reserved),
            TotalExpense = yearDtos.Sum(y => y.Expense),
            TotalIncurred = totalIncurred,
            LossRatio = totalPremium > 0 ? totalIncurred / totalPremium : null,
            AverageSeverity = claimCount > 0 ? totalIncurred / claimCount : null,
            LargestLoss = claimDtos.Count > 0 ? claimDtos.Max(c => c.Incurred) : 0,
            OpenReserve = claimDtos.Where(c => c.Status == LossClaimStatus.Open).Sum(c => c.Reserved),
            Years = yearDtos,
        };
    }

    private static SubmissionLossYearDto MapYearToDto(SubmissionLossYear y)
    {
        var claims = y.Claims.OrderByDescending(c => c.DateOfLoss).Select(MapClaimToDto).ToList();
        var paid = y.PaidOverride ?? claims.Sum(c => c.Paid);
        var reserved = y.ReservedOverride ?? claims.Sum(c => c.Reserved);
        var expense = y.ExpenseOverride ?? claims.Sum(c => c.Expense);
        var incurred = paid + reserved + expense;

        return new SubmissionLossYearDto
        {
            Id = y.Id,
            SubmissionId = y.SubmissionId,
            PolicyYear = y.PolicyYear,
            LineOfBusiness = y.LineOfBusiness,
            CarrierName = y.CarrierName,
            PolicyNumber = y.PolicyNumber,
            PremiumAmount = y.PremiumAmount,
            PremiumBasis = y.PremiumBasis,
            IsSmmWritten = y.IsSmmWritten,
            Source = y.Source,
            AsOfDate = y.AsOfDate,
            PaidOverride = y.PaidOverride,
            ReservedOverride = y.ReservedOverride,
            ExpenseOverride = y.ExpenseOverride,
            Notes = y.Notes,
            Paid = paid,
            Reserved = reserved,
            Expense = expense,
            Incurred = incurred,
            LossRatio = y.PremiumAmount > 0 ? incurred / y.PremiumAmount : null,
            ClaimCount = claims.Count,
            CreatedAt = y.CreatedAt,
            Claims = claims,
        };
    }

    private static SubmissionLossClaimDto MapClaimToDto(SubmissionLossClaim c) => new()
    {
        Id = c.Id,
        SubmissionLossYearId = c.SubmissionLossYearId,
        DateOfLoss = c.DateOfLoss,
        ClaimNumber = c.ClaimNumber,
        Status = c.Status,
        Description = c.Description,
        CoverageType = c.CoverageType,
        Paid = c.Paid,
        Reserved = c.Reserved,
        Expense = c.Expense,
        Incurred = c.Paid + c.Reserved + c.Expense,
        CreatedAt = c.CreatedAt,
    };
}
