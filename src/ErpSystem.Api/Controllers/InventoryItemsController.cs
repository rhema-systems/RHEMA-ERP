using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using AutoMapper;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services;
using ErpSystem.Core.Services.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers;

/// <summary>
/// API Controller for managing inventory items
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "InternalOnly")]
public class InventoryItemsController : ControllerBase
{
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
    private readonly IInventoryItemIdentifierService? _identifierService;
    private readonly IInventoryItemProfileService? _profileService;
    private readonly IProcurementAccessControlService _access;
    private readonly IAuditLogService? _auditLog;
    private readonly IUnitOfWork? _unitOfWork;
    private readonly IWarehouseDefaultLocationService? _defaultLocations;

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
        IProcurementAccessControlService access,
        IMapper mapper,
        ILogger<InventoryItemsController> logger,
        IProcurementMasterDataChangeService? masterDataChanges = null,
        IInventoryItemIdentifierService? identifierService = null,
        IAuditLogService? auditLog = null,
        IUnitOfWork? unitOfWork = null,
        IInventoryItemProfileService? profileService = null,
        IWarehouseDefaultLocationService? defaultLocations = null)
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
        _access = access;
        _mapper = mapper;
        _logger = logger;
        _masterDataChanges = masterDataChanges;
        _identifierService = identifierService;
        _profileService = profileService;
        _auditLog = auditLog;
        _unitOfWork = unitOfWork;
        _defaultLocations = defaultLocations ?? (unitOfWork == null ? null : new WarehouseDefaultLocationService(unitOfWork, currentUserProvider));
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
            var protection = await GuardDirectMutationAsync(null, "InventoryItem.Create");
            if (protection is not null) return protection;
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
            inventoryItem.TenantId = GetTenantId();
            if (_profileService is not null)
            {
                await _profileService.NormalizeAndValidateAsync(inventoryItem, null, HttpContext.RequestAborted);
            }
            else
            {
                NormalizeIdentifiers(inventoryItem);
                if (_identifierService is not null)
                {
                    await _identifierService.ValidateItemIdentifiersAsync(
                        inventoryItem.TenantId,
                        null,
                        inventoryItem.Barcode,
                        inventoryItem.AlternateBarcode,
                        inventoryItem.QRCode,
                        HttpContext.RequestAborted);
                }
            }

            var assignWarehouse = createDto.DefaultWarehouseId.HasValue && createDto.DefaultWarehouseId != Guid.Empty &&
                createDto.ItemType is ItemType.StockItem or ItemType.FixedAsset;
            if (assignWarehouse)
            {
                var warehouse = await _warehouseRepository.GetByIdAsync(createDto.DefaultWarehouseId!.Value);
                if (warehouse == null || warehouse.TenantId != inventoryItem.TenantId || warehouse.IsDeleted || !warehouse.IsActive)
                    return BadRequest("Select an active default warehouse in the current tenant.");
                if (_unitOfWork == null || _defaultLocations == null)
                    throw new InvalidOperationException("Default warehouse assignment is unavailable.");
            }
            var createdItem = inventoryItem;
            var auditQueued = false;
            async Task PersistCreatedAsync(CancellationToken ct)
            {
                createdItem = await _inventoryItemRepository.AddAsync(inventoryItem);
                auditQueued = await QueueAuditAsync("InventoryItem.Created", createdItem, null);
                await _inventoryItemRepository.SaveChangesAsync();
                if (assignWarehouse)
                {
                    await _warehouseQuantityRepository.AddAsync(new WarehouseQuantity
                    {
                        TenantId = inventoryItem.TenantId, WarehouseId = createDto.DefaultWarehouseId!.Value,
                        InventoryItemId = createdItem.Id, AverageCost = createdItem.AverageCost,
                        CreatedById = _currentUserProvider.UserId
                    });
                    await _unitOfWork!.SaveChangesAsync(ct);
                    await _defaultLocations!.EnsureItemAssignmentAsync(createDto.DefaultWarehouseId!.Value, createdItem.Id, _currentUserProvider.UserId, ct);
                }
            }
            if (_unitOfWork != null) await _unitOfWork.ExecuteInTransactionAsync(PersistCreatedAsync, HttpContext.RequestAborted);
            else await PersistCreatedAsync(HttpContext.RequestAborted);
            if (!auditQueued) await AuditFallbackAsync("InventoryItem.Created", createdItem, null);

            var itemDto = _mapper.Map<InventoryItemDto>(createdItem);
            return CreatedAtAction(nameof(GetInventoryItem), new { id = createdItem.Id }, itemDto);
        }
        catch (InventoryIdentifierConflictException ex)
        {
            return Conflict(new ProblemDetails { Status = 409, Title = "Duplicate inventory identifier", Detail = ex.Message, Extensions = { ["code"] = "INVENTORY_IDENTIFIER_DUPLICATE", ["identifier"] = ex.Identifier } });
        }
        catch (InventoryItemProfileValidationException ex)
        {
            return UnprocessableEntity(ProfileProblem(ex));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
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

            if (!MatchesRowVersion(updateDto.RowVersion, existingItem.RowVersion))
            {
                return Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Inventory item changed",
                    Detail = "The item profile was changed by another user. Reload it before saving.",
                    Extensions = { ["code"] = "ITEM_PROFILE_CONCURRENCY_CONFLICT" }
                });
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

            var previousProfile = SnapshotProfile(existingItem);
            _mapper.Map(updateDto, existingItem);
            existingItem.Status = updateDto.IsActive ? updateDto.Status : ItemStatus.Inactive;
            if (_profileService is not null)
            {
                await _profileService.NormalizeAndValidateAsync(existingItem, existingItem.Id, HttpContext.RequestAborted);
            }
            else
            {
                NormalizeIdentifiers(existingItem);
                if (_identifierService is not null)
                {
                    await _identifierService.ValidateItemIdentifiersAsync(
                        GetTenantId(),
                        existingItem.Id,
                        existingItem.Barcode,
                        existingItem.AlternateBarcode,
                        existingItem.QRCode,
                        HttpContext.RequestAborted);
                }
            }

            await _inventoryItemRepository.UpdateAsync(existingItem);
            var auditQueued = await QueueAuditAsync("InventoryItem.Updated", existingItem, previousProfile);
            await _inventoryItemRepository.SaveChangesAsync();
            if (!auditQueued) await AuditFallbackAsync("InventoryItem.Updated", existingItem, previousProfile);

            var itemDto = _mapper.Map<InventoryItemDto>(existingItem);
            return Ok(itemDto);
        }
        catch (InventoryIdentifierConflictException ex)
        {
            return Conflict(new ProblemDetails { Status = 409, Title = "Duplicate inventory identifier", Detail = ex.Message, Extensions = { ["code"] = "INVENTORY_IDENTIFIER_DUPLICATE", ["identifier"] = ex.Identifier } });
        }
        catch (InventoryItemProfileValidationException ex)
        {
            return UnprocessableEntity(ProfileProblem(ex));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Inventory item changed",
                Detail = "The item profile was changed by another user. Reload it before saving.",
                Extensions = { ["code"] = "ITEM_PROFILE_CONCURRENCY_CONFLICT" }
            });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating inventory item {ItemId}", id);
            return StatusCode(500, "An error occurred while updating the inventory item");
        }
    }

    /// <summary>
    /// Atomically imports validated item-master profiles. The same profile boundary
    /// used by direct create/edit and maker-checker application is enforced here.
    /// </summary>
    [HttpPost("import")]
    public async Task<ActionResult<InventoryItemImportResultDto>> ImportInventoryItems(
        [FromBody] ImportInventoryItemsDto request)
    {
        if (!await HasInventoryCapabilityAsync(
                ["procurement.inventory.master-data.manage"], "inventory-item-import"))
            return Forbid();
        var protection = await GuardDirectMutationAsync(null, "InventoryItem.Import");
        if (protection is not null) return protection;
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (_unitOfWork is null || _profileService is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "The item-profile import boundary is unavailable.");

        var tenantId = GetTenantId();
        var normalizedCodes = request.Items.Select(value => value.ItemCode?.Trim().ToUpperInvariant()).ToArray();
        if (normalizedCodes.Any(string.IsNullOrWhiteSpace) ||
            normalizedCodes.GroupBy(value => value, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            return UnprocessableEntity(ProfileProblem(new InventoryItemProfileValidationException(
                "ITEM_IMPORT_DUPLICATE_CODE",
                "Every import row requires a unique stock code.")));
        }

        var identifiers = request.Items
            .SelectMany(value => new[] { value.Barcode, value.AlternateBarcode, value.QRCode })
            .Select(value => _identifierService?.Normalize(value) ?? NormalizeIdentifier(value))
            .Where(value => value is not null)
            .Cast<string>()
            .ToArray();
        if (identifiers.GroupBy(value => value, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            return UnprocessableEntity(ProfileProblem(new InventoryItemProfileValidationException(
                "ITEM_IMPORT_DUPLICATE_IDENTIFIER",
                "An item identifier may occur only once in an import payload.")));
        }

        var imported = new List<InventoryItem>();
        try
        {
            await _unitOfWork.ExecuteInStrategyAsync(async () =>
            {
                await _unitOfWork.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, HttpContext.RequestAborted);
                try
                {
                    await _unitOfWork.AcquireTransactionLockAsync($"inventory:item-profile-import:{tenantId:N}", HttpContext.RequestAborted);
                    foreach (var row in request.Items)
                    {
                        var item = _mapper.Map<InventoryItem>(row);
                        item.TenantId = tenantId;
                        await _profileService.NormalizeAndValidateAsync(item, null, HttpContext.RequestAborted);
                        await _inventoryItemRepository.AddAsync(item);
                        await QueueAuditAsync("InventoryItem.Imported", item, null);
                        imported.Add(item);
                    }

                    await _unitOfWork.SaveChangesAsync(HttpContext.RequestAborted);
                    await _unitOfWork.CommitAsync(HttpContext.RequestAborted);
                }
                catch
                {
                    await _unitOfWork.RollbackAsync(HttpContext.RequestAborted);
                    _unitOfWork.ClearTrackedChanges();
                    imported.Clear();
                    throw;
                }
            }, HttpContext.RequestAborted);

            return Ok(new InventoryItemImportResultDto
            {
                ImportedCount = imported.Count,
                Items = imported.Select(item => _mapper.Map<InventoryItemDto>(item)).ToArray()
            });
        }
        catch (InventoryIdentifierConflictException ex)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Duplicate inventory identifier",
                Detail = ex.Message,
                Extensions = { ["code"] = "INVENTORY_IDENTIFIER_DUPLICATE", ["identifier"] = ex.Identifier }
            });
        }
        catch (InventoryItemProfileValidationException ex)
        {
            return UnprocessableEntity(ProfileProblem(ex));
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Inventory item import conflicted for tenant {TenantId}", tenantId);
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Inventory item import conflict",
                Detail = "The import conflicts with current item-master data. Reload the register and retry.",
                Extensions = { ["code"] = "ITEM_IMPORT_CONFLICT" }
            });
        }
    }

    /// <summary>
    /// Returns the immutable ordinary/staged change history for one tenant item.
    /// </summary>
    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult<IReadOnlyList<InventoryItemChangeAuditDto>>> GetInventoryItemHistory(Guid id)
    {
        if (!await HasInventoryCapabilityAsync(
                ["procurement.inventory.read", "procurement.inventory.master-data.manage"],
                $"inventory-item-history:{id:N}"))
            return Forbid();
        if (_unitOfWork is null) return StatusCode(StatusCodes.Status503ServiceUnavailable);
        var tenantId = GetTenantId();
        var itemExists = await _unitOfWork.Repository<InventoryItem>()
            .GetQueryable(value => value.TenantId == tenantId && value.Id == id && !value.IsDeleted)
            .AnyAsync(HttpContext.RequestAborted);
        if (!itemExists) return NotFound();

        var entries = await _unitOfWork.Repository<AuditLog>()
            .GetQueryable(value => value.TenantId == tenantId && value.ResourceId == id.ToString() &&
                (value.Resource == "InventoryItem" || value.Resource == "InventoryItemIdentifier"))
            .OrderByDescending(value => value.Timestamp)
            .ThenByDescending(value => value.Id)
            .Select(value => new InventoryItemChangeAuditDto
            {
                Id = value.Id,
                OccurredAtUtc = value.Timestamp,
                UserId = value.UserId,
                Username = value.Username,
                Action = value.Action,
                OldValues = value.OldValues,
                NewValues = value.NewValues
            })
            .ToListAsync(HttpContext.RequestAborted);
        return Ok(entries);
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

            var tenantId = GetTenantId();

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
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
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
        [FromQuery] bool dryRun = true,
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] Guid? inventoryItemId = null,
        [FromQuery, MaxLength(500)] string? reason = null)
    {
        try
        {
            var tenantId = GetTenantId();
            var sourceReference = warehouseId.HasValue
                ? $"inventory-stock-reconciliation:{warehouseId.Value:N}"
                : "inventory-stock-reconciliation:all";
            var manageDecision = await _access.CheckCapabilityAsync(new ProcurementAccessCapabilityRequest
            {
                PermissionCode = "procurement.inventory.master-data.manage",
                SourceType = "InventoryStockReconciliation",
                SourceReference = sourceReference
            }, HttpContext.TraceIdentifier, HttpContext.RequestAborted);
            if (!manageDecision.Allowed)
            {
                return Forbid();
            }

            if (!dryRun)
            {
                if (!warehouseId.HasValue || warehouseId.Value == Guid.Empty)
                {
                    return BadRequest(ApiResponse<object>.ErrorResponse(
                        "An actual reconciliation must be restricted to one warehouse."));
                }
                if (string.IsNullOrWhiteSpace(reason))
                {
                    return BadRequest(ApiResponse<object>.ErrorResponse(
                        "A reconciliation reason is required before stock balances can be changed."));
                }
                if (_unitOfWork is null)
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable,
                        ApiResponse<object>.ErrorResponse("The controlled reconciliation boundary is unavailable."));
                }

                var adjustmentDecision = await _access.CheckCapabilityAsync(new ProcurementAccessCapabilityRequest
                {
                    PermissionCode = "procurement.inventory.adjust.approve",
                    WarehouseId = warehouseId.Value,
                    SourceType = "InventoryStockReconciliation",
                    SourceReference = sourceReference
                }, HttpContext.TraceIdentifier, HttpContext.RequestAborted);
                if (!adjustmentDecision.Allowed)
                {
                    return Forbid();
                }
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
                await _unitOfWork!.Repository<AuditLog>().AddAsync(new AuditLog
                {
                    TenantId = tenantId,
                    UserId = _currentUserProvider.UserId,
                    Username = string.IsNullOrWhiteSpace(_currentUserProvider.Username)
                        ? "Unknown"
                        : _currentUserProvider.Username,
                    Action = "InventoryStock.Reconciled",
                    Resource = "InventoryStockReconciliation",
                    ResourceId = warehouseId!.Value.ToString(),
                    NewValues = JsonSerializer.Serialize(new
                    {
                        WarehouseId = warehouseId,
                        InventoryItemId = inventoryItemId,
                        Reason = reason!.Trim(),
                        MovementGroups = movementTotals.Count,
                        WarehouseQuantitiesCreated = createdWarehouseQuantities,
                        WarehouseQuantitiesUpdated = updatedWarehouseQuantities,
                        InventoryItemsUpdated = updatedInventoryItems,
                        CorrelationId = HttpContext.TraceIdentifier
                    }),
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                    UserAgent = Request.Headers.UserAgent.ToString(),
                    Timestamp = DateTime.UtcNow
                });
                // One SaveChanges is enough - all repositories share the same DbContext in this request scope.
                await _inventoryItemRepository.SaveChangesAsync();
            }

            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                TenantId = tenantId,
                DryRun = dryRun,
                Reason = dryRun ? null : reason!.Trim(),
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
                // This is stock held by the selected warehouse, not today's
                // standard/purchase price. Posting still derives exact carrying
                // value from the governed bin valuation ledger.
                UnitCost = wq.AverageCost,
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
    /// Helper method to get the explicit tenant ID from the authenticated token.
    /// </summary>
    private async Task<ObjectResult?> GuardDirectMutationAsync(Guid? id, string action)
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

    private async Task<bool> HasInventoryCapabilityAsync(
        IReadOnlyCollection<string> permissions,
        string sourceReference)
    {
        Guid? denialWarehouseId = null;
        foreach (var permission in permissions)
        {
            var warehouseIds = permission == "procurement.inventory.read" && _unitOfWork is not null
                ? await _unitOfWork.Repository<Warehouse>().GetQueryable(value =>
                        value.TenantId == GetTenantId() && value.IsActive && !value.IsDeleted)
                    .AsNoTracking().Select(value => (Guid?)value.Id).ToListAsync(HttpContext.RequestAborted)
                : new List<Guid?> { null };
            if (permission == "procurement.inventory.read")
                denialWarehouseId = warehouseIds.FirstOrDefault();
            foreach (var warehouseId in warehouseIds)
            {
                var decision = await _access.CheckCapabilityAsync(new ProcurementAccessCapabilityRequest
                {
                    PermissionCode = permission,
                    WarehouseId = warehouseId,
                    SourceType = "InventoryItem",
                    SourceReference = sourceReference
                }, HttpContext.TraceIdentifier, HttpContext.RequestAborted);
                if (decision.Allowed) return true;
            }
        }

        try
        {
            var denial = await _access.EnforceCapabilityAsync(new ProcurementAccessCapabilityRequest
            {
                PermissionCode = permissions.First(),
                WarehouseId = permissions.First() == "procurement.inventory.read" ? denialWarehouseId : null,
                SourceType = "InventoryItem",
                SourceReference = sourceReference
            }, HttpContext.TraceIdentifier, HttpContext.RequestAborted);
            return denial.Allowed;
        }
        catch (Exception exception) when (exception is ProcurementAccessAuthorizationException or ProcurementAccessValidationException)
        {
            return false;
        }
    }

    private Guid GetTenantId()
    {
        var tenantClaim = User.FindFirst("tenant_id")?.Value;
        if (!Guid.TryParse(tenantClaim, out var tenantId) || tenantId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("A tenant-scoped token is required for inventory item access.");
        }

        return tenantId;
    }

    private void NormalizeIdentifiers(InventoryItem item)
    {
        item.Barcode = _identifierService?.Normalize(item.Barcode) ?? NormalizeIdentifier(item.Barcode);
        item.AlternateBarcode = _identifierService?.Normalize(item.AlternateBarcode) ?? NormalizeIdentifier(item.AlternateBarcode);
        item.QRCode = _identifierService?.Normalize(item.QRCode) ?? NormalizeIdentifier(item.QRCode);
    }

    private static string? NormalizeIdentifier(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized.ToUpperInvariant();
    }

    private static bool MatchesRowVersion(string value, byte[] current)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(value) &&
                Convert.FromBase64String(value).AsSpan().SequenceEqual(current);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private ProblemDetails ProfileProblem(InventoryItemProfileValidationException exception) => new()
    {
        Status = StatusCodes.Status422UnprocessableEntity,
        Title = "Inventory item profile is invalid",
        Detail = exception.Message,
        Instance = HttpContext.Request.Path,
        Extensions =
        {
            ["code"] = exception.Code,
            ["correlationId"] = HttpContext.TraceIdentifier
        }
    };

    private static object SnapshotProfile(InventoryItem item) => new
    {
        item.ItemCode,
        item.Name,
        item.Description,
        item.CategoryId,
        item.UnitOfMeasure,
        item.UnitOfMeasureScheduleId,
        item.IsProjectApplicable,
        item.IsCostCentreApplicable,
        item.ValuationMethod,
        item.IsValuationLocked,
        item.StandardCost,
        item.MinimumLevel,
        item.MaximumLevel,
        item.ReorderLevel,
        item.ReorderQuantity,
        item.SafetyStock,
        item.LeadTimeDays,
        item.SafetyLeadTimeDays,
        item.IsSerialTracked,
        item.IsLotTracked,
        item.IsBatchTracked,
        item.IsManufactureDateTracked,
        item.IsExpirationTracked,
        item.ShelfLifeDays,
        item.Status,
        item.Barcode,
        item.AlternateBarcode,
        item.QRCode
    };

    private async Task<bool> QueueAuditAsync(string action, InventoryItem item, object? previous)
    {
        if (_unitOfWork is null) return false;
        await _unitOfWork.Repository<AuditLog>().AddAsync(new AuditLog
        {
            TenantId = item.TenantId,
            UserId = _currentUserProvider.UserId,
            Username = string.IsNullOrWhiteSpace(_currentUserProvider.Username) ? "Unknown" : _currentUserProvider.Username,
            Action = action,
            Resource = "InventoryItem",
            ResourceId = item.Id.ToString(),
            OldValues = previous is null ? null : JsonSerializer.Serialize(previous),
            NewValues = JsonSerializer.Serialize(SnapshotProfile(item)),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
            UserAgent = Request.Headers.UserAgent.ToString(),
            Timestamp = DateTime.UtcNow
        });
        return true;
    }

    private Task AuditFallbackAsync(string action, InventoryItem item, object? previous) =>
        _auditLog?.LogUserActionAsync(
            _currentUserProvider.UserId,
            _currentUserProvider.Username,
            action,
            "InventoryItem",
            item.Id.ToString(),
            previous,
            SnapshotProfile(item))
        ?? Task.CompletedTask;
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
