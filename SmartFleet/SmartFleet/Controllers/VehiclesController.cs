using System.Linq;
using Microsoft.AspNetCore.Mvc;
using SmartFleet.Dtos;
using SmartFleet.Services;

namespace SmartFleet.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VehiclesController(IVehicleService vehicleService) : ControllerBase
{
    private readonly IVehicleService _vehicleService = vehicleService;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<VehicleDto>>> GetVehicles(CancellationToken cancellationToken)
    {
        var vehicles = await _vehicleService.GetAllAsync(cancellationToken);
        return Ok(vehicles.Select(v => v.ToVehicleDto()));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<VehicleDto>> GetVehicleById(int id, CancellationToken cancellationToken)
    {
        var vehicle = await _vehicleService.GetByIdAsync(id, cancellationToken);

        if (vehicle is null)
        {
            return NotFound();
        }

        return Ok(vehicle.ToVehicleDto());
    }

    [HttpPost]
    public async Task<ActionResult<VehicleDto>> CreateVehicle([FromBody] VehicleCreateDto request, CancellationToken cancellationToken)
    {
        if (request.CurrentFuelLevel > request.FuelTankCapacity)
        {
            ModelState.AddModelError(nameof(request.CurrentFuelLevel), "Current fuel level cannot exceed the tank capacity.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var vehicle = await _vehicleService.CreateAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetVehicleById), new { id = vehicle.Id }, vehicle.ToVehicleDto());
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<VehicleDto>> UpdateVehicle(int id, [FromBody] VehicleUpdateDto request, CancellationToken cancellationToken)
    {
        if (request.CurrentFuelLevel > request.FuelTankCapacity)
        {
            ModelState.AddModelError(nameof(request.CurrentFuelLevel), "Current fuel level cannot exceed the tank capacity.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var vehicle = await _vehicleService.UpdateAsync(id, request, cancellationToken);

        if (vehicle is null)
        {
            return NotFound();
        }

        return Ok(vehicle.ToVehicleDto());
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteVehicle(int id, CancellationToken cancellationToken)
    {
        var deleted = await _vehicleService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPost("{vehicleId:int}/driver/{userId:int}")]
    public async Task<ActionResult<VehicleDto>> AssignDriver(int vehicleId, int userId, CancellationToken cancellationToken)
    {
        var result = await _vehicleService.AssignDriverAsync(vehicleId, userId, cancellationToken);

        return result.Status switch
        {
            VehicleDriverAssignmentStatus.Success => Ok(result.Vehicle!.ToVehicleDto()),
            VehicleDriverAssignmentStatus.VehicleNotFound => NotFound(),
            VehicleDriverAssignmentStatus.UserNotFound => NotFound($"User with id {userId} was not found."),
            VehicleDriverAssignmentStatus.UserNotDriver => BadRequest($"User with id {userId} does not have the Driver role."),
            VehicleDriverAssignmentStatus.DriverAlreadyAssigned => Conflict($"Driver with id {userId} is already assigned to another vehicle."),
            _ => Problem("Unexpected assignment status.")
        };
    }

    [HttpDelete("{vehicleId:int}/driver")]
    public async Task<IActionResult> RemoveDriver(int vehicleId, CancellationToken cancellationToken)
    {
        var result = await _vehicleService.RemoveDriverAsync(vehicleId, cancellationToken);

        return result.Status switch
        {
            VehicleDriverRemovalStatus.Success => NoContent(),
            VehicleDriverRemovalStatus.VehicleNotFound => NotFound(),
            VehicleDriverRemovalStatus.NoDriverAssigned => NoContent(),
            _ => Problem("Unexpected removal status.")
        };
    }
}
