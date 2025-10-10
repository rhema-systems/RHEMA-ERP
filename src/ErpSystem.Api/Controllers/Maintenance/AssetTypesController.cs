using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Services;
using System.Security.Claims;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AssetTypesController : ControllerBase
{
    private readonly IAssetTypeService _assetTypeService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<AssetTypesController> _logger;

    public AssetTypesController(
        IAssetTypeService assetTypeService,
        ITenantContext tenantContext,
        ILogger<AssetTypesController> logger)
    {
        _assetTypeService = assetTypeService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <summary>
    /// Get all asset types for the current tenant
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<AssetTypeDto>>> GetAssetTypes([FromQuery] bool includeInactive = false)
    {
        try
        {
            var tenantId = _tenantContext.GetCurrentTenantId();
            var assetTypes = await _assetTypeService.GetAssetTypesAsync(tenantId, includeInactive);
            return Ok(assetTypes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting asset types");
            return StatusCode(500, "An error occurred while retrieving asset types");
        }
    }

    /// <summary>
    /// Get asset type by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<AssetTypeDto>> GetAssetType(Guid id)
    {
        try
        {
            var tenantId = _tenantContext.GetCurrentTenantId();
            var assetType = await _assetTypeService.GetAssetTypeByIdAsync(id, tenantId);
            return Ok(assetType);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Asset type {AssetTypeId} not found", id);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting asset type {AssetTypeId}", id);
            return StatusCode(500, "An error occurred while retrieving the asset type");
        }
    }

    /// <summary>
    /// Get asset types by category
    /// </summary>
    [HttpGet("category/{category}")]
    public async Task<ActionResult<List<AssetTypeDto>>> GetAssetTypesByCategory(
        string category, 
        [FromQuery] bool includeInactive = false)
    {
        try
        {
            var tenantId = _tenantContext.GetCurrentTenantId();
            var assetTypes = await _assetTypeService.GetAssetTypesByCategoryAsync(category, tenantId, includeInactive);
            return Ok(assetTypes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting asset types for category {Category}", category);
            return StatusCode(500, "An error occurred while retrieving asset types");
        }
    }

    /// <summary>
    /// Get all asset type categories
    /// </summary>
    [HttpGet("categories")]
    public async Task<ActionResult<List<string>>> GetAssetTypeCategories()
    {
        try
        {
            var tenantId = _tenantContext.GetCurrentTenantId();
            var categories = await _assetTypeService.GetAssetTypeCategoriesAsync(tenantId);
            return Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting asset type categories");
            return StatusCode(500, "An error occurred while retrieving asset type categories");
        }
    }

    /// <summary>
    /// Create a new asset type
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<AssetTypeDto>> CreateAssetType([FromBody] CreateAssetTypeDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var tenantId = _tenantContext.GetCurrentTenantId();
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var assetType = await _assetTypeService.CreateAssetTypeAsync(dto, userId, tenantId);
            return CreatedAtAction(nameof(GetAssetType), new { id = assetType.Id }, assetType);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation while creating asset type");
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating asset type");
            return StatusCode(500, "An error occurred while creating the asset type");
        }
    }

    /// <summary>
    /// Update an existing asset type
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<AssetTypeDto>> UpdateAssetType(Guid id, [FromBody] UpdateAssetTypeDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var tenantId = _tenantContext.GetCurrentTenantId();
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var assetType = await _assetTypeService.UpdateAssetTypeAsync(id, dto, userId, tenantId);
            return Ok(assetType);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation while updating asset type {AssetTypeId}", id);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating asset type {AssetTypeId}", id);
            return StatusCode(500, "An error occurred while updating the asset type");
        }
    }

    /// <summary>
    /// Delete an asset type
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteAssetType(Guid id)
    {
        try
        {
            var tenantId = _tenantContext.GetCurrentTenantId();
            var deleted = await _assetTypeService.DeleteAssetTypeAsync(id, tenantId);

            if (!deleted)
            {
                return NotFound("Asset type not found");
            }

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation while deleting asset type {AssetTypeId}", id);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting asset type {AssetTypeId}", id);
            return StatusCode(500, "An error occurred while deleting the asset type");
        }
    }

    /// <summary>
    /// Check if an asset type exists
    /// </summary>
    [HttpHead("{id}")]
    public async Task<ActionResult> AssetTypeExists(Guid id)
    {
        try
        {
            var tenantId = _tenantContext.GetCurrentTenantId();
            var exists = await _assetTypeService.AssetTypeExistsAsync(id, tenantId);

            if (exists)
            {
                return Ok();
            }

            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if asset type {AssetTypeId} exists", id);
            return StatusCode(500, "An error occurred while checking asset type existence");
        }
    }
}