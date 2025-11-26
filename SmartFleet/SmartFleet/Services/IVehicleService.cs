using SmartFleet.Dtos;
using SmartFleet.Models;

namespace SmartFleet.Services;

public interface IVehicleService
{
    Task<IReadOnlyCollection<Vehicle>> GetAllAsync(CancellationToken cancellationToken);
    Task<Vehicle?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<Vehicle> CreateAsync(VehicleCreateDto dto, CancellationToken cancellationToken);
    Task<Vehicle?> UpdateAsync(int id, VehicleUpdateDto dto, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);
    Task<VehicleDriverAssignmentResult> AssignDriverAsync(int vehicleId, int userId, CancellationToken cancellationToken);
    Task<VehicleDriverRemovalResult> RemoveDriverAsync(int vehicleId, CancellationToken cancellationToken);
    Task ApplyRoutePreviewAsync(
        int vehicleId,
        IReadOnlyList<VehicleRouteCommandStop> stops,
        double? baseSpeedKmh,
        string? routeLabel,
        string? requestId,
        CancellationToken cancellationToken);
}

public enum VehicleDriverAssignmentStatus
{
    Success,
    VehicleNotFound,
    UserNotFound,
    UserNotDriver,
    DriverAlreadyAssigned
}

public record VehicleDriverAssignmentResult(VehicleDriverAssignmentStatus Status, Vehicle? Vehicle = null)
{
    public static VehicleDriverAssignmentResult VehicleNotFound() => new(VehicleDriverAssignmentStatus.VehicleNotFound);
    public static VehicleDriverAssignmentResult UserNotFound() => new(VehicleDriverAssignmentStatus.UserNotFound);
    public static VehicleDriverAssignmentResult UserNotDriver() => new(VehicleDriverAssignmentStatus.UserNotDriver);
    public static VehicleDriverAssignmentResult DriverAlreadyAssigned() => new(VehicleDriverAssignmentStatus.DriverAlreadyAssigned);
    public static VehicleDriverAssignmentResult Success(Vehicle vehicle) => new(VehicleDriverAssignmentStatus.Success, vehicle);
}

public enum VehicleDriverRemovalStatus
{
    Success,
    VehicleNotFound,
    NoDriverAssigned
}

public record VehicleDriverRemovalResult(VehicleDriverRemovalStatus Status)
{
    public static VehicleDriverRemovalResult VehicleNotFound() => new(VehicleDriverRemovalStatus.VehicleNotFound);
    public static VehicleDriverRemovalResult NoDriverAssigned() => new(VehicleDriverRemovalStatus.NoDriverAssigned);
    public static VehicleDriverRemovalResult Success() => new(VehicleDriverRemovalStatus.Success);
}
