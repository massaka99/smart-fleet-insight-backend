using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using SmartFleet.Services;

namespace SmartFleet.Telemetry;

public interface ITelemetryMessageProcessor
{
    Task ProcessAsync(string rawPayload, CancellationToken cancellationToken);
}

public class TelemetryMessageProcessor(
    IVehicleTelemetryService telemetryService,
    ITelemetryDeadLetterSink deadLetterSink,
    ITelemetryIngestionMonitor monitor,
    ILogger<TelemetryMessageProcessor> logger) : ITelemetryMessageProcessor
{
    private readonly IVehicleTelemetryService _telemetryService = telemetryService;
    private readonly ITelemetryDeadLetterSink _deadLetterSink = deadLetterSink;
    private readonly ITelemetryIngestionMonitor _monitor = monitor;
    private readonly ILogger<TelemetryMessageProcessor> _logger = logger;

    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public async Task ProcessAsync(string rawPayload, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawPayload))
        {
            _monitor.ReportValidationFailure();
            await _deadLetterSink.StoreAsync("Telemetry payload is empty.", string.Empty, cancellationToken);
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(rawPayload);
            var payload = document.RootElement.Deserialize<VehicleTelemetryPayload>(_serializerOptions);

            if (payload is null)
            {
                _monitor.ReportDeserializationFailure();
                await _deadLetterSink.StoreAsync("Deserialization returned null payload.", rawPayload, cancellationToken);
                return;
            }

            var validationErrors = VehicleTelemetryValidator.Validate(payload, document.RootElement);

            if (validationErrors.Count > 0)
            {
                _monitor.ReportValidationFailure();
                var reason = $"Validation failed: {string.Join("; ", validationErrors)}";
                await _deadLetterSink.StoreAsync(reason, rawPayload, cancellationToken);
                _logger.LogWarning("Telemetry payload rejected. {Reason}", reason);
                return;
            }

            await _telemetryService.ProcessTelemetryAsync(rawPayload, payload, cancellationToken);
            _monitor.ReportProcessed();
        }
        catch (JsonException ex)
        {
            _monitor.ReportDeserializationFailure();
            await _deadLetterSink.StoreAsync($"JSON parsing error: {ex.Message}", rawPayload, cancellationToken);
            _logger.LogWarning(ex, "Telemetry payload deserialization failed.");
        }
        catch (OperationCanceledException)
        {
            // Propagate cancellation.
            throw;
        }
        catch (Exception ex)
        {
            await _deadLetterSink.StoreAsync($"Processing error: {ex.Message}", rawPayload, cancellationToken);
            _logger.LogError(ex, "Unexpected error while processing telemetry payload.");
        }
    }
}
