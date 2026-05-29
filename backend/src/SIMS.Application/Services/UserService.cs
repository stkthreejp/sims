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
    private readonly RoleManager<Role> _roleManager;
    private readonly DbContext _db;

    public UserService(UserManager<User> userManager, RoleManager<Role> roleManager, DbContext db)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _db = db;
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
        var requestedRoles = NormalizeRoles(dto.Roles);
        var roleValidation = await ValidateRolesAsync(requestedRoles);
        if (roleValidation is not null)
            return Result<UserDto>.Failure(roleValidation.Value.Code, roleValidation.Value.Message);

        var user = new User
        {
            UserName = dto.UserName,
            Email = dto.Email,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            PhoneNumber = dto.PhoneNumber,
            MustChangePassword = true
        };

        await using var transaction = await BeginTransactionIfSupportedAsync();
        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            return IdentityFailure<UserDto>("CREATE_FAILED", result);

        if (requestedRoles.Any())
        {
            var roleResult = await _userManager.AddToRolesAsync(user, requestedRoles);
            if (!roleResult.Succeeded)
                return IdentityFailure<UserDto>("ROLE_UPDATE_FAILED", roleResult);
        }

        if (transaction is not null)
            await transaction.CommitAsync();

        var roles = await _userManager.GetRolesAsync(user);
        return Result<UserDto>.Success(MapToDto(user, roles));
    }

    public async Task<Result<UserDto>> UpdateAsync(Guid id, UserUpdateDto dto)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null || user.IsDeleted)
            return Result<UserDto>.Failure("NOT_FOUND", "User not found.");

        var requestedRoles = NormalizeRoles(dto.Roles);
        var roleValidation = await ValidateRolesAsync(requestedRoles);
        if (roleValidation is not null)
            return Result<UserDto>.Failure(roleValidation.Value.Code, roleValidation.Value.Message);

        await using var transaction = await BeginTransactionIfSupportedAsync();

        user.Email = dto.Email;
        user.FirstName = dto.FirstName;
        user.LastName = dto.LastName;
        user.PhoneNumber = dto.PhoneNumber;
        user.Status = dto.Status;
        user.UpdatedAt = DateTime.UtcNow;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
            return IdentityFailure<UserDto>("UPDATE_FAILED", updateResult);

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Any())
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded)
                return IdentityFailure<UserDto>("ROLE_UPDATE_FAILED", removeResult);
        }

        if (requestedRoles.Any())
        {
            var addResult = await _userManager.AddToRolesAsync(user, requestedRoles);
            if (!addResult.Succeeded)
                return IdentityFailure<UserDto>("ROLE_UPDATE_FAILED", addResult);
        }

        if (transaction is not null)
            await transaction.CommitAsync();

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
        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
            return IdentityFailure("DELETE_FAILED", updateResult);

        return Result.Success();
    }

    private async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction?> BeginTransactionIfSupportedAsync()
        => _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync()
            : null;

    private async Task<(string Code, string Message)?> ValidateRolesAsync(IReadOnlyCollection<string> roles)
    {
        foreach (var role in roles)
        {
            if (string.IsNullOrWhiteSpace(role) || !await _roleManager.RoleExistsAsync(role))
                return ("ROLE_UPDATE_FAILED", $"Role '{role}' does not exist.");
        }

        return null;
    }

    private static string[] NormalizeRoles(IEnumerable<string> roles) =>
        roles.Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static Result<T> IdentityFailure<T>(string code, IdentityResult result) =>
        Result<T>.Failure(code, FormatIdentityErrors(result));

    private static Result IdentityFailure(string code, IdentityResult result) =>
        Result.Failure(code, FormatIdentityErrors(result));

    private static string FormatIdentityErrors(IdentityResult result) =>
        string.Join(", ", result.Errors.Select(error => error.Description));

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
