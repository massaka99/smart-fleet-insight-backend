using Microsoft.EntityFrameworkCore;
using System.Linq;
using SmartFleet.Data;
using SmartFleet.Dtos;
using SmartFleet.Models;

namespace SmartFleet.Services;

public class VehicleService(ApplicationDbContext context) : IVehicleService
{
    private readonly ApplicationDbContext _context = context;
    private static readonly TimeSpan StaleTelemetryThreshold = TimeSpan.FromMinutes(5);

    public async Task<IReadOnlyCollection<Vehicle>> GetAllAsync(CancellationToken cancellationToken)
    {
        var vehicles = await _context.Vehicles
            .Include(v => v.Driver)
            .AsNoTracking()
            .OrderByDescending(v => v.LastTelemetryAtUtc ?? v.UpdatedAt)
            .ToListAsync(cancellationToken);

        var staleThresholdUtc = DateTime.UtcNow - StaleTelemetryThreshold;

        foreach (var vehicle in vehicles)
        {
            if (vehicle.LastTelemetryAtUtc is null || vehicle.LastTelemetryAtUtc < staleThresholdUtc)
            {
                vehicle.Status = "offline";
            }
        }

        return vehicles;
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

    public async Task ApplyRoutePreviewAsync(
        int vehicleId,
        IReadOnlyList<VehicleRouteCommandStop> stops,
        double? baseSpeedKmh,
        string? routeLabel,
        string? requestId,
        CancellationToken cancellationToken)
    {
        var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken);
        if (vehicle is null || stops.Count < 2)
        {
            return;
        }

        var summary = BuildRouteSummary(stops, routeLabel);
        if (!string.IsNullOrWhiteSpace(summary))
        {
            vehicle.RouteSummary = summary;
        }

        if (!string.IsNullOrWhiteSpace(requestId))
        {
            vehicle.RouteId = requestId.Trim();
        }

        var distanceKm = CalculateRouteDistanceKm(stops);
        double? safeDistanceKm = null;
        if (distanceKm.HasValue)
        {
            safeDistanceKm = Math.Max(distanceKm.Value, 0);
            vehicle.RouteDistanceKm = safeDistanceKm.Value;
            vehicle.DistanceRemainingM = Math.Max(0, safeDistanceKm.Value * 1000);
            vehicle.DistanceTravelledM = 0;
            vehicle.Progress = 0;
        }

        if (baseSpeedKmh.HasValue && baseSpeedKmh.Value > 0)
        {
            vehicle.BaseSpeedKmh = baseSpeedKmh.Value;
            if (safeDistanceKm.HasValue)
            {
                var etaHours = safeDistanceKm.Value <= 0
                    ? 0
                    : safeDistanceKm.Value / baseSpeedKmh.Value;
                vehicle.EtaSeconds = Math.Max(0, etaHours * 3600);
            }
        }

        vehicle.Status = string.IsNullOrWhiteSpace(vehicle.Status) ? "pending_route_update" : vehicle.Status;
        vehicle.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
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

    private static double? CalculateRouteDistanceKm(IReadOnlyList<VehicleRouteCommandStop> stops)
    {
        if (stops.Count < 2)
        {
            return null;
        }

        double total = 0;
        for (var index = 1; index < stops.Count; index += 1)
        {
            var previous = stops[index - 1];
            var current = stops[index];
            total += Haversine(previous.Latitude, previous.Longitude, current.Latitude, current.Longitude);
        }

        if (double.IsNaN(total) || double.IsInfinity(total))
        {
            return null;
        }

        return Math.Max(0, Math.Round(total, 3));
    }

    private static string? BuildRouteSummary(IReadOnlyList<VehicleRouteCommandStop> stops, string? overrideLabel)
    {
        if (!string.IsNullOrWhiteSpace(overrideLabel))
        {
            return overrideLabel.Trim();
        }

        var names = stops.Select(stop => stop.Name?.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray();

        return names.Length > 0 ? string.Join(" -> ", names) : null;
    }

    private static double Haversine(double fromLat, double fromLng, double toLat, double toLng)
    {
        const double EarthRadiusKm = 6371.0;
        var dLat = DegreesToRadians(toLat - fromLat);
        var dLng = DegreesToRadians(toLng - fromLng);

        var fromLatRad = DegreesToRadians(fromLat);
        var toLatRad = DegreesToRadians(toLat);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(fromLatRad) * Math.Cos(toLatRad) *
                Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusKm * c;
    }

    private static double DegreesToRadians(double value) => value * Math.PI / 180.0;
}
