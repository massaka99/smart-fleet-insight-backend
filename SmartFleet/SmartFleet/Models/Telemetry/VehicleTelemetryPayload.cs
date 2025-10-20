using System.Text.Json.Serialization;

namespace SmartFleet.Models.Telemetry;

public class VehicleTelemetryPayload
{
    [JsonPropertyName("telemetry_id")]
    public string TelemetryId { get; set; } = string.Empty;

    [JsonPropertyName("vehicle_id")]
    public string VehicleId { get; set; } = string.Empty;

    [JsonPropertyName("number_plate")]
    public string NumberPlate { get; set; } = string.Empty;

    [JsonPropertyName("brand")]
    public string Brand { get; set; } = string.Empty;

    [JsonPropertyName("fuel_type")]
    public string FuelType { get; set; } = string.Empty;

    [JsonPropertyName("fuel_unit")]
    public string FuelUnit { get; set; } = string.Empty;

    [JsonPropertyName("fuel_capacity")]
    public double? FuelCapacity { get; set; }

    [JsonPropertyName("battery_capacity")]
    public double? BatteryCapacity { get; set; }

    [JsonPropertyName("fuel_level")]
    public double FuelLevel { get; set; }

    [JsonPropertyName("fuel_level_percent")]
    public double FuelLevelPercent { get; set; }

    [JsonPropertyName("fuel_consumption_per_100km")]
    public double FuelConsumptionPer100Km { get; set; }

    [JsonPropertyName("odometer_km")]
    public double OdometerKm { get; set; }

    [JsonPropertyName("co2_emission_kg")]
    public double Co2EmissionKg { get; set; }

    [JsonPropertyName("route_id")]
    public string RouteId { get; set; } = string.Empty;

    [JsonPropertyName("route_summary")]
    public string? RouteSummary { get; set; }

    [JsonPropertyName("route_distance_km")]
    public double RouteDistanceKm { get; set; }

    [JsonPropertyName("base_speed_kmh")]
    public double BaseSpeedKmh { get; set; }

    [JsonPropertyName("timestamp_utc")]
    public DateTime TimestampUtc { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("position")]
    public VehicleTelemetryPosition Position { get; set; } = new();

    [JsonPropertyName("speed_kmh")]
    public double SpeedKmh { get; set; }

    [JsonPropertyName("heading_deg")]
    public double HeadingDeg { get; set; }

    [JsonPropertyName("distance_travelled_m")]
    public double DistanceTravelledM { get; set; }

    [JsonPropertyName("distance_remaining_m")]
    public double DistanceRemainingM { get; set; }

    [JsonPropertyName("progress")]
    public double Progress { get; set; }

    [JsonPropertyName("eta_seconds")]
    public double EtaSeconds { get; set; }

    [JsonPropertyName("stops")]
    public IReadOnlyList<VehicleTelemetryStop> Stops { get; set; } = Array.Empty<VehicleTelemetryStop>();
}

public class VehicleTelemetryPosition
{
    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("lon")]
    public double Lon { get; set; }
}

public class VehicleTelemetryStop
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("lon")]
    public double Lon { get; set; }
}
