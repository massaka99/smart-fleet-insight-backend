using SmartFleet.Models;

namespace SmartFleet.Services;

public interface ITelemetryAnalyticsQueue
{
    ValueTask EnqueueAsync(VehicleState state, CancellationToken cancellationToken);

    IAsyncEnumerable<VehicleState> ReadAllAsync(CancellationToken cancellationToken);
}
