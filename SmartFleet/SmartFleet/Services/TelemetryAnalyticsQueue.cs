using System.Threading.Channels;
using SmartFleet.Models;

namespace SmartFleet.Services;

public class TelemetryAnalyticsQueue : ITelemetryAnalyticsQueue
{
    private readonly Channel<VehicleState> _channel;

    public TelemetryAnalyticsQueue()
    {
        _channel = Channel.CreateBounded<VehicleState>(new BoundedChannelOptions(512)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public ValueTask EnqueueAsync(VehicleState state, CancellationToken cancellationToken)
    {
        return _channel.Writer.WriteAsync(state, cancellationToken);
    }

    public IAsyncEnumerable<VehicleState> ReadAllAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
