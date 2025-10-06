using System.Text.Json.Serialization;

namespace SmartFleet.Models;

public class User
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
    public int Age { get; set; }
    public UserRole Role { get; set; }
    public bool RequiresPasswordReset { get; set; }

    [JsonIgnore]
    public string PasswordHash { get; set; } = string.Empty;
}

