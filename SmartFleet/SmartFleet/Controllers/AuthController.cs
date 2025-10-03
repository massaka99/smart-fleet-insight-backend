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

        if (string.IsNullOrWhiteSpace(firstName))
        {
            ModelState.AddModelError(nameof(request.FirstName), "FirstName is required.");
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            ModelState.AddModelError(nameof(request.LastName), "LastName is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            ModelState.AddModelError(nameof(request.Password), "Password is required.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var existingUser = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.FirstName == firstName && u.LastName == lastName, cancellationToken);

        if (existingUser is not null)
        {
            return Conflict("User already exists.");
        }

        var user = new User
        {
            FirstName = firstName,
            LastName = lastName,
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
            user.Role,
            RolePermissions.GetPermissions(user.Role),
            token);

        return Created($"/api/users/{user.Id}", response);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var firstName = request.FirstName.Trim();
        var lastName = request.LastName.Trim();

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.FirstName == firstName && u.LastName == lastName, cancellationToken);

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
            user.Role,
            RolePermissions.GetPermissions(user.Role),
            token);

        return Ok(response);
    }
}
