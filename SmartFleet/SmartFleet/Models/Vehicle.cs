using System.ComponentModel.DataAnnotations;

namespace SmartFleet.Models;

public class Vehicle
{
    public int Id { get; set; }

    [Required]
    [StringLength(15)]
    public string LicensePlate { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string VehicleType { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string FuelType { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public double FuelTankCapacity { get; set; }

    [Range(0, double.MaxValue)]
    public double CurrentFuelLevel { get; set; }

    [StringLength(100)]
    public string BodyType { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public double KilometersDriven { get; set; }

    [Range(0, double.MaxValue)]
    public double CO2Emission { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public int? DriverId { get; set; }

    public User? Driver { get; set; }
}
