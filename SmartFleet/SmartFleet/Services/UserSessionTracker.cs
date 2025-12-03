using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

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
                // Always replace any existing session (expired or active) so a fresh login
                // becomes the single active session for the user.
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

        if (record.IsExpired)
        {
            RemoveSession(sessionId);
            return false;
        }

        return true;
    }

    public void EndSession(Guid sessionId)
    {
        if (_cache.TryGetValue<SessionRecord>(SessionKey(sessionId), out var record) && record is not null)
        {
            ExecuteWithLock(record.UserId, () =>
            {
                RemoveSession(sessionId);
                return true;
            });
            _locks.TryRemove(record.UserId, out _);
        }
    }

    private bool ExecuteWithLock(int userId, Func<bool> action)
    {
        var lockObject = _locks.GetOrAdd(userId, _ => new object());
        lock (lockObject)
        {
            return action();
        }
    }

    private void StoreSession(SessionRecord record)
    {
        var expiry = record.ExpiresAt > DateTime.UtcNow ? record.ExpiresAt : DateTime.UtcNow;
        var absoluteExpiration = DateTimeOffset.UtcNow + (expiry - DateTime.UtcNow) + CleanupGracePeriod;
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpiration = absoluteExpiration
        };

        _cache.Set(UserKey(record.UserId), record, options);
        _cache.Set(SessionKey(record.SessionId), record, options);
    }

    private void RemoveSession(Guid sessionId)
    {
        if (_cache.TryGetValue<SessionRecord>(SessionKey(sessionId), out var record) && record is not null)
        {
            _cache.Remove(SessionKey(sessionId));
            _cache.Remove(UserKey(record.UserId));
        }
    }

    private static string UserKey(int userId) => $"session:user:{userId}";
    private static string SessionKey(Guid sessionId) => $"session:id:{sessionId}";

    private record SessionRecord(Guid SessionId, int UserId, DateTime ExpiresAt)
    {
        public bool IsExpired => ExpiresAt <= DateTime.UtcNow;
    }
}
