using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Api.Controllers.Inventory;

/// <summary>
/// Controller for managing warehouse item assignments and quantities
/// </summary>
[ApiController]
[Route("api/inventory/warehouse-items")]
[Authorize]
public class WarehouseItemsController : ControllerBase
{
    private const string ManagePermission = "procurement.inventory.master-data.manage";
    private readonly IWarehouseQuantityRepository _warehouseQuantityRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IInventoryItemRepository _inventoryItemRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IProcurementAccessControlService _accessControl;
    private readonly ILogger<WarehouseItemsController> _logger;

    public WarehouseItemsController(
        IWarehouseQuantityRepository warehouseQuantityRepository,
        IWarehouseRepository warehouseRepository,
        IInventoryItemRepository inventoryItemRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        IProcurementAccessControlService accessControl,
        ILogger<WarehouseItemsController> logger)
    {
        _warehouseQuantityRepository = warehouseQuantityRepository;
        _warehouseRepository = warehouseRepository;
        _inventoryItemRepository = inventoryItemRepository;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _accessControl = accessControl;
        _logger = logger;
    }

    /// <summary>
    /// Gets all warehouse items with their quantities, optionally filtered by warehouse
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<WarehouseItemDto>>> GetAll([FromQuery] Guid? warehouseId = null)
    {
        try
        {
            IEnumerable<WarehouseQuantity> items;

            if (warehouseId.HasValue)
            {
                items = await _warehouseQuantityRepository.GetByWarehouseAsync(warehouseId.Value);
            }
            else
            {
                items = await _warehouseQuantityRepository.GetAllWithDetailsAsync();
            }

            var dtos = items.Select(MapToDto).ToList();
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving warehouse items");
            return StatusCode(500, "An error occurred while retrieving warehouse items");
        }
    }

    /// <summary>
    /// Gets a specific warehouse item by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WarehouseItemDto>> GetById(Guid id)
    {
        try
        {
            var item = await _warehouseQuantityRepository.GetByIdWithDetailsAsync(id);
            if (item == null)
                return NotFound($"Warehouse item with ID {id} not found");

            return Ok(MapToDto(item));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving warehouse item {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the warehouse item");
        }
    }

    /// <summary>
    /// Gets warehouse items by inventory item ID (shows which warehouses have this item)
    /// </summary>
    [HttpGet("by-item/{inventoryItemId:guid}")]
    public async Task<ActionResult<IEnumerable<WarehouseItemDto>>> GetByInventoryItem(Guid inventoryItemId)
    {
        try
        {
            var items = await _warehouseQuantityRepository.GetByInventoryItemIdAsync(inventoryItemId);
            var dtos = items.Select(MapToDto).ToList();
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving warehouse items for inventory item {Id}", inventoryItemId);
            return StatusCode(500, "An error occurred while retrieving warehouse items");
        }
    }

    /// <summary>
    /// Assigns one or more items to one or more warehouses (bulk assignment)
    /// </summary>
    [HttpPost("assign")]
    public async Task<ActionResult<BulkAssignmentResultDto>> AssignItemsToWarehouses([FromBody] BulkAssignItemsDto dto)
    {
        try
        {
            if (!await HasMutationPermissionAsync("warehouse-item-assignment"))
                return Forbid();

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var tenantId = _currentUserProvider.TenantId;
            if (tenantId == Guid.Empty)
                return Unauthorized("Tenant context is required");

            var result = new BulkAssignmentResultDto();

            foreach (var warehouseId in dto.WarehouseIds)
            {
                var warehouse = await _warehouseRepository.GetByIdAsync(warehouseId);
                if (warehouse == null)
                {
                    result.Errors.Add($"Warehouse {warehouseId} not found");
                    continue;
                }

                foreach (var itemId in dto.InventoryItemIds)
                {
                    var item = await _inventoryItemRepository.GetByIdAsync(itemId);
                    if (item == null)
                    {
                        result.Errors.Add($"Inventory item {itemId} not found");
                        continue;
                    }

                    // Check if already exists
                    var existing = await _warehouseQuantityRepository.GetByWarehouseAndItemAsync(warehouseId, itemId);
                    if (existing != null)
                    {
                        result.Skipped++;
                        continue;
                    }

                    var warehouseQuantity = new WarehouseQuantity
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        WarehouseId = warehouseId,
                        InventoryItemId = itemId,
                        CurrentStock = 0,
                        AvailableStock = 0,
                        AllocatedStock = 0,
                        ReorderLevel = dto.ReorderLevel,
                        MaxStock = dto.MaxStock,
                        AverageCost = item.AverageCost,
                        CreatedById = _currentUserProvider.UserId,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _warehouseQuantityRepository.AddAsync(warehouseQuantity);
                    result.Created++;
                }
            }

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Bulk assignment completed: {Created} created, {Skipped} skipped, {Errors} errors",
                result.Created, result.Skipped, result.Errors.Count);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning items to warehouses");
            return StatusCode(500, "An error occurred while assigning items to warehouses");
        }
    }

    /// <summary>
    /// Updates stocking parameters for a warehouse item. Quantity changes must
    /// use a governed inventory transaction.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<WarehouseItemDto>> UpdateMetadata(Guid id, [FromBody] UpdateWarehouseItemDto dto)
    {
        try
        {
            if (!await HasMutationPermissionAsync($"warehouse-item:{id:N}:metadata"))
                return Forbid();

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var item = await _warehouseQuantityRepository.GetByIdAsync(id);
            if (item == null)
                return NotFound($"Warehouse item with ID {id} not found");

            item.ReorderLevel = dto.ReorderLevel;
            item.MaxStock = dto.MaxStock;
            item.Notes = dto.Notes;
            item.LastModifiedById = _currentUserProvider.UserId;
            item.UpdatedAt = DateTime.UtcNow;

            await _warehouseQuantityRepository.UpdateAsync(item);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated stocking parameters for warehouse item {Id}", id);
            return Ok(MapToDto(item));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating warehouse item {Id}", id);
            return StatusCode(500, "An error occurred while updating the warehouse item");
        }
    }

    /// <summary>
    /// Removes an item from a warehouse (soft delete)
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        try
        {
            if (!await HasMutationPermissionAsync($"warehouse-item:{id:N}:delete"))
                return Forbid();

            var item = await _warehouseQuantityRepository.GetByIdAsync(id);
            if (item == null)
                return NotFound($"Warehouse item with ID {id} not found");

            if (item.CurrentStock != 0 || item.AvailableStock != 0 || item.AllocatedStock != 0)
                return Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Warehouse assignment is not empty",
                    Detail = "Only an empty warehouse assignment can be removed. Complete the governed stock transfer, issue, return, or adjustment first.",
                    Instance = HttpContext.Request.Path,
                    Extensions = { ["code"] = "WAREHOUSE_ITEM_BALANCE_NOT_ZERO" }
                });

            item.IsDeleted = true;
            item.DeletedAt = DateTime.UtcNow;
            item.DeletedBy = _currentUserProvider.UserId.ToString();
            await _warehouseQuantityRepository.UpdateAsync(item);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Removed warehouse item {Id}", id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing warehouse item {Id}", id);
            return StatusCode(500, "An error occurred while removing the warehouse item");
        }
    }

    private async Task<bool> HasMutationPermissionAsync(string sourceReference)
    {
        try
        {
            var decision = await _accessControl.EnforceCapabilityAsync(new ProcurementAccessCapabilityRequest
            {
                PermissionCode = ManagePermission,
                SourceType = "WarehouseItemAssignment",
                SourceReference = sourceReference
            }, HttpContext.TraceIdentifier, HttpContext.RequestAborted);
            return decision.Allowed;
        }
        catch (Exception exception) when (exception is ProcurementAccessAuthorizationException or ProcurementAccessValidationException)
        {
            return false;
        }
    }

    private static WarehouseItemDto MapToDto(WarehouseQuantity entity)
    {
        return new WarehouseItemDto
        {
            Id = entity.Id,
            WarehouseId = entity.WarehouseId,
            WarehouseName = entity.Warehouse?.Name ?? "",
            WarehouseCode = entity.Warehouse?.Code ?? "",
            InventoryItemId = entity.InventoryItemId,
            ItemCode = entity.InventoryItem?.ItemCode ?? "",
            ItemName = entity.InventoryItem?.Name ?? "",
            ItemType = entity.InventoryItem?.ItemType.ToString() ?? "",
            CategoryName = entity.InventoryItem?.Category?.Name,
            UnitOfMeasure = entity.InventoryItem?.UnitOfMeasure,
            CurrentStock = entity.CurrentStock,
            AvailableStock = entity.AvailableStock,
            AllocatedStock = entity.AllocatedStock,
            ReorderLevel = entity.ReorderLevel,
            MaxStock = entity.MaxStock,
            AverageCost = entity.AverageCost,
            LastMovementDate = entity.LastMovementDate,
            LastStockTakeDate = entity.LastStockTakeDate,
            Notes = entity.Notes,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}

#region DTOs

public class WarehouseItemDto
{
    public Guid Id { get; set; }
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public string WarehouseCode { get; set; } = string.Empty;
    public Guid InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public string? UnitOfMeasure { get; set; }
    public decimal CurrentStock { get; set; }
    public decimal AvailableStock { get; set; }
    public decimal AllocatedStock { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal MaxStock { get; set; }
    public decimal AverageCost { get; set; }
    public DateTime? LastMovementDate { get; set; }
    public DateTime? LastStockTakeDate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class BulkAssignItemsDto
{
    [Required]
    [MinLength(1, ErrorMessage = "At least one inventory item is required")]
    public List<Guid> InventoryItemIds { get; set; } = new();

    [Required]
    [MinLength(1, ErrorMessage = "At least one warehouse is required")]
    public List<Guid> WarehouseIds { get; set; } = new();

    public decimal ReorderLevel { get; set; } = 0;
    public decimal MaxStock { get; set; } = 0;
}

public class UpdateWarehouseItemDto
{
    [Range(0, double.MaxValue)]
    public decimal ReorderLevel { get; set; }

    [Range(0, double.MaxValue)]
    public decimal MaxStock { get; set; }

    public string? Notes { get; set; }
}

public class BulkAssignmentResultDto
{
    public int Created { get; set; }
    public int Skipped { get; set; }
    public List<string> Errors { get; set; } = new();
}

#endregion

