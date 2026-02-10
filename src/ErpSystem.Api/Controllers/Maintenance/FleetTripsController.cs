using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/fleet/trips")]
[Authorize(Policy = "MaintenanceAccess")]
public class FleetTripsController : ControllerBase
{
    private readonly IFleetTripService _fleetTripService;
    private readonly ILogger<FleetTripsController> _logger;

    public FleetTripsController(IFleetTripService fleetTripService, ILogger<FleetTripsController> logger)
    {
        _fleetTripService = fleetTripService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<FleetTripDto>>> GetTrips(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? vehicleAssetId = null)
    {
        try
        {
            var result = await _fleetTripService.GetTripsPagedAsync(page, pageSize, searchTerm, status, vehicleAssetId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fleet trips");
            return StatusCode(500, "An error occurred while retrieving fleet trips");
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FleetTripDto>> GetTrip(Guid id)
    {
        try
        {
            var trip = await _fleetTripService.GetTripByIdAsync(id);
            if (trip == null) return NotFound($"Trip with ID {id} not found");
            return Ok(trip);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fleet trip {TripId}", id);
            return StatusCode(500, "An error occurred while retrieving the trip");
        }
    }

    [HttpPost]
    public async Task<ActionResult<FleetTripDto>> Create([FromBody] CreateFleetTripDto dto)
    {
        try
        {
            var created = await _fleetTripService.CreateTripAsync(dto);
            return CreatedAtAction(nameof(GetTrip), new { id = created.Id }, created);
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
            _logger.LogError(ex, "Error creating fleet trip");
            return StatusCode(500, "An error occurred while creating the trip");
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<FleetTripDto>> Update(Guid id, [FromBody] UpdateFleetTripDto dto)
    {
        try
        {
            var updated = await _fleetTripService.UpdateTripAsync(id, dto);
            return Ok(updated);
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
            _logger.LogError(ex, "Error updating fleet trip {TripId}", id);
            return StatusCode(500, "An error occurred while updating the trip");
        }
    }

    [HttpPost("{id:guid}/submit-for-approval")]
    public async Task<ActionResult> SubmitForApproval(Guid id)
    {
        try
        {
            await _fleetTripService.SubmitForApprovalAsync(id);
            return Ok(new { success = true });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting fleet trip {TripId} for approval", id);
            return StatusCode(500, "An error occurred while submitting the trip for approval");
        }
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult> Approve(Guid id, [FromBody] ApprovalRequest? request)
    {
        try
        {
            await _fleetTripService.ApproveAsync(id, request?.Comments);
            return Ok(new { success = true });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving fleet trip {TripId}", id);
            return StatusCode(500, "An error occurred while approving the trip");
        }
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult> Reject(Guid id, [FromBody] RejectRequest request)
    {
        try
        {
            await _fleetTripService.RejectAsync(id, request?.Reason ?? "Rejected", request?.Comments);
            return Ok(new { success = true });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting fleet trip {TripId}", id);
            return StatusCode(500, "An error occurred while rejecting the trip");
        }
    }

    [HttpPost("{id:guid}/dispatch")]
    public async Task<ActionResult<FleetTripDto>> Dispatch(Guid id, [FromBody] DispatchFleetTripDto dto)
    {
        try
        {
            var updated = await _fleetTripService.DispatchAsync(id, dto);
            return Ok(updated);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dispatching fleet trip {TripId}", id);
            return StatusCode(500, "An error occurred while dispatching the trip");
        }
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<FleetTripDto>> Complete(Guid id, [FromBody] CompleteFleetTripDto dto)
    {
        try
        {
            var updated = await _fleetTripService.CompleteAsync(id, dto);
            return Ok(updated);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing fleet trip {TripId}", id);
            return StatusCode(500, "An error occurred while completing the trip");
        }
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult> Cancel(Guid id, [FromBody] CancelRequest request)
    {
        try
        {
            await _fleetTripService.CancelAsync(id, request?.Reason ?? string.Empty);
            return Ok(new { success = true });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling fleet trip {TripId}", id);
            return StatusCode(500, "An error occurred while cancelling the trip");
        }
    }

    public sealed record ApprovalRequest(string? Comments);
    public sealed record RejectRequest(string? Reason, string? Comments);
    public sealed record CancelRequest(string? Reason);
}
