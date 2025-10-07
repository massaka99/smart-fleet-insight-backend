using System.Net.Mail;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using SmartFleet.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartFleet.Data;
using SmartFleet.Models;
using SmartFleet.Models.Auth;
using SmartFleet.Services;

namespace SmartFleet.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(
    ApplicationDbContext context,
    IPasswordHasher<User> passwordHasher,
    ITokenService tokenService,
    IOtpService otpService) : ControllerBase
{
    private readonly ApplicationDbContext _context = context;
    private readonly IPasswordHasher<User> _passwordHasher = passwordHasher;
    private readonly ITokenService _tokenService = tokenService;
    private readonly IOtpService _otpService = otpService;

    [HttpPost("register")]
    [Authorize(Policy = "RoleAdminAccess")]
    public async Task<ActionResult<LoginResponse>> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var firstName = request.FirstName.Trim();
        var lastName = request.LastName.Trim();
        var hasValidEmail = TryNormalizeEmail(request.Email, out var normalizedEmail);

        if (string.IsNullOrWhiteSpace(firstName))
        {
            ModelState.AddModelError(nameof(request.FirstName), "First name is required.");
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            ModelState.AddModelError(nameof(request.LastName), "Last name is required.");
        }

        if (!hasValidEmail)
        {
            ModelState.AddModelError(nameof(request.Email), "A valid email address is required.");
        }

        if (request.Age <= 0)
        {
            ModelState.AddModelError(nameof(request.Age), "Age must be greater than zero.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var emailInUse = await _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (emailInUse)
        {
            return Conflict("Email is already in use.");
        }

        var user = new User
        {
            FirstName = firstName,
            LastName = lastName,
            Email = normalizedEmail,
            ProfileImageUrl = NormalizeOptional(request.ProfileImageUrl),
            Age = request.Age,
            Role = request.Role,
            RequiresPasswordReset = true
        };

        var temporaryPassword = Guid.NewGuid().ToString("N");
        user.PasswordHash = _passwordHasher.HashPassword(user, temporaryPassword);

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            await _otpService.SendOtpAsync(user, cancellationToken);
        }
        catch
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to send OTP email." });
        }

        var token = _tokenService.GenerateToken(user);
        return Created($"/api/users/{user.Id}", CreateLoginResponse(user, token));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        if (!TryNormalizeEmail(request.Email, out var normalizedEmail))
        {
            return Unauthorized();
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (user is null)
        {
            return Unauthorized();
        }

        if (user.RequiresPasswordReset)
        {
            return BadRequest(new { message = "Password reset required. Use OTP login." });
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return Unauthorized();
        }

        var token = _tokenService.GenerateToken(user);
        return Ok(CreateLoginResponse(user, token));
    }

    [HttpPost("login-otp")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> LoginWithOtp([FromBody] OtpLoginRequest request, CancellationToken cancellationToken)
    {
        if (!TryNormalizeEmail(request.Email, out var normalizedEmail))
        {
            return Unauthorized();
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (user is null)
        {
            return Unauthorized();
        }

        var otpCode = request.OtpPassword?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(otpCode))
        {
            ModelState.AddModelError(nameof(request.OtpPassword), "OTP code is required.");
            return ValidationProblem(ModelState);
        }

        var status = _otpService.VerifyOtp(user, otpCode);

        if (status != OtpVerificationStatus.Valid)
        {
            return status switch
            {
                OtpVerificationStatus.Expired => BadRequest(new { message = "OTP expired." }),
                OtpVerificationStatus.Invalid => BadRequest(new { message = "OTP invalid." }),
                _ => BadRequest(new { message = "OTP not found." })
            };
        }

        var token = _tokenService.GenerateToken(user);
        return Ok(CreateLoginResponse(user, token));
    }

    [HttpPost("set-password")]
    [Authorize]
    public async Task<ActionResult<LoginResponse>> SetPassword([FromBody] SetPasswordRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            ModelState.AddModelError(nameof(request.NewPassword), "New password is required.");
            return ValidationProblem(ModelState);
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            return Unauthorized();
        }

        if (!user.RequiresPasswordReset)
        {
            return BadRequest(new { message = "Password is already set." });
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);
        user.RequiresPasswordReset = false;
        await _context.SaveChangesAsync(cancellationToken);

        var token = _tokenService.GenerateToken(user);
        return Ok(CreateLoginResponse(user, token));
    }

    private static LoginResponse CreateLoginResponse(User user, string token) => new(
        user.Id,
        user.FirstName,
        user.LastName,
        user.Email,
        user.ProfileImageUrl,
        user.Age,
        user.Role,
        RolePermissions.GetPermissions(user.Role),
        user.RequiresPasswordReset,
        token);

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

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private bool TryGetUserId(out int userId)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out userId);
    }
}






