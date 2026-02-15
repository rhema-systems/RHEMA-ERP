using AutoMapper;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Interfaces.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Inventory;

/// <summary>
/// API Controller for managing inventory categories
/// </summary>
[ApiController]
[Route("api/inventory/categories")]
[Authorize]
public class InventoryCategoriesController : ControllerBase
{
    private readonly IInventoryCategoryRepository _categoryRepository;
    private readonly ILogger<InventoryCategoriesController> _logger;

    public InventoryCategoriesController(
        IInventoryCategoryRepository categoryRepository,
        ILogger<InventoryCategoriesController> logger)
    {
        _categoryRepository = categoryRepository;
        _logger = logger;
    }

    /// <summary>
    /// Gets all inventory categories
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<InventoryCategoryDto>>> GetAll([FromQuery] bool activeOnly = false)
    {
        try
        {
            var categories = activeOnly
                ? await _categoryRepository.GetActiveCategoriesAsync()
                : await _categoryRepository.GetAllAsync();

            var dtos = categories.Select(c => new InventoryCategoryDto
            {
                Id = c.Id,
                Code = c.Code,
                Name = c.Name,
                Description = c.Description,
                ParentCategoryId = c.ParentCategoryId,
                Color = c.Color,
                Icon = c.Icon,
                IsActive = c.IsActive,
                DefaultUnitOfMeasure = c.DefaultUnitOfMeasure,
                DefaultSerialTracking = c.DefaultSerialTracking,
                DefaultLotTracking = c.DefaultLotTracking,
                DefaultRequiresInspection = c.DefaultRequiresInspection
            });

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving inventory categories");
            return StatusCode(500, "An error occurred while retrieving inventory categories");
        }
    }

    /// <summary>
    /// Gets active inventory categories
    /// </summary>
    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<InventoryCategoryDto>>> GetActive()
    {
        try
        {
            var categories = await _categoryRepository.GetActiveCategoriesAsync();
            var dtos = categories.Select(c => new InventoryCategoryDto
            {
                Id = c.Id,
                Code = c.Code,
                Name = c.Name,
                Description = c.Description,
                ParentCategoryId = c.ParentCategoryId,
                IsActive = c.IsActive
            });

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active inventory categories");
            return StatusCode(500, "An error occurred while retrieving active categories");
        }
    }

    /// <summary>
    /// Gets root categories (no parent)
    /// </summary>
    [HttpGet("root")]
    public async Task<ActionResult<IEnumerable<InventoryCategoryDto>>> GetRootCategories()
    {
        try
        {
            var categories = await _categoryRepository.GetRootCategoriesAsync();
            var dtos = categories.Select(c => new InventoryCategoryDto
            {
                Id = c.Id,
                Code = c.Code,
                Name = c.Name,
                Description = c.Description,
                IsActive = c.IsActive
            });

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving root categories");
            return StatusCode(500, "An error occurred while retrieving root categories");
        }
    }
}

/// <summary>
/// DTO for Inventory Category
/// </summary>
public class InventoryCategoryDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ParentCategoryId { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public bool IsActive { get; set; }
    public string? DefaultUnitOfMeasure { get; set; }
    public bool DefaultSerialTracking { get; set; }
    public bool DefaultLotTracking { get; set; }
    public bool DefaultRequiresInspection { get; set; }
}

