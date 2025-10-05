using System.ComponentModel.DataAnnotations;
using SmartFleet.Models;

namespace SmartFleet.Dtos;

public class VehicleDto
{
    public int Id { get; init; }
    public string LicensePlate { get; init; } = string.Empty;
    public string VehicleType { get; init; } = string.Empty;
    public string FuelType { get; init; } = string.Empty;
    public double FuelTankCapacity { get; init; }
    public double CurrentFuelLevel { get; init; }
    public string? BodyType { get; init; }
    public double KilometersDriven { get; init; }
    public double CO2Emission { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public VehicleDriverDto? Driver { get; init; }
}

public class VehicleCreateDto
{
    [Required]
    [StringLength(15)]
    public string LicensePlate { get; init; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string VehicleType { get; init; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string FuelType { get; init; } = string.Empty;

    [Range(0, double.MaxValue)]
    public double FuelTankCapacity { get; init; }

    [Range(0, double.MaxValue)]
    public double CurrentFuelLevel { get; init; }

    [StringLength(100)]
    public string? BodyType { get; init; }

    [Range(0, double.MaxValue)]
    public double KilometersDriven { get; init; }

    [Range(0, double.MaxValue)]
    public double CO2Emission { get; init; }
}

public class VehicleUpdateDto
{
    [Required]
    [StringLength(15)]
    public string LicensePlate { get; init; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string VehicleType { get; init; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string FuelType { get; init; } = string.Empty;

    [Range(0, double.MaxValue)]
    public double FuelTankCapacity { get; init; }

    [Range(0, double.MaxValue)]
    public double CurrentFuelLevel { get; init; }

    [StringLength(100)]
    public string? BodyType { get; init; }

    [Range(0, double.MaxValue)]
    public double KilometersDriven { get; init; }

    [Range(0, double.MaxValue)]
    public double CO2Emission { get; init; }
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
        LicensePlate = vehicle.LicensePlate,
        VehicleType = vehicle.VehicleType,
        FuelType = vehicle.FuelType,
        FuelTankCapacity = vehicle.FuelTankCapacity,
        CurrentFuelLevel = vehicle.CurrentFuelLevel,
        BodyType = vehicle.BodyType,
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
