using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Inventory;

/// <summary>
/// Service for managing stock adjustments
/// Handles positive and negative inventory adjustments outside of normal purchasing/requisition flows
/// </summary>
public class StockAdjustmentService : IStockAdjustmentService
{
    private readonly IStockAdjustmentRepository _adjustmentRepository;
    private readonly IInventoryItemRepository _itemRepository;
    private readonly IStockMovementRepository _movementRepository;
    private readonly IWarehouseQuantityRepository _warehouseQuantityRepository;
    private readonly IWarehouseLocationRepository _locationRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IConsignmentSettlementService _consignmentSettlementService;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<StockAdjustmentService> _logger;

    public StockAdjustmentService(
        IStockAdjustmentRepository adjustmentRepository,
        IInventoryItemRepository itemRepository,
        IStockMovementRepository movementRepository,
        IWarehouseQuantityRepository warehouseQuantityRepository,
        IWarehouseLocationRepository locationRepository,
        IWarehouseRepository warehouseRepository,
        IConsignmentSettlementService consignmentSettlementService,
        ICurrentUserProvider currentUserProvider,
        ILogger<StockAdjustmentService> logger)
    {
        _adjustmentRepository = adjustmentRepository;
        _itemRepository = itemRepository;
        _movementRepository = movementRepository;
        _warehouseQuantityRepository = warehouseQuantityRepository;
        _locationRepository = locationRepository;
        _warehouseRepository = warehouseRepository;
        _consignmentSettlementService = consignmentSettlementService;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    #region Query Methods

    /// <summary>
    /// Gets all stock adjustments with optional filtering
    /// </summary>
    public async Task<IEnumerable<StockAdjustmentDto>> GetAllAsync(
        string? status = null,
        string? reasonCode = null,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        try
        {
            IEnumerable<StockAdjustment> adjustments;

            if (startDate.HasValue && endDate.HasValue)
            {
                adjustments = await _adjustmentRepository.GetAdjustmentsByDateRangeAsync(startDate.Value, endDate.Value);
            }
            else
            {
                // Use GetAllWithItemsAsync to include Items collection for accurate ItemCount
                adjustments = await _adjustmentRepository.GetAllWithItemsAsync();
            }

            // Apply filters
            if (!string.IsNullOrEmpty(status))
            {
                adjustments = adjustments.Where(a => a.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(reasonCode))
            {
                adjustments = adjustments.Where(a => a.ReasonCode.Equals(reasonCode, StringComparison.OrdinalIgnoreCase));
            }

            return adjustments
                .OrderByDescending(a => a.AdjustmentDate)
                .Select(MapToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving stock adjustments");
            throw;
        }
    }

    /// <summary>
    /// Gets a stock adjustment by ID with all items
    /// </summary>
    public async Task<StockAdjustmentDetailDto?> GetByIdAsync(Guid id)
    {
        try
        {
            var adjustment = await _adjustmentRepository.GetWithItemsAsync(id);
            if (adjustment == null)
            {
                return null;
            }

            return MapToDetailDto(adjustment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving stock adjustment {AdjustmentId}", id);
            throw;
        }
    }

    /// <summary>
    /// Gets a stock adjustment by adjustment number
    /// </summary>
    public async Task<StockAdjustmentDetailDto?> GetByAdjustmentNumberAsync(string adjustmentNumber)
    {
        try
        {
            var adjustment = await _adjustmentRepository.GetByAdjustmentNumberAsync(adjustmentNumber);
            if (adjustment == null)
            {
                return null;
            }

            return MapToDetailDto(adjustment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving stock adjustment {AdjustmentNumber}", adjustmentNumber);
            throw;
        }
    }

    /// <summary>
    /// Gets pending (draft) adjustments
    /// </summary>
    public async Task<IEnumerable<StockAdjustmentDto>> GetPendingAsync()
    {
        try
        {
            var adjustments = await _adjustmentRepository.GetPendingAdjustmentsAsync();
            return adjustments.Select(MapToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending stock adjustments");
            throw;
        }
    }

    #endregion

    #region Create/Update Methods

    /// <summary>
    /// Creates a new stock adjustment
    /// </summary>
    public async Task<StockAdjustmentDetailDto> CreateAsync(CreateStockAdjustmentDto dto, Guid userId)
    {
        try
        {
            // Get TenantId from current user
            var tenantId = _currentUserProvider.TenantId;
            if (tenantId == Guid.Empty)
            {
                throw new InvalidOperationException("TenantId is required to create a stock adjustment");
            }

            // Generate adjustment number
            var adjustmentNumber = await _adjustmentRepository.GenerateAdjustmentNumberAsync();

            var adjustment = new StockAdjustment
            {
                TenantId = tenantId,
                AdjustmentNumber = adjustmentNumber,
                AdjustmentDate = dto.AdjustmentDate ?? DateTime.UtcNow,
                WarehouseId = dto.WarehouseId,
                ReasonCode = dto.ReasonCode,
                Description = dto.Description,
                Reference = dto.Reference,
                Status = "Draft",
                TotalAdjustmentValue = 0
            };

            // Add items
            foreach (var itemDto in dto.Items)
            {
                var inventoryItem = await _itemRepository.GetByIdAsync(itemDto.InventoryItemId);
                if (inventoryItem == null)
                {
                    throw new ArgumentException($"Inventory item {itemDto.InventoryItemId} not found");
                }

                var unitCost = itemDto.UnitCost ?? inventoryItem.AverageCost;
                var adjustmentValue = itemDto.AdjustmentQuantity * unitCost;

                var adjustmentItem = new StockAdjustmentItem
                {
                    TenantId = tenantId,
                    InventoryItemId = itemDto.InventoryItemId,
                    LocationId = itemDto.LocationId,
                    SerialNumber = itemDto.SerialNumber,
                    LotNumber = itemDto.LotNumber,
                    SystemQuantity = inventoryItem.CurrentStock,
                    PhysicalQuantity = inventoryItem.CurrentStock + itemDto.AdjustmentQuantity,
                    AdjustmentQuantity = itemDto.AdjustmentQuantity,
                    UnitCost = unitCost,
                    AdjustmentValue = adjustmentValue,
                    Reason = itemDto.Reason,
                    Notes = itemDto.Notes
                };

                adjustment.Items.Add(adjustmentItem);
                adjustment.TotalAdjustmentValue += adjustmentValue;
            }

            var created = await _adjustmentRepository.AddAsync(adjustment);
            await _adjustmentRepository.SaveChangesAsync();
            
            _logger.LogInformation("Created stock adjustment {AdjustmentNumber} with {ItemCount} items for tenant {TenantId}",
                adjustmentNumber, dto.Items.Count, tenantId);

            return await GetByIdAsync(created.Id) ?? throw new InvalidOperationException("Failed to retrieve created adjustment");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating stock adjustment");
            throw;
        }
    }

    /// <summary>
    /// Updates an existing stock adjustment (only if in Draft status)
    /// </summary>
    public async Task<StockAdjustmentDetailDto> UpdateAsync(Guid id, UpdateStockAdjustmentDto dto, Guid userId)
    {
        try
        {
            var adjustment = await _adjustmentRepository.GetWithItemsAsync(id)
                ?? throw new ArgumentException($"Stock adjustment {id} not found");

            if (adjustment.Status != "Draft")
            {
                throw new InvalidOperationException($"Cannot update adjustment with status {adjustment.Status}");
            }

            // Update header
            adjustment.ReasonCode = dto.ReasonCode;
            adjustment.Description = dto.Description;
            adjustment.Reference = dto.Reference;
            if (dto.AdjustmentDate.HasValue)
            {
                adjustment.AdjustmentDate = dto.AdjustmentDate.Value;
            }

            // Only update items if provided
            if (dto.Items != null && dto.Items.Count > 0)
            {
                // Clear existing items and add new ones
                adjustment.Items.Clear();
                adjustment.TotalAdjustmentValue = 0;

                foreach (var itemDto in dto.Items)
                {
                    var inventoryItem = await _itemRepository.GetByIdAsync(itemDto.InventoryItemId);
                    if (inventoryItem == null)
                    {
                        throw new ArgumentException($"Inventory item {itemDto.InventoryItemId} not found");
                    }

                    var unitCost = itemDto.UnitCost ?? inventoryItem.AverageCost;
                    var adjustmentValue = itemDto.AdjustmentQuantity * unitCost;

                    var adjustmentItem = new StockAdjustmentItem
                    {
                        TenantId = adjustment.TenantId, // Inherit TenantId from parent adjustment
                        AdjustmentId = adjustment.Id,
                        InventoryItemId = itemDto.InventoryItemId,
                        LocationId = itemDto.LocationId,
                        SerialNumber = itemDto.SerialNumber,
                        LotNumber = itemDto.LotNumber,
                        SystemQuantity = inventoryItem.CurrentStock,
                        PhysicalQuantity = inventoryItem.CurrentStock + itemDto.AdjustmentQuantity,
                        AdjustmentQuantity = itemDto.AdjustmentQuantity,
                        UnitCost = unitCost,
                        AdjustmentValue = adjustmentValue,
                        Reason = itemDto.Reason,
                        Notes = itemDto.Notes
                    };

                    adjustment.Items.Add(adjustmentItem);
                    adjustment.TotalAdjustmentValue += adjustmentValue;
                }
            }

            await _adjustmentRepository.UpdateAsync(adjustment);
            await _adjustmentRepository.SaveChangesAsync();
            
            _logger.LogInformation("Updated stock adjustment {AdjustmentNumber}", adjustment.AdjustmentNumber);

            return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve updated adjustment");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating stock adjustment {AdjustmentId}", id);
            throw;
        }
    }

    /// <summary>
    /// Deletes a stock adjustment (only if in Draft status)
    /// </summary>
    public async Task<bool> DeleteAsync(Guid id, Guid userId)
    {
        try
        {
            var adjustment = await _adjustmentRepository.GetByIdAsync(id)
                ?? throw new ArgumentException($"Stock adjustment {id} not found");

            if (adjustment.Status != "Draft")
            {
                throw new InvalidOperationException($"Cannot delete adjustment with status {adjustment.Status}");
            }

            await _adjustmentRepository.DeleteAsync(id);
            await _adjustmentRepository.SaveChangesAsync();
            
            _logger.LogInformation("Deleted stock adjustment {AdjustmentNumber}", adjustment.AdjustmentNumber);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting stock adjustment {AdjustmentId}", id);
            throw;
        }
    }

    /// <summary>
    /// Deletes an item from a stock adjustment (only if in Draft status)
    /// </summary>
    public async Task<bool> DeleteItemAsync(Guid adjustmentId, Guid itemId, Guid userId)
    {
        try
        {
            var adjustment = await _adjustmentRepository.GetWithItemsAsync(adjustmentId)
                ?? throw new ArgumentException($"Stock adjustment {adjustmentId} not found");

            if (adjustment.Status != "Draft")
            {
                throw new InvalidOperationException($"Cannot delete items from adjustment with status {adjustment.Status}");
            }

            var item = adjustment.Items.FirstOrDefault(i => i.Id == itemId);
            if (item == null)
            {
                throw new ArgumentException($"Item {itemId} not found in adjustment {adjustmentId}");
            }

            // Remove the item
            adjustment.Items.Remove(item);
            
            // Recalculate total value
            adjustment.TotalAdjustmentValue = adjustment.Items.Sum(i => i.AdjustmentValue);

            await _adjustmentRepository.UpdateAsync(adjustment);
            await _adjustmentRepository.SaveChangesAsync();
            
            _logger.LogInformation("Deleted item {ItemId} from stock adjustment {AdjustmentNumber}",
                itemId, adjustment.AdjustmentNumber);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting item {ItemId} from stock adjustment {AdjustmentId}", itemId, adjustmentId);
            throw;
        }
    }

    #endregion

    #region Workflow Methods

    /// <summary>
    /// Approves a stock adjustment (changes status from Draft to Approved)
    /// </summary>
    public async Task<StockAdjustmentDetailDto> ApproveAsync(Guid id, Guid userId)
    {
        try
        {
            var adjustment = await _adjustmentRepository.GetWithItemsAsync(id)
                ?? throw new ArgumentException($"Stock adjustment {id} not found");

            if (adjustment.Status != "Draft")
            {
                throw new InvalidOperationException($"Cannot approve adjustment with status {adjustment.Status}");
            }

            adjustment.Status = "Approved";
            adjustment.ApprovedById = userId;
            adjustment.ApprovedAt = DateTime.UtcNow;

            await _adjustmentRepository.UpdateAsync(adjustment);
            await _adjustmentRepository.SaveChangesAsync();
            
            _logger.LogInformation("Approved stock adjustment {AdjustmentNumber}", adjustment.AdjustmentNumber);

            return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve approved adjustment");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving stock adjustment {AdjustmentId}", id);
            throw;
        }
    }

    /// <summary>
    /// Posts a stock adjustment (applies the adjustment to inventory and creates stock movements)
    /// </summary>
    public async Task<StockAdjustmentDetailDto> PostAsync(Guid id, Guid userId)
    {
        try
        {
            var adjustment = await _adjustmentRepository.GetWithItemsAsync(id)
                ?? throw new ArgumentException($"Stock adjustment {id} not found");

            if (adjustment.Status != "Approved" && adjustment.Status != "Draft")
            {
                throw new InvalidOperationException($"Cannot post adjustment with status {adjustment.Status}");
            }

            // Apply adjustments to inventory
            foreach (var item in adjustment.Items)
            {
                var inventoryItem = await _itemRepository.GetByIdAsync(item.InventoryItemId);

                var location = item.LocationId.HasValue && item.LocationId.Value != Guid.Empty
                    ? await _locationRepository.GetByIdAsync(item.LocationId.Value)
                    : null;

                var effectiveWarehouseId = location != null
                    ? location.InventoryWarehouseId
                    : (adjustment.WarehouseId ?? Guid.Empty);

                var isConsignmentWarehouse = false;
                if (effectiveWarehouseId != Guid.Empty)
                {
                    var wh = await _warehouseRepository.GetByIdAsync(effectiveWarehouseId);
                    isConsignmentWarehouse = wh?.IsConsignmentWarehouse == true;
                }

                // Update warehouse quantity (always; consignment stock still needs accurate warehouse-level balances).
                WarehouseQuantity? warehouseQty = null;
                if (effectiveWarehouseId != Guid.Empty)
                {
                    warehouseQty = await _warehouseQuantityRepository.GetByWarehouseAndItemAsync(effectiveWarehouseId, item.InventoryItemId);
                    if (warehouseQty == null)
                    {
                        warehouseQty = new WarehouseQuantity
                        {
                            TenantId = adjustment.TenantId,
                            InventoryItemId = item.InventoryItemId,
                            WarehouseId = effectiveWarehouseId,
                            CurrentStock = 0,
                            AvailableStock = 0,
                            AllocatedStock = 0,
                            AverageCost = item.UnitCost,
                            LastMovementDate = DateTime.UtcNow,
                            CreatedById = userId
                        };

                        await _warehouseQuantityRepository.AddAsync(warehouseQty);
                    }

                    warehouseQty.CurrentStock += item.AdjustmentQuantity;
                    warehouseQty.AvailableStock = warehouseQty.CurrentStock - warehouseQty.AllocatedStock;
                    warehouseQty.LastMovementDate = DateTime.UtcNow;
                    await _warehouseQuantityRepository.UpdateAsync(warehouseQty);
                }

                // Update owned/main inventory item totals only for non-consignment warehouses/bins.
                if (!isConsignmentWarehouse && inventoryItem != null)
                {
                    inventoryItem.CurrentStock += item.AdjustmentQuantity;
                    inventoryItem.AvailableStock = inventoryItem.CurrentStock - inventoryItem.AllocatedStock;
                    inventoryItem.LastStockDate = DateTime.UtcNow;
                    await _itemRepository.UpdateAsync(inventoryItem);
                }

                // Create stock movement record
                var movementType = item.AdjustmentQuantity >= 0 ? "Adjustment+" : "Adjustment-";
                var movement = new StockMovement
                {
                    TenantId = adjustment.TenantId, // Inherit TenantId from parent adjustment
                    InventoryItemId = item.InventoryItemId,
                    MovementType = movementType,
                    Quantity = item.AdjustmentQuantity,
                    UnitCost = item.UnitCost,
                    TotalValue = item.AdjustmentValue,
                    MovementDate = DateTime.UtcNow,
                    ReferenceType = ReferenceType.Adjustment,
                    ReferenceNumber = adjustment.AdjustmentNumber,
                    ReferenceId = adjustment.Id,
                    WarehouseId = effectiveWarehouseId != Guid.Empty ? effectiveWarehouseId : null,
                    LocationId = item.LocationId,
                    Notes = $"{adjustment.ReasonCode}: {item.Notes ?? adjustment.Description}",
                    SerialNumber = item.SerialNumber,
                    LotNumber = item.LotNumber,
                    RunningBalance = !isConsignmentWarehouse
                        ? (inventoryItem?.CurrentStock ?? 0)
                        : (warehouseQty?.CurrentStock ?? 0),
                    ProcessedById = userId
                };

                await _movementRepository.AddAsync(movement);
                await _consignmentSettlementService.TryCreateFromStockMovementAsync(movement);
            }

            // Update adjustment status
            adjustment.Status = "Posted";
            if (!adjustment.ApprovedById.HasValue)
            {
                adjustment.ApprovedById = userId;
                adjustment.ApprovedAt = DateTime.UtcNow;
            }

            await _adjustmentRepository.UpdateAsync(adjustment);
            await _adjustmentRepository.SaveChangesAsync();
            
            _logger.LogInformation("Posted stock adjustment {AdjustmentNumber} with {ItemCount} items",
                adjustment.AdjustmentNumber, adjustment.Items.Count);

            return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve posted adjustment");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error posting stock adjustment {AdjustmentId}", id);
            throw;
        }
    }

    /// <summary>
    /// Cancels a stock adjustment (only if not yet posted)
    /// </summary>
    public async Task<StockAdjustmentDetailDto> CancelAsync(Guid id, Guid userId)
    {
        try
        {
            var adjustment = await _adjustmentRepository.GetByIdAsync(id)
                ?? throw new ArgumentException($"Stock adjustment {id} not found");

            if (adjustment.Status == "Posted")
            {
                throw new InvalidOperationException("Cannot cancel a posted adjustment");
            }

            adjustment.Status = "Cancelled";
            await _adjustmentRepository.UpdateAsync(adjustment);
            await _adjustmentRepository.SaveChangesAsync();
            
            _logger.LogInformation("Cancelled stock adjustment {AdjustmentNumber}", adjustment.AdjustmentNumber);

            return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve cancelled adjustment");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling stock adjustment {AdjustmentId}", id);
            throw;
        }
    }

    #endregion

    #region Helper Methods

    private static StockAdjustmentDto MapToDto(StockAdjustment adjustment)
    {
        return new StockAdjustmentDto
        {
            Id = adjustment.Id,
            AdjustmentNumber = adjustment.AdjustmentNumber,
            AdjustmentDate = adjustment.AdjustmentDate,
            WarehouseId = adjustment.WarehouseId,
            WarehouseName = adjustment.Warehouse?.Name,
            ReasonCode = adjustment.ReasonCode,
            Description = adjustment.Description,
            Reference = adjustment.Reference,
            Status = adjustment.Status,
            TotalAdjustmentValue = adjustment.TotalAdjustmentValue,
            ItemCount = adjustment.Items?.Count ?? 0,
            ApprovedByName = adjustment.ApprovedBy?.FullName,
            ApprovedAt = adjustment.ApprovedAt,
            CreatedAt = adjustment.CreatedAt
        };
    }

    private static StockAdjustmentDetailDto MapToDetailDto(StockAdjustment adjustment)
    {
        var dto = new StockAdjustmentDetailDto
        {
            Id = adjustment.Id,
            AdjustmentNumber = adjustment.AdjustmentNumber,
            AdjustmentDate = adjustment.AdjustmentDate,
            WarehouseId = adjustment.WarehouseId,
            WarehouseName = adjustment.Warehouse?.Name,
            ReasonCode = adjustment.ReasonCode,
            Description = adjustment.Description,
            Reference = adjustment.Reference,
            Status = adjustment.Status,
            TotalAdjustmentValue = adjustment.TotalAdjustmentValue,
            ItemCount = adjustment.Items?.Count ?? 0,
            ApprovedByName = adjustment.ApprovedBy?.FullName,
            ApprovedAt = adjustment.ApprovedAt,
            CreatedAt = adjustment.CreatedAt,
            Items = adjustment.Items?.Select(item => new StockAdjustmentItemDto
            {
                Id = item.Id,
                AdjustmentId = item.AdjustmentId,
                InventoryItemId = item.InventoryItemId,
                ItemCode = item.InventoryItem?.ItemCode ?? string.Empty,
                ItemName = item.InventoryItem?.Name ?? string.Empty,
                CategoryName = item.InventoryItem?.Category?.Name,
                UnitOfMeasure = item.InventoryItem?.UnitOfMeasure ?? "EA",
                LocationId = item.LocationId,
                LocationCode = item.Location?.LocationCode,
                WarehouseName = item.Location?.Warehouse?.Name ?? adjustment.Warehouse?.Name,
                SerialNumber = item.SerialNumber,
                LotNumber = item.LotNumber,
                SystemQuantity = item.SystemQuantity,
                PhysicalQuantity = item.PhysicalQuantity,
                AdjustmentQuantity = item.AdjustmentQuantity,
                UnitCost = item.UnitCost,
                AdjustmentValue = item.AdjustmentValue,
                TotalValue = Math.Abs(item.AdjustmentQuantity * item.UnitCost),
                PreviousQuantity = item.SystemQuantity,
                NewQuantity = item.SystemQuantity + item.AdjustmentQuantity,
                Reason = item.Reason,
                Notes = item.Notes
            }).ToList() ?? new List<StockAdjustmentItemDto>()
        };

        return dto;
    }

    #endregion
}

/// <summary>
/// Interface for stock adjustment service
/// </summary>
public interface IStockAdjustmentService
{
    Task<IEnumerable<StockAdjustmentDto>> GetAllAsync(string? status = null, string? reasonCode = null, DateTime? startDate = null, DateTime? endDate = null);
    Task<StockAdjustmentDetailDto?> GetByIdAsync(Guid id);
    Task<StockAdjustmentDetailDto?> GetByAdjustmentNumberAsync(string adjustmentNumber);
    Task<IEnumerable<StockAdjustmentDto>> GetPendingAsync();
    Task<StockAdjustmentDetailDto> CreateAsync(CreateStockAdjustmentDto dto, Guid userId);
    Task<StockAdjustmentDetailDto> UpdateAsync(Guid id, UpdateStockAdjustmentDto dto, Guid userId);
    Task<bool> DeleteAsync(Guid id, Guid userId);
    Task<bool> DeleteItemAsync(Guid adjustmentId, Guid itemId, Guid userId);
    Task<StockAdjustmentDetailDto> ApproveAsync(Guid id, Guid userId);
    Task<StockAdjustmentDetailDto> PostAsync(Guid id, Guid userId);
    Task<StockAdjustmentDetailDto> CancelAsync(Guid id, Guid userId);
}
