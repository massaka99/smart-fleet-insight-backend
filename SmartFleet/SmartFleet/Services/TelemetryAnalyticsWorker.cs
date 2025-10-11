using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartFleet.Models;

namespace SmartFleet.Services;

public class TelemetryAnalyticsWorker(
    ITelemetryAnalyticsQueue queue,
    ILogger<TelemetryAnalyticsWorker> logger) : BackgroundService
{
    private readonly ITelemetryAnalyticsQueue _queue = queue;
    private readonly ILogger<TelemetryAnalyticsWorker> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var state in _queue.ReadAllAsync(stoppingToken))
        {
            await DispatchAsync(state, stoppingToken);
        }
    }

    private async Task DispatchAsync(VehicleState state, CancellationToken cancellationToken)
    {
        try
        {
            // TODO: Integrate with analytics job scheduler or message bus.
            _logger.LogDebug("Queued analytics job for vehicle {VehicleId} with telemetry {TelemetryId}.",
                state.VehicleId, state.TelemetryId);
            await Task.CompletedTask;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to dispatch analytics job for vehicle {VehicleId}.", state.VehicleId);
        }
    }
}
