using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartFleet.Authorization;
using SmartFleet.Data;
using SmartFleet.Models;

namespace SmartFleet.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController(ApplicationDbContext context, IPasswordHasher<User> passwordHasher) : ControllerBase
{
    private readonly ApplicationDbContext _context = context;
    private readonly IPasswordHasher<User> _passwordHasher = passwordHasher;

    [HttpGet]
    [Authorize(Policy = "RoleAdminAccess")]
    public async Task<ActionResult<IEnumerable<UserDetailsResponse>>> GetUsers(CancellationToken cancellationToken)
    {
        var users = await _context.Users
            .OrderBy(u => u.Id)
            .AsNoTracking()
            .Select(u => new UserDetailsResponse(
                u.Id,
                u.FirstName,
                u.LastName,
                u.Age,
                u.Role,
                RolePermissions.GetPermissions(u.Role)))
            .ToListAsync(cancellationToken);

        return Ok(users);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserDetailsResponse>> GetUser(int id, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new UserDetailsResponse(
                u.Id,
                u.FirstName,
                u.LastName,
                u.Age,
                u.Role,
                RolePermissions.GetPermissions(u.Role)))
            .FirstOrDefaultAsync(cancellationToken);

        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost]
    [Authorize(Policy = "RoleAdminAccess")]
    public async Task<ActionResult<UserDetailsResponse>> CreateUser([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
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

        var response = new UserDetailsResponse(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Age,
            user.Role,
            RolePermissions.GetPermissions(user.Role));

        return Created($"/api/users/{user.Id}", response);
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
            return Ok(new UserDetailsResponse(
                user.Id,
                user.FirstName,
                user.LastName,
                user.Age,
                user.Role,
                RolePermissions.GetPermissions(user.Role)));
        }

        user.Role = request.Role;
        await _context.SaveChangesAsync(cancellationToken);

        var response = new UserDetailsResponse(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Age,
            user.Role,
            RolePermissions.GetPermissions(user.Role));

        return Ok(response);
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

    public record CreateUserRequest(
        string FirstName,
        string LastName,
        int Age,
        UserRole Role,
        string Password);

    public record UpdateUserRoleRequest(UserRole Role);

    public record UserDetailsResponse(
        int Id,
        string FirstName,
        string LastName,
        int Age,
        UserRole Role,
        IReadOnlyCollection<string> Permissions);
}
