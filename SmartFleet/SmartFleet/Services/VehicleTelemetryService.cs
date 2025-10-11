using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartFleet.Data;
using SmartFleet.Dtos;
using SmartFleet.Hubs;
using SmartFleet.Models;
using SmartFleet.Telemetry;

namespace SmartFleet.Services;

public class VehicleTelemetryService(
    ApplicationDbContext context,
    ILogger<VehicleTelemetryService> logger,
    IHubContext<TelemetryHub> telemetryHub,
    ITelemetryAnalyticsQueue analyticsQueue,
    ITelemetryDeadLetterSink deadLetterSink) : IVehicleTelemetryService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ApplicationDbContext _context = context;
    private readonly ILogger<VehicleTelemetryService> _logger = logger;
    private readonly IHubContext<TelemetryHub> _telemetryHub = telemetryHub;
    private readonly ITelemetryAnalyticsQueue _analyticsQueue = analyticsQueue;
    private readonly ITelemetryDeadLetterSink _deadLetterSink = deadLetterSink;

    public async Task<VehicleState> ProcessTelemetryAsync(string rawPayload, VehicleTelemetryPayload payload, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var normalizedPlate = payload.NumberPlate.Trim().ToUpperInvariant();
        var vehicleCode = payload.VehicleId.Trim();

        var vehicle = await _context.Vehicles
            .FirstOrDefaultAsync(v => v.LicensePlate == normalizedPlate, cancellationToken);

        if (vehicle is null)
        {
            vehicle = new Vehicle
            {
                LicensePlate = normalizedPlate,
                VehicleType = "Simulator Truck",
                FuelType = payload.FuelType,
                FuelTankCapacity = payload.FuelCapacity,
                CurrentFuelLevel = payload.FuelLevel,
                KilometersDriven = payload.OdometerKm,
                CO2Emission = payload.Co2EmissionKg,
                BodyType = "Truck",
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.Vehicles.Add(vehicle);
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Provisioned vehicle {LicensePlate} from telemetry stream.", normalizedPlate);
        }
        else
        {
            vehicle.FuelType = payload.FuelType;
            vehicle.FuelTankCapacity = payload.FuelCapacity;
            vehicle.CurrentFuelLevel = payload.FuelLevel;
            vehicle.KilometersDriven = payload.OdometerKm;
            vehicle.CO2Emission = payload.Co2EmissionKg;
            vehicle.UpdatedAt = now;
        }

        var rawMessage = new VehicleTelemetryRawMessage
        {
            TelemetryId = payload.TelemetryId,
            VehicleCode = vehicleCode,
            PayloadJson = rawPayload,
            ReceivedAtUtc = now
        };
        _context.VehicleTelemetryRawMessages.Add(rawMessage);

        var stopsJson = JsonSerializer.Serialize(payload.Stops, SerializerOptions);

        var state = await _context.VehicleStates
            .FirstOrDefaultAsync(s => s.VehicleId == vehicle.Id, cancellationToken);

        if (state is null)
        {
            state = new VehicleState
            {
                TelemetryId = payload.TelemetryId,
                VehicleId = vehicle.Id,
                VehicleCode = vehicleCode,
                NumberPlate = normalizedPlate,
                FuelType = payload.FuelType,
                FuelUnit = payload.FuelUnit,
                FuelCapacity = payload.FuelCapacity,
                FuelLevel = payload.FuelLevel,
                FuelLevelPercent = payload.FuelLevelPercent,
                FuelConsumptionPer100Km = payload.FuelConsumptionPer100Km,
                OdometerKm = payload.OdometerKm,
                Co2EmissionKg = payload.Co2EmissionKg,
                RouteId = payload.RouteId,
                RouteSummary = payload.RouteSummary,
                RouteDistanceKm = payload.RouteDistanceKm,
                BaseSpeedKmh = payload.BaseSpeedKmh,
                TimestampUtc = payload.TimestampUtc,
                Status = payload.Status,
                Latitude = payload.Position.Latitude,
                Longitude = payload.Position.Longitude,
                SpeedKmh = payload.SpeedKmh,
                HeadingDeg = payload.HeadingDeg,
                DistanceTravelledM = payload.DistanceTravelledM,
                DistanceRemainingM = payload.DistanceRemainingM,
                Progress = payload.Progress,
                EtaSeconds = payload.EtaSeconds,
                StopsJson = stopsJson,
                UpdatedAtUtc = now
            };

            _context.VehicleStates.Add(state);
        }
        else
        {
            state.TelemetryId = payload.TelemetryId;
            state.VehicleCode = vehicleCode;
            state.NumberPlate = normalizedPlate;
            state.FuelType = payload.FuelType;
            state.FuelUnit = payload.FuelUnit;
            state.FuelCapacity = payload.FuelCapacity;
            state.FuelLevel = payload.FuelLevel;
            state.FuelLevelPercent = payload.FuelLevelPercent;
            state.FuelConsumptionPer100Km = payload.FuelConsumptionPer100Km;
            state.OdometerKm = payload.OdometerKm;
            state.Co2EmissionKg = payload.Co2EmissionKg;
            state.RouteId = payload.RouteId;
            state.RouteSummary = payload.RouteSummary;
            state.RouteDistanceKm = payload.RouteDistanceKm;
            state.BaseSpeedKmh = payload.BaseSpeedKmh;
            state.TimestampUtc = payload.TimestampUtc;
            state.Status = payload.Status;
            state.Latitude = payload.Position.Latitude;
            state.Longitude = payload.Position.Longitude;
            state.SpeedKmh = payload.SpeedKmh;
            state.HeadingDeg = payload.HeadingDeg;
            state.DistanceTravelledM = payload.DistanceTravelledM;
            state.DistanceRemainingM = payload.DistanceRemainingM;
            state.Progress = payload.Progress;
            state.EtaSeconds = payload.EtaSeconds;
            state.StopsJson = stopsJson;
            state.UpdatedAtUtc = now;
        }

        await _context.SaveChangesAsync(cancellationToken);

        await PublishAsync(state, rawPayload, cancellationToken);
        await QueueAnalyticsAsync(state, rawPayload, cancellationToken);

        return state;
    }

    public async Task<IReadOnlyCollection<VehicleState>> GetLatestStatesAsync(CancellationToken cancellationToken)
    {
        return await _context.VehicleStates
            .AsNoTracking()
            .OrderBy(s => s.VehicleId)
            .ToListAsync(cancellationToken);
    }

    public async Task<VehicleState?> GetStateByVehicleIdAsync(int vehicleId, CancellationToken cancellationToken)
    {
        return await _context.VehicleStates
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.VehicleId == vehicleId, cancellationToken);
    }

    private async Task PublishAsync(VehicleState state, string rawPayload, CancellationToken cancellationToken)
    {
        try
        {
            var dto = state.ToDto();
            await _telemetryHub.Clients.All.SendAsync("telemetryUpdated", dto, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed broadcasting telemetry update for vehicle {VehicleId}.", state.VehicleId);
            await _deadLetterSink.StoreAsync($"SignalR publish failed: {ex.Message}", rawPayload, cancellationToken);
        }
    }

    private async Task QueueAnalyticsAsync(VehicleState state, string rawPayload, CancellationToken cancellationToken)
    {
        try
        {
            await _analyticsQueue.EnqueueAsync(state, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to enqueue analytics job for vehicle {VehicleId}.", state.VehicleId);
            await _deadLetterSink.StoreAsync($"Analytics enqueue failed: {ex.Message}", rawPayload, cancellationToken);
        }
    }
}
