using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/work-orders")]
[Authorize]
public class WorkOrderLaborController : ControllerBase
{
    private readonly IWorkOrderLaborService _laborService;
    private readonly ILogger<WorkOrderLaborController> _logger;

    public WorkOrderLaborController(
        IWorkOrderLaborService laborService,
        ILogger<WorkOrderLaborController> logger)
    {
        _laborService = laborService;
        _logger = logger;
    }

    /// <summary>
    /// Get all labor records for a work order
    /// </summary>
    [HttpGet("{workOrderId:guid}/labor")]
    public async Task<ActionResult<IEnumerable<WorkOrderLaborDto>>> GetLaborByWorkOrder(Guid workOrderId)
    {
        try
        {
            var labor = await _laborService.GetLaborByWorkOrderAsync(workOrderId);
            return Ok(labor);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting labor for work order {WorkOrderId}", workOrderId);
            return StatusCode(500, "Error retrieving labor records");
        }
    }

    /// <summary>
    /// Start a new labor entry for a work order
    /// </summary>
    [HttpPost("labor")]
    public async Task<ActionResult<WorkOrderLaborDto>> StartLabor([FromBody] CreateWorkOrderLaborDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var labor = await _laborService.StartLaborAsync(createDto);
            return CreatedAtAction(nameof(GetLaborByWorkOrder), new { workOrderId = labor.WorkOrderId }, labor);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting labor for work order {WorkOrderId}", createDto.WorkOrderId);
            return StatusCode(500, "Error starting labor record");
        }
    }

    /// <summary>
    /// End a labor entry
    /// </summary>
    [HttpPost("labor/{id:guid}/end")]
    public async Task<ActionResult<WorkOrderLaborDto>> EndLabor(Guid id, [FromBody] EndLaborDto endDto)
    {
        try
        {
            var labor = await _laborService.EndLaborAsync(id, endDto.EndTime, endDto.Notes);
            return Ok(labor);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ending labor {LaborId}", id);
            return StatusCode(500, "Error ending labor record");
        }
    }

    /// <summary>
    /// Update a labor entry
    /// </summary>
    [HttpPut("labor/{id:guid}")]
    public async Task<ActionResult<WorkOrderLaborDto>> UpdateLabor(Guid id, [FromBody] UpdateWorkOrderLaborDto updateDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var labor = await _laborService.UpdateLaborAsync(id, updateDto);
            return Ok(labor);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating labor {LaborId}", id);
            return StatusCode(500, "Error updating labor record");
        }
    }

    /// <summary>
    /// Delete a labor entry
    /// </summary>
    [HttpDelete("labor/{id:guid}")]
    public async Task<ActionResult> DeleteLabor(Guid id)
    {
        try
        {
            await _laborService.DeleteLaborAsync(id);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting labor {LaborId}", id);
            return StatusCode(500, "Error deleting labor record");
        }
    }

    /// <summary>
    /// Get total labor cost for a work order
    /// </summary>
    [HttpGet("{workOrderId:guid}/labor/total-cost")]
    public async Task<ActionResult<decimal>> GetTotalLaborCost(Guid workOrderId)
    {
        try
        {
            var totalCost = await _laborService.GetTotalLaborCostAsync(workOrderId);
            return Ok(totalCost);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting total labor cost for work order {WorkOrderId}", workOrderId);
            return StatusCode(500, "Error retrieving total labor cost");
        }
    }
}

