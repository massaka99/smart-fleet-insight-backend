namespace SmartFleet.Options;

public class TelemetryIngestionOptions
{
    public string Host { get; set; } = "mosquitto";
    public int Port { get; set; } = 1883;
    public bool UseWebSockets { get; set; }
    public string Topic { get; set; } = "fleet/telemetry";
    public int KeepAliveSeconds { get; set; } = 60;
    public int QueueCapacity { get; set; } = 4_096;
    public int BatchSize { get; set; } = 32;
    public int FlushIntervalMilliseconds { get; set; } = 200;
}
