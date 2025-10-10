using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;

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

            var filter = new WorkOrderFilterDto
            {
                Page = page,
                PageSize = pageSize,
                SearchTerm = searchTerm,
                Status = status,
                AssetId = assetId,
                AssignedTechnicianId = technicianId,
                StartDate = scheduledFrom,
                EndDate = scheduledTo
            };
            
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
            _logger.LogError(ex, "Error retrieving work order {WorkOrderId}", id);
            return StatusCode(500, "An error occurred while retrieving the work order");
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

            var workOrder = await _workOrderService.CreateWorkOrderAsync(createDto);
            return CreatedAtAction(nameof(GetWorkOrder), new { id = workOrder.Id }, workOrder);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
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

            var workOrder = await _workOrderService.UpdateWorkOrderAsync(id, updateDto);
            return Ok(workOrder);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating work order {WorkOrderId}", id);
            return StatusCode(500, "An error occurred while updating the work order");
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
            await _workOrderService.DeleteWorkOrderAsync(id);
            return NoContent();
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
            _logger.LogError(ex, "Error deleting work order {WorkOrderId}", id);
            return StatusCode(500, "An error occurred while deleting the work order");
        }
    }

    /// <summary>
    /// Starts a work order
    /// </summary>
    [HttpPut("{id:guid}/start")]
    public async Task<ActionResult<WorkOrderDto>> StartWorkOrder(Guid id)
    {
        try
        {
            var workOrder = await _workOrderService.StartWorkOrderAsync(id);
            return Ok(workOrder);
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
            _logger.LogError(ex, "Error starting work order {WorkOrderId}", id);
            return StatusCode(500, "An error occurred while starting the work order");
        }
    }

    /// <summary>
    /// Completes a work order
    /// </summary>
    [HttpPut("{id:guid}/complete")]
    public async Task<ActionResult<WorkOrderDto>> CompleteWorkOrder(Guid id, [FromBody] CompleteWorkOrderDto completeDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var workOrder = await _workOrderService.CompleteWorkOrderAsync(id, completeDto);
            return Ok(workOrder);
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
            _logger.LogError(ex, "Error completing work order {WorkOrderId}", id);
            return StatusCode(500, "An error occurred while completing the work order");
        }
    }

    /// <summary>
    /// Cancels a work order
    /// </summary>
    [HttpPut("{id:guid}/cancel")]
    public async Task<ActionResult<WorkOrderDto>> CancelWorkOrder(Guid id, [FromBody] CancelWorkOrderDto cancelDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var workOrder = await _workOrderService.CancelWorkOrderAsync(id, cancelDto.Reason);
            return Ok(workOrder);
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
            _logger.LogError(ex, "Error canceling work order {WorkOrderId}", id);
            return StatusCode(500, "An error occurred while canceling the work order");
        }
    }

    /// <summary>
    /// Assigns a technician to a work order
    /// </summary>
    [HttpPut("{id:guid}/assign")]
    public async Task<ActionResult<WorkOrderDto>> AssignTechnician(Guid id, [FromBody] AssignTechnicianDto assignDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var workOrder = await _workOrderService.AssignTechnicianAsync(id, assignDto.TechnicianId);
            return Ok(workOrder);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning technician to work order {WorkOrderId}", id);
            return StatusCode(500, "An error occurred while assigning the technician");
        }
    }

    /// <summary>
    /// Updates work order priority
    /// </summary>
    [HttpPut("{id:guid}/priority")]
    public async Task<ActionResult<WorkOrderDto>> UpdatePriority(Guid id, [FromBody] UpdatePriorityDto priorityDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var workOrder = await _workOrderService.UpdatePriorityAsync(id, priorityDto.Priority);
            return Ok(workOrder);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating work order priority {WorkOrderId}", id);
            return StatusCode(500, "An error occurred while updating priority");
        }
    }

    /// <summary>
    /// Reschedules a work order
    /// </summary>
    [HttpPut("{id:guid}/reschedule")]
    public async Task<ActionResult<WorkOrderDto>> RescheduleWorkOrder(Guid id, [FromBody] RescheduleWorkOrderDto rescheduleDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var workOrder = await _workOrderService.RescheduleWorkOrderAsync(id, rescheduleDto.NewScheduledStart);
            return Ok(workOrder);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rescheduling work order {WorkOrderId}", id);
            return StatusCode(500, "An error occurred while rescheduling the work order");
        }
    }

    /// <summary>
    /// Gets work orders by status
    /// </summary>
    [HttpGet("by-status/{status}")]
    public async Task<ActionResult<IEnumerable<WorkOrderListDto>>> GetWorkOrdersByStatus(string status)
    {
        try
        {
            var workOrders = await _workOrderService.GetWorkOrdersByStatusAsync(status);
            return Ok(workOrders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving work orders by status {Status}", status);
            return StatusCode(500, "An error occurred while retrieving work orders");
        }
    }

    /// <summary>
    /// Gets work orders assigned to a specific technician
    /// </summary>
    [HttpGet("by-technician/{technicianId:guid}")]
    public async Task<ActionResult<IEnumerable<WorkOrderListDto>>> GetWorkOrdersByTechnician(Guid technicianId)
    {
        try
        {
            var workOrders = await _workOrderService.GetWorkOrdersByTechnicianAsync(technicianId);
            return Ok(workOrders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving work orders for technician {TechnicianId}", technicianId);
            return StatusCode(500, "An error occurred while retrieving work orders");
        }
    }

    /// <summary>
    /// Gets overdue work orders
    /// </summary>
    [HttpGet("overdue")]
    public async Task<ActionResult<IEnumerable<WorkOrderListDto>>> GetOverdueWorkOrders()
    {
        try
        {
            var workOrders = await _workOrderService.GetOverdueWorkOrdersAsync();
            return Ok(workOrders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving overdue work orders");
            return StatusCode(500, "An error occurred while retrieving overdue work orders");
        }
    }

    /// <summary>
    /// Gets work orders scheduled for today
    /// </summary>
    [HttpGet("scheduled-today")]
    public async Task<ActionResult<IEnumerable<WorkOrderListDto>>> GetWorkOrdersScheduledToday()
    {
        try
        {
            var workOrders = await _workOrderService.GetWorkOrdersScheduledTodayAsync();
            return Ok(workOrders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving work orders scheduled for today");
            return StatusCode(500, "An error occurred while retrieving today's work orders");
        }
    }

    /// <summary>
    /// Gets work order statistics and metrics
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
    /// Generates a unique work order number
    /// </summary>
    [HttpGet("generate-number")]
    public async Task<ActionResult<string>> GenerateWorkOrderNumber()
    {
        try
        {
            var workOrderNumber = await _workOrderService.GenerateWorkOrderNumberAsync();
            return Ok(new { workOrderNumber });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating work order number");
            return StatusCode(500, "An error occurred while generating work order number");
        }
    }

    /// <summary>
    /// Creates recurring work orders from a maintenance schedule
    /// </summary>
    [HttpPost("create-from-schedule/{scheduleId:guid}")]
    public async Task<ActionResult<IEnumerable<WorkOrderDto>>> CreateWorkOrdersFromSchedule(Guid scheduleId)
    {
        try
        {
            var workOrders = await _workOrderService.CreateWorkOrdersFromScheduleAsync(scheduleId);
            return Ok(workOrders);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating work orders from schedule {ScheduleId}", scheduleId);
            return StatusCode(500, "An error occurred while creating work orders from schedule");
        }
    }
}

public class CancelWorkOrderDto
{
    public string Reason { get; set; } = string.Empty;
}

public class AssignTechnicianDto
{
    public Guid TechnicianId { get; set; }
}

public class UpdatePriorityDto
{
    public string Priority { get; set; } = string.Empty;
}

public class RescheduleWorkOrderDto
{
    public DateTime NewScheduledStart { get; set; }
    public DateTime NewScheduledEnd { get; set; }
}