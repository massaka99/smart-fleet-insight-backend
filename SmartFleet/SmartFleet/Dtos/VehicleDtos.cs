using System.ComponentModel.DataAnnotations;
using SmartFleet.Models;

namespace SmartFleet.Dtos;

public class VehicleDto
{
    public int Id { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string LicensePlate { get; init; } = string.Empty;
    public string VehicleType { get; init; } = string.Empty;
    public string FuelType { get; init; } = string.Empty;
    public string Brand { get; init; } = string.Empty;
    public string FuelUnit { get; init; } = string.Empty;
    public double FuelTankCapacity { get; init; }
    public double? BatteryCapacity { get; init; }
    public double CurrentFuelLevel { get; init; }
    public double FuelLevelPercent { get; init; }
    public double FuelConsumptionPer100Km { get; init; }
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public double SpeedKmh { get; init; }
    public double HeadingDeg { get; init; }
    public double DistanceTravelledM { get; init; }
    public double DistanceRemainingM { get; init; }
    public double Progress { get; init; }
    public double EtaSeconds { get; init; }
    public string? BodyType { get; init; }
    public string RouteId { get; init; } = string.Empty;
    public string? RouteSummary { get; init; }
    public double RouteDistanceKm { get; init; }
    public double BaseSpeedKmh { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime? LastTelemetryAtUtc { get; init; }
    public double KilometersDriven { get; init; }
    public double CO2Emission { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public VehicleDriverDto? Driver { get; init; }
}

public class VehicleCreateDto
{
    [StringLength(64)]
    public string? ExternalId { get; init; }

    [Required]
    [StringLength(15)]
    public string LicensePlate { get; init; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string VehicleType { get; init; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string FuelType { get; init; } = string.Empty;

    [StringLength(64)]
    public string? Brand { get; init; }

    [StringLength(16)]
    public string? FuelUnit { get; init; }

    [Range(0, double.MaxValue)]
    public double FuelTankCapacity { get; init; }

    [Range(0, double.MaxValue)]
    public double? BatteryCapacity { get; init; }

    [Range(0, double.MaxValue)]
    public double CurrentFuelLevel { get; init; }

    [Range(0, 100)]
    public double FuelLevelPercent { get; init; }

    [Range(0, double.MaxValue)]
    public double FuelConsumptionPer100Km { get; init; }

    [StringLength(100)]
    public string? BodyType { get; init; }

    [Range(0, double.MaxValue)]
    public double KilometersDriven { get; init; }

    [Range(0, double.MaxValue)]
    public double CO2Emission { get; init; }

    [Range(-90, 90)]
    public double Latitude { get; init; }

    [Range(-180, 180)]
    public double Longitude { get; init; }

    [Range(0, double.MaxValue)]
    public double SpeedKmh { get; init; }

    [Range(0, 360)]
    public double HeadingDeg { get; init; }

    [Range(0, double.MaxValue)]
    public double DistanceTravelledM { get; init; }

    [Range(0, double.MaxValue)]
    public double DistanceRemainingM { get; init; }

    [Range(0, 1)]
    public double Progress { get; init; }

    [Range(0, double.MaxValue)]
    public double EtaSeconds { get; init; }

    [StringLength(64)]
    public string? RouteId { get; init; }

    [StringLength(256)]
    public string? RouteSummary { get; init; }

    [Range(0, double.MaxValue)]
    public double RouteDistanceKm { get; init; }

    [Range(0, double.MaxValue)]
    public double BaseSpeedKmh { get; init; }

    [StringLength(32)]
    public string? Status { get; init; }

    public DateTime? LastTelemetryAtUtc { get; init; }
}

public class VehicleUpdateDto
{
    [StringLength(64)]
    public string? ExternalId { get; init; }

    [Required]
    [StringLength(15)]
    public string LicensePlate { get; init; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string VehicleType { get; init; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string FuelType { get; init; } = string.Empty;

    [StringLength(64)]
    public string? Brand { get; init; }

    [StringLength(16)]
    public string? FuelUnit { get; init; }

    [Range(0, double.MaxValue)]
    public double FuelTankCapacity { get; init; }

    [Range(0, double.MaxValue)]
    public double? BatteryCapacity { get; init; }

    [Range(0, double.MaxValue)]
    public double CurrentFuelLevel { get; init; }

    [Range(0, 100)]
    public double FuelLevelPercent { get; init; }

    [Range(0, double.MaxValue)]
    public double FuelConsumptionPer100Km { get; init; }

    [StringLength(100)]
    public string? BodyType { get; init; }

    [Range(0, double.MaxValue)]
    public double KilometersDriven { get; init; }

    [Range(0, double.MaxValue)]
    public double CO2Emission { get; init; }

    [Range(-90, 90)]
    public double Latitude { get; init; }

    [Range(-180, 180)]
    public double Longitude { get; init; }

    [Range(0, double.MaxValue)]
    public double SpeedKmh { get; init; }

    [Range(0, 360)]
    public double HeadingDeg { get; init; }

    [Range(0, double.MaxValue)]
    public double DistanceTravelledM { get; init; }

    [Range(0, double.MaxValue)]
    public double DistanceRemainingM { get; init; }

    [Range(0, 1)]
    public double Progress { get; init; }

    [Range(0, double.MaxValue)]
    public double EtaSeconds { get; init; }

    [StringLength(64)]
    public string? RouteId { get; init; }

    [StringLength(256)]
    public string? RouteSummary { get; init; }

    [Range(0, double.MaxValue)]
    public double RouteDistanceKm { get; init; }

    [Range(0, double.MaxValue)]
    public double BaseSpeedKmh { get; init; }

    [StringLength(32)]
    public string? Status { get; init; }

    public DateTime? LastTelemetryAtUtc { get; init; }
}

public class VehicleDriverDto
{
    public int Id { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
}

public static class VehicleMappingExtensions
{
    public static VehicleDto ToVehicleDto(this Vehicle vehicle) => new()
    {
        Id = vehicle.Id,
        ExternalId = vehicle.ExternalId ?? string.Empty,
        LicensePlate = vehicle.LicensePlate,
        VehicleType = vehicle.VehicleType,
        FuelType = vehicle.FuelType,
        Brand = vehicle.Brand,
        FuelUnit = vehicle.FuelUnit,
        FuelTankCapacity = vehicle.FuelTankCapacity,
        BatteryCapacity = vehicle.BatteryCapacity,
        CurrentFuelLevel = vehicle.CurrentFuelLevel,
        FuelLevelPercent = vehicle.FuelLevelPercent,
        FuelConsumptionPer100Km = vehicle.FuelConsumptionPer100Km,
        Latitude = vehicle.Latitude,
        Longitude = vehicle.Longitude,
        SpeedKmh = vehicle.SpeedKmh,
        HeadingDeg = vehicle.HeadingDeg,
        DistanceTravelledM = vehicle.DistanceTravelledM,
        DistanceRemainingM = vehicle.DistanceRemainingM,
        Progress = vehicle.Progress,
        EtaSeconds = vehicle.EtaSeconds,
        BodyType = vehicle.BodyType,
        RouteId = vehicle.RouteId,
        RouteSummary = vehicle.RouteSummary,
        RouteDistanceKm = vehicle.RouteDistanceKm,
        BaseSpeedKmh = vehicle.BaseSpeedKmh,
        Status = vehicle.Status,
        LastTelemetryAtUtc = vehicle.LastTelemetryAtUtc,
        KilometersDriven = vehicle.KilometersDriven,
        CO2Emission = vehicle.CO2Emission,
        CreatedAt = vehicle.CreatedAt,
        UpdatedAt = vehicle.UpdatedAt,
        Driver = vehicle.Driver?.ToVehicleDriverDto()
    };

    public static VehicleDriverDto ToVehicleDriverDto(this User user) => new()
    {
        Id = user.Id,
        FirstName = user.FirstName,
        LastName = user.LastName,
        Email = user.Email
    };
}
