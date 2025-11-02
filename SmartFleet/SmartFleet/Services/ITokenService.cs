using SmartFleet.Models;

namespace SmartFleet.Services;

public interface ITokenService
{
    GeneratedToken GenerateToken(User user, Guid sessionId);
}
