using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Formatter;
using MQTTnet.Protocol;
using SmartFleet.Data;
using SmartFleet.Models;
using SmartFleet.Models.Telemetry;
using SmartFleet.Options;

namespace SmartFleet.Services.Telemetry;

public class VehicleTelemetryIngestionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VehicleTelemetryIngestionService> _logger;
    private readonly TelemetryIngestionOptions _options;
    private readonly IManagedMqttClient _mqttClient;
    private readonly Channel<TelemetryEnvelope> _telemetryChannel;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public VehicleTelemetryIngestionService(
        IServiceScopeFactory scopeFactory,
        ILogger<VehicleTelemetryIngestionService> logger,
        IOptions<TelemetryIngestionOptions> optionsAccessor)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = optionsAccessor.Value;
        var capacity = Math.Max(1, _options.QueueCapacity);
        _telemetryChannel = Channel.CreateBounded<TelemetryEnvelope>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });

        _mqttClient = new MqttFactory().CreateManagedMqttClient();
        _mqttClient.ApplicationMessageReceivedAsync += HandleMessageAsync;
        _mqttClient.ConnectingFailedAsync += HandleConnectingFailedAsync;
        _mqttClient.DisconnectedAsync += HandleDisconnectedAsync;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping telemetry ingestion service.");

        try
        {
            await _mqttClient.StopAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to stop MQTT client cleanly.");
        }

        _telemetryChannel.Writer.TryComplete();

        await base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting telemetry ingestion service targeting {Host}:{Port} (Topic: {Topic}).",
            _options.Host, _options.Port, _options.Topic);

        var processingTask = ProcessQueueAsync(stoppingToken);

        try
        {
            await StartClientAsync(stoppingToken);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // expected when service stops
        }
        finally
        {
            _telemetryChannel.Writer.TryComplete();
        }

        try
        {
            await _mqttClient.StopAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while stopping MQTT client.");
        }

        await processingTask;
    }

    private async Task StartClientAsync(CancellationToken cancellationToken)
    {
        var clientId = $"telemetry-ingestor-{Environment.MachineName}-{Guid.NewGuid():N}".ToLowerInvariant();

        var clientOptionsBuilder = new MqttClientOptionsBuilder()
            .WithClientId(clientId)
            .WithCleanSession()
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(_options.KeepAliveSeconds))
            .WithProtocolVersion(MqttProtocolVersion.V500);

        if (_options.UseWebSockets)
        {
            var endpoint = $"ws://{_options.Host}:{_options.Port}";
            clientOptionsBuilder.WithWebSocketServer(options =>
            {
                options.WithUri(endpoint);
            });
        }
        else
        {
            clientOptionsBuilder.WithTcpServer(_options.Host, _options.Port);
        }

        var managedOptions = new ManagedMqttClientOptionsBuilder()
            .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
            .WithClientOptions(clientOptionsBuilder.Build())
            .Build();

        var topicFilter = new MqttTopicFilterBuilder()
            .WithTopic(_options.Topic)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
            .Build();

        await _mqttClient.StartAsync(managedOptions);
        await _mqttClient.SubscribeAsync(new[] { topicFilter });

        _logger.LogInformation("Telemetry ingestion connected and subscribed to topic {Topic}.", _options.Topic);
    }

    private async Task ProcessQueueAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var envelope in _telemetryChannel.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await PersistAsync(envelope, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to persist telemetry with id {TelemetryId}.", envelope.Payload.TelemetryId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // service stopping
        }
    }

    private async Task PersistAsync(TelemetryEnvelope envelope, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var payload = envelope.Payload;
        var receivedAt = envelope.ReceivedAtUtc;

        var normalizedExternalId = NullIfEmpty(TrimToLength(payload.VehicleId, 64));
        var normalizedPlate = NullIfEmpty(TrimToLength(payload.NumberPlate, 15));
        var normalizedBrand = TrimToLength(payload.Brand, 64);
        var normalizedFuelType = NullIfEmpty(TrimToLength(payload.FuelType, 50));
        var normalizedFuelUnit = NullIfEmpty(TrimToLength(payload.FuelUnit, 16));
        var normalizedRouteId = NullIfEmpty(TrimToLength(payload.RouteId, 64));
        var normalizedStatus = NullIfEmpty(TrimToLength(payload.Status, 32));
        var normalizedVehicleType = string.IsNullOrWhiteSpace(payload.Brand)
            ? "Unknown"
            : TrimToLength(payload.Brand, 100);

        var telemetryTimestamp = NormalizeTimestamp(payload.TimestampUtc, receivedAt);

        Vehicle? vehicle = null;

        if (!string.IsNullOrEmpty(normalizedExternalId))
        {
            vehicle = await dbContext.Vehicles
                .FirstOrDefaultAsync(v => v.ExternalId == normalizedExternalId, cancellationToken);
        }

        if (vehicle is null && !string.IsNullOrEmpty(normalizedPlate))
        {
            vehicle = await dbContext.Vehicles
                .FirstOrDefaultAsync(v => v.LicensePlate == normalizedPlate, cancellationToken);
        }

        if (vehicle is null)
        {
            vehicle = new Vehicle
            {
                ExternalId = normalizedExternalId,
                LicensePlate = normalizedPlate ?? GeneratePlaceholderPlate(),
                VehicleType = normalizedVehicleType,
                FuelType = string.IsNullOrEmpty(normalizedFuelType) ? "unknown" : normalizedFuelType!,
                Brand = normalizedBrand,
                FuelUnit = normalizedFuelUnit ?? string.Empty,
                FuelTankCapacity = payload.FuelCapacity ?? payload.BatteryCapacity ?? 0,
                BatteryCapacity = payload.BatteryCapacity,
                CurrentFuelLevel = payload.FuelLevel,
                FuelLevelPercent = payload.FuelLevelPercent,
                FuelConsumptionPer100Km = payload.FuelConsumptionPer100Km,
                BodyType = string.Empty,
                KilometersDriven = payload.OdometerKm,
                CO2Emission = payload.Co2EmissionKg,
                Latitude = payload.Position?.Lat ?? 0,
                Longitude = payload.Position?.Lon ?? 0,
                SpeedKmh = payload.SpeedKmh,
                HeadingDeg = payload.HeadingDeg,
                DistanceTravelledM = payload.DistanceTravelledM,
                DistanceRemainingM = payload.DistanceRemainingM,
                Progress = payload.Progress,
                EtaSeconds = payload.EtaSeconds,
                RouteId = normalizedRouteId ?? string.Empty,
                RouteSummary = string.IsNullOrWhiteSpace(payload.RouteSummary)
                    ? null
                    : TrimToLength(payload.RouteSummary, 256),
                RouteDistanceKm = payload.RouteDistanceKm,
                BaseSpeedKmh = payload.BaseSpeedKmh,
                Status = normalizedStatus ?? string.Empty,
                LastTelemetryAtUtc = telemetryTimestamp,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            dbContext.Vehicles.Add(vehicle);
        }
        else
        {
            if (!string.IsNullOrEmpty(normalizedExternalId))
            {
                vehicle.ExternalId = normalizedExternalId;
            }

            if (!string.IsNullOrEmpty(normalizedPlate))
            {
                vehicle.LicensePlate = normalizedPlate!;
            }

            if (!string.IsNullOrWhiteSpace(payload.Brand))
            {
                vehicle.Brand = normalizedBrand;
                if (string.IsNullOrWhiteSpace(vehicle.VehicleType) ||
                    string.Equals(vehicle.VehicleType, "Unknown", StringComparison.OrdinalIgnoreCase))
                {
                    vehicle.VehicleType = normalizedVehicleType;
                }
            }

            if (!string.IsNullOrEmpty(normalizedFuelType))
            {
                vehicle.FuelType = normalizedFuelType!;
            }

            if (!string.IsNullOrEmpty(normalizedFuelUnit))
            {
                vehicle.FuelUnit = normalizedFuelUnit!;
            }

            var capacity = payload.FuelCapacity ?? payload.BatteryCapacity;
            if (capacity.HasValue && capacity.Value >= 0)
            {
                vehicle.FuelTankCapacity = capacity.Value;
            }

            if (payload.BatteryCapacity.HasValue)
            {
                vehicle.BatteryCapacity = payload.BatteryCapacity;
            }

            vehicle.CurrentFuelLevel = payload.FuelLevel;
            vehicle.FuelLevelPercent = payload.FuelLevelPercent;
            vehicle.FuelConsumptionPer100Km = payload.FuelConsumptionPer100Km;
            vehicle.KilometersDriven = Math.Max(vehicle.KilometersDriven, payload.OdometerKm);
            vehicle.CO2Emission = payload.Co2EmissionKg;
            vehicle.Latitude = payload.Position?.Lat ?? vehicle.Latitude;
            vehicle.Longitude = payload.Position?.Lon ?? vehicle.Longitude;
            vehicle.SpeedKmh = payload.SpeedKmh;
            vehicle.HeadingDeg = payload.HeadingDeg;
            vehicle.DistanceTravelledM = Math.Max(vehicle.DistanceTravelledM, payload.DistanceTravelledM);
            vehicle.DistanceRemainingM = payload.DistanceRemainingM;
            vehicle.Progress = payload.Progress;
            vehicle.EtaSeconds = payload.EtaSeconds;

            if (!string.IsNullOrWhiteSpace(payload.RouteId))
            {
                vehicle.RouteId = normalizedRouteId ?? string.Empty;
            }

            vehicle.RouteSummary = string.IsNullOrWhiteSpace(payload.RouteSummary)
                ? vehicle.RouteSummary
                : TrimToLength(payload.RouteSummary, 256);

            vehicle.RouteDistanceKm = payload.RouteDistanceKm;
            vehicle.BaseSpeedKmh = payload.BaseSpeedKmh;

            if (!string.IsNullOrWhiteSpace(payload.Status))
            {
                vehicle.Status = normalizedStatus ?? string.Empty;
            }

            vehicle.LastTelemetryAtUtc = telemetryTimestamp;
            vehicle.UpdatedAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private Task HandleMessageAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        var rawJson = args.ApplicationMessage?.ConvertPayloadToString();

        if (string.IsNullOrWhiteSpace(rawJson))
        {
            _logger.LogWarning("Received telemetry message without payload, dropping.");
            return Task.CompletedTask;
        }

        VehicleTelemetryPayload? payload;

        try
        {
            payload = JsonSerializer.Deserialize<VehicleTelemetryPayload>(rawJson, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse telemetry payload: {PayloadSnippet}", rawJson[..Math.Min(rawJson.Length, 128)]);
            return Task.CompletedTask;
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.TelemetryId))
        {
            _logger.LogWarning("Telemetry payload missing telemetry_id. Payload dropped.");
            return Task.CompletedTask;
        }

        var envelope = new TelemetryEnvelope(payload, DateTime.UtcNow);

        if (!_telemetryChannel.Writer.TryWrite(envelope))
        {
            _logger.LogWarning("Telemetry channel full. Dropping telemetry with id {TelemetryId}.", payload.TelemetryId);
        }

        return Task.CompletedTask;
    }

    private Task HandleConnectingFailedAsync(ConnectingFailedEventArgs args)
    {
        _logger.LogWarning(args.Exception, "Telemetry ingestion failed to connect to MQTT broker. Reconnecting...");
        return Task.CompletedTask;
    }

    private Task HandleDisconnectedAsync(MqttClientDisconnectedEventArgs args)
    {
        if (args.Exception is not null)
        {
            _logger.LogWarning(args.Exception, "Telemetry ingestion disconnected from MQTT broker. Attempting to reconnect.");
        }
        else
        {
            _logger.LogInformation("Telemetry ingestion disconnected from MQTT broker. Attempting to reconnect.");
        }

        return Task.CompletedTask;
    }

    private static string TrimToLength(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static DateTime NormalizeTimestamp(DateTime timestamp, DateTime fallbackUtc)
    {
        if (timestamp == default)
        {
            return fallbackUtc;
        }

        return timestamp.Kind switch
        {
            DateTimeKind.Utc => timestamp,
            DateTimeKind.Local => timestamp.ToUniversalTime(),
            _ => DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)
        };
    }

    private static string GeneratePlaceholderPlate()
    {
        return $"AUTO-{Guid.NewGuid():N}"[..15].ToUpperInvariant();
    }

    private static string? NullIfEmpty(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private sealed record TelemetryEnvelope(VehicleTelemetryPayload Payload, DateTime ReceivedAtUtc);
}
