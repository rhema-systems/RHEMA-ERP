using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Interfaces.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ErpSystem.Api.Controllers.Inventory;

/// <summary>
/// API controller for inventory valuation management
/// Handles FIFO, WAC, and Standard Cost valuation methods
/// </summary>
[ApiController]
[Route("api/inventory/valuation")]
[Authorize]
public class InventoryValuationController : ControllerBase
{
    private readonly IInventoryValuationService _valuationService;
    private readonly IInventoryMovementRepository _movementRepository;
    private readonly IInventoryLayerRepository _layerRepository;
    private readonly IInventoryBalanceRepository _balanceRepository;
    private readonly ILogger<InventoryValuationController> _logger;

    public InventoryValuationController(
        IInventoryValuationService valuationService,
        IInventoryMovementRepository movementRepository,
        IInventoryLayerRepository layerRepository,
        IInventoryBalanceRepository balanceRepository,
        ILogger<InventoryValuationController> logger)
    {
        _valuationService = valuationService;
        _movementRepository = movementRepository;
        _layerRepository = layerRepository;
        _balanceRepository = balanceRepository;
        _logger = logger;
    }

    /// <summary>
    /// Gets valuation summary for a specific inventory item
    /// </summary>
    [HttpGet("items/{inventoryItemId}")]
    public async Task<ActionResult<InventoryValuationSummaryDto>> GetItemValuation(Guid inventoryItemId)
    {
        try
        {
            var valuation = await _valuationService.GetItemValuationAsync(inventoryItemId);
            return Ok(valuation);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving valuation for item {ItemId}", inventoryItemId);
            return StatusCode(500, "An error occurred while retrieving item valuation");
        }
    }

    /// <summary>
    /// Gets cost layers for a specific inventory item (FIFO method)
    /// </summary>
    [HttpGet("items/{inventoryItemId}/layers")]
    public async Task<ActionResult<IEnumerable<InventoryCostLayerDto>>> GetCostLayers(
        Guid inventoryItemId,
        [FromQuery] Guid? warehouseId = null)
    {
        try
        {
            var layers = await _valuationService.GetCostLayersAsync(inventoryItemId, warehouseId);
            return Ok(layers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cost layers for item {ItemId}", inventoryItemId);
            return StatusCode(500, "An error occurred while retrieving cost layers");
        }
    }

    /// <summary>
    /// Gets total inventory value with optional filtering
    /// </summary>
    [HttpGet("total-value")]
    public async Task<ActionResult<decimal>> GetInventoryValue(
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] Guid? categoryId = null)
    {
        try
        {
            var totalValue = await _valuationService.GetInventoryValueAsync(warehouseId, categoryId);
            return Ok(new { totalValue });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating inventory value");
            return StatusCode(500, "An error occurred while calculating inventory value");
        }
    }

    /// <summary>
    /// Calculates weighted average cost for an item
    /// </summary>
    [HttpGet("items/{inventoryItemId}/weighted-average-cost")]
    public async Task<ActionResult<decimal>> GetWeightedAverageCost(Guid inventoryItemId)
    {
        try
        {
            var wac = await _valuationService.CalculateWeightedAverageCostAsync(inventoryItemId);
            return Ok(new { inventoryItemId, weightedAverageCost = wac });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating WAC for item {ItemId}", inventoryItemId);
            return StatusCode(500, "An error occurred while calculating weighted average cost");
        }
    }

    /// <summary>
    /// Gets FIFO cost for a specific quantity
    /// </summary>
    [HttpGet("items/{inventoryItemId}/fifo-cost")]
    public async Task<ActionResult<decimal>> GetFIFOCost(
        Guid inventoryItemId,
        [FromQuery] decimal quantity)
    {
        try
        {
            if (quantity <= 0)
                return BadRequest("Quantity must be greater than zero");

            var fifoCost = await _valuationService.GetFIFOCostAsync(inventoryItemId, quantity);
            return Ok(new { inventoryItemId, quantity, fifoCost, unitCost = fifoCost / quantity });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating FIFO cost for item {ItemId}", inventoryItemId);
            return StatusCode(500, "An error occurred while calculating FIFO cost");
        }
    }

    /// <summary>
    /// Gets LIFO cost for a specific quantity
    /// </summary>
    [HttpGet("items/{inventoryItemId}/lifo-cost")]
    public async Task<ActionResult<decimal>> GetLIFOCost(
        Guid inventoryItemId,
        [FromQuery] decimal quantity)
    {
        try
        {
            if (quantity <= 0)
                return BadRequest("Quantity must be greater than zero");

            var lifoCost = await _valuationService.GetLIFOCostAsync(inventoryItemId, quantity);
            return Ok(new { inventoryItemId, quantity, lifoCost, unitCost = lifoCost / quantity });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating LIFO cost for item {ItemId}", inventoryItemId);
            return StatusCode(500, "An error occurred while calculating LIFO cost");
        }
    }

    /// <summary>
    /// Recalculates cost layers for an item
    /// </summary>
    [HttpPost("items/{inventoryItemId}/recalculate")]
    public async Task<ActionResult> RecalculateCostLayers(Guid inventoryItemId)
    {
        try
        {
            await _valuationService.RecalculateCostLayersAsync(inventoryItemId);
            return Ok(new { message = "Cost layers recalculated successfully", inventoryItemId });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recalculating cost layers for item {ItemId}", inventoryItemId);
            return StatusCode(500, "An error occurred while recalculating cost layers");
        }
    }

    /// <summary>
    /// Gets all inventory movements for an item
    /// </summary>
    [HttpGet("items/{inventoryItemId}/movements")]
    public async Task<ActionResult> GetItemMovements(Guid inventoryItemId)
    {
        try
        {
            var movements = await _movementRepository.GetMovementsByItemAsync(inventoryItemId);
            return Ok(movements.Select(m => new
            {
                m.Id,
                m.MovementNumber,
                m.MovementType,
                m.Direction,
                m.Quantity,
                m.UnitCost,
                m.TotalValue,
                m.MovementDate,
                m.PostingDate,
                m.ReferenceType,
                m.ReferenceNumber,
                m.RunningBalance,
                m.RunningValue,
                m.IsPosted,
                m.IsReversal,
                WarehouseName = m.Warehouse.Name,
                LocationCode = m.Location != null ? m.Location.LocationCode : null,
                CreatedByName = m.CreatedBy != null ? m.CreatedBy.FullName : null,
                m.Notes
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving movements for item {ItemId}", inventoryItemId);
            return StatusCode(500, "An error occurred while retrieving movements");
        }
    }

    /// <summary>
    /// Gets all inventory movements for a warehouse
    /// </summary>
    [HttpGet("warehouses/{warehouseId}/movements")]
    public async Task<ActionResult> GetWarehouseMovements(Guid warehouseId)
    {
        try
        {
            var movements = await _movementRepository.GetMovementsByWarehouseAsync(warehouseId);
            return Ok(movements.Select(m => new
            {
                m.Id,
                m.MovementNumber,
                m.MovementType,
                m.Direction,
                m.Quantity,
                m.UnitCost,
                m.TotalValue,
                m.MovementDate,
                ItemCode = m.InventoryItem.ItemCode,
                ItemName = m.InventoryItem.Name,
                m.ReferenceType,
                m.ReferenceNumber,
                m.IsPosted
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving movements for warehouse {WarehouseId}", warehouseId);
            return StatusCode(500, "An error occurred while retrieving movements");
        }
    }

    /// <summary>
    /// Gets inventory movements by date range
    /// </summary>
    [HttpGet("movements")]
    public async Task<ActionResult> GetMovementsByDateRange(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var start = startDate ?? DateTime.UtcNow.AddMonths(-1);
            var end = endDate ?? DateTime.UtcNow;

            var movements = await _movementRepository.GetMovementsByDateRangeAsync(start, end);
            return Ok(movements.Select(m => new
            {
                m.Id,
                m.MovementNumber,
                m.MovementType,
                m.Direction,
                m.Quantity,
                m.UnitCost,
                m.TotalValue,
                m.MovementDate,
                ItemCode = m.InventoryItem.ItemCode,
                ItemName = m.InventoryItem.Name,
                WarehouseName = m.Warehouse.Name,
                m.ReferenceType,
                m.ReferenceNumber,
                m.IsPosted
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving movements by date range");
            return StatusCode(500, "An error occurred while retrieving movements");
        }
    }

    /// <summary>
    /// Gets inventory balances for an item across all warehouses
    /// </summary>
    [HttpGet("items/{inventoryItemId}/balances")]
    public async Task<ActionResult> GetItemBalances(Guid inventoryItemId)
    {
        try
        {
            var balances = await _balanceRepository.GetBalancesByItemAsync(inventoryItemId);
            return Ok(balances.Select(b => new
            {
                b.Id,
                b.WarehouseId,
                WarehouseName = b.Warehouse.Name,
                b.LocationId,
                LocationCode = b.Location != null ? b.Location.LocationCode : null,
                b.QuantityOnHand,
                b.QuantityAllocated,
                b.QuantityAvailable,
                b.QuantityOnOrder,
                b.TotalValue,
                b.AverageUnitCost,
                b.LastMovementDate,
                b.LastReceiptDate,
                b.LastIssueDate,
                b.LastRecalculatedAt
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving balances for item {ItemId}", inventoryItemId);
            return StatusCode(500, "An error occurred while retrieving balances");
        }
    }

    /// <summary>
    /// Gets inventory balances for a warehouse
    /// </summary>
    [HttpGet("warehouses/{warehouseId}/balances")]
    public async Task<ActionResult> GetWarehouseBalances(Guid warehouseId)
    {
        try
        {
            var balances = await _balanceRepository.GetBalancesByWarehouseAsync(warehouseId);
            return Ok(balances.Select(b => new
            {
                b.Id,
                b.InventoryItemId,
                ItemCode = b.InventoryItem.ItemCode,
                ItemName = b.InventoryItem.Name,
                CategoryName = b.InventoryItem.Category.Name,
                b.QuantityOnHand,
                b.QuantityAllocated,
                b.QuantityAvailable,
                b.TotalValue,
                b.AverageUnitCost,
                b.LastMovementDate
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving balances for warehouse {WarehouseId}", warehouseId);
            return StatusCode(500, "An error occurred while retrieving balances");
        }
    }

    /// <summary>
    /// Gets low stock balances
    /// </summary>
    [HttpGet("low-stock")]
    public async Task<ActionResult> GetLowStockBalances([FromQuery] Guid? warehouseId = null)
    {
        try
        {
            var balances = await _balanceRepository.GetLowStockBalancesAsync(warehouseId);
            return Ok(balances.Select(b => new
            {
                b.Id,
                b.InventoryItemId,
                ItemCode = b.InventoryItem.ItemCode,
                ItemName = b.InventoryItem.Name,
                b.WarehouseId,
                WarehouseName = b.Warehouse.Name,
                b.QuantityOnHand,
                ReorderLevel = b.InventoryItem.ReorderLevel,
                ReorderQuantity = b.InventoryItem.ReorderQuantity,
                Shortage = b.InventoryItem.ReorderLevel - b.QuantityOnHand,
                b.AverageUnitCost,
                EstimatedReorderCost = b.InventoryItem.ReorderQuantity * b.AverageUnitCost
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving low stock balances");
            return StatusCode(500, "An error occurred while retrieving low stock balances");
        }
    }

    /// <summary>
    /// Gets expiring inventory layers (for FIFO items with expiration dates)
    /// </summary>
    [HttpGet("expiring-layers")]
    public async Task<ActionResult> GetExpiringLayers([FromQuery] int daysAhead = 30)
    {
        try
        {
            var expirationDate = DateTime.UtcNow.AddDays(daysAhead);
            var layers = await _layerRepository.GetExpiringLayersAsync(expirationDate);
            
            return Ok(layers.Select(l => new
            {
                l.Id,
                l.LayerNumber,
                l.InventoryItemId,
                ItemCode = l.InventoryItem.ItemCode,
                ItemName = l.InventoryItem.Name,
                l.WarehouseId,
                WarehouseName = l.Warehouse.Name,
                l.RemainingQuantity,
                l.UnitCost,
                l.RemainingValue,
                l.ExpirationDate,
                DaysUntilExpiration = l.ExpirationDate.HasValue 
                    ? (l.ExpirationDate.Value - DateTime.UtcNow).Days 
                    : (int?)null,
                l.LotNumber
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expiring layers");
            return StatusCode(500, "An error occurred while retrieving expiring layers");
        }
    }

    /// <summary>
    /// Gets unposted movements
    /// </summary>
    [HttpGet("movements/unposted")]
    public async Task<ActionResult> GetUnpostedMovements()
    {
        try
        {
            var movements = await _movementRepository.GetUnpostedMovementsAsync();
            return Ok(movements.Select(m => new
            {
                m.Id,
                m.MovementNumber,
                m.MovementType,
                m.Direction,
                m.Quantity,
                m.UnitCost,
                m.TotalValue,
                m.MovementDate,
                ItemCode = m.InventoryItem.ItemCode,
                ItemName = m.InventoryItem.Name,
                WarehouseName = m.Warehouse.Name,
                m.ReferenceType,
                m.ReferenceNumber,
                CreatedByName = m.CreatedBy != null ? m.CreatedBy.FullName : null,
                m.CreatedAt
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving unposted movements");
            return StatusCode(500, "An error occurred while retrieving unposted movements");
        }
    }

    /// <summary>
    /// Gets balances requiring recalculation
    /// </summary>
    [HttpGet("balances/requiring-recalculation")]
    public async Task<ActionResult> GetBalancesRequiringRecalculation([FromQuery] int daysOld = 7)
    {
        try
        {
            var olderThan = DateTime.UtcNow.AddDays(-daysOld);
            var balances = await _balanceRepository.GetBalancesRequiringRecalculationAsync(olderThan);
            
            return Ok(balances.Select(b => new
            {
                b.Id,
                b.InventoryItemId,
                ItemCode = b.InventoryItem.ItemCode,
                ItemName = b.InventoryItem.Name,
                b.WarehouseId,
                WarehouseName = b.Warehouse.Name,
                b.QuantityOnHand,
                b.TotalValue,
                b.AverageUnitCost,
                b.LastRecalculatedAt,
                DaysSinceRecalculation = (DateTime.UtcNow - b.LastRecalculatedAt).Days
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving balances requiring recalculation");
            return StatusCode(500, "An error occurred while retrieving balances");
        }
    }

    /// <summary>
    /// Gets active layers for an item at a specific warehouse
    /// </summary>
    [HttpGet("items/{inventoryItemId}/warehouses/{warehouseId}/active-layers")]
    public async Task<ActionResult> GetActiveLayers(Guid inventoryItemId, Guid warehouseId)
    {
        try
        {
            var layers = await _layerRepository.GetActiveLayersAsync(inventoryItemId, warehouseId);
            return Ok(layers.Select(l => new
            {
                l.Id,
                l.LayerNumber,
                l.LayerDate,
                l.OriginalQuantity,
                l.RemainingQuantity,
                l.UnitCost,
                l.RemainingValue,
                l.SourceType,
                l.SourceReference,
                l.LotNumber,
                l.ExpirationDate,
                ConsumedQuantity = l.OriginalQuantity - l.RemainingQuantity,
                ConsumedPercentage = l.OriginalQuantity > 0 
                    ? ((l.OriginalQuantity - l.RemainingQuantity) / l.OriginalQuantity * 100) 
                    : 0
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active layers for item {ItemId} at warehouse {WarehouseId}", 
                inventoryItemId, warehouseId);
            return StatusCode(500, "An error occurred while retrieving active layers");
        }
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }
}
