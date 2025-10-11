namespace SmartFleet.Services;

public interface ITelemetryDeadLetterSink
{
    Task StoreAsync(string reason, string payloadJson, CancellationToken cancellationToken);
}
