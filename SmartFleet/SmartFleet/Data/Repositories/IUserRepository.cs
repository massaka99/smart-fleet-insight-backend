using SmartFleet.Models;

namespace SmartFleet.Data.Repositories;

public interface IUserRepository
{
    Task<IReadOnlyCollection<User>> GetAllAsync(bool includeVehicle, bool asTracking, CancellationToken cancellationToken);
    Task<User?> GetByIdAsync(int id, bool includeVehicle, bool asTracking, CancellationToken cancellationToken);
    Task<User?> GetByEmailAsync(string email, bool includeVehicle, bool asTracking, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<User>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken);
    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task<User?> GetFirstAvailableDriverAsync(CancellationToken cancellationToken);
    void Add(User user);
    void Update(User user);
    void Remove(User user);
    Task SaveChangesAsync(CancellationToken cancellationToken);
    void Attach(User user);
}
