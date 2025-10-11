using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SmartFleet.Hubs;

[Authorize]
public class TelemetryHub : Hub
{
}
