namespace SmartFleet.Services;

public interface IUserSessionTracker
{
    bool TryBeginSession(int userId, Guid sessionId, DateTime expiresAt);
    bool RenewSession(int userId, Guid sessionId, DateTime expiresAt);
    bool IsSessionActive(int userId, Guid sessionId);
    void EndSession(Guid sessionId);
}

