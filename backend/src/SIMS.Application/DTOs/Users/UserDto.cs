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
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Password { get; set; } = string.Empty;
    public IEnumerable<string> Roles { get; set; } = Enumerable.Empty<string>();
}

public class UserUpdateDto
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public UserStatus Status { get; set; }
    public IEnumerable<string> Roles { get; set; } = Enumerable.Empty<string>();
}
