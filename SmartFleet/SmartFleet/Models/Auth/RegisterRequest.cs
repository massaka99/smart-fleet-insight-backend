using SmartFleet.Models;

namespace SmartFleet.Models.Auth;

public record RegisterRequest(
    string FirstName,
    string LastName,
    string Email,
    int Age,
    UserRole Role,
    string Password,
    string? ProfileImageUrl);
