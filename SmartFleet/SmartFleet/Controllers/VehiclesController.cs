using System;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SmartFleet.Dtos;
using SmartFleet.Models;
using SmartFleet.Models.Auth;
using SmartFleet.Services;
using SmartFleet.Hubs;

namespace SmartFleet.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VehiclesController(
    IVehicleService vehicleService,
    IVehicleCommandPublisher vehicleCommandPublisher,
    IHubContext<VehicleHub> vehicleHubContext) : ControllerBase
{
    private readonly IVehicleService _vehicleService = vehicleService;
    private readonly IVehicleCommandPublisher _vehicleCommandPublisher = vehicleCommandPublisher;
    private readonly IHubContext<VehicleHub> _vehicleHubContext = vehicleHubContext;

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

    [HttpGet("export")]
    [Authorize(Policy = "MapsAccess")]
    public async Task<IActionResult> ExportVehicles([FromQuery] string? status, [FromQuery] string? query, CancellationToken cancellationToken)
    {
        if (User.IsInRole(UserRole.Driver.ToString()))
        {
            return Forbid();
        }

        var vehicles = await _vehicleService.GetAllAsync(cancellationToken);

        var filtered = vehicles.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim().ToLowerInvariant();
            filtered = filtered.Where(v => (v.Status ?? string.Empty).Trim().ToLowerInvariant() == normalizedStatus);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var normalizedQuery = query.Trim().ToLowerInvariant();
            filtered = filtered.Where(v =>
                v.LicensePlate.ToLowerInvariant().Contains(normalizedQuery) ||
                (!string.IsNullOrWhiteSpace(v.RouteSummary) && v.RouteSummary!.ToLowerInvariant().Contains(normalizedQuery)) ||
                (!string.IsNullOrWhiteSpace(v.Brand) && v.Brand!.ToLowerInvariant().Contains(normalizedQuery)));
        }

        var csv = BuildVehicleCsv(filtered);
        var fileName = $"fleet-export-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
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

    [HttpPost("{id:int}/route")]
    [Authorize(Policy = "MapsAccess")]
    public async Task<IActionResult> UpdateVehicleRoute(
        int id,
        [FromBody] VehicleRouteUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int? userId = int.TryParse(userIdValue, out var parsedUserId) ? parsedUserId : null;
        var isDriver = User.IsInRole(UserRole.Driver.ToString());

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (request.Stops is null || request.Stops.Count < 2)
        {
            ModelState.AddModelError(nameof(request.Stops), "At least two stops are required to build a route.");
            return ValidationProblem(ModelState);
        }

        var vehicle = await _vehicleService.GetByIdAsync(id, cancellationToken);
        if (vehicle is null)
        {
            return NotFound();
        }

        if (isDriver && (!userId.HasValue || vehicle.Driver?.Id != userId.Value))
        {
            return Forbid();
        }

        var externalId = string.IsNullOrWhiteSpace(vehicle.ExternalId)
            ? null
            : vehicle.ExternalId.Trim();

        if (string.IsNullOrWhiteSpace(externalId))
        {
            return BadRequest("Vehicle does not have an external identifier needed to control the simulator.");
        }

        var stops = request.Stops
            .Select((stop, index) => new VehicleRouteCommandStop(
                string.IsNullOrWhiteSpace(stop.Name) ? $"Stop {index + 1}" : stop.Name.Trim(),
                stop.Latitude,
                stop.Longitude))
            .ToList();

        var requestId = Guid.NewGuid().ToString("N");

        VehicleRouteCommandRequester? requester = null;
        var displayName = User.Identity?.Name;
        var email = User.FindFirstValue(ClaimTypes.Email);

        if (userId.HasValue || !string.IsNullOrWhiteSpace(displayName) || !string.IsNullOrWhiteSpace(email))
        {
            requester = new VehicleRouteCommandRequester(userId, displayName, email);
        }

        var payload = new VehicleRouteCommandPayload(externalId, stops)
        {
            LicensePlate = vehicle.LicensePlate,
            RouteLabel = string.IsNullOrWhiteSpace(request.RouteLabel) ? null : request.RouteLabel.Trim(),
            BaseSpeedKmh = request.BaseSpeedKmh,
            RequestId = requestId,
            RequestedBy = requester
        };

        await _vehicleCommandPublisher.PublishRouteUpdateAsync(payload, cancellationToken);
        await _vehicleService.ApplyRoutePreviewAsync(
            vehicle.Id,
            stops,
            request.BaseSpeedKmh,
            payload.RouteLabel,
            requestId,
            cancellationToken);

        try
        {
            await _vehicleHubContext.Clients.All.SendAsync(
                VehicleHub.RouteUpdatedMethod,
                new
                {
                    vehicleId = vehicle.Id,
                    routeId = requestId,
                    routeSummary = BuildRoutePreviewSummary(stops, payload.RouteLabel, vehicle.RouteSummary),
                    licensePlate = vehicle.LicensePlate,
                    stops = stops.Select(stop => new
                    {
                        stop.Name,
                        Latitude = stop.Latitude,
                        Longitude = stop.Longitude
                    }).ToList()
                },
                cancellationToken);
        }
        catch
        {
            // best-effort notification; do not fail the request on hub issues
        }

        return Accepted(new
        {
            requestId,
            vehicleId = vehicle.Id,
            externalId,
            stops = stops.Count
        });
    }

    private static string BuildRoutePreviewSummary(
        IReadOnlyList<VehicleRouteCommandStop> stops,
        string? routeLabel,
        string? fallbackSummary)
    {
        if (!string.IsNullOrWhiteSpace(routeLabel))
        {
            return routeLabel.Trim();
        }

        var inferred = string.Join(" -> ",
            stops
                .Select(stop => stop.Name?.Trim())
                .Where(name => !string.IsNullOrWhiteSpace(name)));

        if (!string.IsNullOrWhiteSpace(inferred))
        {
            return inferred;
        }

        return fallbackSummary ?? string.Empty;
    }

    private static string BuildVehicleCsv(IEnumerable<Vehicle> vehicles)
    {
        var culture = CultureInfo.InvariantCulture;
        var header = "licensePlate,status,routeSummary,fuelPercent,progressPercent,driver,etaMinutes,co2Kg";
        var lines = vehicles.Select(v =>
        {
            var driver = v.Driver is null ? string.Empty : $"{v.Driver.FirstName} {v.Driver.LastName}".Trim();
            var etaMinutes = v.EtaSeconds > 0 ? Math.Round(v.EtaSeconds / 60.0, 0) : 0;
            var co2 = v.CO2Emission;

            string Escape(string value) => value.Contains(',') || value.Contains('"')
                ? $"\"{value.Replace("\"", "\"\"")}\""
                : value;

            return string.Join(",",
                Escape(v.LicensePlate ?? string.Empty),
                Escape((v.Status ?? string.Empty).Trim()),
                Escape(v.RouteSummary ?? string.Empty),
                Math.Round(v.FuelLevelPercent, 1).ToString(culture),
                Math.Round(v.Progress * 100, 1).ToString(culture),
                Escape(driver),
                etaMinutes.ToString(culture),
                Math.Round(co2, 2).ToString(culture));
        });

        var builder = new StringBuilder();
        builder.AppendLine(header);
        foreach (var line in lines)
        {
            builder.AppendLine(line);
        }
        return builder.ToString();
    }
}
