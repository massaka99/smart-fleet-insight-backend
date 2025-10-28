using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SmartFleet.Hubs;

[Authorize(Policy = "MapsAccess")]
public class VehicleHub : Hub
{
    public const string VehicleUpdatedMethod = "VehicleUpdated";
}
