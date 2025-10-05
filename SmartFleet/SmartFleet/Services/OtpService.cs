using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartFleet.Models;
using SmartFleet.Options;

namespace SmartFleet.Services;

public class OtpService : IOtpService
{
    private readonly IMemoryCache _cache;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<OtpService> _logger;
    private readonly OtpOptions _options;

    private record CacheEntry(string Hash, DateTimeOffset ExpiresAt);

    public OtpService(
        IMemoryCache cache,
        IEmailSender emailSender,
        ILogger<OtpService> logger,
        IOptions<OtpOptions> options)
    {
        _cache = cache;
        _emailSender = emailSender;
        _logger = logger;
        _options = options.Value;
    }

    public Task<TimeSpan> SendOtpAsync(User user, CancellationToken cancellationToken)
    {
        var otp = GenerateOtp();
        var hashedOtp = HashOtp(otp);
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_options.ExpiresInMinutes);

        var cacheEntry = new CacheEntry(hashedOtp, expiresAt);
        _cache.Set(GetCacheKey(user.Id), cacheEntry, expiresAt);

        var body = string.Format(_options.BodyTemplate, otp, _options.ExpiresInMinutes);
        _emailSender.Send(user.Email, _options.Subject, body);

        _logger.LogInformation("OTP sent for user {UserId} and expires at {ExpiresAt}", user.Id, expiresAt);

        return Task.FromResult(TimeSpan.FromMinutes(_options.ExpiresInMinutes));
    }

    public OtpVerificationStatus VerifyOtp(User user, string otp)
    {
        var cacheKey = GetCacheKey(user.Id);

        if (!_cache.TryGetValue(cacheKey, out CacheEntry? entry))
        {
            return OtpVerificationStatus.NotFound;
        }
        if (entry is null)
        {
            return OtpVerificationStatus.NotFound;
        }


        if (entry.ExpiresAt < DateTimeOffset.UtcNow)
        {
            _cache.Remove(cacheKey);
            _logger.LogInformation("OTP expired for user {UserId}", user.Id);
            return OtpVerificationStatus.Expired;
        }

        var hashedInput = HashOtp(otp);
        if (!string.Equals(entry.Hash, hashedInput, StringComparison.Ordinal))
        {
            _logger.LogInformation("OTP invalid for user {UserId}", user.Id);
            return OtpVerificationStatus.Invalid;
        }

        _cache.Remove(cacheKey);
        _logger.LogInformation("OTP validated for user {UserId}", user.Id);
        return OtpVerificationStatus.Valid;
    }

    private string GenerateOtp()
    {
        var length = Math.Max(4, _options.CodeLength);
        var builder = new StringBuilder(length);

        for (var i = 0; i < length; i++)
        {
            var digit = RandomNumberGenerator.GetInt32(0, 10);
            builder.Append(digit);
        }

        return builder.ToString();
    }

    private static string HashOtp(string otp)
    {
        var bytes = Encoding.UTF8.GetBytes(otp);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static string GetCacheKey(int userId) => $"otp:{userId}";
}
