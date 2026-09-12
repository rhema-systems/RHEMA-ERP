using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Inventory;

public sealed partial class PurchaseReturnService
{
    public async Task<IEnumerable<GoodsReceiptNoteDetailDto>> GetSourceGrnsAsync()
    {
        await RequireReadAsync();
        var candidates = await _grns.GetQueryable(value => value.TenantId == _currentUser.TenantId && !value.IsDeleted && value.StockUpdated)
            .Include(value => value.Warehouse).Include(value => value.Items).ThenInclude(value => value.StorageLocation)
            .AsNoTracking().OrderByDescending(value => value.ReceiptDate).ToListAsync();
        var results = new List<GoodsReceiptNoteDetailDto>();
        foreach (var candidate in candidates)
        {
            if (!await CanAsync("procurement.inventory.read", candidate.WarehouseId)) continue;
            var grn = await ProjectAcceptedSourceAsync(candidate);
            if (grn is null) continue;
            results.Add(new GoodsReceiptNoteDetailDto
            {
                Id = grn.Id, GRNNumber = grn.GRNNumber, SupplierId = grn.SupplierId, SupplierName = grn.SupplierName,
                WarehouseId = grn.WarehouseId, WarehouseName = candidate.Warehouse?.Name ?? string.Empty,
                Status = GRNStatus.StockUpdated, ReceiptDate = grn.ReceiptDate,
                Items = grn.Items.Where(line => !line.IsDeleted && line.AcceptedQuantity > 0).Select(line => new GoodsReceiptNoteItemDto
                {
                    Id = line.Id, InventoryItemId = line.InventoryItemId, ItemCode = line.ItemCode ?? string.Empty,
                    ItemName = line.ItemName ?? string.Empty, AcceptedQuantity = line.AcceptedQuantity, ReceivedQuantity = line.ReceivedQuantity,
                    UnitCost = line.UnitCost, UnitOfMeasure = line.UnitOfMeasure ?? string.Empty,
                    StorageLocationId = line.StorageLocationId, StorageLocationName = line.StorageLocation?.Name,
                    TotalCost = line.AcceptedQuantity * line.UnitCost
                }).ToList()
            });
        }
        return results;
    }

    private async Task<GoodsReceiptNote?> LoadAcceptedSourceAsync(Guid id)
    {
        var grn = await _grns.GetWithItemsAsync(id);
        return grn is null ? null : await ProjectAcceptedSourceAsync(grn);
    }

    // Some older inspection completions left an Inventory GRN's status/quantities stale.
    // Read accepted source facts from Procurement + immutable inventory postings. Do not
    // rewrite the original GRN/inspection or manufacture a second receipt to repair a view.
    private async Task<GoodsReceiptNote?> ProjectAcceptedSourceAsync(GoodsReceiptNote grn)
    {
        if (grn.IsDeleted || grn.TenantId != _currentUser.TenantId || !grn.StockUpdated || grn.Status == GRNStatus.Cancelled) return null;
        if (!grn.PurchaseOrderReceiptId.HasValue) return grn.Status == GRNStatus.StockUpdated ? grn : null;
        var receipt = await _unitOfWork.Repository<PurchaseOrderReceipt>().GetQueryable(value =>
            value.Id == grn.PurchaseOrderReceiptId && value.TenantId == grn.TenantId && !value.IsDeleted)
            .Include(value => value.PurchaseOrder).Include(value => value.Items).ThenInclude(value => value.PurchaseOrderItem)
            .AsNoTracking().SingleOrDefaultAsync();
        if (receipt is null || receipt.Status != "Accepted" || receipt.PurchaseOrderId != grn.PurchaseOrderId ||
            receipt.PurchaseOrder is null || receipt.PurchaseOrder.IsDeleted || receipt.PurchaseOrder.TenantId != grn.TenantId ||
            receipt.PurchaseOrder.BusinessPartnerId != grn.SupplierId) return null;
        var inspection = await _unitOfWork.Repository<ProcurementReceiptInspectionCase>().GetQueryable(value =>
                value.TenantId == grn.TenantId && value.PurchaseOrderReceiptId == receipt.Id && !value.IsDeleted)
            .AsNoTracking().OrderByDescending(value => value.Sequence).FirstOrDefaultAsync();
        if ((receipt.RequiresInspection || inspection is not null) && (inspection is null || inspection.QualityHold ||
            inspection.Status is not (ProcurementReceiptInspectionStatus.Approved or ProcurementReceiptInspectionStatus.ReturnPending or
                ProcurementReceiptInspectionStatus.ReplacementPending or ProcurementReceiptInspectionStatus.ClosureReady or ProcurementReceiptInspectionStatus.Closed) ||
            !inspection.StockPostedAtUtc.HasValue || inspection.StockPostedQuantity <= 0 || inspection.PendingQuantity > 0)) return null;

        var acceptedByItemAndBin = new Dictionary<(Guid ItemId, Guid LocationId), decimal>();
        var receiptLinesByPoItem = new Dictionary<Guid, (PurchaseOrderReceiptItem Line, decimal Conversion)>();
        foreach (var line in receipt.Items.Where(line => !line.IsDeleted && line.TenantId == grn.TenantId))
        {
            if (line.PurchaseOrderItem is null || line.PurchaseOrderItem.IsDeleted ||
                line.PurchaseOrderItem.TenantId != grn.TenantId || line.PurchaseOrderItem.PurchaseOrderId != receipt.PurchaseOrderId) return null;
            if (line.PurchaseOrderItem.LineType != ItemType.StockItem) continue;
            if (line.PurchaseOrderItem.InventoryItemId is not Guid itemId || !line.LocationId.HasValue || line.AcceptedQuantity < 0) return null;
            var conversion = 1m;
            if (line.ItemUnitOfMeasureId.HasValue)
            {
                conversion = await _unitOfWork.Repository<ItemUnitOfMeasure>().GetQueryable(value =>
                    value.Id == line.ItemUnitOfMeasureId && value.TenantId == grn.TenantId && !value.IsDeleted && value.InventoryItemId == itemId)
                    .AsNoTracking().Select(value => value.ConversionToBase).SingleOrDefaultAsync();
                if (conversion <= 0) return null;
            }
            if (!receiptLinesByPoItem.TryAdd(line.PurchaseOrderItemId, (line, conversion))) return null;
            var key = (itemId, line.LocationId.Value);
            acceptedByItemAndBin[key] = acceptedByItemAndBin.GetValueOrDefault(key) + line.AcceptedQuantity * conversion;
        }

        // Receipt movements can be aggregated by item/bin. Validate the entire accepted
        // receipt scope once, rather than reusing one movement independently for each line.
        var movements = _unitOfWork.Repository<InventoryMovement>().GetQueryable();
        foreach (var accepted in acceptedByItemAndBin.Where(value => value.Value > 0))
        {
            var itemId = accepted.Key.ItemId;
            var locationId = accepted.Key.LocationId;
            var posted = await movements.Where(value =>
                    value.TenantId == grn.TenantId && !value.IsDeleted && value.ReferenceType == ReferenceType.PO &&
                    value.ReferenceId == receipt.Id && value.InventoryItemId == itemId && value.WarehouseId == grn.WarehouseId &&
                    value.LocationId == locationId && value.MovementType == InventoryMovementType.PurchaseReceipt &&
                    value.Direction == MovementDirection.In && value.IsPosted && value.PostedAt.HasValue && !value.IsReversal &&
                    !movements.Any(reversal => reversal.TenantId == grn.TenantId && !reversal.IsDeleted &&
                        reversal.ReversedMovementId == value.Id && reversal.IsReversal && reversal.IsPosted && reversal.PostedAt.HasValue))
                .AsNoTracking().SumAsync(value => value.Quantity);
            if (posted < accepted.Value) return null;
        }

        var projected = new GoodsReceiptNote
        {
            Id = grn.Id, TenantId = grn.TenantId, GRNNumber = grn.GRNNumber, ReceiptDate = grn.ReceiptDate,
            PurchaseOrderReceiptId = receipt.Id, PurchaseOrderId = grn.PurchaseOrderId, PurchaseOrderNumber = grn.PurchaseOrderNumber,
            SupplierId = grn.SupplierId, SupplierName = grn.SupplierName, WarehouseId = grn.WarehouseId,
            Status = GRNStatus.StockUpdated, StockUpdated = true
        };
        foreach (var source in grn.Items.Where(line => !line.IsDeleted))
        {
            if (!source.PurchaseOrderItemId.HasValue ||
                !receiptLinesByPoItem.TryGetValue(source.PurchaseOrderItemId.Value, out var receiptSource)) return null;
            var line = receiptSource.Line;
            if (line.PurchaseOrderItem is null || line.PurchaseOrderItem.InventoryItemId != source.InventoryItemId ||
                !line.LocationId.HasValue) return null;
            var conversion = receiptSource.Conversion;
            var accepted = line.AcceptedQuantity * conversion;
            projected.Items.Add(new GoodsReceiptNoteItem
            {
                Id = source.Id, TenantId = source.TenantId, GoodsReceiptNoteId = grn.Id, InventoryItemId = source.InventoryItemId,
                PurchaseOrderItemId = source.PurchaseOrderItemId, ItemCode = source.ItemCode, ItemName = source.ItemName,
                AcceptedQuantity = accepted, ReceivedQuantity = line.ReceivedQuantity * conversion,
                UnitCost = source.UnitCost, UnitOfMeasure = source.UnitOfMeasure, StorageLocationId = line.LocationId,
                StorageLocation = source.StorageLocationId == line.LocationId ? source.StorageLocation : null,
                SerialNumber = line.SerialNumber, LotNumber = line.LotNumber, BatchNumber = line.BatchNumber,
                ManufactureDate = line.ManufactureDate, ExpiryDate = line.ExpirationDate
            });
        }
        return projected;
    }
}
