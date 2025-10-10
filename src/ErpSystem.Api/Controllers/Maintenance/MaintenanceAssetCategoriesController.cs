using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/asset-categories")]
[Authorize]
public class MaintenanceAssetCategoriesController : ControllerBase
{
    private readonly IMaintenanceAssetCategoryService _categoryService;
    private readonly ILogger<MaintenanceAssetCategoriesController> _logger;

    public MaintenanceAssetCategoriesController(
        IMaintenanceAssetCategoryService categoryService,
        ILogger<MaintenanceAssetCategoriesController> logger)
    {
        _categoryService = categoryService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all maintenance asset categories
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<MaintenanceAssetCategoryDto>>> GetCategories()
    {
        try
        {
            var categories = await _categoryService.GetAllCategoriesAsync();
            return Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance asset categories");
            return StatusCode(500, "An error occurred while retrieving categories");
        }
    }

    /// <summary>
    /// Gets a specific maintenance asset category by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MaintenanceAssetCategoryDto>> GetCategory(Guid id)
    {
        try
        {
            var category = await _categoryService.GetCategoryByIdAsync(id);
            if (category == null)
                return NotFound($"Category with ID {id} not found");

            return Ok(category);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance asset category {CategoryId}", id);
            return StatusCode(500, "An error occurred while retrieving the category");
        }
    }

    /// <summary>
    /// Creates a new maintenance asset category
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<MaintenanceAssetCategoryDto>> CreateCategory([FromBody] CreateMaintenanceAssetCategoryDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var category = await _categoryService.CreateCategoryAsync(createDto);
            return CreatedAtAction(nameof(GetCategory), new { id = category.Id }, category);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating maintenance asset category");
            return StatusCode(500, "An error occurred while creating the category");
        }
    }

    /// <summary>
    /// Updates an existing maintenance asset category
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MaintenanceAssetCategoryDto>> UpdateCategory(Guid id, [FromBody] UpdateMaintenanceAssetCategoryDto updateDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var category = await _categoryService.UpdateCategoryAsync(id, updateDto);
            return Ok(category);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating maintenance asset category {CategoryId}", id);
            return StatusCode(500, "An error occurred while updating the category");
        }
    }

    /// <summary>
    /// Deletes a maintenance asset category
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteCategory(Guid id)
    {
        try
        {
            await _categoryService.DeleteCategoryAsync(id);
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
            _logger.LogError(ex, "Error deleting maintenance asset category {CategoryId}", id);
            return StatusCode(500, "An error occurred while deleting the category");
        }
    }

    /// <summary>
    /// Gets category hierarchy (parent-child relationships)
    /// </summary>
    [HttpGet("hierarchy")]
    public async Task<ActionResult<IEnumerable<MaintenanceAssetCategoryDto>>> GetCategoryHierarchy()
    {
        try
        {
            var categories = await _categoryService.GetCategoryHierarchyAsync();
            return Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving category hierarchy");
            return StatusCode(500, "An error occurred while retrieving category hierarchy");
        }
    }

    /// <summary>
    /// Gets child categories for a specific parent category
    /// </summary>
    [HttpGet("{parentId:guid}/children")]
    public async Task<ActionResult<IEnumerable<MaintenanceAssetCategoryDto>>> GetChildCategories(Guid parentId)
    {
        try
        {
            var categories = await _categoryService.GetChildCategoriesAsync(parentId);
            return Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving child categories for {ParentId}", parentId);
            return StatusCode(500, "An error occurred while retrieving child categories");
        }
    }
}