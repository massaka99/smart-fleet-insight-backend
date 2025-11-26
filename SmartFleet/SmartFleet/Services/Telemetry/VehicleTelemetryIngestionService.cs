using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Channels;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
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
using SmartFleet.Dtos;
using SmartFleet.Hubs;
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
    private readonly IHubContext<VehicleHub> _vehicleHubContext;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private readonly int _batchSize;
    private readonly TimeSpan _flushInterval;
    private readonly ConcurrentDictionary<int, ConcurrentDictionary<string, DateTimeOffset>> _alertTracker = new();
    private static readonly TimeSpan AlertCooldown = TimeSpan.FromMinutes(5);
    private const double MaxSpeedSaneKmh = 220.0;
    private const double MinSpeedSaneKmh = 0.0;
    private const double MinFuelPercent = 0.0;
    private const double MaxFuelPercent = 100.0;
    private const double MinLatitude = -90.0;
    private const double MaxLatitude = 90.0;
    private const double MinLongitude = -180.0;
    private const double MaxLongitude = 180.0;

    public VehicleTelemetryIngestionService(
        IServiceScopeFactory scopeFactory,
        ILogger<VehicleTelemetryIngestionService> logger,
        IOptions<TelemetryIngestionOptions> optionsAccessor,
        IHubContext<VehicleHub> vehicleHubContext)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = optionsAccessor.Value;
        _vehicleHubContext = vehicleHubContext;
        var capacity = Math.Max(1, _options.QueueCapacity);
        _telemetryChannel = Channel.CreateBounded<TelemetryEnvelope>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

        _mqttClient = new MqttFactory().CreateManagedMqttClient();
        _mqttClient.ApplicationMessageReceivedAsync += HandleMessageAsync;
        _mqttClient.ConnectingFailedAsync += HandleConnectingFailedAsync;
        _mqttClient.DisconnectedAsync += HandleDisconnectedAsync;

        _batchSize = Math.Max(1, _options.BatchSize);
        var flushMillis = Math.Clamp(_options.FlushIntervalMilliseconds, 25, 2000);
        _flushInterval = TimeSpan.FromMilliseconds(flushMillis);
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
        var batch = new List<TelemetryEnvelope>(_batchSize);

        try
        {
            while (!stoppingToken.IsCancellationRequested &&
                   await _telemetryChannel.Reader.WaitToReadAsync(stoppingToken))
            {
                batch.Clear();

                var firstEnvelope = await _telemetryChannel.Reader.ReadAsync(stoppingToken);
                batch.Add(firstEnvelope);

                var flushDeadline = DateTime.UtcNow + _flushInterval;

                while (batch.Count < _batchSize)
                {
                    if (_telemetryChannel.Reader.TryRead(out var nextEnvelope))
                    {
                        batch.Add(nextEnvelope);
                        continue;
                    }

                    var remaining = flushDeadline - DateTime.UtcNow;
                    if (remaining <= TimeSpan.Zero)
                    {
                        break;
                    }

                    try
                    {
                        var waitTask = _telemetryChannel.Reader.WaitToReadAsync(stoppingToken).AsTask();
                        var delayTask = Task.Delay(remaining, stoppingToken);
                        var completed = await Task.WhenAny(waitTask, delayTask);

                        if (completed == waitTask)
                        {
                            if (!waitTask.Result)
                            {
                                break;
                            }

                            continue;
                        }

                        break;
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        throw;
                    }
                }

                try
                {
                    await PersistBatchAsync(batch, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to persist telemetry batch containing {Count} entries.", batch.Count);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // service stopping
        }
    }

    private async Task PersistBatchAsync(
        IReadOnlyList<TelemetryEnvelope> batch,
        CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var byExternalId = new Dictionary<string, Vehicle>(StringComparer.OrdinalIgnoreCase);
        var byLicensePlate = new Dictionary<string, Vehicle>(StringComparer.OrdinalIgnoreCase);
        var vehiclesToBroadcast = new List<Vehicle>(batch.Count);

        foreach (var envelope in batch)
        {
            try
            {
                if (HasInvalidCoreTelemetry(envelope, out var reason))
                {
                    _logger.LogWarning("Dropping telemetry {TelemetryId} due to invalid {Reason} values.", envelope.Payload.TelemetryId, reason);
                    continue;
                }

                var vehicle = await UpsertVehicleAsync(
                    dbContext,
                    envelope,
                    byExternalId,
                    byLicensePlate,
                    cancellationToken);

                vehiclesToBroadcast.Add(vehicle);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist telemetry with id {TelemetryId}.", envelope.Payload.TelemetryId);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var vehicle in vehiclesToBroadcast.DistinctBy(v => v.Id))
        {
            await BroadcastVehicleUpdateAsync(dbContext, vehicle, cancellationToken);
        }
    }

    private async Task<Vehicle> UpsertVehicleAsync(
        ApplicationDbContext dbContext,
        TelemetryEnvelope envelope,
        IDictionary<string, Vehicle> byExternalId,
        IDictionary<string, Vehicle> byLicensePlate,
        CancellationToken cancellationToken)
    {
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
            if (!byExternalId.TryGetValue(normalizedExternalId, out vehicle))
            {
                vehicle = await dbContext.Vehicles
                    .FirstOrDefaultAsync(v => v.ExternalId == normalizedExternalId, cancellationToken);

                if (vehicle is not null)
                {
                    if (!string.IsNullOrEmpty(vehicle.ExternalId))
                    {
                        byExternalId[vehicle.ExternalId] = vehicle;
                    }

                    if (!string.IsNullOrEmpty(vehicle.LicensePlate))
                    {
                        byLicensePlate[vehicle.LicensePlate] = vehicle;
                    }
                }
            }
        }

        if (vehicle is null && !string.IsNullOrEmpty(normalizedPlate))
        {
            if (!byLicensePlate.TryGetValue(normalizedPlate, out vehicle))
            {
                vehicle = await dbContext.Vehicles
                    .FirstOrDefaultAsync(v => v.LicensePlate == normalizedPlate, cancellationToken);

                if (vehicle is not null)
                {
                    if (!string.IsNullOrEmpty(vehicle.ExternalId))
                    {
                        byExternalId[vehicle.ExternalId] = vehicle;
                    }

                    byLicensePlate[vehicle.LicensePlate] = vehicle;
                }
            }
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
                vehicle.LicensePlate = normalizedPlate;
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

        NormalizeVehicleTelemetry(vehicle);

        if (!string.IsNullOrEmpty(normalizedExternalId))
        {
            byExternalId[normalizedExternalId] = vehicle;
        }
        else if (!string.IsNullOrEmpty(vehicle.ExternalId))
        {
            byExternalId[vehicle.ExternalId] = vehicle;
        }

        var plateKey = normalizedPlate ?? vehicle.LicensePlate;
        if (!string.IsNullOrEmpty(plateKey))
        {
            byLicensePlate[plateKey] = vehicle;
        }

        if (payload.Driver is not null)
        {
            await AssignDriverAsync(dbContext, vehicle, payload.Driver, cancellationToken);
        }
        else
        {
            await EnsureDriverAssignmentAsync(dbContext, vehicle, cancellationToken);
        }

        return vehicle;
    }

    private async Task AssignDriverAsync(
        ApplicationDbContext dbContext,
        Vehicle vehicle,
        VehicleTelemetryDriverPayload driverPayload,
        CancellationToken cancellationToken)
    {
        var user = await ResolveDriverAsync(dbContext, driverPayload, cancellationToken);
        if (user is null)
        {
            return;
        }

        if (user.Role != UserRole.Driver)
        {
            _logger.LogDebug("Telemetry payload referenced user {UserId}, but the user is not a driver.", user.Id);
            return;
        }

        var vehicleDriverEntry = dbContext.Entry(vehicle).Reference(v => v.Driver);
        if (!vehicleDriverEntry.IsLoaded)
        {
            await vehicleDriverEntry.LoadAsync(cancellationToken);
        }

        if (vehicle.Driver?.Id == user.Id)
        {
            return;
        }

        if (vehicle.Driver is not null && vehicle.Driver.Id != user.Id)
        {
            vehicle.Driver.VehicleId = null;
            vehicle.Driver.Vehicle = null;
        }

        if (user.VehicleId.HasValue && user.VehicleId != vehicle.Id)
        {
            var previousVehicle = await dbContext.Vehicles
                .FirstOrDefaultAsync(v => v.Id == user.VehicleId.Value, cancellationToken);
            if (previousVehicle is not null)
            {
                previousVehicle.Driver = null;
                previousVehicle.UpdatedAt = DateTime.UtcNow;
            }
        }

        user.VehicleId = vehicle.Id;
        user.Vehicle = vehicle;
        vehicle.Driver = user;
        vehicle.UpdatedAt = DateTime.UtcNow;
    }

    private async Task EnsureDriverAssignmentAsync(
        ApplicationDbContext dbContext,
        Vehicle vehicle,
        CancellationToken cancellationToken)
    {
        var driverEntry = dbContext.Entry(vehicle).Reference(v => v.Driver);
        if (!driverEntry.IsLoaded)
        {
            await driverEntry.LoadAsync(cancellationToken);
        }

        if (vehicle.Driver is not null)
        {
            return;
        }

        var availableDriver = await dbContext.Users
            .Where(u => u.Role == UserRole.Driver && u.VehicleId == null)
            .OrderBy(u => u.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (availableDriver is null)
        {
            return;
        }

        availableDriver.VehicleId = vehicle.Id;
        availableDriver.Vehicle = vehicle;
        vehicle.Driver = availableDriver;
        vehicle.UpdatedAt = DateTime.UtcNow;
    }

    private async Task<User?> ResolveDriverAsync(
        ApplicationDbContext dbContext,
        VehicleTelemetryDriverPayload payload,
        CancellationToken cancellationToken)
    {
        if (payload.UserId is int userId && userId > 0)
        {
            var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (user is not null)
            {
                return user;
            }
        }

        var normalizedEmail = payload.Email?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(normalizedEmail))
        {
            var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);
            if (user is not null)
            {
                return user;
            }
        }

        return null;
    }

    private static double Clamp(double value, double min, double max)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return min;
        }
        return Math.Min(max, Math.Max(min, value));
    }

    private void NormalizeVehicleTelemetry(Vehicle vehicle)
    {
        vehicle.Latitude = Clamp(vehicle.Latitude, MinLatitude, MaxLatitude);
        vehicle.Longitude = Clamp(vehicle.Longitude, MinLongitude, MaxLongitude);
        vehicle.SpeedKmh = Clamp(vehicle.SpeedKmh, MinSpeedSaneKmh, MaxSpeedSaneKmh);
        vehicle.FuelLevelPercent = Clamp(vehicle.FuelLevelPercent, MinFuelPercent, MaxFuelPercent);
        vehicle.Progress = Clamp(vehicle.Progress, 0.0, 1.0);
        vehicle.DistanceRemainingM = Math.Max(0, vehicle.DistanceRemainingM);
        vehicle.DistanceTravelledM = Math.Max(0, vehicle.DistanceTravelledM);
        if (vehicle.FuelTankCapacity > 0 && vehicle.FuelLevelPercent >= 0)
        {
            vehicle.CurrentFuelLevel = Math.Max(0, vehicle.FuelTankCapacity * (vehicle.FuelLevelPercent / 100.0));
        }
    }

    private bool ShouldEmitAlert(int vehicleId, string type, DateTimeOffset nowUtc)
    {
        var perVehicle = _alertTracker.GetOrAdd(vehicleId, _ => new ConcurrentDictionary<string, DateTimeOffset>());
        if (perVehicle.TryGetValue(type, out var last) && nowUtc - last < AlertCooldown)
        {
            return false;
        }
        perVehicle[type] = nowUtc;
        return true;
    }

    private async Task EmitAlertsAsync(Vehicle vehicle, DateTimeOffset telemetryTimestamp, CancellationToken cancellationToken)
    {
        var alerts = new List<VehicleAlertDto>();
        var fuelFamily = string.Equals(vehicle.FuelType, "electric", StringComparison.OrdinalIgnoreCase)
            ? "electric"
            : "diesel";
        var lowFuelThreshold = fuelFamily == "electric" ? 20.0 : 15.0;

        if (vehicle.FuelLevelPercent <= lowFuelThreshold && ShouldEmitAlert(vehicle.Id, "fuel-low", telemetryTimestamp))
        {
            alerts.Add(new VehicleAlertDto
            {
                VehicleId = vehicle.Id,
                LicensePlate = vehicle.LicensePlate,
                Type = "fuel-low",
                Description = $"{Math.Round(vehicle.FuelLevelPercent, 0)}% {(fuelFamily == "electric" ? "battery" : "fuel")} remaining",
                RaisedAtUtc = telemetryTimestamp
            });
        }

        var baseSpeedThreshold = vehicle.BaseSpeedKmh > 0 ? vehicle.BaseSpeedKmh + 10.0 : 0.0;
        var speedingThreshold = Math.Max(90.0, baseSpeedThreshold);
        if (vehicle.SpeedKmh > speedingThreshold && ShouldEmitAlert(vehicle.Id, "speeding", telemetryTimestamp))
        {
            alerts.Add(new VehicleAlertDto
            {
                VehicleId = vehicle.Id,
                LicensePlate = vehicle.LicensePlate,
                Type = "speeding",
                Description = $"High speed: {Math.Round(vehicle.SpeedKmh, 0)} km/h",
                RaisedAtUtc = telemetryTimestamp
            });
        }

        var isOffline = string.Equals(vehicle.Status, "offline", StringComparison.OrdinalIgnoreCase)
                        || telemetryTimestamp < DateTimeOffset.UtcNow - TimeSpan.FromMinutes(5);
        if (isOffline && ShouldEmitAlert(vehicle.Id, "offline", telemetryTimestamp))
        {
            alerts.Add(new VehicleAlertDto
            {
                VehicleId = vehicle.Id,
                LicensePlate = vehicle.LicensePlate,
                Type = "offline",
                Description = "No telemetry in the last 5 minutes",
                RaisedAtUtc = telemetryTimestamp
            });
        }

        foreach (var alert in alerts)
        {
            try
            {
                await _vehicleHubContext.Clients.All.SendAsync(VehicleHub.VehicleAlertMethod, alert, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to broadcast alert {AlertType} for vehicle {VehicleId}", alert.Type, alert.VehicleId);
            }
        }
    }

    private async Task BroadcastVehicleUpdateAsync(ApplicationDbContext dbContext, Vehicle vehicle, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            var driverEntry = dbContext.Entry(vehicle).Reference(v => v.Driver);
            if (!driverEntry.IsLoaded)
            {
                await driverEntry.LoadAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to load driver relationship for vehicle {VehicleId}", vehicle.Id);
        }

        try
        {
            var dto = vehicle.ToVehicleDto();
            var alertTimestamp = vehicle.LastTelemetryAtUtc.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(vehicle.LastTelemetryAtUtc.Value, DateTimeKind.Utc))
                : DateTimeOffset.UtcNow;
            await EmitAlertsAsync(vehicle, alertTimestamp, cancellationToken);
            NormalizeVehicleTelemetry(vehicle);
            await _vehicleHubContext.Clients.All.SendAsync(VehicleHub.VehicleUpdatedMethod, dto, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Ignore cancellations triggered by shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast telemetry update for vehicle {VehicleId}", vehicle.Id);
        }
    }

    private async Task HandleMessageAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        var rawJson = args.ApplicationMessage?.ConvertPayloadToString();

        if (string.IsNullOrWhiteSpace(rawJson))
        {
            _logger.LogWarning("Received telemetry message without payload, dropping.");
            return;
        }

        VehicleTelemetryPayload? payload;

        try
        {
            payload = JsonSerializer.Deserialize<VehicleTelemetryPayload>(rawJson, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse telemetry payload: {PayloadSnippet}", rawJson[..Math.Min(rawJson.Length, 128)]);
            return;
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.TelemetryId))
        {
            _logger.LogWarning("Telemetry payload missing telemetry_id. Payload dropped.");
            return;
        }

        var envelope = new TelemetryEnvelope(payload, DateTime.UtcNow);

        try
        {
            await _telemetryChannel.Writer.WriteAsync(envelope, CancellationToken.None);
        }
        catch (ChannelClosedException)
        {
            _logger.LogWarning("Telemetry channel closed. Dropping telemetry with id {TelemetryId}.", payload.TelemetryId);
        }
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

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    private bool HasInvalidCoreTelemetry(TelemetryEnvelope envelope, out string reason)
    {
        var payload = envelope.Payload;
        if (payload.Position is null || !IsFinite(payload.Position.Lat) || !IsFinite(payload.Position.Lon))
        {
            reason = "position";
            return true;
        }

        if (!IsFinite(payload.SpeedKmh))
        {
            reason = "speed";
            return true;
        }

        if (!IsFinite(payload.FuelLevel) || !IsFinite(payload.FuelLevelPercent))
        {
            reason = "fuel";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private sealed record TelemetryEnvelope(VehicleTelemetryPayload Payload, DateTime ReceivedAtUtc);
}
