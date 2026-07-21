using System.ComponentModel.DataAnnotations;
using AutoMapper;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers;

/// <summary>
/// API Controller for managing inventory items
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InventoryItemsController : ControllerBase
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IInventoryItemRepository _inventoryItemRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly IInventoryLocationRepository _inventoryLocationRepository;
    private readonly IInventoryAllocationRepository _inventoryAllocationRepository;
    private readonly IWarehouseLocationRepository _warehouseLocationRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IWarehouseQuantityRepository _warehouseQuantityRepository;
    private readonly IInventoryMovementRepository _inventoryMovementRepository;
    private readonly IInventoryBalanceRepository _inventoryBalanceRepository;
    private readonly IItemUnitOfMeasureRepository _itemUnitOfMeasureRepository;
    private readonly IUnitOfMeasureScheduleRepository _uomScheduleRepository;
    private readonly IMapper _mapper;
    private readonly ILogger<InventoryItemsController> _logger;
    private readonly IProcurementMasterDataChangeService? _masterDataChanges;

    public InventoryItemsController(
        ICurrentUserProvider currentUserProvider,
        IInventoryItemRepository inventoryItemRepository,
        IStockMovementRepository stockMovementRepository,
        IInventoryLocationRepository inventoryLocationRepository,
        IInventoryAllocationRepository inventoryAllocationRepository,
        IWarehouseLocationRepository warehouseLocationRepository,
        IWarehouseRepository warehouseRepository,
        IWarehouseQuantityRepository warehouseQuantityRepository,
        IInventoryMovementRepository inventoryMovementRepository,
        IInventoryBalanceRepository inventoryBalanceRepository,
        IItemUnitOfMeasureRepository itemUnitOfMeasureRepository,
        IUnitOfMeasureScheduleRepository uomScheduleRepository,
        IMapper mapper,
        ILogger<InventoryItemsController> logger,
        IProcurementMasterDataChangeService? masterDataChanges = null)
    {
        _currentUserProvider = currentUserProvider;
        _inventoryItemRepository = inventoryItemRepository;
        _stockMovementRepository = stockMovementRepository;
        _inventoryLocationRepository = inventoryLocationRepository;
        _inventoryAllocationRepository = inventoryAllocationRepository;
        _warehouseLocationRepository = warehouseLocationRepository;
        _warehouseRepository = warehouseRepository;
        _warehouseQuantityRepository = warehouseQuantityRepository;
        _inventoryMovementRepository = inventoryMovementRepository;
        _inventoryBalanceRepository = inventoryBalanceRepository;
        _itemUnitOfMeasureRepository = itemUnitOfMeasureRepository;
        _uomScheduleRepository = uomScheduleRepository;
        _mapper = mapper;
        _logger = logger;
        _masterDataChanges = masterDataChanges;
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
    public async Task<ActionResult<InventoryItemDto>> UpdateInventoryItem(Guid id, [FromBody] UpdateInventoryItemDto updateDto)
    {
        try
        {
            var protection = await GuardDirectMutationAsync(id, "InventoryItem.Update");
            if (protection is not null) return protection;
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

            // Handle IsActive -> Status conversion
            existingItem.Status = updateDto.IsActive ? ItemStatus.Active : ItemStatus.Inactive;

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
            var protection = await GuardDirectMutationAsync(id, "InventoryItem.Delete");
            if (protection is not null) return protection;
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
    /// Bin-level stock snapshot (InventoryItem x WarehouseLocation).
    /// Backed by InventoryLocations (operational bin quantities).
    /// </summary>
    [HttpGet("bin-stock")]
    public async Task<ActionResult<PagedResult<BinStockDto>>> GetBinStock(
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] Guid? locationId = null,
        [FromQuery] string? search = null,
        [FromQuery] bool includeZero = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 200);

            var tenantId = _currentUserProvider.TenantId;
            if (tenantId == Guid.Empty)
            {
                tenantId = DefaultTenantId;
            }

            IQueryable<InventoryLocation> query = _inventoryLocationRepository
                .GetQueryable(il => il.TenantId == tenantId)
                .AsNoTracking()
                .Include(il => il.InventoryItem)
                .Include(il => il.Location)
                .ThenInclude(l => l.Warehouse);

            if (warehouseId.HasValue)
            {
                query = query.Where(il => il.Location.WarehouseId == warehouseId.Value);
            }

            if (locationId.HasValue)
            {
                query = query.Where(il => il.LocationId == locationId.Value);
            }

            if (!includeZero)
            {
                query = query.Where(il => il.Quantity != 0);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                var like = $"%{term}%";
                query = query.Where(il =>
                    EF.Functions.Like(il.InventoryItem.ItemCode, like) ||
                    EF.Functions.Like(il.InventoryItem.Name, like) ||
                    EF.Functions.Like(il.Location.LocationCode, like) ||
                    EF.Functions.Like(il.Location.Warehouse.Name, like) ||
                    EF.Functions.Like(il.Location.Warehouse.Code, like));
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderBy(il => il.Location.Warehouse.Name)
                .ThenBy(il => il.Location.LocationCode)
                .ThenBy(il => il.InventoryItem.ItemCode)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(il => new BinStockDto
                {
                    InventoryItemId = il.InventoryItemId,
                    ItemCode = il.InventoryItem.ItemCode,
                    ItemName = il.InventoryItem.Name,
                    UnitOfMeasure = il.InventoryItem.UnitOfMeasure,

                    WarehouseId = il.Location.WarehouseId,
                    WarehouseCode = il.Location.Warehouse.Code,
                    WarehouseName = il.Location.Warehouse.Name,

                    LocationId = il.LocationId,
                    LocationCode = il.Location.LocationCode,

                    Quantity = il.Quantity,
                    AvailableQuantity = il.AvailableQuantity,
                    AllocatedQuantity = il.AllocatedQuantity,
                    AverageCost = il.AverageCost,
                    LastMovementDate = il.LastMovementDate
                })
                .ToListAsync();

            return Ok(new PagedResult<BinStockDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving bin stock snapshot");
            return StatusCode(500, "An error occurred while retrieving bin stock");
        }
    }

    /// <summary>
    /// Reconcile InventoryItems/WarehouseQuantities totals from InventoryMovements (source of truth).
    /// Use this to fix drift when older flows didn't keep totals updated.
    /// </summary>
    [HttpPost("reconcile-stock")]
    public async Task<ActionResult<ApiResponse<object>>> ReconcileStockTotals(
        [FromQuery] bool dryRun = false,
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] Guid? inventoryItemId = null)
    {
        try
        {
            var tenantId = _currentUserProvider.TenantId;
            if (tenantId == Guid.Empty)
            {
                tenantId = DefaultTenantId;
            }

            var consignmentWarehouseIds = await _warehouseRepository
                .GetQueryable(w => w.TenantId == tenantId && w.IsConsignmentWarehouse && !w.IsDeleted)
                .AsNoTracking()
                .Select(w => w.Id)
                .ToListAsync();
            var consignmentWarehouseIdSet = consignmentWarehouseIds.ToHashSet();

            IQueryable<InventoryMovement> movementQuery = _inventoryMovementRepository
                .GetQueryable(m => m.TenantId == tenantId && m.IsPosted)
                .AsNoTracking();

            if (warehouseId.HasValue && warehouseId.Value != Guid.Empty)
            {
                movementQuery = movementQuery.Where(m => m.WarehouseId == warehouseId.Value);
            }

            if (inventoryItemId.HasValue && inventoryItemId.Value != Guid.Empty)
            {
                movementQuery = movementQuery.Where(m => m.InventoryItemId == inventoryItemId.Value);
            }

            var movementTotals = await movementQuery
                .GroupBy(m => new { m.InventoryItemId, m.WarehouseId })
                .Select(g => new
                {
                    g.Key.InventoryItemId,
                    g.Key.WarehouseId,
                    QuantityOnHand = g.Sum(m => m.Direction == MovementDirection.In ? m.Quantity : -m.Quantity)
                })
                .ToListAsync();

            // Average cost from valuation balances (per item/warehouse) - best effort.
            IQueryable<InventoryBalance> balanceQuery = _inventoryBalanceRepository
                .GetQueryable(b => b.TenantId == tenantId)
                .AsNoTracking();

            if (warehouseId.HasValue && warehouseId.Value != Guid.Empty)
            {
                balanceQuery = balanceQuery.Where(b => b.WarehouseId == warehouseId.Value);
            }

            if (inventoryItemId.HasValue && inventoryItemId.Value != Guid.Empty)
            {
                balanceQuery = balanceQuery.Where(b => b.InventoryItemId == inventoryItemId.Value);
            }

            var balanceAverages = await balanceQuery
                .GroupBy(b => new { b.InventoryItemId, b.WarehouseId })
                .Select(g => new
                {
                    g.Key.InventoryItemId,
                    g.Key.WarehouseId,
                    TotalQty = g.Sum(x => x.QuantityOnHand),
                    TotalValue = g.Sum(x => x.TotalValue)
                })
                .ToListAsync();

            var avgCostByItemWarehouse = balanceAverages
                .ToDictionary(
                    x => (x.InventoryItemId, x.WarehouseId),
                    x => x.TotalQty != 0 ? (x.TotalValue / x.TotalQty) : 0m);

            // Load existing warehouse quantities for the scope and reconcile.
            IQueryable<WarehouseQuantity> wqQuery = _warehouseQuantityRepository
                .GetQueryable(q => q.TenantId == tenantId);

            if (warehouseId.HasValue && warehouseId.Value != Guid.Empty)
            {
                wqQuery = wqQuery.Where(q => q.WarehouseId == warehouseId.Value);
            }

            if (inventoryItemId.HasValue && inventoryItemId.Value != Guid.Empty)
            {
                wqQuery = wqQuery.Where(q => q.InventoryItemId == inventoryItemId.Value);
            }

            var existingWarehouseQuantities = await wqQuery.ToListAsync();
            var wqMap = existingWarehouseQuantities.ToDictionary(q => (q.InventoryItemId, q.WarehouseId));
            var movementKeys = movementTotals
                .Select(mt => (mt.InventoryItemId, mt.WarehouseId))
                .ToHashSet();

            var createdWarehouseQuantities = 0;
            var updatedWarehouseQuantities = 0;

            foreach (var mt in movementTotals)
            {
                var key = (mt.InventoryItemId, mt.WarehouseId);
                var computedQty = mt.QuantityOnHand;
                var avgCost = avgCostByItemWarehouse.TryGetValue(key, out var ac) ? ac : 0m;

                if (!wqMap.TryGetValue(key, out var wq))
                {
                    wq = new WarehouseQuantity
                    {
                        TenantId = tenantId,
                        InventoryItemId = mt.InventoryItemId,
                        WarehouseId = mt.WarehouseId,
                        CurrentStock = computedQty,
                        AllocatedStock = 0,
                        AvailableStock = computedQty,
                        AverageCost = avgCost > 0 ? avgCost : 0,
                        LastMovementDate = DateTime.UtcNow,
                        CreatedById = _currentUserProvider.UserId
                    };

                    if (!dryRun)
                    {
                        await _warehouseQuantityRepository.AddAsync(wq);
                    }

                    wqMap[key] = wq;
                    createdWarehouseQuantities++;
                    continue;
                }

                // Preserve allocations; recompute available based on current/allocated.
                wq.CurrentStock = computedQty;
                wq.AvailableStock = computedQty - wq.AllocatedStock;
                wq.LastMovementDate = DateTime.UtcNow;
                if (avgCost > 0)
                {
                    wq.AverageCost = avgCost;
                }
                wq.LastModifiedById = _currentUserProvider.UserId;

                if (!dryRun)
                {
                    await _warehouseQuantityRepository.UpdateAsync(wq);
                }

                updatedWarehouseQuantities++;
            }

            // For existing WarehouseQuantities with no movements in scope, force them to 0 so drift can't persist.
            foreach (var wq in existingWarehouseQuantities)
            {
                var key = (wq.InventoryItemId, wq.WarehouseId);
                if (movementKeys.Contains(key))
                {
                    continue;
                }

                wq.CurrentStock = 0;
                wq.AvailableStock = 0 - wq.AllocatedStock;
                wq.LastMovementDate = DateTime.UtcNow;
                wq.LastModifiedById = _currentUserProvider.UserId;

                if (!dryRun)
                {
                    await _warehouseQuantityRepository.UpdateAsync(wq);
                }

                updatedWarehouseQuantities++;
            }

            // Recompute item totals from warehouse quantities.
            // Only touch items that appear in the scoped movement totals (or the filter item if provided).
            var itemIdsToRecalc = inventoryItemId.HasValue && inventoryItemId.Value != Guid.Empty
                ? new List<Guid> { inventoryItemId.Value }
                : movementTotals.Select(x => x.InventoryItemId)
                    .Concat(existingWarehouseQuantities.Select(q => q.InventoryItemId))
                    .Distinct()
                    .ToList();

            var updatedInventoryItems = 0;
            var items = await _inventoryItemRepository
                .GetQueryable(i => i.TenantId == tenantId && itemIdsToRecalc.Contains(i.Id))
                .ToListAsync();

            // Overall average cost from balances per item (across warehouses/locations).
            var ownedBalanceQuery = balanceQuery.Where(b => !consignmentWarehouseIdSet.Contains(b.WarehouseId));

            var itemAvgCosts = await ownedBalanceQuery
                .GroupBy(b => b.InventoryItemId)
                .Select(g => new
                {
                    InventoryItemId = g.Key,
                    TotalQty = g.Sum(x => x.QuantityOnHand),
                    TotalValue = g.Sum(x => x.TotalValue)
                })
                .ToListAsync();

            var avgCostByItem = itemAvgCosts.ToDictionary(
                x => x.InventoryItemId,
                x => x.TotalQty != 0 ? (x.TotalValue / x.TotalQty) : 0m);

            foreach (var item in items)
            {
                var totalOnHand = wqMap
                    .Where(kvp => kvp.Key.InventoryItemId == item.Id && !consignmentWarehouseIdSet.Contains(kvp.Key.WarehouseId))
                    .Sum(kvp => kvp.Value.CurrentStock);

                item.CurrentStock = totalOnHand;
                item.AvailableStock = totalOnHand - item.AllocatedStock;
                item.LastModifiedById = _currentUserProvider.UserId;
                item.UpdatedAt = DateTime.UtcNow;

                if (avgCostByItem.TryGetValue(item.Id, out var itemAvg) && itemAvg > 0)
                {
                    item.AverageCost = itemAvg;
                }

                if (!dryRun)
                {
                    await _inventoryItemRepository.UpdateAsync(item);
                }

                updatedInventoryItems++;
            }

            if (!dryRun)
            {
                // One SaveChanges is enough - all repositories share the same DbContext in this request scope.
                await _inventoryItemRepository.SaveChangesAsync();
            }

            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                TenantId = tenantId,
                DryRun = dryRun,
                Scope = new { WarehouseId = warehouseId, InventoryItemId = inventoryItemId },
                MovementGroups = movementTotals.Count,
                WarehouseQuantities = new { Created = createdWarehouseQuantities, Updated = updatedWarehouseQuantities },
                InventoryItems = new { Updated = updatedInventoryItems }
            }, dryRun ? "Dry-run completed (no changes saved)." : "Stock totals reconciled successfully."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reconciling stock totals from movements");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("An error occurred while reconciling stock totals."));
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
    /// Get available units of measure for an inventory item
    /// </summary>
    [HttpGet("{id:guid}/units")]
    public async Task<ActionResult<IEnumerable<ItemUnitOfMeasureDto>>> GetItemUnitsOfMeasure(Guid id)
    {
        try
        {
            var item = await _inventoryItemRepository.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound($"Inventory item with ID {id} not found");
            }

            var unitDtos = new List<ItemUnitOfMeasureDto>();

            // Check if item has a UOM Schedule - load it separately
            if (item.UnitOfMeasureScheduleId.HasValue)
            {
                // Load the schedule with details
                var schedule = await _uomScheduleRepository.GetWithDetailsAsync(item.UnitOfMeasureScheduleId.Value);
                
                if (schedule != null)
                {
                    // Add base unit
                    if (schedule.BaseUnitOfMeasure != null)
                    {
                        unitDtos.Add(new ItemUnitOfMeasureDto
                        {
                            Id = Guid.NewGuid(), // Temporary ID for schedule-based UOMs
                            UnitOfMeasureId = schedule.BaseUnitOfMeasureId,
                            UnitCode = schedule.BaseUnitOfMeasure.Code,
                            UnitName = schedule.BaseUnitOfMeasure.Name,
                            ConversionFactor = 1.0m,
                            IsBaseUnit = true,
                            IsPurchaseUnit = true,
                            IsSalesUnit = true,
                            Barcode = null
                        });
                    }

                    // Add units from schedule details
                    if (schedule.Details != null && schedule.Details.Any())
                    {
                        foreach (var detail in schedule.Details.Where(d => !d.IsDeleted))
                        {
                            if (detail.UnitOfMeasure != null)
                            {
                                unitDtos.Add(new ItemUnitOfMeasureDto
                                {
                                    Id = detail.Id,
                                    UnitOfMeasureId = detail.UnitOfMeasureId,
                                    UnitCode = detail.UnitOfMeasure.Code,
                                    UnitName = detail.UnitOfMeasure.Name,
                                    ConversionFactor = detail.BaseQuantity,
                                    IsBaseUnit = false,
                                    IsPurchaseUnit = true,
                                    IsSalesUnit = true,
                                    Barcode = null
                                });
                            }
                        }
                    }
                }
            }
            else
            {
                // Get UOMs from ItemUnitOfMeasures table
                var units = await _itemUnitOfMeasureRepository.GetByItemAsync(id);
                unitDtos = units.Select(u => new ItemUnitOfMeasureDto
                {
                    Id = u.Id,
                    UnitOfMeasureId = u.UnitOfMeasureId,
                    UnitCode = u.UnitOfMeasure?.Code ?? "",
                    UnitName = u.UnitOfMeasure?.Name ?? "",
                    ConversionFactor = u.ConversionToBase,
                    IsBaseUnit = u.IsBaseUnit,
                    IsPurchaseUnit = u.IsPurchaseUnit,
                    IsSalesUnit = u.IsSalesUnit,
                    Barcode = u.Barcode
                }).ToList();
            }

            // If no UOMs found, create a default one from the item's base UOM
            if (!unitDtos.Any() && !string.IsNullOrEmpty(item.UnitOfMeasure))
            {
                unitDtos.Add(new ItemUnitOfMeasureDto
                {
                    Id = Guid.NewGuid(),
                    UnitOfMeasureId = Guid.Empty,
                    UnitCode = item.UnitOfMeasure,
                    UnitName = item.UnitOfMeasure,
                    ConversionFactor = 1.0m,
                    IsBaseUnit = true,
                    IsPurchaseUnit = true,
                    IsSalesUnit = true,
                    Barcode = null
                });
            }

            return Ok(unitDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving units of measure for item {ItemId}", id);
            return StatusCode(500, "An error occurred while retrieving units of measure");
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
    /// Helper method to get tenant ID from the current user context.
    /// Falls back to the seeded default tenant for authenticated requests that
    /// do not carry a tenant claim.
    /// </summary>
    private async Task<ObjectResult?> GuardDirectMutationAsync(Guid id, string action)
    {
        if (_masterDataChanges is null) return null;
        var decision = await _masterDataChanges.CheckDirectMutationAsync(
            new[] { ProcurementMasterDataResourceType.InventoryItem }, id, action,
            HttpContext.TraceIdentifier, HttpContext.RequestAborted);
        return decision.Allowed ? null : Conflict(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Staged inventory-item change required",
            Detail = decision.Message,
            Instance = HttpContext.Request.Path,
            Extensions = { ["code"] = decision.Code, ["correlationId"] = decision.CorrelationId, ["policyId"] = decision.PolicyId }
        });
    }

    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
        {
            _logger.LogWarning(
                "TenantId was empty while processing inventory item request. Falling back to default tenant {DefaultTenantId}",
                DefaultTenantId);
            tenantId = DefaultTenantId;
        }

        return tenantId;
    }
}

/// <summary>
/// DTO for item unit of measure
/// </summary>
public class ItemUnitOfMeasureDto
{
    public Guid Id { get; set; }
    public Guid UnitOfMeasureId { get; set; }
    public string UnitCode { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;
    public decimal ConversionFactor { get; set; }
    public bool IsBaseUnit { get; set; }
    public bool IsPurchaseUnit { get; set; }
    public bool IsSalesUnit { get; set; }
    public string? Barcode { get; set; }
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
