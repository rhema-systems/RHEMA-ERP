using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Inventory;

/// <summary>
/// Main inventory management service - handles all inventory operations across the ERP system
/// This service is used by Maintenance, Production, Sales, and other modules
/// </summary>
public class InventoryManagementService : IInventoryManagementService
{
    private readonly IInventoryItemRepository _itemRepository;
    private readonly IStockMovementRepository _movementRepository;
    private readonly IInventoryLocationRepository _locationRepository;
    private readonly IInventoryAllocationRepository _allocationRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly ILogger<InventoryManagementService> _logger;

    public InventoryManagementService(
        IInventoryItemRepository itemRepository,
        IStockMovementRepository movementRepository,
        IInventoryLocationRepository locationRepository,
        IInventoryAllocationRepository allocationRepository,
        IWarehouseRepository warehouseRepository,
        ILogger<InventoryManagementService> logger)
    {
        _itemRepository = itemRepository;
        _movementRepository = movementRepository;
        _locationRepository = locationRepository;
        _allocationRepository = allocationRepository;
        _warehouseRepository = warehouseRepository;
        _logger = logger;
    }

    #region Item Management

    /// <summary>
    /// Gets inventory items suitable for maintenance work orders
    /// </summary>
    public async Task<IEnumerable<InventoryItemDto>> GetMaintenancePartsAsync(string? searchTerm = null)
    {
        try
        {
            var items = await _itemRepository.GetActiveItemsAsync();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                items = items.Where(i =>
                    i.ItemCode.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    i.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    (i.Description?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            return items.Select(item => new InventoryItemDto
            {
                Id = item.Id,
                ItemCode = item.ItemCode,
                Name = item.Name,
                Description = item.Description,
                UnitOfMeasure = item.UnitOfMeasure,
                CurrentStock = item.CurrentStock,
                AvailableStock = item.AvailableStock,
                StandardCost = item.StandardCost,
                AverageCost = item.AverageCost,
                IsSerialTracked = item.IsSerialTracked,
                IsLotTracked = item.IsLotTracked,
                Status = item.Status,
                CategoryName = item.Category?.Name ?? "Uncategorized"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance parts");
            throw;
        }
    }

    /// <summary>
    /// Gets detailed information about a specific inventory item
    /// </summary>
    public async Task<InventoryItemDetailDto?> GetInventoryItemDetailAsync(Guid itemId)
    {
        try
        {
            var item = await _itemRepository.GetByIdWithDetailsAsync(itemId);
            if (item == null)
            {
                return null;
            }

            var locations = await _locationRepository.GetByInventoryItemAsync(itemId);
            var recentMovements = await _movementRepository.GetRecentMovementsAsync(itemId, 50);

            return new InventoryItemDetailDto
            {
                Id = item.Id,
                ItemCode = item.ItemCode,
                Name = item.Name,
                Description = item.Description,
                UnitOfMeasure = item.UnitOfMeasure,
                CurrentStock = item.CurrentStock,
                AvailableStock = item.AvailableStock,
                AllocatedStock = item.AllocatedStock,
                OnOrderStock = item.OnOrderStock,
                MinimumLevel = item.MinimumLevel,
                MaximumLevel = item.MaximumLevel,
                ReorderLevel = item.ReorderLevel,
                ReorderQuantity = item.ReorderQuantity,
                StandardCost = item.StandardCost,
                AverageCost = item.AverageCost,
                LastPurchaseCost = item.LastPurchaseCost,
                IsSerialTracked = item.IsSerialTracked,
                IsLotTracked = item.IsLotTracked,
                Status = item.Status,
                CategoryName = item.Category?.Name ?? "Uncategorized",
                PrimarySupplier = item.PrimarySupplier,
                LeadTimeDays = item.LeadTimeDays,
                LastStockDate = item.LastStockDate,
                LastPurchaseDate = item.LastPurchaseDate,
                Locations = locations.Select(loc => new InventoryLocationDto
                {
                    LocationId = loc.LocationId,
                    LocationCode = loc.Location.LocationCode,
                    LocationName = loc.Location.Name,
                    WarehouseName = loc.Location.Warehouse.Name,
                    Quantity = loc.Quantity,
                    AvailableQuantity = loc.AvailableQuantity,
                    AllocatedQuantity = loc.AllocatedQuantity
                }).ToList(),
                RecentMovements = recentMovements.Select(mov => new StockMovementDto
                {
                    Id = mov.Id,
                    MovementType = mov.MovementType,
                    Quantity = mov.Quantity,
                    UnitCost = mov.UnitCost,
                    MovementDate = mov.MovementDate,
                    ReferenceType = mov.ReferenceType,
                    ReferenceNumber = mov.ReferenceNumber,
                    Notes = mov.Notes,
                    ProcessedBy = mov.ProcessedBy?.FullName
                }).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving inventory item detail for {ItemId}", itemId);
            throw;
        }
    }

    #endregion

    #region Stock Allocation

    /// <summary>
    /// Allocates inventory for a maintenance work order
    /// </summary>
    public async Task<InventoryAllocationDto> AllocateForWorkOrderAsync(AllocateInventoryDto request)
    {
        try
        {
            // Validate inventory item exists and has sufficient stock
            var item = await _itemRepository.GetByIdAsync(request.InventoryItemId) ?? throw new ArgumentException($"Inventory item {request.InventoryItemId} not found");
            if (item.AvailableStock < request.Quantity)
            {
                throw new InvalidOperationException($"Insufficient stock. Available: {item.AvailableStock}, Requested: {request.Quantity}");
            }

            // Find best location for allocation
            var bestLocation = await FindBestAllocationLocationAsync(request.InventoryItemId, request.Quantity) ?? throw new InvalidOperationException("No suitable location found for allocation");

            // Create allocation record
            var allocation = new InventoryAllocation
            {
                InventoryItemId = request.InventoryItemId,
                LocationId = bestLocation.LocationId,
                AllocationType = "WorkOrder",
                ReferenceNumber = request.ReferenceNumber,
                ReferenceId = request.ReferenceId,
                AllocatedQuantity = request.Quantity,
                ConsumedQuantity = 0,
                RemainingQuantity = request.Quantity,
                AllocationDate = DateTime.UtcNow,
                RequiredDate = request.RequiredDate,
                Status = "Active",
                Notes = request.Notes,
                AllocatedById = request.UserId
            };

            var createdAllocation = await _allocationRepository.AddAsync(allocation);

            // Update stock levels
            await UpdateStockLevelsAsync(request.InventoryItemId, 0, request.Quantity);

            // Update location quantities
            await UpdateLocationQuantitiesAsync(bestLocation.LocationId, request.InventoryItemId, 0, request.Quantity);

            // Create stock movement record
            await CreateStockMovementAsync(new StockMovement
            {
                InventoryItemId = request.InventoryItemId,
                MovementType = "Allocation",
                Quantity = request.Quantity,
                UnitCost = item.AverageCost,
                TotalValue = item.AverageCost * request.Quantity,
                ReferenceType = ReferenceType.WO,
                ReferenceNumber = request.ReferenceNumber,
                ReferenceId = request.ReferenceId,
                LocationId = bestLocation.LocationId,
                Notes = $"Allocated for {request.ReferenceNumber}",
                ProcessedById = request.UserId,
                RunningBalance = item.CurrentStock // This would be calculated properly
            });

            return new InventoryAllocationDto
            {
                Id = createdAllocation.Id,
                InventoryItemId = createdAllocation.InventoryItemId,
                ItemCode = item.ItemCode,
                ItemName = item.Name,
                LocationId = createdAllocation.LocationId ?? Guid.Empty,
                LocationCode = bestLocation.Location.LocationCode,
                AllocationType = createdAllocation.AllocationType,
                ReferenceNumber = createdAllocation.ReferenceNumber,
                AllocatedQuantity = createdAllocation.AllocatedQuantity,
                ConsumedQuantity = createdAllocation.ConsumedQuantity,
                RemainingQuantity = createdAllocation.RemainingQuantity,
                AllocationDate = createdAllocation.AllocationDate,
                RequiredDate = createdAllocation.RequiredDate,
                Status = createdAllocation.Status,
                Notes = createdAllocation.Notes
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error allocating inventory for work order");
            throw;
        }
    }

    /// <summary>
    /// Consumes allocated inventory (when parts are actually used)
    /// </summary>
    public async Task<bool> ConsumeAllocatedInventoryAsync(Guid allocationId, decimal quantity, Guid userId)
    {
        try
        {
            var allocation = await _allocationRepository.GetByIdAsync(allocationId) ?? throw new ArgumentException($"Allocation {allocationId} not found");
            if (quantity > allocation.RemainingQuantity)
            {
                throw new InvalidOperationException($"Cannot consume more than remaining quantity. Remaining: {allocation.RemainingQuantity}, Requested: {quantity}");
            }

            // Update allocation
            allocation.ConsumedQuantity += quantity;
            allocation.RemainingQuantity -= quantity;
            if (allocation.RemainingQuantity == 0)
            {
                allocation.Status = "Consumed";
            }

            await _allocationRepository.UpdateAsync(allocation);

            // Update stock levels (reduce current stock)
            await UpdateStockLevelsAsync(allocation.InventoryItemId, -quantity, -quantity);

            // Update location quantities
            if (allocation.LocationId.HasValue)
            {
                await UpdateLocationQuantitiesAsync(allocation.LocationId.Value, allocation.InventoryItemId, -quantity, -quantity);
            }

            // Create consumption movement record
            var item = await _itemRepository.GetByIdAsync(allocation.InventoryItemId);
            await CreateStockMovementAsync(new StockMovement
            {
                InventoryItemId = allocation.InventoryItemId,
                MovementType = "Consumption",
                Quantity = -quantity, // Negative for outbound
                UnitCost = item?.AverageCost ?? 0,
                TotalValue = (item?.AverageCost ?? 0) * -quantity,
                ReferenceType = ReferenceType.WO, // Assuming work order allocation
                ReferenceNumber = allocation.ReferenceNumber,
                ReferenceId = allocation.ReferenceId,
                LocationId = allocation.LocationId,
                Notes = $"Consumed from allocation {allocation.Id}",
                ProcessedById = userId,
                RunningBalance = (item?.CurrentStock ?? 0) - quantity
            });

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error consuming allocated inventory");
            throw;
        }
    }

    /// <summary>
    /// Releases unused allocation (returns to available stock)
    /// </summary>
    public async Task<bool> ReleaseAllocationAsync(Guid allocationId, Guid userId)
    {
        try
        {
            var allocation = await _allocationRepository.GetByIdAsync(allocationId) ?? throw new ArgumentException($"Allocation {allocationId} not found");
            if (allocation.Status != "Active")
            {
                throw new InvalidOperationException($"Cannot release allocation with status {allocation.Status}");
            }

            var remainingQuantity = allocation.RemainingQuantity;

            // Update allocation status
            allocation.Status = "Cancelled";
            allocation.RemainingQuantity = 0;
            await _allocationRepository.UpdateAsync(allocation);

            // Return to available stock
            await UpdateStockLevelsAsync(allocation.InventoryItemId, 0, -remainingQuantity);

            // Update location quantities
            if (allocation.LocationId.HasValue)
            {
                await UpdateLocationQuantitiesAsync(allocation.LocationId.Value, allocation.InventoryItemId, 0, -remainingQuantity);
            }

            // Create movement record
            var item = await _itemRepository.GetByIdAsync(allocation.InventoryItemId);
            await CreateStockMovementAsync(new StockMovement
            {
                InventoryItemId = allocation.InventoryItemId,
                MovementType = "Allocation-Release",
                Quantity = remainingQuantity,
                UnitCost = item?.AverageCost ?? 0,
                TotalValue = (item?.AverageCost ?? 0) * remainingQuantity,
                ReferenceType = ReferenceType.WO, // Assuming work order allocation
                ReferenceNumber = allocation.ReferenceNumber,
                ReferenceId = allocation.ReferenceId,
                LocationId = allocation.LocationId,
                Notes = $"Released allocation {allocation.Id}",
                ProcessedById = userId,
                RunningBalance = item?.CurrentStock ?? 0
            });

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing allocation");
            throw;
        }
    }

    #endregion

    #region Stock Levels and Availability

    /// <summary>
    /// Checks if sufficient stock is available for allocation
    /// </summary>
    public async Task<StockAvailabilityDto> CheckStockAvailabilityAsync(Guid inventoryItemId, decimal requiredQuantity)
    {
        try
        {
            var item = await _itemRepository.GetByIdAsync(inventoryItemId);
            if (item == null)
            {
                return new StockAvailabilityDto
                {
                    InventoryItemId = inventoryItemId,
                    RequiredQuantity = requiredQuantity,
                    AvailableQuantity = 0,
                    IsAvailable = false,
                    Message = "Item not found"
                };
            }

            var isAvailable = item.AvailableStock >= requiredQuantity;
            var message = isAvailable
                ? "Stock available"
                : $"Insufficient stock. Available: {item.AvailableStock}, Required: {requiredQuantity}, Shortage: {requiredQuantity - item.AvailableStock}";

            return new StockAvailabilityDto
            {
                InventoryItemId = inventoryItemId,
                ItemCode = item.ItemCode,
                ItemName = item.Name,
                RequiredQuantity = requiredQuantity,
                CurrentStock = item.CurrentStock,
                AvailableQuantity = item.AvailableStock,
                AllocatedQuantity = item.AllocatedStock,
                OnOrderQuantity = item.OnOrderStock,
                IsAvailable = isAvailable,
                Message = message,
                ReorderRequired = item.CurrentStock <= item.ReorderLevel,
                ReorderLevel = item.ReorderLevel,
                ReorderQuantity = item.ReorderQuantity
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking stock availability for item {ItemId}", inventoryItemId);
            throw;
        }
    }

    /// <summary>
    /// Gets items that need to be reordered
    /// </summary>
    public async Task<IEnumerable<ReorderRequiredDto>> GetItemsRequiringReorderAsync()
    {
        try
        {
            var itemsNeedingReorder = await _itemRepository.GetItemsBelowReorderLevelAsync();

            return itemsNeedingReorder.Select(item => new ReorderRequiredDto
            {
                InventoryItemId = item.Id,
                ItemCode = item.ItemCode,
                ItemName = item.Name,
                CurrentStock = item.CurrentStock,
                AvailableStock = item.AvailableStock,
                AllocatedStock = item.AllocatedStock,
                ReorderLevel = item.ReorderLevel,
                ReorderQuantity = item.ReorderQuantity,
                RecommendedOrderQuantity = CalculateRecommendedOrderQuantity(item),
                PrimarySupplier = item.PrimarySupplier,
                LeadTimeDays = item.LeadTimeDays,
                LastPurchaseDate = item.LastPurchaseDate
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving items requiring reorder");
            throw;
        }
    }

    #endregion

    #region Helper Methods

    private async Task<InventoryLocation?> FindBestAllocationLocationAsync(Guid inventoryItemId, decimal requiredQuantity)
    {
        var locations = await _locationRepository.GetByInventoryItemAsync(inventoryItemId);

        // Prefer locations that are picking locations and have sufficient available quantity
        return locations
            .Where(loc => loc.AvailableQuantity >= requiredQuantity && loc.Location.IsPickingLocation)
            .OrderByDescending(loc => loc.AvailableQuantity) // Prefer locations with more stock
            .FirstOrDefault();
    }

    private async Task UpdateStockLevelsAsync(Guid inventoryItemId, decimal currentStockChange, decimal allocatedStockChange)
    {
        var item = await _itemRepository.GetByIdAsync(inventoryItemId);
        if (item != null)
        {
            item.CurrentStock += currentStockChange;
            item.AllocatedStock += allocatedStockChange;
            item.AvailableStock = item.CurrentStock - item.AllocatedStock;
            item.LastStockDate = DateTime.UtcNow;

            await _itemRepository.UpdateAsync(item);
        }
    }

    private async Task UpdateLocationQuantitiesAsync(Guid locationId, Guid inventoryItemId, decimal quantityChange, decimal allocatedChange)
    {
        var inventoryLocation = await _locationRepository.GetByLocationAndItemAsync(locationId, inventoryItemId);
        if (inventoryLocation != null)
        {
            inventoryLocation.Quantity += quantityChange;
            inventoryLocation.AllocatedQuantity += allocatedChange;
            inventoryLocation.AvailableQuantity = inventoryLocation.Quantity - inventoryLocation.AllocatedQuantity;
            inventoryLocation.LastMovementDate = DateTime.UtcNow;

            await _locationRepository.UpdateAsync(inventoryLocation);
        }
    }

    private async Task CreateStockMovementAsync(StockMovement movement)
    {
        await _movementRepository.AddAsync(movement);
    }

    private static decimal CalculateRecommendedOrderQuantity(InventoryItem item)
    {
        // Simple EOQ-like calculation
        // In a real system, this would consider demand patterns, carrying costs, etc.
        var shortfall = item.ReorderLevel - item.CurrentStock;
        var recommendedQuantity = Math.Max(item.ReorderQuantity, shortfall);

        // Round up to nearest reorder quantity multiple
        if (item.ReorderQuantity > 0)
        {
            recommendedQuantity = Math.Ceiling(recommendedQuantity / item.ReorderQuantity) * item.ReorderQuantity;
        }

        return recommendedQuantity;
    }

    #endregion
}
