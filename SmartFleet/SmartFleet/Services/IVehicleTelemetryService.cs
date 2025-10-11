using SmartFleet.Dtos;
using SmartFleet.Models;
using SmartFleet.Telemetry;

namespace SmartFleet.Services;

public interface IVehicleTelemetryService
{
    Task<VehicleState> ProcessTelemetryAsync(string rawPayload, VehicleTelemetryPayload payload, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<VehicleState>> GetLatestStatesAsync(CancellationToken cancellationToken);

    Task<VehicleState?> GetStateByVehicleIdAsync(int vehicleId, CancellationToken cancellationToken);
}
