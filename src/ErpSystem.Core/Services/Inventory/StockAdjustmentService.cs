using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.DocumentManagement;
using Microsoft.EntityFrameworkCore;
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly IInventoryTrackingControlService _trackingControls;
    private readonly IInventoryNegativeStockControlService _negativeStockControls;
    private readonly IProcurementAccessControlService _accessControl;
    private readonly IProcurementSodGuardService _sodGuard;
    private readonly IWorkflowIntegrationService _workflow;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly IInventoryAdjustmentFinancePostingService _financePosting;
    private readonly IInventoryValuationService _valuation;
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
        IUnitOfWork unitOfWork,
        IInventoryTrackingControlService trackingControls,
        IInventoryNegativeStockControlService negativeStockControls,
        IProcurementAccessControlService accessControl,
        IProcurementSodGuardService sodGuard,
        IWorkflowIntegrationService workflow,
        IProcurementControlEventService controlEvents,
        IInventoryAdjustmentFinancePostingService financePosting,
        IInventoryValuationService valuation,
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
        _unitOfWork = unitOfWork;
        _trackingControls = trackingControls;
        _negativeStockControls = negativeStockControls;
        _accessControl = accessControl;
        _sodGuard = sodGuard;
        _workflow = workflow;
        _controlEvents = controlEvents;
        _financePosting = financePosting;
        _valuation = valuation;
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

            var readable = new List<StockAdjustmentDto>();
            foreach (var adjustment in adjustments.OrderByDescending(a => a.AdjustmentDate))
            {
                var full = adjustment.Items.Count > 0 ? adjustment : await LoadAsync(adjustment.Id);
                if (full != null && await CanReadAsync(full)) readable.Add(MapToDto(full));
            }
            return readable;
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
            var adjustment = await LoadAsync(id);
            if (adjustment == null)
            {
                return null;
            }

            return await CanReadAsync(adjustment) ? MapToDetailDto(adjustment) : null;
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
            var adjustment = await Adjustments.FirstOrDefaultAsync(x => x.AdjustmentNumber == adjustmentNumber);
            if (adjustment == null)
            {
                return null;
            }

            return await CanReadAsync(adjustment) ? MapToDetailDto(adjustment) : null;
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
            var readable = new List<StockAdjustmentDto>();
            foreach (var adjustment in adjustments)
            {
                var full = adjustment.Items.Count > 0 ? adjustment : await LoadAsync(adjustment.Id);
                if (full != null && await CanReadAsync(full)) readable.Add(MapToDto(full));
            }
            return readable;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending stock adjustments");
            throw;
        }
    }

    public async Task<OpeningStockOptionsDto> GetOpeningStockOptionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // Inventory owns these canonical masters. This read model filters existing evidence only;
        // it never creates, repairs or seeds warehouses, locations or items for Finance.
        EnsureActor(userId);
        var tenantId = _currentUserProvider.TenantId;
        var warehouses = await _unitOfWork.Repository<Warehouse>().GetQueryable(x =>
                x.TenantId == tenantId && !x.IsDeleted && x.IsActive && !x.IsConsignmentWarehouse)
            .AsNoTracking()
            .OrderBy(x => x.Code).ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
        var warehouseIds = warehouses.Select(x => x.Id).ToHashSet();
        var locations = await _unitOfWork.Repository<WarehouseLocation>().GetQueryable(x =>
                x.TenantId == tenantId && !x.IsDeleted && x.IsActive && !x.IsConsignmentBin &&
                warehouseIds.Contains(x.WarehouseId))
            .AsNoTracking()
            .OrderBy(x => x.LocationCode).ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var accessibleLocations = new List<WarehouseLocation>();
        foreach (var location in locations)
        {
            var decision = await _accessControl.CheckCapabilityAsync(new ProcurementAccessCapabilityRequest
            {
                PermissionCode = "procurement.inventory.adjust.request",
                WarehouseId = location.WarehouseId,
                LocationId = location.Id,
                RequireLocationScope = true,
                SourceType = "OpeningStockOptions",
                SourceReference = location.LocationCode
            }, $"opening-stock-options:{tenantId:N}:{userId:N}:{location.Id:N}", cancellationToken);
            if (decision.Allowed) accessibleLocations.Add(location);
        }

        var accessibleWarehouseIds = accessibleLocations.Select(x => x.WarehouseId).ToHashSet();
        var activeItems = await _unitOfWork.Repository<InventoryItem>().GetQueryable(x =>
                x.TenantId == tenantId && !x.IsDeleted && x.Status == ItemStatus.Active &&
                x.ItemType == ItemType.StockItem)
            .AsNoTracking()
            .OrderBy(x => x.ItemCode).ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
        // Do not expose item choices to an actor with no eligible exact-location scope.
        var items = accessibleLocations.Count == 0 ? new List<InventoryItem>() : activeItems;

        var result = new OpeningStockOptionsDto
        {
            RetrievedAtUtc = DateTime.UtcNow,
            Warehouses = warehouses.Where(x => accessibleWarehouseIds.Contains(x.Id)).Select(warehouse =>
                new OpeningStockWarehouseOptionDto
                {
                    Id = warehouse.Id,
                    Code = warehouse.Code,
                    Name = warehouse.Name,
                    Locations = accessibleLocations.Where(x => x.WarehouseId == warehouse.Id).Select(location =>
                        new OpeningStockLocationOptionDto
                        {
                            Id = location.Id,
                            Code = location.LocationCode,
                            Name = location.Name ?? location.LocationCode
                        }).ToList()
                }).ToList(),
            Items = items.Select(item => new OpeningStockItemOptionDto
            {
                Id = item.Id,
                ItemCode = item.ItemCode,
                Name = item.Name,
                UnitOfMeasure = item.UnitOfMeasure,
                IsSerialTracked = item.IsSerialTracked,
                IsLotTracked = item.IsLotTracked,
                IsBatchTracked = item.IsBatchTracked
            }).ToList()
        };
        if (result.Warehouses.Count == 0)
            result.Blockers.Add("No active, owned warehouse location is available within the current actor's Inventory adjustment scope.");
        if (activeItems.Count == 0)
            result.Blockers.Add("No active, non-deleted stock item is available in the current tenant.");
        result.IsReady = result.Blockers.Count == 0;
        return result;
    }

    #endregion

    #region Create/Update Methods

    /// <summary>
    /// Creates a new stock adjustment
    /// </summary>
    // Preserve the established ordinary adjustment behavior while reserving INITIAL_STOCK for the
    // governed Inventory-owned schedule boundary below.
    public Task<StockAdjustmentDetailDto> CreateAsync(CreateStockAdjustmentDto dto, Guid userId) =>
        CreateCoreAsync(dto, userId, openingBookClassification: null);

    public async Task<StockAdjustmentDetailDto> CreateOpeningStockAsync(CreateOpeningStockAdjustmentDto dto, Guid userId)
    {
        // Inventory is the sole owner of opening warehouse/location/item/quantity/unit-cost evidence.
        // Finance receives only the immutable approved projection later through its posting adapter.
        EnsureActor(userId);
        if (dto.OpeningDate == default)
            throw new InvalidOperationException("An explicit opening-stock date is required.");
        var book = Required(dto.BookClassification, "An explicit accounting book is required.", 20).ToUpperInvariant();
        if (book == "ALL_ACTIVE_BOOKS")
            throw new InvalidOperationException("Opening stock must identify one explicit accounting book.");
        var sourceSchedule = Required(dto.SourceScheduleReference,
            "A source schedule reference is required for opening stock.", 50);
        var description = Required(dto.Description, "A source schedule description is required for opening stock.", 1000);
        if (dto.Items.Count == 0)
            throw new InvalidOperationException("At least one opening-stock line is required.");
        if (dto.Items.Any(x => x.InventoryItemId == Guid.Empty || x.LocationId == Guid.Empty || x.Quantity <= 0 || x.UnitCost <= 0))
            throw new InvalidOperationException("Every opening-stock line requires an item, exact location, positive quantity and positive unit cost.");
        if (dto.Items.GroupBy(x => new
            {
                x.InventoryItemId,
                x.LocationId,
                SerialNumber = Normalize(x.SerialNumber, 100)?.ToUpperInvariant(),
                LotNumber = Normalize(x.LotNumber, 100)?.ToUpperInvariant(),
                BatchNumber = Normalize(x.BatchNumber, 100)?.ToUpperInvariant()
            }).Any(x => x.Count() > 1))
            throw new InvalidOperationException("The same opening-stock item/location/tracking identity cannot be entered twice.");

        var openingDate = EnsureUtc(dto.OpeningDate).Date;
        var idempotencyKey = CreateOpeningStockIdempotencyKey(
            _currentUserProvider.TenantId, dto.WarehouseId, openingDate, book, sourceSchedule);
        var request = new CreateStockAdjustmentDto
        {
            WarehouseId = dto.WarehouseId,
            ReasonCode = StockAdjustmentReasonCodes.InitialStock,
            Description = description,
            Reference = sourceSchedule,
            AdjustmentDate = openingDate,
            IdempotencyKey = idempotencyKey,
            CorrelationId = $"opening-stock:{idempotencyKey[^32..]}",
            Items = dto.Items.Select(x => new CreateStockAdjustmentItemDto
            {
                InventoryItemId = x.InventoryItemId,
                LocationId = x.LocationId,
                AdjustmentQuantity = x.Quantity,
                UnitCost = x.UnitCost,
                SerialNumber = x.SerialNumber,
                LotNumber = x.LotNumber,
                BatchNumber = x.BatchNumber,
                ManufactureDate = x.ManufactureDate,
                ExpiryDate = x.ExpiryDate,
                Reason = x.Reason,
                Notes = x.Notes
            }).ToList()
        };
        var payloadHash = CreateOpeningStockPayloadHash(request, description, sourceSchedule, book);
        return await CreateOpeningStockSerializedAsync(
            request, userId, book, sourceSchedule, payloadHash);
    }

    private async Task<StockAdjustmentDetailDto> CreateOpeningStockSerializedAsync(
        CreateStockAdjustmentDto request,
        Guid userId,
        string book,
        string sourceSchedule,
        string payloadHash)
    {
        async Task<StockAdjustmentDetailDto> CreateUnderSourceLockAsync()
        {
            // Inventory owns this serialized schedule identity and its canonical stock evidence.
            // No Inventory masters are created here, and the ordinary adjustment path remains unchanged.
            await _unitOfWork.AcquireTransactionLockAsync(
                $"inventory:opening-stock:{request.IdempotencyKey}");

            var openingDate = EnsureUtc(request.AdjustmentDate!.Value).Date;
            var normalizedSource = sourceSchedule.ToUpperInvariant();
            var existingSource = await Adjustments.FirstOrDefaultAsync(x =>
                x.ReasonCode == StockAdjustmentReasonCodes.InitialStock &&
                x.WarehouseId == request.WarehouseId &&
                x.AdjustmentDate.Date == openingDate &&
                x.BookClassification.ToUpper() == book &&
                x.Reference.ToUpper() == normalizedSource);
            if (existingSource is not null)
            {
                EnsureMatchingCreationPayload(existingSource, payloadHash);
                return await GetByIdAsync(existingSource.Id)
                    ?? throw new InvalidOperationException(
                        "The existing opening-stock schedule could not be loaded.");
            }

            return await CreateCoreAsync(request, userId, book);
        }

        if (_unitOfWork.HasActiveTransaction)
            return await CreateUnderSourceLockAsync();

        return await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var result = await CreateUnderSourceLockAsync();
                await _unitOfWork.CommitAsync();
                return result;
            }
            catch
            {
                if (_unitOfWork.HasActiveTransaction)
                    await _unitOfWork.RollbackAsync();
                else
                    _unitOfWork.ClearTrackedChanges();
                throw;
            }
        });
    }

    private async Task<StockAdjustmentDetailDto> CreateCoreAsync(
        CreateStockAdjustmentDto dto,
        Guid userId,
        string? openingBookClassification)
    {
        EnsureActor(userId);
        if (dto.Items.Count == 0 || dto.Items.Any(x => x.AdjustmentQuantity == 0))
            throw new InvalidOperationException("At least one non-zero stock adjustment line is required.");
        var key = Required(dto.IdempotencyKey, "An idempotency key is required.", 100);
        var reasonCode = ValidateReasonCode(dto.ReasonCode);
        var isOpeningStock = openingBookClassification is not null;
        if (reasonCode == StockAdjustmentReasonCodes.InitialStock && !isOpeningStock)
            throw new InvalidOperationException("INITIAL_STOCK is governed by the dedicated opening-stock endpoint.");
        if (isOpeningStock && reasonCode != StockAdjustmentReasonCodes.InitialStock)
            throw new InvalidOperationException("The governed opening-stock endpoint can create only INITIAL_STOCK adjustments.");
        var description = Required(dto.Description, "A detailed adjustment reason is required.", 1000);
        var reference = Normalize(dto.Reference, 50) ?? string.Empty;
        var payloadHash = isOpeningStock
            ? CreateOpeningStockPayloadHash(dto, description, reference, openingBookClassification!)
            : CreateAdjustmentPayloadHash(dto, reasonCode, description, reference);
        var existing = await Adjustments.FirstOrDefaultAsync(x => x.IdempotencyKey == key);
        if (existing is not null)
        {
            EnsureMatchingCreationPayload(existing, payloadHash);
            return await GetByIdAsync(existing.Id) ?? throw new InvalidOperationException("The existing stock adjustment could not be loaded.");
        }
        var adjustment = new StockAdjustment
        {
            TenantId = _currentUserProvider.TenantId,
            AdjustmentNumber = await _adjustmentRepository.GenerateAdjustmentNumberAsync(_currentUserProvider.TenantId),
            AdjustmentDate = dto.AdjustmentDate ?? DateTime.UtcNow,
            WarehouseId = dto.WarehouseId,
            BookClassification = openingBookClassification ?? "IFRS",
            ReasonCode = reasonCode,
            Description = description,
            Reference = reference,
            Status = "Draft",
            RequestedById = userId,
            RelatedIssueVoucherId = dto.RelatedIssueVoucherId,
            IdempotencyKey = key,
            CorrelationId = Normalize(dto.CorrelationId, 100) ?? $"stock-adjustment:{Guid.NewGuid():N}"
        };
        await BuildLinesAsync(adjustment, dto.Items);
        if (isOpeningStock) await RevalidateOpeningStockAsync(adjustment);
        adjustment.PayloadHash = payloadHash;
        adjustment.IntegrityHash = AdjustmentIntegrityHash(adjustment);
        await RequireAccessAsync("procurement.inventory.adjust.request", adjustment, adjustment.AdjustmentNumber);
        await AddEvidenceAsync(adjustment, dto.Evidence, EvidenceRequired(reasonCode));
        await _adjustmentRepository.AddAsync(adjustment);
        await _unitOfWork.SaveChangesAsync();
        await AddActionAsync(adjustment, "Created", key, adjustment.Description);
        await AddAuditAsync("Create", adjustment, null, Snapshot(adjustment));
        await _unitOfWork.SaveChangesAsync();
        return await GetByIdAsync(adjustment.Id) ?? throw new InvalidOperationException("Failed to retrieve created adjustment.");
    }

    /// <summary>
    /// Updates an existing stock adjustment (only if in Draft status)
    /// </summary>
    public async Task<StockAdjustmentDetailDto> UpdateAsync(Guid id, UpdateStockAdjustmentDto dto, Guid userId)
    {
        EnsureActor(userId);
        var adjustment = await LoadAsync(id) ?? throw new ArgumentException($"Stock adjustment {id} not found");
        if (adjustment.Status != "Draft") throw new InvalidOperationException($"Cannot update adjustment with status {adjustment.Status}");
        if (adjustment.ReasonCode == StockAdjustmentReasonCodes.InitialStock)
            throw new InvalidOperationException(
                "Opening-stock schedule identity and lines are immutable. Cancel this draft and use a revised source schedule reference.");
        var requestedReasonCode = ValidateReasonCode(dto.ReasonCode);
        // Inventory owns INITIAL_STOCK through the dedicated immutable schedule boundary. Reject
        // conversion before access enforcement, tracked mutation, audit or persistence side effects.
        if (requestedReasonCode == StockAdjustmentReasonCodes.InitialStock)
            throw new InvalidOperationException("INITIAL_STOCK is governed by the dedicated opening-stock endpoint.");
        EnsureRowVersion(adjustment.RowVersion, dto.RowVersion);
        await RequireAccessAsync("procurement.inventory.adjust.request", adjustment, adjustment.AdjustmentNumber);
        var before = Snapshot(adjustment);
        adjustment.ReasonCode = requestedReasonCode;
        adjustment.Description = Required(dto.Description, "A detailed adjustment reason is required.", 1000);
        adjustment.Reference = Normalize(dto.Reference, 50) ?? string.Empty;
        if (dto.AdjustmentDate.HasValue) adjustment.AdjustmentDate = dto.AdjustmentDate.Value;
        if (dto.Items is { Count: > 0 })
        {
            if (dto.Items.Any(x => x.AdjustmentQuantity == 0)) throw new InvalidOperationException("Adjustment quantities cannot be zero.");
            adjustment.Items.Clear();
            adjustment.TotalAdjustmentValue = 0;
            await BuildLinesAsync(adjustment, dto.Items);
        }
        if (dto.Evidence is not null)
        {
            var requestedVersionIds = dto.Evidence.Select(x => x.CentralDocumentVersionId).ToHashSet();
            if (adjustment.Evidence.Any(x => !requestedVersionIds.Contains(x.CentralDocumentVersionId)))
                throw new InvalidOperationException("Linked central-DMS evidence is append-only; create a new adjustment instead of removing evidence.");
            var additions = dto.Evidence.Where(x => adjustment.Evidence.All(existing => existing.CentralDocumentVersionId != x.CentralDocumentVersionId)).ToList();
            await AddEvidenceAsync(adjustment, additions, EvidenceRequired(adjustment.ReasonCode) && adjustment.Evidence.Count == 0);
        }
        else if (EvidenceRequired(adjustment.ReasonCode) && adjustment.Evidence.Count == 0)
        {
            throw new InvalidOperationException("Current published central-DMS evidence is required for this adjustment reason.");
        }
        adjustment.PayloadHash = AdjustmentPayloadHash(adjustment);
        adjustment.IntegrityHash = AdjustmentIntegrityHash(adjustment);
        await _adjustmentRepository.UpdateAsync(adjustment);
        await AddActionAsync(adjustment, "Updated", $"update:{Guid.NewGuid():N}", adjustment.Description);
        await AddAuditAsync("Update", adjustment, before, Snapshot(adjustment));
        await _unitOfWork.SaveChangesAsync();
        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve updated adjustment.");
    }

    /// <summary>
    /// Deletes a stock adjustment (only if in Draft status)
    /// </summary>
    public async Task<bool> DeleteAsync(Guid id, Guid userId)
    {
        try
        {
            EnsureActor(userId);
            var adjustment = await LoadAsync(id)
                ?? throw new ArgumentException($"Stock adjustment {id} not found");

            if (adjustment.Status != "Draft")
            {
                throw new InvalidOperationException($"Cannot delete adjustment with status {adjustment.Status}");
            }

            if (adjustment.ReasonCode == StockAdjustmentReasonCodes.InitialStock)
                throw new InvalidOperationException(
                    "Opening-stock schedules are retained evidence and cannot be deleted. Cancel the draft instead.");

            await RequireAccessAsync("procurement.inventory.adjust.request", adjustment, adjustment.AdjustmentNumber);
            await AddAuditAsync("Delete", adjustment, Snapshot(adjustment), new { adjustment.Id, adjustment.AdjustmentNumber, Status = "Deleted" });
            await _unitOfWork.SaveChangesAsync();
            await _adjustmentRepository.DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();
            
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
            EnsureActor(userId);
            var adjustment = await LoadAsync(adjustmentId)
                ?? throw new ArgumentException($"Stock adjustment {adjustmentId} not found");

            if (adjustment.Status != "Draft")
            {
                throw new InvalidOperationException($"Cannot delete items from adjustment with status {adjustment.Status}");
            }

            if (adjustment.ReasonCode == StockAdjustmentReasonCodes.InitialStock)
                throw new InvalidOperationException(
                    "Opening-stock schedule lines are immutable. Cancel this draft and use a revised source schedule reference.");

            var item = adjustment.Items.FirstOrDefault(i => i.Id == itemId);
            if (item == null)
            {
                throw new ArgumentException($"Item {itemId} not found in adjustment {adjustmentId}");
            }

            await RequireAccessAsync("procurement.inventory.adjust.request", adjustment, adjustment.AdjustmentNumber);

            // Remove the item
            adjustment.Items.Remove(item);
            
            // Recalculate total value
            adjustment.TotalAdjustmentValue = adjustment.Items.Sum(i => i.AdjustmentValue);
            adjustment.PayloadHash = AdjustmentPayloadHash(adjustment);
            adjustment.IntegrityHash = AdjustmentIntegrityHash(adjustment);

            await _adjustmentRepository.UpdateAsync(adjustment);
            await AddActionAsync(adjustment, "LineDeleted", $"delete-line:{Guid.NewGuid():N}", $"Removed adjustment line {itemId:N}.");
            await AddAuditAsync("DeleteLine", adjustment, new { adjustment.Id, ItemId = itemId }, Snapshot(adjustment));
            await _unitOfWork.SaveChangesAsync();
            
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

    public async Task<StockAdjustmentDetailDto> SubmitAsync(Guid id, Guid userId, StockAdjustmentActionRequest request)
    {
        EnsureActor(userId);
        var key = Required(request.IdempotencyKey, "An idempotency key is required.", 100);
        return await ExecuteControlledMutationAsync(id, async adjustment =>
        {
            if (adjustment.Status != "Draft")
            {
                if (await HasActionAsync(adjustment.Id, "Submitted", key)) return adjustment;
                throw new InvalidOperationException($"Cannot submit adjustment with status {adjustment.Status}.");
            }
            EnsureRowVersion(adjustment.RowVersion, request.RowVersion);
            if (adjustment.Items.Count == 0) throw new InvalidOperationException("The adjustment has no lines.");
            await RequireAccessAsync("procurement.inventory.adjust.request", adjustment, adjustment.AdjustmentNumber);
            await RevalidateEvidenceAsync(adjustment);
            await RevalidateOpeningStockAsync(adjustment);
            var before = Snapshot(adjustment);
            var workflow = await _workflow.SubmitAsync("StockAdjustment", adjustment.Id);
            if (!workflow.ExecutionResult.Success || workflow.Outcome != WorkflowOutcome.Pending || !workflow.ExecutionResult.WorkflowInstanceId.HasValue)
                throw new InvalidOperationException("The stock-adjustment workflow must start with an independent pending approval step.");
            adjustment.Status = "PendingApproval";
            adjustment.SubmittedById = userId;
            adjustment.SubmittedAtUtc = DateTime.UtcNow;
            adjustment.WorkflowInstanceId = workflow.ExecutionResult.WorkflowInstanceId;
            adjustment.IntegrityHash = AdjustmentIntegrityHash(adjustment);
            await PersistLifecycleStateBeforeActionAsync(adjustment);
            await AddActionAsync(adjustment, "Submitted", key, request.Comment);
            await AddAuditAsync("Submit", adjustment, before, Snapshot(adjustment));
            await RecordEventAsync(adjustment, "Submit", ProcurementControlEventResult.Allowed, before, Snapshot(adjustment));
            return adjustment;
        });
    }

    public async Task<StockAdjustmentDetailDto> DecideAsync(Guid id, Guid userId, DecideStockAdjustmentRequest request)
    {
        EnsureActor(userId);
        var key = Required(request.IdempotencyKey, "An idempotency key is required.", 100);
        return await ExecuteControlledMutationAsync(id, async adjustment =>
        {
            var action = request.Approved ? "Approved" : "Rejected";
            if (adjustment.Status != "PendingApproval")
            {
                if (await HasActionAsync(adjustment.Id, action, key)) return adjustment;
                throw new InvalidOperationException($"Cannot decide adjustment with status {adjustment.Status}.");
            }
            EnsureRowVersion(adjustment.RowVersion, request.RowVersion);
            if (adjustment.RequestedById == userId) throw new InvalidOperationException("The adjustment requester cannot approve the same adjustment.");
            await RequireAccessAsync("procurement.inventory.adjust.approve", adjustment, adjustment.AdjustmentNumber);
            var prohibited = new List<Guid> { adjustment.RequestedById };
            if (adjustment.RelatedIssueVoucherId.HasValue)
            {
                var issuer = await _unitOfWork.Repository<InventoryIssueVoucher>().GetQueryable().AsNoTracking()
                    .Where(x => x.Id == adjustment.RelatedIssueVoucherId && x.TenantId == adjustment.TenantId)
                    .Select(x => (Guid?)x.IssuedById).SingleOrDefaultAsync();
                if (issuer.HasValue) prohibited.Add(issuer.Value);
            }
            await _sodGuard.EnforceAsync(new ProcurementSodGuardRequest
            {
                ControlCode = "SOD-STOCK-ISSUER-ADJUSTMENT",
                SourceType = "StockAdjustment",
                SourceReference = adjustment.AdjustmentNumber,
                ProhibitedActorUserIds = prohibited.Distinct().ToList()
            }, adjustment.CorrelationId ?? Guid.NewGuid().ToString("N"));
            if (!await _workflow.CanUserApproveAsync("StockAdjustment", adjustment.Id, userId))
                throw new InvalidOperationException("The current actor is not eligible for the active adjustment workflow step.");
            await RevalidateEvidenceAsync(adjustment);
            if (request.Approved) await RevalidateOpeningStockAsync(adjustment);
            var before = Snapshot(adjustment);
            var workflow = await _workflow.ProcessApprovalAsync("StockAdjustment", adjustment.Id, userId,
                request.Approved ? "Approve" : "Reject", request.Comment);
            if (!workflow.ExecutionResult.Success) throw new InvalidOperationException(workflow.ExecutionResult.Message ?? "The workflow decision failed.");
            if (workflow.Outcome == WorkflowOutcome.Pending) return adjustment;
            if (request.Approved && workflow.Outcome != WorkflowOutcome.Approved) throw new InvalidOperationException("The shared workflow rejected the adjustment.");
            if (!request.Approved && workflow.Outcome == WorkflowOutcome.Approved) throw new InvalidOperationException("An approved workflow cannot be recorded as rejected.");
            adjustment.Status = request.Approved ? "Approved" : "Rejected";
            if (request.Approved)
            {
                adjustment.ApprovedById = userId;
                adjustment.ApprovedAt = DateTime.UtcNow;
            }
            else
            {
                adjustment.RejectedById = userId;
                adjustment.RejectedAtUtc = DateTime.UtcNow;
                adjustment.RejectionReason = Required(request.Comment, "A rejection reason is required.", 1000);
            }
            adjustment.IntegrityHash = AdjustmentIntegrityHash(adjustment);
            await PersistLifecycleStateBeforeActionAsync(adjustment);
            await AddActionAsync(adjustment, action, key, request.Comment);
            await AddAuditAsync(request.Approved ? "Approve" : "Reject", adjustment, before, Snapshot(adjustment));
            await RecordEventAsync(adjustment, request.Approved ? "Approve" : "Reject",
                request.Approved ? ProcurementControlEventResult.Allowed : ProcurementControlEventResult.Rejected, before, Snapshot(adjustment));
            return adjustment;
        });
    }

    /// <summary>
    /// Posts a stock adjustment (applies the adjustment to inventory and creates stock movements)
    /// </summary>
    private async Task<StockAdjustmentDetailDto> LegacyPostAsync(Guid id, Guid userId)
    {
        try
        {
            var adjustment = await _adjustmentRepository.GetWithItemsAsync(id)
                ?? throw new ArgumentException($"Stock adjustment {id} not found");

            if (adjustment.Status != "Approved")
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
                    : adjustment.WarehouseId;

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
                    WarehouseId = effectiveWarehouseId,
                    LocationId = item.LocationId,
                    Notes = $"{adjustment.ReasonCode}: {item.Notes ?? adjustment.Description}",
                    SerialNumber = item.SerialNumber,
                    LotNumber = item.LotNumber,
                    BatchNumber = item.BatchNumber,
                    ManufactureDate = item.ManufactureDate,
                    ExpirationDate = item.ExpiryDate,
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

    public async Task<StockAdjustmentDetailDto> PostAsync(Guid id, Guid userId, StockAdjustmentActionRequest request)
    {
        EnsureActor(userId);
        return await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                await _unitOfWork.AcquireTransactionLockAsync($"stock-adjustment:{_currentUserProvider.TenantId:N}:{id:N}");
                var adjustment = await LoadAsync(id) ?? throw new ArgumentException($"Stock adjustment {id} not found");
                var key = Required(request.IdempotencyKey, "An idempotency key is required.", 100);
                if (adjustment.Status != "Approved")
                {
                    if (await HasActionAsync(adjustment.Id, "Posted", key))
                    {
                        await _unitOfWork.CommitAsync();
                        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Adjustment not found.");
                    }
                    throw new InvalidOperationException($"Only an independently approved adjustment can post; current status is {adjustment.Status}.");
                }
                EnsureRowVersion(adjustment.RowVersion, request.RowVersion);
                if (adjustment.RequestedById == userId) throw new InvalidOperationException("The adjustment requester cannot post the same adjustment.");
                await RequireAccessAsync("procurement.inventory.adjust.approve", adjustment, adjustment.AdjustmentNumber);
                await RevalidateEvidenceAsync(adjustment);
                await RevalidateOpeningStockAsync(adjustment);
                var before = Snapshot(adjustment);
                var finance = await _financePosting.PostAsync(adjustment);
                adjustment.Status = "Posted";
                adjustment.PostedById = userId;
                adjustment.PostedAtUtc = DateTime.UtcNow;
                adjustment.FinancePostingEventId = finance.PostingEventId;
                adjustment.FinanceJournalEntryId = finance.JournalEntryId;
                adjustment.IntegrityHash = AdjustmentIntegrityHash(adjustment);
                await PersistLifecycleStateBeforeActionAsync(adjustment);
                await AddActionAsync(adjustment, "Posted", key, request.Comment);
                await AddAuditAsync("Post", adjustment, before, Snapshot(adjustment));
                await RecordEventAsync(adjustment, "Post", ProcurementControlEventResult.Allowed, before, Snapshot(adjustment));
                await _adjustmentRepository.UpdateAsync(adjustment);
                // Persist the governed parent state inside the same uncommitted
                // transaction before movement inserts so SQL lineage guards can
                // reject any movement without a posted parent and Finance IDs.
                await _unitOfWork.SaveChangesAsync();
                foreach (var item in adjustment.Items.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id))
                    await ApplyAdjustmentLineAsync(adjustment, item, reverse: false, userId, request.NegativeStockOverrideIds);
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();
                return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve posted adjustment.");
            }
            catch (DbUpdateConcurrencyException exception)
            {
                // Inventory owns the stock mutation graph. Keep concurrency diagnostics limited to
                // entity types so a failed controlled post can be investigated without disclosing
                // inventory values, user data, or persistence tokens in application logs.
                _logger.LogError(exception,
                    "Stock adjustment {AdjustmentId} hit optimistic concurrency while saving {EntityTypes}",
                    id,
                    string.Join(", ", exception.Entries
                        .Select(entry => entry.Metadata.ClrType.Name)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(name => name, StringComparer.Ordinal)));
                if (_unitOfWork.HasActiveTransaction) await _unitOfWork.RollbackAsync();
                _unitOfWork.ClearTrackedChanges();
                throw;
            }
            catch
            {
                if (_unitOfWork.HasActiveTransaction) await _unitOfWork.RollbackAsync();
                _unitOfWork.ClearTrackedChanges();
                throw;
            }
        });
    }

    public async Task<StockAdjustmentDetailDto> ReverseAsync(Guid id, Guid userId, ReverseStockAdjustmentRequest request)
    {
        EnsureActor(userId);
        return await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                await _unitOfWork.AcquireTransactionLockAsync($"stock-adjustment:{_currentUserProvider.TenantId:N}:{id:N}");
                var adjustment = await LoadAsync(id) ?? throw new ArgumentException($"Stock adjustment {id} not found");
                var key = Required(request.IdempotencyKey, "An idempotency key is required.", 100);
                if (adjustment.Status != "Posted")
                {
                    if (await HasActionAsync(adjustment.Id, "Reversed", key))
                    {
                        await _unitOfWork.CommitAsync();
                        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Adjustment not found.");
                    }
                    throw new InvalidOperationException($"Only a posted adjustment can be reversed; current status is {adjustment.Status}.");
                }
                EnsureRowVersion(adjustment.RowVersion, request.RowVersion);
                var reason = Required(request.Reason, "A reversal reason is required.", 1000);
                if (adjustment.RequestedById == userId) throw new InvalidOperationException("The adjustment requester cannot reverse the same adjustment.");
                await RequireAccessAsync("procurement.inventory.adjust.approve", adjustment, adjustment.AdjustmentNumber);
                var before = Snapshot(adjustment);
                var finance = await _financePosting.ReverseAsync(adjustment, reason);
                adjustment.Status = "Reversed";
                adjustment.ReversedById = userId;
                adjustment.ReversedAtUtc = DateTime.UtcNow;
                adjustment.ReversalReason = reason;
                adjustment.ReversalFinancePostingEventId = finance.PostingEventId;
                adjustment.ReversalFinanceJournalEntryId = finance.JournalEntryId;
                adjustment.IntegrityHash = AdjustmentIntegrityHash(adjustment);
                await PersistLifecycleStateBeforeActionAsync(adjustment);
                await AddActionAsync(adjustment, "Reversed", key, reason);
                await AddAuditAsync("Reverse", adjustment, before, Snapshot(adjustment));
                await RecordEventAsync(adjustment, "Reverse", ProcurementControlEventResult.Allowed, before, Snapshot(adjustment));
                await _adjustmentRepository.UpdateAsync(adjustment);
                await _unitOfWork.SaveChangesAsync();
                foreach (var item in adjustment.Items.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id))
                    await ApplyAdjustmentLineAsync(adjustment, item, reverse: true, userId, request.NegativeStockOverrideIds);
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();
                return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve reversed adjustment.");
            }
            catch
            {
                if (_unitOfWork.HasActiveTransaction) await _unitOfWork.RollbackAsync();
                _unitOfWork.ClearTrackedChanges();
                throw;
            }
        });
    }

    /// <summary>
    /// Cancels a stock adjustment (only if not yet posted)
    /// </summary>
    public async Task<StockAdjustmentDetailDto> CancelAsync(Guid id, Guid userId)
    {
        try
        {
            EnsureActor(userId);
            var adjustment = await LoadAsync(id)
                ?? throw new ArgumentException($"Stock adjustment {id} not found");

            if (adjustment.Status != "Draft")
            {
                throw new InvalidOperationException($"Only Draft adjustments can be cancelled; current status is {adjustment.Status}.");
            }

            await RequireAccessAsync("procurement.inventory.adjust.request", adjustment, adjustment.AdjustmentNumber);
            var before = Snapshot(adjustment);
            adjustment.Status = "Cancelled";
            adjustment.IntegrityHash = AdjustmentIntegrityHash(adjustment);
            await AddActionAsync(adjustment, "Cancelled", $"cancel:{Guid.NewGuid():N}", "Draft adjustment cancelled.");
            await AddAuditAsync("Cancel", adjustment, before, Snapshot(adjustment));
            await RecordEventAsync(adjustment, "Cancel", ProcurementControlEventResult.Allowed, before, Snapshot(adjustment));
            await _adjustmentRepository.UpdateAsync(adjustment);
            await _unitOfWork.SaveChangesAsync();
            
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

    private async Task<StockAdjustmentDetailDto> ExecuteControlledMutationAsync(
        Guid adjustmentId,
        Func<StockAdjustment, Task<StockAdjustment>> mutation)
    {
        return await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                await _unitOfWork.AcquireTransactionLockAsync($"stock-adjustment:{_currentUserProvider.TenantId:N}:{adjustmentId:N}");
                var adjustment = await LoadAsync(adjustmentId)
                    ?? throw new ArgumentException($"Stock adjustment {adjustmentId} not found");
                adjustment = await mutation(adjustment);
                // Lifecycle mutations flush the tracked parent before appending their
                // immutable action. An idempotent replay returns the tracked entity
                // unchanged, so do not mark the aggregate Modified again: doing so
                // advances RowVersion and makes the caller's next genuine action stale.
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();
                return await GetByIdAsync(adjustmentId)
                    ?? throw new InvalidOperationException("Failed to retrieve the controlled stock adjustment.");
            }
            catch
            {
                if (_unitOfWork.HasActiveTransaction) await _unitOfWork.RollbackAsync();
                _unitOfWork.ClearTrackedChanges();
                throw;
            }
        });
    }

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
            BookClassification = adjustment.BookClassification,
            Status = adjustment.Status,
            TotalAdjustmentValue = adjustment.TotalAdjustmentValue,
            ItemCount = adjustment.Items?.Count ?? 0,
            ApprovedByName = adjustment.ApprovedBy?.FullName,
            ApprovedAt = adjustment.ApprovedAt,
            CreatedAt = adjustment.CreatedAt,
            RequestedById = adjustment.RequestedById,
            SubmittedById = adjustment.SubmittedById,
            SubmittedAtUtc = adjustment.SubmittedAtUtc,
            PostedById = adjustment.PostedById,
            PostedAtUtc = adjustment.PostedAtUtc,
            ReversedById = adjustment.ReversedById,
            ReversedAtUtc = adjustment.ReversedAtUtc,
            ReversalReason = adjustment.ReversalReason,
            FinancePostingEventId = adjustment.FinancePostingEventId,
            FinanceJournalEntryId = adjustment.FinanceJournalEntryId,
            ReversalFinancePostingEventId = adjustment.ReversalFinancePostingEventId,
            ReversalFinanceJournalEntryId = adjustment.ReversalFinanceJournalEntryId,
            RowVersion = Convert.ToBase64String(adjustment.RowVersion)
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
            BookClassification = adjustment.BookClassification,
            Status = adjustment.Status,
            TotalAdjustmentValue = adjustment.TotalAdjustmentValue,
            ItemCount = adjustment.Items?.Count ?? 0,
            ApprovedByName = adjustment.ApprovedBy?.FullName,
            ApprovedAt = adjustment.ApprovedAt,
            CreatedAt = adjustment.CreatedAt,
            RequestedById = adjustment.RequestedById,
            SubmittedById = adjustment.SubmittedById,
            SubmittedAtUtc = adjustment.SubmittedAtUtc,
            PostedById = adjustment.PostedById,
            PostedAtUtc = adjustment.PostedAtUtc,
            ReversedById = adjustment.ReversedById,
            ReversedAtUtc = adjustment.ReversedAtUtc,
            ReversalReason = adjustment.ReversalReason,
            FinancePostingEventId = adjustment.FinancePostingEventId,
            FinanceJournalEntryId = adjustment.FinanceJournalEntryId,
            ReversalFinancePostingEventId = adjustment.ReversalFinancePostingEventId,
            ReversalFinanceJournalEntryId = adjustment.ReversalFinanceJournalEntryId,
            RowVersion = Convert.ToBase64String(adjustment.RowVersion),
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
                BatchNumber = item.BatchNumber,
                ManufactureDate = item.ManufactureDate,
                ExpiryDate = item.ExpiryDate,
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
            }).ToList() ?? new List<StockAdjustmentItemDto>(),
            Evidence = adjustment.Evidence?.OrderBy(x => x.CreatedAt).Select(x => new InventoryControlEvidenceDto
            {
                Id = x.Id,
                CentralDocumentVersionId = x.CentralDocumentVersionId,
                FileUploadRecordId = x.FileUploadRecordId,
                EvidenceReference = x.EvidenceReference,
                DocumentReference = x.CentralDocumentVersion?.DocumentRecord?.DocumentReference ?? string.Empty,
                VersionNumber = x.CentralDocumentVersion?.VersionNumber ?? string.Empty
            }).ToList() ?? new List<InventoryControlEvidenceDto>(),
            Actions = adjustment.Actions?.OrderBy(x => x.Sequence).Select(x => new StockAdjustmentActionDto
            {
                Sequence = x.Sequence,
                ActionType = x.ActionType,
                ActorUserId = x.ActorUserId,
                OccurredAtUtc = x.OccurredAtUtc,
                Comment = x.Comment
            }).ToList() ?? new List<StockAdjustmentActionDto>()
        };

        return dto;
    }

    private IQueryable<StockAdjustment> Adjustments => _unitOfWork.Repository<StockAdjustment>().GetQueryable()
        .Where(x => x.TenantId == _currentUserProvider.TenantId && !x.IsDeleted)
        .Include(x => x.Warehouse)
        .Include(x => x.ApprovedBy)
        .Include(x => x.Items).ThenInclude(x => x.InventoryItem).ThenInclude(x => x.Category)
        .Include(x => x.Items).ThenInclude(x => x.Location).ThenInclude(x => x!.Warehouse)
        .Include(x => x.Evidence).ThenInclude(x => x.CentralDocumentVersion).ThenInclude(x => x.DocumentRecord)
        .Include(x => x.Actions)
        .AsSplitQuery();

    private Task<StockAdjustment?> LoadAsync(Guid id) => Adjustments.FirstOrDefaultAsync(x => x.Id == id);

    private async Task BuildLinesAsync(StockAdjustment adjustment, IReadOnlyCollection<CreateStockAdjustmentItemDto> requests)
    {
        if (requests.Select(x => Math.Sign(x.AdjustmentQuantity)).Distinct().Count() > 1 &&
            adjustment.ReasonCode is not (StockAdjustmentReasonCodes.CycleCount or StockAdjustmentReasonCodes.PhysicalCount))
            throw new InvalidOperationException("One stock adjustment cannot mix increases and decreases.");
        var warehouse = await _warehouseRepository.GetByIdAsync(adjustment.WarehouseId)
            ?? throw new ArgumentException("The selected warehouse was not found.");
        if (warehouse.TenantId != adjustment.TenantId || warehouse.IsDeleted)
            throw new ArgumentException("The selected warehouse was not found in the current tenant.");
        if (!warehouse.IsActive) throw new InvalidOperationException("The selected warehouse is inactive.");
        if (adjustment.ReasonCode == StockAdjustmentReasonCodes.InitialStock && warehouse.IsConsignmentWarehouse)
            throw new InvalidOperationException("Opening stock can be loaded only into an owned, non-consignment warehouse.");
        foreach (var input in requests)
        {
            if (!input.LocationId.HasValue || input.LocationId == Guid.Empty)
                throw new InvalidOperationException("Every controlled stock-adjustment line requires an exact warehouse location.");
            var location = await _locationRepository.GetByIdAsync(input.LocationId.Value)
                ?? throw new ArgumentException("A selected stock-adjustment location was not found.");
            if (location.TenantId != adjustment.TenantId || location.IsDeleted)
                throw new ArgumentException("A selected stock-adjustment location was not found in the current tenant.");
            if (location.InventoryWarehouseId != adjustment.WarehouseId || !location.IsActive)
                throw new InvalidOperationException("Every adjustment location must be active and belong to the selected warehouse.");
            if (adjustment.ReasonCode == StockAdjustmentReasonCodes.InitialStock &&
                (location.IsConsignmentBin || location.WarehouseId != adjustment.WarehouseId))
                throw new InvalidOperationException("Opening stock requires an owned location in the selected warehouse.");
            var inventoryItem = await _itemRepository.GetByIdAsync(input.InventoryItemId)
                ?? throw new ArgumentException($"Inventory item {input.InventoryItemId} not found.");
            if (inventoryItem.TenantId != adjustment.TenantId || inventoryItem.IsDeleted)
                throw new ArgumentException($"Inventory item {input.InventoryItemId} not found.");
            if (inventoryItem.Status != ItemStatus.Active || !IsStockedInventoryItem(inventoryItem.ItemType))
                throw new InvalidOperationException($"Inventory item {inventoryItem.ItemCode} must be an active stocked item.");
            var exactBalance = await _unitOfWork.Repository<InventoryLocation>().GetQueryable(value =>
                    value.TenantId == adjustment.TenantId && value.InventoryItemId == input.InventoryItemId &&
                    value.LocationId == location.Id && !value.IsDeleted)
                .AsNoTracking().SingleOrDefaultAsync();
            var systemQuantity = exactBalance?.Quantity ?? 0m;
            var isOpeningStock = adjustment.ReasonCode == StockAdjustmentReasonCodes.InitialStock;
            if (isOpeningStock && systemQuantity != 0m)
                throw new InvalidOperationException(
                    $"Opening stock requires an empty exact location balance for {inventoryItem.ItemCode}.");
            var unitCost = isOpeningStock
                ? decimal.Round(input.UnitCost ?? 0m, 4)
                : await ResolveAdjustmentUnitCostAsync(
                    inventoryItem, location.InventoryWarehouseId, location.Id, input.AdjustmentQuantity);
            if (unitCost <= 0)
                throw new InvalidOperationException(isOpeningStock
                    ? $"A positive source-schedule unit cost is required for {inventoryItem.ItemCode}."
                    : $"A server-derived inventory cost is required for {inventoryItem.ItemCode}.");
            var line = new StockAdjustmentItem
            {
                TenantId = adjustment.TenantId,
                AdjustmentId = adjustment.Id,
                InventoryItemId = input.InventoryItemId,
                LocationId = input.LocationId,
                SerialNumber = Normalize(input.SerialNumber, 100),
                LotNumber = Normalize(input.LotNumber, 100),
                BatchNumber = Normalize(input.BatchNumber, 100),
                ManufactureDate = Utc(input.ManufactureDate),
                ExpiryDate = Utc(input.ExpiryDate),
                SystemQuantity = systemQuantity,
                PhysicalQuantity = systemQuantity + input.AdjustmentQuantity,
                AdjustmentQuantity = input.AdjustmentQuantity,
                UnitCost = unitCost,
                AdjustmentValue = decimal.Round(input.AdjustmentQuantity * unitCost, 2),
                Reason = Normalize(input.Reason, 500),
                Notes = Normalize(input.Notes, 1000),
                InventoryItem = inventoryItem,
                Location = location
            };
            adjustment.Items.Add(line);
            adjustment.TotalAdjustmentValue += line.AdjustmentValue;
        }
    }

    private async Task RequireAccessAsync(string permission, StockAdjustment adjustment, string reference)
    {
        var locations = adjustment.Items.Select(x => x.LocationId).Distinct().ToList();
        if (locations.Count == 0) locations.Add(null);
        foreach (var locationId in locations)
        {
            var decision = await _accessControl.EnforceCapabilityAsync(new ProcurementAccessCapabilityRequest
            {
                PermissionCode = permission,
                WarehouseId = adjustment.WarehouseId,
                LocationId = locationId,
                RequireLocationScope = true,
                SourceType = "StockAdjustment",
                SourceReference = reference
            }, adjustment.CorrelationId ?? Guid.NewGuid().ToString("N"));
            if (!decision.Allowed) throw new InvalidOperationException(decision.Message);
        }
    }

    public async Task<StockAdjustmentDetailDto> RetireApprovedForRecountAsync(
        Guid id,
        Guid userId,
        StockAdjustmentActionRequest request)
    {
        EnsureActor(userId);
        var key = Required(request.IdempotencyKey, "An idempotency key is required.", 100);
        var reason = Required(request.Comment, "A controlled recount retirement reason is required.", 1000);
        return await ExecuteControlledMutationAsync(id, async adjustment =>
        {
            if (adjustment.Status == "Cancelled")
            {
                if (await HasActionAsync(adjustment.Id, "Cancelled", key)) return adjustment;
                throw new InvalidOperationException("The approved adjustment was already retired by a different control action.");
            }
            if (adjustment.Status != "Approved")
                throw new InvalidOperationException($"Only an approved, unposted adjustment can be retired for recount; current status is {adjustment.Status}.");
            EnsureRowVersion(adjustment.RowVersion, request.RowVersion);

            var linkedCount = await _unitOfWork.Repository<PhysicalCount>().GetQueryable(value =>
                    value.TenantId == adjustment.TenantId && value.StockAdjustmentId == adjustment.Id &&
                    !value.IsDeleted && (value.Status == "PendingFinanceApproval" ||
                                         value.Status == "PendingAuditAttestation"))
                .AsNoTracking().Select(value => new { value.Id, value.Status, value.CountNumber })
                .SingleOrDefaultAsync()
                ?? throw new InvalidOperationException(
                    "An approved adjustment can be retired only while its physical count awaits Finance or Internal Audit review.");
            await RequireAccessAsync(
                linkedCount.Status == "PendingAuditAttestation"
                    ? "procurement.inventory.read"
                    : "procurement.inventory.adjust.approve",
                adjustment,
                linkedCount.CountNumber);

            var before = Snapshot(adjustment);
            adjustment.Status = "Cancelled";
            adjustment.IntegrityHash = AdjustmentIntegrityHash(adjustment);
            await PersistLifecycleStateBeforeActionAsync(adjustment);
            await AddActionAsync(adjustment, "Cancelled", key, reason);
            await AddAuditAsync("RetireForRecount", adjustment, before, Snapshot(adjustment));
            await RecordEventAsync(adjustment, "RetireForRecount", ProcurementControlEventResult.Rejected,
                before, Snapshot(adjustment));
            return adjustment;
        });
    }

    private async Task<decimal> ResolveAdjustmentUnitCostAsync(
        InventoryItem inventoryItem,
        Guid warehouseId,
        Guid locationId,
        decimal quantityDelta)
    {
        var fallback = inventoryItem.AverageCost > 0 ? inventoryItem.AverageCost
            : inventoryItem.StandardCost > 0 ? inventoryItem.StandardCost : inventoryItem.LastPurchaseCost;
        if (inventoryItem.ValuationMethod == ValuationMethod.StandardCost)
            return inventoryItem.StandardCost;

        var balance = await _unitOfWork.Repository<InventoryBalance>().GetQueryable(value =>
                value.TenantId == inventoryItem.TenantId && value.InventoryItemId == inventoryItem.Id &&
                value.WarehouseId == warehouseId && value.LocationId == locationId && !value.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync();
        if (inventoryItem.ValuationMethod == ValuationMethod.WeightedAverage)
            return balance?.AverageUnitCost > 0 ? balance.AverageUnitCost : fallback;
        if (quantityDelta >= 0)
            return balance?.AverageUnitCost > 0 ? balance.AverageUnitCost : fallback;

        var remaining = Math.Abs(quantityDelta);
        decimal total = 0;
        var layers = await _unitOfWork.Repository<InventoryLayer>().GetQueryable(value =>
                value.TenantId == inventoryItem.TenantId && value.InventoryItemId == inventoryItem.Id &&
                value.WarehouseId == warehouseId && value.LocationId == locationId &&
                !value.IsFullyConsumed && value.RemainingQuantity > 0)
            .AsNoTracking().OrderBy(value => value.LayerDate).ThenBy(value => value.CreatedAt).ToListAsync();
        foreach (var layer in layers)
        {
            if (remaining <= 0) break;
            var consumed = Math.Min(remaining, layer.RemainingQuantity);
            total += consumed * layer.UnitCost;
            remaining -= consumed;
        }
        total += remaining * fallback;
        return total / Math.Abs(quantityDelta);
    }

    private async Task<bool> CanReadAsync(StockAdjustment adjustment)
    {
        var locations = adjustment.Items.Where(x => !x.IsDeleted).Select(x => x.LocationId).Distinct().ToList();
        if (locations.Count == 0) locations.Add(null);
        foreach (var locationId in locations)
        {
            try
            {
                var decision = await _accessControl.CheckCapabilityAsync(new ProcurementAccessCapabilityRequest
                {
                    PermissionCode = "procurement.inventory.read",
                    WarehouseId = adjustment.WarehouseId,
                    LocationId = locationId,
                    RequireLocationScope = true,
                    SourceType = "StockAdjustment",
                    SourceReference = adjustment.AdjustmentNumber
                }, adjustment.CorrelationId ?? Guid.NewGuid().ToString("N"));
                if (!decision.Allowed) return false;
            }
            catch (ProcurementAccessAuthorizationException) { return false; }
            catch (ProcurementAccessValidationException) { return false; }
        }
        return true;
    }

    private async Task AddEvidenceAsync(StockAdjustment adjustment, IReadOnlyCollection<InventoryControlEvidenceRequest> requests, bool required)
    {
        if (required && requests.Count == 0)
            throw new InvalidOperationException("Current published central-DMS evidence is required for this adjustment reason.");
        if (requests.GroupBy(x => x.CentralDocumentVersionId).Any(x => x.Count() > 1))
            throw new InvalidOperationException("The same central-DMS version cannot be linked twice.");
        foreach (var request in requests)
        {
            var version = await _unitOfWork.Repository<CentralDocumentVersion>().GetQueryable()
                .Include(x => x.DocumentRecord)
                .FirstOrDefaultAsync(x => x.Id == request.CentralDocumentVersionId && x.TenantId == adjustment.TenantId &&
                    !x.IsDeleted && !x.DocumentRecord.IsDeleted &&
                    x.DocumentRecord.LifecycleStatus == CentralDocumentEvidenceRules.ActiveLifecycleStatus &&
                    x.DocumentRecord.VersionStatus == CentralDocumentEvidenceRules.PublishedVersionStatus &&
                    x.DocumentRecord.CurrentVersion == x.VersionNumber && x.Status == CentralDocumentEvidenceRules.PublishedVersionStatus &&
                    x.PublishedAt.HasValue && x.FileUploadRecordId.HasValue)
                ?? throw new InvalidOperationException("Evidence must reference the current published version in central DMS.");
            var cleanUpload = await _unitOfWork.Repository<FileUploadRecord>().GetQueryable().AsNoTracking()
                .AnyAsync(x => x.Id == version.FileUploadRecordId!.Value &&
                    x.TenantId == adjustment.TenantId && !x.IsDeleted &&
                    x.VirusScanStatus == FileVirusScanStatus.Clean);
            if (!cleanUpload)
                throw new InvalidOperationException("Evidence must have a successful clean malware scan.");
            var evidence = new StockAdjustmentEvidence
            {
                TenantId = adjustment.TenantId,
                StockAdjustmentId = adjustment.Id,
                CentralDocumentVersionId = version.Id,
                FileUploadRecordId = version.FileUploadRecordId.Value,
                EvidenceReference = Required(request.EvidenceReference, "An evidence reference is required.", 500)
            };
            evidence.IntegrityHash = Hash($"{adjustment.Id:N}|{version.Id:N}|{evidence.FileUploadRecordId:N}|{evidence.EvidenceReference}");
            adjustment.Evidence.Add(evidence);
        }
    }

    private async Task RevalidateEvidenceAsync(StockAdjustment adjustment)
    {
        if (EvidenceRequired(adjustment.ReasonCode) && adjustment.Evidence.Count == 0)
            throw new InvalidOperationException("Current published central-DMS evidence is required for this adjustment reason.");
        foreach (var evidence in adjustment.Evidence)
        {
            var current = await _unitOfWork.Repository<CentralDocumentVersion>().GetQueryable()
                .Include(x => x.DocumentRecord)
                .AnyAsync(x => x.Id == evidence.CentralDocumentVersionId && x.FileUploadRecordId == evidence.FileUploadRecordId &&
                    x.TenantId == adjustment.TenantId && !x.IsDeleted && !x.DocumentRecord.IsDeleted &&
                    x.DocumentRecord.LifecycleStatus == CentralDocumentEvidenceRules.ActiveLifecycleStatus &&
                    x.DocumentRecord.VersionStatus == CentralDocumentEvidenceRules.PublishedVersionStatus &&
                    x.DocumentRecord.CurrentVersion == x.VersionNumber && x.Status == CentralDocumentEvidenceRules.PublishedVersionStatus && x.PublishedAt.HasValue);
            if (!current) throw new InvalidOperationException("Linked central-DMS evidence is no longer current and published.");
            var cleanUpload = await _unitOfWork.Repository<FileUploadRecord>().GetQueryable().AsNoTracking()
                .AnyAsync(x => x.Id == evidence.FileUploadRecordId &&
                    x.TenantId == adjustment.TenantId && !x.IsDeleted &&
                    x.VirusScanStatus == FileVirusScanStatus.Clean);
            if (!cleanUpload)
                throw new InvalidOperationException("Linked central-DMS evidence no longer has a successful clean malware scan.");
        }
    }

    private async Task RevalidateOpeningStockAsync(StockAdjustment adjustment)
    {
        if (adjustment.ReasonCode != StockAdjustmentReasonCodes.InitialStock) return;

        if (adjustment.AdjustmentDate == default)
            throw new InvalidOperationException("The opening-stock date is missing.");
        _ = Required(adjustment.Reference, "The opening-stock source schedule reference is missing.", 50);
        var book = Required(adjustment.BookClassification, "The opening-stock accounting book is missing.", 20)
            .ToUpperInvariant();
        if (book == "ALL_ACTIVE_BOOKS")
            throw new InvalidOperationException("Opening stock must identify one explicit accounting book.");
        if (adjustment.Items.Count == 0)
            throw new InvalidOperationException("The opening-stock schedule has no lines.");
        if (adjustment.Items.Where(x => !x.IsDeleted).GroupBy(x => new
            {
                x.InventoryItemId,
                x.LocationId,
                SerialNumber = x.SerialNumber?.Trim().ToUpperInvariant(),
                LotNumber = x.LotNumber?.Trim().ToUpperInvariant(),
                BatchNumber = x.BatchNumber?.Trim().ToUpperInvariant()
            }).Any(x => x.Count() > 1))
            throw new InvalidOperationException("The opening-stock schedule contains a duplicate item/location/tracking identity.");

        var warehouse = await _warehouseRepository.GetByIdAsync(adjustment.WarehouseId)
            ?? throw new InvalidOperationException("The opening-stock warehouse no longer exists.");
        if (warehouse.TenantId != adjustment.TenantId || warehouse.IsDeleted || !warehouse.IsActive ||
            warehouse.IsConsignmentWarehouse)
            throw new InvalidOperationException("The opening-stock warehouse must remain active, owned and in the current tenant.");

        decimal expectedTotal = 0m;
        foreach (var line in adjustment.Items.Where(x => !x.IsDeleted))
        {
            if (line.TenantId != adjustment.TenantId || line.AdjustmentQuantity <= 0m || line.UnitCost <= 0m ||
                line.SystemQuantity != 0m || line.PhysicalQuantity != line.AdjustmentQuantity)
                throw new InvalidOperationException("Every opening-stock line must retain positive quantity/cost and zero opening balance evidence.");
            var expectedValue = decimal.Round(line.AdjustmentQuantity * line.UnitCost, 2);
            if (line.AdjustmentValue != expectedValue)
                throw new InvalidOperationException("An opening-stock line value no longer agrees with its quantity and unit cost.");
            expectedTotal += expectedValue;

            var location = line.LocationId.HasValue
                ? await _locationRepository.GetByIdAsync(line.LocationId.Value)
                : null;
            if (location is null || location.TenantId != adjustment.TenantId || location.IsDeleted ||
                !location.IsActive || location.IsConsignmentBin || location.WarehouseId != adjustment.WarehouseId)
                throw new InvalidOperationException("Every opening-stock location must remain active, owned and in the selected warehouse.");
            var item = await _itemRepository.GetByIdAsync(line.InventoryItemId);
            if (item is null || item.TenantId != adjustment.TenantId || item.IsDeleted ||
                item.Status != ItemStatus.Active || item.ItemType != ItemType.StockItem)
                throw new InvalidOperationException("Every opening-stock item must remain an active stock item in the current tenant.");
            var currentQuantity = await _unitOfWork.Repository<InventoryLocation>().GetQueryable(x =>
                    x.TenantId == adjustment.TenantId && !x.IsDeleted &&
                    x.InventoryItemId == line.InventoryItemId && x.LocationId == location.Id)
                .AsNoTracking()
                .Select(x => (decimal?)x.Quantity)
                .SingleOrDefaultAsync() ?? 0m;
            if (currentQuantity != line.SystemQuantity)
                throw new InvalidOperationException(
                    $"The exact-location quantity for {item.ItemCode} changed after the opening schedule was created.");
        }
        if (decimal.Round(adjustment.TotalAdjustmentValue, 2) != decimal.Round(expectedTotal, 2))
            throw new InvalidOperationException("The opening-stock schedule total no longer agrees with its line evidence.");
    }

    private async Task ApplyAdjustmentLineAsync(
        StockAdjustment adjustment,
        StockAdjustmentItem item,
        bool reverse,
        Guid userId,
        IReadOnlyDictionary<Guid, Guid> negativeStockOverrideIds)
    {
        var location = (item.LocationId.HasValue ? await _locationRepository.GetByIdAsync(item.LocationId.Value) : null)
            ?? throw new InvalidOperationException("The exact adjustment location no longer exists.");
        if (location.TenantId != adjustment.TenantId || location.IsDeleted ||
            location.InventoryWarehouseId != adjustment.WarehouseId || (!reverse && !location.IsActive))
            throw new InvalidOperationException("The adjustment location is inactive or outside the selected warehouse.");
        var warehouseId = location.InventoryWarehouseId;
        var warehouse = await _warehouseRepository.GetByIdAsync(warehouseId)
            ?? throw new InvalidOperationException("The effective adjustment warehouse no longer exists.");
        var inventoryItem = await _itemRepository.GetByIdAsync(item.InventoryItemId)
            ?? throw new InvalidOperationException("An adjustment item no longer exists.");
        if (warehouse.TenantId != adjustment.TenantId || warehouse.IsDeleted || (!reverse && !warehouse.IsActive))
            throw new InvalidOperationException("The effective adjustment warehouse is inactive or outside the current tenant.");
        if (inventoryItem.TenantId != adjustment.TenantId || inventoryItem.IsDeleted ||
            (!reverse && (inventoryItem.Status != ItemStatus.Active || !IsStockedInventoryItem(inventoryItem.ItemType))))
            throw new InvalidOperationException("The adjustment item is inactive or outside the current tenant.");
        var delta = reverse ? -item.AdjustmentQuantity : item.AdjustmentQuantity;
        InventoryStockDecreaseAuthorization? decreaseAuthorization = null;
        if (delta < 0)
        {
            decreaseAuthorization = await _negativeStockControls.PrepareDecreaseAsync(new InventoryStockDecreaseRequest
            {
                InventoryItemId = item.InventoryItemId,
                WarehouseId = warehouseId,
                LocationId = item.LocationId,
                Quantity = Math.Abs(delta),
                ReferenceType = reverse ? "StockAdjustmentReversal" : "StockAdjustment",
                ReferenceNumber = adjustment.AdjustmentNumber,
                ReferenceId = adjustment.Id,
                ReferenceLineId = item.Id,
                NegativeStockOverrideId = negativeStockOverrideIds.GetValueOrDefault(item.Id),
                CheckInventoryItemBalance = !warehouse.IsConsignmentWarehouse,
                CorrelationId = adjustment.CorrelationId ?? $"stock-adjustment:{adjustment.Id:N}"
            });
        }
        var warehouseQuantity = await _warehouseQuantityRepository.GetByWarehouseAndItemAsync(warehouseId, item.InventoryItemId);
        var warehouseQuantityIsNew = warehouseQuantity is null;
        if (warehouseQuantity is null)
        {
            if (delta < 0) throw new InvalidOperationException($"Warehouse stock is unavailable for {inventoryItem.ItemCode}.");
            warehouseQuantity = new WarehouseQuantity
            {
                TenantId = adjustment.TenantId,
                InventoryItemId = item.InventoryItemId,
                WarehouseId = warehouseId,
                AverageCost = item.UnitCost,
                CreatedById = userId
            };
            await _warehouseQuantityRepository.AddAsync(warehouseQuantity);
        }
        if (delta < 0 && warehouseQuantity.AvailableStock < Math.Abs(delta) &&
            decreaseAuthorization?.EmergencyOverrideApplied != true)
            throw new InvalidOperationException($"Available warehouse stock is insufficient for {inventoryItem.ItemCode}.");
        if (!warehouse.IsConsignmentWarehouse && delta < 0 && inventoryItem.AvailableStock < Math.Abs(delta) &&
            decreaseAuthorization?.EmergencyOverrideApplied != true)
            throw new InvalidOperationException($"Available item stock is insufficient for {inventoryItem.ItemCode}.");

        await _trackingControls.StageEventAsync(new InventoryTrackingMutationRequest
        {
            InventoryItemId = item.InventoryItemId,
            WarehouseId = warehouseId,
            LocationId = item.LocationId,
            Direction = delta > 0 ? InventoryTrackingDirection.AdjustmentIn : InventoryTrackingDirection.AdjustmentOut,
            Quantity = Math.Abs(delta),
            ReferenceType = reverse ? "StockAdjustmentReversal" : "StockAdjustment",
            ReferenceNumber = adjustment.AdjustmentNumber,
            ReferenceId = adjustment.Id,
            ReferenceLineId = item.Id,
            EventKey = $"stock-adjustment:{adjustment.Id:N}:{item.Id:N}:{(reverse ? "reverse" : "post")}",
            LotNumber = item.LotNumber,
            BatchNumber = item.BatchNumber,
            SerialNumber = item.SerialNumber,
            ManufactureDate = item.ManufactureDate,
            ExpiryDate = item.ExpiryDate,
            CorrelationId = adjustment.CorrelationId ?? $"stock-adjustment:{adjustment.Id:N}"
        });
        var inventoryLocationRepository = _unitOfWork.Repository<InventoryLocation>();
        var inventoryLocation = await inventoryLocationRepository.GetQueryable(value =>
                value.TenantId == adjustment.TenantId &&
                value.InventoryItemId == item.InventoryItemId &&
                value.LocationId == location.Id && !value.IsDeleted)
            .SingleOrDefaultAsync();
        var inventoryLocationIsNew = inventoryLocation is null;
        if (inventoryLocation is null)
        {
            if (delta < 0 && decreaseAuthorization?.EmergencyOverrideApplied != true)
                throw new InvalidOperationException($"Exact-bin stock is unavailable for {inventoryItem.ItemCode}.");
            inventoryLocation = new InventoryLocation
            {
                TenantId = adjustment.TenantId,
                InventoryItemId = item.InventoryItemId,
                LocationId = location.Id,
                AverageCost = item.UnitCost,
                CreatedById = userId
            };
            await inventoryLocationRepository.AddAsync(inventoryLocation);
        }
        var openingLocationQuantity = inventoryLocation.Quantity;
        var openingLocationAverageCost = inventoryLocation.AverageCost;
        if (delta < 0 && inventoryLocation.AvailableQuantity < Math.Abs(delta) &&
            decreaseAuthorization?.EmergencyOverrideApplied != true)
            throw new InvalidOperationException($"Available exact-bin stock is insufficient for {inventoryItem.ItemCode}.");

        var authoritativeValue = await _valuation.ProcessAdjustmentAsync(
            item.InventoryItemId,
            warehouseId,
            location.Id,
            delta,
            item.UnitCost,
            openingLocationQuantity,
            decreaseAuthorization?.EmergencyOverrideApplied == true,
            adjustment.AdjustmentNumber,
            adjustment.Id,
            item.LotNumber,
            item.SerialNumber,
            reversalSourceId: reverse && item.AdjustmentQuantity > 0 ? adjustment.Id : null);
        if (decimal.Round(authoritativeValue, 2) != decimal.Round(Math.Abs(item.AdjustmentValue), 2))
            throw new InvalidOperationException(
                $"The authoritative valuation for {inventoryItem.ItemCode} changed after the adjustment was drafted. Refresh the adjustment before posting.");
        inventoryLocation.Quantity += delta;
        inventoryLocation.AvailableQuantity = inventoryLocation.Quantity - inventoryLocation.AllocatedQuantity;
        if (inventoryLocation.Quantity <= 0)
            inventoryLocation.AverageCost = 0;
        else if (delta > 0)
            inventoryLocation.AverageCost =
                ((openingLocationQuantity * (openingLocationAverageCost > 0 ? openingLocationAverageCost : item.UnitCost)) + authoritativeValue) /
                inventoryLocation.Quantity;
        inventoryLocation.LastMovementDate = DateTime.UtcNow;
        // Inventory owns these zero-state balance rows. Preserve Added tracking for the first
        // governed opening receipt; calling UpdateAsync on a just-added row changes it into an
        // UPDATE and produces a false optimistic-concurrency failure because no row exists yet.
        if (!inventoryLocationIsNew)
            await inventoryLocationRepository.UpdateAsync(inventoryLocation);
        warehouseQuantity.CurrentStock += delta;
        warehouseQuantity.AvailableStock = warehouseQuantity.CurrentStock - warehouseQuantity.AllocatedStock;
        warehouseQuantity.LastMovementDate = DateTime.UtcNow;
        if (!warehouseQuantityIsNew)
            await _warehouseQuantityRepository.UpdateAsync(warehouseQuantity);
        if (!warehouse.IsConsignmentWarehouse)
        {
            inventoryItem.CurrentStock += delta;
            inventoryItem.AvailableStock = inventoryItem.CurrentStock - inventoryItem.AllocatedStock;
            inventoryItem.LastStockDate = DateTime.UtcNow;
            await _itemRepository.UpdateAsync(inventoryItem);
        }
        if (decreaseAuthorization?.EmergencyOverrideApplied == true)
        {
            await _unitOfWork.SaveChangesAsync();
            await _negativeStockControls.ClearMutationContextAsync();
        }
        var movement = new StockMovement
        {
            TenantId = adjustment.TenantId,
            InventoryItemId = item.InventoryItemId,
            MovementType = reverse ? "AdjustmentReversal" : delta > 0 ? "Adjustment+" : "Adjustment-",
            Quantity = delta,
            UnitCost = item.UnitCost,
            TotalValue = reverse ? -item.AdjustmentValue : item.AdjustmentValue,
            MovementDate = DateTime.UtcNow,
            ReferenceType = ReferenceType.Adjustment,
            ReferenceNumber = adjustment.AdjustmentNumber,
            ReferenceId = adjustment.Id,
            WarehouseId = warehouseId,
            LocationId = item.LocationId,
            Notes = $"{adjustment.ReasonCode}: {item.Notes ?? adjustment.Description}",
            SerialNumber = item.SerialNumber,
            LotNumber = item.LotNumber,
            BatchNumber = item.BatchNumber,
            ManufactureDate = item.ManufactureDate,
            ExpirationDate = item.ExpiryDate,
            RunningBalance = warehouse.IsConsignmentWarehouse ? warehouseQuantity.CurrentStock : inventoryItem.CurrentStock,
            ProcessedById = userId
        };
        await _movementRepository.AddAsync(movement);
        await _consignmentSettlementService.TryCreateFromStockMovementAsync(movement);
    }

    private async Task PersistLifecycleStateBeforeActionAsync(StockAdjustment adjustment)
    {
        // StockAdjustmentActions is append-only and its SQL guard validates the action against
        // the durable parent lifecycle state. EF orders a dependent action INSERT before a parent
        // UPDATE in a combined save, so flush the governed parent first inside the caller's
        // serializable transaction. A later action/audit failure still rolls the whole mutation back.
        await _adjustmentRepository.UpdateAsync(adjustment);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task AddActionAsync(StockAdjustment adjustment, string type, string key, string? comment)
    {
        var sequence = await _unitOfWork.Repository<StockAdjustmentAction>().GetQueryable().AsNoTracking()
            .CountAsync(x => x.StockAdjustmentId == adjustment.Id) + 1;
        var action = new StockAdjustmentAction
        {
            TenantId = adjustment.TenantId,
            StockAdjustmentId = adjustment.Id,
            Sequence = sequence,
            ActionType = type,
            ActorUserId = _currentUserProvider.UserId,
            OccurredAtUtc = DateTime.UtcNow,
            IdempotencyKey = key,
            CorrelationId = adjustment.CorrelationId ?? $"stock-adjustment:{adjustment.Id:N}",
            Comment = Normalize(comment, 1000),
            SnapshotJson = JsonSerializer.Serialize(Snapshot(adjustment))
        };
        action.IntegrityHash = Hash($"{action.StockAdjustmentId:N}|{action.Sequence}|{action.ActionType}|{action.ActorUserId:N}|{action.OccurredAtUtc:O}|{action.IdempotencyKey}|{action.SnapshotJson}");
        await _unitOfWork.Repository<StockAdjustmentAction>().AddAsync(action);
    }

    private Task<bool> HasActionAsync(Guid adjustmentId, string type, string key) =>
        _unitOfWork.Repository<StockAdjustmentAction>().GetQueryable().AsNoTracking()
            .AnyAsync(x => x.StockAdjustmentId == adjustmentId && x.ActionType == type && x.IdempotencyKey == key);

    private Task AddAuditAsync(string action, StockAdjustment adjustment, object? before, object after) =>
        _unitOfWork.Repository<AuditLog>().AddAsync(new AuditLog
        {
            TenantId = adjustment.TenantId,
            UserId = _currentUserProvider.UserId,
            Username = _currentUserProvider.Username,
            Action = action,
            Resource = "StockAdjustment",
            ResourceId = adjustment.Id.ToString(),
            OldValues = before is null ? null : JsonSerializer.Serialize(before),
            NewValues = JsonSerializer.Serialize(after),
            IpAddress = "system",
            Timestamp = DateTime.UtcNow
        });

    private Task RecordEventAsync(StockAdjustment adjustment, string action, ProcurementControlEventResult result, object? before, object after) =>
        _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = $"stock-adjustment:{adjustment.Id:N}:{action.ToLowerInvariant()}:{adjustment.Actions.Count + 1}",
            EventType = "StockAdjustmentControl",
            Action = action,
            Result = result,
            RuleCode = "INV-011",
            DecisionKeys = Enumerable.Range(1, 14).Select(x => $"DEC-{x:000}").ToList(),
            SourceType = "StockAdjustment",
            SourceId = adjustment.Id,
            SourceReference = adjustment.AdjustmentNumber,
            Before = before,
            After = after,
            CorrelationId = adjustment.CorrelationId ?? $"stock-adjustment:{adjustment.Id:N}",
            OccurredAtUtc = DateTime.UtcNow,
            Evidence = adjustment.Evidence.Select(x => new ProcurementControlEventEvidenceReference
            {
                ReferenceKind = ProcurementControlEvidenceReferenceKind.FileUploadRecord,
                ReferenceId = x.FileUploadRecordId,
                Reference = x.EvidenceReference,
                Label = "Central DMS adjustment evidence",
                RequirementKey = "INV-011"
            }).ToList()
        });

    private static object Snapshot(StockAdjustment item) => new
    {
        item.Id, item.AdjustmentNumber, item.WarehouseId, item.AdjustmentDate, item.BookClassification,
        item.Reference, item.ReasonCode, item.Status, item.TotalAdjustmentValue,
        item.RequestedById, item.SubmittedById, item.ApprovedById, item.PostedById, item.ReversedById,
        item.FinancePostingEventId, item.FinanceJournalEntryId, item.IntegrityHash,
        Lines = item.Items.Where(x => !x.IsDeleted).OrderBy(x => x.InventoryItemId).ThenBy(x => x.LocationId)
            .Select(x => new { x.Id, x.InventoryItemId, x.LocationId, x.SystemQuantity, x.AdjustmentQuantity,
                x.UnitCost, x.AdjustmentValue, x.LotNumber, x.BatchNumber, x.SerialNumber })
    };

    private static string ValidateReasonCode(string value)
    {
        var normalized = Required(value, "A stock-adjustment reason code is required.", 50).ToUpperInvariant();
        if (!StockAdjustmentReasonCodes.ReasonCodeDescriptions.ContainsKey(normalized))
            throw new InvalidOperationException("The stock-adjustment reason code is not supported.");
        return normalized;
    }

    private static bool EvidenceRequired(string reasonCode) => reasonCode is
        StockAdjustmentReasonCodes.Damage or StockAdjustmentReasonCodes.Loss or StockAdjustmentReasonCodes.Theft or
        StockAdjustmentReasonCodes.Expired or StockAdjustmentReasonCodes.QualityIssue or StockAdjustmentReasonCodes.Donation or
        StockAdjustmentReasonCodes.WriteOff or StockAdjustmentReasonCodes.Other;

    private static bool IsStockedInventoryItem(ItemType itemType) =>
        itemType is ItemType.StockItem or ItemType.FixedAsset;

    private static string AdjustmentPayloadHash(StockAdjustment item) => Hash(JsonSerializer.Serialize(new
    {
        item.WarehouseId, item.AdjustmentDate, item.BookClassification, item.ReasonCode, item.Description,
        item.Reference, item.RelatedIssueVoucherId,
        Lines = item.Items.OrderBy(x => x.InventoryItemId).ThenBy(x => x.LocationId)
            .Select(x => new { x.InventoryItemId, x.LocationId, x.AdjustmentQuantity, x.UnitCost, x.LotNumber,
                x.BatchNumber, x.SerialNumber, x.ManufactureDate, x.ExpiryDate, x.Reason, x.Notes }),
        Evidence = item.Evidence.OrderBy(x => x.CentralDocumentVersionId)
            .Select(x => new { x.CentralDocumentVersionId, x.EvidenceReference })
    }));

    internal static string CreateOpeningStockPayloadHash(
        CreateStockAdjustmentDto dto,
        string description,
        string reference,
        string bookClassification) => Hash(JsonSerializer.Serialize(new
    {
        dto.WarehouseId,
        OpeningDate = EnsureUtc(dto.AdjustmentDate!.Value).Date,
        BookClassification = bookClassification,
        SourceScheduleReference = reference.Trim().ToUpperInvariant(),
        Description = description,
        Lines = dto.Items.Select(x => new
            {
                x.InventoryItemId,
                x.LocationId,
                Quantity = x.AdjustmentQuantity,
                UnitCost = decimal.Round(x.UnitCost ?? 0m, 4),
                SerialNumber = Normalize(x.SerialNumber, 100),
                LotNumber = Normalize(x.LotNumber, 100),
                BatchNumber = Normalize(x.BatchNumber, 100),
                ManufactureDate = Utc(x.ManufactureDate),
                ExpiryDate = Utc(x.ExpiryDate),
                Reason = Normalize(x.Reason, 500),
                Notes = Normalize(x.Notes, 1000)
            })
            .OrderBy(x => x.InventoryItemId).ThenBy(x => x.LocationId).ThenBy(x => x.Quantity)
            .ThenBy(x => x.UnitCost).ThenBy(x => x.SerialNumber).ThenBy(x => x.LotNumber)
            .ThenBy(x => x.BatchNumber).ThenBy(x => x.ManufactureDate).ThenBy(x => x.ExpiryDate)
            .ThenBy(x => x.Reason).ThenBy(x => x.Notes)
    }));

    internal static string CreateOpeningStockIdempotencyKey(
        Guid tenantId,
        Guid warehouseId,
        DateTime openingDate,
        string bookClassification,
        string sourceScheduleReference)
    {
        var identity = Hash($"{tenantId:N}|{warehouseId:N}|{EnsureUtc(openingDate):yyyy-MM-dd}|" +
                            $"{bookClassification.Trim().ToUpperInvariant()}|{sourceScheduleReference.Trim().ToUpperInvariant()}");
        return $"OPENING-STOCK:{identity}";
    }

    private static string CreateAdjustmentPayloadHash(
        CreateStockAdjustmentDto dto,
        string reasonCode,
        string description,
        string reference) => Hash(JsonSerializer.Serialize(new
    {
        dto.WarehouseId,
        ReasonCode = reasonCode,
        Description = description,
        Reference = reference,
        dto.AdjustmentDate,
        dto.RelatedIssueVoucherId,
        Lines = dto.Items
            .Select(x => new
            {
                x.InventoryItemId,
                x.LocationId,
                x.AdjustmentQuantity,
                LotNumber = Normalize(x.LotNumber, 100),
                BatchNumber = Normalize(x.BatchNumber, 100),
                SerialNumber = Normalize(x.SerialNumber, 100),
                ManufactureDate = Utc(x.ManufactureDate),
                ExpiryDate = Utc(x.ExpiryDate),
                Reason = Normalize(x.Reason, 500),
                Notes = Normalize(x.Notes, 1000)
            })
            .OrderBy(x => x.InventoryItemId).ThenBy(x => x.LocationId)
            .ThenBy(x => x.AdjustmentQuantity).ThenBy(x => x.LotNumber).ThenBy(x => x.BatchNumber)
            .ThenBy(x => x.SerialNumber).ThenBy(x => x.ManufactureDate).ThenBy(x => x.ExpiryDate)
            .ThenBy(x => x.Reason).ThenBy(x => x.Notes),
        Evidence = (dto.Evidence ?? new List<InventoryControlEvidenceRequest>())
            .Select(x => new
            {
                x.CentralDocumentVersionId,
                EvidenceReference = Normalize(x.EvidenceReference, 500)
            })
            .OrderBy(x => x.CentralDocumentVersionId).ThenBy(x => x.EvidenceReference)
    }));

    private static void EnsureMatchingCreationPayload(StockAdjustment existing, string payloadHash)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(existing.PayloadHash) ||
                !CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(existing.PayloadHash),
                    Convert.FromHexString(payloadHash)))
                throw new StockAdjustmentIdempotencyConflictException(
                    "This stock-adjustment idempotency key was already used for a different payload.");
        }
        catch (FormatException)
        {
            throw new StockAdjustmentIdempotencyConflictException(
                "This stock-adjustment idempotency key is bound to an invalid prior payload and cannot be replayed.");
        }
    }

    private static string AdjustmentIntegrityHash(StockAdjustment item) => Hash(
        $"{item.Id:N}|{item.TenantId:N}|{item.AdjustmentNumber}|{item.WarehouseId:N}|" +
        $"{EnsureUtc(item.AdjustmentDate):O}|{item.BookClassification}|{item.Reference}|{item.ReasonCode}|" +
        $"{item.Status}|{item.TotalAdjustmentValue}|{item.RequestedById:N}|{item.ApprovedById:N}|" +
        $"{item.PostedById:N}|{item.ReversedById:N}|{item.PayloadHash}");
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private void EnsureActor(Guid userId)
    {
        if (!_currentUserProvider.IsAuthenticated || userId == Guid.Empty || userId != _currentUserProvider.UserId || _currentUserProvider.TenantId == Guid.Empty)
            throw new InvalidOperationException("An authenticated tenant user is required.");
    }

    private static void EnsureRowVersion(byte[] current, string value)
    {
        byte[] supplied;
        try { supplied = Convert.FromBase64String(value); }
        catch { throw new InvalidOperationException("The row version is invalid. Reload and retry."); }
        if (!current.SequenceEqual(supplied)) throw new InvalidOperationException("The stock adjustment changed. Reload and retry.");
    }

    private static string Required(string? value, string message, int max) =>
        Normalize(value, max) ?? throw new InvalidOperationException(message);

    private static string? Normalize(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }

    private static DateTime? Utc(DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    #endregion
}

/// <summary>
/// Interface for stock adjustment service
/// </summary>
public sealed class StockAdjustmentIdempotencyConflictException(string message) : InvalidOperationException(message);

public interface IStockAdjustmentService
{
    Task<IEnumerable<StockAdjustmentDto>> GetAllAsync(string? status = null, string? reasonCode = null, DateTime? startDate = null, DateTime? endDate = null);
    Task<StockAdjustmentDetailDto?> GetByIdAsync(Guid id);
    Task<StockAdjustmentDetailDto?> GetByAdjustmentNumberAsync(string adjustmentNumber);
    Task<IEnumerable<StockAdjustmentDto>> GetPendingAsync();
    Task<OpeningStockOptionsDto> GetOpeningStockOptionsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<StockAdjustmentDetailDto> CreateAsync(CreateStockAdjustmentDto dto, Guid userId);
    Task<StockAdjustmentDetailDto> CreateOpeningStockAsync(CreateOpeningStockAdjustmentDto dto, Guid userId);
    Task<StockAdjustmentDetailDto> UpdateAsync(Guid id, UpdateStockAdjustmentDto dto, Guid userId);
    Task<bool> DeleteAsync(Guid id, Guid userId);
    Task<bool> DeleteItemAsync(Guid adjustmentId, Guid itemId, Guid userId);
    Task<StockAdjustmentDetailDto> SubmitAsync(Guid id, Guid userId, StockAdjustmentActionRequest request);
    Task<StockAdjustmentDetailDto> DecideAsync(Guid id, Guid userId, DecideStockAdjustmentRequest request);
    Task<StockAdjustmentDetailDto> PostAsync(Guid id, Guid userId, StockAdjustmentActionRequest request);
    Task<StockAdjustmentDetailDto> ReverseAsync(Guid id, Guid userId, ReverseStockAdjustmentRequest request);
    Task<StockAdjustmentDetailDto> CancelAsync(Guid id, Guid userId);
    Task<StockAdjustmentDetailDto> RetireApprovedForRecountAsync(
        Guid id,
        Guid userId,
        StockAdjustmentActionRequest request);
}
