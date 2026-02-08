using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Inventory;

/// <summary>
/// Goods Receipt Note (GRN) management service
/// Handles receiving goods, quality inspection, and posting to inventory
/// </summary>
public class GoodsReceiptNoteService : IGoodsReceiptNoteService
{
    private readonly IGoodsReceiptNoteRepository _grnRepository;
    private readonly IGoodsReceiptNoteItemRepository _grnItemRepository;
    private readonly IInventoryItemRepository _itemRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IWarehouseQuantityRepository _warehouseQuantityRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly IInventoryCostLayerRepository _costLayerRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GoodsReceiptNoteService> _logger;

    public GoodsReceiptNoteService(
        IGoodsReceiptNoteRepository grnRepository,
        IGoodsReceiptNoteItemRepository grnItemRepository,
        IInventoryItemRepository itemRepository,
        IWarehouseRepository warehouseRepository,
        IWarehouseQuantityRepository warehouseQuantityRepository,
        IStockMovementRepository stockMovementRepository,
        IInventoryCostLayerRepository costLayerRepository,
        IUnitOfWork unitOfWork,
        ILogger<GoodsReceiptNoteService> logger)
    {
        _grnRepository = grnRepository;
        _grnItemRepository = grnItemRepository;
        _itemRepository = itemRepository;
        _warehouseRepository = warehouseRepository;
        _warehouseQuantityRepository = warehouseQuantityRepository;
        _stockMovementRepository = stockMovementRepository;
        _costLayerRepository = costLayerRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<IEnumerable<GoodsReceiptNoteDto>> GetAllAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var grns = await _grnRepository.GetByDateRangeAsync(
            fromDate ?? DateTime.UtcNow.AddMonths(-3),
            toDate ?? DateTime.UtcNow);
        return grns.Select(MapToDto);
    }

    public async Task<IEnumerable<GoodsReceiptNoteDto>> GetByWarehouseAsync(Guid warehouseId)
    {
        var grns = await _grnRepository.GetByWarehouseAsync(warehouseId);
        return grns.Select(MapToDto);
    }

    public async Task<IEnumerable<GoodsReceiptNoteDto>> GetBySupplierAsync(Guid supplierId)
    {
        var grns = await _grnRepository.GetBySupplierAsync(supplierId);
        return grns.Select(MapToDto);
    }

    public async Task<IEnumerable<GoodsReceiptNoteDto>> GetByPurchaseOrderAsync(Guid purchaseOrderId)
    {
        var grns = await _grnRepository.GetByPurchaseOrderAsync(purchaseOrderId);
        return grns.Select(MapToDto);
    }

    public async Task<GoodsReceiptNoteDetailDto?> GetByIdAsync(Guid id)
    {
        var grn = await _grnRepository.GetWithItemsAsync(id);
        return grn != null ? MapToDetailDto(grn) : null;
    }

    public async Task<GoodsReceiptNoteDetailDto?> GetByGRNNumberAsync(string grnNumber)
    {
        var grn = await _grnRepository.GetByGRNNumberAsync(grnNumber);
        if (grn == null) return null;
        var fullGrn = await _grnRepository.GetWithItemsAsync(grn.Id);
        return fullGrn != null ? MapToDetailDto(fullGrn) : null;
    }

    public async Task<GoodsReceiptNoteDto> CreateAsync(CreateGoodsReceiptNoteDto dto, Guid userId)
    {
        var warehouse = await _warehouseRepository.GetByIdAsync(dto.WarehouseId)
            ?? throw new ArgumentException($"Warehouse {dto.WarehouseId} not found");

        var grn = new GoodsReceiptNote
        {
            GRNNumber = await GenerateGRNNumberAsync(),
            ReceiptDate = DateTime.UtcNow,
            WarehouseId = dto.WarehouseId,
            SupplierId = dto.SupplierId,
            PurchaseOrderId = dto.PurchaseOrderId,
            ReceivingLocationId = dto.ReceivingLocationId,
            DeliveryNoteNumber = dto.DeliveryNoteNumber,
            VehicleNumber = dto.VehicleNumber,
            DriverName = dto.DriverName,
            RequiresInspection = dto.RequiresInspection,
            Status = dto.RequiresInspection ? GRNStatus.PendingInspection : GRNStatus.Draft,
            Notes = dto.Notes,
            ReceivedById = userId
        };

        await _grnRepository.AddAsync(grn);

        foreach (var itemDto in dto.Items)
        {
            var item = await _itemRepository.GetByIdAsync(itemDto.InventoryItemId)
                ?? throw new ArgumentException($"Inventory item {itemDto.InventoryItemId} not found");

            var unitCost = itemDto.UnitCost > 0 ? itemDto.UnitCost : item.StandardCost;
            var grnItem = new GoodsReceiptNoteItem
            {
                GoodsReceiptNoteId = grn.Id,
                InventoryItemId = itemDto.InventoryItemId,
                ItemCode = item.ItemCode,
                ItemName = item.Name,
                ReceivedQuantity = itemDto.ReceivedQuantity,
                AcceptedQuantity = dto.RequiresInspection ? 0 : itemDto.ReceivedQuantity,
                RejectedQuantity = 0,
                UnitOfMeasure = item.UnitOfMeasure,
                UnitCost = unitCost,
                LineValue = itemDto.ReceivedQuantity * unitCost,
                LotNumber = itemDto.LotNumber,
                SerialNumber = itemDto.SerialNumber,
                ExpiryDate = itemDto.ExpiryDate,
                StorageLocationId = itemDto.StorageLocationId,
                InspectionResult = dto.RequiresInspection ? InspectionResult.Pending : InspectionResult.Passed,
                Notes = itemDto.Notes
            };

            await _grnItemRepository.AddAsync(grnItem);
        }

        grn.TotalItems = dto.Items.Count;
        grn.TotalQuantityReceived = dto.Items.Sum(i => i.ReceivedQuantity);
        await _grnRepository.UpdateAsync(grn);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Created GRN {GRNNumber} for warehouse {Warehouse}", grn.GRNNumber, warehouse.Name);
        return MapToDto(grn);
    }

    public async Task<bool> SubmitForInspectionAsync(Guid grnId, Guid userId)
    {
        var grn = await _grnRepository.GetByIdAsync(grnId)
            ?? throw new ArgumentException($"GRN {grnId} not found");

        if (grn.Status != GRNStatus.Draft)
            throw new InvalidOperationException($"GRN must be in Draft status to submit for inspection");

        grn.Status = GRNStatus.PendingInspection;
        grn.RequiresInspection = true;
        await _grnRepository.UpdateAsync(grn);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("GRN {GRNNumber} submitted for inspection", grn.GRNNumber);
        return true;
    }

    public async Task<bool> UpdateInspectionResultAsync(UpdateGRNInspectionDto dto, Guid userId)
    {
        var grnItem = await _grnItemRepository.GetByIdAsync(dto.GRNItemId)
            ?? throw new ArgumentException($"GRN Item {dto.GRNItemId} not found");

        grnItem.InspectionResult = dto.InspectionResult;
        grnItem.AcceptedQuantity = dto.AcceptedQuantity;
        grnItem.RejectedQuantity = dto.RejectedQuantity;
        grnItem.InspectionNotes = dto.InspectionNotes;

        await _grnItemRepository.UpdateAsync(grnItem);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    public async Task<bool> CompleteInspectionAsync(Guid grnId, Guid userId)
    {
        var grn = await _grnRepository.GetWithItemsAsync(grnId)
            ?? throw new ArgumentException($"GRN {grnId} not found");

        if (grn.Status != GRNStatus.PendingInspection && grn.Status != GRNStatus.InspectionInProgress)
            throw new InvalidOperationException($"GRN must be in inspection status");

        // Check all items have been inspected
        var pendingItems = grn.Items.Where(i => i.InspectionResult == InspectionResult.Pending).ToList();
        if (pendingItems.Any())
            throw new InvalidOperationException($"{pendingItems.Count} items still pending inspection");

        grn.Status = GRNStatus.Accepted;
        grn.InspectionDate = DateTime.UtcNow;
        grn.InspectedById = userId;
        await _grnRepository.UpdateAsync(grn);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("GRN {GRNNumber} inspection completed", grn.GRNNumber);
        return true;
    }

    public async Task<bool> PostToInventoryAsync(Guid grnId, Guid userId)
    {
        var grn = await _grnRepository.GetWithItemsAsync(grnId)
            ?? throw new ArgumentException($"GRN {grnId} not found");

        if (grn.Status == GRNStatus.StockUpdated)
            throw new InvalidOperationException("GRN already posted to inventory");

        if (grn.RequiresInspection && grn.Status != GRNStatus.Accepted && grn.Status != GRNStatus.PartiallyAccepted)
            throw new InvalidOperationException("GRN inspection must be completed before posting");

        foreach (var item in grn.Items.Where(i => i.AcceptedQuantity > 0))
        {
            // Update warehouse quantity
            var warehouseQty = await _warehouseQuantityRepository.GetByWarehouseAndItemAsync(grn.WarehouseId, item.InventoryItemId);
            if (warehouseQty == null)
            {
                warehouseQty = new WarehouseQuantity
                {
                    WarehouseId = grn.WarehouseId,
                    InventoryItemId = item.InventoryItemId,
                    CurrentStock = 0,
                    AvailableStock = 0
                };
                await _warehouseQuantityRepository.AddAsync(warehouseQty);
            }

            warehouseQty.CurrentStock += item.AcceptedQuantity;
            warehouseQty.AvailableStock += item.AcceptedQuantity;
            warehouseQty.LastMovementDate = DateTime.UtcNow;
            await _warehouseQuantityRepository.UpdateAsync(warehouseQty);

            // Update inventory item totals
            var invItem = await _itemRepository.GetByIdAsync(item.InventoryItemId);
            if (invItem != null)
            {
                invItem.CurrentStock += item.AcceptedQuantity;
                invItem.AvailableStock += item.AcceptedQuantity;
                invItem.LastPurchaseDate = DateTime.UtcNow;
                invItem.LastPurchaseCost = item.UnitCost;
                await _itemRepository.UpdateAsync(invItem);
            }

            // Create cost layer for FIFO/LIFO
            var costLayer = new InventoryCostLayer
            {
                InventoryItemId = item.InventoryItemId,
                WarehouseId = grn.WarehouseId,
                LayerNumber = $"CL-{grn.GRNNumber}-{item.Id.ToString()[..8]}",
                LayerDate = DateTime.UtcNow,
                SourceType = "GRN",
                SourceReference = grn.GRNNumber,
                SourceId = grn.Id,
                OriginalQuantity = item.AcceptedQuantity,
                RemainingQuantity = item.AcceptedQuantity,
                UnitCost = item.UnitCost,
                LotNumber = item.LotNumber,
                ExpiryDate = item.ExpiryDate
            };
            await _costLayerRepository.AddAsync(costLayer);

            // Create stock movement
            var movement = new StockMovement
            {
                InventoryItemId = item.InventoryItemId,
                MovementType = "Receipt",
                Quantity = item.AcceptedQuantity,
                UnitCost = item.UnitCost,
                TotalValue = item.AcceptedQuantity * item.UnitCost,
                ReferenceType = ReferenceType.PO,
                ReferenceNumber = grn.GRNNumber,
                ReferenceId = grn.Id,
                WarehouseId = grn.WarehouseId,
                LocationId = item.StorageLocationId,
                Notes = $"Received from GRN {grn.GRNNumber}",
                ProcessedById = userId,
                RunningBalance = invItem?.CurrentStock ?? item.AcceptedQuantity
            };
            await _stockMovementRepository.AddAsync(movement);

            await _grnItemRepository.UpdateAsync(item);
        }

        grn.Status = GRNStatus.StockUpdated;
        grn.StockUpdated = true;
        grn.StockUpdatedAt = DateTime.UtcNow;
        grn.TotalValue = grn.Items.Sum(i => i.AcceptedQuantity * i.UnitCost);
        await _grnRepository.UpdateAsync(grn);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("GRN {GRNNumber} posted to inventory", grn.GRNNumber);
        return true;
    }

    public async Task<bool> CancelAsync(Guid grnId, string reason, Guid userId)
    {
        var grn = await _grnRepository.GetByIdAsync(grnId)
            ?? throw new ArgumentException($"GRN {grnId} not found");

        if (grn.Status == GRNStatus.StockUpdated)
            throw new InvalidOperationException("Cannot cancel a posted GRN");

        grn.Status = GRNStatus.Cancelled;
        grn.Notes = $"{grn.Notes}\nCancelled: {reason}";
        await _grnRepository.UpdateAsync(grn);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("GRN {GRNNumber} cancelled: {Reason}", grn.GRNNumber, reason);
        return true;
    }

    public async Task<IEnumerable<GoodsReceiptNoteDto>> GetPendingInspectionAsync()
    {
        var grns = await _grnRepository.GetPendingInspectionAsync();
        return grns.Select(MapToDto);
    }

    #region Private Methods

    private async Task<string> GenerateGRNNumberAsync()
    {
        var yearPrefix = DateTime.UtcNow.ToString("yy");
        var monthPrefix = DateTime.UtcNow.ToString("MM");
        var sequence = await GetNextSequenceAsync();
        return $"GRN{yearPrefix}{monthPrefix}{sequence:D4}";
    }

    private Task<int> GetNextSequenceAsync()
    {
        // In production, this would query the database for the next sequence
        return Task.FromResult(new Random().Next(1, 9999));
    }

    private static GoodsReceiptNoteDto MapToDto(GoodsReceiptNote grn)
    {
        return new GoodsReceiptNoteDto
        {
            Id = grn.Id,
            GRNNumber = grn.GRNNumber,
            ReceiptDate = grn.ReceiptDate,
            WarehouseId = grn.WarehouseId,
            WarehouseName = grn.Warehouse?.Name ?? string.Empty,
            SupplierId = grn.SupplierId,
            PurchaseOrderId = grn.PurchaseOrderId,
            Status = grn.Status,
            DeliveryNoteNumber = grn.DeliveryNoteNumber,
            VehicleNumber = grn.VehicleNumber,
            TotalItems = grn.TotalItems,
            TotalQuantity = grn.TotalQuantityReceived,
            TotalValue = grn.TotalValue,
            RequiresInspection = grn.RequiresInspection,
            InspectionDate = grn.InspectionDate,
            ReceivedByName = grn.ReceivedBy?.FullName,
            Notes = grn.Notes,
            CreatedAtFormatted = grn.CreatedAt.ToString("yyyy-MM-dd HH:mm")
        };
    }

    private static GoodsReceiptNoteDetailDto MapToDetailDto(GoodsReceiptNote grn)
    {
        return new GoodsReceiptNoteDetailDto
        {
            Id = grn.Id,
            GRNNumber = grn.GRNNumber,
            ReceiptDate = grn.ReceiptDate,
            WarehouseId = grn.WarehouseId,
            WarehouseName = grn.Warehouse?.Name ?? string.Empty,
            SupplierId = grn.SupplierId,
            PurchaseOrderId = grn.PurchaseOrderId,
            Status = grn.Status,
            DeliveryNoteNumber = grn.DeliveryNoteNumber,
            VehicleNumber = grn.VehicleNumber,
            TotalItems = grn.TotalItems,
            TotalQuantity = grn.TotalQuantityReceived,
            TotalValue = grn.TotalValue,
            RequiresInspection = grn.RequiresInspection,
            InspectionDate = grn.InspectionDate,
            ReceivedByName = grn.ReceivedBy?.FullName,
            Notes = grn.Notes,
            CreatedAtFormatted = grn.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
            ReceivingLocationId = grn.ReceivingLocationId,
            ReceivingLocationName = grn.ReceivingLocation?.LocationCode,
            InspectedByName = grn.InspectedBy?.FullName,
            InspectionNotes = grn.InspectionNotes,
            Items = grn.Items.Select(i => new GoodsReceiptNoteItemDto
            {
                Id = i.Id,
                InventoryItemId = i.InventoryItemId,
                ItemCode = i.InventoryItem?.ItemCode ?? string.Empty,
                ItemName = i.InventoryItem?.Name ?? string.Empty,
                ReceivedQuantity = i.ReceivedQuantity,
                AcceptedQuantity = i.AcceptedQuantity,
                RejectedQuantity = i.RejectedQuantity,
                UnitOfMeasure = i.UnitOfMeasure ?? string.Empty,
                UnitCost = i.UnitCost,
                TotalCost = i.LineValue,
                LotNumber = i.LotNumber,
                SerialNumber = i.SerialNumber,
                ExpiryDate = i.ExpiryDate,
                InspectionResult = i.InspectionResult,
                InspectionNotes = i.InspectionNotes,
                StorageLocationId = i.StorageLocationId,
                StorageLocationName = i.StorageLocation?.LocationCode
            }).ToList()
        };
    }

    #endregion
}

