using System;

namespace SmartFleet.Services;

// Multi-session friendly tracker: simply acknowledges every session as valid.
internal sealed class UserSessionTracker : IUserSessionTracker
{
    public bool TryBeginSession(int userId, Guid sessionId, DateTime expiresAt) => true;

    public bool RenewSession(int userId, Guid sessionId, DateTime expiresAt) => true;

    public bool IsSessionActive(int userId, Guid sessionId) => true;

    public void EndSession(Guid sessionId)
    {
        // No-op – we intentionally allow concurrent sessions and do not track them.
    }
}
