using System.ComponentModel.DataAnnotations;

namespace SmartFleet.Dtos;

public class VehicleRouteStopDto
{
    [Required]
    [StringLength(128)]
    public string Name { get; init; } = string.Empty;

    [Range(-90, 90)]
    public double Latitude { get; init; }

    [Range(-180, 180)]
    public double Longitude { get; init; }
}

public class VehicleRouteUpdateRequest
{
    [Required]
    [MinLength(2)]
    [MaxLength(12)]
    public List<VehicleRouteStopDto> Stops { get; init; } = new();

    [Range(5, 120)]
    public double? BaseSpeedKmh { get; init; }

    [StringLength(80)]
    public string? RouteLabel { get; init; }
}
