using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartFleet.Data;
using SmartFleet.Models;

namespace SmartFleet.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VehiclesController(ApplicationDbContext context) : ControllerBase
{
    private readonly ApplicationDbContext _context = context;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Vehicle>>> GetVehicles(CancellationToken cancellationToken)
    {
        var vehicles = await _context.Vehicles
            .OrderByDescending(v => v.CreatedUtc)
            .ToListAsync(cancellationToken);

        return Ok(vehicles);
    }

    [HttpPost]
    public async Task<ActionResult<Vehicle>> CreateVehicle([FromBody] Vehicle request, CancellationToken cancellationToken)
    {
        request.CreatedUtc = DateTime.UtcNow;

        _context.Vehicles.Add(request);
        await _context.SaveChangesAsync(cancellationToken);

        return Created($"/api/vehicles/{request.Id}", request);
    }
}
