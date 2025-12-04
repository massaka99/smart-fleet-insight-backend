using Microsoft.EntityFrameworkCore;
using SmartFleet.Models;

namespace SmartFleet.Data.Repositories;

public class VehicleRepository(ApplicationDbContext context) : IVehicleRepository
{
    private readonly ApplicationDbContext _context = context;

    public async Task<IReadOnlyCollection<Vehicle>> GetAllWithDriverAsync(CancellationToken cancellationToken)
    {
        return await _context.Vehicles
            .Include(v => v.Driver)
            .AsNoTracking()
            .OrderByDescending(v => v.LastTelemetryAtUtc ?? v.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Vehicle?> GetByIdAsync(int id, bool includeDriver, bool asTracking, CancellationToken cancellationToken)
    {
        var query = _context.Vehicles.AsQueryable();
        if (includeDriver)
        {
            query = query.Include(v => v.Driver);
        }

        if (!asTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
    }

    public async Task<Vehicle?> GetByExternalIdAsync(string externalId, bool asTracking, CancellationToken cancellationToken)
    {
        var query = _context.Vehicles.AsQueryable();
        if (!asTracking)
        {
            query = query.AsNoTracking();
        }
        return await query.FirstOrDefaultAsync(v => v.ExternalId == externalId, cancellationToken);
    }

    public async Task<Vehicle?> GetByLicensePlateAsync(string licensePlate, bool asTracking, CancellationToken cancellationToken)
    {
        var query = _context.Vehicles.AsQueryable();
        if (!asTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(v => v.LicensePlate == licensePlate, cancellationToken);
    }

    public Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken) => _context.Vehicles.AddAsync(vehicle, cancellationToken).AsTask();

    public void Remove(Vehicle vehicle) => _context.Vehicles.Remove(vehicle);

    public Task LoadDriverAsync(Vehicle vehicle, CancellationToken cancellationToken)
    {
        var entry = _context.Entry(vehicle).Reference(v => v.Driver);
        return entry.IsLoaded ? Task.CompletedTask : entry.LoadAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => _context.SaveChangesAsync(cancellationToken);
}
