using System.ComponentModel.DataAnnotations;

namespace SmartFleet.Models;

public class Vehicle
{
    public int Id { get; set; }

    [StringLength(64)]
    public string? ExternalId { get; set; }

    [Required]
    [StringLength(15)]
    public string LicensePlate { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string VehicleType { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string FuelType { get; set; } = string.Empty;

    [StringLength(64)]
    public string Brand { get; set; } = string.Empty;

    [StringLength(16)]
    public string FuelUnit { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public double FuelTankCapacity { get; set; }

    [Range(0, double.MaxValue)]
    public double? BatteryCapacity { get; set; }

    [Range(0, double.MaxValue)]
    public double CurrentFuelLevel { get; set; }

    [Range(0, 100)]
    public double FuelLevelPercent { get; set; }

    [Range(0, double.MaxValue)]
    public double FuelConsumptionPer100Km { get; set; }

    [StringLength(100)]
    public string BodyType { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public double KilometersDriven { get; set; }

    [Range(0, double.MaxValue)]
    public double CO2Emission { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Range(-90, 90)]
    public double Latitude { get; set; }

    [Range(-180, 180)]
    public double Longitude { get; set; }

    [Range(0, double.MaxValue)]
    public double SpeedKmh { get; set; }

    [Range(0, 360)]
    public double HeadingDeg { get; set; }

    [Range(0, double.MaxValue)]
    public double DistanceTravelledM { get; set; }

    [Range(0, double.MaxValue)]
    public double DistanceRemainingM { get; set; }

    [Range(0, 1)]
    public double Progress { get; set; }

    [Range(0, double.MaxValue)]
    public double EtaSeconds { get; set; }

    [StringLength(64)]
    public string RouteId { get; set; } = string.Empty;

    [StringLength(256)]
    public string? RouteSummary { get; set; }

    [Range(0, double.MaxValue)]
    public double RouteDistanceKm { get; set; }

    [Range(0, double.MaxValue)]
    public double BaseSpeedKmh { get; set; }

    [StringLength(32)]
    public string Status { get; set; } = string.Empty;

    public DateTime? LastTelemetryAtUtc { get; set; }

    public User? Driver { get; set; }
}
