using SmartFleet.Models;

namespace SmartFleet.Services;

public interface ITokenService
{
    string GenerateToken(User user);
}
