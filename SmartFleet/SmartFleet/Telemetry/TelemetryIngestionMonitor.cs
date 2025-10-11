using System.Threading;

namespace SmartFleet.Telemetry;

public record TelemetryIngestionMetrics(
    bool IsConnected,
    DateTime? LastMessageUtc,
    long TotalProcessed,
    long DeserializationFailures,
    long ValidationFailures,
    string? LastDisconnectReason,
    DateTime? LastDisconnectUtc);

public interface ITelemetryIngestionMonitor
{
    void ReportConnected();

    void ReportDisconnected(string reason);

    void ReportMessageReceived();

    void ReportProcessed();

    void ReportDeserializationFailure();

    void ReportValidationFailure();

    TelemetryIngestionMetrics GetSnapshot();
}

public class TelemetryIngestionMonitor : ITelemetryIngestionMonitor
{
    private int _isConnected;
    private long _lastMessageTicks;
    private long _processedMessages;
    private long _deserializationFailures;
    private long _validationFailures;
    private long _lastDisconnectTicks;
    private string? _lastDisconnectReason;

    public void ReportConnected()
    {
        Interlocked.Exchange(ref _isConnected, 1);
        Interlocked.Exchange(ref _lastDisconnectTicks, 0);
        _lastDisconnectReason = null;
    }

    public void ReportDisconnected(string reason)
    {
        Interlocked.Exchange(ref _isConnected, 0);
        Interlocked.Exchange(ref _lastDisconnectTicks, DateTime.UtcNow.Ticks);
        _lastDisconnectReason = reason;
    }

    public void ReportMessageReceived()
    {
        Interlocked.Exchange(ref _lastMessageTicks, DateTime.UtcNow.Ticks);
    }

    public void ReportProcessed()
    {
        Interlocked.Increment(ref _processedMessages);
    }

    public void ReportDeserializationFailure()
    {
        Interlocked.Increment(ref _deserializationFailures);
    }

    public void ReportValidationFailure()
    {
        Interlocked.Increment(ref _validationFailures);
    }

    public TelemetryIngestionMetrics GetSnapshot()
    {
        var lastMessageUtc = ReadTimestamp(_lastMessageTicks);
        var lastDisconnectUtc = ReadTimestamp(_lastDisconnectTicks);
        var connected = Interlocked.CompareExchange(ref _isConnected, 0, 0) == 1;

        return new TelemetryIngestionMetrics(
            connected,
            lastMessageUtc,
            Interlocked.Read(ref _processedMessages),
            Interlocked.Read(ref _deserializationFailures),
            Interlocked.Read(ref _validationFailures),
            _lastDisconnectReason,
            lastDisconnectUtc);
    }

    private static DateTime? ReadTimestamp(long ticks)
    {
        return ticks == 0 ? null : new DateTime(ticks, DateTimeKind.Utc);
    }
}
