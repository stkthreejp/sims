using IMS.Application.Common;
using IMS.Application.DTOs.Auth;

namespace IMS.Application.Interfaces.Services;

public interface IAuthService
{
    Task<Result<LoginResponseDto>> LoginAsync(LoginRequestDto dto, string ipAddress);
    Task<Result<LoginResponseDto>> LoginWithMicrosoftAsync(MicrosoftLoginRequestDto dto, string ipAddress);
    Task<Result<LoginResponseDto>> RefreshTokenAsync(string refreshToken, string ipAddress);
    Task<Result> LogoutAsync(string refreshToken, string ipAddress);
    Task<Result<UserInfoDto>> GetCurrentUserAsync(Guid userId);
    Task<Result> ChangePasswordAsync(Guid userId, ChangePasswordDto dto);
}
