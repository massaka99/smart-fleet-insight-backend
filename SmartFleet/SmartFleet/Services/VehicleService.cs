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
            ExternalId = string.IsNullOrWhiteSpace(dto.ExternalId) ? null : dto.ExternalId.Trim(),
            LicensePlate = dto.LicensePlate.Trim(),
            VehicleType = dto.VehicleType.Trim(),
            FuelType = dto.FuelType.Trim(),
            Brand = dto.Brand?.Trim() ?? string.Empty,
            FuelUnit = dto.FuelUnit?.Trim() ?? string.Empty,
            FuelTankCapacity = dto.FuelTankCapacity,
            BatteryCapacity = dto.BatteryCapacity,
            CurrentFuelLevel = dto.CurrentFuelLevel,
            FuelLevelPercent = dto.FuelLevelPercent,
            FuelConsumptionPer100Km = dto.FuelConsumptionPer100Km,
            BodyType = dto.BodyType?.Trim() ?? string.Empty,
            KilometersDriven = dto.KilometersDriven,
            CO2Emission = dto.CO2Emission,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            SpeedKmh = dto.SpeedKmh,
            HeadingDeg = dto.HeadingDeg,
            DistanceTravelledM = dto.DistanceTravelledM,
            DistanceRemainingM = dto.DistanceRemainingM,
            Progress = dto.Progress,
            EtaSeconds = dto.EtaSeconds,
            RouteId = dto.RouteId?.Trim() ?? string.Empty,
            RouteSummary = string.IsNullOrWhiteSpace(dto.RouteSummary) ? null : dto.RouteSummary.Trim(),
            RouteDistanceKm = dto.RouteDistanceKm,
            BaseSpeedKmh = dto.BaseSpeedKmh,
            Status = dto.Status?.Trim() ?? string.Empty,
            LastTelemetryAtUtc = dto.LastTelemetryAtUtc,
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

        if (dto.ExternalId is not null)
        {
            vehicle.ExternalId = string.IsNullOrWhiteSpace(dto.ExternalId)
                ? null
                : dto.ExternalId.Trim();
        }
        vehicle.LicensePlate = dto.LicensePlate.Trim();
        vehicle.VehicleType = dto.VehicleType.Trim();
        vehicle.FuelType = dto.FuelType.Trim();
        if (dto.Brand is not null)
        {
            vehicle.Brand = dto.Brand.Trim();
        }

        if (dto.FuelUnit is not null)
        {
            vehicle.FuelUnit = dto.FuelUnit.Trim();
        }

        vehicle.FuelTankCapacity = dto.FuelTankCapacity;
        vehicle.BatteryCapacity = dto.BatteryCapacity;
        vehicle.CurrentFuelLevel = dto.CurrentFuelLevel;
        vehicle.FuelLevelPercent = dto.FuelLevelPercent;
        vehicle.FuelConsumptionPer100Km = dto.FuelConsumptionPer100Km;
        vehicle.BodyType = dto.BodyType?.Trim() ?? string.Empty;
        vehicle.KilometersDriven = dto.KilometersDriven;
        vehicle.CO2Emission = dto.CO2Emission;
        vehicle.Latitude = dto.Latitude;
        vehicle.Longitude = dto.Longitude;
        vehicle.SpeedKmh = dto.SpeedKmh;
        vehicle.HeadingDeg = dto.HeadingDeg;
        vehicle.DistanceTravelledM = dto.DistanceTravelledM;
        vehicle.DistanceRemainingM = dto.DistanceRemainingM;
        vehicle.Progress = dto.Progress;
        vehicle.EtaSeconds = dto.EtaSeconds;
        if (dto.RouteId is not null)
        {
            vehicle.RouteId = dto.RouteId.Trim();
        }

        vehicle.RouteSummary = dto.RouteSummary switch
        {
            null => vehicle.RouteSummary,
            { Length: 0 } => null,
            _ => dto.RouteSummary.Trim()
        };

        vehicle.RouteDistanceKm = dto.RouteDistanceKm;
        vehicle.BaseSpeedKmh = dto.BaseSpeedKmh;
        if (dto.Status is not null)
        {
            vehicle.Status = dto.Status.Trim();
        }

        vehicle.LastTelemetryAtUtc = dto.LastTelemetryAtUtc;
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

        if (user.VehicleId.HasValue && user.VehicleId != vehicleId)
        {
            return VehicleDriverAssignmentResult.DriverAlreadyAssigned();
        }

        if (vehicle.Driver is not null && vehicle.Driver.Id != user.Id)
        {
            vehicle.Driver.VehicleId = null;
            vehicle.Driver.Vehicle = null;
        }

        user.VehicleId = vehicle.Id;
        user.Vehicle = vehicle;
        vehicle.Driver = user;
        vehicle.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(vehicle).Reference(v => v.Driver).LoadAsync(cancellationToken);

        return VehicleDriverAssignmentResult.Success(vehicle);
    }

    public async Task<VehicleDriverRemovalResult> RemoveDriverAsync(int vehicleId, CancellationToken cancellationToken)
    {
        var vehicle = await _context.Vehicles
            .Include(v => v.Driver)
            .FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken);

        if (vehicle is null)
        {
            return VehicleDriverRemovalResult.VehicleNotFound();
        }

        if (vehicle.Driver is null)
        {
            return VehicleDriverRemovalResult.NoDriverAssigned();
        }

        vehicle.Driver.VehicleId = null;
        vehicle.Driver.Vehicle = null;
        vehicle.Driver = null;
        vehicle.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return VehicleDriverRemovalResult.Success();
    }
}
