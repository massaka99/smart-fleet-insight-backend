using System.Text.Json;
using System.Text.Json.Serialization;
using SmartFleet.Models;

namespace SmartFleet.Dtos;

public record VehicleTelemetryDto(
    int VehicleId,
    string VehicleCode,
    string LicensePlate,
    string FuelType,
    string FuelUnit,
    double FuelCapacity,
    double FuelLevel,
    double FuelLevelPercent,
    double FuelConsumptionPer100Km,
    double OdometerKm,
    double Co2EmissionKg,
    string RouteId,
    string RouteSummary,
    double RouteDistanceKm,
    double BaseSpeedKmh,
    DateTime TimestampUtc,
    string Status,
    double Latitude,
    double Longitude,
    double SpeedKmh,
    double HeadingDeg,
    double DistanceTravelledM,
    double DistanceRemainingM,
    double Progress,
    double EtaSeconds,
    IReadOnlyList<VehicleTelemetryStopDto> Stops,
    DateTime UpdatedAtUtc);

public record VehicleTelemetryStopDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("lat")] double Latitude,
    [property: JsonPropertyName("lon")] double Longitude);

public static class VehicleTelemetryMappingExtensions
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static VehicleTelemetryDto ToDto(this VehicleState state)
    {
        var stops = DeserializeStops(state.StopsJson);
        return new VehicleTelemetryDto(
            state.VehicleId,
            state.VehicleCode,
            state.NumberPlate,
            state.FuelType,
            state.FuelUnit,
            state.FuelCapacity,
            state.FuelLevel,
            state.FuelLevelPercent,
            state.FuelConsumptionPer100Km,
            state.OdometerKm,
            state.Co2EmissionKg,
            state.RouteId,
            state.RouteSummary,
            state.RouteDistanceKm,
            state.BaseSpeedKmh,
            state.TimestampUtc,
            state.Status,
            state.Latitude,
            state.Longitude,
            state.SpeedKmh,
            state.HeadingDeg,
            state.DistanceTravelledM,
            state.DistanceRemainingM,
            state.Progress,
            state.EtaSeconds,
            stops,
            state.UpdatedAtUtc);
    }

    private static IReadOnlyList<VehicleTelemetryStopDto> DeserializeStops(string stopsJson)
    {
        if (string.IsNullOrWhiteSpace(stopsJson))
        {
            return Array.Empty<VehicleTelemetryStopDto>();
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<VehicleTelemetryStopDto>>(stopsJson, SerializerOptions)
                   ?? Array.Empty<VehicleTelemetryStopDto>();
        }
        catch (JsonException)
        {
            return Array.Empty<VehicleTelemetryStopDto>();
        }
    }
}
