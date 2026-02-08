using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
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
    private readonly ILogger<InventoryTransferService> _logger;

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
        _logger = logger;
    }

    public async Task<IEnumerable<InventoryTransferDto>> GetAllAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var transfers = await _transferRepository.GetByDateRangeAsync(
            fromDate ?? DateTime.UtcNow.AddMonths(-3),
            toDate ?? DateTime.UtcNow);
        return transfers.Select(MapToDto);
    }

    public async Task<IEnumerable<InventoryTransferDto>> GetByWarehouseAsync(Guid warehouseId, bool isSource = true)
    {
        var transfers = isSource
            ? await _transferRepository.GetBySourceWarehouseAsync(warehouseId)
            : await _transferRepository.GetByDestinationWarehouseAsync(warehouseId);
        return transfers.Select(MapToDto);
    }

    public async Task<IEnumerable<InventoryTransferDto>> GetInTransitAsync()
    {
        var transfers = await _transferRepository.GetInTransitAsync();
        return transfers.Select(MapToDto);
    }

    public async Task<IEnumerable<InventoryTransferDto>> GetPendingApprovalAsync()
    {
        var transfers = await _transferRepository.GetPendingApprovalAsync();
        return transfers.Select(MapToDto);
    }

    public async Task<InventoryTransferDetailDto?> GetByIdAsync(Guid id)
    {
        var transfer = await _transferRepository.GetWithItemsAsync(id);
        return transfer != null ? MapToDetailDto(transfer) : null;
    }

    public async Task<InventoryTransferDetailDto?> GetByTransferNumberAsync(string transferNumber)
    {
        var transfer = await _transferRepository.GetByTransferNumberAsync(transferNumber);
        if (transfer == null) return null;
        var fullTransfer = await _transferRepository.GetWithItemsAsync(transfer.Id);
        return fullTransfer != null ? MapToDetailDto(fullTransfer) : null;
    }

    public async Task<InventoryTransferDto> CreateAsync(CreateInventoryTransferDto dto, Guid userId)
    {
        var sourceWarehouse = await _warehouseRepository.GetByIdAsync(dto.SourceWarehouseId)
            ?? throw new ArgumentException($"Source warehouse {dto.SourceWarehouseId} not found");
        var destWarehouse = await _warehouseRepository.GetByIdAsync(dto.DestinationWarehouseId)
            ?? throw new ArgumentException($"Destination warehouse {dto.DestinationWarehouseId} not found");

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
                SerialNumber = itemDto.SerialNumber,
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
        var transfer = await _transferRepository.GetByIdAsync(transferId)
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
        var transfer = await _transferRepository.GetByIdAsync(transferId)
            ?? throw new ArgumentException($"Transfer {transferId} not found");

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
        transfer.UpdatedAt = DateTime.UtcNow;
        await _transferRepository.UpdateAsync(transfer);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Transfer {TransferNumber} submitted for approval", transfer.TransferNumber);
        return true;
    }

    public async Task<bool> ApproveAsync(Guid transferId, Guid userId, string? comments = null)
    {
        var transfer = await _transferRepository.GetByIdAsync(transferId)
            ?? throw new ArgumentException($"Transfer {transferId} not found");

        if (transfer.Status != TransferStatus.Submitted)
            throw new InvalidOperationException("Transfer must be in Submitted status");

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
        await _transferRepository.UpdateAsync(transfer);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Transfer {TransferNumber} approved", transfer.TransferNumber);
        return true;
    }

    public async Task<bool> RejectAsync(Guid transferId, string reason, Guid userId, string? comments = null)
    {
        var transfer = await _transferRepository.GetByIdAsync(transferId)
            ?? throw new ArgumentException($"Transfer {transferId} not found");

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
        await _transferRepository.UpdateAsync(transfer);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Transfer {TransferNumber} rejected: {Reason}", transfer.TransferNumber, rejectionText);
        return true;
    }

    public async Task<bool> ShipAsync(Guid transferId, Guid userId, string? trackingNumber = null, Dictionary<Guid, decimal>? shippedItems = null)
    {
        var transfer = await _transferRepository.GetWithItemsAsync(transferId)
            ?? throw new ArgumentException($"Transfer {transferId} not found");

        if (transfer.Status != TransferStatus.Approved && transfer.Status != TransferStatus.InTransit)
            throw new InvalidOperationException("Transfer must be approved or in transit to ship items");

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
            if (shippedItems != null && shippedItems.TryGetValue(item.Id, out var requestedShipQty))
            {
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

            var sourceQty = await _warehouseQuantityRepository.GetByWarehouseAndItemAsync(transfer.SourceWarehouseId, item.InventoryItemId);
            if (sourceQty == null || sourceQty.AvailableStock < quantityToShip)
                throw new InvalidOperationException($"Insufficient stock for item {item.InventoryItemId}");

            // Bin-level tracking: if a source location is specified, it must have sufficient stock.
            // This is required for inter-bin transfers and optional for inter-warehouse transfers.
            if (item.SourceLocationId.HasValue && item.SourceLocationId.Value != Guid.Empty)
            {
                await EnsureLocationBelongsToWarehouseAsync(item.SourceLocationId.Value, transfer.SourceWarehouseId, "SourceLocationId");
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

            item.ShippedQuantity += quantityToShip;
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
                WarehouseId = transfer.SourceWarehouseId,
                LocationId = item.SourceLocationId,
                Notes = $"Transfer to {transfer.DestinationWarehouse?.Name}",
                ProcessedById = userId,
                RunningBalance = sourceQty.CurrentStock,
                TenantId = _currentUserProvider.TenantId
            };
            await _stockMovementRepository.AddAsync(outboundMovement);
            await _consignmentSettlementService.TryCreateFromStockMovementAsync(outboundMovement);
        }

        // Check if all items are fully shipped
        var allItemsFullyShipped = transfer.Items.All(i => i.ShippedQuantity >= i.RequestedQuantity);

        // Only change to InTransit if all items are fully shipped
        // Otherwise keep as Approved to allow more partial shipments
        if (allItemsFullyShipped)
        {
            transfer.Status = TransferStatus.InTransit;
        }

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
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Transfer {TransferNumber} shipped (fully: {FullyShipped})", transfer.TransferNumber, allItemsFullyShipped);
        return true;
    }

    public async Task<bool> ShipWithCostsAsync(Guid transferId, Guid userId, ShipTransferWithCostsDto costsDto)
    {
        var transfer = await _transferRepository.GetWithItemsAsync(transferId)
            ?? throw new ArgumentException($"Transfer {transferId} not found");

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

        // Calculate total additional costs
        var totalAdditionalCost = costsDto.ShippingCost + costsDto.MiscellaneousCost;

        // Store shipping costs on transfer
        transfer.ShippingCost = costsDto.ShippingCost;
        transfer.MiscellaneousCost = costsDto.MiscellaneousCost;
        transfer.MiscellaneousCostDescription = costsDto.MiscellaneousCostDescription;
        transfer.TotalAdditionalCost = totalAdditionalCost;
        transfer.CostAllocationMethod = costsDto.CostAllocationMethod;
        transfer.CostApportionmentBasis = costsDto.CostApportionmentBasis;
        transfer.ExpenseGLAccount = costsDto.ExpenseGLAccount;

        // Handle shipped quantities (if provided)
        Dictionary<Guid, decimal>? shippedItems = null;
        if (costsDto.Items != null && costsDto.Items.Any())
        {
            shippedItems = costsDto.Items.ToDictionary(i => i.ItemId, i => i.ShippedQuantity);
        }

        // Perform the shipment
        var shipmentResult = await ShipAsync(transferId, userId, costsDto.TrackingNumber, shippedItems);
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

        _logger.LogInformation("Transfer {TransferNumber} shipped with costs. Method: {Method}, Total Cost: {Cost}",
            transfer.TransferNumber, costsDto.CostAllocationMethod, totalAdditionalCost);

        return true;
    }

    public async Task<bool> SaveShippingCostsAsync(Guid transferId, Guid userId, ShipTransferWithCostsDto costsDto)
    {
        var transfer = await _transferRepository.GetWithItemsAsync(transferId)
            ?? throw new ArgumentException($"Transfer {transferId} not found");

        // Allow saving costs on Approved or InTransit transfers (before receiving)
        if (transfer.Status != TransferStatus.Approved && transfer.Status != TransferStatus.InTransit)
            throw new InvalidOperationException("Shipping costs can only be saved on approved or in-transit transfers");

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

        // Calculate total additional costs
        var totalAdditionalCost = costsDto.ShippingCost + costsDto.MiscellaneousCost;

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

        // Note: CostsAllocated remains false until actual shipment
        await _transferRepository.UpdateAsync(transfer);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Saved shipping costs for transfer {TransferNumber}. Method: {Method}, Total Cost: {Cost}",
            transfer.TransferNumber, costsDto.CostAllocationMethod, totalAdditionalCost);

        return true;
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

    public async Task<bool> ReceiveAsync(Guid transferId, Guid userId, List<InventoryTransferItemDto>? receivedItems = null)
    {
        var transfer = await _transferRepository.GetWithItemsAsync(transferId)
            ?? throw new ArgumentException($"Transfer {transferId} not found");

        if (transfer.Status != TransferStatus.InTransit)
            throw new InvalidOperationException("Transfer must be in transit to receive");

        foreach (var item in transfer.Items)
        {
            // Same-warehouse transfers behave like inter-bin transfers: require valid bin selections.
            if (transfer.SourceWarehouseId == transfer.DestinationWarehouseId)
            {
                EnsureInterBinLocations(item.SourceLocationId, item.DestinationLocationId);
            }

            var receivedQty = receivedItems?.FirstOrDefault(r => r.Id == item.Id)?.ReceivedQuantity ?? item.ShippedQuantity;

            // Add to destination warehouse
            var destQty = await _warehouseQuantityRepository.GetByWarehouseAndItemAsync(transfer.DestinationWarehouseId, item.InventoryItemId);
            if (destQty == null)
            {
                destQty = new WarehouseQuantity
                {
                    WarehouseId = transfer.DestinationWarehouseId,
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

            // Bin-level tracking: if a destination location is specified, add stock there.
            // This is required for inter-bin transfers and optional for inter-warehouse transfers.
            if (item.DestinationLocationId.HasValue && item.DestinationLocationId.Value != Guid.Empty)
            {
                await EnsureLocationBelongsToWarehouseAsync(item.DestinationLocationId.Value, transfer.DestinationWarehouseId, "DestinationLocationId");
                await AdjustInventoryLocationQuantityAsync(item.DestinationLocationId.Value, item.InventoryItemId, receivedQty);
            }
            else if (transfer.SourceWarehouseId == transfer.DestinationWarehouseId)
            {
                throw new InvalidOperationException("DestinationLocationId is required for same-warehouse (inter-bin) transfers.");
            }

            // Clear allocated from source
            var sourceQty = await _warehouseQuantityRepository.GetByWarehouseAndItemAsync(transfer.SourceWarehouseId, item.InventoryItemId);
            if (sourceQty != null)
            {
                sourceQty.AllocatedStock -= item.ShippedQuantity;
                await _warehouseQuantityRepository.UpdateAsync(sourceQty);
            }

            item.ReceivedQuantity = receivedQty;
            item.DamagedQuantity = item.ShippedQuantity - receivedQty;
            await _transferItemRepository.UpdateAsync(item);

            // Create inbound movement
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
                WarehouseId = transfer.DestinationWarehouseId,
                LocationId = item.DestinationLocationId,
                Notes = $"Transfer from {transfer.SourceWarehouse?.Name}",
                ProcessedById = userId,
                RunningBalance = destQty.CurrentStock,
                TenantId = _currentUserProvider.TenantId
            };
            await _stockMovementRepository.AddAsync(inboundMovement);
            await _consignmentSettlementService.TryCreateFromStockMovementAsync(inboundMovement);
        }

        transfer.Status = TransferStatus.Received;
        transfer.ReceivedDate = DateTime.UtcNow;
        transfer.ReceivedById = userId;
        await _transferRepository.UpdateAsync(transfer);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Transfer {TransferNumber} received", transfer.TransferNumber);
        return true;
    }

    public async Task<bool> CancelAsync(Guid transferId, string reason, Guid userId)
    {
        var transfer = await _transferRepository.GetByIdAsync(transferId)
            ?? throw new ArgumentException($"Transfer {transferId} not found");

        if (transfer.Status == TransferStatus.InTransit || transfer.Status == TransferStatus.Received)
            throw new InvalidOperationException("Cannot cancel a transfer that is in transit or received");

        transfer.Status = TransferStatus.Cancelled;
        transfer.Notes = $"{transfer.Notes}\nCancelled: {reason}";
        await _transferRepository.UpdateAsync(transfer);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Transfer {TransferNumber} cancelled: {Reason}", transfer.TransferNumber, reason);
        return true;
    }

    public async Task<bool> ReverseShipmentAsync(Guid transferId, string reason, Guid userId)
    {
        var transfer = await _transferRepository.GetWithItemsAsync(transferId)
            ?? throw new ArgumentException($"Transfer {transferId} not found");

        if (transfer.Status != TransferStatus.InTransit)
            throw new InvalidOperationException("Only transfers that are in transit can be reversed. Once received, a transfer cannot be reversed.");

        // Reverse the stock movements - add back to source warehouse
        foreach (var item in transfer.Items)
        {
            if (item.ShippedQuantity <= 0)
                continue;

            var sourceQty = await _warehouseQuantityRepository.GetByWarehouseAndItemAsync(transfer.SourceWarehouseId, item.InventoryItemId);
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
                WarehouseId = transfer.SourceWarehouseId,
                LocationId = item.SourceLocationId,
                Notes = $"Shipment reversal: {reason}",
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
        transfer.Notes = $"{transfer.Notes}\nShipment reversed and cancelled on {DateTime.UtcNow:yyyy-MM-dd HH:mm}: {reason}";
        await _transferRepository.UpdateAsync(transfer);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Transfer {TransferNumber} shipment reversed and cancelled: {Reason}", transfer.TransferNumber, reason);
        return true;
    }

    #region Private Methods

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

    private async Task EnsureLocationBelongsToWarehouseAsync(Guid locationId, Guid warehouseId, string fieldName)
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
            Items = transfer.Items.Select(i => new InventoryTransferItemDto
            {
                Id = i.Id,
                InventoryItemId = i.InventoryItemId,
                ItemCode = i.InventoryItem?.ItemCode ?? string.Empty,
                ItemName = i.InventoryItem?.Name ?? string.Empty,
                RequestedQuantity = i.RequestedQuantity,
                ShippedQuantity = i.ShippedQuantity,
                ReceivedQuantity = i.ReceivedQuantity,
                UnitOfMeasure = i.UnitOfMeasure ?? string.Empty,
                UnitCost = i.UnitCost,
                TotalCost = i.RequestedQuantity * i.UnitCost,
                AllocatedCostPerUnit = i.AllocatedCostPerUnit,
                TotalAllocatedCost = i.TotalAllocatedCost,
                LandedUnitCost = i.LandedUnitCost,
                LotNumber = i.LotNumber,
                SerialNumber = i.SerialNumber,
                SourceLocationId = i.SourceLocationId,
                SourceLocationName = i.SourceLocation?.LocationCode,
                DestinationLocationId = i.DestinationLocationId,
                DestinationLocationName = i.DestinationLocation?.LocationCode,
                Notes = i.Notes
            }).ToList()
        };
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

        // Check source warehouse has sufficient stock
        var sourceQty = await _warehouseQuantityRepository.GetByWarehouseAndItemAsync(transfer.SourceWarehouseId, dto.InventoryItemId);
        if (sourceQty == null || sourceQty.AvailableStock < dto.RequestedQuantity)
            throw new InvalidOperationException($"Insufficient stock for {item.ItemCode} in source warehouse. Available: {sourceQty?.AvailableStock ?? 0}");

        // Validate bin selections (optional for inter-warehouse; required for inter-bin/same-warehouse transfers)
        if (transfer.SourceWarehouseId == transfer.DestinationWarehouseId)
        {
            EnsureInterBinLocations(dto.SourceLocationId, dto.DestinationLocationId);
        }

        if (dto.SourceLocationId.HasValue && dto.SourceLocationId.Value != Guid.Empty)
        {
            await EnsureLocationBelongsToWarehouseAsync(dto.SourceLocationId.Value, transfer.SourceWarehouseId, "SourceLocationId");
        }

        if (dto.DestinationLocationId.HasValue && dto.DestinationLocationId.Value != Guid.Empty)
        {
            await EnsureLocationBelongsToWarehouseAsync(dto.DestinationLocationId.Value, transfer.DestinationWarehouseId, "DestinationLocationId");
        }

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
            SerialNumber = dto.SerialNumber,
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
            SerialNumber = transferItem.SerialNumber,
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

        // Check source warehouse has sufficient stock for the new quantity
        var sourceQty = await _warehouseQuantityRepository.GetByWarehouseAndItemAsync(transfer.SourceWarehouseId, transferItem.InventoryItemId);
        if (sourceQty == null || sourceQty.AvailableStock < dto.RequestedQuantity)
            throw new InvalidOperationException($"Insufficient stock for {item.ItemCode} in source warehouse. Available: {sourceQty?.AvailableStock ?? 0}");

        // Validate bin selections (optional for inter-warehouse; required for inter-bin/same-warehouse transfers)
        if (transfer.SourceWarehouseId == transfer.DestinationWarehouseId)
        {
            EnsureInterBinLocations(dto.SourceLocationId, dto.DestinationLocationId);
        }

        if (dto.SourceLocationId.HasValue && dto.SourceLocationId.Value != Guid.Empty)
        {
            await EnsureLocationBelongsToWarehouseAsync(dto.SourceLocationId.Value, transfer.SourceWarehouseId, "SourceLocationId");
        }

        if (dto.DestinationLocationId.HasValue && dto.DestinationLocationId.Value != Guid.Empty)
        {
            await EnsureLocationBelongsToWarehouseAsync(dto.DestinationLocationId.Value, transfer.DestinationWarehouseId, "DestinationLocationId");
        }

        // Update transfer totals
        var qtyDiff = dto.RequestedQuantity - transferItem.RequestedQuantity;
        transfer.TotalQuantity += qtyDiff;
        transfer.TotalValue += qtyDiff * transferItem.UnitCost;

        // Update item
        transferItem.RequestedQuantity = dto.RequestedQuantity;
        transferItem.SourceLocationId = dto.SourceLocationId;
        transferItem.DestinationLocationId = dto.DestinationLocationId;
        transferItem.LotNumber = dto.LotNumber;
        transferItem.SerialNumber = dto.SerialNumber;
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
            SerialNumber = transferItem.SerialNumber,
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
