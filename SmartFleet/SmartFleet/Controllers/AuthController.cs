using System.Net.Mail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartFleet.Authorization;
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
    ITokenService tokenService) : ControllerBase
{
    private readonly ApplicationDbContext _context = context;
    private readonly IPasswordHasher<User> _passwordHasher = passwordHasher;
    private readonly ITokenService _tokenService = tokenService;

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

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            ModelState.AddModelError(nameof(request.Password), "Password is required.");
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
            Role = request.Role
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        var token = _tokenService.GenerateToken(user);
        var response = new LoginResponse(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email,
            user.ProfileImageUrl,
            user.Age,
            user.Role,
            RolePermissions.GetPermissions(user.Role),
            token);

        return Created($"/api/users/{user.Id}", response);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        if (!TryNormalizeEmail(request.Email, out var normalizedEmail))
        {
            return Unauthorized();
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (user is null)
        {
            return Unauthorized();
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return Unauthorized();
        }

        var token = _tokenService.GenerateToken(user);
        var response = new LoginResponse(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email,
            user.ProfileImageUrl,
            user.Age,
            user.Role,
            RolePermissions.GetPermissions(user.Role),
            token);

        return Ok(response);
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
}
