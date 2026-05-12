using SIMS.Application.DTOs.Carriers;
using SIMS.Domain.Entities;
using SIMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/carriers/{carrierId:guid}/additional-interest-rates")]
[Authorize(Policy = AppPermissions.UnderwritingManage)]
public class CarrierAdditionalInterestRatesController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public CarrierAdditionalInterestRatesController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll(Guid carrierId)
    {
        var rows = await _db.CarrierAdditionalInterestRates
            .Where(r => r.CarrierId == carrierId)
            .OrderBy(r => r.LineOfBusiness)
            .ThenBy(r => r.CoverageType)
            .ThenByDescending(r => r.IsActive)
            .Select(r => MapToDto(r))
            .ToListAsync();

        return Ok(rows);
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid carrierId, [FromBody] CarrierAdditionalInterestRateCreateDto dto)
    {
        if (!await _db.Carriers.AnyAsync(c => c.Id == carrierId))
            return NotFound(new { ErrorMessage = "Carrier not found." });

        var validation = Validate(dto);
        if (validation is not null) return BadRequest(new { ErrorMessage = validation });

        var row = new CarrierAdditionalInterestRate { CarrierId = carrierId };
        Apply(row, dto);

        _db.CarrierAdditionalInterestRates.Add(row);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAll), new { carrierId }, MapToDto(row));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid carrierId, Guid id, [FromBody] CarrierAdditionalInterestRateUpdateDto dto)
    {
        var row = await _db.CarrierAdditionalInterestRates
            .FirstOrDefaultAsync(r => r.Id == id && r.CarrierId == carrierId);
        if (row == null) return NotFound();

        var validation = Validate(dto);
        if (validation is not null) return BadRequest(new { ErrorMessage = validation });

        Apply(row, dto);
        await _db.SaveChangesAsync();
        return Ok(MapToDto(row));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid carrierId, Guid id)
    {
        var row = await _db.CarrierAdditionalInterestRates
            .FirstOrDefaultAsync(r => r.Id == id && r.CarrierId == carrierId);
        if (row == null) return NotFound();

        row.IsDeleted = true;
        row.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static string? Validate(CarrierAdditionalInterestRateCreateDto dto)
    {
        if (dto.ChargeMethod == Domain.Enums.AdditionalInterestChargeMethod.PerInterest &&
            (!dto.PerInterestAmount.HasValue || dto.PerInterestAmount < 0))
            return "Per-interest amount is required for per-interest charges.";

        if (dto.ChargeMethod == Domain.Enums.AdditionalInterestChargeMethod.BlanketFlat &&
            (!dto.BlanketAmount.HasValue || dto.BlanketAmount < 0))
            return "Blanket amount is required for blanket charges.";

        if (dto.MinimumCharge.HasValue && dto.MinimumCharge < 0)
            return "Minimum charge cannot be negative.";

        if (dto.MaximumCharge.HasValue && dto.MaximumCharge < 0)
            return "Maximum charge cannot be negative.";

        return null;
    }

    private static void Apply(CarrierAdditionalInterestRate row, CarrierAdditionalInterestRateCreateDto dto)
    {
        row.LineOfBusiness = dto.LineOfBusiness;
        row.CoverageType = dto.CoverageType;
        row.ChargeMethod = dto.ChargeMethod;
        row.PerInterestAmount = dto.PerInterestAmount;
        row.BlanketAmount = dto.BlanketAmount;
        row.MinimumCharge = dto.MinimumCharge;
        row.MaximumCharge = dto.MaximumCharge;
        row.State = dto.State?.Trim().ToUpperInvariant();
        row.EffectiveDate = dto.EffectiveDate;
        row.ExpirationDate = dto.ExpirationDate;
        row.IsActive = dto.IsActive;
    }

    private static CarrierAdditionalInterestRateDto MapToDto(CarrierAdditionalInterestRate r) => new()
    {
        Id = r.Id,
        CarrierId = r.CarrierId,
        LineOfBusiness = r.LineOfBusiness,
        CoverageType = r.CoverageType,
        ChargeMethod = r.ChargeMethod,
        PerInterestAmount = r.PerInterestAmount,
        BlanketAmount = r.BlanketAmount,
        MinimumCharge = r.MinimumCharge,
        MaximumCharge = r.MaximumCharge,
        State = r.State,
        EffectiveDate = r.EffectiveDate,
        ExpirationDate = r.ExpirationDate,
        IsActive = r.IsActive,
        CreatedAt = r.CreatedAt,
    };
}
