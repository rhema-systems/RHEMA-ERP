using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/partner-categories")]
[Authorize]
public class PartnerCategoriesController : ControllerBase
{
    private readonly IPartnerCategoryService _categoryService;
    private readonly ILogger<PartnerCategoriesController> _logger;

    public PartnerCategoriesController(
        IPartnerCategoryService categoryService,
        ILogger<PartnerCategoriesController> logger)
    {
        _categoryService = categoryService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PartnerCategoryDto>>> GetAllCategories()
    {
        try
        {
            var categories = await _categoryService.GetAllCategoriesAsync();
            return Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving partner categories");
            return StatusCode(500, "An error occurred while retrieving partner categories");
        }
    }

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<PartnerCategoryDto>>> GetActiveCategories()
    {
        try
        {
            var categories = await _categoryService.GetActiveCategoriesAsync();
            return Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active partner categories");
            return StatusCode(500, "An error occurred while retrieving active partner categories");
        }
    }

    [HttpGet("root")]
    public async Task<ActionResult<IEnumerable<PartnerCategoryDto>>> GetRootCategories()
    {
        try
        {
            var categories = await _categoryService.GetRootCategoriesAsync();
            return Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving root partner categories");
            return StatusCode(500, "An error occurred while retrieving root partner categories");
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PartnerCategoryDto>> GetCategory(Guid id)
    {
        try
        {
            var category = await _categoryService.GetByIdAsync(id);
            if (category == null)
                return NotFound();

            return Ok(category);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving partner category {CategoryId}", id);
            return StatusCode(500, "An error occurred while retrieving the partner category");
        }
    }

    [HttpPost]
    public async Task<ActionResult<PartnerCategoryDto>> CreateCategory([FromBody] CreatePartnerCategoryDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var category = await _categoryService.CreateAsync(createDto);
            return CreatedAtAction(nameof(GetCategory), new { id = category.Id }, category);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating partner category");
            return StatusCode(500, "An error occurred while creating the partner category");
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PartnerCategoryDto>> UpdateCategory(Guid id, [FromBody] UpdatePartnerCategoryDto updateDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var category = await _categoryService.UpdateAsync(id, updateDto);
            return Ok(category);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating partner category {CategoryId}", id);
            return StatusCode(500, "An error occurred while updating the partner category");
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteCategory(Guid id)
    {
        try
        {
            await _categoryService.DeleteAsync(id);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting partner category {CategoryId}", id);
            return StatusCode(500, "An error occurred while deleting the partner category");
        }
    }
}

