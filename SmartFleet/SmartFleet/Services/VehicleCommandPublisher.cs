using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Formatter;
using MQTTnet.Protocol;
using SmartFleet.Options;

namespace SmartFleet.Services;

public interface IVehicleCommandPublisher
{
    Task PublishRouteUpdateAsync(VehicleRouteCommandPayload payload, CancellationToken cancellationToken);
}

public sealed class VehicleCommandPublisher : IVehicleCommandPublisher
{
    private readonly VehicleCommandOptions _options;
    private readonly ILogger<VehicleCommandPublisher> _logger;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    public VehicleCommandPublisher(IOptions<VehicleCommandOptions> options, ILogger<VehicleCommandPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task PublishRouteUpdateAsync(VehicleRouteCommandPayload payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (payload.Stops is null || payload.Stops.Count < 2)
        {
            throw new ArgumentException("A route update must contain at least two stops.", nameof(payload));
        }

        if (string.IsNullOrWhiteSpace(payload.VehicleId))
        {
            throw new ArgumentException("Vehicle identifier is required.", nameof(payload));
        }

        if (string.IsNullOrWhiteSpace(_options.Topic))
        {
            throw new InvalidOperationException("Vehicle command topic is not configured.");
        }

        var commandPayload = payload with
        {
            RequestedAtUtc = payload.RequestedAtUtc == default ? DateTime.UtcNow : payload.RequestedAtUtc,
        };

        var factory = new MqttFactory();
        using var client = factory.CreateMqttClient();

        var keepAliveSeconds = Math.Clamp(_options.KeepAliveSeconds, 10, 300);
        var endpoints = ResolveBrokerEndpoints().ToArray();
        Exception? lastConnectError = null;

        foreach (var (host, port) in endpoints)
        {
            _logger.LogInformation(
                "Attempting to connect to MQTT broker at {Host}:{Port} for route update {RequestId} targeting {VehicleId}.",
                host,
                port,
                commandPayload.RequestId,
                commandPayload.VehicleId);

            var clientOptionsBuilder = new MqttClientOptionsBuilder()
                .WithClientId($"vehicle-command-{Guid.NewGuid():N}".ToLowerInvariant())
                .WithCleanSession()
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(keepAliveSeconds))
                .WithProtocolVersion(MqttProtocolVersion.V500);

            if (_options.UseWebSockets)
            {
                var endpoint = $"ws://{host}:{port}";
                clientOptionsBuilder.WithWebSocketServer(options => options.WithUri(endpoint));
            }
            else
            {
                clientOptionsBuilder.WithTcpServer(host, port);
            }

            if (!string.IsNullOrWhiteSpace(_options.Username))
            {
                clientOptionsBuilder.WithCredentials(_options.Username, _options.Password ?? string.Empty);
            }

            var mqttClientOptions = clientOptionsBuilder.Build();

            try
            {
                await client.ConnectAsync(mqttClientOptions, cancellationToken);
                _logger.LogDebug("Connected to MQTT broker at {Host}:{Port}.", host, port);
                break;
            }
            catch (Exception connectError)
            {
                lastConnectError = connectError;
                _logger.LogWarning(connectError, "Failed to connect to MQTT broker at {Host}:{Port}.", host, port);
            }
        }

        if (client.IsConnected is false)
        {
            throw new InvalidOperationException("Unable to connect to any configured MQTT broker endpoint.", lastConnectError);
        }

        try
        {
            var messagePayload = JsonSerializer.Serialize(commandPayload, _serializerOptions);
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(_options.Topic)
                .WithPayload(messagePayload)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .WithRetainFlag(false)
                .Build();

            await client.PublishAsync(message, cancellationToken);
            _logger.LogInformation(
                "Published route update {RequestId} to vehicle {VehicleId} ({Stops} stops).",
                commandPayload.RequestId,
                commandPayload.VehicleId,
                commandPayload.Stops.Count);
        }
        finally
        {
            try
            {
                var disconnectOptions = new MqttClientDisconnectOptionsBuilder()
                    .WithReason(MqttClientDisconnectOptionsReason.NormalDisconnection)
                    .Build();
                await client.DisconnectAsync(disconnectOptions, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to disconnect MQTT command client cleanly.");
            }
        }
    }

    private IEnumerable<(string Host, int Port)> ResolveBrokerEndpoints()
    {
        var endpoints = new List<(string Host, int Port)>();

        if (!string.IsNullOrWhiteSpace(_options.Host))
        {
            endpoints.Add((_options.Host, _options.Port));
        }

        string? fallbackHost = _options.FallbackHost;
        if (string.IsNullOrWhiteSpace(fallbackHost))
        {
            var primaryHost = _options.Host;
            if (!string.Equals(primaryHost, "localhost", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(primaryHost, "127.0.0.1", StringComparison.OrdinalIgnoreCase))
            {
                fallbackHost = "localhost";
            }
        }

        if (!string.IsNullOrWhiteSpace(fallbackHost))
        {
            var fallbackPort = _options.FallbackPort ?? _options.Port;
            var alreadyDefined = endpoints.Any(endpoint =>
                string.Equals(endpoint.Host, fallbackHost, StringComparison.OrdinalIgnoreCase) &&
                endpoint.Port == fallbackPort);
            if (!alreadyDefined)
            {
                endpoints.Add((fallbackHost, fallbackPort));
            }
        }

        return endpoints;
    }
}

public sealed record VehicleRouteCommandPayload(
    string VehicleId,
    IReadOnlyList<VehicleRouteCommandStop> Stops
)
{
    public string Type { get; init; } = "set_route";

    public string? LicensePlate { get; init; }

    public string? RouteLabel { get; init; }

    public double? BaseSpeedKmh { get; init; }

    public string RequestId { get; init; } = Guid.NewGuid().ToString("N");

    public DateTime RequestedAtUtc { get; init; } = DateTime.UtcNow;

    public string Source { get; init; } = "dashboard";

    public VehicleRouteCommandRequester? RequestedBy { get; init; }
}

public sealed record VehicleRouteCommandStop(string Name, double Latitude, double Longitude);

public sealed record VehicleRouteCommandRequester(int? UserId, string? Name, string? Email);
