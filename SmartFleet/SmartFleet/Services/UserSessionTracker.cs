using System;

namespace SmartFleet.Services;

internal sealed class UserSessionTracker : IUserSessionTracker
{
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CleanupGracePeriod = TimeSpan.FromMinutes(5);
    private readonly ConcurrentDictionary<int, object> _locks = new();

    public UserSessionTracker(IMemoryCache cache)
    {
        _cache = cache;
    }

    public bool TryBeginSession(int userId, Guid sessionId, DateTime expiresAt)
    {
        return ExecuteWithLock(userId, () =>
        {
            var key = UserKey(userId);
            if (_cache.TryGetValue<SessionRecord>(key, out var existing) && existing is not null)
            {
                if (!existing.IsExpired && existing.SessionId != sessionId)
                {
                    return false;
                }

                RemoveSession(existing.SessionId);
            }

            StoreSession(new SessionRecord(sessionId, userId, expiresAt));
            return true;
        });
    }

    public bool RenewSession(int userId, Guid sessionId, DateTime expiresAt)
    {
        return ExecuteWithLock(userId, () =>
        {
            var key = UserKey(userId);
            if (!_cache.TryGetValue<SessionRecord>(key, out var existing) || existing is null || existing.SessionId != sessionId)
            {
                return false;
            }

            var updated = existing with { ExpiresAt = expiresAt };
            StoreSession(updated);
            return true;
        });
    }

    public bool IsSessionActive(int userId, Guid sessionId)
    {
        var key = UserKey(userId);
        if (!_cache.TryGetValue<SessionRecord>(key, out var record) || record is null)
        {
            return false;
        }

        if (record.SessionId != sessionId)
        {
            return false;
        }

    public bool IsSessionActive(int userId, Guid sessionId) => true;

    public void EndSession(Guid sessionId)
    {
        // No-op – we intentionally allow concurrent sessions and do not track them.
    }
}
