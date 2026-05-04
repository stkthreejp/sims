using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/rating-plan-versions")]
[Authorize(Roles = "Admin,Underwriter")]
public class RatingPlanVersionsController : ControllerBase
{
    private readonly ICarrierRatingAssignmentService _svc;

    public RatingPlanVersionsController(ICarrierRatingAssignmentService svc) => _svc = svc;

    /// <summary>Returns all Active plan versions for a given LOB, for use in assignment pickers.</summary>
    [HttpGet]
    public async Task<IActionResult> GetForLob([FromQuery] PolicyLineOfBusiness lob, CancellationToken ct)
        => Ok(await _svc.GetActiveVersionsForLobAsync(lob, ct));
}
