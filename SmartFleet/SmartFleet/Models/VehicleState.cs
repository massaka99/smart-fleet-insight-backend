using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartFleet.Models;

public class VehicleState
{
    public int Id { get; set; }

    [Required]
    [StringLength(36)]
    public string TelemetryId { get; set; } = string.Empty;

    [Required]
    [StringLength(32)]
    public string VehicleCode { get; set; } = string.Empty;

    [Required]
    [StringLength(15)]
    public string NumberPlate { get; set; } = string.Empty;

    [Required]
    public int VehicleId { get; set; }

    public Vehicle? Vehicle { get; set; }

    [Required]
    [StringLength(32)]
    public string FuelType { get; set; } = string.Empty;

    [Required]
    [StringLength(8)]
    public string FuelUnit { get; set; } = string.Empty;

    public double FuelCapacity { get; set; }

    public double FuelLevel { get; set; }

    public double FuelLevelPercent { get; set; }

    public double FuelConsumptionPer100Km { get; set; }

    public double OdometerKm { get; set; }

    public double Co2EmissionKg { get; set; }

    [StringLength(32)]
    public string RouteId { get; set; } = string.Empty;

    [StringLength(256)]
    public string RouteSummary { get; set; } = string.Empty;

    public double RouteDistanceKm { get; set; }

    public double BaseSpeedKmh { get; set; }

    public DateTime TimestampUtc { get; set; }

    [StringLength(32)]
    public string Status { get; set; } = string.Empty;

    [Column(TypeName = "double precision")]
    public double Latitude { get; set; }

    [Column(TypeName = "double precision")]
    public double Longitude { get; set; }

    public double SpeedKmh { get; set; }

    public double HeadingDeg { get; set; }

    public double DistanceTravelledM { get; set; }

    public double DistanceRemainingM { get; set; }

    public double Progress { get; set; }

    public double EtaSeconds { get; set; }

    [StringLength(2048)]
    public string StopsJson { get; set; } = "[]";

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
