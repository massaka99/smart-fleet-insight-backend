using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using SmartFleet.Models;
using SmartFleet.Options;
using SmartFleet.Services;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace SmartFleet.Tests.Services;

public class JwtTokenServiceTests
{
    [Fact]
    public void GenerateToken_EmbedsAllRelevantClaims()
    {
        var options = OptionsFactory.Create(new JwtOptions
        {
            Key = new string('k', 64),
            Issuer = "smartfleet-api",
            Audience = "smartfleet-clients",
            ExpiresMinutes = 90
        });

        var service = new JwtTokenService(options);

        var user = new User
        {
            Id = 42,
            FirstName = "Sara",
            LastName = "Connor",
            Email = "sara.connor@example.com",
            Role = UserRole.Driver,
            RequiresPasswordReset = true
        };

        var sessionId = Guid.NewGuid();
        var before = DateTime.UtcNow;

        var generated = service.GenerateToken(user, sessionId);

        generated.SessionId.Should().Be(sessionId);
        generated.Token.Should().NotBeNullOrWhiteSpace();
        generated.ExpiresAt.Should().BeCloseTo(before.AddMinutes(options.Value.ExpiresMinutes), TimeSpan.FromSeconds(5));

        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(generated.Token);
        parsed.Issuer.Should().Be(options.Value.Issuer);
        parsed.Audiences.Should().ContainSingle(a => a == options.Value.Audience);

        var claims = parsed.Claims.ToList();
        claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == user.Id.ToString());
        claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == user.Id.ToString());
        claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.GivenName && c.Value == user.FirstName);
        claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.FamilyName && c.Value == user.LastName);
        claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == user.Email);
        claims.Should().Contain(c => c.Type == ClaimTypes.Email && c.Value == user.Email);
        claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == user.Role.ToString());
        claims.Should().Contain(c => c.Type == "requiresPasswordReset" && c.Value == "true");
        claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Jti && c.Value == sessionId.ToString());
    }

    [Fact]
    public void GenerateToken_RespectsPasswordResetFlag()
    {
        var service = new JwtTokenService(OptionsFactory.Create(new JwtOptions
        {
            Key = new string('x', 64),
            Issuer = "issuer",
            Audience = "audience",
            ExpiresMinutes = 15
        }));

        var user = new User
        {
            Id = 7,
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = "ada@example.com",
            Role = UserRole.Admin,
            RequiresPasswordReset = false
        };

        var generated = service.GenerateToken(user, Guid.NewGuid());
        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(generated.Token);

        parsed.Claims.Should().Contain(c => c.Type == "requiresPasswordReset" && c.Value == "false");
    }
}
