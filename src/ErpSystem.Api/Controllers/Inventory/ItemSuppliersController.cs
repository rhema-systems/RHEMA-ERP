using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Interfaces.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Inventory;

/// <summary>
/// API controller for managing item-supplier relationships
/// </summary>
[ApiController]
[Route("api/inventory/item-suppliers")]
[Authorize]
public class ItemSuppliersController : ControllerBase
{
    private readonly IItemSupplierService _itemSupplierService;
    private readonly ILogger<ItemSuppliersController> _logger;

    public ItemSuppliersController(
        IItemSupplierService itemSupplierService,
        ILogger<ItemSuppliersController> logger)
    {
        _itemSupplierService = itemSupplierService;
        _logger = logger;
    }

    /// <summary>
    /// Gets item suppliers by inventory item
    /// </summary>
    [HttpGet("by-item/{inventoryItemId}")]
    public async Task<ActionResult<IEnumerable<ItemSupplierDto>>> GetByItem(Guid inventoryItemId)
    {
        try
        {
            var suppliers = await _itemSupplierService.GetByItemAsync(inventoryItemId);
            return Ok(suppliers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving suppliers for item {ItemId}", inventoryItemId);
            return StatusCode(500, "An error occurred while retrieving item suppliers");
        }
    }

    /// <summary>
    /// Gets item suppliers by supplier
    /// </summary>
    [HttpGet("by-supplier/{supplierId}")]
    public async Task<ActionResult<IEnumerable<ItemSupplierDto>>> GetBySupplier(Guid supplierId)
    {
        try
        {
            var items = await _itemSupplierService.GetBySupplierAsync(supplierId);
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving items for supplier {SupplierId}", supplierId);
            return StatusCode(500, "An error occurred while retrieving supplier items");
        }
    }

    /// <summary>
    /// Gets the preferred supplier for an inventory item
    /// </summary>
    [HttpGet("preferred/{inventoryItemId}")]
    public async Task<ActionResult<ItemSupplierDto>> GetPreferredSupplier(Guid inventoryItemId)
    {
        try
        {
            var supplier = await _itemSupplierService.GetPreferredSupplierAsync(inventoryItemId);
            if (supplier == null)
                return NotFound($"No preferred supplier found for item {inventoryItemId}");

            return Ok(supplier);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving preferred supplier for item {ItemId}", inventoryItemId);
            return StatusCode(500, "An error occurred while retrieving the preferred supplier");
        }
    }

    /// <summary>
    /// Creates a new item-supplier relationship
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ItemSupplierDto>> Create([FromBody] CreateItemSupplierDto dto)
    {
        try
        {
            var itemSupplier = await _itemSupplierService.CreateAsync(dto);
            return Ok(itemSupplier);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating item-supplier relationship");
            return StatusCode(500, "An error occurred while creating the item-supplier relationship");
        }
    }

    /// <summary>
    /// Updates an item-supplier relationship
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<ItemSupplierDto>> Update(Guid id, [FromBody] UpdateItemSupplierDto dto)
    {
        try
        {
            var itemSupplier = await _itemSupplierService.UpdateAsync(id, dto);
            return Ok(itemSupplier);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating item-supplier relationship {Id}", id);
            return StatusCode(500, "An error occurred while updating the item-supplier relationship");
        }
    }

    /// <summary>
    /// Sets an item-supplier as the preferred supplier
    /// </summary>
    [HttpPost("{id}/set-preferred")]
    public async Task<ActionResult> SetAsPreferred(Guid id)
    {
        try
        {
            var result = await _itemSupplierService.SetAsPreferredAsync(id);
            if (!result)
                return BadRequest("Failed to set as preferred supplier");

            return Ok(new { message = "Set as preferred supplier successfully" });
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting item-supplier {Id} as preferred", id);
            return StatusCode(500, "An error occurred while setting the preferred supplier");
        }
    }

    /// <summary>
    /// Deletes an item-supplier relationship
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        try
        {
            var result = await _itemSupplierService.DeleteAsync(id);
            if (!result)
                return NotFound($"Item-supplier relationship with ID {id} not found");

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting item-supplier relationship {Id}", id);
            return StatusCode(500, "An error occurred while deleting the item-supplier relationship");
        }
    }
}

