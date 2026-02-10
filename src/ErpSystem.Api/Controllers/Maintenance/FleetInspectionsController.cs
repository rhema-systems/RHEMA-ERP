using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/fleet/inspections")]
[Authorize(Policy = "MaintenanceAccess")]
public class FleetInspectionsController : ControllerBase
{
    private readonly IFleetInspectionService _fleetInspectionService;
    private readonly ILogger<FleetInspectionsController> _logger;

    public FleetInspectionsController(IFleetInspectionService fleetInspectionService, ILogger<FleetInspectionsController> logger)
    {
        _fleetInspectionService = fleetInspectionService;
        _logger = logger;
    }

    [HttpGet("trip/{tripId:guid}")]
    public async Task<ActionResult<IEnumerable<FleetTripInspectionDto>>> GetForTrip(Guid tripId)
    {
        try
        {
            var items = await _fleetInspectionService.GetTripInspectionsAsync(tripId);
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fleet inspections for trip {TripId}", tripId);
            return StatusCode(500, "An error occurred while retrieving inspections");
        }
    }

    [HttpPost("start")]
    public async Task<ActionResult<FleetTripInspectionDto>> Start([FromBody] StartFleetTripInspectionDto dto)
    {
        try
        {
            var created = await _fleetInspectionService.StartAsync(dto);
            return Ok(created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting fleet inspection");
            return StatusCode(500, "An error occurred while starting the inspection");
        }
    }

    [HttpPost("{inspectionId:guid}/complete")]
    public async Task<ActionResult<FleetTripInspectionDto>> Complete(Guid inspectionId, [FromBody] CompleteFleetTripInspectionDto dto)
    {
        try
        {
            var updated = await _fleetInspectionService.CompleteAsync(inspectionId, dto);
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing fleet inspection {InspectionId}", inspectionId);
            return StatusCode(500, "An error occurred while completing the inspection");
        }
    }

    [HttpPost("{inspectionId:guid}/cancel")]
    public async Task<ActionResult> Cancel(Guid inspectionId, [FromQuery] string? notes = null)
    {
        try
        {
            var ok = await _fleetInspectionService.CancelAsync(inspectionId, notes);
            return ok ? Ok(new { success = true }) : NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling fleet inspection {InspectionId}", inspectionId);
            return StatusCode(500, "An error occurred while cancelling the inspection");
        }
    }
}

