using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Maintenance;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using System.Linq;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/assets")]
[Authorize]
public class MaintenanceAssetsController : ControllerBase
{
    private readonly IMaintenanceAssetService _maintenanceAssetService;
    private readonly ILogger<MaintenanceAssetsController> _logger;

    public MaintenanceAssetsController(
        IMaintenanceAssetService maintenanceAssetService,
        ILogger<MaintenanceAssetsController> logger)
    {
        _maintenanceAssetService = maintenanceAssetService;
        _logger = logger;
    }

    /// <summary>
    /// Gets a paginated list of maintenance assets
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<MaintenanceAssetListDto>>> GetAssets(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? searchTerm = null,
        [FromQuery] Guid? categoryId = null)
    {
        try
        {
            if (pageSize > 100)
                pageSize = 100;

            var result = await _maintenanceAssetService.GetAssetsPagedAsync(page, pageSize, searchTerm, categoryId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance assets");
            return StatusCode(500, "An error occurred while retrieving assets");
        }
    }

    /// <summary>
    /// Gets a specific maintenance asset by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MaintenanceAssetDto>> GetAsset(Guid id)
    {
        try
        {
            var asset = await _maintenanceAssetService.GetAssetByIdAsync(id);
            if (asset == null)
                return NotFound($"Asset with ID {id} not found");

            return Ok(asset);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance asset {AssetId}", id);
            return StatusCode(500, "An error occurred while retrieving the asset");
        }
    }

    /// <summary>
    /// Creates a new maintenance asset
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<MaintenanceAssetDto>> CreateAsset([FromBody] CreateMaintenanceAssetDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var newAsset = await _maintenanceAssetService.CreateAssetAsync(createDto);
            return CreatedAtAction(nameof(GetAsset), new { id = newAsset.Id }, newAsset);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating maintenance asset");
            return StatusCode(500, "An error occurred while creating the asset");
        }
    }

    /// <summary>
    /// Updates an existing maintenance asset
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MaintenanceAssetDto>> UpdateAsset(Guid id, [FromBody] UpdateMaintenanceAssetDto updateDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var updatedAsset = await _maintenanceAssetService.UpdateAssetAsync(id, updateDto);
            return Ok(updatedAsset);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating maintenance asset {AssetId}", id);
            return StatusCode(500, "An error occurred while updating the asset");
        }
    }

    /// <summary>
    /// Deletes a maintenance asset
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsset(Guid id)
    {
        try
        {
            await _maintenanceAssetService.DeleteAssetAsync(id);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting maintenance asset {AssetId}", id);
            return StatusCode(500, "An error occurred while deleting the asset");
        }
    }

    /// <summary>
    /// Gets assets by status
    /// </summary>
    [HttpGet("by-status/{status}")]
    public async Task<ActionResult<IEnumerable<MaintenanceAssetDto>>> GetAssetsByStatus(string status)
    {
        try
        {
            var assets = await _maintenanceAssetService.GetAssetsByStatusAsync(status);
            return Ok(assets);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving assets by status: {Status}", status);
            return StatusCode(500, "An error occurred while retrieving assets");
        }
    }

    /// <summary>
    /// Gets assets by category
    /// </summary>
    [HttpGet("by-category/{categoryId:guid}")]
    public async Task<ActionResult<IEnumerable<MaintenanceAssetDto>>> GetAssetsByCategory(Guid categoryId)
    {
        try
        {
            var assets = await _maintenanceAssetService.GetAssetsByCategoryAsync(categoryId);
            return Ok(assets);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving assets by category: {CategoryId}", categoryId);
            return StatusCode(500, "An error occurred while retrieving assets");
        }
    }

    /// <summary>
    /// Gets assets with active work orders
    /// </summary>
    [HttpGet("with-active-work-orders")]
    public async Task<ActionResult<IEnumerable<MaintenanceAssetDto>>> GetAssetsWithActiveWorkOrders()
    {
        try
        {
            var assets = await _maintenanceAssetService.GetAssetsWithActiveWorkOrdersAsync();
            return Ok(assets);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving assets with active work orders");
            return StatusCode(500, "An error occurred while retrieving assets");
        }
    }

    /// <summary>
    /// Gets assets requiring maintenance
    /// </summary>
    [HttpGet("requiring-maintenance")]
    public async Task<ActionResult<IEnumerable<MaintenanceAssetDto>>> GetAssetsRequiringMaintenance()
    {
        try
        {
            var assets = await _maintenanceAssetService.GetAssetsRequiringMaintenanceAsync();
            return Ok(assets);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving assets requiring maintenance");
            return StatusCode(500, "An error occurred while retrieving assets");
        }
    }

    /// <summary>
    /// Gets asset hierarchy for a given root asset
    /// </summary>
    [HttpGet("{rootAssetId:guid}/hierarchy")]
    public async Task<ActionResult<IEnumerable<MaintenanceAssetDto>>> GetAssetHierarchy(Guid rootAssetId)
    {
        try
        {
            var assets = await _maintenanceAssetService.GetAssetHierarchyAsync(rootAssetId);
            return Ok(assets);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving asset hierarchy for: {RootAssetId}", rootAssetId);
            return StatusCode(500, "An error occurred while retrieving asset hierarchy");
        }
    }

    /// <summary>
    /// Gets asset metrics and statistics
    /// </summary>
    [HttpGet("metrics")]
    public async Task<ActionResult<AssetMetricsDto>> GetAssetMetrics()
    {
        try
        {
            var metrics = await _maintenanceAssetService.GetAssetMetricsAsync();
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving asset metrics");
            return StatusCode(500, "An error occurred while retrieving asset metrics");
        }
    }

    /// <summary>
    /// Updates operating hours for an asset
    /// </summary>
    [HttpPut("{assetId:guid}/operating-hours")]
    public async Task<IActionResult> UpdateOperatingHours(Guid assetId, [FromBody] double operatingHours)
    {
        try
        {
            if (operatingHours < 0)
                return BadRequest("Operating hours cannot be negative");

            await _maintenanceAssetService.UpdateAssetOperatingHoursAsync(assetId, operatingHours);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating operating hours for asset {AssetId}", assetId);
            return StatusCode(500, "An error occurred while updating operating hours");
        }
    }

    /// <summary>
    /// Updates mileage for an asset
    /// </summary>
    [HttpPut("{assetId:guid}/mileage")]
    public async Task<IActionResult> UpdateMileage(Guid assetId, [FromBody] double mileage)
    {
        try
        {
            if (mileage < 0)
                return BadRequest("Mileage cannot be negative");

            await _maintenanceAssetService.UpdateAssetMileageAsync(assetId, mileage);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating mileage for asset {AssetId}", assetId);
            return StatusCode(500, "An error occurred while updating mileage");
        }
    }

    /// <summary>
    /// Generates a unique asset number for a category
    /// </summary>
    [HttpGet("generate-asset-number")]
    public async Task<ActionResult<string>> GenerateAssetNumber([FromQuery] Guid categoryId)
    {
        try
        {
            var assetNumber = await _maintenanceAssetService.GenerateAssetNumberAsync(categoryId);
            return Ok(new { assetNumber });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating asset number for category {CategoryId}", categoryId);
            return StatusCode(500, "An error occurred while generating asset number");
        }
    }

    /// <summary>
    /// Validates if an asset number is unique
    /// </summary>
    [HttpGet("validate-asset-number")]
    public async Task<ActionResult<bool>> ValidateAssetNumber([FromQuery] string assetNumber, [FromQuery] Guid? excludeId = null)
    {
        try
        {
            if (string.IsNullOrEmpty(assetNumber))
                return BadRequest("Asset number is required");

            var isUnique = await _maintenanceAssetService.IsAssetNumberUniqueAsync(assetNumber, excludeId);
            return Ok(new { isUnique });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating asset number uniqueness: {AssetNumber}", assetNumber);
            return StatusCode(500, "An error occurred while validating asset number");
        }
    }
}
