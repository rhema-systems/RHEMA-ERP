using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/fleet/inspections")]
[Authorize(Policy = "MaintenanceRead")]
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

    [HttpGet("assets/{assetId:guid}")]
    public async Task<ActionResult<IEnumerable<FleetTripInspectionDto>>> GetForAsset(Guid assetId, [FromQuery] int take = 50)
    {
        try
        {
            return Ok(await _fleetInspectionService.GetAssetInspectionsAsync(assetId, take));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fleet inspections for asset {AssetId}", assetId);
            return StatusCode(500, "An error occurred while retrieving inspections");
        }
    }

    [HttpPost("assets/{assetId:guid}/submit")]
    [Authorize(Policy = "FleetInspectionWrite")]
    public async Task<ActionResult<FleetTripInspectionDto>> SubmitForAsset(Guid assetId, [FromBody] SubmitFleetAssetInspectionDto dto)
    {
        try
        {
            return Ok(await _fleetInspectionService.SubmitAssetInspectionAsync(assetId, dto));
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
            _logger.LogError(ex, "Error submitting pre-start inspection for asset {AssetId}", assetId);
            return StatusCode(500, "An error occurred while submitting the inspection");
        }
    }

    [HttpPost("start")]
    [Authorize(Policy = "FleetInspectionWrite")]
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
    [Authorize(Policy = "FleetInspectionWrite")]
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
    [Authorize(Policy = "FleetInspectionWrite")]
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

    [HttpPost("{inspectionId:guid}/submit-approval")]
    [Authorize(Policy = "FleetInspectionWrite")]
    public async Task<ActionResult<FleetTripInspectionDto>> SubmitApproval(Guid inspectionId)
    {
        try
        {
            return Ok(await _fleetInspectionService.SubmitForApprovalAsync(inspectionId));
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("{inspectionId:guid}/approve")]
    [Authorize(Policy = "FleetInspectionWrite")]
    public async Task<ActionResult<FleetTripInspectionDto>> Approve(Guid inspectionId, [FromBody] WorkflowCommentDto? dto)
    {
        try
        {
            return Ok(await _fleetInspectionService.ApproveAsync(inspectionId, dto?.Comments));
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, ex.Message); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("{inspectionId:guid}/reject")]
    [Authorize(Policy = "FleetInspectionWrite")]
    public async Task<ActionResult<FleetTripInspectionDto>> Reject(Guid inspectionId, [FromBody] WorkflowCommentDto? dto)
    {
        try
        {
            return Ok(await _fleetInspectionService.RejectAsync(inspectionId, dto?.Comments));
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, ex.Message); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    public sealed class WorkflowCommentDto
    {
        public string? Comments { get; set; }
    }
}

