using System.Text.Json.Serialization;

namespace SmartFleet.Models;

public class User
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int Age { get; set; }
    public UserRole Role { get; set; }

    [JsonIgnore]
    public string PasswordHash { get; set; } = string.Empty;
}
