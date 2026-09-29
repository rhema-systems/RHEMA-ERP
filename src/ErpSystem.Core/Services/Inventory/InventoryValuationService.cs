using ErpSystem.Core.DTOs.Inventory;
using System.Data.SqlTypes;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Inventory;

/// <summary>
/// Comprehensive inventory valuation service supporting FIFO, WAC, and Standard Cost methods.
/// Manages inventory movements, cost layers, and balance calculations.
/// </summary>
public partial class InventoryValuationService : IInventoryValuationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<InventoryValuationService> _logger;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IProcurementReceiptSourceControlService
        _receiptSourceControl;

    // Movement numbers are generated multiple times inside a single receipt/issue operation (often before SaveChanges).
    // Using a raw DB count each time can generate duplicates within the same request because unsaved movements
    // aren't visible to the DB query yet. We keep a per-request (scoped) in-memory sequence per prefix.
    private string? _movementNumberPrefix;
    private int _movementNumberNext;

    // Prevent duplicate InventoryBalance inserts when multiple lines post into the same
    // (TenantId, InventoryItemId, WarehouseId, LocationId) within a single request before SaveChanges.
    private readonly Dictionary<(Guid ItemId, Guid WarehouseId, Guid? LocationId), InventoryBalance> _balanceCache = new();

    public InventoryValuationService(
        IUnitOfWork unitOfWork,
        ILogger<InventoryValuationService> logger,
        ICurrentUserProvider currentUserProvider,
        IProcurementReceiptSourceControlService receiptSourceControl)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _currentUserProvider = currentUserProvider;
        _receiptSourceControl = receiptSourceControl;
    }

    #region Public Interface Methods

    public void ResetProcessingAttempt()
    {
        _balanceCache.Clear();
        _transferMovementCache.Clear();
        _movementNumberPrefix = null;
        _movementNumberNext = 0;
    }

    public async Task<InventoryValuationSummaryDto> GetItemValuationAsync(Guid inventoryItemId)
    {
        var item = await _unitOfWork.Repository<InventoryItem>()
            .GetByIdAsync(inventoryItemId);

        if (item == null)
            throw new KeyNotFoundException($"Inventory item {inventoryItemId} not found");

        var balances = await _unitOfWork.Repository<InventoryBalance>()
            .FindAsync(b => b.InventoryItemId == inventoryItemId && b.TenantId == _currentUserProvider.TenantId && !b.IsDeleted);

        var totalQuantity = balances.Sum(b => b.QuantityOnHand);
        var totalValue = balances.Sum(b => b.TotalValue);
        var averageCost = totalQuantity > 0 ? totalValue / totalQuantity : 0;

        // Get cost layers if FIFO
        var costLayers = new List<InventoryCostLayerDto>();
        if (item.ValuationMethod == ValuationMethod.FIFO)
        {
            var layers = await GetCostLayersAsync(inventoryItemId);
            costLayers = layers.ToList();
        }

        return new InventoryValuationSummaryDto
        {
            InventoryItemId = inventoryItemId,
            ItemCode = item.ItemCode,
            ItemName = item.Name,
            TotalQuantity = totalQuantity,
            TotalValue = totalValue,
            AverageCost = averageCost,
            ValuationMethod = item.ValuationMethod,
            CostLayers = costLayers
        };
    }

    public async Task<IEnumerable<InventoryCostLayerDto>> GetCostLayersAsync(Guid inventoryItemId, Guid? warehouseId = null)
    {
        var query = _unitOfWork.Repository<InventoryLayer>()
            .GetQueryable()
            .Where(l => l.InventoryItemId == inventoryItemId && l.IsActive);

        if (warehouseId.HasValue)
            query = query.Where(l => l.WarehouseId == warehouseId.Value);

        var layers = await query
            .Include(l => l.InventoryItem)
            .Include(l => l.Warehouse)
            .OrderBy(l => l.LayerDate)
            .ToListAsync();

        return layers.Select(l => new InventoryCostLayerDto
        {
            Id = l.Id,
            InventoryItemId = l.InventoryItemId,
            ItemCode = l.InventoryItem.ItemCode,
            ItemName = l.InventoryItem.Name,
            WarehouseId = l.WarehouseId,
            WarehouseName = l.Warehouse.Name,
            LayerDate = l.LayerDate,
            ReferenceType = l.SourceType ?? "Unknown",
            ReferenceNumber = l.SourceReference,
            OriginalQuantity = l.OriginalQuantity,
            RemainingQuantity = l.RemainingQuantity,
            UnitCost = l.UnitCost,
            TotalValue = l.RemainingValue,
            LotNumber = l.LotNumber,
            SerialNumber = null // Layers don't track serial numbers directly
        });
    }

    public async Task<decimal> GetInventoryValueAsync(Guid? warehouseId = null, Guid? categoryId = null)
    {
        var query = _unitOfWork.Repository<InventoryBalance>().GetQueryable();

        if (warehouseId.HasValue)
            query = query.Where(b => b.WarehouseId == warehouseId.Value);

        if (categoryId.HasValue)
        {
            query = query.Where(b => b.InventoryItem.CategoryId == categoryId.Value);
        }

        return await query.SumAsync(b => b.TotalValue);
    }

    public async Task<decimal> CalculateWeightedAverageCostAsync(Guid inventoryItemId)
    {
        var balances = await _unitOfWork.Repository<InventoryBalance>()
            .FindAsync(b => b.InventoryItemId == inventoryItemId);

        var totalQuantity = balances.Sum(b => b.QuantityOnHand);
        var totalValue = balances.Sum(b => b.TotalValue);

        return totalQuantity > 0 ? totalValue / totalQuantity : 0;
    }

    public async Task<decimal> GetFIFOCostAsync(Guid inventoryItemId, decimal quantity)
    {
        var layers = await _unitOfWork.Repository<InventoryLayer>()
            .GetQueryable()
            .Where(l => l.InventoryItemId == inventoryItemId && 
                       !l.IsFullyConsumed && 
                       l.RemainingQuantity > 0)
            .OrderBy(l => l.LayerDate)
            .ToListAsync();

        decimal totalCost = 0;
        decimal remainingQty = quantity;

        foreach (var layer in layers)
        {
            if (remainingQty <= 0) break;

            var qtyFromLayer = Math.Min(remainingQty, layer.RemainingQuantity);
            totalCost += qtyFromLayer * layer.UnitCost;
            remainingQty -= qtyFromLayer;
        }

        if (remainingQty > 0)
        {
            _logger.LogWarning("Insufficient inventory layers for FIFO cost calculation. Item: {ItemId}, Requested: {Quantity}, Remaining: {Remaining}",
                inventoryItemId, quantity, remainingQty);
        }

        return totalCost;
    }

    public async Task<decimal> GetLIFOCostAsync(Guid inventoryItemId, decimal quantity)
    {
        var layers = await _unitOfWork.Repository<InventoryLayer>()
            .GetQueryable()
            .Where(l => l.InventoryItemId == inventoryItemId && 
                       !l.IsFullyConsumed && 
                       l.RemainingQuantity > 0)
            .OrderByDescending(l => l.LayerDate)
            .ToListAsync();

        decimal totalCost = 0;
        decimal remainingQty = quantity;

        foreach (var layer in layers)
        {
            if (remainingQty <= 0) break;

            var qtyFromLayer = Math.Min(remainingQty, layer.RemainingQuantity);
            totalCost += qtyFromLayer * layer.UnitCost;
            remainingQty -= qtyFromLayer;
        }

        if (remainingQty > 0)
        {
            _logger.LogWarning("Insufficient inventory layers for LIFO cost calculation. Item: {ItemId}, Requested: {Quantity}, Remaining: {Remaining}",
                inventoryItemId, quantity, remainingQty);
        }

        return totalCost;
    }

    public async Task RecalculateCostLayersAsync(Guid inventoryItemId)
    {
        var item = await _unitOfWork.Repository<InventoryItem>()
            .GetByIdAsync(inventoryItemId);

        if (item == null)
            throw new KeyNotFoundException($"Inventory item {inventoryItemId} not found");

        if (item.IsValuationLocked)
        {
            _logger.LogWarning("Cannot recalculate cost layers for locked item: {ItemId}", inventoryItemId);
            return;
        }

        // Recalculate based on valuation method
        switch (item.ValuationMethod)
        {
            case ValuationMethod.FIFO:
                await RecalculateFIFOLayersAsync(inventoryItemId);
                break;
            case ValuationMethod.WeightedAverage:
                await RecalculateWACBalancesAsync(inventoryItemId);
                break;
            case ValuationMethod.StandardCost:
                await RecalculateStandardCostBalancesAsync(inventoryItemId);
                break;
        }

        await _unitOfWork.SaveChangesAsync();
    }

    #endregion

    #region FIFO Methods

    /// <summary>
    /// Creates a new FIFO cost layer for a receipt transaction
    /// </summary>
    public async Task<InventoryLayer> CreateFIFOLayerAsync(
        Guid inventoryItemId,
        Guid warehouseId,
        Guid? locationId,
        decimal quantity,
        decimal unitCost,
        string sourceType,
        string? sourceReference,
        Guid? sourceId,
        string? lotNumber,
        DateTime? expirationDate)
    {
        var layerNumber = await GenerateLayerNumberAsync(inventoryItemId);

        var layer = new InventoryLayer
        {
            LayerNumber = layerNumber,
            InventoryItemId = inventoryItemId,
            WarehouseId = warehouseId,
            LocationId = locationId,
            LayerDate = DateTime.UtcNow,
            OriginalQuantity = quantity,
            RemainingQuantity = quantity,
            UnitCost = unitCost,
            RemainingValue = quantity * unitCost,
            SourceType = sourceType,
            SourceReference = sourceReference,
            SourceId = sourceId,
            LotNumber = lotNumber,
            ExpirationDate = expirationDate,
            IsFullyConsumed = false,
            IsActive = true,
            TenantId = _currentUserProvider.TenantId
        };

        await _unitOfWork.Repository<InventoryLayer>().AddAsync(layer);
        return layer;
    }

    /// <summary>
    /// Consumes inventory using FIFO method (oldest layers first)
    /// </summary>
    public Task<decimal> ConsumeFIFOLayersAsync(
        Guid inventoryItemId,
        Guid warehouseId,
        Guid? locationId,
        decimal quantity,
        List<InventoryMovement> movements) =>
        ConsumeFIFOLayersCoreAsync(inventoryItemId, warehouseId, locationId, quantity, movements, Array.Empty<InventoryLayer>());

    private async Task<decimal> ConsumeFIFOLayersCoreAsync(
        Guid inventoryItemId, Guid warehouseId, Guid? locationId, decimal quantity,
        List<InventoryMovement> movements, IReadOnlyCollection<InventoryLayer> pendingLayers, string? selectedLot = null)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        var query = _unitOfWork.Repository<InventoryLayer>()
            .GetQueryable()
            .Where(l => l.TenantId == _currentUserProvider.TenantId && !l.IsDeleted && l.IsActive &&
                       l.InventoryItemId == inventoryItemId &&
                       l.WarehouseId == warehouseId &&
                       l.LocationId == locationId &&
                       !l.IsFullyConsumed &&
                       l.RemainingQuantity > 0);
        selectedLot = string.IsNullOrWhiteSpace(selectedLot) ? null : selectedLot.Trim().ToUpperInvariant();
        if (selectedLot is not null) query = query.Where(l => l.LotNumber != null && l.LotNumber.ToUpper() == selectedLot);

        var layers = await query
            .OrderBy(l => l.LayerDate)
            .ThenBy(l => l.CreatedAt)
            .ThenBy(l => l.Id)
            .ToListAsync();

        // Only the controlled adjustment owner supplies its newly adopted exact-bin
        // layers. A database query alone cannot see Added entities before SaveChanges.
        if (pendingLayers.Count > 0)
            layers = layers.Concat(pendingLayers.Where(layer =>
                    layer.TenantId == _currentUserProvider.TenantId && !layer.IsDeleted && layer.IsActive &&
                    layer.InventoryItemId == inventoryItemId && layer.WarehouseId == warehouseId &&
                    layer.LocationId == locationId && !layer.IsFullyConsumed && layer.RemainingQuantity > 0 &&
                    (selectedLot is null || string.Equals(layer.LotNumber, selectedLot, StringComparison.OrdinalIgnoreCase))))
                .DistinctBy(layer => layer.Id).OrderBy(layer => layer.LayerDate).ThenBy(layer => layer.CreatedAt)
                .ThenBy(layer => new SqlGuid(layer.Id)).ToList();

        // Validate before touching tracked layers, including callers that own their transaction.
        if (layers.Sum(layer => layer.RemainingQuantity) < quantity)
            throw new InvalidOperationException("Insufficient inventory layers in the selected stock location.");

        decimal totalCost = 0;
        decimal remainingQty = quantity;

        foreach (var layer in layers)
        {
            if (remainingQty <= 0) break;

            var qtyFromLayer = Math.Min(remainingQty, layer.RemainingQuantity);
            // A transferred FIFO layer may carry a cent remainder that cannot
            // be represented by its rounded unit cost. Settle the last quantity
            // at the retained layer value; never discard or invent that remainder.
            var costFromLayer = qtyFromLayer == layer.RemainingQuantity
                ? layer.RemainingValue
                : Math.Min(layer.RemainingValue, decimal.Round(qtyFromLayer * layer.UnitCost, 2, MidpointRounding.AwayFromZero));

            // Update layer
            layer.RemainingQuantity -= qtyFromLayer;
            layer.RemainingValue -= costFromLayer;
            layer.IsFullyConsumed = layer.RemainingQuantity == 0;

            // Track cost for this consumption
            totalCost += costFromLayer;
            remainingQty -= qtyFromLayer;

            // Link movements to this layer
            foreach (var movement in movements)
            {
                movement.CostLayerId = layer.Id;
            }

            _logger.LogDebug("Consumed {Quantity} from layer {LayerNumber} at cost {UnitCost}. Remaining: {Remaining}",
                qtyFromLayer, layer.LayerNumber, layer.UnitCost, layer.RemainingQuantity);
        }

        if (remainingQty > 0)
        {
            _logger.LogWarning("Insufficient FIFO layers to consume full quantity. Item: {ItemId}, Requested: {Quantity}, Remaining: {Remaining}",
                inventoryItemId, quantity, remainingQty);
            throw new InvalidOperationException($"Insufficient inventory layers. Short by {remainingQty} units.");
        }

        return totalCost;
    }

    private async Task RecalculateFIFOLayersAsync(Guid inventoryItemId)
    {
        // Get all movements in chronological order
        var movements = await _unitOfWork.Repository<InventoryMovement>()
            .GetQueryable()
            .Where(m => m.InventoryItemId == inventoryItemId && m.IsPosted)
            .OrderBy(m => m.MovementDate)
            .ThenBy(m => m.CreatedAt)
            .ToListAsync();

        // Clear existing layers
        var existingLayers = await _unitOfWork.Repository<InventoryLayer>()
            .FindAsync(l => l.InventoryItemId == inventoryItemId);
        
        foreach (var layer in existingLayers)
        {
            await _unitOfWork.Repository<InventoryLayer>().DeleteAsync(layer.Id);
        }

        // Rebuild layers from movements
        foreach (var movement in movements)
        {
            if (movement.Direction == MovementDirection.In)
            {
                // Create new layer for receipts
                await CreateFIFOLayerAsync(
                    movement.InventoryItemId,
                    movement.WarehouseId,
                    movement.LocationId,
                    movement.Quantity,
                    movement.UnitCost,
                    movement.ReferenceType.ToString(),
                    movement.ReferenceNumber,
                    movement.ReferenceId,
                    movement.LotNumber,
                    movement.ExpirationDate);
            }
        }

        _logger.LogInformation("Recalculated FIFO layers for item {ItemId}", inventoryItemId);
    }

    #endregion

    #region WAC (Weighted Average Cost) Methods

    /// <summary>
    /// Calculates and updates weighted average cost for an item
    /// </summary>
    public async Task<decimal> CalculateAndUpdateWACAsync(
        Guid inventoryItemId,
        Guid warehouseId,
        Guid? locationId)
    {
        var balance = await GetOrCreateBalanceAsync(inventoryItemId, warehouseId, locationId);

        if (balance.QuantityOnHand <= 0)
        {
            balance.AverageUnitCost = 0;
            balance.TotalValue = 0;
            return 0;
        }

        balance.AverageUnitCost = balance.TotalValue / balance.QuantityOnHand;
        balance.LastRecalculatedAt = DateTime.UtcNow;

        return balance.AverageUnitCost;
    }

    /// <summary>
    /// Processes a receipt using WAC method
    /// </summary>
    public async Task ProcessWACReceiptAsync(
        Guid inventoryItemId,
        Guid warehouseId,
        Guid? locationId,
        decimal quantity,
        decimal unitCost)
    {
        var balance = await GetOrCreateBalanceAsync(inventoryItemId, warehouseId, locationId);

        // Calculate new weighted average
        var oldValue = balance.TotalValue;
        var newValue = quantity * unitCost;
        var totalValue = oldValue + newValue;
        var totalQuantity = balance.QuantityOnHand + quantity;

        balance.QuantityOnHand = totalQuantity;
        balance.TotalValue = totalValue;
        balance.AverageUnitCost = totalQuantity > 0 ? totalValue / totalQuantity : 0;
        balance.LastReceiptDate = DateTime.UtcNow;
        balance.LastMovementDate = DateTime.UtcNow;
        balance.LastRecalculatedAt = DateTime.UtcNow;

        _logger.LogDebug("WAC Receipt: Item {ItemId}, Qty {Quantity} @ {UnitCost}. New Avg: {AvgCost}",
            inventoryItemId, quantity, unitCost, balance.AverageUnitCost);
    }

    /// <summary>
    /// Processes an issue using WAC method
    /// </summary>
    public async Task<decimal> ProcessWACIssueAsync(
        Guid inventoryItemId,
        Guid warehouseId,
        Guid? locationId,
        decimal quantity)
    {
        var balance = await GetOrCreateBalanceAsync(inventoryItemId, warehouseId, locationId);

        if (balance.QuantityOnHand < quantity)
        {
            throw new InvalidOperationException($"Insufficient inventory. Available: {balance.QuantityOnHand}, Requested: {quantity}");
        }

        // The rounded average is a display/cache value, not the valuation source.
        // Settle the retained value on the last quantity so issuing 3 units worth
        // 10.00 cannot silently discard a cent because the cached average is 3.33.
        var costPerUnit = balance.QuantityOnHand > 0 ? balance.TotalValue / balance.QuantityOnHand : 0;
        var totalCost = quantity == balance.QuantityOnHand ? balance.TotalValue
            : decimal.Round(quantity * costPerUnit, 2, MidpointRounding.AwayFromZero);

        balance.QuantityOnHand -= quantity;
        balance.TotalValue -= totalCost;
        balance.LastIssueDate = DateTime.UtcNow;
        balance.LastMovementDate = DateTime.UtcNow;

        // Recalculate average (should remain the same for WAC)
        if (balance.QuantityOnHand > 0)
        {
            balance.AverageUnitCost = balance.TotalValue / balance.QuantityOnHand;
        }
        else
        {
            balance.AverageUnitCost = 0;
            balance.TotalValue = 0; // Ensure no rounding errors
        }

        _logger.LogDebug("WAC Issue: Item {ItemId}, Qty {Quantity} @ {UnitCost}. Total Cost: {TotalCost}",
            inventoryItemId, quantity, costPerUnit, totalCost);

        return totalCost;
    }

    private async Task RecalculateWACBalancesAsync(Guid inventoryItemId)
    {
        var balances = await _unitOfWork.Repository<InventoryBalance>()
            .FindAsync(b => b.InventoryItemId == inventoryItemId && b.TenantId == _currentUserProvider.TenantId && !b.IsDeleted);

        foreach (var balance in balances)
        {
            // Get all movements for this balance
            var movements = await _unitOfWork.Repository<InventoryMovement>()
                .GetQueryable()
                .Where(m => m.InventoryItemId == inventoryItemId &&
                           m.TenantId == _currentUserProvider.TenantId && !m.IsDeleted &&
                           m.WarehouseId == balance.WarehouseId &&
                           m.LocationId == balance.LocationId &&
                           m.IsPosted)
                .OrderBy(m => m.MovementDate)
                .ToListAsync();

            // Recalculate from scratch
            decimal runningQty = 0;
            decimal runningValue = 0;

            foreach (var movement in movements)
            {
                if (movement.MovementType == InventoryMovementType.InvoiceCostAdjustment)
                {
                    if (movement.Quantity != 0)
                        throw new InvalidOperationException("Invoice cost adjustments cannot change stock quantity.");
                    runningValue += movement.TotalValue; // This value-only event retains its signed ledger amount.
                    continue;
                }
                if (movement.Direction == MovementDirection.In)
                {
                    runningValue += movement.TotalValue;
                    runningQty += movement.Quantity;
                }
                else
                {
                    // Preserve the exact posted issue cost, including retained rounding cents.
                    // Recalculation must not silently reprice an immutable outbound movement.
                    runningValue -= movement.TotalValue;
                    runningQty -= movement.Quantity;
                }
            }

            balance.QuantityOnHand = runningQty;
            balance.QuantityAvailable = runningQty - balance.QuantityAllocated;
            balance.TotalValue = runningValue;
            balance.AverageUnitCost = runningQty > 0 ? runningValue / runningQty : 0;
            balance.LastRecalculatedAt = DateTime.UtcNow;
        }

        _logger.LogInformation("Recalculated WAC balances for item {ItemId}", inventoryItemId);
    }

    #endregion

    #region Standard Cost Methods

    /// <summary>
    /// Processes a receipt using Standard Cost method
    /// </summary>
    public async Task<decimal> ProcessStandardCostReceiptAsync(
        Guid inventoryItemId,
        Guid warehouseId,
        Guid? locationId,
        decimal quantity,
        decimal actualUnitCost)
    {
        var item = await _unitOfWork.Repository<InventoryItem>()
            .GetByIdAsync(inventoryItemId);

        if (item == null)
            throw new KeyNotFoundException($"Inventory item {inventoryItemId} not found");

        var standardCost = item.StandardCost;
        var variance = (actualUnitCost - standardCost) * quantity;

        var balance = await GetOrCreateBalanceAsync(inventoryItemId, warehouseId, locationId);

        // Inventory is valued at standard cost
        balance.QuantityOnHand += quantity;
        balance.TotalValue += quantity * standardCost;
        balance.AverageUnitCost = standardCost;
        balance.LastReceiptDate = DateTime.UtcNow;
        balance.LastMovementDate = DateTime.UtcNow;

        _logger.LogDebug("Standard Cost Receipt: Item {ItemId}, Qty {Quantity}. Standard: {StandardCost}, Actual: {ActualCost}, Variance: {Variance}",
            inventoryItemId, quantity, standardCost, actualUnitCost, variance);

        return variance; // Return variance for posting to variance account
    }

    /// <summary>
    /// Processes an issue using Standard Cost method
    /// </summary>
    public async Task<decimal> ProcessStandardCostIssueAsync(
        Guid inventoryItemId,
        Guid warehouseId,
        Guid? locationId,
        decimal quantity)
    {
        var item = await _unitOfWork.Repository<InventoryItem>()
            .GetByIdAsync(inventoryItemId);

        if (item == null)
            throw new KeyNotFoundException($"Inventory item {inventoryItemId} not found");

        var balance = await GetOrCreateBalanceAsync(inventoryItemId, warehouseId, locationId);

        if (balance.QuantityOnHand < quantity)
        {
            throw new InvalidOperationException($"Insufficient inventory. Available: {balance.QuantityOnHand}, Requested: {quantity}");
        }

        var standardCost = item.StandardCost;
        var totalCost = quantity * standardCost;

        balance.QuantityOnHand -= quantity;
        balance.TotalValue -= totalCost;
        balance.LastIssueDate = DateTime.UtcNow;
        balance.LastMovementDate = DateTime.UtcNow;

        _logger.LogDebug("Standard Cost Issue: Item {ItemId}, Qty {Quantity} @ {StandardCost}. Total Cost: {TotalCost}",
            inventoryItemId, quantity, standardCost, totalCost);

        return totalCost;
    }

    private async Task RecalculateStandardCostBalancesAsync(Guid inventoryItemId)
    {
        var item = await _unitOfWork.Repository<InventoryItem>()
            .GetByIdAsync(inventoryItemId);

        if (item == null)
            throw new KeyNotFoundException($"Inventory item {inventoryItemId} not found");

        var balances = await _unitOfWork.Repository<InventoryBalance>()
            .FindAsync(b => b.InventoryItemId == inventoryItemId);

        foreach (var balance in balances)
        {
            // Revalue at current standard cost
            balance.TotalValue = balance.QuantityOnHand * item.StandardCost;
            balance.AverageUnitCost = item.StandardCost;
            balance.LastRecalculatedAt = DateTime.UtcNow;
        }

        _logger.LogInformation("Recalculated Standard Cost balances for item {ItemId}", inventoryItemId);
    }

    #endregion

    #region Movement Processing Methods

    /// <summary>
    /// Creates an inventory movement record
    /// </summary>
    public async Task<InventoryMovement> CreateMovementAsync(
        Guid inventoryItemId,
        Guid warehouseId,
        Guid? locationId,
        InventoryMovementType movementType,
        MovementDirection direction,
        decimal quantity,
        decimal unitCost,
        ReferenceType referenceType,
        string? referenceNumber,
        Guid? referenceId,
        string? lotNumber = null,
        string? serialNumber = null,
        DateTime? expirationDate = null,
        string? notes = null)
    {
        var movementNumber = await GenerateMovementNumberAsync();

        // Get current balance for running totals
        var balance = await GetOrCreateBalanceAsync(inventoryItemId, warehouseId, locationId);
        
        var runningBalance = balance.QuantityOnHand;
        var runningValue = balance.TotalValue;

        if (direction == MovementDirection.In)
        {
            runningBalance += quantity;
            runningValue += quantity * unitCost;
        }
        else
        {
            runningBalance -= quantity;
            runningValue -= quantity * unitCost;
        }

        var movement = new InventoryMovement
        {
            MovementNumber = movementNumber,
            InventoryItemId = inventoryItemId,
            WarehouseId = warehouseId,
            LocationId = locationId,
            MovementType = movementType,
            Direction = direction,
            Quantity = quantity,
            UnitCost = unitCost,
            TotalValue = quantity * unitCost,
            MovementDate = DateTime.UtcNow,
            PostingDate = DateTime.UtcNow,
            ReferenceType = referenceType,
            ReferenceNumber = referenceNumber,
            ReferenceId = referenceId,
            RunningBalance = runningBalance,
            RunningValue = runningValue,
            LotNumber = lotNumber,
            SerialNumber = serialNumber,
            ExpirationDate = expirationDate,
            CreatedById = _currentUserProvider.UserId,
            PostedById = _currentUserProvider.UserId,
            PostedAt = DateTime.UtcNow,
            IsPosted = true,
            IsReversal = false,
            Notes = notes,
            TenantId = _currentUserProvider.TenantId
        };

        await _unitOfWork.Repository<InventoryMovement>().AddAsync(movement);
        if (referenceType == ReferenceType.Transfer && referenceId.HasValue)
        {
            if (!_transferMovementCache.TryGetValue(referenceId.Value, out var pending))
                _transferMovementCache[referenceId.Value] = pending = new List<InventoryMovement>();
            pending.Add(movement);
        }
        return movement;
    }

    /// <summary>
    /// Processes an inventory receipt based on the item's valuation method
    /// </summary>
    public async Task<decimal> ProcessReceiptAsync(
        Guid inventoryItemId,
        Guid warehouseId,
        Guid? locationId,
        decimal quantity,
        decimal unitCost,
        ReferenceType referenceType,
        string? referenceNumber,
        Guid? referenceId,
        string? lotNumber = null,
        string? serialNumber = null,
        DateTime? expirationDate = null,
        string authorizationAction = "AuthorizeInventoryPosting")
    {
        await InventoryTransitProtection.EnsureOrdinaryStockScopeAsync(
            _unitOfWork, _currentUserProvider.TenantId, warehouseId, locationId);
        await _receiptSourceControl.EnforceInventoryPostingAsync(
            referenceType,
            referenceId,
            inventoryItemId,
            quantity,
            referenceId.HasValue
                ? $"{referenceId.Value:N}:{inventoryItemId:N}"
                : $"{Guid.NewGuid():N}:{inventoryItemId:N}",
            authorizationAction);

        var item = await _unitOfWork.Repository<InventoryItem>()
            .GetQueryable(value =>
                value.TenantId == _currentUserProvider.TenantId &&
                value.Id == inventoryItemId &&
                !value.IsDeleted)
            .SingleOrDefaultAsync();

        if (item == null)
            throw new KeyNotFoundException($"Inventory item {inventoryItemId} not found");

        // Lock valuation method after first transaction
        if (!item.IsValuationLocked)
        {
            item.IsValuationLocked = true;
            await _unitOfWork.Repository<InventoryItem>().UpdateAsync(item);
        }

        decimal variance = 0;

        switch (item.ValuationMethod)
        {
            case ValuationMethod.FIFO:
                // Create FIFO layer
                await CreateFIFOLayerAsync(
                    inventoryItemId, warehouseId, locationId,
                    quantity, unitCost,
                    referenceType.ToString(), referenceNumber, referenceId,
                    lotNumber, expirationDate);
                
                // Update balance
                var fifoBalance = await GetOrCreateBalanceAsync(inventoryItemId, warehouseId, locationId);
                fifoBalance.QuantityOnHand += quantity;
                fifoBalance.TotalValue += quantity * unitCost;
                fifoBalance.AverageUnitCost = fifoBalance.QuantityOnHand > 0 
                    ? fifoBalance.TotalValue / fifoBalance.QuantityOnHand : 0;
                fifoBalance.LastReceiptDate = DateTime.UtcNow;
                fifoBalance.LastMovementDate = DateTime.UtcNow;
                break;

            case ValuationMethod.WeightedAverage:
                await ProcessWACReceiptAsync(inventoryItemId, warehouseId, locationId, quantity, unitCost);
                break;

            case ValuationMethod.StandardCost:
                variance = await ProcessStandardCostReceiptAsync(inventoryItemId, warehouseId, locationId, quantity, unitCost);
                break;
        }

        // The immutable movement must carry the value actually held in the inventory
        // subledger. Standard-cost receipts therefore use standard cost and retain the
        // actual-versus-standard difference separately for the Finance variance line.
        var movementUnitCost = item.ValuationMethod == ValuationMethod.StandardCost
            ? item.StandardCost
            : unitCost;
        var movement = await CreateMovementAsync(
            inventoryItemId, warehouseId, locationId,
            InventoryMovementType.PurchaseReceipt, MovementDirection.In,
            quantity, movementUnitCost,
            referenceType, referenceNumber, referenceId,
            lotNumber, serialNumber, expirationDate);
        movement.VarianceAmount = variance;

        // Receipt valuation has already changed the balance. Retain its closing
        // totals rather than adding this receipt a second time in movement history.
        var receiptBalance = await GetOrCreateBalanceAsync(inventoryItemId, warehouseId, locationId);
        receiptBalance.QuantityAvailable = receiptBalance.QuantityOnHand - receiptBalance.QuantityAllocated;
        movement.RunningBalance = receiptBalance.QuantityOnHand;
        movement.RunningValue = receiptBalance.TotalValue;

        return variance;
    }

    /// <summary>
    /// Processes an inventory issue based on the item's valuation method
    /// </summary>
    public async Task<decimal> ProcessIssueAsync(
        Guid inventoryItemId,
        Guid warehouseId,
        Guid? locationId,
        decimal quantity,
        InventoryMovementType movementType,
        ReferenceType referenceType,
        string? referenceNumber,
        Guid? referenceId,
        string? lotNumber = null,
        string? serialNumber = null)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        await InventoryTransitProtection.EnsureOrdinaryStockScopeAsync(
            _unitOfWork, _currentUserProvider.TenantId, warehouseId, locationId);
        var item = await _unitOfWork.Repository<InventoryItem>()
            .GetQueryable(value => value.Id == inventoryItemId &&
                value.TenantId == _currentUserProvider.TenantId && !value.IsDeleted)
            .SingleOrDefaultAsync();

        if (item == null)
            throw new KeyNotFoundException($"Inventory item {inventoryItemId} not found");

        if (item.ValuationMethod is not (ValuationMethod.FIFO or ValuationMethod.WeightedAverage or ValuationMethod.StandardCost))
            throw new InvalidOperationException($"Issue valuation is not supported for {item.ValuationMethod}; no stock was issued.");

        // Check for negative stock
        var balance = await GetOrCreateBalanceAsync(inventoryItemId, warehouseId, locationId);
        if (balance.QuantityOnHand < quantity)
        {
            throw new InvalidOperationException(
                $"Insufficient inventory for item {item.ItemCode}. Available: {balance.QuantityOnHand}, Requested: {quantity}");
        }

        decimal totalCost = 0;

        switch (item.ValuationMethod)
        {
            case ValuationMethod.FIFO:
                // Consume FIFO layers
                var movements = new List<InventoryMovement>();
                totalCost = await ConsumeFIFOLayersCoreAsync(
                    inventoryItemId, warehouseId, locationId,
                    quantity, movements, Array.Empty<InventoryLayer>(),
                    referenceType == ReferenceType.SalesInvoice ? lotNumber?.Trim() : null);
                
                // Update balance
                balance.QuantityOnHand -= quantity;
                balance.TotalValue -= totalCost;
                balance.AverageUnitCost = balance.QuantityOnHand > 0 
                    ? balance.TotalValue / balance.QuantityOnHand : 0;
                balance.LastIssueDate = DateTime.UtcNow;
                balance.LastMovementDate = DateTime.UtcNow;
                break;

            case ValuationMethod.WeightedAverage:
                totalCost = await ProcessWACIssueAsync(inventoryItemId, warehouseId, locationId, quantity);
                break;

            case ValuationMethod.StandardCost:
                totalCost = await ProcessStandardCostIssueAsync(inventoryItemId, warehouseId, locationId, quantity);
                break;
        }

        var unitCost = quantity > 0 ? totalCost / quantity : 0;

        // Create movement record
        var movement = await CreateMovementAsync(
            inventoryItemId, warehouseId, locationId,
            movementType, MovementDirection.Out,
            quantity, unitCost,
            referenceType, referenceNumber, referenceId,
            lotNumber, serialNumber, null);

        // These are closing balances: the valuation routines above already applied the issue.
        // CreateMovementAsync also serves other callers with pre-mutation balances.
        balance.QuantityAvailable = balance.QuantityOnHand - balance.QuantityAllocated;
        movement.RunningBalance = balance.QuantityOnHand;
        movement.RunningValue = balance.TotalValue;

        return totalCost;
    }

    public async Task<decimal> ProcessReturnAsync(Guid returnVoucherLineId, bool reverse = false)
    {
        var tenantId = _currentUserProvider.TenantId;
        var line = await _unitOfWork.Repository<InventoryReturnVoucherLine>()
            .GetQueryable(value => value.Id == returnVoucherLineId && value.TenantId == tenantId && !value.IsDeleted)
            .Include(value => value.InventoryReturnVoucher).SingleOrDefaultAsync()
            ?? throw new InvalidOperationException("The governed return line was not found in this tenant.");
        var voucher = line.InventoryReturnVoucher;
        var expectedState = reverse ? InventoryReturnVoucherStatus.Reversed : InventoryReturnVoucherStatus.Posted;
        if (voucher.TenantId != tenantId || voucher.Status != expectedState || !line.LocationId.HasValue ||
            line.Quantity <= 0 || line.TotalValue <= 0)
            throw new InvalidOperationException("Only a governed posted return (or its approved reversal) can update valuation.");
        await InventoryTransitProtection.EnsureOrdinaryStockScopeAsync(
            _unitOfWork, tenantId, voucher.WarehouseId, line.LocationId);
        var item = await _unitOfWork.Repository<InventoryItem>()
            .GetQueryable(value => value.Id == line.InventoryItemId && value.TenantId == tenantId && !value.IsDeleted)
            .SingleAsync();
        if (item.ValuationMethod is not (ValuationMethod.FIFO or ValuationMethod.WeightedAverage or ValuationMethod.StandardCost))
            throw new InvalidOperationException($"Return valuation method {item.ValuationMethod} is not supported.");

        var note = $"Store Return Voucher {voucher.VoucherNumber}; line {line.Id:D}";
        var original = await _unitOfWork.Repository<InventoryMovement>()
            .GetQueryable(value => value.TenantId == tenantId && value.ReferenceId == voucher.InventoryRequisitionId &&
                value.MovementType == InventoryMovementType.RequisitionReturn && value.Notes == note &&
                !value.IsReversal && !value.IsDeleted).SingleOrDefaultAsync();
        if ((!reverse && original is not null) || (reverse && original is null))
            throw new InvalidOperationException("The return valuation history does not match the requested transition.");
        if (reverse && await _unitOfWork.Repository<InventoryMovement>().GetQueryable(value =>
            value.TenantId == tenantId && value.ReversedMovementId == original!.Id && !value.IsDeleted).AnyAsync())
            throw new InvalidOperationException("The return valuation has already been reversed.");

        var balance = await GetOrCreateBalanceAsync(line.InventoryItemId, voucher.WarehouseId, line.LocationId);
        var delta = reverse ? -line.Quantity : line.Quantity;
        var valueDelta = reverse ? -line.TotalValue : line.TotalValue;
        if (reverse && (balance.QuantityAvailable < line.Quantity || balance.TotalValue < line.TotalValue ||
            (balance.QuantityOnHand == line.Quantity && balance.TotalValue != line.TotalValue)))
            throw new InvalidOperationException("The original return quantity and value are no longer available for reversal.");
        Guid? layerId = null;
        if (item.ValuationMethod == ValuationMethod.FIFO)
        {
            if (reverse)
            {
                var layer = await _unitOfWork.Repository<InventoryLayer>().GetQueryable(value =>
                    value.TenantId == tenantId && value.SourceType == "InventoryReturnVoucher" && value.SourceId == line.Id &&
                    value.InventoryItemId == line.InventoryItemId && value.WarehouseId == voucher.WarehouseId &&
                    value.LocationId == line.LocationId && !value.IsDeleted).SingleOrDefaultAsync();
                if (layer is null || layer.RemainingQuantity != line.Quantity || layer.RemainingValue != line.TotalValue)
                    throw new InvalidOperationException("The original FIFO return layer has been consumed; unrelated stock cannot reverse it.");
                layerId = layer.Id;
                layer.RemainingQuantity = 0;
                layer.RemainingValue = 0;
                layer.IsFullyConsumed = true;
            }
            else
            {
                var layer = await CreateFIFOLayerAsync(line.InventoryItemId, voucher.WarehouseId, line.LocationId,
                    line.Quantity, line.TotalValue / line.Quantity, "InventoryReturnVoucher", voucher.VoucherNumber,
                    line.Id, line.LotNumber, line.ExpiryDate);
                layer.RemainingValue = line.TotalValue;
                layerId = layer.Id;
            }
        }
        balance.QuantityOnHand += delta;
        balance.TotalValue += valueDelta;
        balance.QuantityAvailable = balance.QuantityOnHand - balance.QuantityAllocated;
        balance.AverageUnitCost = balance.QuantityOnHand > 0 ? balance.TotalValue / balance.QuantityOnHand : 0;
        balance.LastMovementDate = DateTime.UtcNow;
        balance.LastRecalculatedAt = DateTime.UtcNow;
        if (reverse) balance.LastIssueDate = DateTime.UtcNow;
        else balance.LastReceiptDate = DateTime.UtcNow;
        var movement = await CreateMovementAsync(line.InventoryItemId, voucher.WarehouseId, line.LocationId,
            InventoryMovementType.RequisitionReturn, reverse ? MovementDirection.Out : MovementDirection.In,
            line.Quantity, line.TotalValue / line.Quantity, ReferenceType.Requisition, voucher.VoucherNumber,
            voucher.InventoryRequisitionId, line.LotNumber, line.SerialNumber, line.ExpiryDate, note);
        movement.TotalValue = line.TotalValue;
        movement.RunningBalance = balance.QuantityOnHand;
        movement.RunningValue = balance.TotalValue;
        movement.CostLayerId = layerId;
        movement.IsReversal = reverse;
        movement.ReversedMovementId = reverse ? original!.Id : null;
        return balance.AverageUnitCost;
    }

    public async Task<decimal> ProcessAdjustmentAsync(
        Guid inventoryItemId,
        Guid warehouseId,
        Guid locationId,
        decimal quantityDelta,
        decimal unitCost,
        decimal openingQuantity,
        bool allowNegative,
        string? referenceNumber,
        Guid referenceId,
        string? lotNumber = null,
        string? serialNumber = null,
        DateTime? expirationDate = null,
        Guid? reversalSourceId = null,
        DateTime? valuationTimestamp = null,
        decimal? fifoOpeningFallbackCost = null,
        Guid? reversalAdjustmentLineId = null)
    {
        if (quantityDelta == 0)
            throw new ArgumentOutOfRangeException(nameof(quantityDelta), "An adjustment quantity cannot be zero.");
        if (unitCost <= 0)
            throw new ArgumentOutOfRangeException(nameof(unitCost), "An adjustment requires a positive server-derived unit cost.");
        await InventoryTransitProtection.EnsureOrdinaryStockScopeAsync(
            _unitOfWork, _currentUserProvider.TenantId, warehouseId, locationId);

        decimal? retainedReversalValue = null;
        if (reversalAdjustmentLineId.HasValue)
        {
            // A rounded four-decimal display price cannot reconstruct every cent of
            // a consumed FIFO layer. Only the controlled reversal owner may restore
            // the immutable original line value; no caller-supplied value is accepted.
            if (quantityDelta <= 0 || reversalSourceId.HasValue)
                throw new InvalidOperationException("An adjustment receipt reversal requires its exact original negative line.");
            retainedReversalValue = await _unitOfWork.Repository<StockAdjustmentItem>()
                .GetQueryable(line => line.TenantId == _currentUserProvider.TenantId &&
                    line.Id == reversalAdjustmentLineId.Value && line.AdjustmentId == referenceId && !line.IsDeleted &&
                    line.InventoryItemId == inventoryItemId && line.LocationId == locationId &&
                    line.InventoryItem.ValuationMethod == ValuationMethod.FIFO &&
                    line.AdjustmentQuantity == -quantityDelta && line.AdjustmentValue < 0 &&
                    line.Adjustment.TenantId == _currentUserProvider.TenantId && !line.Adjustment.IsDeleted &&
                    line.Adjustment.WarehouseId == warehouseId && line.Adjustment.Status == "Reversed")
                .Select(line => (decimal?)-line.AdjustmentValue).SingleOrDefaultAsync()
                ?? throw new InvalidOperationException("The reversal line does not match the retained adjustment quantity, location and value.");
        }

        var item = await _unitOfWork.Repository<InventoryItem>()
            .GetQueryable(value => value.TenantId == _currentUserProvider.TenantId &&
                value.Id == inventoryItemId && !value.IsDeleted)
            .SingleOrDefaultAsync()
            ?? throw new KeyNotFoundException($"Inventory item {inventoryItemId} not found");
        if (!item.IsValuationLocked)
        {
            item.IsValuationLocked = true;
            await _unitOfWork.Repository<InventoryItem>().UpdateAsync(item);
        }

        var balance = await GetOrCreateBalanceAsync(inventoryItemId, warehouseId, locationId);
        // TDC-0603 deliberately did not manufacture legacy movement history. Adopt an
        // exact-bin opening value only while this authoritative scope is still pristine.
        var fifoFallbackCost = fifoOpeningFallbackCost ?? (item.AverageCost > 0 ? item.AverageCost
            : item.StandardCost > 0 ? item.StandardCost : item.LastPurchaseCost);
        if (!balance.LastMovementDate.HasValue && balance.QuantityOnHand == 0 && openingQuantity != 0)
        {
            var openingUnitCost = item.ValuationMethod == ValuationMethod.FIFO && fifoFallbackCost > 0
                ? fifoFallbackCost : unitCost;
            balance.QuantityOnHand = openingQuantity;
            balance.TotalValue = openingQuantity * openingUnitCost;
            balance.AverageUnitCost = openingUnitCost;
        }

        var quantity = Math.Abs(quantityDelta);
        if (quantityDelta < 0 && balance.QuantityOnHand < quantity && !allowNegative)
            throw new InvalidOperationException(
                $"Insufficient authoritative inventory balance. Available: {balance.QuantityOnHand}, Requested: {quantity}");

        decimal totalValue;
        decimal movementUnitCost;
        Guid? movementCostLayerId = null;
        if (quantityDelta > 0)
        {
            movementUnitCost = item.ValuationMethod == ValuationMethod.StandardCost
                ? item.StandardCost
                : unitCost;
            if (movementUnitCost <= 0)
                throw new InvalidOperationException($"A valid {item.ValuationMethod} cost is required for {item.ItemCode}.");
            totalValue = retainedReversalValue ?? quantity * movementUnitCost;
            if (retainedReversalValue.HasValue) movementUnitCost = totalValue / quantity;
            if (item.ValuationMethod == ValuationMethod.FIFO)
            {
                var layer = await CreateFIFOLayerAsync(
                    inventoryItemId, warehouseId, locationId, quantity, movementUnitCost,
                    ReferenceType.Adjustment.ToString(), referenceNumber, referenceId,
                    lotNumber, expirationDate);
                if (valuationTimestamp.HasValue) layer.LayerDate = valuationTimestamp.Value;
                if (retainedReversalValue.HasValue) layer.RemainingValue = totalValue;
                movementCostLayerId = layer.Id;
            }
            balance.QuantityOnHand += quantity;
            balance.TotalValue += totalValue;
        }
        else
        {
            switch (item.ValuationMethod)
            {
                case ValuationMethod.FIFO:
                {
                    if (reversalSourceId.HasValue)
                    {
                        var reversal = await ConsumeAdjustmentReversalLayersAsync(
                            inventoryItemId, warehouseId, locationId, quantity,
                            reversalSourceId.Value, unitCost, lotNumber);
                        totalValue = reversal.TotalCost;
                        movementUnitCost = totalValue / quantity;
                        movementCostLayerId = reversal.CostLayerId;
                        break;
                    }

                    var activeLayerQuantity = await _unitOfWork.Repository<InventoryLayer>()
                        .GetQueryable(value => value.TenantId == _currentUserProvider.TenantId &&
                            value.InventoryItemId == inventoryItemId && value.WarehouseId == warehouseId &&
                            value.LocationId == locationId && !value.IsDeleted && value.IsActive &&
                            !value.IsFullyConsumed && value.RemainingQuantity > 0)
                        .SumAsync(value => (decimal?)value.RemainingQuantity) ?? 0;
                    // Legacy exact-bin stock can predate the valuation layer ledger. Adopt
                    // only the missing opening quantity and retain explicit source lineage.
                    var missingOpeningLayer = Math.Max(0, openingQuantity - activeLayerQuantity);
                    if (missingOpeningLayer > 0)
                    {
                        var openingLayer = await CreateFIFOLayerAsync(
                            inventoryItemId, warehouseId, locationId, missingOpeningLayer, fifoFallbackCost,
                            "LegacyExactBinAdoption", referenceNumber, referenceId, lotNumber, expirationDate);
                        if (valuationTimestamp.HasValue) openingLayer.LayerDate = valuationTimestamp.Value;
                        openingLayer.UnitCost = decimal.Round(openingLayer.UnitCost, 2, MidpointRounding.AwayFromZero);
                        openingLayer.RemainingValue = decimal.Round(openingLayer.RemainingValue, 4, MidpointRounding.AwayFromZero);
                    }
                    var coveredQuantity = allowNegative
                        ? Math.Min(quantity, activeLayerQuantity + missingOpeningLayer)
                        : quantity;
                    totalValue = coveredQuantity > 0
                        ? await ConsumeFIFOLayersCoreAsync(
                            inventoryItemId, warehouseId, locationId, coveredQuantity, new List<InventoryMovement>(),
                            _unitOfWork.Repository<InventoryLayer>().GetAddedEntities())
                        : 0;
                    totalValue += (quantity - coveredQuantity) * fifoFallbackCost;
                    movementUnitCost = totalValue / quantity;
                    break;
                }
                case ValuationMethod.StandardCost:
                    movementUnitCost = item.StandardCost;
                    if (movementUnitCost <= 0)
                        throw new InvalidOperationException($"A standard cost is required for {item.ItemCode}.");
                    totalValue = quantity * movementUnitCost;
                    break;
                default:
                    movementUnitCost = balance.AverageUnitCost > 0 ? balance.AverageUnitCost : unitCost;
                    totalValue = quantity * movementUnitCost;
                    break;
            }
            balance.QuantityOnHand -= quantity;
            balance.TotalValue -= totalValue;
        }

        if (balance.QuantityOnHand == 0) balance.TotalValue = 0;
        balance.AverageUnitCost = balance.QuantityOnHand > 0 ? balance.TotalValue / balance.QuantityOnHand : 0;
        balance.QuantityAvailable = balance.QuantityOnHand - balance.QuantityAllocated;
        balance.LastMovementDate = DateTime.UtcNow;
        if (quantityDelta > 0) balance.LastReceiptDate = DateTime.UtcNow;
        else balance.LastIssueDate = DateTime.UtcNow;
        balance.LastRecalculatedAt = DateTime.UtcNow;

        var movement = await CreateMovementAsync(
            inventoryItemId, warehouseId, locationId,
            quantityDelta > 0 ? InventoryMovementType.AdjustmentIn : InventoryMovementType.AdjustmentOut,
            quantityDelta > 0 ? MovementDirection.In : MovementDirection.Out,
            quantity, movementUnitCost, ReferenceType.Adjustment, referenceNumber, referenceId,
            lotNumber, serialNumber, expirationDate, "Governed stock-adjustment posting");
        // This path mutates the balance before recording its immutable movement.
        movement.RunningBalance = balance.QuantityOnHand;
        movement.RunningValue = balance.TotalValue;
        movement.TotalValue = totalValue;
        movement.CostLayerId = movementCostLayerId;
        return totalValue;
    }

    private async Task<(decimal TotalCost, Guid? CostLayerId)> ConsumeAdjustmentReversalLayersAsync(
        Guid inventoryItemId,
        Guid warehouseId,
        Guid locationId,
        decimal quantity,
        Guid reversalSourceId,
        decimal unitCost,
        string? lotNumber)
    {
        var normalizedLot = string.IsNullOrWhiteSpace(lotNumber) ? null : lotNumber.Trim();
        var layers = await _unitOfWork.Repository<InventoryLayer>()
            .GetQueryable(value => value.TenantId == _currentUserProvider.TenantId &&
                value.InventoryItemId == inventoryItemId && value.WarehouseId == warehouseId &&
                value.LocationId == locationId && value.SourceType == ReferenceType.Adjustment.ToString() &&
                value.SourceId == reversalSourceId && value.UnitCost == unitCost &&
                !value.IsFullyConsumed && value.RemainingQuantity > 0)
            .OrderBy(value => value.LayerDate)
            .ThenBy(value => value.CreatedAt)
            .ToListAsync();
        layers = layers.Where(value => string.Equals(value.LotNumber?.Trim(), normalizedLot,
            StringComparison.OrdinalIgnoreCase)).ToList();

        var available = layers.Sum(value => value.RemainingQuantity);
        if (available < quantity)
            throw new InvalidOperationException(
                $"The original FIFO adjustment layer has only {available} units remaining; {quantity} units cannot be reversed without consuming unrelated stock.");

        var remaining = quantity;
        var totalCost = 0m;
        Guid? firstLayerId = null;
        foreach (var layer in layers)
        {
            if (remaining <= 0) break;
            var consumed = Math.Min(remaining, layer.RemainingQuantity);
            firstLayerId ??= layer.Id;
            layer.RemainingQuantity -= consumed;
            layer.RemainingValue = layer.RemainingQuantity * layer.UnitCost;
            layer.IsFullyConsumed = layer.RemainingQuantity == 0;
            totalCost += consumed * layer.UnitCost;
            remaining -= consumed;
        }

        return (totalCost, firstLayerId);
    }

    /// <summary>
    /// Reverses a posted movement (creates a reversal movement)
    /// </summary>
    public async Task<InventoryMovement> ReverseMovementAsync(Guid movementId, string reason)
    {
        var originalMovement = await _unitOfWork.Repository<InventoryMovement>()
            .GetByIdAsync(movementId);

        if (originalMovement == null || originalMovement.TenantId != _currentUserProvider.TenantId || originalMovement.IsDeleted)
            throw new KeyNotFoundException($"Movement {movementId} not found");
        await InventoryTransitProtection.EnsureOrdinaryStockScopeAsync(
            _unitOfWork, _currentUserProvider.TenantId, originalMovement.WarehouseId, originalMovement.LocationId);

        if (!originalMovement.IsPosted)
            throw new InvalidOperationException("Cannot reverse an unposted movement");

        // Create reversal movement with opposite direction
        var reversalDirection = originalMovement.Direction == MovementDirection.In 
            ? MovementDirection.Out 
            : MovementDirection.In;

        var reversalNumber = await GenerateMovementNumberAsync();

        var reversal = new InventoryMovement
        {
            MovementNumber = reversalNumber,
            InventoryItemId = originalMovement.InventoryItemId,
            WarehouseId = originalMovement.WarehouseId,
            LocationId = originalMovement.LocationId,
            MovementType = originalMovement.MovementType,
            Direction = reversalDirection,
            Quantity = originalMovement.Quantity,
            UnitCost = originalMovement.UnitCost,
            TotalValue = originalMovement.TotalValue,
            MovementDate = DateTime.UtcNow,
            PostingDate = DateTime.UtcNow,
            ReferenceType = originalMovement.ReferenceType,
            ReferenceNumber = originalMovement.ReferenceNumber,
            ReferenceId = originalMovement.ReferenceId,
            RunningBalance = 0, // Will be calculated
            RunningValue = 0, // Will be calculated
            LotNumber = originalMovement.LotNumber,
            SerialNumber = originalMovement.SerialNumber,
            ExpirationDate = originalMovement.ExpirationDate,
            CreatedById = _currentUserProvider.UserId,
            PostedById = _currentUserProvider.UserId,
            PostedAt = DateTime.UtcNow,
            IsPosted = true,
            IsReversal = true,
            ReversedMovementId = movementId,
            Notes = $"Reversal of {originalMovement.MovementNumber}: {reason}",
            TenantId = _currentUserProvider.TenantId
        };

        await _unitOfWork.Repository<InventoryMovement>().AddAsync(reversal);

        // Update balance
        var balance = await GetOrCreateBalanceAsync(
            originalMovement.InventoryItemId,
            originalMovement.WarehouseId,
            originalMovement.LocationId);

        if (reversalDirection == MovementDirection.In)
        {
            balance.QuantityOnHand += originalMovement.Quantity;
            balance.TotalValue += originalMovement.TotalValue;
        }
        else
        {
            balance.QuantityOnHand -= originalMovement.Quantity;
            balance.TotalValue -= originalMovement.TotalValue;
        }

        balance.LastMovementDate = DateTime.UtcNow;
        reversal.RunningBalance = balance.QuantityOnHand;
        reversal.RunningValue = balance.TotalValue;

        _logger.LogInformation("Reversed movement {MovementNumber}. Reason: {Reason}",
            originalMovement.MovementNumber, reason);

        return reversal;
    }

    #endregion

    #region Helper Methods

    private async Task<InventoryBalance> GetOrCreateBalanceAsync(
        Guid inventoryItemId,
        Guid warehouseId,
        Guid? locationId)
    {
        var key = (inventoryItemId, warehouseId, locationId);
        if (_balanceCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var tenantId = _currentUserProvider.TenantId;

        var balance = await _unitOfWork.Repository<InventoryBalance>()
            .FirstOrDefaultAsync(b => b.TenantId == tenantId &&
                                     b.InventoryItemId == inventoryItemId &&
                                     b.WarehouseId == warehouseId &&
                                     b.LocationId == locationId);

        if (balance == null)
        {
            balance = new InventoryBalance
            {
                InventoryItemId = inventoryItemId,
                WarehouseId = warehouseId,
                LocationId = locationId,
                QuantityOnHand = 0,
                QuantityAllocated = 0,
                QuantityAvailable = 0,
                QuantityOnOrder = 0,
                TotalValue = 0,
                AverageUnitCost = 0,
                LastRecalculatedAt = DateTime.UtcNow,
                TenantId = tenantId
            };

            await _unitOfWork.Repository<InventoryBalance>().AddAsync(balance);
        }

        _balanceCache[key] = balance;
        return balance;
    }

    private async Task<string> GenerateLayerNumberAsync(Guid inventoryItemId)
    {
        var item = await _unitOfWork.Repository<InventoryItem>()
            .GetByIdAsync(inventoryItemId);

        if (item == null)
            throw new KeyNotFoundException($"Inventory item {inventoryItemId} not found");

        var count = await _unitOfWork.Repository<InventoryLayer>()
            .CountAsync(l => l.InventoryItemId == inventoryItemId);

        return $"{item.ItemCode}-L{(count + 1):D6}";
    }

    private async Task<string> GenerateMovementNumberAsync()
    {
        var today = DateTime.UtcNow;
        var prefix = $"MV{today:yyyyMMdd}";

        // Initialize sequence for the day (per tenant) on first use.
        if (!string.Equals(_movementNumberPrefix, prefix, StringComparison.Ordinal))
        {
            _movementNumberPrefix = prefix;

            // Only count movements for this tenant + prefix.
            var count = await _unitOfWork.Repository<InventoryMovement>()
                .CountAsync(m => m.TenantId == _currentUserProvider.TenantId &&
                                 m.MovementNumber.StartsWith(prefix));

            _movementNumberNext = count + 1;
        }

        var movementNumber = $"{prefix}-{_movementNumberNext:D4}";
        _movementNumberNext++;

        return movementNumber;
    }

    #endregion
}
