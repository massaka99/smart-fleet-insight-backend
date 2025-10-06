using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartFleet.Dtos;
using SmartFleet.Services;

namespace SmartFleet.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController(
    IUserService userService,
    IOtpService otpService) : ControllerBase
{
    private readonly IUserService _userService = userService;
    private readonly IOtpService _otpService = otpService;

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
}






