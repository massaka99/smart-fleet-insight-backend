using System;
using System.Collections.Generic;
using SmartFleet.Models.Auth;

namespace SmartFleet.Services;

public interface IAuthService
{
    Task<AuthRegisterResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
    Task<AuthLoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<AuthLoginResult> LoginWithOtpAsync(OtpLoginRequest request, CancellationToken cancellationToken);
    Task<ForgotPasswordResult> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken);
    Task<SetPasswordResult> SetPasswordAsync(int userId, Guid? sessionId, SetPasswordRequest request, CancellationToken cancellationToken);
    void EndSession(Guid sessionId);
}

public enum AuthRegisterStatus
{
    Success,
    ValidationFailed,
    EmailInUse,
    OtpSendFailed
}

public record AuthRegisterResult(
    AuthRegisterStatus Status,
    LoginResponse? Response = null,
    IDictionary<string, string[]>? Errors = null,
    string? Message = null);

public enum AuthLoginStatus
{
    Success,
    Unauthorized,
    PasswordResetRequired,
    OtpInvalid,
    OtpExpired,
    OtpNotFound,
    ValidationFailed
}

public record AuthLoginResult(
    AuthLoginStatus Status,
    LoginResponse? Response = null,
    IDictionary<string, string[]>? Errors = null,
    string? Message = null);

public enum ForgotPasswordStatus
{
    Completed,
    SendFailed
}

public record ForgotPasswordResult(ForgotPasswordStatus Status, string? Message = null);

public enum SetPasswordStatus
{
    Success,
    NotFound,
    PasswordAlreadySet,
    ValidationFailed
}

public record SetPasswordResult(
    SetPasswordStatus Status,
    LoginResponse? Response = null,
    IDictionary<string, string[]>? Errors = null,
    string? Message = null);
