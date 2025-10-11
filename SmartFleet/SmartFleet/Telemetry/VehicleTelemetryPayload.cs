using System.Text.Json.Serialization;

namespace SmartFleet.Telemetry;

public sealed record VehicleTelemetryPayload(
    [property: JsonPropertyName("telemetry_id")] string TelemetryId,
    [property: JsonPropertyName("vehicle_id")] string VehicleId,
    [property: JsonPropertyName("number_plate")] string NumberPlate,
    [property: JsonPropertyName("fuel_type")] string FuelType,
    [property: JsonPropertyName("fuel_unit")] string FuelUnit,
    [property: JsonPropertyName("fuel_capacity")] double FuelCapacity,
    [property: JsonPropertyName("fuel_level")] double FuelLevel,
    [property: JsonPropertyName("fuel_level_percent")] double FuelLevelPercent,
    [property: JsonPropertyName("fuel_consumption_per_100km")] double FuelConsumptionPer100Km,
    [property: JsonPropertyName("odometer_km")] double OdometerKm,
    [property: JsonPropertyName("co2_emission_kg")] double Co2EmissionKg,
    [property: JsonPropertyName("route_id")] string RouteId,
    [property: JsonPropertyName("route_summary")] string RouteSummary,
    [property: JsonPropertyName("route_distance_km")] double RouteDistanceKm,
    [property: JsonPropertyName("base_speed_kmh")] double BaseSpeedKmh,
    [property: JsonPropertyName("timestamp_utc")] DateTime TimestampUtc,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("position")] VehicleTelemetryPosition Position,
    [property: JsonPropertyName("speed_kmh")] double SpeedKmh,
    [property: JsonPropertyName("heading_deg")] double HeadingDeg,
    [property: JsonPropertyName("distance_travelled_m")] double DistanceTravelledM,
    [property: JsonPropertyName("distance_remaining_m")] double DistanceRemainingM,
    [property: JsonPropertyName("progress")] double Progress,
    [property: JsonPropertyName("eta_seconds")] double EtaSeconds,
    [property: JsonPropertyName("stops")] IReadOnlyList<VehicleTelemetryStop> Stops);

public sealed record VehicleTelemetryPosition(
    [property: JsonPropertyName("lat")] double Latitude,
    [property: JsonPropertyName("lon")] double Longitude);

public sealed record VehicleTelemetryStop(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("lat")] double Latitude,
    [property: JsonPropertyName("lon")] double Longitude);
