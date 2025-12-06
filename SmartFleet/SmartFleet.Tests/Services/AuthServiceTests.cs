using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmartFleet.Authorization;
using SmartFleet.Data.Repositories;
using SmartFleet.Models;
using SmartFleet.Models.Auth;
using SmartFleet.Services;

namespace SmartFleet.Tests.Services;

public class AuthServiceTests
{
    [Fact]
    public async Task LoginAsync_ReturnsSuccess_WhenPasswordMatches()
    {
        var user = new User
        {
            Id = 1,
            FirstName = "Test",
            LastName = "User",
            Email = "user@example.com",
            PasswordHash = "hashed",
            Role = UserRole.Driver,
            RequiresPasswordReset = false
        };

        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByEmailAsync("user@example.com", true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var passwordHasher = new Mock<IPasswordHasher<User>>();
        passwordHasher.Setup(h => h.VerifyHashedPassword(user, "hashed", "Secret123"))
            .Returns(PasswordVerificationResult.Success);

        var tokenService = new Mock<ITokenService>();
        tokenService.Setup(t => t.GenerateToken(user, It.IsAny<Guid>()))
            .Returns(new GeneratedToken("token-abc", DateTime.UtcNow.AddMinutes(15), Guid.NewGuid()));

        var sessionTracker = new Mock<IUserSessionTracker>();
        var auth = new AuthService(repo.Object, passwordHasher.Object, tokenService.Object,
            Mock.Of<IOtpService>(), sessionTracker.Object, NullLogger<AuthService>.Instance);

        var result = await auth.LoginAsync(new LoginRequest("user@example.com", "Secret123"), CancellationToken.None);

        result.Status.Should().Be(AuthLoginStatus.Success);
        result.Response!.Token.Should().Be("token-abc");
        sessionTracker.Verify(s => s.TryBeginSession(user.Id, It.IsAny<Guid>(), It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_ReturnsUnauthorized_WhenPasswordWrong()
    {
        var user = new User { Id = 2, Email = "user@example.com", PasswordHash = "hashed", RequiresPasswordReset = false };

        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByEmailAsync("user@example.com", true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var passwordHasher = new Mock<IPasswordHasher<User>>();
        passwordHasher.Setup(h => h.VerifyHashedPassword(user, "hashed", "bad"))
            .Returns(PasswordVerificationResult.Failed);

        var auth = new AuthService(repo.Object, passwordHasher.Object, Mock.Of<ITokenService>(),
            Mock.Of<IOtpService>(), Mock.Of<IUserSessionTracker>(), NullLogger<AuthService>.Instance);

        var result = await auth.LoginAsync(new LoginRequest("user@example.com", "bad"), CancellationToken.None);

        result.Status.Should().Be(AuthLoginStatus.Unauthorized);
    }

    [Fact]
    public async Task LoginWithOtp_ReturnsInvalid_WhenOtpFails()
    {
        var user = new User { Id = 3, Email = "user@example.com", RequiresPasswordReset = false };

        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByEmailAsync("user@example.com", true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var otp = new Mock<IOtpService>();
        otp.Setup(o => o.VerifyOtp(user, "0000")).Returns(OtpVerificationStatus.Invalid);

        var auth = new AuthService(repo.Object, Mock.Of<IPasswordHasher<User>>(),
            Mock.Of<ITokenService>(), otp.Object, Mock.Of<IUserSessionTracker>(), NullLogger<AuthService>.Instance);

        var result = await auth.LoginWithOtpAsync(new OtpLoginRequest("user@example.com", "0000"), CancellationToken.None);

        result.Status.Should().Be(AuthLoginStatus.OtpInvalid);
    }

    [Fact]
    public async Task RegisterAsync_ReturnsEmailInUse_WhenDuplicate()
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.EmailExistsAsync("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var auth = new AuthService(repo.Object, Mock.Of<IPasswordHasher<User>>(),
            Mock.Of<ITokenService>(), Mock.Of<IOtpService>(), Mock.Of<IUserSessionTracker>(), NullLogger<AuthService>.Instance);

        var result = await auth.RegisterAsync(
            new RegisterRequest("A", "B", "user@example.com", 30, UserRole.Driver),
            CancellationToken.None);

        result.Status.Should().Be(AuthRegisterStatus.EmailInUse);
    }

    [Fact]
    public async Task LoginWithOtp_ReturnsSuccess_WhenOtpValid()
    {
        var user = new User { Id = 4, Email = "otp@example.com", RequiresPasswordReset = true };

        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByEmailAsync("otp@example.com", true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var otp = new Mock<IOtpService>();
        otp.Setup(o => o.VerifyOtp(user, "1111")).Returns(OtpVerificationStatus.Valid);

        var token = new GeneratedToken("otp-token", DateTime.UtcNow.AddMinutes(10), Guid.NewGuid());
        var tokenService = new Mock<ITokenService>();
        tokenService.Setup(t => t.GenerateToken(user, It.IsAny<Guid>())).Returns(token);

        var sessions = new Mock<IUserSessionTracker>();

        var auth = new AuthService(repo.Object, Mock.Of<IPasswordHasher<User>>(),
            tokenService.Object, otp.Object, sessions.Object, NullLogger<AuthService>.Instance);

        var result = await auth.LoginWithOtpAsync(new OtpLoginRequest("otp@example.com", "1111"), CancellationToken.None);

        result.Status.Should().Be(AuthLoginStatus.Success);
        result.Response!.Token.Should().Be("otp-token");
        sessions.Verify(s => s.TryBeginSession(user.Id, token.SessionId, token.ExpiresAt), Times.Once);
    }

    [Fact]
    public async Task ForgotPassword_SetsResetFlagAndSendsOtp()
    {
        var user = new User { Id = 5, Email = "forgot@example.com", RequiresPasswordReset = false };
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByEmailAsync("forgot@example.com", true, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var otp = new Mock<IOtpService>();
        var auth = new AuthService(repo.Object, Mock.Of<IPasswordHasher<User>>(),
            Mock.Of<ITokenService>(), otp.Object, Mock.Of<IUserSessionTracker>(), NullLogger<AuthService>.Instance);

        var result = await auth.ForgotPasswordAsync(new ForgotPasswordRequest("forgot@example.com"), CancellationToken.None);

        result.Status.Should().Be(ForgotPasswordStatus.Completed);
        user.RequiresPasswordReset.Should().BeTrue();
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        otp.Verify(o => o.SendOtpAsync(user, It.IsAny<CancellationToken>()), Times.Once);
    }
}
