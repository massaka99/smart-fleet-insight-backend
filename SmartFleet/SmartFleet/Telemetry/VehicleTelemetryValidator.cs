using System.Globalization;
using System.Text.Json;

namespace SmartFleet.Telemetry;

public static class VehicleTelemetryValidator
{
    public static IReadOnlyList<string> Validate(VehicleTelemetryPayload payload, JsonElement rootElement)
    {
        var errors = new List<string>();

        if (payload.Progress is < 0 or > 1)
        {
            errors.Add("progress must be between 0 and 1.");
        }

        if (payload.FuelLevelPercent is < 0 or > 100)
        {
            errors.Add("fuel_level_percent must be between 0 and 100.");
        }

        if (payload.Stops is null || payload.Stops.Count < 2)
        {
            errors.Add("stops must include at least two entries.");
        }

        if (!TryValidateTimestamp(rootElement, out var timestampError))
        {
            errors.Add(timestampError);
        }

        return errors;
    }

    private static bool TryValidateTimestamp(JsonElement rootElement, out string error)
    {
        error = string.Empty;

        if (!rootElement.TryGetProperty("timestamp_utc", out var timestampElement))
        {
            error = "timestamp_utc property is missing.";
            return false;
        }

        if (timestampElement.ValueKind != JsonValueKind.String)
        {
            error = "timestamp_utc must be a string.";
            return false;
        }

        var timestampString = timestampElement.GetString();

        if (string.IsNullOrWhiteSpace(timestampString))
        {
            error = "timestamp_utc cannot be empty.";
            return false;
        }

        if (!DateTimeOffset.TryParse(timestampString, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            error = "timestamp_utc is not a valid ISO 8601 timestamp.";
            return false;
        }

        if (parsed.Offset != TimeSpan.Zero)
        {
            error = "timestamp_utc must be expressed in UTC (e.g. end with 'Z').";
            return false;
        }

        return true;
    }
}
