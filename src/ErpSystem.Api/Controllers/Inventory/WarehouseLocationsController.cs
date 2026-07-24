using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Inventory;

[ApiController]
[Route("api/inventory/warehouse-locations")]
[Authorize]
public class WarehouseLocationsController : ControllerBase
{
    private readonly IWarehouseLocationRepository _locationRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<WarehouseLocationsController> _logger;
    private readonly IProcurementMasterDataChangeService? _masterDataChanges;

    public WarehouseLocationsController(
        IWarehouseLocationRepository locationRepository,
        IWarehouseRepository warehouseRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<WarehouseLocationsController> logger,
        IProcurementMasterDataChangeService? masterDataChanges = null)
    {
        _locationRepository = locationRepository;
        _warehouseRepository = warehouseRepository;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
        _masterDataChanges = masterDataChanges;
    }

    /// <summary>
    /// Gets all warehouse locations, optionally filtered by warehouse
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<WarehouseLocationDto>>> GetAll([FromQuery] Guid? warehouseId = null)
    {
        try
        {
            IEnumerable<WarehouseLocation> locations;
            if (warehouseId.HasValue)
                locations = await _locationRepository.GetLocationsByWarehouseAsync(warehouseId.Value);
            else
                locations = await _locationRepository.GetAllAsync();

            var dtos = locations.Select(MapToDto).ToList();
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving warehouse locations");
            return StatusCode(500, "An error occurred while retrieving warehouse locations");
        }
    }

    /// <summary>
    /// Gets a warehouse location by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WarehouseLocationDto>> GetById(Guid id)
    {
        try
        {
            var location = await _locationRepository.GetByIdAsync(id);
            if (location == null)
                return NotFound($"Warehouse location with ID {id} not found");

            return Ok(MapToDto(location));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving warehouse location {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the warehouse location");
        }
    }

    /// <summary>
    /// Creates a new warehouse location
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<WarehouseLocationDto>> Create([FromBody] CreateWarehouseLocationDto dto)
    {
        try
        {
            var protection = await GuardDirectMutationAsync(null, "WarehouseLocation.Create");
            if (protection is not null) return protection;
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var tenantId = _currentUserProvider.TenantId;
            if (tenantId == Guid.Empty)
                return Unauthorized("Tenant context is required");

            // Verify warehouse exists
            var warehouse = await _warehouseRepository.GetByIdAsync(dto.WarehouseId);
            if (warehouse == null)
                return BadRequest($"Warehouse with ID {dto.WarehouseId} not found");

            if (dto.IsConsignmentBin)
            {
                if (!dto.ConsignmentWarehouseId.HasValue || dto.ConsignmentWarehouseId.Value == Guid.Empty)
                {
                    return BadRequest("ConsignmentWarehouseId is required when IsConsignmentBin is true.");
                }

                var consignmentWarehouse = await _warehouseRepository.GetByIdAsync(dto.ConsignmentWarehouseId.Value);
                if (consignmentWarehouse == null)
                {
                    return BadRequest($"Consignment warehouse with ID {dto.ConsignmentWarehouseId.Value} not found");
                }

                if (!consignmentWarehouse.IsConsignmentWarehouse)
                {
                    return BadRequest("Selected ConsignmentWarehouseId is not marked as a consignment warehouse.");
                }
            }
            else
            {
                if (dto.ConsignmentWarehouseId.HasValue && dto.ConsignmentWarehouseId.Value != Guid.Empty)
                {
                    return BadRequest("ConsignmentWarehouseId can only be set when IsConsignmentBin is true.");
                }
            }

            // Check for duplicate location code
            var existing = await _locationRepository.GetByLocationCodeAsync(dto.LocationCode);
            if (existing != null)
                return BadRequest($"Location with code '{dto.LocationCode}' already exists");

            var location = new WarehouseLocation
            {
                WarehouseId = dto.WarehouseId,
                LocationCode = dto.LocationCode,
                Name = dto.Name,
                Description = dto.Description,
                LocationType = dto.LocationType,
                ParentLocationId = dto.ParentLocationId,
                IsPickingLocation = dto.IsPickingLocation,
                IsReceivingLocation = dto.IsReceivingLocation,
                IsConsignmentBin = dto.IsConsignmentBin,
                ConsignmentWarehouseId = dto.IsConsignmentBin ? dto.ConsignmentWarehouseId : null,
                MaxWeight = dto.MaxWeight,
                MaxVolume = dto.MaxVolume,
                MaxItems = dto.MaxItems,
                IsActive = true,
                TenantId = tenantId,
                CreatedById = _currentUserProvider.UserId
            };

            await _locationRepository.AddAsync(location);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Created warehouse location {Code} for tenant {TenantId}", location.LocationCode, tenantId);
            return CreatedAtAction(nameof(GetById), new { id = location.Id }, MapToDto(location));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating warehouse location");
            return StatusCode(500, "An error occurred while creating the warehouse location");
        }
    }

    /// <summary>
    /// Updates a warehouse location
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<WarehouseLocationDto>> Update(Guid id, [FromBody] UpdateWarehouseLocationDto dto)
    {
        try
        {
            var protection = await GuardDirectMutationAsync(id, "WarehouseLocation.Update");
            if (protection is not null) return protection;
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var location = await _locationRepository.GetByIdAsync(id);
            if (location == null)
                return NotFound($"Warehouse location with ID {id} not found");

            // Check for duplicate code (if changed)
            if (location.LocationCode != dto.LocationCode)
            {
                var existing = await _locationRepository.GetByLocationCodeAsync(dto.LocationCode);
                if (existing != null)
                    return BadRequest($"Location with code '{dto.LocationCode}' already exists");
            }

            // Validate consignment configuration
            if (dto.IsConsignmentBin)
            {
                if (!dto.ConsignmentWarehouseId.HasValue || dto.ConsignmentWarehouseId.Value == Guid.Empty)
                {
                    return BadRequest("ConsignmentWarehouseId is required when IsConsignmentBin is true.");
                }

                var consignmentWarehouse = await _warehouseRepository.GetByIdAsync(dto.ConsignmentWarehouseId.Value);
                if (consignmentWarehouse == null)
                {
                    return BadRequest($"Consignment warehouse with ID {dto.ConsignmentWarehouseId.Value} not found");
                }

                if (!consignmentWarehouse.IsConsignmentWarehouse)
                {
                    return BadRequest("Selected ConsignmentWarehouseId is not marked as a consignment warehouse.");
                }
            }
            else
            {
                if (dto.ConsignmentWarehouseId.HasValue && dto.ConsignmentWarehouseId.Value != Guid.Empty)
                {
                    return BadRequest("ConsignmentWarehouseId can only be set when IsConsignmentBin is true.");
                }
            }

            location.WarehouseId = dto.WarehouseId;
            location.LocationCode = dto.LocationCode;
            location.Name = dto.Name;
            location.Description = dto.Description;
            location.LocationType = dto.LocationType;
            location.ParentLocationId = dto.ParentLocationId;
            location.IsPickingLocation = dto.IsPickingLocation;
            location.IsReceivingLocation = dto.IsReceivingLocation;
            location.IsConsignmentBin = dto.IsConsignmentBin;
            location.ConsignmentWarehouseId = dto.IsConsignmentBin ? dto.ConsignmentWarehouseId : null;
            location.MaxWeight = dto.MaxWeight;
            location.MaxVolume = dto.MaxVolume;
            location.MaxItems = dto.MaxItems;
            location.IsActive = dto.IsActive;
            location.LastModifiedById = _currentUserProvider.UserId;
            location.UpdatedAt = DateTime.UtcNow;

            await _locationRepository.UpdateAsync(location);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated warehouse location {Id}", id);
            return Ok(MapToDto(location));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating warehouse location {Id}", id);
            return StatusCode(500, "An error occurred while updating the warehouse location");
        }
    }

    /// <summary>
    /// Deletes a warehouse location (soft delete)
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        try
        {
            var protection = await GuardDirectMutationAsync(id, "WarehouseLocation.Delete");
            if (protection is not null) return protection;
            var location = await _locationRepository.GetByIdAsync(id);
            if (location == null)
                return NotFound($"Warehouse location with ID {id} not found");

            location.IsDeleted = true;
            location.DeletedAt = DateTime.UtcNow;
            location.DeletedBy = _currentUserProvider.UserId.ToString();
            await _locationRepository.UpdateAsync(location);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Deleted warehouse location {Id}", id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting warehouse location {Id}", id);
            return StatusCode(500, "An error occurred while deleting the warehouse location");
        }
    }

    /// <summary>
    /// Reclassify existing stock currently stored in a bin that has been toggled to a consignment bin.
    /// This converts ownership: decreases owned/main inventory (physical warehouse) and increases consignment inventory (parent consignment warehouse),
    /// without changing the physical bin quantities.
    /// </summary>
    [HttpPost("{id:guid}/reclassify-stock-to-consignment")]
    public async Task<ActionResult<object>> ReclassifyStockToConsignment(Guid id)
    {
        try
        {
            var tenantId = _currentUserProvider.TenantId;
            if (tenantId == Guid.Empty)
            {
                return Unauthorized("Tenant context is required");
            }

            var location = await _locationRepository.GetByIdAsync(id);
            if (location == null || location.IsDeleted)
            {
                return NotFound($"Warehouse location with ID {id} not found");
            }

            if (!location.IsConsignmentBin)
            {
                return BadRequest("This location is not a consignment bin.");
            }

            if (!location.ConsignmentWarehouseId.HasValue || location.ConsignmentWarehouseId.Value == Guid.Empty)
            {
                return BadRequest("ConsignmentWarehouseId must be set on this location before reclassification.");
            }

            var sourceWarehouseId = location.WarehouseId;
            var consignmentWarehouseId = location.ConsignmentWarehouseId.Value;

            if (sourceWarehouseId == consignmentWarehouseId)
            {
                return Ok(new
                {
                    movedLines = 0,
                    totalQuantityMoved = 0m,
                    message = "Source warehouse and consignment warehouse are the same; nothing to reclassify."
                });
            }

            var sourceWarehouse = await _warehouseRepository.GetByIdAsync(sourceWarehouseId);
            if (sourceWarehouse == null)
            {
                return BadRequest($"Source warehouse with ID {sourceWarehouseId} not found");
            }

            var consignmentWarehouse = await _warehouseRepository.GetByIdAsync(consignmentWarehouseId);
            if (consignmentWarehouse == null)
            {
                return BadRequest($"Consignment warehouse with ID {consignmentWarehouseId} not found");
            }

            if (!consignmentWarehouse.IsConsignmentWarehouse)
            {
                return BadRequest("Selected ConsignmentWarehouseId is not marked as a consignment warehouse.");
            }

            if (sourceWarehouse.IsConsignmentWarehouse)
            {
                return BadRequest("Cannot reclassify stock from a consignment warehouse location.");
            }

            var inventoryLocationRepo = _unitOfWork.Repository<InventoryLocation>();
            var inventoryItemRepo = _unitOfWork.Repository<InventoryItem>();
            var warehouseQtyRepo = _unitOfWork.Repository<WarehouseQuantity>();
            var stockMovementRepo = _unitOfWork.Repository<StockMovement>();

            var invLocs = await inventoryLocationRepo
                .GetQueryable(il => il.TenantId == tenantId && il.LocationId == id && !il.IsDeleted && il.Quantity > 0)
                .AsNoTracking()
                .ToListAsync();

            if (invLocs.Count == 0)
            {
                return Ok(new
                {
                    movedLines = 0,
                    totalQuantityMoved = 0m,
                    message = "No stock found in this bin."
                });
            }

            var allocatedInBin = invLocs.Where(x => x.AllocatedQuantity > 0).ToList();
            if (allocatedInBin.Count > 0)
            {
                return BadRequest("This bin has allocated/reserved quantities. Clear allocations before reclassifying ownership.");
            }

            var now = DateTime.UtcNow;
            var userId = _currentUserProvider.UserId;

            var details = new List<object>();
            var totalQty = 0m;

            foreach (var il in invLocs)
            {
                var qtyToMove = il.Quantity;
                if (qtyToMove <= 0)
                {
                    continue;
                }

                var inventoryItem = await inventoryItemRepo
                    .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == il.InventoryItemId && !x.IsDeleted);

                if (inventoryItem == null)
                {
                    return BadRequest($"Inventory item {il.InventoryItemId} not found for tenant context.");
                }

                var sourceWq = await warehouseQtyRepo.FirstOrDefaultAsync(q =>
                    q.TenantId == tenantId && q.WarehouseId == sourceWarehouseId && q.InventoryItemId == il.InventoryItemId && !q.IsDeleted);

                if (sourceWq == null)
                {
                    return BadRequest($"Warehouse quantity not found for item {il.InventoryItemId} in source warehouse.");
                }

                if (sourceWq.CurrentStock < qtyToMove)
                {
                    return BadRequest(
                        $"Insufficient source warehouse stock for item {inventoryItem.ItemCode}. " +
                        $"Bin has {qtyToMove}, but source warehouse has {sourceWq.CurrentStock}.");
                }

                var destWq = await warehouseQtyRepo.FirstOrDefaultAsync(q =>
                    q.TenantId == tenantId && q.WarehouseId == consignmentWarehouseId && q.InventoryItemId == il.InventoryItemId && !q.IsDeleted);

                if (destWq == null)
                {
                    destWq = new WarehouseQuantity
                    {
                        TenantId = tenantId,
                        InventoryItemId = il.InventoryItemId,
                        WarehouseId = consignmentWarehouseId,
                        CurrentStock = 0,
                        AvailableStock = 0,
                        AllocatedStock = 0,
                        AverageCost = sourceWq.AverageCost != 0 ? sourceWq.AverageCost : inventoryItem.AverageCost,
                        LastMovementDate = now,
                        CreatedById = userId,
                        CreatedAt = now
                    };
                    await warehouseQtyRepo.AddAsync(destWq);
                }

                // Move warehouse-level quantities (ownership conversion).
                sourceWq.CurrentStock -= qtyToMove;
                sourceWq.AvailableStock = sourceWq.CurrentStock - sourceWq.AllocatedStock;
                sourceWq.LastMovementDate = now;
                sourceWq.UpdatedAt = now;
                sourceWq.LastModifiedById = userId;

                destWq.CurrentStock += qtyToMove;
                destWq.AvailableStock = destWq.CurrentStock - destWq.AllocatedStock;
                destWq.LastMovementDate = now;
                destWq.UpdatedAt = now;
                destWq.LastModifiedById = userId;

                await warehouseQtyRepo.UpdateAsync(sourceWq);
                await warehouseQtyRepo.UpdateAsync(destWq);

                // Reduce owned/main inventory totals (consignment stock is excluded from InventoryItem totals).
                if (inventoryItem.CurrentStock < qtyToMove)
                {
                    return BadRequest(
                        $"Insufficient owned inventory totals for item {inventoryItem.ItemCode}. " +
                        $"Owned total is {inventoryItem.CurrentStock}, but bin has {qtyToMove}.");
                }

                inventoryItem.CurrentStock -= qtyToMove;
                inventoryItem.AvailableStock = inventoryItem.CurrentStock - inventoryItem.AllocatedStock;
                inventoryItem.LastStockDate = now;
                inventoryItem.UpdatedAt = now;
                inventoryItem.LastModifiedById = userId;
                await inventoryItemRepo.UpdateAsync(inventoryItem);

                var unitCost = sourceWq.AverageCost != 0 ? sourceWq.AverageCost : inventoryItem.AverageCost;

                // Audit via stock movements (does not change physical bin quantities).
                var outMovement = new StockMovement
                {
                    TenantId = tenantId,
                    InventoryItemId = il.InventoryItemId,
                    MovementType = "ConsignmentReclass-Out",
                    Quantity = -qtyToMove,
                    UnitCost = unitCost,
                    TotalValue = -qtyToMove * unitCost,
                    MovementDate = now,
                    ReferenceType = ReferenceType.Manual,
                    ReferenceNumber = location.LocationCode,
                    ReferenceId = location.Id,
                    WarehouseId = sourceWarehouseId,
                    LocationId = location.Id,
                    Notes = $"Reclassified stock in bin {location.LocationCode} to consignment warehouse {consignmentWarehouse.Code}",
                    RunningBalance = inventoryItem.CurrentStock,
                    ProcessedById = userId
                };

                var inMovement = new StockMovement
                {
                    TenantId = tenantId,
                    InventoryItemId = il.InventoryItemId,
                    MovementType = "ConsignmentReclass-In",
                    Quantity = qtyToMove,
                    UnitCost = unitCost,
                    TotalValue = qtyToMove * unitCost,
                    MovementDate = now,
                    ReferenceType = ReferenceType.Manual,
                    ReferenceNumber = location.LocationCode,
                    ReferenceId = location.Id,
                    WarehouseId = consignmentWarehouseId,
                    LocationId = location.Id,
                    Notes = $"Reclassified stock in bin {location.LocationCode} from owned warehouse {sourceWarehouse.Code}",
                    RunningBalance = destWq.CurrentStock,
                    ProcessedById = userId
                };

                await stockMovementRepo.AddAsync(outMovement);
                await stockMovementRepo.AddAsync(inMovement);

                totalQty += qtyToMove;
                details.Add(new
                {
                    inventoryItemId = il.InventoryItemId,
                    itemCode = inventoryItem.ItemCode,
                    quantityMoved = qtyToMove,
                    unitCost
                });
            }

            await _unitOfWork.SaveChangesAsync();

            return Ok(new
            {
                movedLines = details.Count,
                totalQuantityMoved = totalQty,
                sourceWarehouseId,
                consignmentWarehouseId,
                locationId = location.Id,
                locationCode = location.LocationCode,
                details
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reclassifying stock to consignment for location {LocationId}", id);
            return StatusCode(500, "An error occurred while reclassifying stock to consignment.");
        }
    }

    private async Task<ObjectResult?> GuardDirectMutationAsync(Guid? id, string action)
    {
        if (_masterDataChanges is null) return null;
        var decision = await _masterDataChanges.CheckDirectMutationAsync(
            new[] { ProcurementMasterDataResourceType.WarehouseLocation }, id, action,
            HttpContext.TraceIdentifier, HttpContext.RequestAborted);
        return decision.Allowed ? null : Conflict(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Staged warehouse-location change required",
            Detail = decision.Message,
            Instance = HttpContext.Request.Path,
            Extensions = { ["code"] = decision.Code, ["correlationId"] = decision.CorrelationId, ["policyId"] = decision.PolicyId }
        });
    }

    private static WarehouseLocationDto MapToDto(WarehouseLocation entity)
    {
        return new WarehouseLocationDto
        {
            Id = entity.Id,
            WarehouseId = entity.WarehouseId,
            LocationCode = entity.LocationCode,
            Name = entity.Name,
            Description = entity.Description,
            LocationType = entity.LocationType,
            ParentLocationId = entity.ParentLocationId,
            IsActive = entity.IsActive,
            IsPickingLocation = entity.IsPickingLocation,
            IsReceivingLocation = entity.IsReceivingLocation,
            IsConsignmentBin = entity.IsConsignmentBin,
            ConsignmentWarehouseId = entity.ConsignmentWarehouseId,
            MaxWeight = entity.MaxWeight,
            MaxVolume = entity.MaxVolume,
            MaxItems = entity.MaxItems,
            CurrentWeight = entity.CurrentWeight,
            CurrentVolume = entity.CurrentVolume,
            CurrentItemCount = entity.CurrentItemCount
        };
    }
}

