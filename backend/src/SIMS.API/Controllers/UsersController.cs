using SIMS.Application.Common;
using SIMS.Application.DTOs.Users;
using SIMS.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService) => _userService = userService;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] QueryParameters query)
    {
        var result = await _userService.GetAllAsync(query);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _userService.GetByIdAsync(id);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { result.ErrorMessage });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UserCreateDto dto)
    {
        var result = await _userService.CreateAsync(dto);
        if (!result.IsSuccess) return BadRequest(new { result.ErrorCode, result.ErrorMessage });
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UserUpdateDto dto)
    {
        var result = await _userService.UpdateAsync(id, dto);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _userService.DeleteAsync(id);
        return result.IsSuccess ? NoContent() : NotFound(new { result.ErrorMessage });
    }
}
