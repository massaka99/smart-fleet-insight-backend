using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmartFleet.Data;
using SmartFleet.Models;
using SmartFleet.Options;
using SmartFleet.Services;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace SmartFleet.Tests.Services;

public class OtpServiceTests
{
    [Fact]
    public void VerifyOtp_ReturnsNotFound_WhenNoOtpStored()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var context = CreateContext();
        var service = CreateService(context, cache);
        var user = CreateUser(1);
        context.Users.Add(user);
        context.SaveChanges();

        var status = service.VerifyOtp(user, "0000");

        status.Should().Be(OtpVerificationStatus.NotFound);
    }

    [Fact]
    public void VerifyOtp_ReturnsExpiredAndClearsPersistedOtp()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var context = CreateContext();
        var service = CreateService(context, cache);
        var user = CreateUser(2);
        user.OtpHash = HashOtp("123456");
        user.OtpExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        context.Users.Add(user);
        context.SaveChanges();

        var status = service.VerifyOtp(user, "123456");

        status.Should().Be(OtpVerificationStatus.Expired);
        context.Entry(user).State = EntityState.Detached;
        var persisted = context.Users.Single(u => u.Id == user.Id);
        persisted.OtpHash.Should().BeNull();
        persisted.OtpExpiresAt.Should().BeNull();
    }

    [Fact]
    public void VerifyOtp_ReturnsInvalid_WhenInputDoesNotMatch()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var context = CreateContext();
        var service = CreateService(context, cache);
        var user = CreateUser(3);
        user.OtpHash = HashOtp("222222");
        user.OtpExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5);
        context.Users.Add(user);
        context.SaveChanges();

        var status = service.VerifyOtp(user, "999999");

        status.Should().Be(OtpVerificationStatus.Invalid);
        context.Entry(user).State = EntityState.Detached;
        var persisted = context.Users.Single(u => u.Id == user.Id);
        persisted.OtpHash.Should().NotBeNull();
        persisted.OtpExpiresAt.Should().NotBeNull();
    }

    [Fact]
    public void VerifyOtp_ReturnsValidAndClearsCacheAndPersistence()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var context = CreateContext();
        var service = CreateService(context, cache);
        var otp = "654321";
        var user = CreateUser(4);
        user.OtpHash = HashOtp(otp);
        user.OtpExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5);
        context.Users.Add(user);
        context.SaveChanges();

        var status = service.VerifyOtp(user, otp);

        status.Should().Be(OtpVerificationStatus.Valid);
        context.Entry(user).State = EntityState.Detached;
        var persisted = context.Users.Single(u => u.Id == user.Id);
        persisted.OtpHash.Should().BeNull();
        persisted.OtpExpiresAt.Should().BeNull();

        service.VerifyOtp(persisted, otp).Should().Be(OtpVerificationStatus.NotFound);
    }

    [Fact]
    public async Task SendOtpAsync_ThrowsWhenTemplateIdMissing()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var context = CreateContext();
        var user = CreateUser(5);
        context.Users.Add(user);
        context.SaveChanges();

        var service = CreateService(
            context,
            cache,
            sendGridOptions: new SendGridOptions { OtpTemplateId = string.Empty });

        var action = () => service.SendOtpAsync(user, CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static OtpService CreateService(
        ApplicationDbContext context,
        IMemoryCache cache,
        OtpOptions? otpOptions = null,
        SendGridOptions? sendGridOptions = null,
        IEmailSender? emailSender = null)
    {
        return new OtpService(
            cache,
            emailSender ?? Mock.Of<IEmailSender>(),
            NullLogger<OtpService>.Instance,
            context,
            OptionsFactory.Create(otpOptions ?? new OtpOptions()),
            OptionsFactory.Create(sendGridOptions ?? new SendGridOptions { OtpTemplateId = "otp-template" }));
    }

    private static User CreateUser(int id) => new()
    {
        Id = id,
        FirstName = "Test",
        LastName = "User",
        Email = $"user{id}@example.com",
        Role = UserRole.Driver
    };

    private static string HashOtp(string otp)
    {
        var bytes = Encoding.UTF8.GetBytes(otp);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }
}
