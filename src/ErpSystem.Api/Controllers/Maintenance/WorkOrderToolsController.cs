using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/workorders/{workOrderId}/tools")]
[Authorize]
public class WorkOrderToolsController : ControllerBase
{
    private readonly IWorkOrderToolService _workOrderToolService;
    private readonly ILogger<WorkOrderToolsController> _logger;

    public WorkOrderToolsController(
        IWorkOrderToolService workOrderToolService,
        ILogger<WorkOrderToolsController> logger)
    {
        _workOrderToolService = workOrderToolService;
        _logger = logger;
    }

    /// <summary>
    /// Get all tools allocated to a work order
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<WorkOrderToolDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWorkOrderTools(Guid workOrderId)
    {
        try
        {
            var tools = await _workOrderToolService.GetToolsByWorkOrderAsync(workOrderId);
            return Ok(tools);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tools for work order {WorkOrderId}", workOrderId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving tools");
        }
    }

    /// <summary>
    /// Get summary statistics for work order tools
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(WorkOrderToolSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetToolSummary(Guid workOrderId)
    {
        try
        {
            var summary = await _workOrderToolService.GetWorkOrderToolSummaryAsync(workOrderId);
            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tool summary for work order {WorkOrderId}", workOrderId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving summary");
        }
    }

    /// <summary>
    /// Get checked out tools for a work order
    /// </summary>
    [HttpGet("checked-out")]
    [ProducesResponseType(typeof(IEnumerable<WorkOrderToolDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCheckedOutTools(Guid workOrderId)
    {
        try
        {
            var tools = await _workOrderToolService.GetCheckedOutToolsForWorkOrderAsync(workOrderId);
            return Ok(tools);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving checked out tools for work order {WorkOrderId}", workOrderId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving checked out tools");
        }
    }

    /// <summary>
    /// Get overdue tools for a work order
    /// </summary>
    [HttpGet("overdue")]
    [ProducesResponseType(typeof(IEnumerable<WorkOrderToolDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOverdueTools(Guid workOrderId)
    {
        try
        {
            var tools = await _workOrderToolService.GetOverdueToolsForWorkOrderAsync(workOrderId);
            return Ok(tools);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving overdue tools for work order {WorkOrderId}", workOrderId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving overdue tools");
        }
    }

    /// <summary>
    /// Get total tool rental cost for a work order
    /// </summary>
    [HttpGet("cost")]
    [ProducesResponseType(typeof(decimal), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTotalCost(Guid workOrderId)
    {
        try
        {
            var cost = await _workOrderToolService.GetTotalToolCostAsync(workOrderId);
            return Ok(cost);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating tool cost for work order {WorkOrderId}", workOrderId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while calculating cost");
        }
    }

    /// <summary>
    /// Allocate a tool to a work order
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(WorkOrderToolDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AllocateTool(Guid workOrderId, [FromBody] AllocateWorkOrderToolDto allocateDto)
    {
        try
        {
            if (workOrderId != allocateDto.WorkOrderId)
            {
                return BadRequest("Work order ID mismatch");
            }

            var result = await _workOrderToolService.AllocateToolAsync(allocateDto);
            return CreatedAtAction(nameof(GetWorkOrderTools), new { workOrderId }, result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument when allocating tool to work order {WorkOrderId}", workOrderId);
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation when allocating tool to work order {WorkOrderId}", workOrderId);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error allocating tool to work order {WorkOrderId}", workOrderId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while allocating tool");
        }
    }

    /// <summary>
    /// Allocate multiple tools to a work order in bulk
    /// </summary>
    [HttpPost("bulk")]
    [ProducesResponseType(typeof(IEnumerable<WorkOrderToolDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AllocateToolsBulk(Guid workOrderId, [FromBody] IEnumerable<AllocateWorkOrderToolDto> allocateDtos)
    {
        try
        {
            // Validate all DTOs have matching work order ID
            if (allocateDtos.Any(dto => dto.WorkOrderId != workOrderId))
            {
                return BadRequest("All tools must be for the specified work order");
            }

            var results = await _workOrderToolService.AllocateToolsBulkAsync(allocateDtos);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error allocating tools in bulk to work order {WorkOrderId}", workOrderId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while allocating tools");
        }
    }

    /// <summary>
    /// Remove tool allocation from a work order
    /// </summary>
    [HttpDelete("{toolId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveToolAllocation(Guid workOrderId, Guid toolId)
    {
        try
        {
            await _workOrderToolService.RemoveToolAllocationAsync(workOrderId, toolId);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Tool allocation not found: WorkOrder {WorkOrderId}, Tool {ToolId}", workOrderId, toolId);
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot remove tool allocation: WorkOrder {WorkOrderId}, Tool {ToolId}", workOrderId, toolId);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing tool allocation: WorkOrder {WorkOrderId}, Tool {ToolId}", workOrderId, toolId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while removing tool allocation");
        }
    }

    /// <summary>
    /// Checkout an allocated tool for the work order
    /// </summary>
    [HttpPost("{toolId}/checkout")]
    [ProducesResponseType(typeof(WorkOrderToolDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CheckoutTool(Guid workOrderId, Guid toolId, [FromBody] CheckoutWorkOrderToolDto checkoutDto)
    {
        try
        {
            if (workOrderId != checkoutDto.WorkOrderId)
            {
                return BadRequest("Work order ID mismatch");
            }

            if (toolId != checkoutDto.ToolId)
            {
                return BadRequest("Tool ID mismatch");
            }

            var result = await _workOrderToolService.CheckoutToolAsync(checkoutDto);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument when checking out tool {ToolId} for work order {WorkOrderId}", toolId, workOrderId);
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation when checking out tool {ToolId} for work order {WorkOrderId}", toolId, workOrderId);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking out tool {ToolId} for work order {WorkOrderId}", toolId, workOrderId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while checking out tool");
        }
    }

    /// <summary>
    /// Return a checked out tool
    /// </summary>
    [HttpPost("{toolId}/return")]
    [ProducesResponseType(typeof(WorkOrderToolDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReturnTool(Guid workOrderId, Guid toolId, [FromBody] ReturnWorkOrderToolDto returnDto)
    {
        try
        {
            var result = await _workOrderToolService.ReturnToolAsync(workOrderId, toolId, returnDto);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument when returning tool {ToolId} for work order {WorkOrderId}", toolId, workOrderId);
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation when returning tool {ToolId} for work order {WorkOrderId}", toolId, workOrderId);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error returning tool {ToolId} for work order {WorkOrderId}", toolId, workOrderId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while returning tool");
        }
    }
}
