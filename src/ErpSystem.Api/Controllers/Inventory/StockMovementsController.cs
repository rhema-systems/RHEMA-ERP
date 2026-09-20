using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Inventory;

/// <summary>
/// API controller for querying stock movements
/// </summary>
[ApiController]
[Route("api/inventory/stock-movements")]
[Authorize]
public class StockMovementsController : ControllerBase
{
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly IInventoryMovementRepository _inventoryMovementRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<StockMovementsController> _logger;
    private readonly IUnitOfWork _unitOfWork;

    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public StockMovementsController(
        IStockMovementRepository stockMovementRepository,
        IInventoryMovementRepository inventoryMovementRepository,
        ICurrentUserProvider currentUserProvider,
        ILogger<StockMovementsController> logger,
        IUnitOfWork unitOfWork)
    {
        _stockMovementRepository = stockMovementRepository;
        _inventoryMovementRepository = inventoryMovementRepository;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Gets stock movements with optional filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<StockMovementDto>>> GetStockMovements(
        [FromQuery] Guid? inventoryItemId = null,
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] string? movementType = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? referenceNumber = null,
        [FromQuery] int limit = 100)
    {
        try
        {
            _logger.LogInformation(
                "GetStockMovements called with: inventoryItemId={InventoryItemId}, warehouseId={WarehouseId}, movementType={MovementType}, startDate={StartDate}, endDate={EndDate}, referenceNumber={ReferenceNumber}, limit={Limit}",
                inventoryItemId, warehouseId, movementType, startDate, endDate, referenceNumber, limit);

            var tenantId = GetTenantId();

            // Inventory valuation is in functional currency. Return only this display
            // metadata so stores users do not need access to Finance settings APIs.
            var settings = await _unitOfWork.Repository<FinanceSettings>()
                .FirstOrDefaultAsync(value => value.TenantId == tenantId && !value.IsDeleted);
            var currencyCode = settings?.BaseCurrency?.Trim().ToUpperInvariant();
            if (currencyCode?.Length != 3 || !currencyCode.All(c => c is >= 'A' and <= 'Z'))
                currencyCode = null;

            // We have two movement systems:
            // 1) StockMovement (legacy operational movement log)
            // 2) InventoryMovement (valuation source of truth)
            // The Stock Movements page should show both (especially PO receipts, which post InventoryMovements).

            // Base queries (tenant-scoped)
            var stockQuery = _stockMovementRepository
                .GetQueryable(sm => sm.TenantId == tenantId)
                .AsNoTracking()
                .Include(sm => sm.InventoryItem)
                .Include(sm => sm.Warehouse)
                .Include(sm => sm.Location)
                    .ThenInclude(l => l!.Warehouse)
                .AsQueryable();

            var invQuery = _inventoryMovementRepository
                .GetQueryable(m => m.TenantId == tenantId && m.IsPosted)
                .AsNoTracking()
                .Include(m => m.InventoryItem)
                .Include(m => m.Warehouse)
                .Include(m => m.Location)
                .Include(m => m.CreatedBy)
                .AsQueryable();

            // If searching by reference number, skip the default date window to allow searching all history
            if (string.IsNullOrWhiteSpace(referenceNumber))
            {
                var effectiveStartDate = startDate ?? DateTime.UtcNow.AddDays(-30);
                var effectiveEndDate = endDate ?? DateTime.UtcNow;
                stockQuery = stockQuery.Where(sm => sm.MovementDate >= effectiveStartDate && sm.MovementDate <= effectiveEndDate);
                invQuery = invQuery.Where(m => m.MovementDate >= effectiveStartDate && m.MovementDate <= effectiveEndDate);
            }
            else
            {
                if (startDate.HasValue)
                {
                    stockQuery = stockQuery.Where(sm => sm.MovementDate >= startDate.Value);
                    invQuery = invQuery.Where(m => m.MovementDate >= startDate.Value);
                }
                if (endDate.HasValue)
                {
                    stockQuery = stockQuery.Where(sm => sm.MovementDate <= endDate.Value);
                    invQuery = invQuery.Where(m => m.MovementDate <= endDate.Value);
                }

                var lowerRef = referenceNumber.Trim().ToLower();
                stockQuery = stockQuery.Where(sm => sm.ReferenceNumber != null && sm.ReferenceNumber.ToLower().Contains(lowerRef));
                invQuery = invQuery.Where(m => m.ReferenceNumber != null && m.ReferenceNumber.ToLower().Contains(lowerRef));
            }

            if (inventoryItemId.HasValue && inventoryItemId.Value != Guid.Empty)
            {
                stockQuery = stockQuery.Where(sm => sm.InventoryItemId == inventoryItemId.Value);
                invQuery = invQuery.Where(m => m.InventoryItemId == inventoryItemId.Value);
            }

            if (warehouseId.HasValue && warehouseId.Value != Guid.Empty)
            {
                stockQuery = stockQuery.Where(sm => sm.WarehouseId == warehouseId.Value);
                invQuery = invQuery.Where(m => m.WarehouseId == warehouseId.Value);
            }

            if (!string.IsNullOrWhiteSpace(movementType))
            {
                var lowerType = movementType.Trim().ToLower();
                stockQuery = stockQuery.Where(sm => sm.MovementType.ToLower() == lowerType);
            }

            var stockMovements = await stockQuery
                .OrderByDescending(sm => sm.MovementDate)
                .Take(Math.Clamp(limit, 1, 500))
                .ToListAsync();

            var invMovements = await invQuery
                .OrderByDescending(m => m.MovementDate)
                .Take(Math.Clamp(limit, 1, 500))
                .ToListAsync();

            var combined = stockMovements
                .Select(MapToDto)
                .Concat(invMovements.Select(MapToDto))
                .ToList();

            // Apply movement type filter to InventoryMovements after mapping (so PurchaseReceipt maps to "Receipt", etc.).
            if (!string.IsNullOrWhiteSpace(movementType))
            {
                combined = combined
                    .Where(m => string.Equals(m.MovementType, movementType.Trim(), StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            var result = combined
                .OrderByDescending(m => m.MovementDate)
                .Take(Math.Clamp(limit, 1, 500))
                .ToList();

            foreach (var movement in result)
                movement.CurrencyCode = currencyCode;

            _logger.LogInformation("Returning {Count} movements (StockMovements={StockCount}, InventoryMovements={InvCount})",
                result.Count, stockMovements.Count, invMovements.Count);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving stock movements");
            return StatusCode(500, "An error occurred while retrieving stock movements");
        }
    }

    /// <summary>
    /// Gets stock movements for a specific inventory item
    /// </summary>
    [HttpGet("by-item/{inventoryItemId}")]
    public async Task<ActionResult<IEnumerable<StockMovementDto>>> GetByItem(
        Guid inventoryItemId,
        [FromQuery] int limit = 50)
    {
        try
        {
            // Reuse the unified endpoint behavior.
            return await GetStockMovements(inventoryItemId: inventoryItemId, limit: limit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving stock movements for item {ItemId}", inventoryItemId);
            return StatusCode(500, "An error occurred while retrieving stock movements");
        }
    }

    /// <summary>
    /// Gets stock movements for a specific warehouse
    /// </summary>
    [HttpGet("by-warehouse/{warehouseId}")]
    public async Task<ActionResult<IEnumerable<StockMovementDto>>> GetByWarehouse(
        Guid warehouseId,
        [FromQuery] int limit = 50)
    {
        try
        {
            // Reuse the unified endpoint behavior.
            return await GetStockMovements(warehouseId: warehouseId, limit: limit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving stock movements for warehouse {WarehouseId}", warehouseId);
            return StatusCode(500, "An error occurred while retrieving stock movements");
        }
    }

    /// <summary>
    /// Gets distinct movement types from existing data (for filter dropdown)
    /// </summary>
    [HttpGet("movement-types")]
    public async Task<ActionResult<IEnumerable<string>>> GetMovementTypes()
    {
        try
        {
            var tenantId = GetTenantId();
            var start = DateTime.UtcNow.AddDays(-365);
            var end = DateTime.UtcNow;

            var stockTypes = await _stockMovementRepository
                .GetQueryable(sm => sm.TenantId == tenantId && sm.MovementDate >= start && sm.MovementDate <= end)
                .AsNoTracking()
                .Select(sm => sm.MovementType)
                .Distinct()
                .ToListAsync();

            var invTypesRaw = await _inventoryMovementRepository
                .GetQueryable(m => m.TenantId == tenantId && m.IsPosted && m.MovementDate >= start && m.MovementDate <= end)
                .AsNoTracking()
                .Select(m => new { m.MovementType, m.Direction })
                .Distinct()
                .ToListAsync();

            var invTypes = invTypesRaw
                .Select(x => MapMovementType(x.MovementType, x.Direction))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var types = stockTypes
                .Concat(invTypes)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(t => t)
                .ToList();

            return Ok(types);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving movement types");
            return StatusCode(500, "An error occurred while retrieving movement types");
        }
    }

    /// <summary>
    /// Debug endpoint to see what warehouse data exists on movements
    /// </summary>
    [HttpGet("debug-warehouse-data")]
    public async Task<ActionResult<object>> DebugWarehouseData()
    {
        try
        {
            var start = DateTime.UtcNow.AddDays(-30);
            var end = DateTime.UtcNow;
            var movements = (await _stockMovementRepository.GetMovementsByDateRangeAsync(start, end)).ToList();

            var debugInfo = new
            {
                TotalMovements = movements.Count,
                MovementsWithWarehouseId = movements.Count(m => m.WarehouseId != null),
                MovementsWithLocationId = movements.Count(m => m.LocationId != null),
                UniqueWarehouseIds = movements
                    .Where(m => m.WarehouseId != null)
                    .Select(m => new { m.WarehouseId, WarehouseName = m.Warehouse?.Name })
                    .Distinct()
                    .ToList(),
                SampleMovements = movements.Take(5).Select(m => new
                {
                    m.Id,
                    m.MovementType,
                    m.WarehouseId,
                    WarehouseName = m.Warehouse?.Name,
                    m.LocationId
                }).ToList()
            };

            return Ok(debugInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in debug endpoint");
            return StatusCode(500, ex.Message);
        }
    }

    private static StockMovementDto MapToDto(StockMovement m)
    {
        return new StockMovementDto
        {
            Id = m.Id,
            MovementNumber = $"SM-{m.Id.ToString()[..8].ToUpper()}",
            InventoryItemId = m.InventoryItemId,
            ItemCode = m.InventoryItem?.ItemCode ?? "",
            ItemName = m.InventoryItem?.Name ?? "",
            MovementType = m.MovementType,
            Quantity = m.Quantity,
            UnitCost = m.UnitCost,
            TotalCost = m.TotalValue,
            SourceLocationId = m.MovementType.Contains("Out") || m.MovementType.Contains("Transfer") ? m.LocationId : null,
            SourceLocationName = m.MovementType.Contains("Out") || m.MovementType.Contains("Transfer") ? m.Location?.LocationCode : null,
            DestinationLocationId = m.MovementType.Contains("In") || m.MovementType.Contains("Receipt") ? m.LocationId : null,
            DestinationLocationName = m.MovementType.Contains("In") || m.MovementType.Contains("Receipt") ? m.Location?.LocationCode : null,
            ReferenceType = m.ReferenceType.ToString(),
            ReferenceId = m.ReferenceId,
            ReferenceNumber = m.ReferenceNumber ?? "",
            MovementDate = m.MovementDate,
            Notes = m.Notes,
            CreatedByName = null // TODO: Add when user tracking is available
        };
    }

    private static StockMovementDto MapToDto(InventoryMovement m)
    {
        var mappedType = MapMovementType(m.MovementType, m.Direction);

        // For the UI: treat Quantity as absolute and let the movement type determine +/- display.
        // (The page already uses Math.Abs(quantity) and prefixes sign based on type.)
        var locationCode = m.Location?.LocationCode;

        return new StockMovementDto
        {
            Id = m.Id,
            MovementNumber = m.MovementNumber,
            InventoryItemId = m.InventoryItemId,
            ItemCode = m.InventoryItem?.ItemCode ?? "",
            ItemName = m.InventoryItem?.Name ?? "",
            MovementType = mappedType,
            Quantity = m.Quantity,
            UnitCost = m.UnitCost,
            TotalCost = m.TotalValue,
            SourceLocationId = m.Direction == MovementDirection.Out ? m.LocationId : null,
            SourceLocationName = m.Direction == MovementDirection.Out ? locationCode : null,
            DestinationLocationId = m.Direction == MovementDirection.In ? m.LocationId : null,
            DestinationLocationName = m.Direction == MovementDirection.In ? locationCode : null,
            ReferenceType = m.ReferenceType.ToString(),
            ReferenceId = m.ReferenceId,
            ReferenceNumber = m.ReferenceNumber ?? "",
            MovementDate = m.MovementDate,
            Notes = m.Notes,
            CreatedByName = m.CreatedBy?.FullName
        };
    }

    private static string MapMovementType(InventoryMovementType movementType, MovementDirection direction)
    {
        // Map valuation movement types to the legacy StockMovement types used by the UI.
        return movementType switch
        {
            InventoryMovementType.PurchaseReceipt => "Receipt",
            InventoryMovementType.CustomerReturn => "Return",
            InventoryMovementType.SupplierReturn => "Issue", // outbound return to supplier

            InventoryMovementType.SalesIssue => "Sale",
            InventoryMovementType.RequisitionIssue => "Issue",
            InventoryMovementType.RequisitionReturn => direction == MovementDirection.In ? "Return" : "Issue",

            InventoryMovementType.TransferIn => "Transfer-In",
            InventoryMovementType.TransferOut => "Transfer-Out",

            InventoryMovementType.AdjustmentIn => "Adjustment+",
            InventoryMovementType.AdjustmentOut => "Adjustment-",
            InventoryMovementType.CountAdjustment => direction == MovementDirection.In ? "Adjustment+" : "Adjustment-",
            InventoryMovementType.OpeningBalance => direction == MovementDirection.In ? "Adjustment+" : "Adjustment-",

            InventoryMovementType.ProductionReceipt => "Production",
            InventoryMovementType.ProductionIssue => "Consumption",

            InventoryMovementType.Scrap => "Scrap",
            _ => movementType.ToString()
        };
    }

    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
        {
            tenantId = DefaultTenantId;
        }

        return tenantId;
    }
}

/// <summary>
/// DTO for stock movement data
/// </summary>
public class StockMovementDto
{
    public string? CurrencyCode { get; set; }
    public Guid Id { get; set; }
    public string MovementNumber { get; set; } = string.Empty;
    public Guid InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string MovementType { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public Guid? SourceLocationId { get; set; }
    public string? SourceLocationName { get; set; }
    public Guid? DestinationLocationId { get; set; }
    public string? DestinationLocationName { get; set; }
    public string ReferenceType { get; set; } = string.Empty;
    public Guid? ReferenceId { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public DateTime MovementDate { get; set; }
    public string? Notes { get; set; }
    public string? CreatedByName { get; set; }
}
