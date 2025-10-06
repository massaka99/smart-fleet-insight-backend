using SmartFleet.Authorization;
using SmartFleet.Models;

namespace SmartFleet.Models.Auth;

public record LoginResponse(
    int UserId,
    string FirstName,
    string LastName,
    string Email,
    string? ProfileImageUrl,
    int Age,
    UserRole Role,
    IReadOnlyCollection<string> Permissions,
    bool RequiresPasswordReset,
    string Token);


