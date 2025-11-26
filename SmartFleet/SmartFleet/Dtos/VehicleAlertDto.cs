using System;
using System.Text.Json.Serialization;

namespace SmartFleet.Dtos;

public class VehicleAlertDto
{
    public int VehicleId { get; init; }
    public string LicensePlate { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    [JsonPropertyName("raisedAtUtc")]
    public DateTimeOffset RaisedAtUtc { get; init; }
}
