using System.ComponentModel.DataAnnotations;

namespace SIMS.Application.DTOs.Auth;

public class LoginRequestDto
{
    [Required, MaxLength(256)]
    public string UserName { get; set; } = string.Empty;

    [Required, MaxLength(256)]
    public string Password { get; set; } = string.Empty;
}
