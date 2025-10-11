using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartFleet.Dtos;
using SmartFleet.Services;

namespace SmartFleet.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TelemetryController(IVehicleTelemetryService telemetryService) : ControllerBase
{
    private readonly IVehicleTelemetryService _telemetryService = telemetryService;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<VehicleTelemetryDto>>> GetAll(CancellationToken cancellationToken)
    {
        var states = await _telemetryService.GetLatestStatesAsync(cancellationToken);
        return Ok(states.Select(state => state.ToDto()));
    }

    [HttpGet("vehicles/{vehicleId:int}")]
    public async Task<ActionResult<VehicleTelemetryDto>> GetByVehicle(int vehicleId, CancellationToken cancellationToken)
    {
        var state = await _telemetryService.GetStateByVehicleIdAsync(vehicleId, cancellationToken);

        if (state is null)
        {
            return NotFound();
        }

        return Ok(state.ToDto());
    }
}
