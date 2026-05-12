using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIMS.Application.DTOs.Carriers;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;
using SIMS.Infrastructure.Data;

namespace SIMS.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/premium-charges")]
[Authorize(Policy = AppPermissions.AdminSystemManage)]
public class PremiumChargesController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public PremiumChargesController(ApplicationDbContext db) => _db = db;

    [HttpGet("additional-interest-rates")]
    public async Task<IActionResult> GetAdditionalInterestRates(CancellationToken ct)
    {
        var rows = await _db.CarrierAdditionalInterestRates
            .OrderBy(r => r.CarrierId == null ? 0 : 1)
            .ThenBy(r => r.Carrier!.Name)
            .ThenBy(r => r.LineOfBusiness)
            .ThenBy(r => r.CoverageType)
            .Select(r => MapToDto(r))
            .ToListAsync(ct);

        return Ok(rows);
    }

    [HttpPost("additional-interest-rates")]
    public async Task<IActionResult> CreateAdditionalInterestRate(
        [FromBody] CarrierAdditionalInterestRateCreateDto dto,
        CancellationToken ct)
    {
        var validation = await Validate(dto, ct);
        if (validation is not null) return BadRequest(new { ErrorMessage = validation });

        var row = new CarrierAdditionalInterestRate();
        Apply(row, dto);

        _db.CarrierAdditionalInterestRates.Add(row);
        await _db.SaveChangesAsync(ct);
        return Ok(MapToDto(row));
    }

    [HttpPut("additional-interest-rates/{id:guid}")]
    public async Task<IActionResult> UpdateAdditionalInterestRate(
        Guid id,
        [FromBody] CarrierAdditionalInterestRateUpdateDto dto,
        CancellationToken ct)
    {
        var row = await _db.CarrierAdditionalInterestRates.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (row is null) return NotFound();

        var validation = await Validate(dto, ct);
        if (validation is not null) return BadRequest(new { ErrorMessage = validation });

        Apply(row, dto);
        await _db.SaveChangesAsync(ct);
        return Ok(MapToDto(row));
    }

    [HttpDelete("additional-interest-rates/{id:guid}")]
    public async Task<IActionResult> DeleteAdditionalInterestRate(Guid id, CancellationToken ct)
    {
        var row = await _db.CarrierAdditionalInterestRates.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (row is null) return NotFound();

        row.IsDeleted = true;
        row.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<string?> Validate(CarrierAdditionalInterestRateCreateDto dto, CancellationToken ct)
    {
        if (dto.CarrierId.HasValue && !await _db.Carriers.AnyAsync(c => c.Id == dto.CarrierId.Value, ct))
            return "Carrier not found.";

        if (dto.ChargeMethod == AdditionalInterestChargeMethod.PerInterest &&
            (!dto.PerInterestAmount.HasValue || dto.PerInterestAmount < 0))
            return "Per-interest amount is required for per-interest charges.";

        if (dto.ChargeMethod == AdditionalInterestChargeMethod.BlanketFlat &&
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
        row.CarrierId = dto.CarrierId;
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
