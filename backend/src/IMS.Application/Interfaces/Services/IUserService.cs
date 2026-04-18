using IMS.Application.Common;
using IMS.Application.DTOs.Users;

namespace IMS.Application.Interfaces.Services;

public interface IUserService
{
    Task<PagedResult<UserDto>> GetAllAsync(QueryParameters query);
    Task<Result<UserDto>> GetByIdAsync(Guid id);
    Task<Result<UserDto>> CreateAsync(UserCreateDto dto);
    Task<Result<UserDto>> UpdateAsync(Guid id, UserUpdateDto dto);
    Task<Result> DeleteAsync(Guid id);
}
