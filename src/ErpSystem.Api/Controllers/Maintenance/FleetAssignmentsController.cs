using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/fleet/assignments")]
[Authorize(Policy = "MaintenanceRead")]
public class FleetAssignmentsController : ControllerBase
{
    private readonly IFleetAssignmentService _fleetAssignmentService;
    private readonly ILogger<FleetAssignmentsController> _logger;

    public FleetAssignmentsController(IFleetAssignmentService fleetAssignmentService, ILogger<FleetAssignmentsController> logger)
    {
        _fleetAssignmentService = fleetAssignmentService;
        _logger = logger;
    }

    [HttpGet("vehicle/{vehicleAssetId:guid}")]
    public async Task<ActionResult<IEnumerable<FleetVehicleAssignmentDto>>> GetForVehicle(Guid vehicleAssetId)
    {
        try
        {
            var items = await _fleetAssignmentService.GetAssignmentsAsync(vehicleAssetId);
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fleet assignments for vehicle {VehicleAssetId}", vehicleAssetId);
            return StatusCode(500, "An error occurred while retrieving assignments");
        }
    }

    [HttpGet("vehicle/{vehicleAssetId:guid}/current")]
    public async Task<ActionResult<FleetVehicleAssignmentDto?>> GetCurrent(Guid vehicleAssetId)
    {
        try
        {
            var item = await _fleetAssignmentService.GetCurrentAssignmentAsync(vehicleAssetId);
            return Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current fleet assignment for vehicle {VehicleAssetId}", vehicleAssetId);
            return StatusCode(500, "An error occurred while retrieving the current assignment");
        }
    }

    [HttpPost]
    [Authorize(Policy = "MaintenanceWrite")]
    public async Task<ActionResult<FleetVehicleAssignmentDto>> Assign([FromBody] AssignFleetDriverDto dto)
    {
        try
        {
            var created = await _fleetAssignmentService.AssignDriverAsync(dto);
            return Ok(created);
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
            _logger.LogError(ex, "Error assigning fleet driver");
            return StatusCode(500, "An error occurred while assigning the driver");
        }
    }

    [HttpPost("{assignmentId:guid}/end")]
    [Authorize(Policy = "MaintenanceWrite")]
    public async Task<ActionResult> End(Guid assignmentId, [FromBody] EndFleetDriverAssignmentDto dto)
    {
        try
        {
            var ok = await _fleetAssignmentService.EndAssignmentAsync(assignmentId, dto);
            return ok ? Ok(new { success = true }) : NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ending fleet assignment {AssignmentId}", assignmentId);
            return StatusCode(500, "An error occurred while ending the assignment");
        }
    }
}

