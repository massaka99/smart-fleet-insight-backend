using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartFleet.Data;
using SmartFleet.Models;
using SmartFleet.Options;

namespace SmartFleet.Services;

public class OtpService : IOtpService
{
    private readonly IMemoryCache _cache;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<OtpService> _logger;
    private readonly ApplicationDbContext _context;
    private readonly OtpOptions _options;
    private readonly SendGridOptions _sendGridOptions;

    private record CacheEntry(string Hash, DateTimeOffset ExpiresAt);

    public OtpService(
        IMemoryCache cache,
        IEmailSender emailSender,
        ILogger<OtpService> logger,
        ApplicationDbContext context,
        IOptions<OtpOptions> options,
        IOptions<SendGridOptions> sendGridOptions)
    {
        _cache = cache;
        _emailSender = emailSender;
        _logger = logger;
        _context = context;
        _options = options.Value;
        _sendGridOptions = sendGridOptions.Value;
    }

    public async Task<TimeSpan> SendOtpAsync(User user, CancellationToken cancellationToken)
    {
        var otp = GenerateOtp();
        var hashedOtp = HashOtp(otp);
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_options.ExpiresInMinutes);

        var cacheEntry = new CacheEntry(hashedOtp, expiresAt);
        _cache.Set(GetCacheKey(user.Id), cacheEntry, expiresAt);

        // Persist OTP to the database to survive process restarts.
        if (_context.Entry(user).State == EntityState.Detached)
        {
            _context.Attach(user);
        }

        user.OtpHash = hashedOtp;
        user.OtpExpiresAt = expiresAt;
        _context.Entry(user).Property(u => u.OtpHash).IsModified = true;
        _context.Entry(user).Property(u => u.OtpExpiresAt).IsModified = true;
        await _context.SaveChangesAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(_sendGridOptions.OtpTemplateId))
        {
            throw new InvalidOperationException("SendGrid OTP template id must be configured.");
        }

        var payload = new
        {
            first_name = user.FirstName,
            code = otp,
            expires_minutes = _options.ExpiresInMinutes,
            year = DateTime.UtcNow.Year
        };

        _ = Task.Run(
            async () =>
            {
                try
                {
                    await _emailSender.SendAsync(user.Email, _sendGridOptions.OtpTemplateId, payload, CancellationToken.None);
                    _logger.LogInformation("OTP sent for user {UserId} and expires at {ExpiresAt}", user.Id, expiresAt);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send OTP email for user {UserId}", user.Id);
                }
            },
            CancellationToken.None);

        return TimeSpan.FromMinutes(_options.ExpiresInMinutes);
    }

    public OtpVerificationStatus VerifyOtp(User user, string otp)
    {
        var cacheKey = GetCacheKey(user.Id);

        if (!_cache.TryGetValue(cacheKey, out CacheEntry? entry) || entry is null)
        {
            if (string.IsNullOrWhiteSpace(user.OtpHash) || user.OtpExpiresAt is null)
            {
                return OtpVerificationStatus.NotFound;
            }

            entry = new CacheEntry(user.OtpHash, user.OtpExpiresAt.Value);
            _cache.Set(cacheKey, entry, entry.ExpiresAt);
        }

        if (entry.ExpiresAt < DateTimeOffset.UtcNow)
        {
            _cache.Remove(cacheKey);
            _logger.LogInformation("OTP expired for user {UserId}", user.Id);
            ClearPersistedOtp(user);
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
        ClearPersistedOtp(user);
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

    private void ClearPersistedOtp(User user)
    {
        if (_context.Entry(user).State == EntityState.Detached)
        {
            _context.Attach(user);
        }

        user.OtpHash = null;
        user.OtpExpiresAt = null;
        _context.Entry(user).Property(u => u.OtpHash).IsModified = true;
        _context.Entry(user).Property(u => u.OtpExpiresAt).IsModified = true;
        _context.SaveChanges();
    }
}
