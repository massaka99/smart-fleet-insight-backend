using System.Linq;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartFleet.Data;
using SmartFleet.Dtos;
using SmartFleet.Models;
using SmartFleet.Services;

namespace IntegrationTest;

public class UserServiceIntegrationTests
{
    [Fact]
    public async Task GetAllAsync_ReturnsUsersOrderedById()
    {
        await using var context = CreateContext();
        var service = CreateService(context);
        context.Users.AddRange(CreateUser(2, "b@example.com"), CreateUser(1, "a@example.com"));
        await context.SaveChangesAsync();

        var result = await service.GetAllAsync(CancellationToken.None);

        result.Select(u => u.Id).Should().Equal(1, 2);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsUser_WhenItExists()
    {
        await using var context = CreateContext();
        var service = CreateService(context);
        var user = CreateUser(3, "driver@example.com");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var found = await service.GetByIdAsync(user.Id, CancellationToken.None);

        found.Should().NotBeNull();
        found!.Email.Should().Be(user.Email);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenUserIsMissing()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var found = await service.GetByIdAsync(999, CancellationToken.None);

        found.Should().BeNull();
    }

    [Fact]
    public async Task GetByEmailAsync_NormalizesLookup()
    {
        await using var context = CreateContext();
        var service = CreateService(context);
        var user = CreateUser(4, "pilot@example.com");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var found = await service.GetByEmailAsync("  PILOT@Example.com  ", CancellationToken.None);

        found.Should().NotBeNull();
        found!.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task MarkForPasswordResetAsync_ReturnsNull_WhenEmailNotFound()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var result = await service.MarkForPasswordResetAsync("missing@example.com", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task MarkForPasswordResetAsync_SetsFlagAndPersists()
    {
        await using var context = CreateContext();
        var service = CreateService(context);
        var user = CreateUser(5, "user@example.com");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var updated = await service.MarkForPasswordResetAsync(" USER@example.com ", CancellationToken.None);

        updated.Should().NotBeNull();
        updated!.RequiresPasswordReset.Should().BeTrue();
        var persisted = await context.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        persisted.RequiresPasswordReset.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateRoleAsync_ReturnsNull_WhenUserDoesNotExist()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var result = await service.UpdateRoleAsync(123, UserRole.Admin, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateRoleAsync_DoesNotChange_WhenRoleIsUnchanged()
    {
        await using var context = CreateContext();
        var service = CreateService(context);
        var user = CreateUser(6, "static@example.com");
        user.Role = UserRole.Driver;
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var result = await service.UpdateRoleAsync(user.Id, UserRole.Driver, CancellationToken.None);

        result.Should().NotBeNull();
        (await context.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id)).Role.Should().Be(UserRole.Driver);
    }

    [Fact]
    public async Task UpdateRoleAsync_ChangesRole_WhenDifferent()
    {
        await using var context = CreateContext();
        var service = CreateService(context);
        var user = CreateUser(7, "change@example.com");
        user.Role = UserRole.Driver;
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var result = await service.UpdateRoleAsync(user.Id, UserRole.Coordinator, CancellationToken.None);

        result.Should().NotBeNull();
        (await context.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id)).Role.Should().Be(UserRole.Coordinator);
    }

    [Fact]
    public async Task UpdateProfileAsync_PersistsNormalizedValues_WhenEmailIsUnique()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var existing = CreateUser(8, "driver@example.com");
        context.Users.Add(existing);
        await context.SaveChangesAsync();

        var dto = new UserProfileUpdateDto
        {
            FirstName = "  Maria  ",
            LastName = "   Jensen ",
            Email = "New@Example.Com ",
            Age = 34
        };

        var result = await service.UpdateProfileAsync(existing.Id, dto, CancellationToken.None);

        result.Status.Should().Be(UserProfileUpdateStatus.Success);
        result.User.Should().NotBeNull();
        result.User!.FirstName.Should().Be("Maria");
        result.User.LastName.Should().Be("Jensen");
        result.User.Email.Should().Be("new@example.com");
        result.User.Age.Should().Be(34);

        var persisted = await context.Users.AsNoTracking().SingleAsync(u => u.Id == existing.Id);
        persisted.FirstName.Should().Be("Maria");
        persisted.LastName.Should().Be("Jensen");
        persisted.Email.Should().Be("new@example.com");
        persisted.Age.Should().Be(34);
    }

    [Fact]
    public async Task UpdateProfileAsync_ReturnsEmailInUse_WhenAnotherUserHasTheEmail()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var driver = CreateUser(9, "driver@example.com");
        var admin = CreateUser(10, "admin@example.com");
        context.Users.AddRange(driver, admin);
        await context.SaveChangesAsync();

        var dto = new UserProfileUpdateDto
        {
            FirstName = "Driver",
            LastName = "One",
            Email = "admin@example.com",
            Age = 40
        };

        var result = await service.UpdateProfileAsync(driver.Id, dto, CancellationToken.None);

        result.Status.Should().Be(UserProfileUpdateStatus.EmailInUse);
        var persisted = await context.Users.AsNoTracking().SingleAsync(u => u.Id == driver.Id);
        persisted.Email.Should().Be("driver@example.com");
    }

    [Fact]
    public async Task UpdateProfileAsync_ReturnsNotFound_WhenUserMissing()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var dto = new UserProfileUpdateDto
        {
            FirstName = "N/A",
            LastName = "N/A",
            Email = "none@example.com",
            Age = 50
        };

        var result = await service.UpdateProfileAsync(404, dto, CancellationToken.None);

        result.Status.Should().Be(UserProfileUpdateStatus.NotFound);
    }

    [Fact]
    public async Task UpdateProfileImageAsync_ReturnsNull_WhenUserMissing()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var result = await service.UpdateProfileImageAsync(100, "/images/avatar.png", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateProfileImageAsync_PersistsPath()
    {
        await using var context = CreateContext();
        var service = CreateService(context);
        var user = CreateUser(11, "avatar@example.com");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var updated = await service.UpdateProfileImageAsync(user.Id, "/images/avatar.png", CancellationToken.None);

        updated.Should().NotBeNull();
        (await context.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id)).ProfileImageUrl.Should().Be("/images/avatar.png");
    }

    [Fact]
    public async Task UpdatePasswordAsync_ReturnsInvalid_WhenCurrentPasswordDoesNotMatch()
    {
        await using var context = CreateContext();
        var hasher = new PasswordHasher<User>();
        var service = CreateService(context, hasher);

        var user = CreateUser(12, "pilot@example.com");
        user.PasswordHash = hasher.HashPassword(user, "CorrectHorseBattery1!");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var dto = new UserPasswordUpdateDto
        {
            CurrentPassword = "wrong-password",
            NewPassword = "MoreSecurePass2!"
        };

        var result = await service.UpdatePasswordAsync(user.Id, dto, CancellationToken.None);

        result.Status.Should().Be(UserPasswordUpdateStatus.InvalidCurrentPassword);
        var persisted = await context.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        persisted.PasswordHash.Should().Be(user.PasswordHash);
    }

    [Fact]
    public async Task UpdatePasswordAsync_ReHashesPassword_WhenCurrentPasswordMatches()
    {
        await using var context = CreateContext();
        var hasher = new PasswordHasher<User>();
        var service = CreateService(context, hasher);

        var user = CreateUser(13, "captain@example.com");
        var originalHash = hasher.HashPassword(user, "OriginalPass123!");
        user.PasswordHash = originalHash;
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var dto = new UserPasswordUpdateDto
        {
            CurrentPassword = "OriginalPass123!",
            NewPassword = "FreshPass456!"
        };

        var result = await service.UpdatePasswordAsync(user.Id, dto, CancellationToken.None);

        result.Status.Should().Be(UserPasswordUpdateStatus.Success);

        var persisted = await context.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        persisted.PasswordHash.Should().NotBe(originalHash);
        hasher.VerifyHashedPassword(persisted, persisted.PasswordHash, dto.NewPassword)
            .Should().Be(PasswordVerificationResult.Success);
    }

    [Fact]
    public async Task UpdatePasswordAsync_ReturnsNotFound_WhenUserMissing()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var dto = new UserPasswordUpdateDto
        {
            CurrentPassword = "Anything1!",
            NewPassword = "Else2!"
        };

        var result = await service.UpdatePasswordAsync(555, dto, CancellationToken.None);

        result.Status.Should().Be(UserPasswordUpdateStatus.NotFound);
    }

    [Fact]
    public async Task ResetPasswordAsync_DoesNothing_WhenUserMissing()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        await service.ResetPasswordAsync(999, "NewPass!1", CancellationToken.None);

        (await context.Users.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ResetPasswordAsync_UpdatesHash_WhenUserExists()
    {
        await using var context = CreateContext();
        var hasher = new PasswordHasher<User>();
        var service = CreateService(context, hasher);

        var user = CreateUser(14, "reset@example.com");
        var originalHash = hasher.HashPassword(user, "Original!");
        user.PasswordHash = originalHash;
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await service.ResetPasswordAsync(user.Id, "BrandNew1!", CancellationToken.None);

        var persisted = await context.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        persisted.PasswordHash.Should().NotBe(originalHash);
        hasher.VerifyHashedPassword(persisted, persisted.PasswordHash, "BrandNew1!")
            .Should().Be(PasswordVerificationResult.Success);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenUserMissing()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var result = await service.DeleteAsync(321, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_RemovesUser()
    {
        await using var context = CreateContext();
        var service = CreateService(context);
        var user = CreateUser(15, "delete@example.com");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var result = await service.DeleteAsync(user.Id, CancellationToken.None);

        result.Should().BeTrue();
        (await context.Users.CountAsync()).Should().Be(0);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static UserService CreateService(ApplicationDbContext context, IPasswordHasher<User>? passwordHasher = null)
    {
        return new UserService(context, passwordHasher ?? new PasswordHasher<User>());
    }

    private static User CreateUser(int id, string email)
    {
        return new User
        {
            Id = id,
            FirstName = $"First{id}",
            LastName = $"Last{id}",
            Email = email,
            Age = 30 + id,
            Role = UserRole.Driver,
            PasswordHash = Guid.NewGuid().ToString("N")
        };
    }
}
