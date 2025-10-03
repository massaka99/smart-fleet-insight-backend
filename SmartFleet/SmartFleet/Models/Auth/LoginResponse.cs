using SmartFleet.Authorization;
using SmartFleet.Models;

namespace SmartFleet.Models.Auth;

public record LoginResponse(
    int UserId,
    string FirstName,
    string LastName,
    UserRole Role,
    IReadOnlyCollection<string> Permissions,
    string Token);
