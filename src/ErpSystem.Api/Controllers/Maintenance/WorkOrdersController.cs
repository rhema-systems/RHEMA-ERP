using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Maintenance;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/work-orders")]
[Authorize]
public class WorkOrdersController : ControllerBase
{
    private readonly IWorkOrderService _workOrderService;
    private readonly ILogger<WorkOrdersController> _logger;

    public WorkOrdersController(
        IWorkOrderService workOrderService,
        ILogger<WorkOrdersController> logger)
    {
        _workOrderService = workOrderService;
        _logger = logger;
    }

    /// <summary>
    /// Gets a paginated list of work orders with optional filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<WorkOrderListDto>>> GetWorkOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? status = null,
        [FromQuery] string? priority = null,
        [FromQuery] Guid? assetId = null,
        [FromQuery] Guid? technicianId = null,
        [FromQuery] DateTime? scheduledFrom = null,
        [FromQuery] DateTime? scheduledTo = null)
    {
        try
        {
            if (pageSize > 100)
                pageSize = 100;
            
            // Create filter DTO
            var filter = new WorkOrderFilterDto
            {
                SearchTerm = searchTerm,
                Status = string.IsNullOrEmpty(status) ? null : status,
                AssetId = assetId,
                AssignedTechnicianId = technicianId,
                StartDate = scheduledFrom,
                EndDate = scheduledTo,
                Page = page,
                PageSize = pageSize
            };

            // Get work orders from service
            var result = await _workOrderService.GetWorkOrdersPagedAsync(filter);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving work orders");
            return StatusCode(500, "An error occurred while retrieving work orders");
        }
    }

    /// <summary>
    /// Gets a specific work order by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkOrderDto>> GetWorkOrder(Guid id)
    {
        try
        {
            var workOrder = await _workOrderService.GetWorkOrderByIdAsync(id);
            if (workOrder == null)
                return NotFound($"Work order with ID {id} not found");

            return Ok(workOrder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error retrieving work order with ID {id}");
            return StatusCode(500, $"An error occurred while retrieving work order with ID {id}");
        }
    }

    /// <summary>
    /// Creates a new work order
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<WorkOrderDto>> CreateWorkOrder([FromBody] CreateWorkOrderDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var newWorkOrder = await _workOrderService.CreateWorkOrderAsync(createDto);
            return CreatedAtAction(nameof(GetWorkOrder), new { id = newWorkOrder.Id }, newWorkOrder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating work order");
            return StatusCode(500, "An error occurred while creating the work order");
        }
    }

    /// <summary>
    /// Updates an existing work order
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<WorkOrderDto>> UpdateWorkOrder(Guid id, [FromBody] UpdateWorkOrderDto updateDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var existingWorkOrder = await _workOrderService.GetWorkOrderByIdAsync(id);
            if (existingWorkOrder == null)
                return NotFound($"Work order with ID {id} not found");

            var updatedWorkOrder = await _workOrderService.UpdateWorkOrderAsync(id, updateDto);
            return Ok(updatedWorkOrder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating work order with ID {id}");
            return StatusCode(500, $"An error occurred while updating work order with ID {id}");
        }
    }

    /// <summary>
    /// Deletes a work order (if not started)
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteWorkOrder(Guid id)
    {
        try
        {
            var workOrder = await _workOrderService.GetWorkOrderByIdAsync(id);
            if (workOrder == null)
                return NotFound($"Work order with ID {id} not found");

            await _workOrderService.DeleteWorkOrderAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting work order with ID {id}");
            return StatusCode(500, $"An error occurred while deleting work order with ID {id}");
        }
    }

    /// <summary>
    /// Updates work order status
    /// </summary>
    [HttpPut("{id:guid}/status")]
    public async Task<ActionResult<WorkOrderDto>> UpdateWorkOrderStatus(Guid id, [FromBody] UpdateWorkOrderStatusRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);
                
            var workOrder = await _workOrderService.GetWorkOrderByIdAsync(id);
            if (workOrder == null)
                return NotFound($"Work order with ID {id} not found");

            var updatedWorkOrder = await _workOrderService.UpdateWorkOrderStatusAsync(id, request.Status, request.Notes);
            return Ok(updatedWorkOrder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating status for work order with ID {id}");
            return StatusCode(500, $"An error occurred while updating status for work order with ID {id}");
        }
    }

    /// <summary>
    /// Approves a work order
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<WorkOrderDto>> ApproveWorkOrder(Guid id, [FromBody] ApproveWorkOrderRequest? request = null)
    {
        try
        {
            var workOrder = await _workOrderService.GetWorkOrderByIdAsync(id);
            if (workOrder == null)
                return NotFound($"Work order with ID {id} not found");

            var approvedWorkOrder = await _workOrderService.ApproveWorkOrderAsync(id, request?.Notes);
            return Ok(approvedWorkOrder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error approving work order with ID {id}");
            return StatusCode(500, $"An error occurred while approving work order with ID {id}");
        }
    }

    /// <summary>
    /// Gets work order metrics
    /// </summary>
    [HttpGet("metrics")]
    public async Task<ActionResult<WorkOrderMetricsDto>> GetWorkOrderMetrics()
    {
        try
        {
            var metrics = await _workOrderService.GetWorkOrderMetricsAsync();
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving work order metrics");
            return StatusCode(500, "An error occurred while retrieving work order metrics");
        }
    }

    /// <summary>
    /// Updates a work order task status
    /// </summary>
    [HttpPut("tasks/{taskId:guid}/status")]
    public async Task<ActionResult<ErpSystem.Core.DTOs.Maintenance.WorkOrderTaskDto>> UpdateTaskStatus(
        Guid taskId, 
        [FromBody] UpdateTaskStatusRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var updatedTask = await _workOrderService.UpdateTaskStatusAsync(
                taskId, 
                request.Status, 
                request.ActualHours, 
                request.CompletionNotes);
            
            if (updatedTask == null)
                return NotFound($"Task with ID {taskId} not found");

            return Ok(updatedTask);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating task {taskId} status");
            return StatusCode(500, $"An error occurred while updating task status");
        }
    }
}

public class UpdateTaskStatusRequest
{
    public string Status { get; set; } = string.Empty;
    public double? ActualHours { get; set; }
    public string? CompletionNotes { get; set; }
}
