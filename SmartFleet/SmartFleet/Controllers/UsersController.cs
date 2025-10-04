using System.IdentityModel.Tokens.Jwt;
using System.Net.Mail;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartFleet.Authorization;
using SmartFleet.Data;
using SmartFleet.Models;
using SmartFleet.Options;
using SmartFleet.Services;

namespace SmartFleet.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController(
    ApplicationDbContext context,
    IPasswordHasher<User> passwordHasher,
    IOtpService otpService,
    IOptions<OtpOptions> otpOptions) : ControllerBase
{
    private readonly ApplicationDbContext _context = context;
    private readonly IPasswordHasher<User> _passwordHasher = passwordHasher;
    private readonly IOtpService _otpService = otpService;
    private readonly OtpOptions _otpOptions = otpOptions.Value;

    [HttpGet]
    [Authorize(Policy = "RoleAdminAccess")]
    public async Task<ActionResult<IEnumerable<UserDetailsResponse>>> GetUsers(CancellationToken cancellationToken)
    {
        var users = await _context.Users
            .OrderBy(u => u.Id)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return Ok(users.Select(MapToResponse));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserDetailsResponse>> GetUser(int id, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        return user is null ? NotFound() : Ok(MapToResponse(user));
    }

    [HttpPut("{id:int}/role")]
    [Authorize(Policy = "RoleAdminAccess")]
    public async Task<ActionResult<UserDetailsResponse>> UpdateUserRole(int id, [FromBody] UpdateUserRoleRequest request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        if (user.Role == request.Role)
        {
            return Ok(MapToResponse(user));
        }

        user.Role = request.Role;
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(MapToResponse(user));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "RoleAdminAccess")]
    public async Task<IActionResult> DeleteUser(int id, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpPut("me/profile")]
    public async Task<ActionResult<UserDetailsResponse>> UpdateMyProfile([FromBody] UpdateUserProfileRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

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

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        var emailInUse = await _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.Email == normalizedEmail && u.Id != userId, cancellationToken);

        if (emailInUse)
        {
            return Conflict("Email is already in use.");
        }

        user.FirstName = firstName;
        user.LastName = lastName;
        user.Email = normalizedEmail;
        user.Age = request.Age;
        user.ProfileImageUrl = NormalizeOptional(request.ProfileImageUrl);

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(MapToResponse(user));
    }

    [HttpPut("me/password")]
    public async Task<IActionResult> UpdateMyPassword([FromBody] UpdateUserPasswordRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
        {
            ModelState.AddModelError(nameof(request.CurrentPassword), "Current password is required.");
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            ModelState.AddModelError(nameof(request.NewPassword), "New password is required.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword);

        if (verificationResult == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(nameof(request.CurrentPassword), "Current password is incorrect.");
            return ValidationProblem(ModelState);
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);
        await _context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpPost("send-otp")]
    [Authorize(Policy = "RoleAdminAccess")]
    public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest request, CancellationToken cancellationToken)
    {
        if (!TryNormalizeEmail(request.Email, out var normalizedEmail))
        {
            ModelState.AddModelError(nameof(request.Email), "A valid email address is required.");
            return ValidationProblem(ModelState);
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        try
        {
            var expiresIn = await _otpService.SendOtpAsync(user, cancellationToken);
            return Ok(new
            {
                message = $"OTP sent to {normalizedEmail}",
                expiresInMinutes = (int)Math.Ceiling(expiresIn.TotalMinutes)
            });
        }
        catch (SmtpException)
        {
            ModelState.AddModelError(nameof(request.Email), "Failed to send OTP email.");
            return ValidationProblem(ModelState);
        }
        catch (Exception)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to send OTP." });
        }
    }

    [HttpPost("verify-otp")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request, CancellationToken cancellationToken)
    {
        if (!TryNormalizeEmail(request.Email, out var normalizedEmail))
        {
            ModelState.AddModelError(nameof(request.Email), "A valid email address is required.");
            return ValidationProblem(ModelState);
        }

        var code = request.Code?.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            ModelState.AddModelError(nameof(request.Code), "OTP code is required.");
            return ValidationProblem(ModelState);
        }

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        var status = _otpService.VerifyOtp(user, code);

        return status switch
        {
            OtpVerificationStatus.Valid => Ok(new { message = "OTP verified." }),
            OtpVerificationStatus.Expired => BadRequest(new { message = "OTP expired." }),
            OtpVerificationStatus.Invalid => BadRequest(new { message = "OTP invalid." }),
            _ => BadRequest(new { message = "OTP not found." })
        };
    }
    private static UserDetailsResponse MapToResponse(User user) => new(
        user.Id,
        user.FirstName,
        user.LastName,
        user.Email,
        user.ProfileImageUrl,
        user.Age,
        user.Role,
        RolePermissions.GetPermissions(user.Role));

    private bool TryGetUserId(out int userId)
    {
        var claim = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out userId);
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

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public record UpdateUserRoleRequest(UserRole Role);

    public record UpdateUserProfileRequest(
        string FirstName,
        string LastName,
        string Email,
        int Age,
        string? ProfileImageUrl);

    public record UpdateUserPasswordRequest(
        string CurrentPassword,
        string NewPassword);

    public record SendOtpRequest(string Email);

    public record VerifyOtpRequest(string Email, string Code);

    public record UserDetailsResponse(
        int Id,
        string FirstName,
        string LastName,
        string Email,
        string? ProfileImageUrl,
        int Age,
        UserRole Role,
        IReadOnlyCollection<string> Permissions);
}


