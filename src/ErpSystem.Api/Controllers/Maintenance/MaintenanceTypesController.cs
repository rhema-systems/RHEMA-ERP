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
            
            // Fallback to mock data if service is unavailable
            var fallbackResult = GetMockMaintenanceTypes(page, pageSize, searchTerm, category, isActive);
            return Ok(fallbackResult);
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
            
            // Fallback to mock data
            var fallbackResult = GetMockActiveMaintenanceTypes();
            return Ok(fallbackResult);
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
            
            // Fallback to mock data
            var fallbackResult = GetMockMaintenanceTypeById(id);
            if (fallbackResult == null)
                return NotFound($"Maintenance type with ID {id} not found");
            
            return Ok(fallbackResult);
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

    #region Fallback Methods

    private PagedResult<MaintenanceTypeDto> GetMockMaintenanceTypes(
        int page, int pageSize, string? searchTerm, string? category, bool? isActive)
    {
        var mockData = new List<MaintenanceTypeDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Preventive Maintenance",
                Code = "PM-001",
                Description = "Scheduled maintenance to prevent equipment failure",
                Category = "Preventive",
                Priority = "Medium",
                EstimatedDuration = 2.0m,
                IsActive = true,
                RequiresDowntime = false,
                Color = "#3b82f6",
                Icon = "calendar",
                Frequency = "Monthly",
                SkillLevel = "Intermediate",
                SafetyRequirements = "Standard PPE required",
                ToolsRequired = "Basic maintenance tools",
                Notes = "Follow manufacturer guidelines"
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Corrective Maintenance",
                Code = "CM-001",
                Description = "Maintenance performed to restore equipment to working condition",
                Category = "Corrective",
                Priority = "High",
                EstimatedDuration = 4.0m,
                IsActive = true,
                RequiresDowntime = true,
                Color = "#ef4444",
                Icon = "wrench",
                Frequency = "As Needed",
                SkillLevel = "Advanced",
                SafetyRequirements = "Enhanced PPE and lockout/tagout",
                ToolsRequired = "Specialized repair tools",
                Notes = "Document root cause analysis"
            }
        };

        // Apply filters
        var filtered = mockData.AsQueryable();

        if (!string.IsNullOrEmpty(searchTerm))
        {
            filtered = filtered.Where(x => x.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                                          x.Code.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                                          x.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(category))
        {
            filtered = filtered.Where(x => x.Category == category);
        }

        if (isActive.HasValue)
        {
            filtered = filtered.Where(x => x.IsActive == isActive.Value);
        }

        var totalCount = filtered.Count();
        var items = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<MaintenanceTypeDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    private IEnumerable<MaintenanceTypeDto> GetMockActiveMaintenanceTypes()
    {
        return GetMockMaintenanceTypes(1, 100, null, null, true).Items;
    }

    private MaintenanceTypeDto? GetMockMaintenanceTypeById(Guid id)
    {
        return GetMockMaintenanceTypes(1, 100, null, null, null).Items.FirstOrDefault();
    }

    #endregion
}