using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/fleet/vehicles")]
[Authorize(Policy = "MaintenanceAccess")]
public class FleetVehiclesController : ControllerBase
{
    private readonly IFleetVehicleService _fleetVehicleService;
    private readonly ILogger<FleetVehiclesController> _logger;
    private readonly IHostEnvironment _environment;

    public FleetVehiclesController(IFleetVehicleService fleetVehicleService, ILogger<FleetVehiclesController> logger, IHostEnvironment environment)
    {
        _fleetVehicleService = fleetVehicleService;
        _logger = logger;
        _environment = environment;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<FleetVehicleListDto>>> GetVehicles(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? searchTerm = null,
        [FromQuery] Guid? categoryId = null)
    {
        try
        {
            var result = await _fleetVehicleService.GetVehiclesPagedAsync(page, pageSize, searchTerm, categoryId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fleet vehicles");

            var root = ex;
            while (root.InnerException != null) root = root.InnerException;

            if (root is SqlException sqlEx && sqlEx.Number == 208)
            {
                return Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Fleet vehicles database schema is missing.",
                    detail: "Run the latest EF Core migrations (dotnet ef database update) and restart the API.");
            }

            var detail = _environment.IsDevelopment()
                ? $"{root.GetType().Name}: {root.Message}"
                : "An error occurred while retrieving fleet vehicles.";

            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Error retrieving fleet vehicles", detail: detail);
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MaintenanceAssetDto>> GetVehicle(Guid id)
    {
        try
        {
            var vehicle = await _fleetVehicleService.GetVehicleByIdAsync(id);
            if (vehicle == null) return NotFound($"Vehicle with ID {id} not found");
            return Ok(vehicle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fleet vehicle {VehicleId}", id);
            return StatusCode(500, "An error occurred while retrieving the vehicle");
        }
    }

    [HttpPost]
    public async Task<ActionResult<MaintenanceAssetDto>> Create([FromBody] CreateFleetVehicleDto dto)
    {
        try
        {
            var created = await _fleetVehicleService.CreateVehicleAsync(dto);
            return CreatedAtAction(nameof(GetVehicle), new { id = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating fleet vehicle");
            return StatusCode(500, "An error occurred while creating the vehicle");
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MaintenanceAssetDto>> Update(Guid id, [FromBody] UpdateFleetVehicleDto dto)
    {
        try
        {
            var updated = await _fleetVehicleService.UpdateVehicleAsync(id, dto);
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating fleet vehicle {VehicleId}", id);
            return StatusCode(500, "An error occurred while updating the vehicle");
        }
    }
}

