using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartFleet.Models.Auth;
using SmartFleet.Services;

namespace SmartFleet.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthService authService) : ControllerBase
{
    private readonly IAuthService _authService = authService;

    [HttpPost("register")]
    [Authorize(Policy = "RoleAdminAccess")]
    public async Task<ActionResult<LoginResponse>> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.RegisterAsync(request, cancellationToken);

        return result.Status switch
        {
            AuthRegisterStatus.Success => Created($"/api/users/{result.Response!.UserId}", result.Response),
            AuthRegisterStatus.ValidationFailed => BuildValidationProblem(result.Errors),
            AuthRegisterStatus.EmailInUse => Conflict("Email is already in use."),
            AuthRegisterStatus.OtpSendFailed => StatusCode(StatusCodes.Status500InternalServerError, new { message = result.Message ?? "Failed to send OTP email." }),
            _ => Problem("Unexpected registration result.")
        };
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(request, cancellationToken);

        return result.Status switch
        {
            AuthLoginStatus.Success => Ok(result.Response),
            AuthLoginStatus.PasswordResetRequired => BadRequest(new { message = result.Message ?? "Password reset required. Use OTP login." }),
            AuthLoginStatus.Unauthorized => Unauthorized(),
            _ => Problem("Unexpected login result.")
        };
    }

    [HttpPost("login-otp")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> LoginWithOtp([FromBody] OtpLoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginWithOtpAsync(request, cancellationToken);

        return result.Status switch
        {
            AuthLoginStatus.Success => Ok(result.Response),
            AuthLoginStatus.ValidationFailed => BuildValidationProblem(result.Errors),
            AuthLoginStatus.OtpExpired => BadRequest(new { message = result.Message ?? "OTP expired." }),
            AuthLoginStatus.OtpInvalid => BadRequest(new { message = result.Message ?? "OTP invalid." }),
            AuthLoginStatus.OtpNotFound => BadRequest(new { message = result.Message ?? "OTP not found." }),
            AuthLoginStatus.Unauthorized => Unauthorized(),
            _ => Problem("Unexpected OTP login result.")
        };
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.ForgotPasswordAsync(request, cancellationToken);

        return result.Status switch
        {
            ForgotPasswordStatus.Completed => NoContent(),
            ForgotPasswordStatus.SendFailed => StatusCode(StatusCodes.Status500InternalServerError, new { message = result.Message ?? "Failed to send reset email." }),
            _ => Problem("Unexpected forgot password result.")
        };
    }

    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout()
    {
        if (TryGetSessionId(out var sessionId))
        {
            _authService.EndSession(sessionId);
        }

        return NoContent();
    }

    [HttpPost("set-password")]
    [Authorize]
    public async Task<ActionResult<LoginResponse>> SetPassword([FromBody] SetPasswordRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var hasExistingSession = TryGetSessionId(out var sessionId);

        var result = await _authService.SetPasswordAsync(userId, hasExistingSession ? sessionId : null, request, cancellationToken);

        return result.Status switch
        {
            SetPasswordStatus.Success => Ok(result.Response),
            SetPasswordStatus.ValidationFailed => BuildValidationProblem(result.Errors),
            SetPasswordStatus.PasswordAlreadySet => BadRequest(new { message = result.Message ?? "Password is already set." }),
            SetPasswordStatus.NotFound => Unauthorized(),
            _ => Problem("Unexpected set password result.")
        };
    }

    private ActionResult BuildValidationProblem(IDictionary<string, string[]>? errors)
    {
        if (errors is null || errors.Count == 0)
        {
            return ValidationProblem();
        }

        foreach (var error in errors)
        {
            foreach (var message in error.Value)
            {
                ModelState.AddModelError(error.Key, message);
            }
        }

        return ValidationProblem(ModelState);
    }

    private bool TryGetSessionId(out Guid sessionId)
    {
        var claim = User.FindFirstValue(JwtRegisteredClaimNames.Jti) ?? User.FindFirstValue("jti");
        return Guid.TryParse(claim, out sessionId);
    }

    private bool TryGetUserId(out int userId)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out userId);
    }
}

