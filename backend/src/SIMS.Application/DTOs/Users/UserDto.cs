using System.ComponentModel.DataAnnotations;
using SIMS.Domain.Enums;

namespace SIMS.Application.DTOs.Users;

public class UserDto
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public UserStatus Status { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTime CreatedAt { get; set; }
    public IEnumerable<string> Roles { get; set; } = Enumerable.Empty<string>();
}

public class UserCreateDto
{
    [Required, MaxLength(50)]
    public string UserName { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Phone, MaxLength(30)]
    public string? PhoneNumber { get; set; }

    [Required, MinLength(8), MaxLength(256)]
    public string Password { get; set; } = string.Empty;

    public IEnumerable<string> Roles { get; set; } = Enumerable.Empty<string>();
}

public class UserUpdateDto
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Phone, MaxLength(30)]
    public string? PhoneNumber { get; set; }

    public UserStatus Status { get; set; }
    public IEnumerable<string> Roles { get; set; } = Enumerable.Empty<string>();
}
