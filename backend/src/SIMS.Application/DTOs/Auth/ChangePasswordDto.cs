using System.ComponentModel.DataAnnotations;

namespace SIMS.Application.DTOs.Auth;

public class ChangePasswordDto
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, MinLength(8), MaxLength(256)]
    public string NewPassword { get; set; } = string.Empty;
}
