using System.ComponentModel.DataAnnotations;
using AutoMapper;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers;

/// <summary>
/// API Controller for managing inventory items
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InventoryItemsController : ControllerBase
{
    private readonly IInventoryItemRepository _inventoryItemRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly IInventoryLocationRepository _inventoryLocationRepository;
    private readonly IInventoryAllocationRepository _inventoryAllocationRepository;
    private readonly IWarehouseLocationRepository _warehouseLocationRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IWarehouseQuantityRepository _warehouseQuantityRepository;
    private readonly IMapper _mapper;
    private readonly ILogger<InventoryItemsController> _logger;

    public InventoryItemsController(
        IInventoryItemRepository inventoryItemRepository,
        IStockMovementRepository stockMovementRepository,
        IInventoryLocationRepository inventoryLocationRepository,
        IInventoryAllocationRepository inventoryAllocationRepository,
        IWarehouseLocationRepository warehouseLocationRepository,
        IWarehouseRepository warehouseRepository,
        IWarehouseQuantityRepository warehouseQuantityRepository,
        IMapper mapper,
        ILogger<InventoryItemsController> logger)
    {
        _inventoryItemRepository = inventoryItemRepository;
        _stockMovementRepository = stockMovementRepository;
        _inventoryLocationRepository = inventoryLocationRepository;
        _inventoryAllocationRepository = inventoryAllocationRepository;
        _warehouseLocationRepository = warehouseLocationRepository;
        _warehouseRepository = warehouseRepository;
        _warehouseQuantityRepository = warehouseQuantityRepository;
        _mapper = mapper;
        _logger = logger;
    }

    /// <summary>
    /// Get all active inventory items, optionally filtered by item type
    /// </summary>
    /// <param name="itemType">Optional item type filter (1=StockItem, 2=Service, 3=NonStock, 4=FixedAsset)</param>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<InventoryItemDto>>> GetInventoryItems([FromQuery] ItemType? itemType = null)
    {
        try
        {
            var items = (await _inventoryItemRepository.GetActiveItemsAsync()).ToList();

            // Filter by item type if specified
            if (itemType.HasValue)
            {
                items = items.Where(i => i.ItemType == itemType.Value).ToList();
            }

            var itemDtos = _mapper.Map<IEnumerable<InventoryItemDto>>(items);
            return Ok(itemDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving inventory items. ItemType filter: {ItemType}", itemType);
            return StatusCode(500, "An error occurred while retrieving inventory items");
        }
    }

    /// <summary>
    /// Get inventory item by ID with full details
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InventoryItemDetailDto>> GetInventoryItem(Guid id)
    {
        try
        {
            var item = await _inventoryItemRepository.GetByIdWithDetailsAsync(id);
            if (item == null)
            {
                return NotFound($"Inventory item with ID {id} not found");
            }

            var itemDto = _mapper.Map<InventoryItemDetailDto>(item);
            return Ok(itemDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving inventory item {ItemId}", id);
            return StatusCode(500, "An error occurred while retrieving the inventory item");
        }
    }

    /// <summary>
    /// Get inventory item by item code
    /// </summary>
    [HttpGet("by-code/{itemCode}")]
    public async Task<ActionResult<InventoryItemDto>> GetInventoryItemByCode(string itemCode)
    {
        try
        {
            var item = await _inventoryItemRepository.GetByItemCodeAsync(itemCode);
            if (item == null)
            {
                return NotFound($"Inventory item with code '{itemCode}' not found");
            }

            var itemDto = _mapper.Map<InventoryItemDto>(item);
            return Ok(itemDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving inventory item by code {ItemCode}", itemCode);
            return StatusCode(500, "An error occurred while retrieving the inventory item");
        }
    }

    /// <summary>
    /// Search inventory items
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<InventoryItemDto>>> SearchInventoryItems([Required] string searchTerm)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return BadRequest("Search term is required");
            }

            var items = await _inventoryItemRepository.SearchItemsAsync(searchTerm);
            var itemDtos = _mapper.Map<IEnumerable<InventoryItemDto>>(items);
            return Ok(itemDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching inventory items with term {SearchTerm}", searchTerm);
            return StatusCode(500, "An error occurred while searching inventory items");
        }
    }

    /// <summary>
    /// Get inventory items by category
    /// </summary>
    [HttpGet("by-category/{categoryId:guid}")]
    public async Task<ActionResult<IEnumerable<InventoryItemDto>>> GetInventoryItemsByCategory(Guid categoryId)
    {
        try
        {
            var items = await _inventoryItemRepository.GetItemsByCategoryAsync(categoryId);
            var itemDtos = _mapper.Map<IEnumerable<InventoryItemDto>>(items);
            return Ok(itemDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving inventory items for category {CategoryId}", categoryId);
            return StatusCode(500, "An error occurred while retrieving inventory items");
        }
    }

    /// <summary>
    /// Get items below reorder level
    /// </summary>
    [HttpGet("reorder-required")]
    public async Task<ActionResult<IEnumerable<ReorderRequiredDto>>> GetItemsBelowReorderLevel()
    {
        try
        {
            var items = await _inventoryItemRepository.GetItemsBelowReorderLevelAsync();
            var reorderDtos = items.Select(item => new ReorderRequiredDto
            {
                InventoryItemId = item.Id,
                ItemCode = item.ItemCode,
                ItemName = item.Name,
                CurrentStock = item.CurrentStock,
                AvailableStock = item.AvailableStock,
                AllocatedStock = item.AllocatedStock,
                ReorderLevel = item.ReorderLevel,
                ReorderQuantity = item.ReorderQuantity,
                RecommendedOrderQuantity = Math.Max(item.ReorderQuantity, item.ReorderLevel - item.CurrentStock),
                PrimarySupplier = item.PrimarySupplier,
                LeadTimeDays = item.LeadTimeDays,
                LastPurchaseDate = item.LastPurchaseDate
            });

            return Ok(reorderDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving items below reorder level");
            return StatusCode(500, "An error occurred while retrieving reorder required items");
        }
    }

    /// <summary>
    /// Check stock availability
    /// </summary>
    [HttpPost("check-availability")]
    public async Task<ActionResult<IEnumerable<StockAvailabilityDto>>> CheckStockAvailability([FromBody] List<StockAvailabilityCheckDto> items)
    {
        try
        {
            var results = new List<StockAvailabilityDto>();

            foreach (var checkItem in items)
            {
                var item = await _inventoryItemRepository.GetByIdAsync(checkItem.InventoryItemId);
                if (item != null)
                {
                    var availabilityDto = new StockAvailabilityDto
                    {
                        InventoryItemId = item.Id,
                        ItemCode = item.ItemCode,
                        ItemName = item.Name,
                        RequiredQuantity = checkItem.RequiredQuantity,
                        CurrentStock = item.CurrentStock,
                        AvailableQuantity = item.AvailableStock,
                        AllocatedQuantity = item.AllocatedStock,
                        OnOrderQuantity = item.OnOrderStock,
                        IsAvailable = item.AvailableStock >= checkItem.RequiredQuantity,
                        ReorderRequired = item.CurrentStock <= item.ReorderLevel,
                        ReorderLevel = item.ReorderLevel,
                        ReorderQuantity = item.ReorderQuantity
                    };

                    if (!availabilityDto.IsAvailable)
                    {
                        availabilityDto.Message = $"Insufficient stock. Required: {checkItem.RequiredQuantity}, Available: {item.AvailableStock}";
                    }
                    else
                    {
                        availabilityDto.Message = "Stock available";
                    }

                    results.Add(availabilityDto);
                }
            }

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking stock availability");
            return StatusCode(500, "An error occurred while checking stock availability");
        }
    }

    /// <summary>
    /// Create a new inventory item
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<InventoryItemDto>> CreateInventoryItem([FromBody] CreateInventoryItemDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Check if item code already exists
            var existingItem = await _inventoryItemRepository.GetByItemCodeAsync(createDto.ItemCode);
            if (existingItem != null)
            {
                return Conflict($"Inventory item with code '{createDto.ItemCode}' already exists");
            }

            var inventoryItem = _mapper.Map<InventoryItem>(createDto);
            inventoryItem.TenantId = GetTenantId(); // Assuming you have a method to get tenant ID

            var createdItem = await _inventoryItemRepository.AddAsync(inventoryItem);
            await _inventoryItemRepository.SaveChangesAsync();

            var itemDto = _mapper.Map<InventoryItemDto>(createdItem);
            return CreatedAtAction(nameof(GetInventoryItem), new { id = createdItem.Id }, itemDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating inventory item");
            return StatusCode(500, "An error occurred while creating the inventory item");
        }
    }

    /// <summary>
    /// Update an existing inventory item
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<InventoryItemDto>> UpdateInventoryItem(Guid id, [FromBody] CreateInventoryItemDto updateDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var existingItem = await _inventoryItemRepository.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound($"Inventory item with ID {id} not found");
            }

            // Check if item code is being changed and if the new code already exists
            if (existingItem.ItemCode != updateDto.ItemCode)
            {
                var duplicateItem = await _inventoryItemRepository.GetByItemCodeAsync(updateDto.ItemCode);
                if (duplicateItem != null && duplicateItem.Id != id)
                {
                    return Conflict($"Inventory item with code '{updateDto.ItemCode}' already exists");
                }
            }

            _mapper.Map(updateDto, existingItem);
            await _inventoryItemRepository.UpdateAsync(existingItem);
            await _inventoryItemRepository.SaveChangesAsync();

            var itemDto = _mapper.Map<InventoryItemDto>(existingItem);
            return Ok(itemDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating inventory item {ItemId}", id);
            return StatusCode(500, "An error occurred while updating the inventory item");
        }
    }

    /// <summary>
    /// Delete an inventory item (soft delete)
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteInventoryItem(Guid id)
    {
        try
        {
            var item = await _inventoryItemRepository.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound($"Inventory item with ID {id} not found");
            }

            // Check if item has stock or is allocated
            if (item.CurrentStock > 0)
            {
                return BadRequest("Cannot delete item with current stock. Please adjust stock to zero first.");
            }

            var activeAllocations = await _inventoryAllocationRepository.GetAllocationsByItemAsync(id);
            if (activeAllocations.Any(a => a.Status == "Active"))
            {
                return BadRequest("Cannot delete item with active allocations.");
            }

            await _inventoryItemRepository.DeleteAsync(id);
            await _inventoryItemRepository.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting inventory item {ItemId}", id);
            return StatusCode(500, "An error occurred while deleting the inventory item");
        }
    }

    /// <summary>
    /// Get stock movements for an inventory item
    /// </summary>
    [HttpGet("{id:guid}/movements")]
    public async Task<ActionResult<IEnumerable<StockMovementDto>>> GetInventoryItemMovements(Guid id)
    {
        try
        {
            var item = await _inventoryItemRepository.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound($"Inventory item with ID {id} not found");
            }

            var movements = await _stockMovementRepository.GetMovementsByItemAsync(id);
            var movementDtos = _mapper.Map<IEnumerable<StockMovementDto>>(movements);
            return Ok(movementDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving stock movements for item {ItemId}", id);
            return StatusCode(500, "An error occurred while retrieving stock movements");
        }
    }

    /// <summary>
    /// Get inventory locations for an item
    /// </summary>
    [HttpGet("{id:guid}/locations")]
    public async Task<ActionResult<IEnumerable<InventoryLocationDto>>> GetInventoryItemLocations(Guid id)
    {
        try
        {
            var item = await _inventoryItemRepository.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound($"Inventory item with ID {id} not found");
            }

            var locations = await _inventoryLocationRepository.GetByInventoryItemAsync(id);
            var locationDtos = _mapper.Map<IEnumerable<InventoryLocationDto>>(locations);
            return Ok(locationDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving inventory locations for item {ItemId}", id);
            return StatusCode(500, "An error occurred while retrieving inventory locations");
        }
    }

    /// <summary>
    /// Get allocations for an inventory item
    /// </summary>
    [HttpGet("{id:guid}/allocations")]
    public async Task<ActionResult<IEnumerable<InventoryAllocationDto>>> GetInventoryItemAllocations(Guid id)
    {
        try
        {
            var item = await _inventoryItemRepository.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound($"Inventory item with ID {id} not found");
            }

            var allocations = await _inventoryAllocationRepository.GetAllocationsByItemAsync(id);
            var allocationDtos = _mapper.Map<IEnumerable<InventoryAllocationDto>>(allocations);
            return Ok(allocationDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving allocations for item {ItemId}", id);
            return StatusCode(500, "An error occurred while retrieving inventory allocations");
        }
    }

    /// <summary>
    /// Get all warehouses
    /// </summary>
    [HttpGet("warehouses")]
    public async Task<ActionResult<IEnumerable<WarehouseDto>>> GetWarehouses()
    {
        try
        {
            var warehouses = await _warehouseRepository.GetActiveWarehousesAsync();
            var warehouseDtos = _mapper.Map<IEnumerable<WarehouseDto>>(warehouses);
            return Ok(warehouseDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving warehouses");
            return StatusCode(500, "An error occurred while retrieving warehouses");
        }
    }

    /// <summary>
    /// Get inventory items by warehouse with available stock, optionally filtered by item type
    /// </summary>
    /// <param name="warehouseId">The warehouse ID</param>
    /// <param name="itemType">Optional item type filter (1=Consumable, 4=Tool)</param>
    [HttpGet("by-warehouse/{warehouseId:guid}")]
    public async Task<ActionResult<IEnumerable<WarehouseInventoryDto>>> GetInventoryByWarehouse(
        Guid warehouseId,
        [FromQuery] int? itemType = null)
    {
        try
        {
            var warehouse = await _warehouseRepository.GetByIdAsync(warehouseId);
            if (warehouse == null)
            {
                return NotFound($"Warehouse with ID {warehouseId} not found");
            }

            var warehouseQuantities = await _warehouseQuantityRepository.GetItemsWithStockAsync(warehouseId, itemType);

            var inventoryDtos = warehouseQuantities.Select(wq => new WarehouseInventoryDto
            {
                InventoryItemId = wq.InventoryItemId,
                ItemCode = wq.InventoryItem.ItemCode,
                ItemName = wq.InventoryItem.Name,
                ItemType = (int)wq.InventoryItem.ItemType,
                Description = wq.InventoryItem.Description,
                UnitOfMeasure = wq.InventoryItem.UnitOfMeasure,
                CurrentStock = wq.CurrentStock,
                AvailableStock = wq.AvailableStock,
                AllocatedStock = wq.AllocatedStock,
                UnitCost = wq.InventoryItem.StandardCost,
                DailyRentalRate = wq.InventoryItem.DailyRentalRate,
                CategoryName = wq.InventoryItem.Category?.Name
            });

            return Ok(inventoryDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving inventory for warehouse {WarehouseId}", warehouseId);
            return StatusCode(500, "An error occurred while retrieving warehouse inventory");
        }
    }

    /// <summary>
    /// Get all warehouse locations
    /// </summary>
    [HttpGet("warehouse-locations")]
    public async Task<ActionResult<IEnumerable<WarehouseLocationDto>>> GetWarehouseLocations([FromQuery] Guid? warehouseId = null)
    {
        try
        {
            IEnumerable<WarehouseLocation> locations;

            if (warehouseId.HasValue)
            {
                locations = await _warehouseLocationRepository.GetLocationsByWarehouseAsync(warehouseId.Value);
            }
            else
            {
                locations = await _warehouseLocationRepository.GetAllAsync();
            }

            var locationDtos = _mapper.Map<IEnumerable<WarehouseLocationDto>>(locations);
            return Ok(locationDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving warehouse locations");
            return StatusCode(500, "An error occurred while retrieving warehouse locations");
        }
    }

    /// <summary>
    /// Helper method to get tenant ID from claims
    /// </summary>
    private static Guid GetTenantId()
    {
        // Implementation would extract tenant ID from JWT claims or session
        // For now, returning a placeholder
        return Guid.Empty;
    }
}

/// <summary>
/// DTO for stock availability check requests
/// </summary>
public class StockAvailabilityCheckDto
{
    [Required]
    public Guid InventoryItemId { get; set; }

    [Required]
    public decimal RequiredQuantity { get; set; }
}

/// <summary>
/// DTO for warehouse inventory with quantities
/// </summary>
public class WarehouseInventoryDto
{
    public Guid InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public int ItemType { get; set; }
    public string? Description { get; set; }
    public string? UnitOfMeasure { get; set; }
    public decimal CurrentStock { get; set; }
    public decimal AvailableStock { get; set; }
    public decimal AllocatedStock { get; set; }
    public decimal UnitCost { get; set; }
    public decimal DailyRentalRate { get; set; }
    public string? CategoryName { get; set; }
}
