using SmartFleet.Dtos;
using SmartFleet.Models;

namespace SmartFleet.Services;

public interface IUserService
{
    Task<IReadOnlyCollection<User>> GetAllAsync(CancellationToken cancellationToken);
    Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken);
    Task<User?> MarkForPasswordResetAsync(string email, CancellationToken cancellationToken);
    Task<User?> UpdateRoleAsync(int id, UserRole role, CancellationToken cancellationToken);
    Task<UserProfileUpdateResult> UpdateProfileAsync(int userId, UserProfileUpdateDto dto, CancellationToken cancellationToken);
    Task<User?> UpdateProfileImageAsync(int userId, string? profileImagePath, CancellationToken cancellationToken);
    Task<UserPasswordUpdateResult> UpdatePasswordAsync(int userId, UserPasswordUpdateDto dto, CancellationToken cancellationToken);
    Task ResetPasswordAsync(int userId, string newPassword, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);
}

public enum UserProfileUpdateStatus
{
    Success,
    NotFound,
    EmailInUse
}

public record UserProfileUpdateResult(UserProfileUpdateStatus Status, User? User = null)
{
    public static UserProfileUpdateResult NotFound() => new(UserProfileUpdateStatus.NotFound);
    public static UserProfileUpdateResult EmailInUse() => new(UserProfileUpdateStatus.EmailInUse);
    public static UserProfileUpdateResult Success(User user) => new(UserProfileUpdateStatus.Success, user);
}

public enum UserPasswordUpdateStatus
{
    Success,
    NotFound,
    InvalidCurrentPassword
}

public record UserPasswordUpdateResult(UserPasswordUpdateStatus Status)
{
    public static UserPasswordUpdateResult NotFound() => new(UserPasswordUpdateStatus.NotFound);
    public static UserPasswordUpdateResult InvalidCurrentPassword() => new(UserPasswordUpdateStatus.InvalidCurrentPassword);
    public static UserPasswordUpdateResult Success() => new(UserPasswordUpdateStatus.Success);
}

