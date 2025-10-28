using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/work-order-types")]
[Authorize]
public class WorkOrderTypesController : ControllerBase
{
    private readonly IWorkOrderTypeService _workOrderTypeService;
    private readonly ILogger<WorkOrderTypesController> _logger;

    public WorkOrderTypesController(
        IWorkOrderTypeService workOrderTypeService,
        ILogger<WorkOrderTypesController> logger)
    {
        _workOrderTypeService = workOrderTypeService;
        _logger = logger;
    }

    /// <summary>
    /// Gets a paginated list of work order types with optional filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<WorkOrderTypeDto>>> GetWorkOrderTypes(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? category = null,
        [FromQuery] string? priority = null,
        [FromQuery] bool? isActive = null)
    {
        try
        {
            if (pageSize > 100)
                pageSize = 100;

            var filter = new WorkOrderTypeFilterDto
            {
                Page = page,
                PageSize = pageSize,
                SearchTerm = searchTerm,
                Category = category,
                Priority = priority,
                IsActive = isActive
            };
            
            var result = await _workOrderTypeService.GetWorkOrderTypesPagedAsync(filter);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving work order types");
            return StatusCode(500, "An error occurred while retrieving work order types");
        }
    }

    /// <summary>
    /// Gets all active work order types
    /// </summary>
    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<WorkOrderTypeDto>>> GetActiveWorkOrderTypes()
    {
        try
        {
            var result = await _workOrderTypeService.GetActiveWorkOrderTypesAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active work order types");
            return StatusCode(500, "An error occurred while retrieving active work order types");
        }
    }

    /// <summary>
    /// Gets a specific work order type by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkOrderTypeDto>> GetWorkOrderType(Guid id)
    {
        try
        {
            var workOrderType = await _workOrderTypeService.GetWorkOrderTypeByIdAsync(id);
            if (workOrderType == null)
                return NotFound($"Work order type with ID {id} not found");

            return Ok(workOrderType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving work order type {WorkOrderTypeId}", id);
            return StatusCode(500, "An error occurred while retrieving the work order type");
        }
    }

    /// <summary>
    /// Creates a new work order type
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<WorkOrderTypeDto>> CreateWorkOrderType([FromBody] CreateWorkOrderTypeDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var workOrderType = await _workOrderTypeService.CreateWorkOrderTypeAsync(createDto);
            return CreatedAtAction(nameof(GetWorkOrderType), new { id = workOrderType.Id }, workOrderType);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating work order type");
            return StatusCode(500, "An error occurred while creating the work order type");
        }
    }

    /// <summary>
    /// Updates an existing work order type
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<WorkOrderTypeDto>> UpdateWorkOrderType(Guid id, [FromBody] UpdateWorkOrderTypeDto updateDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var workOrderType = await _workOrderTypeService.UpdateWorkOrderTypeAsync(id, updateDto);
            return Ok(workOrderType);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating work order type {WorkOrderTypeId}", id);
            return StatusCode(500, "An error occurred while updating the work order type");
        }
    }

    /// <summary>
    /// Deletes a work order type
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteWorkOrderType(Guid id)
    {
        try
        {
            await _workOrderTypeService.DeleteWorkOrderTypeAsync(id);
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
            _logger.LogError(ex, "Error deleting work order type {WorkOrderTypeId}", id);
            return StatusCode(500, "An error occurred while deleting the work order type");
        }
    }

    /// <summary>
    /// Toggles the active status of a work order type
    /// </summary>
    [HttpPut("{id:guid}/toggle-status")]
    public async Task<ActionResult<WorkOrderTypeDto>> ToggleStatus(Guid id)
    {
        try
        {
            var workOrderType = await _workOrderTypeService.ToggleWorkOrderTypeStatusAsync(id);
            return Ok(workOrderType);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling work order type status {WorkOrderTypeId}", id);
            return StatusCode(500, "An error occurred while toggling the work order type status");
        }
    }

    /// <summary>
    /// Gets work order types by category
    /// </summary>
    [HttpGet("category/{category}")]
    public async Task<ActionResult<IEnumerable<WorkOrderTypeDto>>> GetWorkOrderTypesByCategory(string category)
    {
        try
        {
            var result = await _workOrderTypeService.GetWorkOrderTypesByCategoryAsync(category);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving work order types for category {Category}", category);
            return StatusCode(500, "An error occurred while retrieving work order types by category");
        }
    }

    /// <summary>
    /// Test endpoint to verify API is working
    /// </summary>
    [HttpGet("test")]
    public ActionResult<object> Test()
    {
        return Ok(new { message = "Work Order Types API is working!", timestamp = DateTime.UtcNow });
    }

    /// <summary>
    /// Public test endpoint to verify API is working (no auth required)
    /// </summary>
    [HttpGet("ping")]
    [AllowAnonymous]
    public ActionResult<object> Ping()
    {
        return Ok(new { message = "Work Order Types API is reachable!", timestamp = DateTime.UtcNow });
    }
}