namespace SmartFleet.Options;

public sealed class MqttOptions
{
    public const string SectionName = "Mqtt";

    public bool Enabled { get; set; } = true;

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 1883;

    public string? Username { get; set; }

    public string? Password { get; set; }

    public bool UseTls { get; set; }

    public string TelemetryTopic { get; set; } = "fleet/telemetry";

    public string StatusTopic { get; set; } = "fleet/telemetry/backend-status";

    public string OfflineWillPayload { get; set; } = "offline";

    public int KeepAliveSeconds { get; set; } = 30;

    public int ConnectionTimeoutSeconds { get; set; } = 10;

    public int ReconnectBaseSeconds { get; set; } = 1;

    public int ReconnectMaxSeconds { get; set; } = 30;

    public int InactivityWarningSeconds { get; set; } = 30;

    public int ProcessingQueueCapacity { get; set; } = 256;
}
