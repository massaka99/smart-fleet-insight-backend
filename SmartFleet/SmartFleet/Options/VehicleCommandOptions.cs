namespace SmartFleet.Options;

public class VehicleCommandOptions
{
    public string Host { get; init; } = "localhost";

    public int Port { get; init; } = 1883;

    public string? FallbackHost { get; init; }

    public int? FallbackPort { get; init; }

    public bool UseWebSockets { get; init; }

    public string Topic { get; init; } = "fleet/commands";

    public int KeepAliveSeconds { get; init; } = 30;

    public string? Username { get; init; }

    public string? Password { get; init; }
}
