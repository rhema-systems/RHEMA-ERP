using System.Data;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Inventory;

/// <summary>
/// Governed outbound supplier returns for stock that was accepted on an inventory GRN.
/// Finance/AP remains the owner of a debit note or payment adjustment; this service owns
/// the physical-stock lifecycle and its GRN lineage.
/// </summary>
public sealed class PurchaseReturnService : IPurchaseReturnService
{
    private const string WorkflowEntityType = "PurchaseReturn";
    private readonly IPurchaseReturnRepository _returns;
    private readonly IGoodsReceiptNoteRepository _grns;
    private readonly IInventoryItemRepository _items;
    private readonly IWarehouseRepository _warehouses;
    private readonly IWarehouseQuantityRepository _warehouseQuantities;
    private readonly IInventoryLocationRepository _locations;
    private readonly IStockMovementRepository _movements;
    private readonly IConsignmentSettlementService _consignment;
    private readonly IProcurementAccessControlService _access;
    private readonly IWorkflowIntegrationService _workflow;
    private readonly IProcurementControlEventService _events;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly ILogger<PurchaseReturnService> _logger;

    public PurchaseReturnService(
        IPurchaseReturnRepository returns,
        IGoodsReceiptNoteRepository grns,
        IInventoryItemRepository items,
        IWarehouseRepository warehouses,
        IWarehouseQuantityRepository warehouseQuantities,
        IInventoryLocationRepository locations,
        IStockMovementRepository movements,
        IConsignmentSettlementService consignment,
        IProcurementAccessControlService access,
        IWorkflowIntegrationService workflow,
        IProcurementControlEventService events,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        ILogger<PurchaseReturnService> logger)
    {
        _returns = returns;
        _grns = grns;
        _items = items;
        _warehouses = warehouses;
        _warehouseQuantities = warehouseQuantities;
        _locations = locations;
        _movements = movements;
        _consignment = consignment;
        _access = access;
        _workflow = workflow;
        _events = events;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<IEnumerable<PurchaseReturnDto>> GetAllAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        await RequireReadAsync();
        var query = _returns.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.Warehouse)
            .Include(item => item.RequestedBy)
            .Include(item => item.ApprovedBy)
            .AsNoTracking();
        if (fromDate.HasValue) query = query.Where(item => item.ReturnDate >= fromDate.Value.Date);
        if (toDate.HasValue) query = query.Where(item => item.ReturnDate < toDate.Value.Date.AddDays(1));
        return (await query.OrderByDescending(item => item.ReturnDate).ThenByDescending(item => item.CreatedAt).ToListAsync())
            .Select(Map);
    }

    public async Task<IEnumerable<PurchaseReturnDto>> GetBySupplierAsync(Guid supplierId)
    {
        await RequireReadAsync();
        return (await _returns.GetBySupplierAsync(supplierId))
            .Where(item => item.TenantId == _currentUser.TenantId).Select(Map);
    }

    public async Task<IEnumerable<PurchaseReturnDto>> GetByWarehouseAsync(Guid warehouseId)
    {
        await RequireReadAsync();
        await RequireCapabilityAsync("procurement.inventory.read", warehouseId, null, "Read", warehouseId.ToString("N"));
        return (await _returns.GetByWarehouseAsync(warehouseId))
            .Where(item => item.TenantId == _currentUser.TenantId).Select(Map);
    }

    public async Task<IEnumerable<PurchaseReturnDto>> GetPendingApprovalAsync()
    {
        await RequireReadAsync();
        return (await _returns.GetPendingApprovalAsync())
            .Where(item => item.TenantId == _currentUser.TenantId).Select(Map);
    }

    public async Task<PurchaseReturnDetailDto?> GetByIdAsync(Guid id)
    {
        var value = await RequireReturnAsync(id);
        await RequireCapabilityAsync("procurement.inventory.read", value.WarehouseId, null, "Read", value.ReturnNumber);
        return MapDetail(value);
    }

    public async Task<PurchaseReturnDetailDto?> GetByReturnNumberAsync(string returnNumber)
    {
        var value = await _returns.GetByReturnNumberAsync(returnNumber);
        if (value is null || value.TenantId != _currentUser.TenantId) return null;
        await RequireCapabilityAsync("procurement.inventory.read", value.WarehouseId, null, "Read", value.ReturnNumber);
        return MapDetail(await RequireReturnAsync(value.Id));
    }

    public async Task<PurchaseReturnDto> CreateAsync(CreatePurchaseReturnDto dto, Guid userId)
    {
        EnsureActor(userId);
        if (dto.GoodsReceiptNoteId is null || dto.GoodsReceiptNoteId == Guid.Empty)
            throw Validation("INV_SUPPLIER_RETURN_GRN_REQUIRED", "Select the accepted goods receipt note to return stock to the supplier.");
        if (dto.Items is null || dto.Items.Count == 0)
            throw Validation("INV_SUPPLIER_RETURN_LINES_REQUIRED", "Select at least one accepted GRN line to return.");
        if (dto.Items.Any(item => item.ReturnQuantity <= 0))
            throw Validation("INV_SUPPLIER_RETURN_QUANTITY_REQUIRED", "Every supplier-return quantity must be greater than zero.");
        if (dto.Items.GroupBy(item => item.GRNItemId).Any(group => !group.Key.HasValue || group.Count() > 1))
            throw Validation("INV_SUPPLIER_RETURN_DUPLICATE_LINE", "Each selected GRN line may appear only once in a supplier return.");

        var grn = await _grns.GetWithItemsAsync(dto.GoodsReceiptNoteId.Value)
            ?? throw NotFound("INV_SUPPLIER_RETURN_GRN_NOT_FOUND", "The selected goods receipt note was not found.");
        if (grn.TenantId != _currentUser.TenantId || grn.IsDeleted)
            throw NotFound("INV_SUPPLIER_RETURN_GRN_NOT_FOUND", "The selected goods receipt note was not found.");
        if (grn.Status != GRNStatus.StockUpdated || !grn.StockUpdated)
            throw Conflict("INV_SUPPLIER_RETURN_GRN_NOT_RECEIPTED", "Only an accepted GRN whose stock has been updated can be returned to its supplier.");
        if (!grn.SupplierId.HasValue || grn.SupplierId != dto.SupplierId || grn.WarehouseId != dto.WarehouseId)
            throw Validation("INV_SUPPLIER_RETURN_SOURCE_MISMATCH", "Supplier and warehouse are derived from the selected GRN and cannot be changed.");

        await RequireCapabilityAsync("procurement.inventory.issue", grn.WarehouseId, null, "Create", grn.GRNNumber);
        var previous = await PriorReturnQuantitiesAsync(grn.Id);
        var created = new PurchaseReturn
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUser.TenantId,
            ReturnNumber = $"SRT-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..30],
            GoodsReceiptNoteId = grn.Id,
            GRNNumber = grn.GRNNumber,
            PurchaseOrderId = grn.PurchaseOrderId,
            PurchaseOrderNumber = grn.PurchaseOrderNumber,
            SupplierId = grn.SupplierId.Value,
            SupplierName = grn.SupplierName,
            WarehouseId = grn.WarehouseId,
            ReturnDate = DateTime.UtcNow,
            ReturnReason = NormalizeReason(dto.ReturnReason),
            ReturnReasonDetails = Normalize(dto.Notes, 2000),
            Description = $"Supplier return from GRN {grn.GRNNumber}",
            Notes = Normalize(dto.Notes, 2000),
            RequestedById = userId,
            Status = "Draft",
            CreatedAt = DateTime.UtcNow,
            CreatedById = userId,
            CreatedBy = _currentUser.Username
        };

        foreach (var input in dto.Items)
        {
            var source = grn.Items.SingleOrDefault(item => item.Id == input.GRNItemId!.Value)
                ?? throw Validation("INV_SUPPLIER_RETURN_LINE_NOT_FOUND", "A selected source line does not belong to the selected GRN.");
            if (source.AcceptedQuantity <= 0)
                throw Validation("INV_SUPPLIER_RETURN_LINE_NOT_ACCEPTED", $"{source.ItemCode} has no accepted quantity available for a supplier return.");
            var alreadyReserved = previous.GetValueOrDefault(source.Id);
            if (input.ReturnQuantity > source.AcceptedQuantity - alreadyReserved)
                throw Conflict("INV_SUPPLIER_RETURN_EXCEEDS_RECEIVED", $"Return quantity exceeds the remaining accepted quantity for {source.ItemCode}.");

            created.Items.Add(new PurchaseReturnItem
            {
                Id = Guid.NewGuid(),
                TenantId = created.TenantId,
                InventoryItemId = source.InventoryItemId,
                GoodsReceiptNoteItemId = source.Id,
                ItemCode = source.ItemCode,
                ItemName = source.ItemName,
                ReceivedQuantity = source.AcceptedQuantity,
                ReturnQuantity = input.ReturnQuantity,
                UnitCost = source.UnitCost,
                LineValue = decimal.Round(source.UnitCost * input.ReturnQuantity, 2),
                UnitOfMeasure = source.UnitOfMeasure,
                LocationId = source.StorageLocationId,
                SerialNumber = source.SerialNumber,
                LotNumber = source.LotNumber,
                BatchNumber = source.BatchNumber,
                ReturnReason = NormalizeReason(input.ReturnReason, created.ReturnReason),
                ReturnReasonDetails = Normalize(input.Notes, 1000),
                Notes = Normalize(input.Notes, 1000),
                CreatedAt = DateTime.UtcNow,
                CreatedById = userId,
                CreatedBy = _currentUser.Username
            });
        }
        created.TotalItems = created.Items.Count;
        created.TotalQuantity = created.Items.Sum(item => item.ReturnQuantity);
        created.TotalValue = created.Items.Sum(item => item.LineValue);

        await _returns.AddAsync(created);
        await _unitOfWork.SaveChangesAsync();
        await RecordAsync(created, "Create", ProcurementControlEventResult.Allowed, "Draft supplier return created from an accepted GRN.");
        _logger.LogInformation("Created supplier return {ReturnNumber} from GRN {GrnNumber}", created.ReturnNumber, grn.GRNNumber);
        return Map(created);
    }

    public async Task<bool> SubmitForApprovalAsync(Guid returnId, Guid userId)
    {
        EnsureActor(userId);
        var value = await RequireReturnAsync(returnId);
        await RequireCapabilityAsync("procurement.inventory.issue", value.WarehouseId, null, "Submit", value.ReturnNumber);
        if (value.Status != "Draft") throw Conflict("INV_SUPPLIER_RETURN_STATE", "Only a draft supplier return can be submitted.");
        var workflow = await _workflow.SubmitAsync(WorkflowEntityType, returnId);
        if (!workflow.ExecutionResult.Success)
            throw Conflict("INV_SUPPLIER_RETURN_WORKFLOW_SUBMIT", workflow.ExecutionResult.Message ?? "The supplier-return workflow could not be started.");
        value.Status = workflow.Outcome switch
        {
            WorkflowOutcome.Approved => "Approved",
            WorkflowOutcome.Rejected => "Rejected",
            _ => "Submitted"
        };
        if (value.Status == "Approved") { value.ApprovedById = userId; value.ApprovedDate = DateTime.UtcNow; }
        await _returns.UpdateAsync(value);
        await _unitOfWork.SaveChangesAsync();
        await RecordAsync(value, "Submit", workflow.Outcome == WorkflowOutcome.Rejected ? ProcurementControlEventResult.Rejected : ProcurementControlEventResult.Allowed,
            "Supplier return submitted to the shared workflow.");
        return true;
    }

    public async Task<bool> ApproveAsync(Guid returnId, Guid userId)
    {
        EnsureActor(userId);
        var value = await RequireReturnAsync(returnId);
        await RequireCapabilityAsync("procurement.inventory.adjust.approve", value.WarehouseId, null, "Approve", value.ReturnNumber);
        if (value.Status != "Submitted") throw Conflict("INV_SUPPLIER_RETURN_STATE", "Only a submitted supplier return can be approved.");
        if (value.RequestedById == userId) throw Forbidden("INV_SUPPLIER_RETURN_SOD", "The supplier-return requester cannot approve the same return.");
        if (!await _workflow.CanUserApproveAsync(WorkflowEntityType, returnId, userId))
            throw Forbidden("INV_SUPPLIER_RETURN_WORKFLOW_FORBIDDEN", "You are not assigned to the active supplier-return workflow step.");
        var result = await _workflow.ProcessApprovalAsync(WorkflowEntityType, returnId, userId, "Approve");
        if (!result.ExecutionResult.Success || result.Outcome != WorkflowOutcome.Approved)
            throw Conflict("INV_SUPPLIER_RETURN_WORKFLOW_APPROVAL", result.ExecutionResult.Message ?? "The supplier-return workflow did not approve this return.");
        value.Status = "Approved";
        value.ApprovedById = userId;
        value.ApprovedDate = DateTime.UtcNow;
        await _returns.UpdateAsync(value);
        await _unitOfWork.SaveChangesAsync();
        await RecordAsync(value, "Approve", ProcurementControlEventResult.Allowed, "Independent supplier-return approval recorded.");
        return true;
    }

    public async Task<bool> RejectAsync(Guid returnId, string reason, Guid userId)
    {
        EnsureActor(userId);
        var value = await RequireReturnAsync(returnId);
        await RequireCapabilityAsync("procurement.inventory.adjust.approve", value.WarehouseId, null, "Reject", value.ReturnNumber);
        if (value.Status != "Submitted") throw Conflict("INV_SUPPLIER_RETURN_STATE", "Only a submitted supplier return can be rejected.");
        if (value.RequestedById == userId) throw Forbidden("INV_SUPPLIER_RETURN_SOD", "The supplier-return requester cannot reject the same return.");
        if (string.IsNullOrWhiteSpace(reason)) throw Validation("INV_SUPPLIER_RETURN_REJECTION_REASON", "A rejection reason is required.");
        if (!await _workflow.CanUserApproveAsync(WorkflowEntityType, returnId, userId))
            throw Forbidden("INV_SUPPLIER_RETURN_WORKFLOW_FORBIDDEN", "You are not assigned to the active supplier-return workflow step.");
        var result = await _workflow.ProcessApprovalAsync(WorkflowEntityType, returnId, userId, "Reject", reason.Trim());
        if (!result.ExecutionResult.Success || result.Outcome != WorkflowOutcome.Rejected)
            throw Conflict("INV_SUPPLIER_RETURN_WORKFLOW_REJECTION", result.ExecutionResult.Message ?? "The supplier-return workflow did not reject this return.");
        value.Status = "Rejected";
        value.ReturnReasonDetails = reason.Trim();
        await _returns.UpdateAsync(value);
        await _unitOfWork.SaveChangesAsync();
        await RecordAsync(value, "Reject", ProcurementControlEventResult.Rejected, reason.Trim());
        return true;
    }

    public async Task<bool> ShipAsync(Guid returnId, Guid userId, string? trackingNumber = null)
    {
        EnsureActor(userId);
        return await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                await _unitOfWork.AcquireTransactionLockAsync($"supplier-return:{_currentUser.TenantId:N}:{returnId:N}");
                var value = await RequireReturnAsync(returnId);
                await RequireCapabilityAsync("procurement.inventory.issue", value.WarehouseId, null, "Ship", value.ReturnNumber);
                if (value.Status != "Approved") throw Conflict("INV_SUPPLIER_RETURN_STATE", "Only an approved supplier return can be dispatched.");
                if (value.ApprovedById == userId) throw Forbidden("INV_SUPPLIER_RETURN_SOD", "The supplier-return approver cannot dispatch the same return.");

                foreach (var line in value.Items)
                    await DispatchLineAsync(value, line, userId);
                value.Status = "Shipped";
                value.ShippedDate = DateTime.UtcNow;
                value.TrackingNumber = Normalize(trackingNumber, 100);
                await _returns.UpdateAsync(value);
                await _unitOfWork.SaveChangesAsync();
                await RecordAsync(value, "Ship", ProcurementControlEventResult.Allowed, "Approved goods dispatched back to supplier.");
                await _unitOfWork.CommitAsync();
                return true;
            }
            catch
            {
                if (_unitOfWork.HasActiveTransaction) await _unitOfWork.RollbackAsync();
                _unitOfWork.ClearTrackedChanges();
                throw;
            }
        });
    }

    public async Task<bool> RecordCreditNoteAsync(Guid returnId, string creditNoteNumber, decimal amount, Guid userId)
    {
        EnsureActor(userId);
        var value = await RequireReturnAsync(returnId);
        await RequireCapabilityAsync("procurement.inventory.adjust.approve", value.WarehouseId, null, "RecordCredit", value.ReturnNumber);
        if (value.Status != "Shipped") throw Conflict("INV_SUPPLIER_RETURN_STATE", "A supplier credit can be recorded only after dispatch.");
        if (string.IsNullOrWhiteSpace(creditNoteNumber) || amount <= 0)
            throw Validation("INV_SUPPLIER_RETURN_CREDIT_REQUIRED", "A supplier credit reference and positive amount are required.");
        value.CreditNoteNumber = creditNoteNumber.Trim();
        value.CreditAmount = amount;
        value.AcknowledgedDate = DateTime.UtcNow;
        value.Status = "Completed";
        await _returns.UpdateAsync(value);
        await _unitOfWork.SaveChangesAsync();
        await RecordAsync(value, "RecordCredit", ProcurementControlEventResult.Allowed,
            "Supplier credit was recorded; Finance/AP remains responsible for the debit-note and ledger treatment.");
        return true;
    }

    public async Task<bool> CancelAsync(Guid returnId, string reason, Guid userId)
    {
        EnsureActor(userId);
        var value = await RequireReturnAsync(returnId);
        await RequireCapabilityAsync("procurement.inventory.issue", value.WarehouseId, null, "Cancel", value.ReturnNumber);
        if (value.Status is not ("Draft" or "Submitted"))
            throw Conflict("INV_SUPPLIER_RETURN_STATE", "Only a draft or submitted supplier return can be cancelled.");
        if (string.IsNullOrWhiteSpace(reason)) throw Validation("INV_SUPPLIER_RETURN_CANCEL_REASON", "A cancellation reason is required.");
        if (value.Status == "Submitted")
        {
            var cancelled = await _workflow.CancelWorkflowAsync(WorkflowEntityType, value.Id, reason.Trim());
            if (!cancelled.Success) throw Conflict("INV_SUPPLIER_RETURN_WORKFLOW_CANCEL", cancelled.Message ?? "The supplier-return workflow could not be cancelled.");
        }
        value.Status = "Cancelled";
        value.Notes = Combine(value.Notes, $"Cancelled: {reason.Trim()}");
        await _returns.UpdateAsync(value);
        await _unitOfWork.SaveChangesAsync();
        await RecordAsync(value, "Cancel", ProcurementControlEventResult.Rejected, reason.Trim());
        return true;
    }

    private async Task DispatchLineAsync(PurchaseReturn parent, PurchaseReturnItem line, Guid userId)
    {
        var warehouse = await _warehouses.GetByIdAsync(parent.WarehouseId)
            ?? throw NotFound("INV_SUPPLIER_RETURN_WAREHOUSE_NOT_FOUND", "The return warehouse was not found.");
        var quantity = await _warehouseQuantities.GetByWarehouseAndItemAsync(parent.WarehouseId, line.InventoryItemId)
            ?? throw Conflict("INV_SUPPLIER_RETURN_STOCK_MISSING", $"Warehouse stock is not available for {line.ItemCode}.");
        if (quantity.AvailableStock < line.ReturnQuantity)
            throw Conflict("INV_SUPPLIER_RETURN_STOCK_INSUFFICIENT", $"Available stock is insufficient to dispatch {line.ItemCode} to the supplier.");
        InventoryLocation? location = null;
        if (line.LocationId.HasValue)
        {
            location = await _locations.GetByLocationAndItemAsync(line.LocationId.Value, line.InventoryItemId);
            if (location is not null && location.Quantity < line.ReturnQuantity)
                throw Conflict("INV_SUPPLIER_RETURN_LOCATION_STOCK_INSUFFICIENT", $"The selected location does not hold enough {line.ItemCode} for the supplier return.");
        }
        var item = await _items.GetByIdAsync(line.InventoryItemId)
            ?? throw NotFound("INV_SUPPLIER_RETURN_ITEM_NOT_FOUND", "An inventory item on the return was not found.");

        quantity.CurrentStock -= line.ReturnQuantity;
        quantity.AvailableStock = quantity.CurrentStock - quantity.AllocatedStock;
        quantity.LastMovementDate = DateTime.UtcNow;
        if (quantity.CurrentStock < 0 || quantity.AvailableStock < 0)
            throw Conflict("INV_SUPPLIER_RETURN_NEGATIVE_STOCK", "Supplier return would create a negative warehouse balance.");
        await _warehouseQuantities.UpdateAsync(quantity);
        if (location is not null)
        {
            location.Quantity -= line.ReturnQuantity;
            location.LastMovementDate = DateTime.UtcNow;
            await _locations.UpdateAsync(location);
        }
        if (!warehouse.IsConsignmentWarehouse)
        {
            item.CurrentStock -= line.ReturnQuantity;
            item.AvailableStock = item.CurrentStock - item.AllocatedStock;
            item.LastStockDate = DateTime.UtcNow;
            if (item.CurrentStock < 0 || item.AvailableStock < 0)
                throw Conflict("INV_SUPPLIER_RETURN_NEGATIVE_STOCK", "Supplier return would create a negative inventory balance.");
            await _items.UpdateAsync(item);
        }
        line.StockReversed = true;
        line.StockReversedAt = DateTime.UtcNow;
        await _unitOfWork.Repository<PurchaseReturnItem>().UpdateAsync(line);
        var movement = new StockMovement
        {
            Id = Guid.NewGuid(), TenantId = parent.TenantId, InventoryItemId = line.InventoryItemId,
            WarehouseId = parent.WarehouseId, LocationId = line.LocationId, MovementType = "SupplierReturn",
            Quantity = -line.ReturnQuantity, UnitCost = line.UnitCost, TotalValue = -line.LineValue,
            MovementDate = DateTime.UtcNow, ReferenceType = ReferenceType.Return,
            ReferenceId = parent.Id, ReferenceNumber = parent.ReturnNumber, LotNumber = line.LotNumber,
            BatchNumber = line.BatchNumber, SerialNumber = line.SerialNumber, Notes = parent.ReturnReasonDetails,
            ProcessedById = userId, RunningBalance = warehouse.IsConsignmentWarehouse ? quantity.CurrentStock : item.CurrentStock
        };
        await _movements.AddAsync(movement);
        await _consignment.TryCreateFromStockMovementAsync(movement);
    }

    private async Task<Dictionary<Guid, decimal>> PriorReturnQuantitiesAsync(Guid grnId)
        => await _unitOfWork.Repository<PurchaseReturnItem>().GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
                item.PurchaseReturn.GoodsReceiptNoteId == grnId && item.PurchaseReturn.Status != "Cancelled")
            .GroupBy(item => item.GoodsReceiptNoteItemId!.Value)
            .Select(group => new { Id = group.Key, Quantity = group.Sum(item => item.ReturnQuantity) })
            .ToDictionaryAsync(item => item.Id, item => item.Quantity);

    private async Task<PurchaseReturn> RequireReturnAsync(Guid id)
    {
        var value = await _returns.GetWithItemsAsync(id);
        if (value is null || value.TenantId != _currentUser.TenantId)
            throw NotFound("INV_SUPPLIER_RETURN_NOT_FOUND", "The supplier return was not found.");
        return value;
    }

    private Task RequireReadAsync() => RequireCapabilityAsync("procurement.inventory.read", null, null, "Read", "supplier-returns");

    private async Task RequireCapabilityAsync(string permission, Guid? warehouseId, Guid? locationId, string action, string reference)
    {
        var decision = await _access.EnforceCapabilityAsync(new ProcurementAccessCapabilityRequest
        {
            PermissionCode = permission, WarehouseId = warehouseId, LocationId = locationId,
            RequireLocationScope = false, SourceType = "PurchaseReturn", SourceReference = reference
        }, $"supplier-return:{action.ToLowerInvariant()}:{reference}");
        if (!decision.Allowed) throw Forbidden("INV_SUPPLIER_RETURN_FORBIDDEN", decision.Message);
    }

    private async Task RecordAsync(PurchaseReturn value, string action, ProcurementControlEventResult result, string reason)
    {
        await _events.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = $"supplier-return:{value.Id:N}:{action.ToLowerInvariant()}:{value.Status.ToLowerInvariant()}",
            EventType = "SupplierReturn", Action = action, Result = result, RuleCode = "INV-SUPPLIER-RETURN",
            SourceType = "PurchaseReturn", SourceId = value.Id, SourceReference = value.ReturnNumber,
            Reason = reason, InputValues = new { value.GoodsReceiptNoteId, value.SupplierId, value.WarehouseId },
            ResultValues = new { value.Status, value.TotalQuantity, value.TotalValue, value.CreditNoteNumber, value.CreditAmount },
            CorrelationId = $"supplier-return:{value.Id:N}", OccurredAtUtc = DateTime.UtcNow
        });
    }

    private void EnsureActor(Guid userId)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty || userId != _currentUser.UserId)
            throw Forbidden("INV_SUPPLIER_RETURN_ACTOR_INVALID", "The supplier-return actor does not match the authenticated user.");
    }

    private static PurchaseReturnDto Map(PurchaseReturn value) => new()
    {
        Id = value.Id, ReturnNumber = value.ReturnNumber, ReturnDate = value.ReturnDate, SupplierId = value.SupplierId,
        SupplierName = value.SupplierName ?? string.Empty, WarehouseId = value.WarehouseId,
        WarehouseName = value.Warehouse?.Name ?? string.Empty, GoodsReceiptNoteId = value.GoodsReceiptNoteId,
        GRNNumber = value.GRNNumber, Status = value.Status, ReturnReason = value.ReturnReason,
        TotalItems = value.TotalItems, TotalQuantity = value.TotalQuantity, TotalValue = value.TotalValue,
        RequestedByName = DisplayName(value.RequestedBy), ApprovedByName = DisplayName(value.ApprovedBy), Notes = value.Notes,
        CreatedAtFormatted = value.CreatedAt.ToLocalTime().ToString("dd MMM yyyy, HH:mm")
    };

    private static PurchaseReturnDetailDto MapDetail(PurchaseReturn value) => new()
    {
        Id = Map(value).Id, ReturnNumber = value.ReturnNumber, ReturnDate = value.ReturnDate, SupplierId = value.SupplierId,
        SupplierName = value.SupplierName ?? string.Empty, WarehouseId = value.WarehouseId, WarehouseName = value.Warehouse?.Name ?? string.Empty,
        GoodsReceiptNoteId = value.GoodsReceiptNoteId, GRNNumber = value.GRNNumber, Status = value.Status,
        ReturnReason = value.ReturnReason, TotalItems = value.TotalItems, TotalQuantity = value.TotalQuantity,
        TotalValue = value.TotalValue, RequestedByName = DisplayName(value.RequestedBy), ApprovedByName = DisplayName(value.ApprovedBy),
        Notes = value.Notes, CreatedAtFormatted = value.CreatedAt.ToLocalTime().ToString("dd MMM yyyy, HH:mm"),
        ApprovedDate = value.ApprovedDate, ShippedDate = value.ShippedDate, TrackingNumber = value.TrackingNumber,
        CreditNoteNumber = value.CreditNoteNumber, CreditNoteAmount = value.CreditAmount,
        Items = value.Items.Select(item => new PurchaseReturnItemDto
        {
            Id = item.Id, InventoryItemId = item.InventoryItemId, ItemCode = item.ItemCode ?? string.Empty,
            ItemName = item.ItemName ?? string.Empty, ReturnQuantity = item.ReturnQuantity,
            UnitOfMeasure = item.UnitOfMeasure ?? string.Empty, UnitCost = item.UnitCost, TotalCost = item.LineValue,
            ReturnReason = item.ReturnReason ?? value.ReturnReason, LotNumber = item.LotNumber,
            SerialNumber = item.SerialNumber, GRNItemId = item.GoodsReceiptNoteItemId, Notes = item.Notes
        }).ToList()
    };

    private static string DisplayName(ApplicationUser? user) => user is null ? string.Empty :
        $"{user.FirstName} {user.LastName}".Trim() is { Length: > 0 } name ? name : user.UserName ?? string.Empty;
    private static string NormalizeReason(string? value, string? fallback = null)
    {
        var normalized = (string.IsNullOrWhiteSpace(value) ? fallback : value)?.Trim() ?? string.Empty;
        var allowed = new[] { "Quality", "Damage", "Excess", "Wrong", "Other" };
        if (!allowed.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            throw Validation("INV_SUPPLIER_RETURN_REASON_INVALID", "Select a controlled supplier-return reason.");
        return allowed.Single(item => string.Equals(item, normalized, StringComparison.OrdinalIgnoreCase));
    }
    private static string? Normalize(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, max)];
    private static string Combine(string? current, string addition) => string.IsNullOrWhiteSpace(current) ? addition : $"{current}\n{addition}";
    private static InventorySupplierReturnException Validation(string code, string message) => new(code, message, 422);
    private static InventorySupplierReturnException Conflict(string code, string message) => new(code, message, 409);
    private static InventorySupplierReturnException NotFound(string code, string message) => new(code, message, 404);
    private static InventorySupplierReturnException Forbidden(string code, string message) => new(code, message, 403);
}

public sealed class InventorySupplierReturnException(string code, string message, int statusCode) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}
