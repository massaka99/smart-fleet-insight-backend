using Microsoft.EntityFrameworkCore;
using SmartFleet.Data;
using SmartFleet.Dtos;
using SmartFleet.Models;

namespace SmartFleet.Services;

public class VehicleService(ApplicationDbContext context) : IVehicleService
{
    private readonly ApplicationDbContext _context = context;

    public async Task<IReadOnlyCollection<Vehicle>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await _context.Vehicles
            .Include(v => v.Driver)
            .AsNoTracking()
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Vehicle?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _context.Vehicles
            .Include(v => v.Driver)
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
    }

    public async Task<Vehicle> CreateAsync(VehicleCreateDto dto, CancellationToken cancellationToken)
    {
        var vehicle = new Vehicle
        {
            LicensePlate = dto.LicensePlate.Trim(),
            VehicleType = dto.VehicleType.Trim(),
            FuelType = dto.FuelType.Trim(),
            FuelTankCapacity = dto.FuelTankCapacity,
            CurrentFuelLevel = dto.CurrentFuelLevel,
            BodyType = dto.BodyType?.Trim() ?? string.Empty,
            KilometersDriven = dto.KilometersDriven,
            CO2Emission = dto.CO2Emission,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Vehicles.Add(vehicle);
        await _context.SaveChangesAsync(cancellationToken);

        return vehicle;
    }

    public async Task<Vehicle?> UpdateAsync(int id, VehicleUpdateDto dto, CancellationToken cancellationToken)
    {
        var vehicle = await _context.Vehicles
            .Include(v => v.Driver)
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

        if (vehicle is null)
        {
            return null;
        }

        vehicle.LicensePlate = dto.LicensePlate.Trim();
        vehicle.VehicleType = dto.VehicleType.Trim();
        vehicle.FuelType = dto.FuelType.Trim();
        vehicle.FuelTankCapacity = dto.FuelTankCapacity;
        vehicle.CurrentFuelLevel = dto.CurrentFuelLevel;
        vehicle.BodyType = dto.BodyType?.Trim() ?? string.Empty;
        vehicle.KilometersDriven = dto.KilometersDriven;
        vehicle.CO2Emission = dto.CO2Emission;
        vehicle.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return vehicle;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

        if (vehicle is null)
        {
            return false;
        }

        _context.Vehicles.Remove(vehicle);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<VehicleDriverAssignmentResult> AssignDriverAsync(int vehicleId, int userId, CancellationToken cancellationToken)
    {
        var vehicle = await _context.Vehicles
            .Include(v => v.Driver)
            .FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken);

        if (vehicle is null)
        {
            return VehicleDriverAssignmentResult.VehicleNotFound();
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            return VehicleDriverAssignmentResult.UserNotFound();
        }

        if (user.Role != UserRole.Driver)
        {
            return VehicleDriverAssignmentResult.UserNotDriver();
        }

        var isDriverAssignedElsewhere = await _context.Vehicles
            .AsNoTracking()
            .AnyAsync(v => v.DriverId == userId && v.Id != vehicleId, cancellationToken);

        if (isDriverAssignedElsewhere)
        {
            return VehicleDriverAssignmentResult.DriverAlreadyAssigned();
        }

        vehicle.DriverId = userId;
        vehicle.Driver = user;
        vehicle.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(vehicle).Reference(v => v.Driver).LoadAsync(cancellationToken);

        return VehicleDriverAssignmentResult.Success(vehicle);
    }

    public async Task<VehicleDriverRemovalResult> RemoveDriverAsync(int vehicleId, CancellationToken cancellationToken)
    {
        var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken);

        if (vehicle is null)
        {
            return VehicleDriverRemovalResult.VehicleNotFound();
        }

        if (vehicle.DriverId is null)
        {
            return VehicleDriverRemovalResult.NoDriverAssigned();
        }

        vehicle.DriverId = null;
        vehicle.Driver = null;
        vehicle.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return VehicleDriverRemovalResult.Success();
    }
}
