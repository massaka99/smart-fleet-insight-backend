using System.ComponentModel.DataAnnotations;
using SmartFleet.Authorization;
using SmartFleet.Models;

namespace SmartFleet.Dtos;

public class UserDto
{
    public int Id { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? ProfileImageUrl { get; init; }
    public int Age { get; init; }
    public UserRole Role { get; init; }
    public IReadOnlyCollection<string> Permissions { get; init; } = Array.Empty<string>();
}

public class UserProfileUpdateDto
{
    [Required]
    [StringLength(100)]
    public string FirstName { get; init; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string LastName { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Range(1, 150)]
    public int Age { get; init; }

    [StringLength(2048)]
    public string? ProfileImageUrl { get; init; }
}

public class UserRoleUpdateDto
{
    [Required]
    public UserRole Role { get; init; }
}

public class UserPasswordUpdateDto
{
    [Required]
    public string CurrentPassword { get; init; } = string.Empty;

    [Required]
    public string NewPassword { get; init; } = string.Empty;
}

public class UserSendOtpDto
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;
}

public class UserVerifyOtpDto
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Code { get; init; } = string.Empty;
}

public class UserResetPasswordDto
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string OtpPassword { get; init; } = string.Empty;

    [Required]
    public string NewPassword { get; init; } = string.Empty;
}

public static class UserMappingExtensions
{
    public static UserDto ToUserDto(this User user) => new()
    {
        Id = user.Id,
        FirstName = user.FirstName,
        LastName = user.LastName,
        Email = user.Email,
        ProfileImageUrl = user.ProfileImageUrl,
        Age = user.Age,
        Role = user.Role,
        Permissions = RolePermissions.GetPermissions(user.Role)
    };
}

