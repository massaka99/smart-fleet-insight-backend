using SmartFleet.Models;

namespace SmartFleet.Services;

public interface IOtpService
{
    Task<TimeSpan> SendOtpAsync(User user, CancellationToken cancellationToken);
    OtpVerificationStatus VerifyOtp(User user, string otp);
}

public enum OtpVerificationStatus
{
    Valid,
    NotFound,
    Expired,
    Invalid
}
