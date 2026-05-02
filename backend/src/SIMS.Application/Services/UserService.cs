using SIMS.Application.Common;
using SIMS.Application.DTOs.Users;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace SIMS.Application.Services;

public class UserService : IUserService
{
    private readonly UserManager<User> _userManager;

    public UserService(UserManager<User> userManager)
    {
        _userManager = userManager;
    }

    public async Task<PagedResult<UserDto>> GetAllAsync(QueryParameters query)
    {
        var q = _userManager.Users
            .Where(u => !u.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.ToLower();
            q = q.Where(u =>
                u.UserName!.ToLower().Contains(s) ||
                u.Email!.ToLower().Contains(s) ||
                u.FirstName.ToLower().Contains(s) ||
                u.LastName.ToLower().Contains(s));
        }

        var total = await q.CountAsync();
        var users = await q
            .OrderByDescending(u => u.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        var dtos = new List<UserDto>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            dtos.Add(MapToDto(user, roles));
        }

        return new PagedResult<UserDto>
        {
            Items = dtos,
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<Result<UserDto>> GetByIdAsync(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null || user.IsDeleted)
            return Result<UserDto>.Failure("NOT_FOUND", "User not found.");

        var roles = await _userManager.GetRolesAsync(user);
        return Result<UserDto>.Success(MapToDto(user, roles));
    }

    public async Task<Result<UserDto>> CreateAsync(UserCreateDto dto)
    {
        var user = new User
        {
            UserName = dto.UserName,
            Email = dto.Email,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            PhoneNumber = dto.PhoneNumber,
            MustChangePassword = true
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            return Result<UserDto>.Failure("CREATE_FAILED", string.Join(", ", result.Errors.Select(e => e.Description)));

        if (dto.Roles.Any())
            await _userManager.AddToRolesAsync(user, dto.Roles);

        var roles = await _userManager.GetRolesAsync(user);
        return Result<UserDto>.Success(MapToDto(user, roles));
    }

    public async Task<Result<UserDto>> UpdateAsync(Guid id, UserUpdateDto dto)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null || user.IsDeleted)
            return Result<UserDto>.Failure("NOT_FOUND", "User not found.");

        user.Email = dto.Email;
        user.FirstName = dto.FirstName;
        user.LastName = dto.LastName;
        user.PhoneNumber = dto.PhoneNumber;
        user.Status = dto.Status;
        user.UpdatedAt = DateTime.UtcNow;

        await _userManager.UpdateAsync(user);

        var currentRoles = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, currentRoles);
        if (dto.Roles.Any())
            await _userManager.AddToRolesAsync(user, dto.Roles);

        var roles = await _userManager.GetRolesAsync(user);
        return Result<UserDto>.Success(MapToDto(user, roles));
    }

    public async Task<Result> DeleteAsync(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null || user.IsDeleted)
            return Result.Failure("NOT_FOUND", "User not found.");

        user.IsDeleted = true;
        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);
        return Result.Success();
    }

    private static UserDto MapToDto(User u, IList<string> roles) => new()
    {
        Id = u.Id,
        UserName = u.UserName!,
        Email = u.Email!,
        FirstName = u.FirstName,
        LastName = u.LastName,
        FullName = u.FullName,
        PhoneNumber = u.PhoneNumber,
        Status = u.Status,
        LastLoginAt = u.LastLoginAt,
        MustChangePassword = u.MustChangePassword,
        CreatedAt = u.CreatedAt,
        Roles = roles
    };
}
