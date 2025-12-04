using SmartFleet.Models;

namespace SmartFleet.Data.Repositories;

public interface IVehicleRepository
{
    Task<IReadOnlyCollection<Vehicle>> GetAllWithDriverAsync(CancellationToken cancellationToken);
    Task<Vehicle?> GetByIdAsync(int id, bool includeDriver, bool asTracking, CancellationToken cancellationToken);
    Task<Vehicle?> GetByExternalIdAsync(string externalId, bool asTracking, CancellationToken cancellationToken);
    Task<Vehicle?> GetByLicensePlateAsync(string licensePlate, bool asTracking, CancellationToken cancellationToken);
    Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken);
    void Remove(Vehicle vehicle);
    Task LoadDriverAsync(Vehicle vehicle, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
