using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using SmartFleet.Services;

namespace SmartFleet.Tests.Services;

public class UserSessionTrackerTests
{
    [Fact]
    public void TryBeginSession_BlocksConcurrentSessionsForSameUser()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var tracker = new UserSessionTracker(cache);
        var userId = 10;
        var firstSession = Guid.NewGuid();

        tracker.TryBeginSession(userId, firstSession, DateTime.UtcNow.AddMinutes(5)).Should().BeTrue();

        var competingSession = Guid.NewGuid();
        tracker.TryBeginSession(userId, competingSession, DateTime.UtcNow.AddMinutes(5)).Should().BeFalse();
        tracker.IsSessionActive(userId, firstSession).Should().BeTrue();
    }

    [Fact]
    public void TryBeginSession_AllowsReplacementAfterExpiredSession()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var tracker = new UserSessionTracker(cache);
        var userId = 11;

        tracker.TryBeginSession(userId, Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-5)).Should().BeTrue();

        var replacementSession = Guid.NewGuid();
        tracker.TryBeginSession(userId, replacementSession, DateTime.UtcNow.AddMinutes(5)).Should().BeTrue();
    }

    [Fact]
    public async Task RenewSession_ExtendsActiveSession()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var tracker = new UserSessionTracker(cache);
        var userId = 12;
        var sessionId = Guid.NewGuid();

        tracker.TryBeginSession(userId, sessionId, DateTime.UtcNow.AddMilliseconds(20)).Should().BeTrue();

        tracker.RenewSession(userId, sessionId, DateTime.UtcNow.AddMilliseconds(200)).Should().BeTrue();

        await Task.Delay(80);

        tracker.IsSessionActive(userId, sessionId).Should().BeTrue();
    }

    [Fact]
    public void RenewSession_ReturnsFalse_WhenSessionIsUnknown()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var tracker = new UserSessionTracker(cache);

        tracker.RenewSession(13, Guid.NewGuid(), DateTime.UtcNow.AddMinutes(1)).Should().BeFalse();
    }

    [Fact]
    public void EndSession_RemovesSessionForUser()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var tracker = new UserSessionTracker(cache);
        var userId = 14;
        var sessionId = Guid.NewGuid();

        tracker.TryBeginSession(userId, sessionId, DateTime.UtcNow.AddMinutes(1)).Should().BeTrue();

        tracker.EndSession(sessionId);

        tracker.IsSessionActive(userId, sessionId).Should().BeFalse();
        tracker.TryBeginSession(userId, Guid.NewGuid(), DateTime.UtcNow.AddMinutes(1)).Should().BeTrue();
    }

    [Fact]
    public async Task IsSessionActive_RemovesExpiredSessionAndAllowsNewOne()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var tracker = new UserSessionTracker(cache);
        var userId = 15;
        var sessionId = Guid.NewGuid();

        tracker.TryBeginSession(userId, sessionId, DateTime.UtcNow.AddMilliseconds(30)).Should().BeTrue();

        await Task.Delay(80);

        tracker.IsSessionActive(userId, sessionId).Should().BeFalse();
        tracker.TryBeginSession(userId, Guid.NewGuid(), DateTime.UtcNow.AddMinutes(1)).Should().BeTrue();
    }

    [Fact]
    public void IsSessionActive_ReturnsFalse_WhenSessionIdDoesNotMatch()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var tracker = new UserSessionTracker(cache);
        var userId = 16;
        var sessionId = Guid.NewGuid();
        tracker.TryBeginSession(userId, sessionId, DateTime.UtcNow.AddMinutes(1)).Should().BeTrue();

        tracker.IsSessionActive(userId, Guid.NewGuid()).Should().BeFalse();
    }
}
