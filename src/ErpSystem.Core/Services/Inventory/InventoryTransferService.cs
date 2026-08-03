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
/// Inventory Transfer management service
/// Handles inter-warehouse transfers with in-transit tracking
/// </summary>
public class InventoryTransferService : IInventoryTransferService
{
    private const string SpreadToItemCost = "SpreadToItemCost";
    private const string GLExpense = "GLExpense";
    private const string BasisValue = "Value";
    private const string BasisWeight = "Weight";
    private const string BasisQuantity = "Quantity";

    private readonly IInventoryTransferRepository _transferRepository;
    private readonly IInventoryTransferItemRepository _transferItemRepository;
    private readonly IInventoryItemRepository _itemRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IWarehouseLocationRepository _warehouseLocationRepository;
    private readonly IWarehouseQuantityRepository _warehouseQuantityRepository;
    private readonly IInventoryLocationRepository _inventoryLocationRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly IConsignmentSettlementService _consignmentSettlementService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IWorkflowIntegrationService _workflowIntegrationService;
    private readonly IWorkflowStatusAdapterRegistry _workflowStatusAdapterRegistry;
    private readonly IInventoryTrackingControlService _trackingControls;
    private readonly IInventoryNegativeStockControlService _negativeStockControls;
    private readonly IProcurementAccessControlService _accessControl;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly ILogger<InventoryTransferService> _logger;
    private readonly Dictionary<Guid, bool> _consignmentWarehouseFlagCache = new();

    public InventoryTransferService(
        IInventoryTransferRepository transferRepository,
        IInventoryTransferItemRepository transferItemRepository,
        IInventoryItemRepository itemRepository,
        IWarehouseRepository warehouseRepository,
        IWarehouseLocationRepository warehouseLocationRepository,
        IWarehouseQuantityRepository warehouseQuantityRepository,
        IInventoryLocationRepository inventoryLocationRepository,
        IStockMovementRepository stockMovementRepository,
        IConsignmentSettlementService consignmentSettlementService,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        IWorkflowIntegrationService workflowIntegrationService,
        IWorkflowStatusAdapterRegistry workflowStatusAdapterRegistry,
        IInventoryTrackingControlService trackingControls,
        IInventoryNegativeStockControlService negativeStockControls,
        IProcurementAccessControlService accessControl,
        IProcurementControlEventService controlEvents,
        ILogger<InventoryTransferService> logger)
    {
        _transferRepository = transferRepository;
        _transferItemRepository = transferItemRepository;
        _itemRepository = itemRepository;
        _warehouseRepository = warehouseRepository;
        _warehouseLocationRepository = warehouseLocationRepository;
        _warehouseQuantityRepository = warehouseQuantityRepository;
        _inventoryLocationRepository = inventoryLocationRepository;
        _stockMovementRepository = stockMovementRepository;
        _consignmentSettlementService = consignmentSettlementService;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _workflowIntegrationService = workflowIntegrationService;
        _workflowStatusAdapterRegistry = workflowStatusAdapterRegistry;
        _trackingControls = trackingControls;
        _negativeStockControls = negativeStockControls;
        _accessControl = accessControl;
        _controlEvents = controlEvents;
        _logger = logger;
    }

    public async Task<IEnumerable<InventoryTransferDto>> GetAllAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var transfers = await _transferRepository.GetByDateRangeAsync(
            fromDate ?? DateTime.UtcNow.AddMonths(-3),
            toDate ?? DateTime.UtcNow);
        return (await FilterReadableAsync(transfers)).Select(MapToDto);
    }

    public async Task<IEnumerable<InventoryTransferDto>> GetByWarehouseAsync(Guid warehouseId, bool isSource = true)
    {
        var transfers = isSource
            ? await _transferRepository.GetBySourceWarehouseAsync(warehouseId)
            : await _transferRepository.GetByDestinationWarehouseAsync(warehouseId);
        return (await FilterReadableAsync(transfers)).Select(MapToDto);
    }

    public async Task<IEnumerable<InventoryTransferDto>> GetInTransitAsync()
    {
        var transfers = await _transferRepository.GetInTransitAsync();
        return (await FilterReadableAsync(transfers)).Select(MapToDto);
    }

    public async Task<IEnumerable<InventoryTransferDto>> GetPendingApprovalAsync()
    {
        var transfers = await _transferRepository.GetPendingApprovalAsync();
        return (await FilterReadableAsync(transfers)).Select(MapToDto);
    }

    public async Task<InventoryTransferDetailDto?> GetByIdAsync(Guid id)
    {
        var transfer = await _transferRepository.GetWithItemsAsync(id);
        return transfer != null && await CanReadAsync(transfer)
            ? await MapToControlledDetailDtoAsync(transfer)
            : null;
    }

    public async Task<InventoryTransferDetailDto?> GetByTransferNumberAsync(string transferNumber)
    {
        var transfer = await _transferRepository.GetByTransferNumberAsync(transferNumber);
        if (transfer == null) return null;
        var fullTransfer = await _transferRepository.GetWithItemsAsync(transfer.Id);
        return fullTransfer != null && await CanReadAsync(fullTransfer)
            ? await MapToControlledDetailDtoAsync(fullTransfer)
            : null;
    }

    public async Task<InventoryTransferDto> CreateAsync(CreateInventoryTransferDto dto, Guid userId)
    {
        var sourceWarehouse = await _warehouseRepository.GetByIdAsync(dto.SourceWarehouseId)
            ?? throw new ArgumentException($"Source warehouse {dto.SourceWarehouseId} not found");
        var destWarehouse = await _warehouseRepository.GetByIdAsync(dto.DestinationWarehouseId)
            ?? throw new ArgumentException($"Destination warehouse {dto.DestinationWarehouseId} not found");

        await EnsureWarehouseLocationsAsync(
            "procurement.inventory.transfer",
            dto.SourceWarehouseId,
            dto.Items.Select(item => item.SourceLocationId),
            "create-source");
        await EnsureWarehouseLocationsAsync(
            "procurement.inventory.transfer",
            dto.DestinationWarehouseId,
            dto.Items.Select(item => item.DestinationLocationId),
            "create-destination");

        // Allow same-warehouse transfers only when they're effectively inter-bin transfers (locations drive the move).
        // We don't enforce item-level locations here when the UI creates an empty transfer first and adds items later.
        if (dto.SourceWarehouseId == dto.DestinationWarehouseId && dto.Items.Any())
        {
            foreach (var item in dto.Items)
            {
                EnsureInterBinLocations(item.SourceLocationId, item.DestinationLocationId);
            }
        }

        var transfer = new InventoryTransfer
        {
            TransferNumber = await GenerateTransferNumberAsync(),
            SourceWarehouseId = dto.SourceWarehouseId,
            DestinationWarehouseId = dto.DestinationWarehouseId,
            Description = dto.Reason,
            Status = TransferStatus.Draft,
            RequestDate = DateTime.UtcNow,
            RequiredDate = dto.RequiredDate,
            Notes = dto.Notes,
            RequestedById = userId,
            TenantId = _currentUserProvider.TenantId
        };

        await _transferRepository.AddAsync(transfer);

        foreach (var itemDto in dto.Items)
        {
            var item = await _itemRepository.GetByIdAsync(itemDto.InventoryItemId)
                ?? throw new ArgumentException($"Inventory item {itemDto.InventoryItemId} not found");

            // Check source warehouse has sufficient stock
            var sourceQty = await _warehouseQuantityRepository.GetByWarehouseAndItemAsync(dto.SourceWarehouseId, itemDto.InventoryItemId);
            if (sourceQty == null || sourceQty.AvailableStock < itemDto.RequestedQuantity)
                throw new InvalidOperationException($"Insufficient stock for {item.ItemCode} in source warehouse");

            // Use AverageCost if available, otherwise fall back to StandardCost or LastPurchaseCost
            var itemUnitCost = item.AverageCost > 0 ? item.AverageCost
                : (item.StandardCost > 0 ? item.StandardCost : item.LastPurchaseCost);

            var transferItem = new InventoryTransferItem
            {
                InventoryTransferId = transfer.Id,
                InventoryItemId = itemDto.InventoryItemId,
                RequestedQuantity = itemDto.RequestedQuantity,
                ShippedQuantity = 0,
                ReceivedQuantity = 0,
                UnitOfMeasure = item.UnitOfMeasure,
                UnitCost = itemUnitCost,
                SourceLocationId = itemDto.SourceLocationId,
                DestinationLocationId = itemDto.DestinationLocationId,
                LotNumber = itemDto.LotNumber,
                BatchNumber = itemDto.BatchNumber,
                SerialNumber = itemDto.SerialNumber,
                ManufactureDate = itemDto.ManufactureDate,
                ExpiryDate = itemDto.ExpiryDate,
                InventoryTrackingExceptionId = itemDto.InventoryTrackingExceptionId,
                Notes = itemDto.Notes,
                TenantId = _currentUserProvider.TenantId
            };

            await _transferItemRepository.AddAsync(transferItem);
        }

        transfer.TotalItems = dto.Items.Count;
        transfer.TotalQuantity = dto.Items.Sum(i => i.RequestedQuantity);
        // No need to call UpdateAsync - entity is already tracked and changes will be persisted on SaveChanges
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Created transfer {TransferNumber} from {Source} to {Dest}",
            transfer.TransferNumber, sourceWarehouse.Name, destWarehouse.Name);
        return MapToDto(transfer);
    }

    public async Task<InventoryTransferDto> UpdateAsync(Guid transferId, UpdateInventoryTransferDto dto, Guid userId)
    {
        var transfer = await _transferRepository.GetWithItemsAsync(transferId)
            ?? throw new ArgumentException($"Transfer {transferId} not found");

        if (transfer.Status != TransferStatus.Draft)
            throw new InvalidOperationException("Transfer can only be edited in Draft status");

        // Validate warehouse changes if provided
        if (dto.SourceWarehouseId.HasValue && dto.SourceWarehouseId != transfer.SourceWarehouseId)
        {
            _ = await _warehouseRepository.GetByIdAsync(dto.SourceWarehouseId.Value)
                ?? throw new ArgumentException($"Source warehouse {dto.SourceWarehouseId} not found");
            transfer.SourceWarehouseId = dto.SourceWarehouseId.Value;
        }

        if (dto.DestinationWarehouseId.HasValue && dto.DestinationWarehouseId != transfer.DestinationWarehouseId)
        {
            _ = await _warehouseRepository.GetByIdAsync(dto.DestinationWarehouseId.Value)
                ?? throw new ArgumentException($"Destination warehouse {dto.DestinationWarehouseId} not found");
            transfer.DestinationWarehouseId = dto.DestinationWarehouseId.Value;
        }

        // If this becomes a same-warehouse transfer, it behaves like an inter-bin transfer.
        // Ensure any existing items already have valid bin selections.
        if (transfer.SourceWarehouseId == transfer.DestinationWarehouseId)
        {
            var items = await _transferItemRepository.GetByTransferAsync(transfer.Id);
            foreach (var item in items)
            {
                EnsureInterBinLocations(item.SourceLocationId, item.DestinationLocationId);
            }
        }

        await EnsureTransferAccessAsync(transfer, TransferAccessDirection.Both, "update");

        // Update other fields
        if (dto.RequiredDate.HasValue)
            transfer.RequiredDate = dto.RequiredDate;

        if (dto.Reason != null)
            transfer.Description = dto.Reason;

        if (dto.Notes != null)
            transfer.Notes = dto.Notes;

        await _transferRepository.UpdateAsync(transfer);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Updated transfer {TransferNumber}", transfer.TransferNumber);
        return MapToDto(transfer);
    }

    public async Task<bool> SubmitForApprovalAsync(Guid transferId, Guid userId)
    {
        if (!_unitOfWork.HasActiveTransaction)
            return await ExecuteControlledMutationAsync(() => SubmitForApprovalAsync(transferId, userId));

        var ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction) await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
        var transfer = await _transferRepository.GetWithItemsAsync(transferId)
            ?? throw new ArgumentException($"Transfer {transferId} not found");

        await EnsureTransferAccessAsync(transfer, TransferAccessDirection.Both, "submit");

        if (transfer.Status != TransferStatus.Draft)
            throw new InvalidOperationException("Transfer must be in Draft status");

        // Start unified workflow first. If no active workflow is configured, this will throw and we will keep
        // the transfer in Draft (so the UI can correct the configuration).
        var workflowResult = await _workflowIntegrationService.SubmitAsync("InventoryTransfer", transferId);
        if (!workflowResult.ExecutionResult.Success)
        {
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to start workflow");
        }

        var statusAdapter = _workflowStatusAdapterRegistry.GetAdapter("InventoryTransfer");
        statusAdapter.ApplySubmitOutcome(transfer, workflowResult.Outcome, userId);
        if (transfer.Status != TransferStatus.Submitted)
            throw new InvalidOperationException("Inventory transfers require an independently assigned approval step; immediate approval or rejection on submission is not permitted.");
        transfer.UpdatedAt = DateTime.UtcNow;
        var payloadHash = Hash(JsonSerializer.Serialize(new { transferId, userId, transfer.TotalQuantity, transfer.TotalValue }));
        var correlationId = $"transfer-submit:{transfer.Id:N}:{Guid.NewGuid():N}";
        var action = await AddActionAsync(transfer, InventoryTransferActionType.Submitted, userId,
            $"workflow-submit:{transfer.Id:N}:{Guid.NewGuid():N}", payloadHash, correlationId, null, []);
        await _transferRepository.UpdateAsync(transfer);
        await AddAuditAsync("Submit", transfer, new { action.Id, action.Sequence, payloadHash });
        await RecordControlEventAsync(transfer, "Submit", correlationId, action.Id, payloadHash);
        await _unitOfWork.SaveChangesAsync();
        if (ownsTransaction) await _unitOfWork.CommitAsync();

        _logger.LogInformation("Transfer {TransferNumber} submitted for approval", transfer.TransferNumber);
        return true;
        }
        catch
        {
            if (ownsTransaction && _unitOfWork.HasActiveTransaction) await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> ApproveAsync(Guid transferId, Guid userId, string? comments = null)
    {
        if (!_unitOfWork.HasActiveTransaction)
            return await ExecuteControlledMutationAsync(() => ApproveAsync(transferId, userId, comments));

        var ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction) await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
        var transfer = await _transferRepository.GetWithItemsAsync(transferId)
            ?? throw new ArgumentException($"Transfer {transferId} not found");

        await EnsureTransferAccessAsync(transfer, TransferAccessDirection.Both, "approve");

        if (transfer.Status != TransferStatus.Submitted)
            throw new InvalidOperationException("Transfer must be in Submitted status");
        if (transfer.RequestedById == userId)
            throw new UnauthorizedAccessException("The transfer requester cannot approve the same transfer.");

        var canApprove = await _workflowIntegrationService.CanUserApproveAsync("InventoryTransfer", transferId, userId);
        if (!canApprove)
        {
            throw new UnauthorizedAccessException("You are not assigned as an approver for the current workflow step");
        }

        var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(
            "InventoryTransfer",
            transferId,
            userId,
            "Approve",
            comments);

        if (!workflowResult.ExecutionResult.Success)
        {
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to process approval");
        }

        var statusAdapter = _workflowStatusAdapterRegistry.GetAdapter("InventoryTransfer");
        statusAdapter.ApplyApprovalOutcome(transfer, workflowResult.Outcome, userId);
        transfer.UpdatedAt = DateTime.UtcNow;
        if (transfer.Status == TransferStatus.Approved)
        {
            var payloadHash = Hash(JsonSerializer.Serialize(new { transferId, userId, comments = Normalize(comments, 1000) }));
            var correlationId = $"transfer-approval:{transfer.Id:N}:{Guid.NewGuid():N}";
            var action = await AddActionAsync(transfer, InventoryTransferActionType.Approved, userId,
                $"workflow-approve:{transfer.Id:N}:{userId:N}", payloadHash, correlationId, comments, []);
            await AddAuditAsync("Approve", transfer, new { action.Id, action.Sequence, payloadHash });
            await RecordControlEventAsync(transfer, "Approve", correlationId, action.Id, payloadHash);
        }
        await _transferRepository.UpdateAsync(transfer);
        await _unitOfWork.SaveChangesAsync();
        if (ownsTransaction) await _unitOfWork.CommitAsync();

        _logger.LogInformation("Transfer {TransferNumber} approved", transfer.TransferNumber);
        return true;
        }
        catch
        {
            if (ownsTransaction && _unitOfWork.HasActiveTransaction) await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> RejectAsync(Guid transferId, string reason, Guid userId, string? comments = null)
    {
        if (!_unitOfWork.HasActiveTransaction)
            return await ExecuteControlledMutationAsync(() => RejectAsync(transferId, reason, userId, comments));

        var ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction) await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
        var transfer = await _transferRepository.GetWithItemsAsync(transferId)
            ?? throw new ArgumentException($"Transfer {transferId} not found");

        await EnsureTransferAccessAsync(transfer, TransferAccessDirection.Both, "reject");
        if (transfer.RequestedById == userId)
            throw new UnauthorizedAccessException("The transfer requester cannot decide the same transfer.");

        var canApprove = await _workflowIntegrationService.CanUserApproveAsync("InventoryTransfer", transferId, userId);
        if (!canApprove)
        {
            throw new UnauthorizedAccessException("You are not assigned as an approver for the current workflow step");
        }

        var rejectionText = !string.IsNullOrWhiteSpace(comments) ? comments : reason;
        var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(
            "InventoryTransfer",
            transferId,
            userId,
            "Reject",
            rejectionText);

        if (!workflowResult.ExecutionResult.Success)
        {
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to process rejection");
        }

        var statusAdapter = _workflowStatusAdapterRegistry.GetAdapter("InventoryTransfer");
        statusAdapter.ApplyApprovalOutcome(transfer, workflowResult.Outcome, userId, rejectionText);
        transfer.UpdatedAt = DateTime.UtcNow;
        if (transfer.Status == TransferStatus.Rejected)
        {
            var payloadHash = Hash(JsonSerializer.Serialize(new { transferId, userId, rejectionText }));
            var correlationId = $"transfer-rejection:{transfer.Id:N}:{Guid.NewGuid():N}";
            var action = await AddActionAsync(transfer, InventoryTransferActionType.Rejected, userId,
                $"workflow-reject:{transfer.Id:N}:{userId:N}", payloadHash, correlationId, rejectionText, []);
            await AddAuditAsync("Reject", transfer, new { action.Id, action.Sequence, payloadHash });
            await RecordControlEventAsync(transfer, "Reject", correlationId, action.Id, payloadHash);
        }
        await _transferRepository.UpdateAsync(transfer);
        await _unitOfWork.SaveChangesAsync();
        if (ownsTransaction) await _unitOfWork.CommitAsync();

        _logger.LogInformation("Transfer {TransferNumber} rejected: {Reason}", transfer.TransferNumber, rejectionText);
        return true;
        }
        catch
        {
            if (ownsTransaction && _unitOfWork.HasActiveTransaction) await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> ShipAsync(Guid transferId, Guid userId, string? trackingNumber = null, Dictionary<Guid, decimal>? shippedItems = null, InventoryTransferMutationContext? control = null)
    {
        if (!_unitOfWork.HasActiveTransaction)
            return await ExecuteControlledMutationAsync(() => ShipAsync(transferId, userId, trackingNumber, shippedItems, control));

        var ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction) await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
        var transfer = await _transferRepository.GetWithItemsAsync(transferId)
            ?? throw new ArgumentException($"Transfer {transferId} not found");

        await EnsureTransferAccessAsync(transfer, TransferAccessDirection.Source, "dispatch");

        var payloadHash = DispatchPayloadHash(
            transferId,
            trackingNumber,
            shippedItems,
            control?.NegativeStockOverrideIds,
            control?.PayloadSalt);
        var key = MutationKey(control, $"internal-dispatch:{transfer.Id:N}:{payloadHash}");
        if (await IsActionReplayAsync(transfer.Id, InventoryTransferActionType.Dispatched, key, payloadHash))
        {
            if (ownsTransaction) await _unitOfWork.CommitAsync();
            return true;
        }

        if (transfer.Status != TransferStatus.Approved && transfer.Status != TransferStatus.InTransit)
            throw new InvalidOperationException("Transfer must be approved or in transit to ship items");
        if (userId == transfer.RequestedById || userId == transfer.ApprovedById)
            throw new UnauthorizedAccessException("The requester or transfer approver cannot dispatch the same transfer.");
        if (control is not null) EnsureRowVersion(transfer.RowVersion, control.RowVersion);
        if (shippedItems?.Keys.Any(id => transfer.Items.All(item => item.Id != id)) == true)
            throw new ArgumentException("A dispatch line does not belong to this transfer.");

        var dispatchQuantities = transfer.Items.ToDictionary(
            item => item.Id,
            item => shippedItems is null
                ? item.RequestedQuantity - item.ShippedQuantity
                : shippedItems.GetValueOrDefault(item.Id));
        dispatchQuantities = dispatchQuantities.Where(value => value.Value > 0).ToDictionary();
        if (dispatchQuantities.Count == 0)
            throw new InvalidOperationException("At least one positive dispatch quantity is required.");
        foreach (var value in dispatchQuantities)
        {
            var item = transfer.Items.Single(candidate => candidate.Id == value.Key);
            var remaining = item.RequestedQuantity - item.ShippedQuantity;
            if (value.Value > remaining)
                throw new InvalidOperationException($"Cannot dispatch {value.Value}; only {remaining} remains for item {item.InventoryItemId}.");
        }

        var correlationId = Normalize(control?.CorrelationId, 100) ?? $"transfer-dispatch:{transfer.Id:N}:{Guid.NewGuid():N}";
        var action = await AddActionAsync(
            transfer,
            InventoryTransferActionType.Dispatched,
            userId,
            key,
            payloadHash,
            correlationId,
            control?.Comment,
            dispatchQuantities.Select(value => new TransferActionLineInput(value.Key, value.Value, 0, 0, 0)).ToList());
        await _unitOfWork.SaveChangesAsync();
        shippedItems = dispatchQuantities;

        // Deduct from source warehouse and mark as in-transit
        foreach (var item in transfer.Items)
        {
            // Same-warehouse transfers behave like inter-bin transfers: require valid bin selections.
            if (transfer.SourceWarehouseId == transfer.DestinationWarehouseId)
            {
                EnsureInterBinLocations(item.SourceLocationId, item.DestinationLocationId);
            }

            // Get shipped quantity for this item (from request, or remaining requested qty for full shipment)
            decimal quantityToShip;
            if (shippedItems != null)
            {
                if (!shippedItems.TryGetValue(item.Id, out var requestedShipQty))
                    continue;
                quantityToShip = requestedShipQty;
            }
            else
            {
                // If no specific quantity provided, ship the remaining requested amount
                quantityToShip = item.RequestedQuantity - item.ShippedQuantity;
            }

            // Skip if nothing to ship for this item
            if (quantityToShip <= 0)
                continue;

            // Validate quantity doesn't exceed what's remaining to ship
            var remainingToShip = item.RequestedQuantity - item.ShippedQuantity;
            if (quantityToShip > remainingToShip)
                throw new InvalidOperationException($"Cannot ship {quantityToShip} - only {remainingToShip} remaining for item {item.InventoryItemId}");

            WarehouseLocation? sourceLocation = null;
            if (item.SourceLocationId.HasValue && item.SourceLocationId.Value != Guid.Empty)
            {
                sourceLocation = await EnsureLocationBelongsToWarehouseAsync(item.SourceLocationId.Value, transfer.SourceWarehouseId, "SourceLocationId");
            }

            WarehouseLocation? destinationLocation = null;
            if (item.DestinationLocationId.HasValue && item.DestinationLocationId.Value != Guid.Empty)
            {
                destinationLocation = await EnsureLocationBelongsToWarehouseAsync(item.DestinationLocationId.Value, transfer.DestinationWarehouseId, "DestinationLocationId");
            }

            var effectiveSourceWarehouseId = sourceLocation?.InventoryWarehouseId ?? transfer.SourceWarehouseId;
            var effectiveDestinationWarehouseId = destinationLocation?.InventoryWarehouseId ?? transfer.DestinationWarehouseId;

            var sourceIsConsignment = await IsConsignmentWarehouseAsync(effectiveSourceWarehouseId);
            var destIsConsignment = await IsConsignmentWarehouseAsync(effectiveDestinationWarehouseId);
            if (sourceIsConsignment != destIsConsignment)
            {
                throw new InvalidOperationException("Transfers between owned and consignment inventory are not supported. Use a dedicated settlement/ownership conversion process.");
            }

            var decreaseAuthorization = await _negativeStockControls.PrepareDecreaseAsync(new InventoryStockDecreaseRequest
            {
                InventoryItemId = item.InventoryItemId,
                WarehouseId = effectiveSourceWarehouseId,
                LocationId = item.SourceLocationId,
                Quantity = quantityToShip,
                ReferenceType = "InventoryTransfer",
                ReferenceNumber = transfer.TransferNumber,
                ReferenceId = transfer.Id,
                ReferenceLineId = item.Id,
                NegativeStockOverrideId = control?.NegativeStockOverrideIds.GetValueOrDefault(item.Id),
                CheckInventoryItemBalance = false,
                CorrelationId = correlationId
            });
            var sourceQty = await _warehouseQuantityRepository.GetByWarehouseAndItemAsync(effectiveSourceWarehouseId, item.InventoryItemId);
            if (sourceQty == null ||
                (sourceQty.AvailableStock < quantityToShip && !decreaseAuthorization.EmergencyOverrideApplied))
                throw new InvalidOperationException($"Insufficient stock for item {item.InventoryItemId}");

            var trackingSequence = item.TrackingSequence + 1;
            await _trackingControls.StageEventAsync(new InventoryTrackingMutationRequest
            {
                InventoryItemId = item.InventoryItemId,
                WarehouseId = effectiveSourceWarehouseId,
                LocationId = item.SourceLocationId,
                Direction = InventoryTrackingDirection.TransferOut,
                Quantity = quantityToShip,
                ReferenceType = "InventoryTransfer",
                ReferenceNumber = transfer.TransferNumber,
                ReferenceId = transfer.Id,
                ReferenceLineId = item.Id,
                EventKey = $"transfer:{transfer.Id:N}:{item.Id:N}:{trackingSequence}:out",
                LotNumber = item.LotNumber,
                BatchNumber = item.BatchNumber,
                SerialNumber = item.SerialNumber,
                ManufactureDate = item.ManufactureDate,
                ExpiryDate = item.ExpiryDate,
                TrackingExceptionId = item.InventoryTrackingExceptionId,
                CorrelationId = correlationId
            });

            // Bin-level tracking: if a source location is specified, it must have sufficient stock.
            // This is required for inter-bin transfers and optional for inter-warehouse transfers.
            if (sourceLocation != null)
            {
                await AdjustInventoryLocationQuantityAsync(item.SourceLocationId.Value, item.InventoryItemId, -quantityToShip);
            }
            else if (transfer.SourceWarehouseId == transfer.DestinationWarehouseId)
            {
                throw new InvalidOperationException("SourceLocationId is required for same-warehouse (inter-bin) transfers.");
            }

            sourceQty.CurrentStock -= quantityToShip;
            sourceQty.AvailableStock -= quantityToShip;
            sourceQty.AllocatedStock += quantityToShip; // Track as allocated during transit
            sourceQty.LastMovementDate = DateTime.UtcNow;
            await _warehouseQuantityRepository.UpdateAsync(sourceQty);

            if (decreaseAuthorization.EmergencyOverrideApplied)
            {
                await _unitOfWork.SaveChangesAsync();
                await _negativeStockControls.ClearMutationContextAsync();
            }

            item.ShippedQuantity += quantityToShip;
            item.TrackingSequence = trackingSequence;
            await _transferItemRepository.UpdateAsync(item);

            // Create outbound movement
            var outboundMovement = new StockMovement
            {
                InventoryItemId = item.InventoryItemId,
                MovementType = "TransferOut",
                Quantity = -quantityToShip,
                UnitCost = item.UnitCost,
                TotalValue = -quantityToShip * item.UnitCost,
                ReferenceType = ReferenceType.Transfer,
                ReferenceNumber = transfer.TransferNumber,
                ReferenceId = transfer.Id,
                WarehouseId = effectiveSourceWarehouseId,
                LocationId = item.SourceLocationId,
                LotNumber = item.LotNumber,
                BatchNumber = item.BatchNumber,
                SerialNumber = item.SerialNumber,
                ManufactureDate = item.ManufactureDate,
                ExpirationDate = item.ExpiryDate,
                InventoryTrackingExceptionId = item.InventoryTrackingExceptionId,
                Notes = $"Controlled dispatch {action.Sequence} to {transfer.DestinationWarehouse?.Name}",
                ProcessedById = userId,
                RunningBalance = sourceQty.CurrentStock,
                TenantId = _currentUserProvider.TenantId
            };
            await _stockMovementRepository.AddAsync(outboundMovement);
            await _consignmentSettlementService.TryCreateFromStockMovementAsync(outboundMovement);
        }

        var allItemsFullyShipped = transfer.Items.All(i => i.ShippedQuantity >= i.RequestedQuantity);
        // A partial dispatch is physically in transit too. Keeping it Approved hid
        // deducted source stock and allowed pre-dispatch behavior after movement.
        transfer.Status = TransferStatus.InTransit;

        // Set shipped date on first shipment
        if (transfer.ShippedDate == null)
        {
            transfer.ShippedDate = DateTime.UtcNow;
            transfer.ShippedById = userId;
        }

        if (!string.IsNullOrEmpty(trackingNumber))
        {
            transfer.TrackingNumber = trackingNumber;
        }

        await _transferRepository.UpdateAsync(transfer);
        await AddAuditAsync("Dispatch", transfer, new { action.Id, action.Sequence, payloadHash, allItemsFullyShipped });
        await RecordControlEventAsync(transfer, "Dispatch", correlationId, action.Id, payloadHash);
        await _unitOfWork.SaveChangesAsync();
        if (ownsTransaction) await _unitOfWork.CommitAsync();

        _logger.LogInformation("Transfer {TransferNumber} shipped (fully: {FullyShipped})", transfer.TransferNumber, allItemsFullyShipped);
        return true;
        }
        catch
        {
            if (ownsTransaction && _unitOfWork.HasActiveTransaction) await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> ShipWithCostsAsync(Guid transferId, Guid userId, ShipTransferWithCostsDto costsDto)
    {
        if (!_unitOfWork.HasActiveTransaction)
            return await ExecuteControlledMutationAsync(() => ShipWithCostsAsync(transferId, userId, costsDto));

        var ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction) await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
        var transfer = await _transferRepository.GetWithItemsAsync(transferId)
            ?? throw new ArgumentException($"Transfer {transferId} not found");

        await EnsureTransferAccessAsync(transfer, TransferAccessDirection.Source, "ship-with-costs");

        if (transfer.Status != TransferStatus.Approved && transfer.Status != TransferStatus.InTransit)
            throw new InvalidOperationException("Transfer must be approved or in transit to ship items");

        // Validate cost allocation method
        if (costsDto.CostAllocationMethod != SpreadToItemCost && costsDto.CostAllocationMethod != GLExpense)
            throw new ArgumentException("CostAllocationMethod must be either 'SpreadToItemCost' or 'GLExpense'");

        if (costsDto.CostAllocationMethod == GLExpense && string.IsNullOrWhiteSpace(costsDto.ExpenseGLAccount))
            throw new ArgumentException("ExpenseGLAccount is required when CostAllocationMethod is GLExpense");

        if (costsDto.CostApportionmentBasis != BasisValue &&
            costsDto.CostApportionmentBasis != BasisWeight &&
            costsDto.CostApportionmentBasis != BasisQuantity)
            throw new ArgumentException("CostApportionmentBasis must be 'Value', 'Weight', or 'Quantity'");

        // Handle shipped quantities (if provided)
        Dictionary<Guid, decimal>? shippedItems = null;
        if (costsDto.Items != null && costsDto.Items.Any())
        {
            shippedItems = costsDto.Items.ToDictionary(i => i.ItemId, i => i.ShippedQuantity);
        }

        var costPayloadSalt = Hash(JsonSerializer.Serialize(new
        {
            costsDto.ShippingCost,
            costsDto.MiscellaneousCost,
            costsDto.MiscellaneousCostDescription,
            costsDto.CostAllocationMethod,
            costsDto.CostApportionmentBasis,
            costsDto.ExpenseGLAccount,
            costsDto.CarrierName,
            costsDto.Comment
        }));
        var dispatchPayloadHash = DispatchPayloadHash(transferId, costsDto.TrackingNumber, shippedItems, null, costPayloadSalt);
        var dispatchKey = MutationKey(new InventoryTransferMutationContext { IdempotencyKey = costsDto.IdempotencyKey }, string.Empty);
        if (await IsActionReplayAsync(transfer.Id, InventoryTransferActionType.Dispatched, dispatchKey, dispatchPayloadHash))
        {
            if (ownsTransaction) await _unitOfWork.CommitAsync();
            return true;
        }

        // These cost fields and the stock dispatch commit as one serializable unit.
        var totalAdditionalCost = costsDto.ShippingCost + costsDto.MiscellaneousCost;
        transfer.ShippingCost = costsDto.ShippingCost;
        transfer.MiscellaneousCost = costsDto.MiscellaneousCost;
        transfer.MiscellaneousCostDescription = costsDto.MiscellaneousCostDescription;
        transfer.TotalAdditionalCost = totalAdditionalCost;
        transfer.CostAllocationMethod = costsDto.CostAllocationMethod;
        transfer.CostApportionmentBasis = costsDto.CostApportionmentBasis;
        transfer.ExpenseGLAccount = costsDto.ExpenseGLAccount;
        transfer.CarrierName = Normalize(costsDto.CarrierName, 100);

        // Perform the shipment
        var shipmentResult = await ShipAsync(transferId, userId, costsDto.TrackingNumber, shippedItems, new InventoryTransferMutationContext
        {
            RowVersion = costsDto.RowVersion,
            IdempotencyKey = costsDto.IdempotencyKey,
            CorrelationId = costsDto.CorrelationId,
            Comment = Normalize(costsDto.Comment, 1000) ?? "Dispatch with governed transfer costs.",
            PayloadSalt = costPayloadSalt
        });
        if (!shipmentResult)
            return false;

        // Reload transfer to get updated data
        transfer = await _transferRepository.GetWithItemsAsync(transferId);
        if (transfer == null)
            return false;

        // Allocate costs based on the selected method
        if (costsDto.CostAllocationMethod == SpreadToItemCost)
        {
            await AllocateCostsToItemsAsync(transfer, totalAdditionalCost, costsDto.CostApportionmentBasis);
        }
        else if (costsDto.CostAllocationMethod == GLExpense)
        {
            await PostCostsToGLAsync(transfer, totalAdditionalCost, costsDto.ExpenseGLAccount!, userId);
        }

        // Mark costs as allocated
        transfer.CostsAllocated = true;
        await _transferRepository.UpdateAsync(transfer);
        await _unitOfWork.SaveChangesAsync();
        if (ownsTransaction) await _unitOfWork.CommitAsync();

        _logger.LogInformation("Transfer {TransferNumber} shipped with costs. Method: {Method}, Total Cost: {Cost}",
            transfer.TransferNumber, costsDto.CostAllocationMethod, totalAdditionalCost);

        return true;
    }

        catch
        {
            if (ownsTransaction && _unitOfWork.HasActiveTransaction) await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> SaveShippingCostsAsync(Guid transferId, Guid userId, ShipTransferWithCostsDto costsDto)
    {
        if (!_unitOfWork.HasActiveTransaction)
            return await ExecuteControlledMutationAsync(() => SaveShippingCostsAsync(transferId, userId, costsDto));

        var ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction) await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
        var transfer = await _transferRepository.GetWithItemsAsync(transferId)
            ?? throw new ArgumentException($"Transfer {transferId} not found");

        await EnsureTransferAccessAsync(transfer, TransferAccessDirection.Source, "save-shipping-costs");

        // Validate cost allocation method
        if (!string.IsNullOrEmpty(costsDto.CostAllocationMethod) && 
            costsDto.CostAllocationMethod != SpreadToItemCost && costsDto.CostAllocationMethod != GLExpense)
            throw new ArgumentException("CostAllocationMethod must be either 'SpreadToItemCost' or 'GLExpense'");

        if (costsDto.CostAllocationMethod == GLExpense && string.IsNullOrWhiteSpace(costsDto.ExpenseGLAccount))
            throw new ArgumentException("ExpenseGLAccount is required when CostAllocationMethod is GLExpense");

        if (!string.IsNullOrEmpty(costsDto.CostApportionmentBasis) &&
            costsDto.CostApportionmentBasis != BasisValue &&
            costsDto.CostApportionmentBasis != BasisWeight &&
            costsDto.CostApportionmentBasis != BasisQuantity)
            throw new ArgumentException("CostApportionmentBasis must be 'Value', 'Weight', or 'Quantity'");

        var payloadHash = Hash(JsonSerializer.Serialize(new
        {
            transferId,
            costsDto.TrackingNumber,
            costsDto.CarrierName,
            costsDto.ShippingCost,
            costsDto.MiscellaneousCost,
            costsDto.MiscellaneousCostDescription,
            costsDto.CostAllocationMethod,
            costsDto.CostApportionmentBasis,
            costsDto.ExpenseGLAccount,
            costsDto.Comment
        }));
        var key = MutationKey(new InventoryTransferMutationContext { IdempotencyKey = costsDto.IdempotencyKey }, string.Empty);
        if (await IsActionReplayAsync(transfer.Id, InventoryTransferActionType.ShippingCostsSaved, key, payloadHash))
        {
            if (ownsTransaction) await _unitOfWork.CommitAsync();
            return true;
        }
        if (transfer.Status != TransferStatus.Approved || transfer.Items.Any(item => item.ShippedQuantity > 0))
            throw new InvalidOperationException("Shipping costs can only be prepared before the first controlled dispatch.");
        if (userId == transfer.RequestedById || userId == transfer.ApprovedById)
            throw new UnauthorizedAccessException("The requester or transfer approver cannot prepare dispatch costs for the same transfer.");
        EnsureRowVersion(transfer.RowVersion, costsDto.RowVersion);

        var totalAdditionalCost = costsDto.ShippingCost + costsDto.MiscellaneousCost;

        var correlationId = Normalize(costsDto.CorrelationId, 100) ?? $"transfer-shipping-costs:{transfer.Id:N}:{Guid.NewGuid():N}";
        var action = await AddActionAsync(
            transfer,
            InventoryTransferActionType.ShippingCostsSaved,
            userId,
            key,
            payloadHash,
            correlationId,
            Normalize(costsDto.Comment, 1000) ?? "Prepared controlled transfer shipping costs.",
            []);
        await _unitOfWork.SaveChangesAsync();

        // Store shipping costs on transfer (without shipping)
        transfer.ShippingCost = costsDto.ShippingCost;
        transfer.MiscellaneousCost = costsDto.MiscellaneousCost;
        transfer.MiscellaneousCostDescription = costsDto.MiscellaneousCostDescription;
        transfer.TotalAdditionalCost = totalAdditionalCost;
        transfer.CostAllocationMethod = costsDto.CostAllocationMethod ?? SpreadToItemCost;
        transfer.CostApportionmentBasis = costsDto.CostApportionmentBasis ?? BasisValue;
        transfer.ExpenseGLAccount = costsDto.ExpenseGLAccount;
        
        // Update tracking info if provided
        if (!string.IsNullOrEmpty(costsDto.TrackingNumber))
            transfer.TrackingNumber = costsDto.TrackingNumber;
        
        if (!string.IsNullOrEmpty(costsDto.CarrierName))
            transfer.CarrierName = costsDto.CarrierName;

        transfer.CostsAllocated = false;
        await _transferRepository.UpdateAsync(transfer);
        await AddAuditAsync("SaveShippingCosts", transfer, new { action.Id, action.Sequence, payloadHash, totalAdditionalCost });
        await RecordControlEventAsync(transfer, "SaveShippingCosts", correlationId, action.Id, payloadHash);
        await _unitOfWork.SaveChangesAsync();
        if (ownsTransaction) await _unitOfWork.CommitAsync();

        _logger.LogInformation("Saved shipping costs for transfer {TransferNumber}. Method: {Method}, Total Cost: {Cost}",
            transfer.TransferNumber, costsDto.CostAllocationMethod, totalAdditionalCost);

        return true;
        }
        catch
        {
            if (ownsTransaction && _unitOfWork.HasActiveTransaction) await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    private async Task AllocateCostsToItemsAsync(InventoryTransfer transfer, decimal totalAdditionalCost, string apportionmentBasis)
    {
        if (totalAdditionalCost <= 0)
            return;

        var shippedItems = transfer.Items.Where(i => i.ShippedQuantity > 0).ToList();
        if (!shippedItems.Any())
        {
            _logger.LogWarning("Cannot allocate costs - no shipped items for transfer {TransferNumber}", transfer.TransferNumber);
            return;
        }

        var basisByItem = shippedItems.ToDictionary(
            i => i.Id,
            i => apportionmentBasis switch
            {
                BasisQuantity => i.ShippedQuantity,
                BasisWeight => i.ShippedQuantity, // Weight placeholder uses quantity until transfer-line weight is captured
                _ => i.ShippedQuantity * i.UnitCost
            });

        var totalBasis = basisByItem.Values.Sum();
        if (totalBasis <= 0)
        {
            // Fallback to value basis if selected basis is not available.
            basisByItem = shippedItems.ToDictionary(i => i.Id, i => i.ShippedQuantity * i.UnitCost);
            totalBasis = basisByItem.Values.Sum();
        }

        if (totalBasis <= 0)
        {
            // Last fallback to quantity.
            basisByItem = shippedItems.ToDictionary(i => i.Id, i => i.ShippedQuantity);
            totalBasis = basisByItem.Values.Sum();
        }

        if (totalBasis <= 0)
        {
            _logger.LogWarning("Cannot allocate costs - basis total is zero for transfer {TransferNumber}", transfer.TransferNumber);
            return;
        }

        decimal allocatedRunningTotal = 0;
        for (var index = 0; index < shippedItems.Count; index++)
        {
            var item = shippedItems[index];
            decimal allocatedCost;

            if (index == shippedItems.Count - 1)
            {
                allocatedCost = totalAdditionalCost - allocatedRunningTotal;
            }
            else
            {
                allocatedCost = Math.Round((basisByItem[item.Id] / totalBasis) * totalAdditionalCost, 2, MidpointRounding.AwayFromZero);
                allocatedRunningTotal += allocatedCost;
            }

            item.AllocatedCostPerUnit = item.ShippedQuantity > 0
                ? Math.Round(allocatedCost / item.ShippedQuantity, 4, MidpointRounding.AwayFromZero)
                : 0;
            item.TotalAllocatedCost = allocatedCost;
            item.LandedUnitCost = item.UnitCost + item.AllocatedCostPerUnit;

            await _transferItemRepository.UpdateAsync(item);

            _logger.LogInformation("Allocated cost {AllocatedCost} to item {ItemId} on transfer {TransferNumber} using basis {Basis}",
                allocatedCost, item.InventoryItemId, transfer.TransferNumber, apportionmentBasis);
        }
    }

    private async Task PostCostsToGLAsync(InventoryTransfer transfer, decimal totalAdditionalCost, string glAccount, Guid userId)
    {
        // TODO: Implement GL posting logic
        // This would typically involve:
        // 1. Creating a GL journal entry
        // 2. Debiting the expense account
        // 3. Crediting inventory or transfer clearing account

        _logger.LogInformation("GL expense posting required for transfer {TransferNumber}: Account {Account}, Amount {Amount}",
            transfer.TransferNumber, glAccount, totalAdditionalCost);

        // For now, just log - actual GL integration would depend on the accounting module
        // throw new NotImplementedException("GL expense posting not yet implemented");
    }

    public async Task<bool> ReceiveAsync(
        Guid transferId,
        Guid userId,
        List<InventoryTransferItemDto>? receivedItems = null,
        InventoryTransferMutationContext? control = null)
    {
        if (!_unitOfWork.HasActiveTransaction)
            return await ExecuteControlledMutationAsync(() => ReceiveAsync(transferId, userId, receivedItems, control));

        var ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction) await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var transfer = await _transferRepository.GetWithItemsAsync(transferId)
                ?? throw new ArgumentException($"Transfer {transferId} not found");
            await EnsureTransferAccessAsync(transfer, TransferAccessDirection.Destination, "receive");

            var payloadHash = Hash(JsonSerializer.Serialize(new
            {
                transferId,
                lines = receivedItems is null
                    ? "ALL_OUTSTANDING"
                    : JsonSerializer.Serialize(receivedItems.OrderBy(value => value.Id).Select(value => new
                    {
                        value.Id,
                        value.ReceivedQuantity,
                        value.DamagedQuantity,
                        value.ShortageQuantity,
                        value.DiscrepancyReasonCode,
                        value.DiscrepancyReason,
                        evidence = value.Evidence.OrderBy(item => item.CentralDocumentVersionId)
                    }))
            }));
            var key = MutationKey(control, $"internal-receipt:{transfer.Id:N}:{payloadHash}");
            if (await IsActionReplayAsync(transfer.Id, InventoryTransferActionType.Received, key, payloadHash))
            {
                if (ownsTransaction) await _unitOfWork.CommitAsync();
                return true;
            }
            if (transfer.Status != TransferStatus.InTransit)
                throw new InvalidOperationException("Transfer must be in transit to receive items.");
            if (control is not null) EnsureRowVersion(transfer.RowVersion, control.RowVersion);
            if (userId == transfer.RequestedById || userId == transfer.ApprovedById || userId == transfer.ShippedById)
                throw new UnauthorizedAccessException("The requester, approver or dispatcher cannot receive the same transfer.");
            if (receivedItems?.Any(value => transfer.Items.All(item => item.Id != value.Id)) == true)
                throw new ArgumentException("A receipt line does not belong to this transfer.");

            var normalizedLines = receivedItems ?? transfer.Items.Select(item => new InventoryTransferItemDto
            {
                Id = item.Id,
                ReceivedQuantity = item.ShippedQuantity - item.ReceivedQuantity - item.DamagedQuantity - item.ShortageQuantity
            }).Where(item => item.ReceivedQuantity > 0).ToList();
            normalizedLines = normalizedLines.Where(value => value.ReceivedQuantity + value.DamagedQuantity + value.ShortageQuantity > 0).ToList();
            if (normalizedLines.Count == 0) throw new InvalidOperationException("At least one received, damaged or shortage quantity is required.");

            var evidenceByLine = new Dictionary<Guid, IReadOnlyList<ValidatedTransferEvidence>>();
            foreach (var line in normalizedLines)
            {
                var item = transfer.Items.Single(value => value.Id == line.Id);
                var outstanding = item.ShippedQuantity - item.ReceivedQuantity - item.DamagedQuantity - item.ShortageQuantity;
                var accounted = line.ReceivedQuantity + line.DamagedQuantity + line.ShortageQuantity;
                if (line.ReceivedQuantity < 0 || line.DamagedQuantity < 0 || line.ShortageQuantity < 0 || accounted > outstanding)
                    throw new InvalidOperationException($"Receipt quantities must be non-negative and cannot exceed outstanding in-transit quantity {outstanding} for item {item.InventoryItemId}.");
                if (line.DamagedQuantity > 0 || line.ShortageQuantity > 0)
                {
                    var reasonCode = Normalize(line.DiscrepancyReasonCode, 50)?.ToUpperInvariant();
                    if (reasonCode is null || !InventoryTransferDiscrepancyReasonCodes.All.ContainsKey(reasonCode))
                        throw new InvalidOperationException("A valid controlled discrepancy reason code is required for damage or shortage.");
                    if (string.IsNullOrWhiteSpace(line.DiscrepancyReason))
                        throw new InvalidOperationException("A detailed discrepancy reason is required for damage or shortage.");
                    evidenceByLine[line.Id] = await ValidateTransferEvidenceAsync(line.Evidence, true);
                }
            }

            var correlationId = Normalize(control?.CorrelationId, 100) ?? $"transfer-receipt:{transfer.Id:N}:{Guid.NewGuid():N}";
            var action = await AddActionAsync(
                transfer,
                InventoryTransferActionType.Received,
                userId,
                key,
                payloadHash,
                correlationId,
                control?.Comment,
                normalizedLines.Select(value => new TransferActionLineInput(value.Id, 0, value.ReceivedQuantity, value.DamagedQuantity, value.ShortageQuantity)).ToList());
            await _unitOfWork.SaveChangesAsync();

            foreach (var line in normalizedLines.Where(value => value.DamagedQuantity > 0 || value.ShortageQuantity > 0))
            {
                var discrepancy = new InventoryTransferDiscrepancy
                {
                    TenantId = transfer.TenantId,
                    InventoryTransferId = transfer.Id,
                    InventoryTransferItemId = line.Id,
                    ReceiptActionId = action.Id,
                    DamagedQuantity = line.DamagedQuantity,
                    ShortageQuantity = line.ShortageQuantity,
                    ReasonCode = line.DiscrepancyReasonCode!.Trim().ToUpperInvariant(),
                    Reason = line.DiscrepancyReason!.Trim(),
                    Status = InventoryTransferDiscrepancyStatus.Open
                };
                discrepancy.IntegrityHash = Hash($"{discrepancy.InventoryTransferId:N}|{discrepancy.InventoryTransferItemId:N}|{discrepancy.ReceiptActionId:N}|{discrepancy.DamagedQuantity}|{discrepancy.ShortageQuantity}|{discrepancy.ReasonCode}|{discrepancy.Reason}");
                await _unitOfWork.Repository<InventoryTransferDiscrepancy>().AddAsync(discrepancy);
                foreach (var evidence in evidenceByLine[line.Id])
                {
                    var link = new InventoryTransferDiscrepancyEvidence
                    {
                        TenantId = transfer.TenantId,
                        InventoryTransferDiscrepancyId = discrepancy.Id,
                        CentralDocumentVersionId = evidence.VersionId,
                        FileUploadRecordId = evidence.FileUploadRecordId,
                        EvidenceReference = evidence.Reference
                    };
                    link.IntegrityHash = Hash($"{discrepancy.Id:N}|{link.CentralDocumentVersionId:N}|{link.FileUploadRecordId:N}|{link.EvidenceReference}");
                    await _unitOfWork.Repository<InventoryTransferDiscrepancyEvidence>().AddAsync(link);
                }
            }

            var result = await ReceiveCoreAsync(transfer, userId, normalizedLines, action, correlationId);
            await AddAuditAsync("Receive", transfer, new { action.Id, action.Sequence, payloadHash });
            await RecordControlEventAsync(transfer, "Receive", correlationId, action.Id, payloadHash);
            await _unitOfWork.SaveChangesAsync();
            if (ownsTransaction) await _unitOfWork.CommitAsync();
            return result;
        }
        catch
        {
            if (ownsTransaction && _unitOfWork.HasActiveTransaction) await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    private async Task<bool> ReceiveCoreAsync(InventoryTransfer transfer, Guid userId, List<InventoryTransferItemDto> receivedItems, InventoryTransferAction action, string correlationId)
    {
        foreach (var item in transfer.Items)
        {
            // Same-warehouse transfers behave like inter-bin transfers: require valid bin selections.
            if (transfer.SourceWarehouseId == transfer.DestinationWarehouseId)
            {
                EnsureInterBinLocations(item.SourceLocationId, item.DestinationLocationId);
            }

            var receivedLine = receivedItems.FirstOrDefault(r => r.Id == item.Id);
            if (receivedLine is null) continue;
            var receivedQty = receivedLine.ReceivedQuantity;
            var damagedQty = receivedLine.DamagedQuantity;
            var shortageQty = receivedLine.ShortageQuantity;
            var accountedQty = receivedQty + damagedQty + shortageQty;
            item.DestinationLocationId = receivedLine.DestinationLocationId ?? item.DestinationLocationId;
            item.LotNumber = receivedLine.LotNumber ?? item.LotNumber;
            item.BatchNumber = receivedLine.BatchNumber ?? item.BatchNumber;
            item.SerialNumber = receivedLine.SerialNumber ?? item.SerialNumber;
            item.ManufactureDate = receivedLine.ManufactureDate ?? item.ManufactureDate;
            item.ExpiryDate = receivedLine.ExpiryDate ?? item.ExpiryDate;
            item.InventoryTrackingExceptionId = receivedLine.InventoryTrackingExceptionId ?? item.InventoryTrackingExceptionId;

            WarehouseLocation? sourceLocation = null;
            if (item.SourceLocationId.HasValue && item.SourceLocationId.Value != Guid.Empty)
            {
                sourceLocation = await EnsureLocationBelongsToWarehouseAsync(item.SourceLocationId.Value, transfer.SourceWarehouseId, "SourceLocationId");
            }

            WarehouseLocation? destinationLocation = null;
            if (item.DestinationLocationId.HasValue && item.DestinationLocationId.Value != Guid.Empty)
            {
                destinationLocation = await EnsureLocationBelongsToWarehouseAsync(item.DestinationLocationId.Value, transfer.DestinationWarehouseId, "DestinationLocationId");
            }

            var effectiveSourceWarehouseId = sourceLocation?.InventoryWarehouseId ?? transfer.SourceWarehouseId;
            var effectiveDestinationWarehouseId = destinationLocation?.InventoryWarehouseId ?? transfer.DestinationWarehouseId;

            var sourceIsConsignment = await IsConsignmentWarehouseAsync(effectiveSourceWarehouseId);
            var destIsConsignment = await IsConsignmentWarehouseAsync(effectiveDestinationWarehouseId);
            if (sourceIsConsignment != destIsConsignment)
            {
                throw new InvalidOperationException("Transfers between owned and consignment inventory are not supported. Use a dedicated settlement/ownership conversion process.");
            }

            var trackingSequence = item.TrackingSequence + 1;
            if (receivedQty > 0)
            {
                await _trackingControls.StageEventAsync(new InventoryTrackingMutationRequest
                {
                    InventoryItemId = item.InventoryItemId,
                    WarehouseId = effectiveDestinationWarehouseId,
                    LocationId = item.DestinationLocationId,
                    Direction = InventoryTrackingDirection.TransferIn,
                    Quantity = receivedQty,
                    ReferenceType = "InventoryTransfer",
                    ReferenceNumber = transfer.TransferNumber,
                    ReferenceId = transfer.Id,
                    ReferenceLineId = item.Id,
                    EventKey = $"transfer:{transfer.Id:N}:{item.Id:N}:{trackingSequence}:in",
                    LotNumber = item.LotNumber,
                    BatchNumber = item.BatchNumber,
                    SerialNumber = item.SerialNumber,
                    ManufactureDate = item.ManufactureDate,
                    ExpiryDate = item.ExpiryDate,
                    TrackingExceptionId = item.InventoryTrackingExceptionId,
                    CorrelationId = correlationId
                });
            }

            // Add to destination warehouse
            WarehouseQuantity? destQty = null;
            if (receivedQty > 0)
            {
                destQty = await _warehouseQuantityRepository.GetByWarehouseAndItemAsync(effectiveDestinationWarehouseId, item.InventoryItemId);
                if (destQty == null)
                {
                    destQty = new WarehouseQuantity
                    {
                        WarehouseId = effectiveDestinationWarehouseId,
                        InventoryItemId = item.InventoryItemId,
                        CurrentStock = 0,
                        AvailableStock = 0,
                        TenantId = _currentUserProvider.TenantId
                    };
                    await _warehouseQuantityRepository.AddAsync(destQty);
                }
                destQty.CurrentStock += receivedQty;
                destQty.AvailableStock += receivedQty;
                destQty.LastMovementDate = DateTime.UtcNow;
                await _warehouseQuantityRepository.UpdateAsync(destQty);
            }

            // Bin-level tracking: if a destination location is specified, add stock there.
            // This is required for inter-bin transfers and optional for inter-warehouse transfers.
            if (receivedQty > 0 && destinationLocation != null)
            {
                await AdjustInventoryLocationQuantityAsync(item.DestinationLocationId.Value, item.InventoryItemId, receivedQty);
            }
            else if (receivedQty > 0 && transfer.SourceWarehouseId == transfer.DestinationWarehouseId)
            {
                throw new InvalidOperationException("DestinationLocationId is required for same-warehouse (inter-bin) transfers.");
            }

            // Clear allocated from source
            var sourceQty = await _warehouseQuantityRepository.GetByWarehouseAndItemAsync(effectiveSourceWarehouseId, item.InventoryItemId);
            if (sourceQty != null)
            {
                sourceQty.AllocatedStock -= accountedQty;
                if (sourceQty.AllocatedStock < 0)
                    throw new InvalidOperationException("Transfer in-transit allocation would become negative.");
                await _warehouseQuantityRepository.UpdateAsync(sourceQty);
            }

            item.ReceivedQuantity += receivedQty;
            item.DamagedQuantity += damagedQty;
            item.ShortageQuantity += shortageQty;
            item.TrackingSequence = trackingSequence;
            await _transferItemRepository.UpdateAsync(item);

            if (receivedQty > 0)
            {
                var inboundMovement = new StockMovement
                {
                    InventoryItemId = item.InventoryItemId,
                    MovementType = "TransferIn",
                    Quantity = receivedQty,
                    UnitCost = item.UnitCost,
                    TotalValue = receivedQty * item.UnitCost,
                    ReferenceType = ReferenceType.Transfer,
                    ReferenceNumber = transfer.TransferNumber,
                    ReferenceId = transfer.Id,
                    WarehouseId = effectiveDestinationWarehouseId,
                    LocationId = item.DestinationLocationId,
                    LotNumber = item.LotNumber,
                    BatchNumber = item.BatchNumber,
                    SerialNumber = item.SerialNumber,
                    ManufactureDate = item.ManufactureDate,
                    ExpirationDate = item.ExpiryDate,
                    InventoryTrackingExceptionId = item.InventoryTrackingExceptionId,
                    Notes = $"Controlled receipt {action.Sequence} from {transfer.SourceWarehouse?.Name}",
                    ProcessedById = userId,
                    RunningBalance = destQty!.CurrentStock,
                    TenantId = _currentUserProvider.TenantId
                };
                await _stockMovementRepository.AddAsync(inboundMovement);
                await _consignmentSettlementService.TryCreateFromStockMovementAsync(inboundMovement);
            }
        }

        var fullyAccounted = transfer.Items.All(item =>
            item.ShippedQuantity >= item.RequestedQuantity &&
            item.ReceivedQuantity + item.DamagedQuantity + item.ShortageQuantity >= item.ShippedQuantity);
        transfer.Status = fullyAccounted ? TransferStatus.Received : TransferStatus.InTransit;
        transfer.HasOpenDiscrepancy = transfer.HasOpenDiscrepancy || receivedItems.Any(value => value.DamagedQuantity > 0 || value.ShortageQuantity > 0);
        if (fullyAccounted) transfer.ReceivedDate = DateTime.UtcNow;
        transfer.ReceivedById ??= userId;
        transfer.UpdatedAt = DateTime.UtcNow;
        await _transferRepository.UpdateAsync(transfer);
        _logger.LogInformation("Transfer {TransferNumber} receipt {Sequence} recorded (fully accounted: {FullyAccounted})", transfer.TransferNumber, action.Sequence, fullyAccounted);
        return true;
    }

    public async Task<bool> ResolveDiscrepanciesAsync(Guid transferId, Guid userId, ResolveInventoryTransferDiscrepancyRequest request)
    {
        if (!_unitOfWork.HasActiveTransaction)
            return await ExecuteControlledMutationAsync(() => ResolveDiscrepanciesAsync(transferId, userId, request));

        var ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction) await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var transfer = await _transferRepository.GetWithItemsAsync(transferId)
                ?? throw new ArgumentException($"Transfer {transferId} not found");
            await EnsureTransferAccessAsync(transfer, TransferAccessDirection.Both, "resolve-discrepancy");
            var resolutionCode = Normalize(request.ResolutionCode, 50)?.ToUpperInvariant();
            var payloadHash = Hash(JsonSerializer.Serialize(new
            {
                transferId,
                discrepancyIds = request.DiscrepancyIds.Distinct().OrderBy(value => value),
                resolutionCode,
                notes = Normalize(request.ResolutionNotes, 1000),
                evidence = request.Evidence.OrderBy(value => value.CentralDocumentVersionId)
            }));
            var key = MutationKey(request, $"internal-discrepancy:{transfer.Id:N}:{payloadHash}");
            if (await IsActionReplayAsync(transfer.Id, InventoryTransferActionType.DiscrepancyResolved, key, payloadHash))
            {
                if (ownsTransaction) await _unitOfWork.CommitAsync();
                return true;
            }
            if (transfer.Status != TransferStatus.Received || !transfer.HasOpenDiscrepancy)
                throw new InvalidOperationException("Only a fully accounted received transfer with open discrepancies can be resolved.");
            EnsureRowVersion(transfer.RowVersion, request.RowVersion);
            if (userId == transfer.RequestedById || userId == transfer.ApprovedById ||
                userId == transfer.ShippedById || userId == transfer.ReceivedById)
                throw new UnauthorizedAccessException("The requester, approver, dispatcher or receiver cannot resolve the same transfer discrepancy.");
            if (resolutionCode is null || !InventoryTransferDiscrepancyResolutionCodes.All.ContainsKey(resolutionCode))
                throw new ArgumentException("A valid discrepancy resolution code is required.");
            if (request.DiscrepancyIds.Count == 0 || request.DiscrepancyIds.Count != request.DiscrepancyIds.Distinct().Count())
                throw new ArgumentException("At least one unique discrepancy is required.");

            var discrepancies = await _unitOfWork.Repository<InventoryTransferDiscrepancy>().GetQueryable()
                .Include(value => value.InventoryTransferItem)
                .Where(value => value.InventoryTransferId == transfer.Id && request.DiscrepancyIds.Contains(value.Id) && value.Status == InventoryTransferDiscrepancyStatus.Open)
                .ToListAsync();
            if (discrepancies.Count != request.DiscrepancyIds.Count)
                throw new ArgumentException("One or more open discrepancies were not found in the current tenant transfer.");
            var resolutionEvidence = await ValidateTransferEvidenceAsync(request.Evidence, true);
            var correlationId = Normalize(request.CorrelationId, 100) ?? $"transfer-discrepancy:{transfer.Id:N}:{Guid.NewGuid():N}";
            var action = await AddActionAsync(transfer, InventoryTransferActionType.DiscrepancyResolved, userId, key, payloadHash, correlationId,
                request.ResolutionNotes, discrepancies.GroupBy(value => value.InventoryTransferItemId)
                    .Select(values => new TransferActionLineInput(values.Key, 0, 0, values.Sum(value => value.DamagedQuantity), values.Sum(value => value.ShortageQuantity))).ToList(),
                new { ResolutionCode = resolutionCode });
            await _unitOfWork.SaveChangesAsync();

            foreach (var discrepancy in discrepancies)
            {
                var item = discrepancy.InventoryTransferItem;
                var quantity = discrepancy.DamagedQuantity + discrepancy.ShortageQuantity;
                if (resolutionCode is InventoryTransferDiscrepancyResolutionCodes.ReturnedToSource or InventoryTransferDiscrepancyResolutionCodes.ReplacementReceived)
                {
                    var toSource = resolutionCode == InventoryTransferDiscrepancyResolutionCodes.ReturnedToSource;
                    var warehouseId = toSource ? transfer.SourceWarehouseId : transfer.DestinationWarehouseId;
                    var locationId = toSource ? item.SourceLocationId : item.DestinationLocationId;
                    var location = locationId.HasValue
                        ? await EnsureLocationBelongsToWarehouseAsync(locationId.Value, warehouseId,
                            toSource ? "SourceLocationId" : "DestinationLocationId")
                        : null;
                    var inventoryWarehouseId = location?.InventoryWarehouseId ?? warehouseId;
                    var quantityRecord = await _warehouseQuantityRepository.GetByWarehouseAndItemAsync(inventoryWarehouseId, item.InventoryItemId);
                    if (quantityRecord is null)
                    {
                        quantityRecord = new WarehouseQuantity
                        {
                            TenantId = transfer.TenantId,
                            WarehouseId = inventoryWarehouseId,
                            InventoryItemId = item.InventoryItemId
                        };
                        await _warehouseQuantityRepository.AddAsync(quantityRecord);
                    }
                    quantityRecord.CurrentStock += quantity;
                    quantityRecord.AvailableStock += quantity;
                    quantityRecord.LastMovementDate = DateTime.UtcNow;
                    await _warehouseQuantityRepository.UpdateAsync(quantityRecord);
                    if (locationId.HasValue) await AdjustInventoryLocationQuantityAsync(locationId.Value, item.InventoryItemId, quantity);

                    if (!toSource)
                    {
                        item.ReceivedQuantity += quantity;
                        item.DamagedQuantity -= discrepancy.DamagedQuantity;
                        item.ShortageQuantity -= discrepancy.ShortageQuantity;
                        await _transferItemRepository.UpdateAsync(item);
                    }
                    await _stockMovementRepository.AddAsync(new StockMovement
                    {
                        TenantId = transfer.TenantId,
                        InventoryItemId = item.InventoryItemId,
                        MovementType = toSource ? "TransferDiscrepancyReturn" : "TransferReplacementIn",
                        Quantity = quantity,
                        UnitCost = item.UnitCost,
                        TotalValue = quantity * item.UnitCost,
                        ReferenceType = ReferenceType.Transfer,
                        ReferenceId = transfer.Id,
                        ReferenceNumber = transfer.TransferNumber,
                        WarehouseId = inventoryWarehouseId,
                        LocationId = locationId,
                        Notes = $"Controlled discrepancy resolution {action.Sequence}: {resolutionCode}",
                        ProcessedById = userId,
                        RunningBalance = quantityRecord.CurrentStock
                    });
                }

                discrepancy.Status = InventoryTransferDiscrepancyStatus.Resolved;
                discrepancy.ResolutionCode = resolutionCode;
                discrepancy.ResolutionNotes = request.ResolutionNotes.Trim();
                discrepancy.ResolvedById = userId;
                discrepancy.ResolvedAtUtc = DateTime.UtcNow;
                discrepancy.UpdatedAt = DateTime.UtcNow;
                foreach (var evidence in resolutionEvidence)
                {
                    if (await _unitOfWork.Repository<InventoryTransferDiscrepancyEvidence>().GetQueryable()
                        .AnyAsync(value => value.InventoryTransferDiscrepancyId == discrepancy.Id && value.CentralDocumentVersionId == evidence.VersionId)) continue;
                    var link = new InventoryTransferDiscrepancyEvidence
                    {
                        TenantId = transfer.TenantId,
                        InventoryTransferDiscrepancyId = discrepancy.Id,
                        CentralDocumentVersionId = evidence.VersionId,
                        FileUploadRecordId = evidence.FileUploadRecordId,
                        EvidenceReference = evidence.Reference
                    };
                    link.IntegrityHash = Hash($"{discrepancy.Id:N}|{link.CentralDocumentVersionId:N}|{link.FileUploadRecordId:N}|{link.EvidenceReference}");
                    await _unitOfWork.Repository<InventoryTransferDiscrepancyEvidence>().AddAsync(link);
                }
            }

            transfer.HasOpenDiscrepancy = await _unitOfWork.Repository<InventoryTransferDiscrepancy>().GetQueryable().AsNoTracking()
                .AnyAsync(value => value.InventoryTransferId == transfer.Id && value.Status == InventoryTransferDiscrepancyStatus.Open && !request.DiscrepancyIds.Contains(value.Id));
            transfer.UpdatedAt = DateTime.UtcNow;
            await _transferRepository.UpdateAsync(transfer);
            await AddAuditAsync("ResolveDiscrepancy", transfer, new { action.Id, action.Sequence, payloadHash, resolutionCode });
            await RecordControlEventAsync(transfer, "ResolveDiscrepancy", correlationId, action.Id, payloadHash);
            await _unitOfWork.SaveChangesAsync();
            if (ownsTransaction) await _unitOfWork.CommitAsync();
            return true;
        }
        catch
        {
            if (ownsTransaction && _unitOfWork.HasActiveTransaction) await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> CloseAsync(Guid transferId, Guid userId, CloseInventoryTransferRequest request)
    {
        if (!_unitOfWork.HasActiveTransaction)
            return await ExecuteControlledMutationAsync(() => CloseAsync(transferId, userId, request));

        var ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction) await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var transfer = await _transferRepository.GetWithItemsAsync(transferId)
                ?? throw new ArgumentException($"Transfer {transferId} not found");
            await EnsureTransferAccessAsync(transfer, TransferAccessDirection.Both, "close");
            var payloadHash = Hash(JsonSerializer.Serialize(new { transferId, comment = Normalize(request.Comment, 1000) }));
            var key = MutationKey(request, $"internal-close:{transfer.Id:N}:{payloadHash}");
            if (await IsActionReplayAsync(transfer.Id, InventoryTransferActionType.Closed, key, payloadHash))
            {
                if (ownsTransaction) await _unitOfWork.CommitAsync();
                return true;
            }
            if (transfer.Status != TransferStatus.Received || transfer.HasOpenDiscrepancy)
                throw new InvalidOperationException("Transfer must be fully received with every discrepancy resolved before closure.");
            EnsureRowVersion(transfer.RowVersion, request.RowVersion);
            if (userId == transfer.RequestedById || userId == transfer.ApprovedById ||
                userId == transfer.ShippedById || userId == transfer.ReceivedById)
                throw new UnauthorizedAccessException("The requester, approver, dispatcher or receiver cannot close the same transfer.");
            if (transfer.Items.Any(item => item.ShippedQuantity != item.RequestedQuantity || item.ReceivedQuantity + item.DamagedQuantity + item.ShortageQuantity != item.ShippedQuantity))
                throw new InvalidOperationException("Every requested quantity must be dispatched and fully reconciled before closure.");

            var correlationId = Normalize(request.CorrelationId, 100) ?? $"transfer-close:{transfer.Id:N}:{Guid.NewGuid():N}";
            var action = await AddActionAsync(transfer, InventoryTransferActionType.Closed, userId, key, payloadHash, correlationId, request.Comment, []);
            await _unitOfWork.SaveChangesAsync();
            transfer.Status = TransferStatus.Completed;
            transfer.CompletedDate = DateTime.UtcNow;
            transfer.ClosedById = userId;
            transfer.UpdatedAt = DateTime.UtcNow;
            await _transferRepository.UpdateAsync(transfer);
            await AddAuditAsync("Close", transfer, new { action.Id, action.Sequence, payloadHash });
            await RecordControlEventAsync(transfer, "Close", correlationId, action.Id, payloadHash);
            await _unitOfWork.SaveChangesAsync();
            if (ownsTransaction) await _unitOfWork.CommitAsync();
            return true;
        }
        catch
        {
            if (ownsTransaction && _unitOfWork.HasActiveTransaction) await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    public async Task ApplyScanMetadataAsync(
        Guid transferId,
        InventoryScanOperation operation,
        IReadOnlyList<InventoryTransactionScanLineDto> lines,
        Guid userId)
    {
        if (operation is not (InventoryScanOperation.TransferShipment or InventoryScanOperation.TransferReceipt))
            throw new ArgumentException("A transfer scan operation is required.", nameof(operation));
        var transfer = await _unitOfWork.Repository<InventoryTransfer>().GetQueryable(value =>
                value.TenantId == _currentUserProvider.TenantId && value.Id == transferId && !value.IsDeleted)
            .Include(value => value.Items).SingleOrDefaultAsync()
            ?? throw new ArgumentException($"Transfer {transferId} not found in the current tenant.");
        await EnsureTransferAccessAsync(
            transfer,
            operation == InventoryScanOperation.TransferShipment
                ? TransferAccessDirection.Source
                : TransferAccessDirection.Destination,
            "scan-metadata");
        var validStatus = operation == InventoryScanOperation.TransferShipment
            ? transfer.Status is TransferStatus.Approved or TransferStatus.InTransit
            : transfer.Status == TransferStatus.InTransit;
        if (!validStatus)
            throw new InvalidOperationException($"Transfer is not in a valid state for {operation} scanned metadata.");

        foreach (var scan in lines)
        {
            var item = transfer.Items.SingleOrDefault(value => value.Id == scan.DocumentLineId && value.InventoryItemId == scan.InventoryItemId)
                ?? throw new ArgumentException($"Transfer line {scan.DocumentLineId} was not found for the scanned item.");
            var expectedQuantity = operation == InventoryScanOperation.TransferShipment
                ? item.RequestedQuantity - item.ShippedQuantity
                : item.ShippedQuantity - item.ReceivedQuantity - item.DamagedQuantity - item.ShortageQuantity;
            if (scan.BaseQuantity != expectedQuantity)
                throw new InvalidOperationException($"The complete transfer quantity for {item.ItemCode} must be scanned before applying the transaction.");
            if (scan.LocationId.HasValue)
            {
                var warehouseId = operation == InventoryScanOperation.TransferShipment ? transfer.SourceWarehouseId : transfer.DestinationWarehouseId;
                await EnsureLocationBelongsToWarehouseAsync(scan.LocationId.Value, warehouseId,
                    operation == InventoryScanOperation.TransferShipment ? "SourceLocationId" : "DestinationLocationId");
                if (operation == InventoryScanOperation.TransferShipment) item.SourceLocationId = scan.LocationId;
                else item.DestinationLocationId = scan.LocationId;
            }
            item.LotNumber = string.IsNullOrWhiteSpace(scan.LotNumber) ? item.LotNumber : scan.LotNumber.Trim();
            item.BatchNumber = string.IsNullOrWhiteSpace(scan.BatchNumber) ? item.BatchNumber : scan.BatchNumber.Trim();
            item.SerialNumber = string.IsNullOrWhiteSpace(scan.SerialNumber) ? item.SerialNumber : scan.SerialNumber.Trim();
            item.ManufactureDate = scan.ManufactureDate ?? item.ManufactureDate;
            item.ExpiryDate = scan.ExpiryDate ?? item.ExpiryDate;
            item.InventoryTrackingExceptionId = scan.InventoryTrackingExceptionId ?? item.InventoryTrackingExceptionId;
            item.UpdatedAt = DateTime.UtcNow;
            item.LastModifiedById = userId;
            await _transferItemRepository.UpdateAsync(item);
        }
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<bool> CancelAsync(Guid transferId, string reason, Guid userId, InventoryTransferMutationContext? control = null)
    {
        if (!_unitOfWork.HasActiveTransaction)
            return await ExecuteControlledMutationAsync(() => CancelAsync(transferId, reason, userId, control));

        var ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction) await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var transfer = await _transferRepository.GetWithItemsAsync(transferId)
                ?? throw new ArgumentException($"Transfer {transferId} not found");
            await EnsureTransferAccessAsync(transfer, TransferAccessDirection.Both, "cancel");
            var normalizedReason = Normalize(reason, 1000) ?? throw new ArgumentException("A cancellation reason is required.");
            var payloadHash = Hash(JsonSerializer.Serialize(new { transferId, reason = normalizedReason }));
            var key = MutationKey(control, $"internal-cancel:{transfer.Id:N}:{payloadHash}");
            if (await IsActionReplayAsync(transfer.Id, InventoryTransferActionType.Cancelled, key, payloadHash))
            {
                if (ownsTransaction) await _unitOfWork.CommitAsync();
                return true;
            }
            if (transfer.Status is TransferStatus.InTransit or TransferStatus.Received or TransferStatus.Completed)
                throw new InvalidOperationException("A dispatched, received or completed transfer cannot be cancelled.");
            if (control is not null) EnsureRowVersion(transfer.RowVersion, control.RowVersion);
            var correlationId = Normalize(control?.CorrelationId, 100) ?? $"transfer-cancel:{transfer.Id:N}:{Guid.NewGuid():N}";
            var action = await AddActionAsync(transfer, InventoryTransferActionType.Cancelled, userId, key, payloadHash, correlationId, normalizedReason, []);
            await _unitOfWork.SaveChangesAsync();
            transfer.Status = TransferStatus.Cancelled;
            transfer.CancellationReason = normalizedReason;
            transfer.Notes = $"{transfer.Notes}\nCancelled: {normalizedReason}";
            transfer.UpdatedAt = DateTime.UtcNow;
            await _transferRepository.UpdateAsync(transfer);
            await AddAuditAsync("Cancel", transfer, new { action.Id, action.Sequence, payloadHash });
            await RecordControlEventAsync(transfer, "Cancel", correlationId, action.Id, payloadHash);
            await _unitOfWork.SaveChangesAsync();
            if (ownsTransaction) await _unitOfWork.CommitAsync();
            _logger.LogInformation("Transfer {TransferNumber} cancelled: {Reason}", transfer.TransferNumber, normalizedReason);
            return true;
        }
        catch
        {
            if (ownsTransaction && _unitOfWork.HasActiveTransaction) await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> ReverseShipmentAsync(Guid transferId, string reason, Guid userId, InventoryTransferMutationContext? control = null)
    {
        if (!_unitOfWork.HasActiveTransaction)
            return await ExecuteControlledMutationAsync(() => ReverseShipmentAsync(transferId, reason, userId, control));

        var ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction) await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
        var transfer = await _transferRepository.GetWithItemsAsync(transferId)
            ?? throw new ArgumentException($"Transfer {transferId} not found");

        await EnsureTransferAccessAsync(transfer, TransferAccessDirection.Source, "reverse-shipment");

        var normalizedReason = Normalize(reason, 1000) ?? throw new ArgumentException("A reversal reason is required.");
        var payloadHash = Hash(JsonSerializer.Serialize(new { transferId, reason = normalizedReason }));
        var key = MutationKey(control, $"internal-reverse:{transfer.Id:N}:{payloadHash}");
        if (await IsActionReplayAsync(transfer.Id, InventoryTransferActionType.ShipmentReversed, key, payloadHash))
        {
            if (ownsTransaction) await _unitOfWork.CommitAsync();
            return true;
        }

        if (transfer.Status != TransferStatus.InTransit)
            throw new InvalidOperationException("Only transfers that are in transit can be reversed. Once received, a transfer cannot be reversed.");
        if (transfer.Items.Any(item => item.ReceivedQuantity > 0 || item.DamagedQuantity > 0 || item.ShortageQuantity > 0))
            throw new InvalidOperationException("A transfer with any recorded receipt or discrepancy cannot reverse its shipment.");
        if (userId == transfer.ShippedById)
            throw new UnauthorizedAccessException("The dispatcher cannot reverse the same shipment.");
        if (control is not null) EnsureRowVersion(transfer.RowVersion, control.RowVersion);
        var correlationId = Normalize(control?.CorrelationId, 100) ?? $"transfer-reverse:{transfer.Id:N}:{Guid.NewGuid():N}";
        var action = await AddActionAsync(transfer, InventoryTransferActionType.ShipmentReversed, userId, key, payloadHash, correlationId,
            normalizedReason, transfer.Items.Where(item => item.ShippedQuantity > 0)
                .Select(item => new TransferActionLineInput(item.Id, item.ShippedQuantity, 0, 0, 0)).ToList());
        await _unitOfWork.SaveChangesAsync();

        // Reverse the stock movements - add back to source warehouse
        foreach (var item in transfer.Items)
        {
            if (item.ShippedQuantity <= 0)
                continue;

            WarehouseLocation? sourceLocation = null;
            if (item.SourceLocationId.HasValue && item.SourceLocationId.Value != Guid.Empty)
            {
                sourceLocation = await EnsureLocationBelongsToWarehouseAsync(item.SourceLocationId.Value, transfer.SourceWarehouseId, "SourceLocationId");
            }

            var effectiveSourceWarehouseId = sourceLocation?.InventoryWarehouseId ?? transfer.SourceWarehouseId;

            var sourceQty = await _warehouseQuantityRepository.GetByWarehouseAndItemAsync(effectiveSourceWarehouseId, item.InventoryItemId);
            if (sourceQty != null)
            {
                // Reinstate the stock to source warehouse
                sourceQty.CurrentStock += item.ShippedQuantity;
                sourceQty.AvailableStock += item.ShippedQuantity;
                sourceQty.AllocatedStock -= item.ShippedQuantity; // Remove from allocated
                sourceQty.LastMovementDate = DateTime.UtcNow;
                await _warehouseQuantityRepository.UpdateAsync(sourceQty);
            }

            // Reinstate bin stock if shipment deducted it.
            if (item.SourceLocationId.HasValue && item.SourceLocationId.Value != Guid.Empty)
            {
                await AdjustInventoryLocationQuantityAsync(item.SourceLocationId.Value, item.InventoryItemId, item.ShippedQuantity);
            }

            // Create reversal movement
            var reversalMovement = new StockMovement
            {
                InventoryItemId = item.InventoryItemId,
                MovementType = "TransferReversal",
                Quantity = item.ShippedQuantity,
                UnitCost = item.UnitCost,
                TotalValue = item.ShippedQuantity * item.UnitCost,
                ReferenceType = ReferenceType.Transfer,
                ReferenceNumber = transfer.TransferNumber,
                ReferenceId = transfer.Id,
                WarehouseId = effectiveSourceWarehouseId,
                LocationId = item.SourceLocationId,
                Notes = $"Controlled shipment reversal {action.Sequence}: {normalizedReason}",
                ProcessedById = userId,
                RunningBalance = sourceQty?.CurrentStock ?? 0,
                TenantId = _currentUserProvider.TenantId
            };
            await _stockMovementRepository.AddAsync(reversalMovement);
            await _consignmentSettlementService.TryCreateFromStockMovementAsync(reversalMovement);

            // Reset shipped quantity
            item.ShippedQuantity = 0;
            await _transferItemRepository.UpdateAsync(item);
        }

        // Set transfer status to Cancelled
        transfer.Status = TransferStatus.Cancelled;
        transfer.ShippedDate = null;
        transfer.ShippedById = null;
        transfer.TrackingNumber = null;
        transfer.Notes = $"{transfer.Notes}\nShipment reversed and cancelled on {DateTime.UtcNow:yyyy-MM-dd HH:mm}: {normalizedReason}";
        transfer.CancellationReason = normalizedReason;
        transfer.UpdatedAt = DateTime.UtcNow;
        await _transferRepository.UpdateAsync(transfer);
        await AddAuditAsync("ReverseShipment", transfer, new { action.Id, action.Sequence, payloadHash });
        await RecordControlEventAsync(transfer, "ReverseShipment", correlationId, action.Id, payloadHash);
        await _unitOfWork.SaveChangesAsync();
        if (ownsTransaction) await _unitOfWork.CommitAsync();

        _logger.LogInformation("Transfer {TransferNumber} shipment reversed and cancelled: {Reason}", transfer.TransferNumber, normalizedReason);
        return true;
        }
        catch
        {
            if (ownsTransaction && _unitOfWork.HasActiveTransaction) await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    #region Private Methods

    private sealed record TransferActionLineInput(
        Guid TransferItemId,
        decimal DispatchedQuantity,
        decimal ReceivedQuantity,
        decimal DamagedQuantity,
        decimal ShortageQuantity);

    private async Task<bool> ExecuteControlledMutationAsync(Func<Task<bool>> operation)
    {
        return await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var result = await operation();
                await _unitOfWork.CommitAsync();
                return result;
            }
            catch
            {
                if (_unitOfWork.HasActiveTransaction)
                    await _unitOfWork.RollbackAsync();
                _unitOfWork.ClearTrackedChanges();
                throw;
            }
        });
    }

    private sealed record ValidatedTransferEvidence(Guid VersionId, Guid FileUploadRecordId, string Reference);

    private async Task<InventoryTransferAction> AddActionAsync(
        InventoryTransfer transfer,
        InventoryTransferActionType actionType,
        Guid actorUserId,
        string idempotencyKey,
        string payloadHash,
        string correlationId,
        string? comment,
        IReadOnlyList<TransferActionLineInput> lines,
        object? metadata = null)
    {
        var sequence = await _unitOfWork.Repository<InventoryTransferAction>().GetQueryable().AsNoTracking()
            .CountAsync(value => value.InventoryTransferId == transfer.Id) + 1;
        var snapshot = JsonSerializer.Serialize(new
        {
            transfer.Id,
            transfer.TransferNumber,
            Status = transfer.Status.ToString(),
            transfer.SourceWarehouseId,
            transfer.DestinationWarehouseId,
            Lines = lines,
            Metadata = metadata
        });
        var action = new InventoryTransferAction
        {
            TenantId = transfer.TenantId,
            InventoryTransferId = transfer.Id,
            Sequence = sequence,
            ActionType = actionType,
            ActorUserId = actorUserId,
            OccurredAtUtc = DateTime.UtcNow,
            IdempotencyKey = idempotencyKey,
            PayloadHash = payloadHash,
            CorrelationId = correlationId,
            Comment = Normalize(comment, 1000),
            SnapshotJson = snapshot
        };
        action.IntegrityHash = Hash($"{action.InventoryTransferId:N}|{action.Sequence}|{(int)action.ActionType}|{action.ActorUserId:N}|{action.OccurredAtUtc:O}|{action.IdempotencyKey}|{action.PayloadHash}|{action.SnapshotJson}");
        await _unitOfWork.Repository<InventoryTransferAction>().AddAsync(action);
        foreach (var value in lines)
        {
            var line = new InventoryTransferActionLine
            {
                TenantId = transfer.TenantId,
                InventoryTransferActionId = action.Id,
                InventoryTransferItemId = value.TransferItemId,
                DispatchedQuantity = value.DispatchedQuantity,
                ReceivedQuantity = value.ReceivedQuantity,
                DamagedQuantity = value.DamagedQuantity,
                ShortageQuantity = value.ShortageQuantity
            };
            line.IntegrityHash = Hash($"{action.Id:N}|{line.InventoryTransferItemId:N}|{line.DispatchedQuantity}|{line.ReceivedQuantity}|{line.DamagedQuantity}|{line.ShortageQuantity}");
            await _unitOfWork.Repository<InventoryTransferActionLine>().AddAsync(line);
        }
        return action;
    }

    private async Task<bool> IsActionReplayAsync(Guid transferId, InventoryTransferActionType actionType, string key, string payloadHash)
    {
        var existing = await _unitOfWork.Repository<InventoryTransferAction>().GetQueryable().AsNoTracking()
            .SingleOrDefaultAsync(value => value.InventoryTransferId == transferId && value.ActionType == actionType && value.IdempotencyKey == key);
        if (existing is null) return false;
        if (!string.Equals(existing.PayloadHash, payloadHash, StringComparison.Ordinal))
            throw new InvalidOperationException("The idempotency key was already used with a different transfer payload.");
        return true;
    }

    private static string MutationKey(InventoryTransferMutationContext? context, string fallback)
    {
        if (context is null) return fallback.Length <= 100 ? fallback : Hash(fallback);
        var key = Normalize(context.IdempotencyKey, 100);
        return key ?? throw new ArgumentException("An idempotency key is required.");
    }

    private static void EnsureRowVersion(byte[] current, string supplied)
    {
        byte[] expected;
        try { expected = Convert.FromBase64String(supplied ?? string.Empty); }
        catch (FormatException) { throw new InvalidOperationException("The transfer row version is invalid."); }
        if (!current.SequenceEqual(expected))
            throw new InvalidOperationException("The transfer changed after it was loaded. Refresh and retry.");
    }

    private async Task<IReadOnlyList<ValidatedTransferEvidence>> ValidateTransferEvidenceAsync(
        IReadOnlyCollection<InventoryControlEvidenceRequest>? requested,
        bool required)
    {
        var values = requested ?? Array.Empty<InventoryControlEvidenceRequest>();
        if (required && values.Count == 0)
            throw new InvalidOperationException("Current published central-DMS evidence is required.");
        if (values.Select(value => value.CentralDocumentVersionId).Distinct().Count() != values.Count)
            throw new ArgumentException("Duplicate central-DMS evidence is not allowed.");

        var result = new List<ValidatedTransferEvidence>();
        foreach (var value in values)
        {
            var version = await _unitOfWork.Repository<CentralDocumentVersion>().GetQueryable()
                .Include(item => item.DocumentRecord)
                .SingleOrDefaultAsync(item => item.Id == value.CentralDocumentVersionId && !item.IsDeleted &&
                    item.TenantId == _currentUserProvider.TenantId && !item.DocumentRecord.IsDeleted &&
                    item.DocumentRecord.LifecycleStatus == CentralDocumentEvidenceRules.ActiveLifecycleStatus &&
                    item.DocumentRecord.VersionStatus == CentralDocumentEvidenceRules.PublishedVersionStatus &&
                    item.DocumentRecord.CurrentVersion == item.VersionNumber &&
                    item.Status == CentralDocumentEvidenceRules.PublishedVersionStatus && item.PublishedAt.HasValue && item.FileUploadRecordId.HasValue);
            if (version is null)
                throw new InvalidOperationException("Linked central-DMS evidence must be the current published version in this tenant.");
            var reference = Normalize(value.EvidenceReference, 500)
                ?? throw new ArgumentException("An evidence reference is required.");
            result.Add(new ValidatedTransferEvidence(version.Id, version.FileUploadRecordId!.Value, reference));
        }
        return result;
    }

    private async Task AddAuditAsync(string action, InventoryTransfer transfer, object after) =>
        await _unitOfWork.Repository<AuditLog>().AddAsync(new AuditLog
        {
            TenantId = transfer.TenantId,
            UserId = _currentUserProvider.UserId,
            Username = _currentUserProvider.Username,
            Action = action,
            Resource = "InventoryTransfer",
            ResourceId = transfer.Id.ToString(),
            NewValues = JsonSerializer.Serialize(after),
            IpAddress = "system",
            Timestamp = DateTime.UtcNow
        });

    private Task RecordControlEventAsync(InventoryTransfer transfer, string action, string correlationId, Guid actionId, string payloadHash) =>
        _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = $"inventory-transfer:{transfer.Id:N}:{action.ToLowerInvariant()}:{actionId:N}",
            EventType = "InventoryTransferControl",
            Action = action,
            Result = ProcurementControlEventResult.Allowed,
            RuleCode = "INV-008",
            DecisionKeys = Enumerable.Range(1, 14).Select(value => $"DEC-{value:000}").ToList(),
            SourceType = "InventoryTransfer",
            SourceId = transfer.Id,
            SourceReference = transfer.TransferNumber,
            Reason = action,
            After = new { transfer.Status, transfer.HasOpenDiscrepancy, payloadHash },
            CorrelationId = correlationId,
            OccurredAtUtc = DateTime.UtcNow
        });

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string DispatchPayloadHash(
        Guid transferId,
        string? trackingNumber,
        IReadOnlyDictionary<Guid, decimal>? shippedItems,
        IReadOnlyDictionary<Guid, Guid>? negativeStockOverrideIds,
        string? payloadSalt) =>
        Hash(JsonSerializer.Serialize(new
        {
            transferId,
            trackingNumber = Normalize(trackingNumber, 100),
            PayloadSalt = payloadSalt,
            lines = shippedItems is null
                ? "ALL_REMAINING"
                : JsonSerializer.Serialize(shippedItems.OrderBy(value => value.Key)),
            NegativeStockOverrides = negativeStockOverrideIds is null
                ? null
                : JsonSerializer.Serialize(negativeStockOverrideIds.OrderBy(value => value.Key))
        }));

    private static string? Normalize(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private enum TransferAccessDirection
    {
        Source,
        Destination,
        Both
    }

    private async Task<IReadOnlyList<InventoryTransfer>> FilterReadableAsync(
        IEnumerable<InventoryTransfer> transfers)
    {
        var readable = new List<InventoryTransfer>();
        foreach (var transfer in transfers)
        {
            var fullTransfer = transfer.Items.Count > 0
                ? transfer
                : await _transferRepository.GetWithItemsAsync(transfer.Id) ?? transfer;
            if (await CanReadAsync(fullTransfer))
                readable.Add(transfer);
        }

        return readable;
    }

    private async Task<bool> CanReadAsync(InventoryTransfer transfer)
    {
        return await CanAccessDirectionAsync(transfer, TransferAccessDirection.Source) ||
               await CanAccessDirectionAsync(transfer, TransferAccessDirection.Destination);
    }

    private async Task<bool> CanAccessDirectionAsync(
        InventoryTransfer transfer,
        TransferAccessDirection direction)
    {
        var source = direction == TransferAccessDirection.Source;
        var warehouseId = source ? transfer.SourceWarehouseId : transfer.DestinationWarehouseId;
        var locationIds = transfer.Items.Count == 0
            ? new Guid?[] { null }
            : transfer.Items
                .Where(item => !item.IsDeleted)
                .Select(item => source ? item.SourceLocationId : item.DestinationLocationId)
                .Distinct()
                .ToArray();
        if (locationIds.Length == 0)
            locationIds = [null];

        foreach (var locationId in locationIds)
        {
            try
            {
                var decision = await _accessControl.CheckCapabilityAsync(
                    BuildAccessRequest(
                        "procurement.inventory.read",
                        warehouseId,
                        locationId,
                        $"{transfer.TransferNumber}:{direction.ToString().ToLowerInvariant()}"),
                    Guid.NewGuid().ToString("N"));
                if (decision.Allowed)
                    return true;
            }
            catch (ProcurementAccessAuthorizationException)
            {
                // A read list omits transfers outside the actor's effective store scope.
            }
            catch (ProcurementAccessValidationException)
            {
                // Invalid or foreign scope is indistinguishable from a missing record on reads.
            }
        }

        return false;
    }

    private async Task EnsureTransferAccessAsync(
        InventoryTransfer transfer,
        TransferAccessDirection direction,
        string action)
    {
        if (direction is TransferAccessDirection.Source or TransferAccessDirection.Both)
        {
            await EnsureWarehouseLocationsAsync(
                "procurement.inventory.transfer",
                transfer.SourceWarehouseId,
                transfer.Items.Where(item => !item.IsDeleted).Select(item => item.SourceLocationId),
                $"{transfer.TransferNumber}:{action}:source");
        }

        if (direction is TransferAccessDirection.Destination or TransferAccessDirection.Both)
        {
            await EnsureWarehouseLocationsAsync(
                "procurement.inventory.transfer",
                transfer.DestinationWarehouseId,
                transfer.Items.Where(item => !item.IsDeleted).Select(item => item.DestinationLocationId),
                $"{transfer.TransferNumber}:{action}:destination");
        }
    }

    private async Task EnsureWarehouseLocationsAsync(
        string permission,
        Guid warehouseId,
        IEnumerable<Guid?> locationIds,
        string sourceReference)
    {
        var scopes = locationIds.Distinct().ToList();
        if (scopes.Count == 0)
            scopes.Add(null);

        foreach (var locationId in scopes)
        {
            var decision = await _accessControl.EnforceCapabilityAsync(
                BuildAccessRequest(permission, warehouseId, locationId, sourceReference),
                Guid.NewGuid().ToString("N"));
            if (!decision.Allowed)
                throw new ProcurementAccessAuthorizationException(decision.Message);
        }
    }

    private static ProcurementAccessCapabilityRequest BuildAccessRequest(
        string permission,
        Guid warehouseId,
        Guid? locationId,
        string sourceReference) => new()
        {
            PermissionCode = permission,
            WarehouseId = warehouseId,
            LocationId = locationId,
            RequireLocationScope = true,
            SourceType = "InventoryTransfer",
            SourceReference = sourceReference
        };

    private static void EnsureInterBinLocations(Guid? sourceLocationId, Guid? destinationLocationId)
    {
        if (!sourceLocationId.HasValue || sourceLocationId.Value == Guid.Empty)
        {
            throw new InvalidOperationException("SourceLocationId is required for same-warehouse (inter-bin) transfers.");
        }

        if (!destinationLocationId.HasValue || destinationLocationId.Value == Guid.Empty)
        {
            throw new InvalidOperationException("DestinationLocationId is required for same-warehouse (inter-bin) transfers.");
        }

        if (sourceLocationId.Value == destinationLocationId.Value)
        {
            throw new InvalidOperationException("SourceLocationId and DestinationLocationId must be different for inter-bin transfers.");
        }
    }

    private async Task<WarehouseLocation> EnsureLocationBelongsToWarehouseAsync(Guid locationId, Guid warehouseId, string fieldName)
    {
        if (locationId == Guid.Empty)
        {
            throw new InvalidOperationException($"{fieldName} is invalid.");
        }

        var location = await _warehouseLocationRepository.GetByIdAsync(locationId);
        if (location == null)
        {
            throw new InvalidOperationException($"{fieldName} ({locationId}) does not exist.");
        }

        if (location.WarehouseId != warehouseId)
        {
            throw new InvalidOperationException($"{fieldName} must belong to the selected warehouse.");
        }

        return location;
    }

    private async Task<bool> IsConsignmentWarehouseAsync(Guid warehouseId)
    {
        if (warehouseId == Guid.Empty)
        {
            return false;
        }

        if (_consignmentWarehouseFlagCache.TryGetValue(warehouseId, out var cached))
        {
            return cached;
        }

        var warehouse = await _warehouseRepository.GetByIdAsync(warehouseId);
        var isConsignment = warehouse?.IsConsignmentWarehouse == true;
        _consignmentWarehouseFlagCache[warehouseId] = isConsignment;
        return isConsignment;
    }

    private async Task AdjustInventoryLocationQuantityAsync(Guid locationId, Guid inventoryItemId, decimal deltaQuantity)
    {
        if (deltaQuantity == 0)
        {
            return;
        }

        var existing = await _inventoryLocationRepository.GetByLocationAndItemAsync(locationId, inventoryItemId);
        if (existing == null)
        {
            // If we don't have location-level balances yet, allow the transfer to proceed by initializing
            // a minimal record (prevents hard-blocking adoption of bin tracking).
            var initialQty = deltaQuantity < 0 ? Math.Abs(deltaQuantity) : 0;
            existing = new InventoryLocation
            {
                LocationId = locationId,
                InventoryItemId = inventoryItemId,
                Quantity = initialQty,
                AllocatedQuantity = 0,
                AvailableQuantity = initialQty,
                AverageCost = 0,
                LastMovementDate = DateTime.UtcNow,
                TenantId = _currentUserProvider.TenantId,
                CreatedById = _currentUserProvider.UserId,
                CreatedAt = DateTime.UtcNow
            };

            await _inventoryLocationRepository.AddAsync(existing);
        }

        var newQty = existing.Quantity + deltaQuantity;
        if (newQty < 0)
        {
            throw new InvalidOperationException("Insufficient stock in the selected source bin.");
        }

        existing.Quantity = newQty;
        existing.AvailableQuantity = existing.Quantity - existing.AllocatedQuantity;
        existing.LastMovementDate = DateTime.UtcNow;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.LastModifiedById = _currentUserProvider.UserId;

        await _inventoryLocationRepository.UpdateAsync(existing);
    }

    private async Task<string> GenerateTransferNumberAsync()
    {
        var yearPrefix = DateTime.UtcNow.ToString("yy");
        var monthPrefix = DateTime.UtcNow.ToString("MM");
        var sequence = await GetNextSequenceAsync();
        return $"TRF{yearPrefix}{monthPrefix}{sequence:D4}";
    }

    private Task<int> GetNextSequenceAsync()
    {
        return Task.FromResult(new Random().Next(1, 9999));
    }

    private static InventoryTransferDto MapToDto(InventoryTransfer transfer)
    {
        return new InventoryTransferDto
        {
            Id = transfer.Id,
            TransferNumber = transfer.TransferNumber,
            SourceWarehouseId = transfer.SourceWarehouseId,
            SourceWarehouseName = transfer.SourceWarehouse?.Name ?? string.Empty,
            DestinationWarehouseId = transfer.DestinationWarehouseId,
            DestinationWarehouseName = transfer.DestinationWarehouse?.Name ?? string.Empty,
            Status = transfer.Status,
            TransferType = TransferType.Standard,
            Priority = "Normal",
            RequestDate = transfer.RequestDate,
            RequiredDate = transfer.RequiredDate,
            ShippedDate = transfer.ShippedDate,
            ReceivedDate = transfer.ReceivedDate,
            TotalItems = transfer.TotalItems,
            TotalQuantity = transfer.TotalQuantity,
            TotalValue = transfer.TotalValue,
            ShippingCost = transfer.ShippingCost,
            MiscellaneousCost = transfer.MiscellaneousCost,
            MiscellaneousCostDescription = transfer.MiscellaneousCostDescription,
            TotalAdditionalCost = transfer.TotalAdditionalCost,
            CostAllocationMethod = transfer.CostAllocationMethod,
            CostApportionmentBasis = transfer.CostApportionmentBasis,
            ExpenseGLAccount = transfer.ExpenseGLAccount,
            CostsAllocated = transfer.CostsAllocated,
            RequestedByName = transfer.RequestedBy?.FullName,
            ApprovedByName = transfer.ApprovedBy?.FullName,
            Notes = transfer.Notes,
            CreatedAtFormatted = transfer.CreatedAt.ToString("yyyy-MM-dd HH:mm")
        };
    }

    private static InventoryTransferDetailDto MapToDetailDto(InventoryTransfer transfer)
    {
        return new InventoryTransferDetailDto
        {
            Id = transfer.Id,
            TransferNumber = transfer.TransferNumber,
            SourceWarehouseId = transfer.SourceWarehouseId,
            SourceWarehouseName = transfer.SourceWarehouse?.Name ?? string.Empty,
            DestinationWarehouseId = transfer.DestinationWarehouseId,
            DestinationWarehouseName = transfer.DestinationWarehouse?.Name ?? string.Empty,
            Status = transfer.Status,
            TransferType = TransferType.Standard,
            Priority = "Normal",
            RequestDate = transfer.RequestDate,
            RequiredDate = transfer.RequiredDate,
            ShippedDate = transfer.ShippedDate,
            ReceivedDate = transfer.ReceivedDate,
            TotalItems = transfer.TotalItems,
            TotalQuantity = transfer.TotalQuantity,
            TotalValue = transfer.TotalValue,
            ShippingCost = transfer.ShippingCost,
            MiscellaneousCost = transfer.MiscellaneousCost,
            MiscellaneousCostDescription = transfer.MiscellaneousCostDescription,
            TotalAdditionalCost = transfer.TotalAdditionalCost,
            CostAllocationMethod = transfer.CostAllocationMethod,
            CostApportionmentBasis = transfer.CostApportionmentBasis,
            ExpenseGLAccount = transfer.ExpenseGLAccount,
            CostsAllocated = transfer.CostsAllocated,
            RequestedByName = transfer.RequestedBy?.FullName,
            ApprovedByName = transfer.ApprovedBy?.FullName,
            Notes = transfer.Notes,
            CreatedAtFormatted = transfer.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
            ApprovedDate = transfer.ApprovalDate,
            ShippedByName = transfer.ShippedBy?.FullName,
            ReceivedByName = transfer.ReceivedBy?.FullName,
            TrackingNumber = transfer.TrackingNumber,
            CarrierName = transfer.CarrierName,
            HasOpenDiscrepancy = transfer.HasOpenDiscrepancy,
            CompletedDate = transfer.CompletedDate,
            RowVersion = Convert.ToBase64String(transfer.RowVersion),
            Items = transfer.Items.Select(i => new InventoryTransferItemDto
            {
                Id = i.Id,
                InventoryItemId = i.InventoryItemId,
                ItemCode = i.InventoryItem?.ItemCode ?? string.Empty,
                ItemName = i.InventoryItem?.Name ?? string.Empty,
                RequestedQuantity = i.RequestedQuantity,
                ShippedQuantity = i.ShippedQuantity,
                ReceivedQuantity = i.ReceivedQuantity,
                DamagedQuantity = i.DamagedQuantity,
                ShortageQuantity = i.ShortageQuantity,
                UnitOfMeasure = i.UnitOfMeasure ?? string.Empty,
                UnitCost = i.UnitCost,
                TotalCost = i.RequestedQuantity * i.UnitCost,
                AllocatedCostPerUnit = i.AllocatedCostPerUnit,
                TotalAllocatedCost = i.TotalAllocatedCost,
                LandedUnitCost = i.LandedUnitCost,
                LotNumber = i.LotNumber,
                BatchNumber = i.BatchNumber,
                SerialNumber = i.SerialNumber,
                ManufactureDate = i.ManufactureDate,
                ExpiryDate = i.ExpiryDate,
                InventoryTrackingExceptionId = i.InventoryTrackingExceptionId,
                SourceLocationId = i.SourceLocationId,
                SourceLocationName = i.SourceLocation?.LocationCode,
                DestinationLocationId = i.DestinationLocationId,
                DestinationLocationName = i.DestinationLocation?.LocationCode,
                Notes = i.Notes
            }).ToList()
        };
    }

    private async Task<InventoryTransferDetailDto> MapToControlledDetailDtoAsync(InventoryTransfer transfer)
    {
        var dto = MapToDetailDto(transfer);
        dto.Actions = await _unitOfWork.Repository<InventoryTransferAction>().GetQueryable().AsNoTracking()
            .Where(value => value.InventoryTransferId == transfer.Id)
            .OrderBy(value => value.Sequence)
            .Select(value => new InventoryTransferActionDto
            {
                Id = value.Id,
                Sequence = value.Sequence,
                ActionType = value.ActionType.ToString(),
                ActorUserId = value.ActorUserId,
                OccurredAtUtc = value.OccurredAtUtc,
                CorrelationId = value.CorrelationId,
                Comment = value.Comment
            }).ToListAsync();
        var discrepancies = await _unitOfWork.Repository<InventoryTransferDiscrepancy>().GetQueryable().AsNoTracking()
            .Where(value => value.InventoryTransferId == transfer.Id)
            .Include(value => value.Evidence).ThenInclude(value => value.CentralDocumentVersion).ThenInclude(value => value.DocumentRecord)
            .OrderBy(value => value.CreatedAt)
            .ToListAsync();
        dto.Discrepancies = discrepancies.Select(value => new InventoryTransferDiscrepancyDto
        {
            Id = value.Id,
            InventoryTransferItemId = value.InventoryTransferItemId,
            DamagedQuantity = value.DamagedQuantity,
            ShortageQuantity = value.ShortageQuantity,
            ReasonCode = value.ReasonCode,
            Reason = value.Reason,
            Status = value.Status.ToString(),
            ResolutionCode = value.ResolutionCode,
            ResolutionNotes = value.ResolutionNotes,
            ResolvedAtUtc = value.ResolvedAtUtc,
            Evidence = value.Evidence.Select(item => new InventoryControlEvidenceDto
            {
                Id = item.Id,
                CentralDocumentVersionId = item.CentralDocumentVersionId,
                FileUploadRecordId = item.FileUploadRecordId,
                EvidenceReference = item.EvidenceReference,
                DocumentReference = item.CentralDocumentVersion.DocumentRecord.DocumentReference,
                VersionNumber = item.CentralDocumentVersion.VersionNumber
            }).ToList()
        }).ToList();
        return dto;
    }

    #endregion

    #region Item Management Methods

    public async Task<InventoryTransferItemDto> AddItemAsync(Guid transferId, AddTransferItemDto dto, Guid userId)
    {
        var transfer = await _transferRepository.GetByIdAsync(transferId)
            ?? throw new ArgumentException($"Transfer {transferId} not found");

        if (transfer.Status != TransferStatus.Draft)
            throw new InvalidOperationException("Items can only be added to transfers in Draft status");

        var item = await _itemRepository.GetByIdAsync(dto.InventoryItemId)
            ?? throw new ArgumentException($"Inventory item {dto.InventoryItemId} not found");

        WarehouseLocation? sourceLocation = null;
        if (dto.SourceLocationId.HasValue && dto.SourceLocationId.Value != Guid.Empty)
        {
            sourceLocation = await EnsureLocationBelongsToWarehouseAsync(dto.SourceLocationId.Value, transfer.SourceWarehouseId, "SourceLocationId");
        }

        WarehouseLocation? destinationLocation = null;
        if (dto.DestinationLocationId.HasValue && dto.DestinationLocationId.Value != Guid.Empty)
        {
            destinationLocation = await EnsureLocationBelongsToWarehouseAsync(dto.DestinationLocationId.Value, transfer.DestinationWarehouseId, "DestinationLocationId");
        }

        await EnsureWarehouseLocationsAsync(
            "procurement.inventory.transfer",
            transfer.SourceWarehouseId,
            [dto.SourceLocationId],
            $"{transfer.TransferNumber}:add-item-source");
        await EnsureWarehouseLocationsAsync(
            "procurement.inventory.transfer",
            transfer.DestinationWarehouseId,
            [dto.DestinationLocationId],
            $"{transfer.TransferNumber}:add-item-destination");

        var effectiveSourceWarehouseId = sourceLocation?.InventoryWarehouseId ?? transfer.SourceWarehouseId;
        var effectiveDestinationWarehouseId = destinationLocation?.InventoryWarehouseId ?? transfer.DestinationWarehouseId;

        var sourceIsConsignment = await IsConsignmentWarehouseAsync(effectiveSourceWarehouseId);
        var destIsConsignment = await IsConsignmentWarehouseAsync(effectiveDestinationWarehouseId);
        if (sourceIsConsignment != destIsConsignment)
        {
            throw new InvalidOperationException("Transfers between owned and consignment inventory are not supported. Use a dedicated settlement/ownership conversion process.");
        }

        // Check source warehouse has sufficient stock (ownership warehouse if source bin is a consignment bin)
        var sourceQty = await _warehouseQuantityRepository.GetByWarehouseAndItemAsync(effectiveSourceWarehouseId, dto.InventoryItemId);
        if (sourceQty == null || sourceQty.AvailableStock < dto.RequestedQuantity)
            throw new InvalidOperationException($"Insufficient stock for {item.ItemCode} in source warehouse. Available: {sourceQty?.AvailableStock ?? 0}");

        // Validate bin selections (optional for inter-warehouse; required for inter-bin/same-warehouse transfers)
        if (transfer.SourceWarehouseId == transfer.DestinationWarehouseId)
        {
            EnsureInterBinLocations(dto.SourceLocationId, dto.DestinationLocationId);
        }

        // Location validation is handled above when resolving effective ownership warehouses.

        // Use AverageCost if available, otherwise fall back to StandardCost or LastPurchaseCost
        var unitCost = item.AverageCost > 0 ? item.AverageCost
            : (item.StandardCost > 0 ? item.StandardCost : item.LastPurchaseCost);

        var transferItem = new InventoryTransferItem
        {
            InventoryTransferId = transferId,
            InventoryItemId = dto.InventoryItemId,
            RequestedQuantity = dto.RequestedQuantity,
            ShippedQuantity = 0,
            ReceivedQuantity = 0,
            UnitOfMeasure = item.UnitOfMeasure,
            UnitCost = unitCost,
            SourceLocationId = dto.SourceLocationId,
            DestinationLocationId = dto.DestinationLocationId,
            LotNumber = dto.LotNumber,
            BatchNumber = dto.BatchNumber,
            SerialNumber = dto.SerialNumber,
            ManufactureDate = dto.ManufactureDate,
            ExpiryDate = dto.ExpiryDate,
            InventoryTrackingExceptionId = dto.InventoryTrackingExceptionId,
            Notes = dto.Notes,
            TenantId = _currentUserProvider.TenantId
        };

        await _transferItemRepository.AddAsync(transferItem);

        // Update transfer totals
        transfer.TotalItems += 1;
        transfer.TotalQuantity += dto.RequestedQuantity;
        transfer.TotalValue += dto.RequestedQuantity * unitCost;
        await _transferRepository.UpdateAsync(transfer);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Added item {ItemCode} to transfer {TransferNumber}", item.ItemCode, transfer.TransferNumber);

        return new InventoryTransferItemDto
        {
            Id = transferItem.Id,
            InventoryItemId = transferItem.InventoryItemId,
            ItemCode = item.ItemCode,
            ItemName = item.Name,
            RequestedQuantity = transferItem.RequestedQuantity,
            ShippedQuantity = transferItem.ShippedQuantity,
            ReceivedQuantity = transferItem.ReceivedQuantity,
            UnitOfMeasure = transferItem.UnitOfMeasure ?? string.Empty,
            UnitCost = transferItem.UnitCost,
            TotalCost = transferItem.RequestedQuantity * transferItem.UnitCost,
            LotNumber = transferItem.LotNumber,
            BatchNumber = transferItem.BatchNumber,
            SerialNumber = transferItem.SerialNumber,
            ManufactureDate = transferItem.ManufactureDate,
            ExpiryDate = transferItem.ExpiryDate,
            InventoryTrackingExceptionId = transferItem.InventoryTrackingExceptionId,
            Notes = transferItem.Notes
        };
    }

    public async Task<InventoryTransferItemDto> UpdateItemAsync(Guid transferId, Guid itemId, UpdateTransferItemDto dto, Guid userId)
    {
        var transfer = await _transferRepository.GetByIdAsync(transferId)
            ?? throw new ArgumentException($"Transfer {transferId} not found");

        if (transfer.Status != TransferStatus.Draft)
            throw new InvalidOperationException("Items can only be updated on transfers in Draft status");

        var transferItem = await _transferItemRepository.GetByIdAsync(itemId)
            ?? throw new ArgumentException($"Transfer item {itemId} not found");

        if (transferItem.InventoryTransferId != transferId)
            throw new ArgumentException("Transfer item does not belong to this transfer");

        var item = await _itemRepository.GetByIdAsync(transferItem.InventoryItemId)
            ?? throw new ArgumentException($"Inventory item not found");

        WarehouseLocation? sourceLocation = null;
        if (dto.SourceLocationId.HasValue && dto.SourceLocationId.Value != Guid.Empty)
        {
            sourceLocation = await EnsureLocationBelongsToWarehouseAsync(dto.SourceLocationId.Value, transfer.SourceWarehouseId, "SourceLocationId");
        }

        WarehouseLocation? destinationLocation = null;
        if (dto.DestinationLocationId.HasValue && dto.DestinationLocationId.Value != Guid.Empty)
        {
            destinationLocation = await EnsureLocationBelongsToWarehouseAsync(dto.DestinationLocationId.Value, transfer.DestinationWarehouseId, "DestinationLocationId");
        }

        await EnsureWarehouseLocationsAsync(
            "procurement.inventory.transfer",
            transfer.SourceWarehouseId,
            [dto.SourceLocationId],
            $"{transfer.TransferNumber}:update-item-source");
        await EnsureWarehouseLocationsAsync(
            "procurement.inventory.transfer",
            transfer.DestinationWarehouseId,
            [dto.DestinationLocationId],
            $"{transfer.TransferNumber}:update-item-destination");

        var effectiveSourceWarehouseId = sourceLocation?.InventoryWarehouseId ?? transfer.SourceWarehouseId;
        var effectiveDestinationWarehouseId = destinationLocation?.InventoryWarehouseId ?? transfer.DestinationWarehouseId;

        var sourceIsConsignment = await IsConsignmentWarehouseAsync(effectiveSourceWarehouseId);
        var destIsConsignment = await IsConsignmentWarehouseAsync(effectiveDestinationWarehouseId);
        if (sourceIsConsignment != destIsConsignment)
        {
            throw new InvalidOperationException("Transfers between owned and consignment inventory are not supported. Use a dedicated settlement/ownership conversion process.");
        }

        // Check source warehouse has sufficient stock for the new quantity
        var sourceQty = await _warehouseQuantityRepository.GetByWarehouseAndItemAsync(effectiveSourceWarehouseId, transferItem.InventoryItemId);
        if (sourceQty == null || sourceQty.AvailableStock < dto.RequestedQuantity)
            throw new InvalidOperationException($"Insufficient stock for {item.ItemCode} in source warehouse. Available: {sourceQty?.AvailableStock ?? 0}");

        // Validate bin selections (optional for inter-warehouse; required for inter-bin/same-warehouse transfers)
        if (transfer.SourceWarehouseId == transfer.DestinationWarehouseId)
        {
            EnsureInterBinLocations(dto.SourceLocationId, dto.DestinationLocationId);
        }

        // Location validation is handled above when resolving effective ownership warehouses.

        // Update transfer totals
        var qtyDiff = dto.RequestedQuantity - transferItem.RequestedQuantity;
        transfer.TotalQuantity += qtyDiff;
        transfer.TotalValue += qtyDiff * transferItem.UnitCost;

        // Update item
        transferItem.RequestedQuantity = dto.RequestedQuantity;
        transferItem.SourceLocationId = dto.SourceLocationId;
        transferItem.DestinationLocationId = dto.DestinationLocationId;
        transferItem.LotNumber = dto.LotNumber;
        transferItem.BatchNumber = dto.BatchNumber;
        transferItem.SerialNumber = dto.SerialNumber;
        transferItem.ManufactureDate = dto.ManufactureDate;
        transferItem.ExpiryDate = dto.ExpiryDate;
        transferItem.InventoryTrackingExceptionId = dto.InventoryTrackingExceptionId;
        transferItem.Notes = dto.Notes;

        await _transferItemRepository.UpdateAsync(transferItem);
        await _transferRepository.UpdateAsync(transfer);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Updated item {ItemCode} on transfer {TransferNumber}", item.ItemCode, transfer.TransferNumber);

        return new InventoryTransferItemDto
        {
            Id = transferItem.Id,
            InventoryItemId = transferItem.InventoryItemId,
            ItemCode = item.ItemCode,
            ItemName = item.Name,
            RequestedQuantity = transferItem.RequestedQuantity,
            ShippedQuantity = transferItem.ShippedQuantity,
            ReceivedQuantity = transferItem.ReceivedQuantity,
            UnitOfMeasure = transferItem.UnitOfMeasure ?? string.Empty,
            UnitCost = transferItem.UnitCost,
            TotalCost = transferItem.RequestedQuantity * transferItem.UnitCost,
            LotNumber = transferItem.LotNumber,
            BatchNumber = transferItem.BatchNumber,
            SerialNumber = transferItem.SerialNumber,
            ManufactureDate = transferItem.ManufactureDate,
            ExpiryDate = transferItem.ExpiryDate,
            InventoryTrackingExceptionId = transferItem.InventoryTrackingExceptionId,
            Notes = transferItem.Notes
        };
    }

    public async Task<bool> RemoveItemAsync(Guid transferId, Guid itemId, Guid userId)
    {
        var transfer = await _transferRepository.GetByIdAsync(transferId)
            ?? throw new ArgumentException($"Transfer {transferId} not found");

        if (transfer.Status != TransferStatus.Draft)
            throw new InvalidOperationException("Items can only be removed from transfers in Draft status");

        var transferItem = await _transferItemRepository.GetByIdAsync(itemId)
            ?? throw new ArgumentException($"Transfer item {itemId} not found");

        if (transferItem.InventoryTransferId != transferId)
            throw new ArgumentException("Transfer item does not belong to this transfer");

        await EnsureWarehouseLocationsAsync(
            "procurement.inventory.transfer",
            transfer.SourceWarehouseId,
            [transferItem.SourceLocationId],
            $"{transfer.TransferNumber}:remove-item-source");
        await EnsureWarehouseLocationsAsync(
            "procurement.inventory.transfer",
            transfer.DestinationWarehouseId,
            [transferItem.DestinationLocationId],
            $"{transfer.TransferNumber}:remove-item-destination");

        // Update transfer totals
        transfer.TotalItems -= 1;
        transfer.TotalQuantity -= transferItem.RequestedQuantity;
        transfer.TotalValue -= transferItem.RequestedQuantity * transferItem.UnitCost;

        await _transferItemRepository.DeleteAsync(transferItem);
        await _transferRepository.UpdateAsync(transfer);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Removed item from transfer {TransferNumber}", transfer.TransferNumber);
        return true;
    }

    #endregion
}
