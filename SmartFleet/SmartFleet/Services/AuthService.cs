using System;
using System.Collections.Generic;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using SmartFleet.Authorization;
using SmartFleet.Dtos;
using SmartFleet.Models;
using SmartFleet.Models.Auth;
using SmartFleet.Data.Repositories;

namespace SmartFleet.Services;

public class AuthService(
    IUserRepository userRepository,
    IPasswordHasher<User> passwordHasher,
    ITokenService tokenService,
    IOtpService otpService,
    IUserSessionTracker sessionTracker,
    ILogger<AuthService> logger) : IAuthService
{
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IPasswordHasher<User> _passwordHasher = passwordHasher;
    private readonly ITokenService _tokenService = tokenService;
    private readonly IOtpService _otpService = otpService;
    private readonly IUserSessionTracker _sessionTracker = sessionTracker;
    private readonly ILogger<AuthService> _logger = logger;

    public async Task<AuthRegisterResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var errors = ValidateRegisterRequest(request, out var normalizedEmail);

        if (errors.Count > 0 || normalizedEmail is null)
        {
            return new AuthRegisterResult(AuthRegisterStatus.ValidationFailed, Errors: errors);
        }

        if (await _userRepository.EmailExistsAsync(normalizedEmail, cancellationToken))
        {
            return new AuthRegisterResult(AuthRegisterStatus.EmailInUse);
        }

        var user = new User
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = normalizedEmail,
            Age = request.Age,
            Role = request.Role,
            RequiresPasswordReset = true
        };

        var temporaryPassword = Guid.NewGuid().ToString("N");
        user.PasswordHash = _passwordHasher.HashPassword(user, temporaryPassword);

        _userRepository.Add(user);
        await _userRepository.SaveChangesAsync(cancellationToken);

        try
        {
            await _otpService.SendOtpAsync(user, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send OTP email for user {UserId}", user.Id);
            return new AuthRegisterResult(
                AuthRegisterStatus.OtpSendFailed,
                Message: "Failed to send OTP email.");
        }

        var token = _tokenService.GenerateToken(user, Guid.NewGuid());
        _sessionTracker.TryBeginSession(user.Id, token.SessionId, token.ExpiresAt);

        return new AuthRegisterResult(
            AuthRegisterStatus.Success,
            CreateLoginResponse(user, token.Token));
    }

    public async Task<AuthLoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        if (!TryNormalizeEmail(request.Email, out var normalizedEmail))
        {
            return new AuthLoginResult(AuthLoginStatus.Unauthorized);
        }

        var user = await _userRepository.GetByEmailAsync(normalizedEmail, includeVehicle: true, asTracking: false, cancellationToken);

        if (user is null)
        {
            return new AuthLoginResult(AuthLoginStatus.Unauthorized);
        }

        if (user.RequiresPasswordReset)
        {
            return new AuthLoginResult(
                AuthLoginStatus.PasswordResetRequired,
                Message: "Password reset required. Use OTP login.");
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return new AuthLoginResult(AuthLoginStatus.Unauthorized);
        }

        var token = _tokenService.GenerateToken(user, Guid.NewGuid());
        _sessionTracker.TryBeginSession(user.Id, token.SessionId, token.ExpiresAt);

        return new AuthLoginResult(
            AuthLoginStatus.Success,
            CreateLoginResponse(user, token.Token));
    }

    public async Task<AuthLoginResult> LoginWithOtpAsync(OtpLoginRequest request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();

        if (!TryNormalizeEmail(request.Email, out var normalizedEmail))
        {
            return new AuthLoginResult(AuthLoginStatus.Unauthorized);
        }

        var otpCode = request.OtpPassword?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(otpCode))
        {
            errors[nameof(request.OtpPassword)] = new[] { "OTP code is required." };
            return new AuthLoginResult(AuthLoginStatus.ValidationFailed, Errors: errors);
        }

        var user = await _userRepository.GetByEmailAsync(normalizedEmail, includeVehicle: true, asTracking: false, cancellationToken);

        if (user is null)
        {
            return new AuthLoginResult(AuthLoginStatus.Unauthorized);
        }

        var status = _otpService.VerifyOtp(user, otpCode);

        if (status != OtpVerificationStatus.Valid)
        {
            return status switch
            {
                OtpVerificationStatus.Expired => new AuthLoginResult(AuthLoginStatus.OtpExpired, Message: "OTP expired."),
                OtpVerificationStatus.Invalid => new AuthLoginResult(AuthLoginStatus.OtpInvalid, Message: "OTP invalid."),
                _ => new AuthLoginResult(AuthLoginStatus.OtpNotFound, Message: "OTP not found.")
            };
        }

        var token = _tokenService.GenerateToken(user, Guid.NewGuid());
        _sessionTracker.TryBeginSession(user.Id, token.SessionId, token.ExpiresAt);

        return new AuthLoginResult(
            AuthLoginStatus.Success,
            CreateLoginResponse(user, token.Token));
    }

    public async Task<ForgotPasswordResult> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        if (!TryNormalizeEmail(request.Email, out var normalizedEmail))
        {
            return new ForgotPasswordResult(ForgotPasswordStatus.Completed);
        }

        var user = await _userRepository.GetByEmailAsync(normalizedEmail, includeVehicle: true, asTracking: true, cancellationToken);

        if (user is null)
        {
            return new ForgotPasswordResult(ForgotPasswordStatus.Completed);
        }

        try
        {
            await _otpService.SendOtpAsync(user, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset OTP for user {UserId}", user.Id);
            return new ForgotPasswordResult(ForgotPasswordStatus.SendFailed, "Failed to send reset email.");
        }

        user.RequiresPasswordReset = true;
        await _userRepository.SaveChangesAsync(cancellationToken);

        return new ForgotPasswordResult(ForgotPasswordStatus.Completed);
    }

    public async Task<SetPasswordResult> SetPasswordAsync(int userId, Guid? sessionId, SetPasswordRequest request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            errors[nameof(request.NewPassword)] = new[] { "New password is required." };
            return new SetPasswordResult(SetPasswordStatus.ValidationFailed, Errors: errors);
        }

        var user = await _userRepository.GetByIdAsync(userId, includeVehicle: true, asTracking: true, cancellationToken);

        if (user is null)
        {
            return new SetPasswordResult(SetPasswordStatus.NotFound);
        }

        if (!user.RequiresPasswordReset)
        {
            return new SetPasswordResult(
                SetPasswordStatus.PasswordAlreadySet,
                Message: "Password is already set.");
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);
        user.RequiresPasswordReset = false;
        await _userRepository.SaveChangesAsync(cancellationToken);

        var token = _tokenService.GenerateToken(user, sessionId ?? Guid.NewGuid());

        if (sessionId.HasValue)
        {
            _sessionTracker.RenewSession(user.Id, token.SessionId, token.ExpiresAt);
        }
        else
        {
            _sessionTracker.TryBeginSession(user.Id, token.SessionId, token.ExpiresAt);
        }

        return new SetPasswordResult(
            SetPasswordStatus.Success,
            CreateLoginResponse(user, token.Token));
    }

    public void EndSession(Guid sessionId)
    {
        _sessionTracker.EndSession(sessionId);
    }

    private static bool TryNormalizeEmail(string email, out string normalizedEmail)
    {
        var trimmed = email.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || !MailAddress.TryCreate(trimmed, out _))
        {
            normalizedEmail = string.Empty;
            return false;
        }

        normalizedEmail = trimmed.ToLowerInvariant();
        return true;
    }

    private static Dictionary<string, string[]> ValidateRegisterRequest(RegisterRequest request, out string? normalizedEmail)
    {
        var errors = new Dictionary<string, string[]>();
        normalizedEmail = null;

        if (string.IsNullOrWhiteSpace(request.FirstName))
        {
            errors[nameof(request.FirstName)] = new[] { "First name is required." };
        }

        if (string.IsNullOrWhiteSpace(request.LastName))
        {
            errors[nameof(request.LastName)] = new[] { "Last name is required." };
        }

        if (!TryNormalizeEmail(request.Email, out var email))
        {
            errors[nameof(request.Email)] = new[] { "A valid email address is required." };
        }
        else
        {
            normalizedEmail = email;
        }

        if (request.Age <= 0)
        {
            errors[nameof(request.Age)] = new[] { "Age must be greater than zero." };
        }

        return errors;
    }

    private static LoginResponse CreateLoginResponse(User user, string token) => new(
        user.Id,
        user.FirstName,
        user.LastName,
        user.Email,
        ProfileImageSanitizer.Normalize(user.ProfileImageUrl),
        user.Age,
        user.Role,
        RolePermissions.GetPermissions(user.Role),
        user.RequiresPasswordReset,
        user.Vehicle?.ToUserVehicleSummaryDto(),
        token);
}
