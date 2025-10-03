using SmartFleet.Models;

namespace SmartFleet.Models.Auth;

public record RegisterRequest(
    string FirstName,
    string LastName,
    int Age,
    UserRole Role,
    string Password);
