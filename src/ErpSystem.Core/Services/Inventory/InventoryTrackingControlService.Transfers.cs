using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;

namespace ErpSystem.Core.Services.Inventory;

public sealed partial class InventoryTrackingControlService
{
    private async Task RequireTransitAllocationCapabilityAsync(InventoryTrackingMutationRequest request, CancellationToken cancellationToken)
    {
        if (!_unitOfWork.HasActiveTransaction || request.ReferenceType != "InventoryTransfer")
            throw new InvalidOperationException("Transit tracking requires a governed transfer transaction.");
        var dispatch = await _unitOfWork.Repository<InventoryTransferDispatchAllocation>().GetQueryable()
            .Include(x => x.InventoryTransferAction).Include(x => x.InventoryTransferItem).ThenInclude(x => x.InventoryTransfer)
            .Include(x => x.InTransitLocation).Include(x => x.SourceLocation)
            .SingleOrDefaultAsync(x => x.Id == request.TransferDispatchAllocationId && x.TenantId == TenantId && !x.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("The transit tracking source allocation is unavailable.");
        var transfer = dispatch.InventoryTransferItem.InventoryTransfer;
        if (transfer.IsDeleted || dispatch.InventoryTransferItem.IsDeleted ||
            dispatch.InventoryTransferItem.TenantId != TenantId ||
            dispatch.InventoryTransferAction.TenantId != TenantId || dispatch.InventoryTransferAction.IsDeleted ||
            dispatch.InventoryTransferAction.InventoryTransferId != transfer.Id ||
            dispatch.InventoryTransferAction.ActionType != InventoryTransferActionType.Dispatched ||
            dispatch.InTransitLocation.IsDeleted || !dispatch.InTransitLocation.IsActive ||
            request.ReferenceId != transfer.Id || request.ReferenceLineId != dispatch.InventoryTransferItemId ||
            request.InventoryItemId != dispatch.InventoryTransferItem.InventoryItemId || request.Quantity > dispatch.Quantity ||
            request.LocationId != dispatch.InTransitLocationId || request.WarehouseId != dispatch.InTransitLocation.WarehouseId ||
            !dispatch.InTransitLocation.IsInTransitLocation || dispatch.InTransitLocation.TenantId != TenantId || transfer.TenantId != TenantId)
            throw new InvalidOperationException("Transit tracking does not match its retained transfer source.");
        if (!request.TransferReceiptAllocationId.HasValue)
        {
            if (request.Direction != InventoryTrackingDirection.TransferIn || dispatch.InventoryTransferAction.ActorUserId != UserId ||
                transfer.Status is not (TransferStatus.Approved or TransferStatus.InTransit) ||
                dispatch.SourceLocation.TenantId != TenantId || dispatch.SourceLocation.IsDeleted ||
                !dispatch.SourceLocation.IsActive || dispatch.SourceLocation.WarehouseId != transfer.SourceWarehouseId)
                throw new InvalidOperationException("Transit tracking inbound requires its original dispatch actor.");
            await RequireCurrentTransitActionAsync(dispatch.InventoryTransferAction, cancellationToken);
            RequireTransitSnapshotRow(request, dispatch.Id, "transit-in", dispatch.TrackingSnapshotJson);
            await RequireCapabilityAsync("procurement.inventory.transfer", transfer.SourceWarehouseId,
                transfer.TransferNumber, cancellationToken, dispatch.SourceLocationId);
            return;
        }
        var receipt = await _unitOfWork.Repository<InventoryTransferReceiptAllocation>().GetQueryable()
            .Include(x => x.InventoryTransferAction).Include(x => x.DestinationLocation)
            .SingleOrDefaultAsync(x => x.Id == request.TransferReceiptAllocationId && x.TenantId == TenantId && !x.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("The transit receipt allocation is unavailable.");
        if (receipt.DispatchAllocationId != dispatch.Id || receipt.InventoryTransferAction.ActorUserId != UserId ||
            receipt.InventoryTransferAction.TenantId != TenantId || receipt.InventoryTransferAction.IsDeleted ||
            !IsTransitReceiptLifecycleAllowed(transfer.Status, receipt.InventoryTransferAction.ActionType,
                receipt.ReturnedToSource, request.Direction) ||
            receipt.DestinationLocation.TenantId != TenantId || receipt.DestinationLocation.IsDeleted ||
            !receipt.DestinationLocation.IsActive || receipt.DestinationLocation.IsInTransitLocation ||
            receipt.DestinationLocation.WarehouseId != (receipt.ReturnedToSource ? transfer.SourceWarehouseId : transfer.DestinationWarehouseId) ||
            (receipt.ReturnedToSource && receipt.DestinationLocationId != dispatch.SourceLocationId) ||
            (receipt.ReturnedToSource && receipt.InventoryTransferAction.ActionType == InventoryTransferActionType.Received) ||
            (!receipt.ReturnedToSource && receipt.InventoryTransferAction.ActionType == InventoryTransferActionType.ShipmentReversed) ||
            receipt.InventoryTransferAction.InventoryTransferId != transfer.Id || request.Quantity > receipt.Quantity ||
            request.Direction != InventoryTrackingDirection.TransferOut ||
            receipt.InventoryTransferAction.ActionType is not (InventoryTransferActionType.Received or InventoryTransferActionType.ShipmentReversed or InventoryTransferActionType.DiscrepancyResolved))
            throw new InvalidOperationException("Transit tracking outbound requires its governed receiving actor and allocation.");
        await RequireCurrentTransitActionAsync(receipt.InventoryTransferAction, cancellationToken);
        RequireTransitSnapshotRow(request, receipt.Id, "transit-out", receipt.TrackingSnapshotJson);
        await RequireCapabilityAsync("procurement.inventory.transfer",
            receipt.ReturnedToSource ? transfer.SourceWarehouseId : transfer.DestinationWarehouseId,
            transfer.TransferNumber, cancellationToken, receipt.DestinationLocationId);
    }

    private async Task RequireCurrentTransitActionAsync(InventoryTransferAction action, CancellationToken cancellationToken)
    {
        if (await _unitOfWork.Repository<InventoryTransferAction>().GetQueryable().AnyAsync(value =>
                value.TenantId == TenantId && !value.IsDeleted && value.InventoryTransferId == action.InventoryTransferId &&
                value.Sequence > action.Sequence, cancellationToken))
            throw new InvalidOperationException("Transit tracking requires the current governed transfer action.");
    }

    private static bool IsTransitReceiptLifecycleAllowed(TransferStatus status, InventoryTransferActionType action,
        bool returnedToSource, InventoryTrackingDirection direction) =>
        direction == InventoryTrackingDirection.TransferOut && (action switch
        {
            InventoryTransferActionType.Received => status == TransferStatus.InTransit && !returnedToSource,
            InventoryTransferActionType.ShipmentReversed => status == TransferStatus.InTransit && returnedToSource,
            InventoryTransferActionType.DiscrepancyResolved => status == TransferStatus.Received,
            _ => false
        });

    private static void RequireTransitSnapshotRow(InventoryTrackingMutationRequest request, Guid allocationId, string leg, string snapshotJson)
    {
        var prefix = $"transfer-allocation:{allocationId:N}:{leg}:";
        if (!request.EventKey.StartsWith(prefix, StringComparison.Ordinal) ||
            !int.TryParse(request.EventKey.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var index) ||
            request.EventKey != prefix + index.ToString(CultureInfo.InvariantCulture))
            throw new InvalidOperationException("Transit tracking event must identify its exact retained allocation row.");
        var rows = JsonSerializer.Deserialize<List<InventoryTransactionScanLineDto>>(snapshotJson);
        if (rows is null || index <= 0 || index > rows.Count)
            throw new InvalidOperationException("The retained transit tracking snapshot is invalid.");
        var row = rows[index - 1];
        if (row.InventoryItemId != request.InventoryItemId || row.DocumentLineId != request.ReferenceLineId ||
            row.BaseQuantity != request.Quantity || !Same(row.LotNumber, request.LotNumber) ||
            !Same(row.BatchNumber, request.BatchNumber) || !Same(row.SerialNumber, request.SerialNumber) ||
            row.ManufactureDate?.ToUniversalTime() != request.ManufactureDate?.ToUniversalTime() ||
            row.ExpiryDate?.ToUniversalTime() != request.ExpiryDate?.ToUniversalTime() ||
            row.InventoryTrackingExceptionId != request.TrackingExceptionId)
            throw new InvalidOperationException("Transit tracking quantity and identity must exactly match the retained allocation row.");
    }
}
