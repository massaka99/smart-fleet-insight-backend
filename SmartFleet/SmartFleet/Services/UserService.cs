using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartFleet.Data;
using SmartFleet.Dtos;
using SmartFleet.Models;

namespace SmartFleet.Services;

public class UserService(ApplicationDbContext context, IPasswordHasher<User> passwordHasher) : IUserService
{
    private readonly ApplicationDbContext _context = context;
    private readonly IPasswordHasher<User> _passwordHasher = passwordHasher;

    public async Task<IReadOnlyCollection<User>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await _context.Users
            .AsNoTracking()
            .OrderBy(u => u.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(email);
        return await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);
    }

    public async Task<User?> MarkForPasswordResetAsync(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(email);
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (user is null)
        {
            return null;
        }

        user.RequiresPasswordReset = true;
        await _context.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task<User?> UpdateRoleAsync(int id, UserRole role, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user is null)
        {
            return null;
        }

        if (user.Role != role)
        {
            user.Role = role;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return user;
    }

    public async Task<UserProfileUpdateResult> UpdateProfileAsync(int userId, UserProfileUpdateDto dto, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            return UserProfileUpdateResult.NotFound();
        }

        var firstName = dto.FirstName.Trim();
        var lastName = dto.LastName.Trim();
        var normalizedEmail = NormalizeEmail(dto.Email);

        var emailInUse = await _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.Email == normalizedEmail && u.Id != userId, cancellationToken);

        if (emailInUse)
        {
            return UserProfileUpdateResult.EmailInUse();
        }

        user.FirstName = firstName;
        user.LastName = lastName;
        user.Email = normalizedEmail;
        user.Age = dto.Age;

        await _context.SaveChangesAsync(cancellationToken);

        return UserProfileUpdateResult.Success(user);
    }

    public async Task<User?> UpdateProfileImageAsync(int userId, string? profileImagePath, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            return null;
        }

        user.ProfileImageUrl = profileImagePath;
        await _context.SaveChangesAsync(cancellationToken);

        return user;
    }

    public async Task<UserPasswordUpdateResult> UpdatePasswordAsync(int userId, UserPasswordUpdateDto dto, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            return UserPasswordUpdateResult.NotFound();
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.CurrentPassword);

        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return UserPasswordUpdateResult.InvalidCurrentPassword();
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, dto.NewPassword);
        await _context.SaveChangesAsync(cancellationToken);

        return UserPasswordUpdateResult.Success();
    }

    public async Task ResetPasswordAsync(int userId, string newPassword, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            return;
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user is null)
        {
            return false;
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}


