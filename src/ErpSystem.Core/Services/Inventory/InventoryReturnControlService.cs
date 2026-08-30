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
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.DocumentManagement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Inventory;

public sealed class InventoryReturnControlService : IInventoryReturnControlService
{
    private const string WorkflowEntityType = "InventoryReturnVoucher";
    private readonly IUnitOfWork _unitOfWork;
    private readonly IInventoryRequisitionRepository _requisitions;
    private readonly IInventoryRequisitionItemRepository _requisitionItems;
    private readonly IInventoryItemRepository _items;
    private readonly IWarehouseRepository _warehouses;
    private readonly IWarehouseLocationRepository _locations;
    private readonly IWarehouseQuantityRepository _warehouseQuantities;
    private readonly IStockMovementRepository _movements;
    private readonly IConsignmentSettlementService _consignmentSettlement;
    private readonly IInventoryTrackingControlService _tracking;
    private readonly IProcurementAccessControlService _access;
    private readonly IProcurementSodGuardService _sod;
    private readonly IWorkflowIntegrationService _workflow;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly IProjectService _projects;
    private readonly IInventoryIssueFinanceAssetService _issueFinanceAssets;
    private readonly ICurrentUserProvider _currentUser;

    public InventoryReturnControlService(
        IUnitOfWork unitOfWork,
        IInventoryRequisitionRepository requisitions,
        IInventoryRequisitionItemRepository requisitionItems,
        IInventoryItemRepository items,
        IWarehouseRepository warehouses,
        IWarehouseLocationRepository locations,
        IWarehouseQuantityRepository warehouseQuantities,
        IStockMovementRepository movements,
        IConsignmentSettlementService consignmentSettlement,
        IInventoryTrackingControlService tracking,
        IProcurementAccessControlService access,
        IProcurementSodGuardService sod,
        IWorkflowIntegrationService workflow,
        IProcurementControlEventService controlEvents,
        IProjectService projects,
        IInventoryIssueFinanceAssetService issueFinanceAssets,
        ICurrentUserProvider currentUser)
    {
        _unitOfWork = unitOfWork;
        _requisitions = requisitions;
        _requisitionItems = requisitionItems;
        _items = items;
        _warehouses = warehouses;
        _locations = locations;
        _warehouseQuantities = warehouseQuantities;
        _movements = movements;
        _consignmentSettlement = consignmentSettlement;
        _tracking = tracking;
        _access = access;
        _sod = sod;
        _workflow = workflow;
        _controlEvents = controlEvents;
        _projects = projects;
        _issueFinanceAssets = issueFinanceAssets;
        _currentUser = currentUser;
    }

    public Task<IReadOnlyList<InventoryReturnVoucherDto>> GetAsync(Guid? requisitionId = null, CancellationToken cancellationToken = default) =>
        GetListAsync(requisitionId, cancellationToken);

    public async Task<InventoryReturnVoucherDto?> GetAsync(Guid voucherId, CancellationToken cancellationToken = default)
    {
        var voucher = await Query().AsNoTracking().FirstOrDefaultAsync(x => x.Id == voucherId, cancellationToken);
        if (voucher is null) return null;
        await RequireAccessAsync("procurement.inventory.read", voucher.WarehouseId,
            voucher.Lines.Select(x => x.LocationId), voucher.VoucherNumber, cancellationToken);
        return Map(voucher);
    }

    public async Task<InventoryReturnVoucherDto> RequestAsync(Guid requisitionId, ReturnRequisitionDto request, CancellationToken cancellationToken = default)
    {
        var key = Required(request.IdempotencyKey, "INV_RETURN_IDEMPOTENCY_REQUIRED", "An idempotency key is required.", 100);
        var correlation = Normalize(request.CorrelationId, 100) ?? $"inventory-return:{Guid.NewGuid():N}";
        var reasonCode = Required(request.ReasonCode, "INV_RETURN_REASON_REQUIRED", "A controlled return reason code is required.", 50).ToUpperInvariant();
        if (!InventoryReturnReasonCodes.All.ContainsKey(reasonCode))
            throw Validation("INV_RETURN_REASON_INVALID", "The selected return reason code is not supported.");
        var reason = Required(request.Reason, "INV_RETURN_REASON_DETAIL_REQUIRED", "A return reason is required.", 1000);
        if (request.Items.Count == 0 || request.Items.Any(x => x.ReturnedQuantity <= 0))
            throw Validation("INV_RETURN_LINES_REQUIRED", "At least one positive return line is required.");
        if (request.Items.GroupBy(x => new
            {
                x.ItemId,
                x.LocationId,
                LotNumber = TrackingKey(x.LotNumber),
                BatchNumber = TrackingKey(x.BatchNumber),
                SerialNumber = TrackingKey(x.SerialNumber)
            }).Any(x => x.Count() > 1))
            throw Validation("INV_RETURN_DUPLICATE_TRACKING_LINE",
                "The same requisition line and tracking identity may appear only once in a return voucher.");

        var payloadHash = Hash(JsonSerializer.Serialize(new
        {
            requisitionId,
            reasonCode,
            reason,
            request.Notes,
            Lines = request.Items.OrderBy(x => x.ItemId).Select(x => new
            {
                x.ItemId, x.ReturnedQuantity, x.LocationId, x.LotNumber, x.BatchNumber, x.SerialNumber,
                x.ManufactureDate, x.ExpiryDate, x.InventoryTrackingExceptionId
            }),
            Evidence = request.Evidence.OrderBy(x => x.CentralDocumentVersionId)
                .Select(x => new { x.CentralDocumentVersionId, Reference = x.EvidenceReference.Trim() })
        }));

        return await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            var ownsTransaction = !_unitOfWork.HasActiveTransaction;
            if (ownsTransaction)
                await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try
            {
                await _unitOfWork.AcquireTransactionLockAsync($"inventory-return:{_currentUser.TenantId:N}:{requisitionId:N}", cancellationToken);
                var replay = await Query().FirstOrDefaultAsync(x => x.IdempotencyKey == key, cancellationToken);
                if (replay is not null)
                {
                    if (!string.Equals(replay.PayloadHash, payloadHash, StringComparison.Ordinal))
                        throw Conflict("INV_RETURN_IDEMPOTENCY_CONFLICT", "The idempotency key was already used with different return content.");
                    if (ownsTransaction) await _unitOfWork.CommitAsync(cancellationToken);
                    return Map(replay);
                }

                var requisition = await _requisitions.GetWithItemsAsync(requisitionId)
                    ?? throw NotFound("INV_RETURN_REQUISITION_NOT_FOUND", "The requisition was not found in the current tenant.");
                if (requisition.TenantId != _currentUser.TenantId)
                    throw NotFound("INV_RETURN_REQUISITION_NOT_FOUND", "The requisition was not found in the current tenant.");
                if (requisition.Status is not (RequisitionStatus.PartiallyIssued or RequisitionStatus.Issued or RequisitionStatus.Completed))
                    throw Conflict("INV_RETURN_SOURCE_STATE", "Only requisitions with issued stock can start a controlled return.");
                EnsureRowVersion(requisition.RowVersion, request.RowVersion, "INV_RETURN_REQUISITION_STALE");
                _ = await _warehouses.GetByIdAsync(requisition.WarehouseId)
                    ?? throw NotFound("INV_RETURN_WAREHOUSE_NOT_FOUND", "The requisition warehouse no longer exists.");

                var pending = await _unitOfWork.Repository<InventoryReturnVoucherLine>().GetQueryable().AsNoTracking()
                    .Where(x => x.InventoryReturnVoucher.InventoryRequisitionId == requisitionId &&
                        (x.InventoryReturnVoucher.Status == InventoryReturnVoucherStatus.PendingApproval ||
                         x.InventoryReturnVoucher.Status == InventoryReturnVoucherStatus.Approved))
                    .GroupBy(x => x.InventoryRequisitionItemId)
                    .Select(x => new { Id = x.Key, Quantity = x.Sum(y => y.Quantity) })
                    .ToDictionaryAsync(x => x.Id, x => x.Quantity, cancellationToken);

                var voucher = new InventoryReturnVoucher
                {
                    TenantId = _currentUser.TenantId,
                    VoucherNumber = $"SRV-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..26].ToUpperInvariant(),
                    InventoryRequisitionId = requisition.Id,
                    WarehouseId = requisition.WarehouseId,
                    RequestedById = _currentUser.UserId,
                    ReasonCode = reasonCode,
                    Reason = reason,
                    Notes = Normalize(request.Notes, 2000),
                    IdempotencyKey = key,
                    PayloadHash = payloadHash,
                    CorrelationId = correlation,
                    Status = InventoryReturnVoucherStatus.PendingApproval
                };

                foreach (var group in request.Items.GroupBy(value => value.ItemId))
                {
                    var source = requisition.Items.FirstOrDefault(value => value.Id == group.Key)
                        ?? throw Validation("INV_RETURN_LINE_NOT_FOUND", $"Requisition line {group.Key} was not found.");
                    var reserved = pending.GetValueOrDefault(source.Id);
                    if (group.Sum(value => value.ReturnedQuantity) > source.IssuedQuantity - reserved)
                        throw Conflict("INV_RETURN_EXCEEDS_ISSUED",
                            $"Return quantity exceeds unreserved issued quantity for {source.ItemCode}.");
                }

                foreach (var input in request.Items)
                {
                    var source = requisition.Items.FirstOrDefault(x => x.Id == input.ItemId)
                        ?? throw Validation("INV_RETURN_LINE_NOT_FOUND", $"Requisition line {input.ItemId} was not found.");
                    var locationId = input.LocationId ?? source.LocationId ?? requisition.LocationId;
                    await ValidateLocationAsync(requisition.WarehouseId, locationId, cancellationToken);
                    if (source.UnitCost <= 0)
                        throw Validation("INV_RETURN_COST_REQUIRED", $"A server-derived issue cost is required for {source.ItemCode}.");

                    var line = new InventoryReturnVoucherLine
                    {
                        TenantId = voucher.TenantId,
                        InventoryRequisitionItemId = source.Id,
                        InventoryItemId = source.InventoryItemId,
                        LocationId = locationId,
                        Quantity = input.ReturnedQuantity,
                        UnitCost = source.UnitCost,
                        TotalValue = decimal.Round(input.ReturnedQuantity * source.UnitCost, 2),
                        LotNumber = Normalize(input.LotNumber, 100) ?? source.LotNumber,
                        BatchNumber = Normalize(input.BatchNumber, 100) ?? source.BatchNumber,
                        SerialNumber = Normalize(input.SerialNumber, 100) ?? source.SerialNumber,
                        ManufactureDate = input.ManufactureDate ?? source.ManufactureDate,
                        ExpiryDate = input.ExpiryDate ?? source.ExpiryDate,
                        InventoryTrackingExceptionId = input.InventoryTrackingExceptionId ?? source.InventoryTrackingExceptionId
                    };
                    line.IntegrityHash = LineHash(line);
                    voucher.Lines.Add(line);
                    voucher.TotalValue += line.TotalValue;
                }

                await RequireAccessAsync("procurement.inventory.issue", requisition.WarehouseId,
                    voucher.Lines.Select(x => x.LocationId), voucher.VoucherNumber, cancellationToken);
                var evidence = await ValidateEvidenceAsync(request.Evidence, reasonCode is InventoryReturnReasonCodes.Defective or InventoryReturnReasonCodes.Other, cancellationToken);
                foreach (var item in evidence)
                {
                    voucher.Evidence.Add(new InventoryReturnVoucherEvidence
                    {
                        TenantId = voucher.TenantId,
                        CentralDocumentVersionId = item.Version.Id,
                        FileUploadRecordId = item.Version.FileUploadRecordId!.Value,
                        EvidenceReference = item.Reference,
                        IntegrityHash = Hash($"{voucher.Id:N}|{item.Version.Id:N}|{item.Version.FileUploadRecordId:N}|{item.Reference}")
                    });
                }

                voucher.IntegrityHash = VoucherHash(voucher);
                await _unitOfWork.Repository<InventoryReturnVoucher>().AddAsync(voucher);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await AddActionAsync(voucher, InventoryReturnVoucherActionType.Submitted, key, reason, cancellationToken);
                await AddAuditAsync("Submit", voucher, null, Snapshot(voucher), cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                var workflow = await _workflow.SubmitAsync(WorkflowEntityType, voucher.Id);
                if (!workflow.ExecutionResult.Success)
                    throw Conflict("INV_RETURN_WORKFLOW_SUBMIT_FAILED", workflow.ExecutionResult.Message ?? "The return approval workflow could not be started.");
                if (workflow.Outcome != WorkflowOutcome.Pending || !workflow.ExecutionResult.WorkflowInstanceId.HasValue)
                    throw Conflict("INV_RETURN_INDEPENDENT_APPROVAL_REQUIRED", "The return workflow must contain an independent approval step.");
                voucher.WorkflowInstanceId = workflow.ExecutionResult.WorkflowInstanceId;
                voucher.IntegrityHash = VoucherHash(voucher);
                await _unitOfWork.Repository<InventoryReturnVoucher>().UpdateAsync(voucher);
                await RecordEventAsync(voucher, "Submit", ProcurementControlEventResult.Allowed, null, Snapshot(voucher), cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                if (ownsTransaction) await _unitOfWork.CommitAsync(cancellationToken);
                return Map(await Query().AsNoTracking().FirstAsync(x => x.Id == voucher.Id, cancellationToken));
            }
            catch
            {
                if (ownsTransaction && _unitOfWork.HasActiveTransaction)
                {
                    await _unitOfWork.RollbackAsync(cancellationToken);
                    _unitOfWork.ClearTrackedChanges();
                }
                throw;
            }
        }, cancellationToken);
    }

    public async Task<InventoryReturnVoucherDto> DecideAsync(Guid voucherId, DecideInventoryReturnVoucherRequest request, CancellationToken cancellationToken = default)
    {
        var key = Required(request.IdempotencyKey, "INV_RETURN_DECISION_KEY_REQUIRED", "A decision idempotency key is required.", 100);
        return await ExecuteAsync(voucherId, async voucher =>
        {
            var actionType = request.Approved ? InventoryReturnVoucherActionType.Approved : InventoryReturnVoucherActionType.Rejected;
            await RequireAccessAsync("procurement.inventory.adjust.approve", voucher.WarehouseId,
                voucher.Lines.Select(x => x.LocationId), voucher.VoucherNumber, cancellationToken);
            var replayHash = ReturnActionPayloadHash(voucher.Id, actionType, _currentUser.UserId, request.Comment);
            if (await ReplayActionAsync(voucher, key, actionType, replayHash, cancellationToken)) return voucher;
            EnsureRowVersion(voucher.RowVersion, request.RowVersion, "INV_RETURN_STALE");
            if (voucher.Status != InventoryReturnVoucherStatus.PendingApproval)
                throw Conflict("INV_RETURN_STATE_CONFLICT", $"The Store Return Voucher is already {voucher.Status}.");
            if (voucher.RequestedById == _currentUser.UserId)
                throw new InventoryReturnAuthorizationException("The return requester cannot approve or reject the same return.");
            await _sod.EnforceAsync(new ProcurementSodGuardRequest
            {
                ControlCode = "SOD-INITIATOR-APPROVER",
                SourceType = WorkflowEntityType,
                SourceReference = voucher.VoucherNumber,
                ProhibitedActorUserIds = new List<Guid> { voucher.RequestedById }
            }, voucher.CorrelationId, cancellationToken);
            if (!await _workflow.CanUserApproveAsync(WorkflowEntityType, voucher.Id, _currentUser.UserId))
                throw new InventoryReturnAuthorizationException("The current actor is not eligible for the active return workflow step.");
            await RevalidateEvidenceAsync(voucher, cancellationToken);
            var before = Snapshot(voucher);
            var result = await _workflow.ProcessApprovalAsync(WorkflowEntityType, voucher.Id, _currentUser.UserId,
                request.Approved ? "Approve" : "Reject", request.Comment);
            if (!result.ExecutionResult.Success)
                throw Conflict("INV_RETURN_WORKFLOW_DECISION_FAILED", result.ExecutionResult.Message ?? "The return workflow decision failed.");
            if (result.Outcome == WorkflowOutcome.Pending) return voucher;
            if (request.Approved && result.Outcome != WorkflowOutcome.Approved)
                throw Conflict("INV_RETURN_WORKFLOW_REJECTED", "The shared workflow rejected the return.");
            if (!request.Approved && result.Outcome == WorkflowOutcome.Approved)
                throw Conflict("INV_RETURN_WORKFLOW_ALREADY_APPROVED", "An approved workflow cannot be recorded as rejected.");

            if (request.Approved)
            {
                voucher.Status = InventoryReturnVoucherStatus.Approved;
                voucher.ApprovedById = _currentUser.UserId;
                voucher.ApprovedAtUtc = DateTime.UtcNow;
            }
            else
            {
                voucher.Status = InventoryReturnVoucherStatus.Rejected;
                voucher.RejectedById = _currentUser.UserId;
                voucher.RejectedAtUtc = DateTime.UtcNow;
                voucher.RejectionReason = Required(request.Comment, "INV_RETURN_REJECTION_REASON_REQUIRED", "A rejection reason is required.", 1000);
            }
            voucher.IntegrityHash = VoucherHash(voucher);
            // Persist the controlled lifecycle state before appending its immutable action.
            // SQL validates each action against the durable voucher state; both saves remain
            // inside this serializable transaction and therefore still commit or roll back together.
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await AddActionAsync(voucher, actionType, key, request.Comment, cancellationToken);
            await AddAuditAsync(request.Approved ? "Approve" : "Reject", voucher, before, Snapshot(voucher), cancellationToken);
            await RecordEventAsync(voucher, request.Approved ? "Approve" : "Reject",
                request.Approved ? ProcurementControlEventResult.Allowed : ProcurementControlEventResult.Rejected, before, Snapshot(voucher), cancellationToken);
            return voucher;
        }, cancellationToken);
    }

    public Task<InventoryReturnVoucherDto> PostAsync(Guid voucherId, PostInventoryReturnVoucherRequest request, CancellationToken cancellationToken = default) =>
        ExecuteAsync(voucherId, async voucher =>
        {
            var key = Required(request.IdempotencyKey, "INV_RETURN_POST_KEY_REQUIRED", "A posting idempotency key is required.", 100);
            await RequireAccessAsync("procurement.inventory.adjust.approve", voucher.WarehouseId,
                voucher.Lines.Select(x => x.LocationId), voucher.VoucherNumber, cancellationToken);
            var replayHash = ReturnActionPayloadHash(voucher.Id, InventoryReturnVoucherActionType.Posted,
                _currentUser.UserId, "Returned stock posted.");
            if (await ReplayActionAsync(voucher, key, InventoryReturnVoucherActionType.Posted, replayHash, cancellationToken))
                return voucher;
            EnsureRowVersion(voucher.RowVersion, request.RowVersion, "INV_RETURN_STALE");
            if (voucher.Status != InventoryReturnVoucherStatus.Approved)
                throw Conflict("INV_RETURN_STATE_CONFLICT", $"The Store Return Voucher is already {voucher.Status}.");
            if (voucher.RequestedById == _currentUser.UserId)
                throw new InventoryReturnAuthorizationException("The return requester cannot post the same return.");
            await RevalidateEvidenceAsync(voucher, cancellationToken);
            var requisition = await _requisitions.GetWithItemsAsync(voucher.InventoryRequisitionId)
                ?? throw NotFound("INV_RETURN_REQUISITION_NOT_FOUND", "The return source requisition no longer exists.");
            var before = Snapshot(voucher);
            voucher.Status = InventoryReturnVoucherStatus.Posted;
            voucher.PostedById = _currentUser.UserId;
            voucher.PostedAtUtc = DateTime.UtcNow;
            voucher.IntegrityHash = VoucherHash(voucher);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await AddActionAsync(voucher, InventoryReturnVoucherActionType.Posted, key, "Returned stock posted.", cancellationToken);
            await AddAuditAsync("Post", voucher, before, Snapshot(voucher), cancellationToken);
            await RecordEventAsync(voucher, "Post", ProcurementControlEventResult.Allowed, before, Snapshot(voucher), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            foreach (var line in voucher.Lines.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id))
                await ApplyLineAsync(voucher, requisition, line, reverse: false, cancellationToken);
            ApplyRequisitionStatus(requisition);
            await _requisitions.UpdateAsync(requisition);
            await _issueFinanceAssets.PostReturnAsync(voucher.Id, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (requisition.ProjectId.HasValue) await _projects.SyncInventoryRequisitionMaterialCostAsync(requisition.Id);
            return voucher;
        }, cancellationToken);

    public Task<InventoryReturnVoucherDto> ReverseAsync(Guid voucherId, ReverseInventoryReturnVoucherRequest request, CancellationToken cancellationToken = default) =>
        ExecuteAsync(voucherId, async voucher =>
        {
            var key = Required(request.IdempotencyKey, "INV_RETURN_REVERSAL_KEY_REQUIRED", "A reversal idempotency key is required.", 100);
            var reason = Required(request.Reason, "INV_RETURN_REVERSAL_REASON_REQUIRED", "A reversal reason is required.", 1000);
            await RequireAccessAsync("procurement.inventory.adjust.approve", voucher.WarehouseId,
                voucher.Lines.Select(x => x.LocationId), voucher.VoucherNumber, cancellationToken);
            var replayHash = ReturnActionPayloadHash(voucher.Id, InventoryReturnVoucherActionType.Reversed,
                _currentUser.UserId, reason);
            if (await ReplayActionAsync(voucher, key, InventoryReturnVoucherActionType.Reversed, replayHash, cancellationToken))
                return voucher;
            EnsureRowVersion(voucher.RowVersion, request.RowVersion, "INV_RETURN_STALE");
            if (voucher.Status != InventoryReturnVoucherStatus.Posted)
                throw Conflict("INV_RETURN_STATE_CONFLICT", $"The Store Return Voucher is already {voucher.Status}.");
            if (voucher.RequestedById == _currentUser.UserId)
                throw new InventoryReturnAuthorizationException("The return requester cannot reverse the same return.");
            var requisition = await _requisitions.GetWithItemsAsync(voucher.InventoryRequisitionId)
                ?? throw NotFound("INV_RETURN_REQUISITION_NOT_FOUND", "The return source requisition no longer exists.");
            var before = Snapshot(voucher);
            voucher.Status = InventoryReturnVoucherStatus.Reversed;
            voucher.ReversedById = _currentUser.UserId;
            voucher.ReversedAtUtc = DateTime.UtcNow;
            voucher.ReversalReason = reason;
            voucher.IntegrityHash = VoucherHash(voucher);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await AddActionAsync(voucher, InventoryReturnVoucherActionType.Reversed, key, reason, cancellationToken);
            await AddAuditAsync("Reverse", voucher, before, Snapshot(voucher), cancellationToken);
            await RecordEventAsync(voucher, "Reverse", ProcurementControlEventResult.Allowed, before, Snapshot(voucher), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            foreach (var line in voucher.Lines.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id))
                await ApplyLineAsync(voucher, requisition, line, reverse: true, cancellationToken);
            ApplyRequisitionStatus(requisition);
            await _requisitions.UpdateAsync(requisition);
            await _issueFinanceAssets.ReverseReturnAsync(voucher.Id, reason, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (requisition.ProjectId.HasValue) await _projects.SyncInventoryRequisitionMaterialCostAsync(requisition.Id);
            return voucher;
        }, cancellationToken);

    private async Task<InventoryReturnVoucherDto> ExecuteAsync(Guid voucherId, Func<InventoryReturnVoucher, Task<InventoryReturnVoucher>> action, CancellationToken cancellationToken)
    {
        return await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try
            {
                await _unitOfWork.AcquireTransactionLockAsync($"inventory-return:{_currentUser.TenantId:N}:{voucherId:N}", cancellationToken);
                var voucher = await Query().FirstOrDefaultAsync(x => x.Id == voucherId, cancellationToken)
                    ?? throw NotFound("INV_RETURN_VOUCHER_NOT_FOUND", "The Store Return Voucher was not found in the current tenant.");
                voucher = await action(voucher);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitAsync(cancellationToken);
                return Map(await Query().AsNoTracking().FirstAsync(x => x.Id == voucher.Id, cancellationToken));
            }
            catch
            {
                if (_unitOfWork.HasActiveTransaction) await _unitOfWork.RollbackAsync(cancellationToken);
                _unitOfWork.ClearTrackedChanges();
                throw;
            }
        }, cancellationToken);
    }

    private async Task ApplyLineAsync(InventoryReturnVoucher voucher, InventoryRequisition requisition, InventoryReturnVoucherLine line, bool reverse, CancellationToken cancellationToken)
    {
        var source = requisition.Items.FirstOrDefault(x => x.Id == line.InventoryRequisitionItemId)
            ?? throw Conflict("INV_RETURN_SOURCE_LINE_MISSING", "A return source line no longer exists.");
        var location = line.LocationId.HasValue ? await _locations.GetByIdAsync(line.LocationId.Value) : null;
        var warehouseId = location?.InventoryWarehouseId ?? voucher.WarehouseId;
        var warehouse = await _warehouses.GetByIdAsync(warehouseId)
            ?? throw NotFound("INV_RETURN_WAREHOUSE_NOT_FOUND", "The return warehouse no longer exists.");
        var quantity = line.Quantity;
        if (!reverse && source.IssuedQuantity < quantity)
            throw Conflict("INV_RETURN_EXCEEDS_CURRENT_ISSUED", $"Current issued quantity is insufficient for {source.ItemCode}.");
        if (reverse && source.IssuedQuantity + quantity > source.ApprovedQuantity)
            throw Conflict("INV_RETURN_REVERSAL_EXCEEDS_APPROVED", $"Reversal would exceed approved quantity for {source.ItemCode}.");
        var warehouseQuantity = await _warehouseQuantities.GetByWarehouseAndItemAsync(warehouseId, line.InventoryItemId)
            ?? throw Conflict("INV_RETURN_STOCK_RECORD_MISSING", $"Warehouse stock is missing for {source.ItemCode}.");
        if (reverse && warehouseQuantity.AvailableStock < quantity)
            throw Conflict("INV_RETURN_REVERSAL_STOCK_UNAVAILABLE", $"Available stock is insufficient to reverse {source.ItemCode}.");

        var trackingSequence = source.TrackingSequence + 1;
        await _tracking.StageEventAsync(new InventoryTrackingMutationRequest
        {
            InventoryItemId = line.InventoryItemId,
            WarehouseId = warehouseId,
            LocationId = line.LocationId,
            Direction = reverse ? InventoryTrackingDirection.Issue : InventoryTrackingDirection.Return,
            Quantity = quantity,
            ReferenceType = reverse ? "InventoryReturnVoucherReversal" : "InventoryReturnVoucher",
            ReferenceNumber = voucher.VoucherNumber,
            ReferenceId = voucher.Id,
            ReferenceLineId = line.Id,
            EventKey = $"return-voucher:{voucher.Id:N}:{line.Id:N}:{(reverse ? "reverse" : "post")}",
            LotNumber = line.LotNumber,
            BatchNumber = line.BatchNumber,
            SerialNumber = line.SerialNumber,
            ManufactureDate = line.ManufactureDate,
            ExpiryDate = line.ExpiryDate,
            TrackingExceptionId = line.InventoryTrackingExceptionId,
            CorrelationId = voucher.CorrelationId
        });

        var delta = reverse ? -quantity : quantity;
        source.IssuedQuantity -= delta;
        source.TrackingSequence = trackingSequence;
        source.LineValue = source.IssuedQuantity * source.UnitCost;
        await _requisitionItems.UpdateAsync(source);
        warehouseQuantity.CurrentStock += delta;
        warehouseQuantity.AvailableStock += delta;
        await _warehouseQuantities.UpdateAsync(warehouseQuantity);
        var exactLocationId = line.LocationId
            ?? throw Conflict("INV_RETURN_LOCATION_REQUIRED", "A Store Return Voucher line requires an exact stock location.");
        var inventoryLocationRepository = _unitOfWork.Repository<InventoryLocation>();
        var inventoryLocation = await inventoryLocationRepository.GetQueryable(value =>
                value.TenantId == voucher.TenantId &&
                value.InventoryItemId == line.InventoryItemId &&
                value.LocationId == exactLocationId && !value.IsDeleted)
            .SingleOrDefaultAsync(cancellationToken);
        var isNewInventoryLocation = inventoryLocation is null;
        if (isNewInventoryLocation)
        {
            if (reverse)
                throw Conflict("INV_RETURN_LOCATION_STOCK_MISSING", $"Exact-location stock is missing for {source.ItemCode}.");
            inventoryLocation = new InventoryLocation
            {
                TenantId = voucher.TenantId,
                InventoryItemId = line.InventoryItemId,
                LocationId = exactLocationId,
                AverageCost = line.UnitCost,
                CreatedById = _currentUser.UserId
            };
        }
        if (reverse && inventoryLocation.AvailableQuantity < quantity)
            throw Conflict("INV_RETURN_LOCATION_STOCK_UNAVAILABLE", $"Available exact-location stock is insufficient to reverse {source.ItemCode}.");
        inventoryLocation.Quantity += delta;
        inventoryLocation.AvailableQuantity = inventoryLocation.Quantity - inventoryLocation.AllocatedQuantity;
        inventoryLocation.AverageCost = line.UnitCost;
        inventoryLocation.LastMovementDate = DateTime.UtcNow;
        if (isNewInventoryLocation)
            await inventoryLocationRepository.AddAsync(inventoryLocation);
        else
            await inventoryLocationRepository.UpdateAsync(inventoryLocation);
        if (!warehouse.IsConsignmentWarehouse)
        {
            var inventoryItem = await _items.GetByIdAsync(line.InventoryItemId)
                ?? throw NotFound("INV_RETURN_ITEM_NOT_FOUND", "A return item no longer exists.");
            if (reverse && inventoryItem.AvailableStock < quantity)
                throw Conflict("INV_RETURN_REVERSAL_ITEM_STOCK_UNAVAILABLE", $"Available item stock is insufficient to reverse {source.ItemCode}.");
            inventoryItem.CurrentStock += delta;
            inventoryItem.AvailableStock += delta;
            await _items.UpdateAsync(inventoryItem);
        }

        var movement = new StockMovement
        {
            TenantId = voucher.TenantId,
            InventoryItemId = line.InventoryItemId,
            MovementType = reverse ? "ReturnReversal" : "Return",
            MovementDate = DateTime.UtcNow,
            Quantity = reverse ? -quantity : quantity,
            UnitCost = line.UnitCost,
            TotalValue = reverse ? -line.TotalValue : line.TotalValue,
            ReferenceType = ReferenceType.Requisition,
            ReferenceNumber = voucher.VoucherNumber,
            ReferenceId = voucher.InventoryRequisitionId,
            InventoryReturnVoucherId = voucher.Id,
            WarehouseId = warehouseId,
            LocationId = line.LocationId,
            LotNumber = line.LotNumber,
            BatchNumber = line.BatchNumber,
            SerialNumber = line.SerialNumber,
            ManufactureDate = line.ManufactureDate,
            ExpirationDate = line.ExpiryDate,
            InventoryTrackingExceptionId = line.InventoryTrackingExceptionId,
            Notes = reverse ? $"Reversal of Store Return Voucher {voucher.VoucherNumber}" : $"Store Return Voucher {voucher.VoucherNumber}",
            ProcessedById = _currentUser.UserId
        };
        await _movements.AddAsync(movement);
        await _consignmentSettlement.TryCreateFromStockMovementAsync(movement);
    }

    private async Task<IReadOnlyList<InventoryReturnVoucherDto>> GetListAsync(Guid? requisitionId, CancellationToken cancellationToken)
    {
        const int take = 200;
        const int pageSize = 200;
        var query = Query().AsNoTracking()
            .Where(x => !requisitionId.HasValue || x.InventoryRequisitionId == requisitionId);
        var allowed = new List<InventoryReturnVoucherDto>(take);
        var offset = 0;
        while (allowed.Count < take)
        {
            var candidates = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
                .Skip(offset).Take(pageSize).ToListAsync(cancellationToken);
            foreach (var item in candidates)
            {
                try
                {
                    await RequireAccessAsync("procurement.inventory.read", item.WarehouseId, item.Lines.Select(x => x.LocationId), item.VoucherNumber, cancellationToken);
                    allowed.Add(Map(item));
                }
                catch (InventoryReturnAuthorizationException) { }
                if (allowed.Count == take) break;
            }
            offset += candidates.Count;
            if (candidates.Count < pageSize) break;
        }
        return allowed;
    }

    private IQueryable<InventoryReturnVoucher> Query() => _unitOfWork.Repository<InventoryReturnVoucher>().GetQueryable()
        .Where(x => !x.IsDeleted && x.TenantId == _currentUser.TenantId)
        .Include(x => x.InventoryRequisition)
        .Include(x => x.Warehouse)
        .Include(x => x.RequestedBy)
        .Include(x => x.Lines).ThenInclude(x => x.InventoryItem)
        .Include(x => x.Evidence).ThenInclude(x => x.CentralDocumentVersion).ThenInclude(x => x.DocumentRecord)
        .Include(x => x.Actions).ThenInclude(x => x.ActorUser)
        .AsSplitQuery();

    private async Task RequireAccessAsync(string permission, Guid warehouseId, IEnumerable<Guid?> locations, string reference, CancellationToken cancellationToken)
    {
        var scopes = locations.Distinct().ToList();
        if (scopes.Count == 0) scopes.Add(null);
        foreach (var locationId in scopes)
        {
            var decision = await _access.EnforceCapabilityAsync(new ProcurementAccessCapabilityRequest
            {
                PermissionCode = permission,
                WarehouseId = warehouseId,
                LocationId = locationId,
                RequireLocationScope = true,
                SourceType = WorkflowEntityType,
                SourceReference = reference
            }, Guid.NewGuid().ToString("N"), cancellationToken);
            if (!decision.Allowed) throw new InventoryReturnAuthorizationException(decision.Message);
        }
    }

    private async Task ValidateLocationAsync(Guid warehouseId, Guid? locationId, CancellationToken cancellationToken)
    {
        if (!locationId.HasValue || locationId == Guid.Empty)
            throw Validation("INV_RETURN_LOCATION_REQUIRED", "A controlled return location is required.");
        var location = await _locations.GetByIdAsync(locationId.Value);
        if (location is null || location.TenantId != _currentUser.TenantId ||
            location.InventoryWarehouseId != warehouseId || !location.IsActive)
            throw Validation("INV_RETURN_LOCATION_INVALID", "The return location is inactive or outside the requisition warehouse.");
    }

    private async Task<List<(CentralDocumentVersion Version, string Reference)>> ValidateEvidenceAsync(IEnumerable<InventoryControlEvidenceRequest> requests, bool required, CancellationToken cancellationToken)
    {
        var list = requests.ToList();
        if (required && list.Count == 0)
            throw Validation("INV_RETURN_EVIDENCE_REQUIRED", "Published central-DMS evidence is required for this return reason.");
        if (list.GroupBy(x => x.CentralDocumentVersionId).Any(x => x.Count() > 1))
            throw Validation("INV_RETURN_EVIDENCE_DUPLICATE", "The same DMS version cannot be linked twice.");
        var result = new List<(CentralDocumentVersion, string)>();
        foreach (var input in list)
        {
            var reference = Required(input.EvidenceReference, "INV_RETURN_EVIDENCE_REFERENCE_REQUIRED", "An evidence reference is required.", 500);
            var version = await _unitOfWork.Repository<CentralDocumentVersion>().GetQueryable()
                .Include(x => x.DocumentRecord)
                .FirstOrDefaultAsync(x => x.Id == input.CentralDocumentVersionId && !x.IsDeleted &&
                    x.TenantId == _currentUser.TenantId && x.DocumentRecord.TenantId == _currentUser.TenantId &&
                    !x.DocumentRecord.IsDeleted && x.DocumentRecord.LifecycleStatus == CentralDocumentEvidenceRules.ActiveLifecycleStatus &&
                    x.DocumentRecord.VersionStatus == CentralDocumentEvidenceRules.PublishedVersionStatus &&
                    x.DocumentRecord.CurrentVersion == x.VersionNumber && x.Status == CentralDocumentEvidenceRules.PublishedVersionStatus &&
                    x.PublishedAt.HasValue && x.FileUploadRecordId.HasValue, cancellationToken);
            if (version is null)
                throw Validation("INV_RETURN_EVIDENCE_NOT_CURRENT", "Evidence must reference the current published version in central DMS.");
            var cleanUpload = await _unitOfWork.Repository<FileUploadRecord>().GetQueryable().AsNoTracking()
                .AnyAsync(x => x.Id == version.FileUploadRecordId!.Value &&
                    x.TenantId == _currentUser.TenantId && !x.IsDeleted &&
                    x.VirusScanStatus == FileVirusScanStatus.Clean, cancellationToken);
            if (!cleanUpload)
                throw Validation("INV_RETURN_EVIDENCE_NOT_CLEAN", "Evidence must have a successful clean malware scan.");
            result.Add((version, reference));
        }
        return result;
    }

    private async Task RevalidateEvidenceAsync(InventoryReturnVoucher voucher, CancellationToken cancellationToken)
    {
        if (voucher.ReasonCode is InventoryReturnReasonCodes.Defective or InventoryReturnReasonCodes.Other && voucher.Evidence.Count == 0)
            throw Validation("INV_RETURN_EVIDENCE_REQUIRED", "Published central-DMS evidence is required for this return reason.");
        foreach (var evidence in voucher.Evidence)
        {
            var current = await _unitOfWork.Repository<CentralDocumentVersion>().GetQueryable()
                .Include(x => x.DocumentRecord)
                .AnyAsync(x => x.Id == evidence.CentralDocumentVersionId && x.FileUploadRecordId == evidence.FileUploadRecordId &&
                    !x.IsDeleted && x.TenantId == voucher.TenantId && !x.DocumentRecord.IsDeleted &&
                    x.DocumentRecord.LifecycleStatus == CentralDocumentEvidenceRules.ActiveLifecycleStatus &&
                    x.DocumentRecord.VersionStatus == CentralDocumentEvidenceRules.PublishedVersionStatus &&
                    x.DocumentRecord.CurrentVersion == x.VersionNumber && x.Status == CentralDocumentEvidenceRules.PublishedVersionStatus && x.PublishedAt.HasValue,
                    cancellationToken);
            if (!current) throw Conflict("INV_RETURN_EVIDENCE_STALE", "Linked central-DMS evidence is no longer current and published.");
            var cleanUpload = await _unitOfWork.Repository<FileUploadRecord>().GetQueryable().AsNoTracking()
                .AnyAsync(x => x.Id == evidence.FileUploadRecordId &&
                    x.TenantId == voucher.TenantId && !x.IsDeleted &&
                    x.VirusScanStatus == FileVirusScanStatus.Clean, cancellationToken);
            if (!cleanUpload)
                throw Conflict("INV_RETURN_EVIDENCE_NOT_CLEAN", "Linked central-DMS evidence no longer has a successful clean malware scan.");
        }
    }

    private async Task AddActionAsync(InventoryReturnVoucher voucher, InventoryReturnVoucherActionType type, string key, string? comment, CancellationToken cancellationToken)
    {
        var sequence = await _unitOfWork.Repository<InventoryReturnVoucherAction>().GetQueryable().AsNoTracking()
            .CountAsync(x => x.InventoryReturnVoucherId == voucher.Id, cancellationToken) + 1;
        var action = new InventoryReturnVoucherAction
        {
            TenantId = voucher.TenantId,
            InventoryReturnVoucherId = voucher.Id,
            Sequence = sequence,
            ActionType = type,
            ActorUserId = _currentUser.UserId,
            OccurredAtUtc = DateTime.UtcNow,
            IdempotencyKey = key,
            CorrelationId = voucher.CorrelationId,
            Comment = Normalize(comment, 1000),
            SnapshotJson = JsonSerializer.Serialize(Snapshot(voucher))
        };
        action.IntegrityHash = Hash($"{action.InventoryReturnVoucherId:N}|{action.Sequence}|{(int)type}|{action.ActorUserId:N}|{action.OccurredAtUtc:O}|{action.IdempotencyKey}|{action.SnapshotJson}");
        await _unitOfWork.Repository<InventoryReturnVoucherAction>().AddAsync(action);
    }

    private async Task<bool> ReplayActionAsync(
        InventoryReturnVoucher voucher,
        string key,
        InventoryReturnVoucherActionType type,
        string payloadHash,
        CancellationToken cancellationToken)
    {
        var replay = await _unitOfWork.Repository<InventoryReturnVoucherAction>().GetQueryable().AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == voucher.TenantId && x.IdempotencyKey == key, cancellationToken);
        if (replay is null) return false;
        var recordedHash = ReturnActionPayloadHash(replay.InventoryReturnVoucherId, replay.ActionType,
            replay.ActorUserId, replay.Comment);
        if (replay.InventoryReturnVoucherId != voucher.Id || replay.ActionType != type ||
            !CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(recordedHash), Convert.FromHexString(payloadHash)))
            throw Conflict("INV_RETURN_IDEMPOTENCY_CONFLICT",
                "The idempotency key already identifies a different return action payload or actor.");
        return true;
    }

    private async Task AddAuditAsync(string action, InventoryReturnVoucher voucher, object? before, object after, CancellationToken cancellationToken) =>
        await _unitOfWork.Repository<AuditLog>().AddAsync(new AuditLog
        {
            TenantId = voucher.TenantId,
            UserId = _currentUser.UserId,
            Username = _currentUser.Username,
            Action = action,
            Resource = "InventoryReturnVoucher",
            ResourceId = voucher.Id.ToString(),
            OldValues = before is null ? null : JsonSerializer.Serialize(before),
            NewValues = JsonSerializer.Serialize(after),
            IpAddress = "system",
            Timestamp = DateTime.UtcNow
        });

    private Task RecordEventAsync(InventoryReturnVoucher voucher, string action, ProcurementControlEventResult result, object? before, object after, CancellationToken cancellationToken) =>
        _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = $"inventory-return:{voucher.Id:N}:{action.ToLowerInvariant()}:{voucher.Actions.Count + 1}",
            EventType = "InventoryReturnControl",
            Action = action,
            Result = result,
            RuleCode = "INV-011",
            DecisionKeys = Enumerable.Range(1, 14).Select(x => $"DEC-{x:000}").ToList(),
            SourceType = WorkflowEntityType,
            SourceId = voucher.Id,
            SourceReference = voucher.VoucherNumber,
            Reason = action,
            Before = before,
            After = after,
            CorrelationId = voucher.CorrelationId,
            OccurredAtUtc = DateTime.UtcNow,
            Evidence = voucher.Evidence.Select(x => new ProcurementControlEventEvidenceReference
            {
                ReferenceKind = ProcurementControlEvidenceReferenceKind.FileUploadRecord,
                ReferenceId = x.FileUploadRecordId,
                Reference = x.EvidenceReference,
                Label = "Central DMS return evidence",
                RequirementKey = "INV-011"
            }).ToList()
        }, cancellationToken);

    private static void ApplyRequisitionStatus(InventoryRequisition requisition)
    {
        var anyIssued = requisition.Items.Any(x => x.IssuedQuantity > 0);
        var allIssued = requisition.Items.All(x => x.IssuedQuantity >= x.ApprovedQuantity && x.ApprovedQuantity > 0);
        requisition.Status = allIssued ? RequisitionStatus.Issued : anyIssued ? RequisitionStatus.PartiallyIssued : RequisitionStatus.Approved;
        if (requisition.Status != RequisitionStatus.Completed) requisition.CompletedDate = null;
    }

    private static object Snapshot(InventoryReturnVoucher voucher) => new
    {
        voucher.Id, voucher.VoucherNumber, voucher.InventoryRequisitionId, voucher.WarehouseId, voucher.Status,
        voucher.ReasonCode, voucher.Reason, voucher.RequestedById, voucher.WorkflowInstanceId, voucher.ApprovedById,
        voucher.PostedById, voucher.ReversedById, voucher.TotalValue, voucher.IntegrityHash
    };

    private static string LineHash(InventoryReturnVoucherLine line) => Hash($"{line.InventoryRequisitionItemId:N}|{line.InventoryItemId:N}|{line.LocationId:N}|{line.Quantity}|{line.UnitCost}|{line.TotalValue}|{line.LotNumber}|{line.BatchNumber}|{line.SerialNumber}|{line.ExpiryDate:O}");
    private static string VoucherHash(InventoryReturnVoucher voucher) => Hash($"{voucher.Id:N}|{voucher.TenantId:N}|{voucher.VoucherNumber}|{voucher.InventoryRequisitionId:N}|{voucher.WarehouseId:N}|{voucher.RequestedById:N}|{(int)voucher.Status}|{voucher.ReasonCode}|{voucher.Reason}|{voucher.TotalValue}|{voucher.IdempotencyKey}|{voucher.PayloadHash}|{voucher.ApprovedById:N}|{voucher.PostedById:N}|{voucher.ReversedById:N}");
    private static string ReturnActionPayloadHash(Guid voucherId, InventoryReturnVoucherActionType type,
        Guid actorUserId, string? comment) => Hash(JsonSerializer.Serialize(new
        {
            VoucherId = voucherId,
            ActionType = type,
            ActorUserId = actorUserId,
            Comment = Normalize(comment, 1000)
        }));
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static void EnsureRowVersion(byte[] current, string supplied, string code)
    {
        byte[] requested;
        try { requested = Convert.FromBase64String(supplied); }
        catch { throw Conflict(code, "The row version is invalid. Reload and retry."); }
        if (!current.SequenceEqual(requested)) throw Conflict(code, "The record changed. Reload and retry.");
    }

    private static string Required(string? value, string code, string message, int max)
    {
        var normalized = Normalize(value, max);
        return normalized ?? throw Validation(code, message);
    }

    private static string? Normalize(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }

    private static string TrackingKey(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();

    private static InventoryReturnControlException Validation(string code, string message) => new(code, message);
    private static InventoryReturnControlException Conflict(string code, string message) => new(code, message);
    private static InventoryReturnControlException NotFound(string code, string message) => new(code, message);

    private static InventoryReturnVoucherDto Map(InventoryReturnVoucher voucher) => new()
    {
        Id = voucher.Id,
        VoucherNumber = voucher.VoucherNumber,
        InventoryRequisitionId = voucher.InventoryRequisitionId,
        RequisitionNumber = voucher.InventoryRequisition?.RequisitionNumber ?? string.Empty,
        WarehouseId = voucher.WarehouseId,
        WarehouseName = voucher.Warehouse?.Name ?? string.Empty,
        Status = voucher.Status.ToString(),
        ReasonCode = voucher.ReasonCode,
        Reason = voucher.Reason,
        Notes = voucher.Notes,
        RequestedById = voucher.RequestedById,
        RequestedByName = voucher.RequestedBy?.FullName ?? string.Empty,
        ApprovedById = voucher.ApprovedById,
        ApprovedAtUtc = voucher.ApprovedAtUtc,
        PostedById = voucher.PostedById,
        PostedAtUtc = voucher.PostedAtUtc,
        ReversedById = voucher.ReversedById,
        ReversedAtUtc = voucher.ReversedAtUtc,
        ReversalReason = voucher.ReversalReason,
        TotalValue = voucher.TotalValue,
        RowVersion = Convert.ToBase64String(voucher.RowVersion),
        Lines = voucher.Lines.OrderBy(x => x.CreatedAt).Select(x => new InventoryReturnVoucherLineDto
        {
            Id = x.Id,
            RequisitionItemId = x.InventoryRequisitionItemId,
            InventoryItemId = x.InventoryItemId,
            ItemCode = x.InventoryItem?.ItemCode ?? string.Empty,
            ItemName = x.InventoryItem?.Name ?? string.Empty,
            LocationId = x.LocationId,
            Quantity = x.Quantity,
            UnitCost = x.UnitCost,
            TotalValue = x.TotalValue,
            LotNumber = x.LotNumber,
            BatchNumber = x.BatchNumber,
            SerialNumber = x.SerialNumber,
            ExpiryDate = x.ExpiryDate
        }).ToList(),
        Evidence = voucher.Evidence.OrderBy(x => x.CreatedAt).Select(x => new InventoryControlEvidenceDto
        {
            Id = x.Id,
            CentralDocumentVersionId = x.CentralDocumentVersionId,
            FileUploadRecordId = x.FileUploadRecordId,
            EvidenceReference = x.EvidenceReference,
            DocumentReference = x.CentralDocumentVersion?.DocumentRecord?.DocumentReference ?? string.Empty,
            VersionNumber = x.CentralDocumentVersion?.VersionNumber ?? string.Empty
        }).ToList(),
        Actions = voucher.Actions.OrderBy(x => x.Sequence).Select(x => new InventoryReturnVoucherActionDto
        {
            Sequence = x.Sequence,
            ActionType = x.ActionType.ToString(),
            ActorUserId = x.ActorUserId,
            ActorName = x.ActorUser?.FullName ?? string.Empty,
            OccurredAtUtc = x.OccurredAtUtc,
            Comment = x.Comment
        }).ToList()
    };
}
