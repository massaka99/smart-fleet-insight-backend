using Microsoft.AspNetCore.Identity;
using SmartFleet.Data.Repositories;
using SmartFleet.Dtos;
using SmartFleet.Models;

namespace SmartFleet.Services;

public class UserService(IUserRepository userRepository, IChatRepository chatRepository, IPasswordHasher<User> passwordHasher) : IUserService
{
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IChatRepository _chatRepository = chatRepository;
    private readonly IPasswordHasher<User> _passwordHasher = passwordHasher;

    public async Task<IReadOnlyCollection<User>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await _userRepository.GetAllAsync(includeVehicle: true, asTracking: false, cancellationToken);
    }

    public async Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _userRepository.GetByIdAsync(id, includeVehicle: true, asTracking: false, cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(email);
        return await _userRepository.GetByEmailAsync(normalizedEmail, includeVehicle: true, asTracking: false, cancellationToken);
    }

    public async Task<User?> MarkForPasswordResetAsync(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(email);
        var user = await _userRepository.GetByEmailAsync(normalizedEmail, includeVehicle: false, asTracking: true, cancellationToken);

        if (user is null)
        {
            return null;
        }

        user.RequiresPasswordReset = true;
        await _userRepository.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task<User?> UpdateRoleAsync(int id, UserRole role, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(id, includeVehicle: true, asTracking: true, cancellationToken);

        if (user is null)
        {
            return null;
        }

        if (user.Role != role)
        {
            user.Role = role;
            await _userRepository.SaveChangesAsync(cancellationToken);
        }

        return user;
    }

    public async Task<UserProfileUpdateResult> UpdateProfileAsync(int userId, UserProfileUpdateDto dto, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(userId, includeVehicle: true, asTracking: true, cancellationToken);

        if (user is null)
        {
            return UserProfileUpdateResult.NotFound();
        }

        var firstName = dto.FirstName.Trim();
        var lastName = dto.LastName.Trim();
        var normalizedEmail = NormalizeEmail(dto.Email);

        var otherUserWithEmail = await _userRepository.GetByEmailAsync(normalizedEmail, includeVehicle: false, asTracking: false, cancellationToken);
        var emailInUse = otherUserWithEmail is not null && otherUserWithEmail.Id != userId;

        if (emailInUse)
        {
            return UserProfileUpdateResult.EmailInUse();
        }

        user.FirstName = firstName;
        user.LastName = lastName;
        user.Email = normalizedEmail;
        user.Age = dto.Age;

        await _userRepository.SaveChangesAsync(cancellationToken);

        return UserProfileUpdateResult.Success(user);
    }

    public async Task<User?> UpdateProfileImageAsync(int userId, string? profileImagePath, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(userId, includeVehicle: true, asTracking: true, cancellationToken);

        if (user is null)
        {
            return null;
        }

        user.ProfileImageUrl = profileImagePath;
        await _userRepository.SaveChangesAsync(cancellationToken);

        return user;
    }

    public async Task<UserPasswordUpdateResult> UpdatePasswordAsync(int userId, UserPasswordUpdateDto dto, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(userId, includeVehicle: true, asTracking: true, cancellationToken);

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
        await _userRepository.SaveChangesAsync(cancellationToken);

        return UserPasswordUpdateResult.Success();
    }

    public async Task ResetPasswordAsync(int userId, string newPassword, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(userId, includeVehicle: false, asTracking: true, cancellationToken);

        if (user is null)
        {
            return;
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
        await _userRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(id, includeVehicle: true, asTracking: true, cancellationToken);

        if (user is null)
        {
            return false;
        }

        if (user.Vehicle is not null)
        {
            user.Vehicle.Driver = null;
            user.Vehicle.UpdatedAt = DateTime.UtcNow;
            user.Vehicle = null;
            user.VehicleId = null;
        }

        await _chatRepository.RemoveThreadsByParticipantAsync(id, cancellationToken);
        _userRepository.Remove(user);
        await _userRepository.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}


