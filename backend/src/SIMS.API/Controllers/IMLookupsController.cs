using SIMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SIMS.API.Controllers;

// Reference data specific to Inland Marine rating (equipment types, territories).
// These tables are seeded by the rating module and shared across all IM submissions.
[ApiController]
[Route("api/v1/im")]
[Authorize]
public class IMLookupsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public IMLookupsController(ApplicationDbContext db) => _db = db;

    [HttpGet("equipment-types")]
    public async Task<IActionResult> GetEquipmentTypes()
    {
        var list = await _db.EquipmentTypes
            .OrderBy(t => t.TypeNumber)
            .Select(t => new EquipmentTypeDto
            {
                Id = t.Id,
                TypeNumber = t.TypeNumber,
                Name = t.Name,
            })
            .ToListAsync();
        return Ok(list);
    }

    [HttpGet("territories")]
    public async Task<IActionResult> GetTerritories()
    {
        var list = await _db.Territories
            .OrderBy(t => t.TerritoryNumber)
            .Select(t => new TerritoryDto
            {
                Id = t.Id,
                TerritoryNumber = t.TerritoryNumber,
                Code = t.TerritoryNumber.ToString(),
                States = t.States,
                Modifier = t.Modifier,
            })
            .ToListAsync();
        return Ok(list);
    }

    public class EquipmentTypeDto
    {
        public Guid Id { get; set; }
        public int TypeNumber { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class TerritoryDto
    {
        public Guid Id { get; set; }
        public int TerritoryNumber { get; set; }
        public string Code { get; set; } = string.Empty;
        public string States { get; set; } = string.Empty;
        public decimal Modifier { get; set; }
    }
}
