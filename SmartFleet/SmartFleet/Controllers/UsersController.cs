using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using SmartFleet.Dtos;
using SmartFleet.Services;

namespace SmartFleet.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController(
    IUserService userService,
    IOtpService otpService,
    IWebHostEnvironment environment) : ControllerBase
{
    private readonly IUserService _userService = userService;
    private readonly IOtpService _otpService = otpService;
    private readonly IWebHostEnvironment _environment = environment;
    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
    private static readonly HashSet<string> AllowedImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp"
    };
    private const long MaxProfileImageBytes = 5 * 1024 * 1024;

    [HttpGet]
    [Authorize(Policy = "RoleAdminAccess")]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetUsers(CancellationToken cancellationToken)
    {
        var users = await _userService.GetAllAsync(cancellationToken);
        return Ok(users.Select(u => u.ToUserDto()));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserDto>> GetUser(int id, CancellationToken cancellationToken)
    {
        var user = await _userService.GetByIdAsync(id, cancellationToken);
        return user is null ? NotFound() : Ok(user.ToUserDto());
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> GetOwnProfile(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var user = await _userService.GetByIdAsync(userId, cancellationToken);
        return user is null ? NotFound() : Ok(user.ToUserDto());
    }

    [HttpPut("{id:int}/role")]
    [Authorize(Policy = "RoleAdminAccess")]
    public async Task<ActionResult<UserDto>> UpdateUserRole(int id, [FromBody] UserRoleUpdateDto request, CancellationToken cancellationToken)
    {
        var user = await _userService.UpdateRoleAsync(id, request.Role, cancellationToken);
        return user is null ? NotFound() : Ok(user.ToUserDto());
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "RoleAdminAccess")]
    public async Task<IActionResult> DeleteUser(int id, CancellationToken cancellationToken)
    {
        var deleted = await _userService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPut("me/profile")]
    public async Task<ActionResult<UserDto>> UpdateMyProfile([FromBody] UserProfileUpdateDto request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _userService.UpdateProfileAsync(userId, request, cancellationToken);

        return result.Status switch
        {
            UserProfileUpdateStatus.Success => Ok(result.User!.ToUserDto()),
            UserProfileUpdateStatus.EmailInUse => Conflict("Email is already in use."),
            _ => NotFound()
        };
    }

    [HttpPost("me/profile/photo")]
    [RequestSizeLimit(MaxProfileImageBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxProfileImageBytes)]
    public async Task<ActionResult<UserDto>> UploadMyProfilePhoto([FromForm] IFormFile? file, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        if (file is null)
        {
            return BadRequest(new { message = "No file uploaded." });
        }

        if (file.Length == 0)
        {
            return BadRequest(new { message = "Uploaded file is empty." });
        }

        if (file.Length > MaxProfileImageBytes)
        {
            return BadRequest(new { message = "File exceeds the 5 MB limit." });
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedImageExtensions.Contains(extension))
        {
            return BadRequest(new { message = "Unsupported file type." });
        }

        if (!string.IsNullOrWhiteSpace(file.ContentType) && !AllowedImageContentTypes.Contains(file.ContentType))
        {
            return BadRequest(new { message = "Unsupported file type." });
        }

        var user = await _userService.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        var webRoot = ResolveWebRootPath();
        var uploadsRoot = Path.Combine(webRoot, "uploads", "users");
        Directory.CreateDirectory(uploadsRoot);

        var safeFileName = $"{userId}_{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var destinationPath = Path.Combine(uploadsRoot, safeFileName);

        await using (var stream = System.IO.File.Create(destinationPath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        var relativePath = $"/uploads/users/{safeFileName}";

        await _userService.UpdateProfileImageAsync(userId, relativePath, cancellationToken);

        if (!string.IsNullOrWhiteSpace(user.ProfileImageUrl))
        {
            TryDeleteProfileImage(user.ProfileImageUrl);
        }

        var updatedUser = await _userService.GetByIdAsync(userId, cancellationToken);
        return updatedUser is null ? NotFound() : Ok(updatedUser.ToUserDto());
    }

    [HttpPut("me/password")]
    public async Task<IActionResult> UpdateMyPassword([FromBody] UserPasswordUpdateDto request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _userService.UpdatePasswordAsync(userId, request, cancellationToken);

        if (result.Status == UserPasswordUpdateStatus.InvalidCurrentPassword)
        {
            ModelState.AddModelError(nameof(request.CurrentPassword), "Current password is incorrect.");
            return ValidationProblem(ModelState);
        }

        return result.Status == UserPasswordUpdateStatus.Success ? NoContent() : NotFound();
    }

    [HttpPost("send-otp")]
    [Authorize(Policy = "RoleAdminAccess")]
    public async Task<IActionResult> SendOtp([FromBody] UserSendOtpDto request, CancellationToken cancellationToken)
    {
        var user = await _userService.MarkForPasswordResetAsync(request.Email, cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        try
        {
            var expiresIn = await _otpService.SendOtpAsync(user, cancellationToken);
            return Ok(new
            {
                message = $"OTP sent to {user.Email}",
                expiresInMinutes = (int)Math.Ceiling(expiresIn.TotalMinutes)
            });
        }
        catch
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to send OTP." });
        }
    }

    private bool TryGetUserId(out int userId)
    {
        var claim = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out userId);
    }

    private string ResolveWebRootPath()
    {
        if (!string.IsNullOrWhiteSpace(_environment.WebRootPath))
        {
            return _environment.WebRootPath;
        }

        var fallback = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        Directory.CreateDirectory(fallback);
        return fallback;
    }

    private void TryDeleteProfileImage(string? profileImageUrl)
    {
        if (string.IsNullOrWhiteSpace(profileImageUrl))
        {
            return;
        }

        var webRoot = ResolveWebRootPath();
        var uploadsRoot = Path.Combine(webRoot, "uploads", "users");
        var sanitizedRelative = profileImageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var candidatePath = Path.GetFullPath(Path.Combine(webRoot, sanitizedRelative));
        var uploadsRootFull = Path.GetFullPath(uploadsRoot);

        if (!candidatePath.StartsWith(uploadsRootFull, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (System.IO.File.Exists(candidatePath))
        {
            try
            {
                System.IO.File.Delete(candidatePath);
            }
            catch
            {
                // Ignore cleanup failures; a subsequent upload attempt can replace the file.
            }
        }
    }
}






