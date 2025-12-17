using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/task-templates")]
[Authorize]
public class TaskTemplatesController : ControllerBase
{
    private readonly ITaskTemplateService _taskTemplateService;
    private readonly ILogger<TaskTemplatesController> _logger;

    public TaskTemplatesController(
        ITaskTemplateService taskTemplateService,
        ILogger<TaskTemplatesController> logger)
    {
        _taskTemplateService = taskTemplateService;
        _logger = logger;
    }

    #region Asset Task Templates

    /// <summary>
    /// Get all asset task templates across all assets
    /// </summary>
    [HttpGet("asset")]
    public async Task<IActionResult> GetAllAssetTaskTemplates()
    {
        try
        {
            // Query all asset task templates from the database
            var templates = await _taskTemplateService.GetAllAssetTaskTemplatesAsync();
            return Ok(templates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all asset task templates");
            return StatusCode(500, "An error occurred while retrieving asset task templates");
        }
    }

    /// <summary>
    /// Get all task templates for a specific asset
    /// </summary>
    [HttpGet("asset/{assetId}")]
    public async Task<IActionResult> GetAssetTaskTemplates(Guid assetId)
    {
        try
        {
            var templates = await _taskTemplateService.GetAssetTaskTemplatesAsync(assetId);
            return Ok(templates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving asset task templates for asset {AssetId}", assetId);
            return StatusCode(500, "An error occurred while retrieving asset task templates");
        }
    }

    /// <summary>
    /// Get task templates for work order generation
    /// </summary>
    [HttpGet("asset/{assetId}/maintenance-type/{maintenanceTypeId}/for-work-order")]
    public async Task<IActionResult> GetTaskTemplatesForWorkOrder(Guid assetId, Guid maintenanceTypeId)
    {
        try
        {
            var templates = await _taskTemplateService.GetTaskTemplatesForWorkOrderAsync(assetId, maintenanceTypeId);
            return Ok(templates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving task templates for work order");
            return StatusCode(500, "An error occurred while retrieving task templates");
        }
    }

    /// <summary>
    /// Create a new asset task template
    /// </summary>
    [HttpPost("asset")]
    public async Task<IActionResult> CreateAssetTaskTemplate([FromBody] CreateAssetTaskTemplateDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var template = await _taskTemplateService.CreateAssetTaskTemplateAsync(createDto);
            return CreatedAtAction(nameof(GetAssetTaskTemplates), new { assetId = template.AssetId }, template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating asset task template");
            return StatusCode(500, "An error occurred while creating the asset task template");
        }
    }

    /// <summary>
    /// Update an asset task template
    /// </summary>
    [HttpPut("asset/{id}")]
    public async Task<IActionResult> UpdateAssetTaskTemplate(Guid id, [FromBody] UpdateAssetTaskTemplateDto updateDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var template = await _taskTemplateService.UpdateAssetTaskTemplateAsync(id, updateDto);
            return Ok(template);
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Asset task template with ID {id} not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating asset task template {Id}", id);
            return StatusCode(500, "An error occurred while updating the asset task template");
        }
    }

    /// <summary>
    /// Delete an asset task template
    /// </summary>
    [HttpDelete("asset/{id}")]
    public async Task<IActionResult> DeleteAssetTaskTemplate(Guid id)
    {
        try
        {
            await _taskTemplateService.DeleteAssetTaskTemplateAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Asset task template with ID {id} not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting asset task template {Id}", id);
            return StatusCode(500, "An error occurred while deleting the asset task template");
        }
    }

    #endregion

    #region Asset Type Task Templates

    /// <summary>
    /// Get all asset type task templates across all asset types
    /// </summary>
    [HttpGet("asset-type")]
    public async Task<IActionResult> GetAllAssetTypeTaskTemplates()
    {
        try
        {
            // Query all asset type task templates from the database
            var templates = await _taskTemplateService.GetAllAssetTypeTaskTemplatesAsync();
            return Ok(templates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all asset type task templates");
            return StatusCode(500, "An error occurred while retrieving asset type task templates");
        }
    }

    /// <summary>
    /// Get all task templates for a specific asset type
    /// </summary>
    [HttpGet("asset-type/{assetTypeId}")]
    public async Task<IActionResult> GetAssetTypeTaskTemplates(Guid assetTypeId)
    {
        try
        {
            var templates = await _taskTemplateService.GetAssetTypeTaskTemplatesAsync(assetTypeId);
            return Ok(templates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving asset type task templates for asset type {AssetTypeId}", assetTypeId);
            return StatusCode(500, "An error occurred while retrieving asset type task templates");
        }
    }

    /// <summary>
    /// Create a new asset type task template
    /// </summary>
    [HttpPost("asset-type")]
    public async Task<IActionResult> CreateAssetTypeTaskTemplate([FromBody] CreateAssetTypeTaskTemplateDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var template = await _taskTemplateService.CreateAssetTypeTaskTemplateAsync(createDto);
            return CreatedAtAction(nameof(GetAssetTypeTaskTemplates), new { assetTypeId = template.AssetTypeId }, template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating asset type task template");
            return StatusCode(500, "An error occurred while creating the asset type task template");
        }
    }

    /// <summary>
    /// Update an asset type task template
    /// </summary>
    [HttpPut("asset-type/{id}")]
    public async Task<IActionResult> UpdateAssetTypeTaskTemplate(Guid id, [FromBody] UpdateAssetTypeTaskTemplateDto updateDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var template = await _taskTemplateService.UpdateAssetTypeTaskTemplateAsync(id, updateDto);
            return Ok(template);
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Asset type task template with ID {id} not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating asset type task template {Id}", id);
            return StatusCode(500, "An error occurred while updating the asset type task template");
        }
    }

    /// <summary>
    /// Delete an asset type task template
    /// </summary>
    [HttpDelete("asset-type/{id}")]
    public async Task<IActionResult> DeleteAssetTypeTaskTemplate(Guid id)
    {
        try
        {
            await _taskTemplateService.DeleteAssetTypeTaskTemplateAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Asset type task template with ID {id} not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting asset type task template {Id}", id);
            return StatusCode(500, "An error occurred while deleting the asset type task template");
        }
    }

    #endregion

    #region Maintenance Type Task Templates

    /// <summary>
    /// Get all maintenance task templates (optionally filtered by maintenance type)
    /// </summary>
    [HttpGet("maintenance-type")]
    public async Task<IActionResult> GetAllMaintenanceTaskTemplates([FromQuery] Guid? maintenanceTypeId)
    {
        try
        {
            if (maintenanceTypeId.HasValue)
            {
                var templates = await _taskTemplateService.GetMaintenanceTaskTemplatesAsync(maintenanceTypeId.Value);
                return Ok(templates);
            }
            else
            {
                // Return all maintenance task templates
                var allTemplates = await _taskTemplateService.GetAllMaintenanceTaskTemplatesAsync();
                return Ok(allTemplates);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance task templates");
            return StatusCode(500, "An error occurred while retrieving maintenance task templates");
        }
    }

    /// <summary>
    /// Get all task templates for a specific maintenance type
    /// </summary>
    [HttpGet("maintenance-type/{maintenanceTypeId}")]
    public async Task<IActionResult> GetMaintenanceTaskTemplates(Guid maintenanceTypeId)
    {
        try
        {
            var templates = await _taskTemplateService.GetMaintenanceTaskTemplatesAsync(maintenanceTypeId);
            return Ok(templates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance task templates for maintenance type {MaintenanceTypeId}", maintenanceTypeId);
            return StatusCode(500, "An error occurred while retrieving maintenance task templates");
        }
    }

    /// <summary>
    /// Create a new maintenance type task template
    /// </summary>
    [HttpPost("maintenance-type")]
    public async Task<IActionResult> CreateMaintenanceTaskTemplate([FromBody] CreateMaintenanceTaskTemplateDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var template = await _taskTemplateService.CreateMaintenanceTaskTemplateAsync(createDto);
            return CreatedAtAction(nameof(GetMaintenanceTaskTemplates), new { maintenanceTypeId = template.MaintenanceTypeId }, template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating maintenance task template");
            return StatusCode(500, "An error occurred while creating the maintenance task template");
        }
    }

    /// <summary>
    /// Update a maintenance type task template
    /// </summary>
    [HttpPut("maintenance-type/{id}")]
    public async Task<IActionResult> UpdateMaintenanceTaskTemplate(Guid id, [FromBody] UpdateMaintenanceTaskTemplateDto updateDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var template = await _taskTemplateService.UpdateMaintenanceTaskTemplateAsync(id, updateDto);
            return Ok(template);
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Maintenance task template with ID {id} not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating maintenance task template {Id}", id);
            return StatusCode(500, "An error occurred while updating the maintenance task template");
        }
    }

    /// <summary>
    /// Delete a maintenance type task template
    /// </summary>
    [HttpDelete("maintenance-type/{id}")]
    public async Task<IActionResult> DeleteMaintenanceTaskTemplate(Guid id)
    {
        try
        {
            await _taskTemplateService.DeleteMaintenanceTaskTemplateAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Maintenance task template with ID {id} not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting maintenance task template {Id}", id);
            return StatusCode(500, "An error occurred while deleting the maintenance task template");
        }
    }

    #endregion
}
