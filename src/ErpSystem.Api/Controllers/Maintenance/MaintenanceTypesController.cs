using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/maintenance-types")]
[Authorize]
public class MaintenanceTypesController : ControllerBase
{
    private readonly IMaintenanceTypeService _maintenanceTypeService;
    private readonly ILogger<MaintenanceTypesController> _logger;

    public MaintenanceTypesController(
        IMaintenanceTypeService maintenanceTypeService,
        ILogger<MaintenanceTypesController> logger)
    {
        _maintenanceTypeService = maintenanceTypeService;
        _logger = logger;
    }

    /// <summary>
    /// Gets a paginated list of maintenance types with optional filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<MaintenanceTypeDto>>> GetMaintenanceTypes(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? category = null,
        [FromQuery] bool? isActive = null)
    {
        try
        {
            if (pageSize > 100)
                pageSize = 100;

            var filter = new MaintenanceTypeFilterDto
            {
                Page = page,
                PageSize = pageSize,
                SearchTerm = searchTerm,
                Category = category,
                IsActive = isActive
            };
            
            var result = await _maintenanceTypeService.GetMaintenanceTypesPagedAsync(filter);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance types");
            return StatusCode(500, "An error occurred while retrieving maintenance types");
        }
    }

    /// <summary>
    /// Gets all active maintenance types
    /// </summary>
    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<MaintenanceTypeDto>>> GetActiveMaintenanceTypes()
    {
        try
        {
            var result = await _maintenanceTypeService.GetActiveMaintenanceTypesAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active maintenance types");
            return StatusCode(500, "An error occurred while retrieving active maintenance types");
        }
    }

    /// <summary>
    /// Gets a specific maintenance type by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MaintenanceTypeDto>> GetMaintenanceType(Guid id)
    {
        try
        {
            var maintenanceType = await _maintenanceTypeService.GetMaintenanceTypeByIdAsync(id);
            if (maintenanceType == null)
                return NotFound($"Maintenance type with ID {id} not found");

            return Ok(maintenanceType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance type {MaintenanceTypeId}", id);
            return StatusCode(500, $"An error occurred while retrieving maintenance type {id}");
        }
    }

    /// <summary>
    /// Creates a new maintenance type
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<MaintenanceTypeDto>> CreateMaintenanceType([FromBody] CreateMaintenanceTypeDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var maintenanceType = await _maintenanceTypeService.CreateMaintenanceTypeAsync(createDto);
            return CreatedAtAction(nameof(GetMaintenanceType), new { id = maintenanceType.Id }, maintenanceType);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating maintenance type");
            return StatusCode(500, "An error occurred while creating the maintenance type");
        }
    }

    /// <summary>
    /// Updates an existing maintenance type
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MaintenanceTypeDto>> UpdateMaintenanceType(Guid id, [FromBody] UpdateMaintenanceTypeDto updateDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var maintenanceType = await _maintenanceTypeService.UpdateMaintenanceTypeAsync(id, updateDto);
            return Ok(maintenanceType);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating maintenance type {MaintenanceTypeId}", id);
            return StatusCode(500, "An error occurred while updating the maintenance type");
        }
    }

    /// <summary>
    /// Deletes a maintenance type
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteMaintenanceType(Guid id)
    {
        try
        {
            await _maintenanceTypeService.DeleteMaintenanceTypeAsync(id);
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
            _logger.LogError(ex, "Error deleting maintenance type {MaintenanceTypeId}", id);
            return StatusCode(500, "An error occurred while deleting the maintenance type");
        }
    }

    /// <summary>
    /// Toggles the active status of a maintenance type
    /// </summary>
    [HttpPut("{id:guid}/toggle-status")]
    public async Task<ActionResult<MaintenanceTypeDto>> ToggleStatus(Guid id)
    {
        try
        {
            var maintenanceType = await _maintenanceTypeService.ToggleMaintenanceTypeStatusAsync(id);
            return Ok(maintenanceType);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling maintenance type status {MaintenanceTypeId}", id);
            return StatusCode(500, "An error occurred while toggling the maintenance type status");
        }
    }
}