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
    public UserVehicleSummaryDto? AssignedVehicle { get; init; }
}

public class UserVehicleSummaryDto
{
    public int Id { get; init; }
    public string LicensePlate { get; init; } = string.Empty;
    public string VehicleType { get; init; } = string.Empty;
    public string Brand { get; init; } = string.Empty;
    public string? RouteSummary { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? ExternalId { get; init; }
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
        ProfileImageUrl = ProfileImageSanitizer.Normalize(user.ProfileImageUrl),
        Age = user.Age,
        Role = user.Role,
        Permissions = RolePermissions.GetPermissions(user.Role),
        AssignedVehicle = user.Vehicle?.ToUserVehicleSummaryDto()
    };

    public static UserVehicleSummaryDto ToUserVehicleSummaryDto(this Vehicle vehicle) => new()
    {
        Id = vehicle.Id,
        LicensePlate = vehicle.LicensePlate,
        VehicleType = vehicle.VehicleType,
        Brand = vehicle.Brand,
        RouteSummary = string.IsNullOrWhiteSpace(vehicle.RouteSummary) ? null : vehicle.RouteSummary,
        Status = vehicle.Status,
        ExternalId = vehicle.ExternalId
    };
}

