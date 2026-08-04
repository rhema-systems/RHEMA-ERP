using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Inventory;

public class LandedCostService : ILandedCostService
{
    private static readonly HashSet<string> AllowedAllocationMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "ByValue",
        "ByQuantity",
        "ByWeight",
        "ByVolume",
        "Equal",
        "Manual"
    };

    private readonly ILandedCostRepository _landedCostRepository;
    private readonly ILandedCostItemRepository _landedCostItemRepository;
    private readonly ILandedCostAllocationRepository _landedCostAllocationRepository;
    private readonly IGoodsReceiptNoteRepository _grnRepository;
    private readonly IPurchaseOrderLandedCostPlanRepository _poLandedCostPlanRepository;
    private readonly IPurchaseOrderReceiptRepository _purchaseOrderReceiptRepository;
    private readonly IBusinessPartnerRepository _businessPartnerRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LandedCostService> _logger;
    private readonly IInventoryLandedCostFinancePostingService? _financePosting;

    public LandedCostService(
        ILandedCostRepository landedCostRepository,
        ILandedCostItemRepository landedCostItemRepository,
        ILandedCostAllocationRepository landedCostAllocationRepository,
        IGoodsReceiptNoteRepository grnRepository,
        IPurchaseOrderLandedCostPlanRepository poLandedCostPlanRepository,
        IPurchaseOrderReceiptRepository purchaseOrderReceiptRepository,
        IBusinessPartnerRepository businessPartnerRepository,
        IUnitOfWork unitOfWork,
        ILogger<LandedCostService> logger,
        IInventoryLandedCostFinancePostingService? financePosting = null)
    {
        _landedCostRepository = landedCostRepository;
        _landedCostItemRepository = landedCostItemRepository;
        _landedCostAllocationRepository = landedCostAllocationRepository;
        _grnRepository = grnRepository;
        _poLandedCostPlanRepository = poLandedCostPlanRepository;
        _purchaseOrderReceiptRepository = purchaseOrderReceiptRepository;
        _businessPartnerRepository = businessPartnerRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _financePosting = financePosting;
    }

    public async Task<IEnumerable<LandedCostDto>> GetAllAsync()
    {
        var landedCosts = await _landedCostRepository.GetAllAsync();
        return landedCosts.Select(MapToDto);
    }

    public async Task<IEnumerable<LandedCostDto>> GetByGRNAsync(Guid grnId)
    {
        if (grnId == Guid.Empty) return [];

        // The UI passes the Procurement PurchaseOrderReceipt.Id.
        // LandedCost documents are stored against the Inventory GoodsReceiptNote.Id.
        // Resolve the correct Inventory GRN snapshot first.
        var resolvedGrnId = await ResolveInventoryGrnIdAsync(grnId);

        var landedCosts = await _landedCostRepository.GetByGRNAsync(resolvedGrnId);
        return landedCosts.Select(MapToDto);
    }

    public async Task<LandedCostDetailDto?> GetByIdAsync(Guid id)
    {
        var landedCost = await _landedCostRepository.GetWithDetailsAsync(id);
        if (landedCost == null) return null;

        var grn = await _grnRepository.GetWithItemsAsync(landedCost.GoodsReceiptNoteId);
        return MapToDetailDto(landedCost, grn);
    }

    public async Task<LandedCostDto> CreateAsync(CreateLandedCostDto dto, Guid userId)
    {
        var grn = await _grnRepository.GetWithItemsAsync(dto.GoodsReceiptNoteId)
            ?? throw new ArgumentException($"GRN {dto.GoodsReceiptNoteId} not found");

        if (dto.CostItems == null || dto.CostItems.Count == 0)
            throw new ArgumentException("At least one landed cost item is required");

        var landedCost = new LandedCost
        {
            TenantId = grn.TenantId,
            LandedCostNumber = await GenerateLandedCostNumberAsync(),
            GoodsReceiptNoteId = grn.Id,
            GRNNumber = grn.GRNNumber,
            CostDate = DateTime.UtcNow,
            Currency = dto.Currency,
            Status = "Draft",
            Notes = dto.Notes
        };

        await _landedCostRepository.AddAsync(landedCost);

        foreach (var item in dto.CostItems)
        {
            EnsureAllowedAllocationMethod(item.AllocationMethod);

            var supplierName = await ResolveSupplierNameAsync(item.SupplierId);
            var itemBase = new LandedCostItem
            {
                TenantId = grn.TenantId,
                LandedCostId = landedCost.Id,
                CostType = item.CostType,
                Description = item.Description,
                Amount = item.Amount,
                Currency = item.Currency,
                ExchangeRate = item.ExchangeRate,
                AmountInBaseCurrency = RoundMoney(item.Amount * item.ExchangeRate),
                AllocationMethod = item.AllocationMethod,
                SupplierId = item.SupplierId,
                SupplierName = supplierName,
                ReferenceNumber = item.ReferenceNumber
            };

            await _landedCostItemRepository.AddAsync(itemBase);
        }

        // Persist header + lines first, then calculate totals based on saved lines.
        await _unitOfWork.SaveChangesAsync();
        await RecalculateHeaderTotalsAsync(landedCost.Id);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Created landed cost {LandedCostNumber} for GRN {GRNNumber}", landedCost.LandedCostNumber, grn.GRNNumber);
        var reloaded = await _landedCostRepository.GetByIdAsync(landedCost.Id);
        // Be resilient: if query filters/tenant scoping prevent reload, return the in-memory entity.
        return reloaded != null ? MapToDto(reloaded) : MapToDto(landedCost);
    }

    public async Task<LandedCostDto> InitializeFromPurchaseOrderPlanAsync(Guid goodsReceiptNoteId, Guid userId)
    {
        // goodsReceiptNoteId is actually the Procurement PurchaseOrderReceipt.Id in the UI.
        var grn = await GetOrCreateGrnFromProcurementReceiptAsync(goodsReceiptNoteId, userId);

        if (grn.PurchaseOrderId == null || grn.PurchaseOrderId == Guid.Empty)
            throw new InvalidOperationException("GRN is not linked to a Purchase Order");

        var plan = await _poLandedCostPlanRepository.GetWithItemsByPurchaseOrderIdAsync(grn.PurchaseOrderId.Value);
        if (plan == null || plan.Items.Count == 0)
            throw new InvalidOperationException("No planned landed cost found for the related Purchase Order");

        var existing = (await _landedCostRepository.GetByGRNAsync(grn.Id))
            .FirstOrDefault(l => l.Status == "Draft");

        var landedCost = existing ?? new LandedCost
        {
            TenantId = grn.TenantId,
            LandedCostNumber = await GenerateLandedCostNumberAsync(),
            GoodsReceiptNoteId = grn.Id,
            GRNNumber = grn.GRNNumber,
            CostDate = DateTime.UtcNow,
            Status = "Draft"
        };

        // Ensure tenant is always set (in case the context tenant accessor isn't configured for this request).
        if (landedCost.TenantId == Guid.Empty)
            landedCost.TenantId = grn.TenantId;

        landedCost.Currency = plan.Currency;
        landedCost.Notes = plan.Notes;

        if (existing == null)
            await _landedCostRepository.AddAsync(landedCost);
        else
            await _landedCostRepository.UpdateAsync(landedCost);

        // Replace existing items and allocations
        await SoftDeleteAllocationsAsync(landedCost.Id);
        var existingItems = await _landedCostItemRepository.GetByLandedCostAsync(landedCost.Id);
        await _landedCostItemRepository.DeleteRangeAsync(existingItems);

        foreach (var planItem in plan.Items.Where(i => !i.IsDeleted))
        {
            EnsureAllowedAllocationMethod(planItem.AllocationMethod);
            var supplierName = planItem.SupplierName ?? await ResolveSupplierNameAsync(planItem.SupplierId);

            var item = new LandedCostItem
            {
                TenantId = grn.TenantId,
                LandedCostId = landedCost.Id,
                CostType = planItem.CostType,
                Description = planItem.Description,
                Amount = planItem.Amount,
                Currency = planItem.Currency,
                ExchangeRate = planItem.ExchangeRate,
                AmountInBaseCurrency = RoundMoney(planItem.AmountInPlanCurrency),
                AllocationMethod = planItem.AllocationMethod,
                SupplierId = planItem.SupplierId,
                SupplierName = supplierName,
                ReferenceNumber = planItem.ReferenceNumber,
                Notes = planItem.Notes
            };

            await _landedCostItemRepository.AddAsync(item);
        }

        // Persist header + lines first, then calculate totals based on saved lines.
        await _unitOfWork.SaveChangesAsync();
        await RecalculateHeaderTotalsAsync(landedCost.Id);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Initialized landed cost {LandedCostNumber} from PO plan for GRN {GRNNumber}", landedCost.LandedCostNumber, grn.GRNNumber);
        var reloaded = await _landedCostRepository.GetByIdAsync(landedCost.Id);
        // Be resilient: if query filters/tenant scoping prevent reload, return the in-memory entity.
        return reloaded != null ? MapToDto(reloaded) : MapToDto(landedCost);
    }

    private async Task<GoodsReceiptNote> GetOrCreateGrnFromProcurementReceiptAsync(Guid goodsReceiptNoteId, Guid userId)
    {
        var grn = await _grnRepository.GetWithItemsAsync(goodsReceiptNoteId);
        if (grn != null) return grn;

        // Compatibility bridge:
        // If callers pass a Procurement PurchaseOrderReceipt.Id (GRN in procurement module),
        // lazily create an Inventory GoodsReceiptNote snapshot with the same Id so Landed Cost can work.
        var receipt = await _purchaseOrderReceiptRepository.GetReceiptWithItemsAsync(goodsReceiptNoteId);
        if (receipt == null)
            throw new ArgumentException($"GRN {goodsReceiptNoteId} not found");

        // If a snapshot already exists for this receipt, reuse it.
        var existingSnapshot = await _unitOfWork.Repository<GoodsReceiptNote>()
            .GetQueryable(g => g.PurchaseOrderReceiptId == receipt.Id)
            .IgnoreQueryFilters()
            .Include(g => g.Items)
                .ThenInclude(i => i.InventoryItem)
            .AsNoTracking()
            .OrderByDescending(g => g.CreatedAt)
            .FirstOrDefaultAsync();
        if (existingSnapshot != null && !existingSnapshot.IsDeleted)
        {
            return await _grnRepository.GetWithItemsAsync(existingSnapshot.Id) ?? existingSnapshot;
        }

        // Backward compatibility: if older snapshots exist for this receipt (but without the linkage column),
        // find the most likely one and link it now to avoid creating duplicates.
        var receiptIdToken = receipt.Id.ToString("N").ToUpperInvariant();
        var receiptPrefix = receipt.Id.ToString("N")[..6].ToUpperInvariant();
        var legacySnapshot = await _unitOfWork.Repository<GoodsReceiptNote>()
            .GetQueryable(g => g.PurchaseOrderId == receipt.PurchaseOrderId &&
                               g.GRNNumber != null &&
                               (g.GRNNumber.Contains(receiptIdToken) ||
                                g.GRNNumber.Contains(receiptPrefix) ||
                                (receipt.ReceiptNumber != null && g.GRNNumber.Contains(receipt.ReceiptNumber))))
            .IgnoreQueryFilters()
            .Include(g => g.Items)
                .ThenInclude(i => i.InventoryItem)
            .AsNoTracking()
            .OrderByDescending(g => g.CreatedAt)
            .FirstOrDefaultAsync();

        if (legacySnapshot != null && !legacySnapshot.IsDeleted)
        {
            try
            {
                var tracked = await _unitOfWork.Repository<GoodsReceiptNote>().GetByIdAsync(legacySnapshot.Id);
                if (tracked != null && tracked.PurchaseOrderReceiptId != receipt.Id)
                {
                    tracked.PurchaseOrderReceiptId = receipt.Id;
                    await _unitOfWork.Repository<GoodsReceiptNote>().UpdateAsync(tracked);
                    await _unitOfWork.SaveChangesAsync();
                }
            }
            catch
            {
                // ignore; linkage is best-effort
            }

            return await _grnRepository.GetWithItemsAsync(legacySnapshot.Id) ?? legacySnapshot;
        }

        // If another request already created the snapshot (or it exists but is hidden by filters),
        // return it instead of attempting a duplicate insert.
        var existingById = await _unitOfWork.Repository<GoodsReceiptNote>()
            .GetQueryable(g => g.Id == receipt.Id)
            .IgnoreQueryFilters()
            .Include(g => g.Items)
                .ThenInclude(i => i.InventoryItem)
            .AsNoTracking()
            .FirstOrDefaultAsync();
        if (existingById != null && !existingById.IsDeleted)
        {
            return await _grnRepository.GetWithItemsAsync(receipt.Id) ?? existingById;
        }

        // Re-check in case another request created it while we were loading the receipt.
        grn = await _grnRepository.GetWithItemsAsync(goodsReceiptNoteId);
        if (grn != null) return grn;

        var warehouseId = await InferWarehouseIdForReceiptAsync(receipt);
        if (warehouseId == Guid.Empty)
            throw new InvalidOperationException("Unable to infer Warehouse for this receipt. Select a warehouse/location during receiving and try again.");

        var inventoryItemIds = (receipt.Items ?? [])
            .Select(i => i.PurchaseOrderItem?.InventoryItemId)
            .Where(id => id.HasValue && id.Value != Guid.Empty)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var inventoryItemsById = await _unitOfWork.Repository<InventoryItem>()
            .GetQueryable(i => inventoryItemIds.Contains(i.Id))
            .AsNoTracking()
            .ToDictionaryAsync(i => i.Id, i => i);

        var grnItems = new List<GoodsReceiptNoteItem>();

        foreach (var receiptItem in (receipt.Items ?? []).Where(i => !i.IsDeleted))
        {
            var poItem = receiptItem.PurchaseOrderItem;
            if (poItem == null) continue;

            if (!poItem.InventoryItemId.HasValue || poItem.InventoryItemId.Value == Guid.Empty)
                continue;

            var inventoryItemId = poItem.InventoryItemId.Value;
            inventoryItemsById.TryGetValue(inventoryItemId, out var inventoryItem);

            var conversionToBase = await ResolveConversionToBaseAsync(poItem);
            if (conversionToBase <= 0) conversionToBase = 1m;

            var unitCostPerPurchaseUom = poItem.LandedUnitCost > 0 ? poItem.LandedUnitCost : poItem.UnitPrice;
            var baseUnitCost = conversionToBase != 0 ? unitCostPerPurchaseUom / conversionToBase : unitCostPerPurchaseUom;

            var baseOrdered = poItem.OrderedQuantity * conversionToBase;
            var baseReceived = receiptItem.ReceivedQuantity * conversionToBase;
            var baseAccepted = receiptItem.AcceptedQuantity * conversionToBase;
            var baseRejected = receiptItem.RejectedQuantity * conversionToBase;

            var allocQty = baseAccepted > 0 ? baseAccepted : baseReceived;
            var lineValue = baseUnitCost * allocQty;

            grnItems.Add(new GoodsReceiptNoteItem
            {
                TenantId = receipt.TenantId,
                GoodsReceiptNoteId = receipt.Id,
                InventoryItemId = inventoryItemId,
                PurchaseOrderItemId = poItem.Id,
                ItemCode = inventoryItem?.ItemCode ?? poItem.BusinessPartnerItemCode,
                ItemName = inventoryItem?.Name ?? poItem.ItemDescription,
                OrderedQuantity = baseOrdered,
                ReceivedQuantity = baseReceived,
                PreviouslyReceiptedQuantitySnapshot =
                    receiptItem.PreviouslyReceiptedQuantitySnapshot *
                    conversionToBase,
                ToleranceQuantitySnapshot =
                    receiptItem.ToleranceQuantitySnapshot *
                    conversionToBase,
                MaximumReceivableQuantitySnapshot =
                    receiptItem.MaximumReceivableQuantitySnapshot *
                    conversionToBase,
                RemainingQuantityBeforeReceiptSnapshot =
                    receiptItem.RemainingQuantityBeforeReceiptSnapshot *
                    conversionToBase,
                ReceiptLineIntegrityHash =
                    receiptItem.ReceiptLineIntegrityHash,
                AcceptedQuantity = baseAccepted,
                RejectedQuantity = baseRejected,
                UnitCost = baseUnitCost,
                LineValue = RoundMoney(lineValue),
                UnitOfMeasure = inventoryItem?.UnitOfMeasure ?? poItem.UnitOfMeasure,
                StorageLocationId = receiptItem.LocationId,
                SerialNumber = receiptItem.SerialNumber,
                LotNumber = receiptItem.LotNumber,
                ExpiryDate = receiptItem.ExpirationDate,
                InspectionResult = MapInspectionResult(receiptItem.QualityStatus),
                InspectionNotes = receiptItem.QualityNotes,
                RejectionReason = receiptItem.RejectionReason,
                Notes = receiptItem.Notes,
                CreatedById = userId
            });
        }

        async Task<bool> GrnNumberExistsAsync(string number) =>
            await _unitOfWork.Repository<GoodsReceiptNote>()
                .GetQueryable(g => g.GRNNumber == number)
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync();

        // Use a deterministic, collision-resistant number to avoid unique index violations.
        // (Receipt numbers are not unique across modules, and truncated GUID prefixes can collide.)
        var grnNumberCandidate = $"IGRN-{receipt.Id:N}".ToUpperInvariant();
        if (await GrnNumberExistsAsync(grnNumberCandidate))
        {
            grnNumberCandidate = $"IGRN-{Guid.NewGuid():N}".ToUpperInvariant();
        }

        var createdGrn = new GoodsReceiptNote
        {
            TenantId = receipt.TenantId,
            GRNNumber = grnNumberCandidate,
            PurchaseOrderReceiptId = receipt.Id,
            PurchaseOrderId = receipt.PurchaseOrderId,
            PurchaseOrderNumber = receipt.PurchaseOrder?.OrderNumber,
            SupplierId = receipt.PurchaseOrder?.BusinessPartnerId,
            SupplierName = receipt.PurchaseOrder?.BusinessPartner?.PartnerName,
            ReceiptDate = receipt.ReceiptDate,
            WarehouseId = warehouseId,
            DeliveryNoteNumber = receipt.DeliveryNote,
            CarrierName = receipt.CarrierName,
            TrackingNumber = receipt.TrackingNumber,
            Status = receipt.RequiresInspection ? GRNStatus.PendingInspection : GRNStatus.StockUpdated,
            RequiresInspection = receipt.RequiresInspection,
            ReceivedById = receipt.ReceivedById,
            InspectedById = receipt.InspectedById,
            Notes = string.IsNullOrWhiteSpace(receipt.Notes) ? $"Created from Procurement Receipt {receipt.ReceiptNumber}" : receipt.Notes,
            IdempotencyKey =
                $"procurement-receipt:{receipt.Id:N}",
            IdempotencyRequestHash =
                BuildReceiptProjectionRequestHash(receipt, grnItems, warehouseId),
            CorrelationId = receipt.CorrelationId,
            ReceiptTolerancePercent =
                receipt.ReceiptTolerancePercent,
            ReceiptSourceSnapshotJson =
                receipt.ReceiptSourceSnapshotJson,
            ReceiptSourceIntegrityHash =
                receipt.ReceiptSourceIntegrityHash,
            ReceiptSourceValidatedAtUtc =
                receipt.ReceiptSourceValidatedAtUtc,
            StockUpdated = true,
            TotalItems = grnItems.Count,
            TotalQuantityReceived = grnItems.Sum(i => i.ReceivedQuantity),
            TotalQuantityAccepted = grnItems.Sum(i => i.AcceptedQuantity),
            TotalQuantityRejected = grnItems.Sum(i => i.RejectedQuantity),
            TotalValue = RoundMoney(grnItems.Sum(i => i.LineValue)),
            CreatedById = userId,
            Items = grnItems
        };

        try
        {
            // If a soft-deleted snapshot exists with the same Id, revive it rather than creating a new row.
            if (existingById != null && existingById.IsDeleted)
            {
                var revive = await _unitOfWork.Repository<GoodsReceiptNote>()
                    .GetQueryable(g => g.Id == receipt.Id)
                    .IgnoreQueryFilters()
                    .FirstAsync();

                revive.IsDeleted = false;
                revive.DeletedAt = null;
                revive.DeletedBy = null;
                revive.GRNNumber = createdGrn.GRNNumber;
                revive.PurchaseOrderReceiptId = receipt.Id;
                revive.PurchaseOrderId = createdGrn.PurchaseOrderId;
                revive.PurchaseOrderNumber = createdGrn.PurchaseOrderNumber;
                revive.SupplierId = createdGrn.SupplierId;
                revive.SupplierName = createdGrn.SupplierName;
                revive.ReceiptDate = createdGrn.ReceiptDate;
                revive.WarehouseId = createdGrn.WarehouseId;
                revive.DeliveryNoteNumber = createdGrn.DeliveryNoteNumber;
                revive.CarrierName = createdGrn.CarrierName;
                revive.TrackingNumber = createdGrn.TrackingNumber;
                revive.Status = createdGrn.Status;
                revive.RequiresInspection = createdGrn.RequiresInspection;
                revive.ReceivedById = createdGrn.ReceivedById;
                revive.InspectedById = createdGrn.InspectedById;
                revive.Notes = createdGrn.Notes;
                revive.IdempotencyKey =
                    createdGrn.IdempotencyKey;
                revive.IdempotencyRequestHash =
                    createdGrn.IdempotencyRequestHash;
                revive.CorrelationId =
                    createdGrn.CorrelationId;
                revive.ReceiptTolerancePercent =
                    createdGrn.ReceiptTolerancePercent;
                revive.ReceiptSourceSnapshotJson =
                    createdGrn.ReceiptSourceSnapshotJson;
                revive.ReceiptSourceIntegrityHash =
                    createdGrn.ReceiptSourceIntegrityHash;
                revive.ReceiptSourceValidatedAtUtc =
                    createdGrn.ReceiptSourceValidatedAtUtc;
                revive.StockUpdated = createdGrn.StockUpdated;
                revive.TotalItems = createdGrn.TotalItems;
                revive.TotalQuantityReceived = createdGrn.TotalQuantityReceived;
                revive.TotalQuantityAccepted = createdGrn.TotalQuantityAccepted;
                revive.TotalQuantityRejected = createdGrn.TotalQuantityRejected;
                revive.TotalValue = createdGrn.TotalValue;
                revive.UpdatedAt = DateTime.UtcNow;
                revive.LastModifiedById = userId;

                // Replace items (best-effort) for revived snapshot
                var existingItems = await _unitOfWork.Repository<GoodsReceiptNoteItem>()
                    .GetQueryable(i => i.GoodsReceiptNoteId == receipt.Id)
                    .IgnoreQueryFilters()
                    .ToListAsync();
                await _unitOfWork.Repository<GoodsReceiptNoteItem>().DeleteRangeAsync(existingItems);

                await _unitOfWork.Repository<GoodsReceiptNoteItem>().AddRangeAsync(grnItems);
            }
            else
            {
                await _grnRepository.AddAsync(createdGrn);
            }

            await _unitOfWork.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Failed to create Inventory GRN snapshot for receipt {ReceiptId}. Will attempt to load existing.", receipt.Id);
            var fallback = await _unitOfWork.Repository<GoodsReceiptNote>()
                .GetQueryable(g => g.Id == receipt.Id && !g.IsDeleted)
                .IgnoreQueryFilters()
                .Include(g => g.Items)
                    .ThenInclude(i => i.InventoryItem)
                .AsNoTracking()
                .FirstOrDefaultAsync();
            if (fallback != null)
            {
                return await _grnRepository.GetWithItemsAsync(receipt.Id) ?? fallback;
            }

            throw;
        }

        _logger.LogInformation("Created Inventory GRN snapshot {GRNNumber} from Procurement receipt {ReceiptNumber} for landed cost", createdGrn.GRNNumber, receipt.ReceiptNumber);

        // Prefer tenant-filtered repo load (normal path).
        var loaded = await _grnRepository.GetWithItemsAsync(createdGrn.Id);
        if (loaded != null) return loaded;

        // Fallback: load by PK without tenant filters (should still be safe because the PK is unique and we just created it).
        var loadedIgnoringFilters = await _unitOfWork.Repository<GoodsReceiptNote>()
            .GetQueryable(g => g.Id == createdGrn.Id && !g.IsDeleted)
            .IgnoreQueryFilters()
            .Include(g => g.Warehouse)
            .Include(g => g.ReceivingLocation)
            .Include(g => g.ReceivedBy)
            .Include(g => g.InspectedBy)
            .Include(g => g.Items)
                .ThenInclude(i => i.InventoryItem)
            .Include(g => g.Items)
                .ThenInclude(i => i.StorageLocation)
            .AsNoTracking()
            .FirstOrDefaultAsync();
        if (loadedIgnoringFilters != null) return loadedIgnoringFilters;

        _logger.LogWarning("Created Inventory GRN snapshot {GRNId} but could not reload it. Returning in-memory snapshot.", createdGrn.Id);
        // Populate InventoryItem navigation in-memory for allocation bases (weight/volume) if needed.
        foreach (var item in createdGrn.Items)
        {
            if (item.InventoryItem == null && inventoryItemsById.TryGetValue(item.InventoryItemId, out var inv))
            {
                item.InventoryItem = inv;
            }
        }
        return createdGrn;
    }

    private static string BuildReceiptProjectionRequestHash(
        PurchaseOrderReceipt receipt,
        IEnumerable<GoodsReceiptNoteItem> items,
        Guid warehouseId)
    {
        var canonical = new
        {
            receipt.Id,
            receipt.PurchaseOrderId,
            warehouseId,
            receipt.ReceiptDate,
            receipt.DeliveryNote,
            receipt.CarrierName,
            receipt.TrackingNumber,
            receipt.RequiresInspection,
            receipt.Notes,
            receipt.ReceiptSourceIntegrityHash,
            Items = items
                .OrderBy(item => item.PurchaseOrderItemId)
                .ThenBy(item => item.InventoryItemId)
                .ThenBy(item => item.StorageLocationId)
                .ThenBy(item => item.LotNumber, StringComparer.Ordinal)
                .ThenBy(item => item.SerialNumber, StringComparer.Ordinal)
                .ThenBy(item => item.ExpiryDate)
                .ThenBy(item => item.ReceivedQuantity)
                .ThenBy(item => item.AcceptedQuantity)
                .ThenBy(item => item.RejectedQuantity)
                .ThenBy(item => item.UnitCost)
                .ThenBy(item => item.Notes, StringComparer.Ordinal)
                .Select(item => new
                {
                    item.PurchaseOrderItemId,
                    item.InventoryItemId,
                    item.ReceivedQuantity,
                    item.AcceptedQuantity,
                    item.RejectedQuantity,
                    item.UnitCost,
                    item.StorageLocationId,
                    item.LotNumber,
                    item.SerialNumber,
                    item.ExpiryDate,
                    item.Notes
                })
                .ToArray()
        };
        var json = JsonSerializer.Serialize(canonical);
        return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(json)))
            .ToLowerInvariant();
    }

    private async Task<Guid> ResolveInventoryGrnIdAsync(Guid receiptOrGrnId)
    {
        // If this is already an Inventory GRN id, return it.
        var grn = await _unitOfWork.Repository<GoodsReceiptNote>()
            .GetQueryable(g => g.Id == receiptOrGrnId)
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync();
        if (grn != null && !grn.IsDeleted)
            return grn.Id;

        // Otherwise, treat it as Procurement receipt id and resolve the snapshot.
        var receipt = await _purchaseOrderReceiptRepository.GetReceiptWithItemsAsync(receiptOrGrnId);
        if (receipt == null)
            return receiptOrGrnId;

        var snapshot = await _unitOfWork.Repository<GoodsReceiptNote>()
            .GetQueryable(g => g.PurchaseOrderReceiptId == receipt.Id)
            .IgnoreQueryFilters()
            .AsNoTracking()
            .OrderByDescending(g => g.CreatedAt)
            .FirstOrDefaultAsync();
        if (snapshot != null && !snapshot.IsDeleted)
            return snapshot.Id;

        // Backward compatibility: find older snapshots that encoded receipt id / receipt number in GRNNumber.
        var receiptIdToken = receipt.Id.ToString("N").ToUpperInvariant();
        var receiptPrefix = receipt.Id.ToString("N")[..6].ToUpperInvariant();
        var fallback = await _unitOfWork.Repository<GoodsReceiptNote>()
            .GetQueryable(g => g.PurchaseOrderId == receipt.PurchaseOrderId &&
                               g.GRNNumber != null &&
                               (g.GRNNumber.Contains(receiptIdToken) ||
                                g.GRNNumber.Contains(receiptPrefix) ||
                                (receipt.ReceiptNumber != null && g.GRNNumber.Contains(receipt.ReceiptNumber))))
            .IgnoreQueryFilters()
            .AsNoTracking()
            .OrderByDescending(g => g.CreatedAt)
            .FirstOrDefaultAsync();

        if (fallback != null && !fallback.IsDeleted)
        {
            // Best-effort: persist the new link so future resolves are stable.
            try
            {
                var tracked = await _unitOfWork.Repository<GoodsReceiptNote>().GetByIdAsync(fallback.Id);
                if (tracked != null && tracked.PurchaseOrderReceiptId != receipt.Id)
                {
                    tracked.PurchaseOrderReceiptId = receipt.Id;
                    await _unitOfWork.Repository<GoodsReceiptNote>().UpdateAsync(tracked);
                    await _unitOfWork.SaveChangesAsync();
                }
            }
            catch
            {
                // ignore; linkage is best-effort
            }

            return fallback.Id;
        }

        return receiptOrGrnId;
    }

    private async Task<Guid> InferWarehouseIdForReceiptAsync(PurchaseOrderReceipt receipt)
    {
        // 1) From receipt line location -> warehouse
        var firstLocationId = (receipt.Items ?? [])
            .Where(i => i.LocationId.HasValue && i.LocationId.Value != Guid.Empty)
            .Select(i => i.LocationId!.Value)
            .FirstOrDefault();

        if (firstLocationId != Guid.Empty)
        {
            var location = await _unitOfWork.Repository<WarehouseLocation>().GetByIdAsync(firstLocationId);
            if (location != null && location.WarehouseId != Guid.Empty)
                return location.WarehouseId;
        }

        // 2) From PO line warehouse (if any)
        var poWarehouses = (receipt.Items ?? [])
            .Select(i => i.PurchaseOrderItem?.WarehouseId)
            .Where(id => id.HasValue && id.Value != Guid.Empty)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        if (poWarehouses.Count > 0)
            return poWarehouses[0];

        // 3) From posted inventory movements (receipt posting uses ReferenceId = receipt.Id)
        var movementWarehouse = await _unitOfWork.Repository<StockMovement>()
            .GetQueryable(m => !m.IsDeleted &&
                               m.ReferenceId == receipt.Id &&
                               m.ReferenceType == ReferenceType.PO &&
                               m.WarehouseId != Guid.Empty)
            .AsNoTracking()
            .Select(m => m.WarehouseId)
            .FirstOrDefaultAsync();

        return movementWarehouse;
    }

    private async Task<decimal> ResolveConversionToBaseAsync(PurchaseOrderItem poItem)
    {
        // Prefer explicit PO line UOM linkage
        if (poItem.ItemUnitOfMeasureId.HasValue && poItem.ItemUnitOfMeasureId.Value != Guid.Empty)
        {
            var poUom = await _unitOfWork.Repository<ItemUnitOfMeasure>().GetByIdAsync(poItem.ItemUnitOfMeasureId.Value);
            if (poUom != null && poUom.ConversionToBase > 0)
                return poUom.ConversionToBase;
        }

        // Fallback: match by UOM code within item's UOM schedule
        if (poItem.InventoryItemId.HasValue &&
            poItem.InventoryItemId.Value != Guid.Empty &&
            !string.IsNullOrWhiteSpace(poItem.UnitOfMeasure))
        {
            var match = await _unitOfWork.Repository<ItemUnitOfMeasure>()
                .GetQueryable(u => u.InventoryItemId == poItem.InventoryItemId.Value && u.IsActive)
                .AsNoTracking()
                .Include(u => u.UnitOfMeasure)
                .FirstOrDefaultAsync(u => u.UnitOfMeasure.Code == poItem.UnitOfMeasure);

            if (match != null && match.ConversionToBase > 0)
                return match.ConversionToBase;
        }

        return 1m;
    }

    private static InspectionResult MapInspectionResult(string? qualityStatus)
    {
        if (string.IsNullOrWhiteSpace(qualityStatus)) return InspectionResult.Pending;
        var normalized = qualityStatus.Trim();
        if (normalized.Equals("Passed", StringComparison.OrdinalIgnoreCase)) return InspectionResult.Passed;
        if (normalized.Equals("Failed", StringComparison.OrdinalIgnoreCase)) return InspectionResult.Failed;
        if (normalized.Equals("Conditional", StringComparison.OrdinalIgnoreCase)) return InspectionResult.ConditionalPass;
        if (normalized.Equals("ConditionalPass", StringComparison.OrdinalIgnoreCase)) return InspectionResult.ConditionalPass;
        if (normalized.Equals("Conditional Pass", StringComparison.OrdinalIgnoreCase)) return InspectionResult.ConditionalPass;
        if (normalized.Equals("Pending", StringComparison.OrdinalIgnoreCase)) return InspectionResult.Pending;
        return InspectionResult.Pending;
    }

    public async Task<bool> SetManualAllocationsAsync(Guid landedCostId, Guid landedCostItemId, SetManualLandedCostAllocationsDto dto, Guid userId)
    {
        var landedCost = await _landedCostRepository.GetByIdAsync(landedCostId)
            ?? throw new ArgumentException($"Landed cost {landedCostId} not found");

        var item = await _landedCostItemRepository.GetByIdAsync(landedCostItemId)
            ?? throw new ArgumentException($"Landed cost item {landedCostItemId} not found");

        if (item.LandedCostId != landedCost.Id)
            throw new InvalidOperationException("Landed cost item does not belong to landed cost document");

        var grn = await _grnRepository.GetWithItemsAsync(landedCost.GoodsReceiptNoteId)
            ?? throw new InvalidOperationException("GRN not found for landed cost document");

        item.AllocationMethod = "Manual";
        await _landedCostItemRepository.UpdateAsync(item);

        // Replace allocations for this cost item
        var existing = await _landedCostAllocationRepository.FindAsync(a => a.LandedCostId == landedCost.Id && a.LandedCostItemId == item.Id);
        await _landedCostAllocationRepository.DeleteRangeAsync(existing);

        var allocQtyByGrnItem = GetAllocatableQuantities(grn);
        var grnItemById = grn.Items.ToDictionary(i => i.Id, i => i);

        foreach (var line in dto.Allocations)
        {
            if (!grnItemById.TryGetValue(line.GoodsReceiptNoteItemId, out var grnItem))
                throw new ArgumentException($"GRN item {line.GoodsReceiptNoteItemId} not found for this GRN");

            var qty = allocQtyByGrnItem.TryGetValue(grnItem.Id, out var q) ? q : 0;
            var allocated = RoundMoney(line.AllocatedAmount);

            var allocation = new LandedCostAllocation
            {
                TenantId = grn.TenantId,
                LandedCostId = landedCost.Id,
                LandedCostItemId = item.Id,
                GoodsReceiptNoteItemId = grnItem.Id,
                InventoryItemId = grnItem.InventoryItemId,
                Quantity = qty,
                AllocatedAmount = allocated,
                CostPerUnit = qty > 0 ? allocated / qty : 0,
                Notes = dto.Notes
            };

            await _landedCostAllocationRepository.AddAsync(allocation);
        }

        var sum = dto.Allocations.Sum(a => RoundMoney(a.AllocatedAmount));
        var expected = RoundMoney(item.AmountInBaseCurrency);
        if (sum != expected)
            throw new InvalidOperationException($"Manual allocations must sum to {expected} ({landedCost.Currency}). Current sum is {sum}.");

        await RecalculateHeaderTotalsAsync(landedCost.Id);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<bool> AllocateCostsAsync(Guid landedCostId, Guid userId)
    {
        var landedCost = await _landedCostRepository.GetByIdAsync(landedCostId)
            ?? throw new ArgumentException($"Landed cost {landedCostId} not found");

        if (!string.Equals(landedCost.Status, "Draft", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only Draft landed costs can be allocated");

        var grn = await _grnRepository.GetWithItemsAsync(landedCost.GoodsReceiptNoteId)
            ?? throw new InvalidOperationException("GRN not found for landed cost document");

        if (landedCost.TenantId == Guid.Empty)
        {
            landedCost.TenantId = grn.TenantId;
            await _landedCostRepository.UpdateAsync(landedCost);
        }

        var allocQtyByGrnItem = GetAllocatableQuantities(grn);
        var eligibleGrnItems = grn.Items.Where(i => allocQtyByGrnItem.TryGetValue(i.Id, out var q) && q > 0).ToList();
        if (!eligibleGrnItems.Any())
            throw new InvalidOperationException("No received/accepted GRN lines available for allocation");

        var costItems = await _landedCostItemRepository.GetByLandedCostAsync(landedCost.Id);
        foreach (var costItem in costItems)
        {
            EnsureAllowedAllocationMethod(costItem.AllocationMethod);

            if (string.Equals(costItem.AllocationMethod, "Manual", StringComparison.OrdinalIgnoreCase))
            {
                // Validate manual allocations exist and sum correctly
                var manual = await _landedCostAllocationRepository.FindAsync(a => a.LandedCostId == landedCost.Id && a.LandedCostItemId == costItem.Id);
                var manualSum = manual.Sum(a => RoundMoney(a.AllocatedAmount));
                if (manualSum != RoundMoney(costItem.AmountInBaseCurrency))
                    throw new InvalidOperationException($"Manual allocations for item '{costItem.Description}' must sum to {RoundMoney(costItem.AmountInBaseCurrency)}.");
                continue;
            }

            await AllocateCostItemAsync(landedCost, costItem, eligibleGrnItems, allocQtyByGrnItem);
        }

        landedCost.Status = "Allocated";
        await _landedCostRepository.UpdateAsync(landedCost);

        await RecalculateHeaderTotalsAsync(landedCost.Id);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ApproveAsync(Guid landedCostId, Guid userId)
    {
        var landedCost = await _landedCostRepository.GetByIdAsync(landedCostId)
            ?? throw new ArgumentException($"Landed cost {landedCostId} not found");

        landedCost.Status = "Approved";
        landedCost.ApprovedById = userId;
        landedCost.ApprovedDate = DateTime.UtcNow;
        await _landedCostRepository.UpdateAsync(landedCost);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public Task<bool> PostToInventoryAsync(Guid landedCostId, Guid userId)
    {
        if (_unitOfWork.HasActiveTransaction)
            return PostToInventoryOnlyAsync(landedCostId, userId);
        return _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var result = await PostToInventoryOnlyAsync(landedCostId, userId);
                await _unitOfWork.CommitAsync();
                return result;
            }
            catch
            {
                if (_unitOfWork.HasActiveTransaction) await _unitOfWork.RollbackAsync();
                _unitOfWork.ClearTrackedChanges();
                throw;
            }
        });
    }

    private async Task<bool> PostToInventoryOnlyAsync(Guid landedCostId, Guid userId)
    {
        var landedCost = await _landedCostRepository.GetWithDetailsAsync(landedCostId)
            ?? throw new ArgumentException($"Landed cost {landedCostId} not found");

        if (string.Equals(landedCost.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Cannot post a cancelled landed cost");

        if (string.Equals(landedCost.Status, "Posted", StringComparison.OrdinalIgnoreCase))
        {
            await PostFinanceAsync(landedCost.Id);
            return true;
        }

        if (!string.Equals(landedCost.Status, "Allocated", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(landedCost.Status, "Approved", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Landed cost must be Allocated or Approved before posting to inventory");

        await RecalculateHeaderTotalsAsync(landedCost.Id);
        await _unitOfWork.SaveChangesAsync();

        // Reload header (totals were recalculated)
        landedCost = await _landedCostRepository.GetWithDetailsAsync(landedCostId)
            ?? throw new InvalidOperationException("Failed to reload landed cost after recalculation");

        if (RoundMoney(landedCost.UnallocatedAmount) != 0)
            throw new InvalidOperationException($"Cannot post landed cost with unallocated amount {RoundMoney(landedCost.UnallocatedAmount)} {landedCost.Currency}. Allocate all costs first.");

        var grn = await _grnRepository.GetWithItemsAsync(landedCost.GoodsReceiptNoteId)
            ?? throw new InvalidOperationException("GRN not found for landed cost document");

        // Guard: landed costs should be posted only after the receipt has posted inventory valuation (layers/balances).
        // If the receipt didn't create valuation movements (e.g., pending inspection, missing warehouse during receipt),
        // we would otherwise push everything into "variance stub" which is confusing for users.
        var receiptPostedToValuation = await _unitOfWork.Repository<InventoryMovement>()
            .GetQueryable()
            .AnyAsync(m =>
                m.ReferenceId == grn.Id &&
                m.ReferenceType == ReferenceType.PO &&
                m.MovementType == InventoryMovementType.PurchaseReceipt &&
                m.IsPosted);
        if (!receiptPostedToValuation)
        {
            throw new InvalidOperationException(
                "This GRN has not been posted to inventory valuation yet. Complete/inspect the receipt and ensure stock was posted before posting landed cost to inventory.");
        }

        // Ensure tenant is set correctly (avoid FK issues when tenant-scoped DbContext isn't configured)
        if (landedCost.TenantId == Guid.Empty)
        {
            landedCost.TenantId = grn.TenantId;
            await _landedCostRepository.UpdateAsync(landedCost);
        }

        var allocations = landedCost.Allocations?.Where(a => !a.IsDeleted).ToList() ?? [];
        if (allocations.Count == 0)
            throw new InvalidOperationException("No allocations found. Allocate costs first.");

        var itemIds = allocations.Select(a => a.InventoryItemId).Where(id => id != Guid.Empty).Distinct().ToList();
        var inventoryItems = await _unitOfWork.Repository<InventoryItem>()
            .GetQueryable(i => itemIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i);

        var locationIds = allocations
            .Select(a => a.GoodsReceiptNoteItem?.StorageLocationId)
            .Where(id => id.HasValue && id.Value != Guid.Empty)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        // Load balances for the affected item/warehouse/location keys
        var balances = await _unitOfWork.Repository<InventoryBalance>()
            .GetQueryable()
            .Where(b => itemIds.Contains(b.InventoryItemId) && b.WarehouseId == grn.WarehouseId)
            .ToListAsync();

        var balanceByKey = balances.ToDictionary(b => (b.InventoryItemId, b.WarehouseId, b.LocationId));

        // Load receipt FIFO layers once (for matching by item/location/lot)
        var receiptLayers = await _unitOfWork.Repository<InventoryLayer>()
            .GetQueryable()
            .Where(l => itemIds.Contains(l.InventoryItemId) &&
                        l.WarehouseId == grn.WarehouseId &&
                        l.IsActive &&
                        l.SourceType == ReferenceType.PO.ToString() &&
                        l.SourceId == grn.Id)
            .ToListAsync();

        var allocationsByKey = allocations
            .Where(a => a.InventoryItemId != Guid.Empty)
            .GroupBy(a => new
            {
                a.InventoryItemId,
                LocationId = a.GoodsReceiptNoteItem?.StorageLocationId,
                LotNumber = a.GoodsReceiptNoteItem?.LotNumber
            })
            .ToList();

        foreach (var group in allocationsByKey)
        {
            if (!inventoryItems.TryGetValue(group.Key.InventoryItemId, out var invItem))
                continue;

            var locationId = group.Key.LocationId.HasValue && group.Key.LocationId.Value != Guid.Empty
                ? group.Key.LocationId.Value
                : (Guid?)null;

            var totalAllocatedAmount = RoundMoney(group.Sum(x => x.AllocatedAmount));
            var totalAllocatedQty = group.Sum(x => x.Quantity);

            if (totalAllocatedAmount == 0)
                continue;

            var method = invItem.ValuationMethod;

            decimal applyToInventory = 0;
            decimal varianceStub = 0;

            if (method == ValuationMethod.StandardCost)
            {
                // Standard cost items stay at standard cost; variances are posted to GL later.
                varianceStub = totalAllocatedAmount;
            }
            else if (method == ValuationMethod.FIFO)
            {
                var matchingLayers = receiptLayers
                    .Where(l =>
                        l.InventoryItemId == invItem.Id &&
                        (!locationId.HasValue || l.LocationId == locationId.Value) &&
                        (string.IsNullOrWhiteSpace(group.Key.LotNumber) || l.LotNumber == group.Key.LotNumber))
                    .ToList();

                // Best-effort traceability: map each allocation row to a FIFO layer created by this receipt.
                // If there are multiple layers (e.g., multiple receipt lines for the same item/location/lot),
                // match by closest original quantity and assign each layer at most once.
                if (matchingLayers.Count > 0)
                {
                    var availableLayers = matchingLayers
                        .OrderByDescending(l => l.OriginalQuantity)
                        .ThenBy(l => l.LayerDate)
                        .ToList();

                    var allocRows = group
                        .Where(a => a.Quantity > 0 && a.InventoryItemId != Guid.Empty)
                        .OrderByDescending(a => a.Quantity)
                        .ToList();

                    foreach (var alloc in allocRows)
                    {
                        var best = availableLayers
                            .OrderBy(l => Math.Abs(l.OriginalQuantity - alloc.Quantity))
                            .ThenByDescending(l => l.RemainingQuantity)
                            .FirstOrDefault();

                        if (best != null)
                        {
                            alloc.InventoryLayerId = best.Id;
                            availableLayers.Remove(best);
                        }
                    }
                }

                var remainingQty = matchingLayers.Sum(l => l.RemainingQuantity);
                if (remainingQty <= 0 || totalAllocatedQty <= 0)
                {
                    varianceStub = totalAllocatedAmount;
                }
                else
                {
                    var ratio = Math.Min(1m, remainingQty / totalAllocatedQty);
                    applyToInventory = RoundMoney(totalAllocatedAmount * ratio);
                    varianceStub = RoundMoney(totalAllocatedAmount - applyToInventory);

                    if (applyToInventory != 0)
                    {
                        var totalRemainingQty = matchingLayers.Sum(l => l.RemainingQuantity);
                        if (totalRemainingQty > 0)
                        {
                            foreach (var layer in matchingLayers)
                            {
                                if (layer.RemainingQuantity <= 0) continue;

                                var layerCostInc = applyToInventory * (layer.RemainingQuantity / totalRemainingQty);
                                var unitInc = layer.RemainingQuantity > 0 ? layerCostInc / layer.RemainingQuantity : 0;
                                layer.UnitCost += unitInc;
                                layer.RemainingValue += layerCostInc;
                            }
                        }
                    }
                }
            }
            else // WeightedAverage
            {
                // For WAC, apply to the current on-hand value (if any). If stock is zero, treat as variance.
                var key = (invItem.Id, grn.WarehouseId, locationId);
                if (!balanceByKey.TryGetValue(key, out var balance))
                {
                    balance = new InventoryBalance
                    {
                        TenantId = landedCost.TenantId,
                        InventoryItemId = invItem.Id,
                        WarehouseId = grn.WarehouseId,
                        LocationId = locationId,
                        QuantityOnHand = 0,
                        QuantityAllocated = 0,
                        QuantityAvailable = 0,
                        QuantityOnOrder = 0,
                        TotalValue = 0,
                        AverageUnitCost = 0,
                        LastRecalculatedAt = DateTime.UtcNow
                    };
                    await _unitOfWork.Repository<InventoryBalance>().AddAsync(balance);
                    balanceByKey[key] = balance;
                }

                if (balance.QuantityOnHand <= 0)
                {
                    varianceStub = totalAllocatedAmount;
                }
                else
                {
                    applyToInventory = totalAllocatedAmount;
                }
            }

            // Update balance for FIFO/WAC when applying to inventory value
            if (applyToInventory != 0)
            {
                var key = (invItem.Id, grn.WarehouseId, locationId);
                if (!balanceByKey.TryGetValue(key, out var balance))
                {
                    balance = new InventoryBalance
                    {
                        TenantId = landedCost.TenantId,
                        InventoryItemId = invItem.Id,
                        WarehouseId = grn.WarehouseId,
                        LocationId = locationId,
                        QuantityOnHand = 0,
                        QuantityAllocated = 0,
                        QuantityAvailable = 0,
                        QuantityOnOrder = 0,
                        TotalValue = 0,
                        AverageUnitCost = 0,
                        LastRecalculatedAt = DateTime.UtcNow
                    };
                    await _unitOfWork.Repository<InventoryBalance>().AddAsync(balance);
                    balanceByKey[key] = balance;
                }

                balance.TotalValue += applyToInventory;
                balance.AverageUnitCost = balance.QuantityOnHand > 0 ? balance.TotalValue / balance.QuantityOnHand : balance.AverageUnitCost;
                balance.LastMovementDate = DateTime.UtcNow;
                balance.LastRecalculatedAt = DateTime.UtcNow;

                await CreateValueAdjustmentMovementAsync(
                    tenantId: landedCost.TenantId,
                    inventoryItemId: invItem.Id,
                    warehouseId: grn.WarehouseId,
                    locationId: locationId,
                    valueAdjustment: applyToInventory,
                    referenceNumber: landedCost.LandedCostNumber,
                    referenceId: landedCost.Id,
                    runningBalance: balance.QuantityOnHand,
                    runningValue: balance.TotalValue,
                    varianceAmount: null,
                    notes: $"Landed cost revaluation ({landedCost.LandedCostNumber})",
                    userId: userId);
            }

            // Record variance stub (for later GL posting) if any.
            if (varianceStub != 0)
            {
                var key = (invItem.Id, grn.WarehouseId, locationId);
                if (!balanceByKey.TryGetValue(key, out var balance))
                {
                    // create a minimal balance entry so RunningValue is meaningful in the movement record
                    balance = new InventoryBalance
                    {
                        TenantId = landedCost.TenantId,
                        InventoryItemId = invItem.Id,
                        WarehouseId = grn.WarehouseId,
                        LocationId = locationId,
                        QuantityOnHand = 0,
                        QuantityAllocated = 0,
                        QuantityAvailable = 0,
                        QuantityOnOrder = 0,
                        TotalValue = 0,
                        AverageUnitCost = 0,
                        LastRecalculatedAt = DateTime.UtcNow
                    };
                    await _unitOfWork.Repository<InventoryBalance>().AddAsync(balance);
                    balanceByKey[key] = balance;
                }

                await CreateValueAdjustmentMovementAsync(
                    tenantId: landedCost.TenantId,
                    inventoryItemId: invItem.Id,
                    warehouseId: grn.WarehouseId,
                    locationId: locationId,
                    valueAdjustment: 0,
                    referenceNumber: landedCost.LandedCostNumber,
                    referenceId: landedCost.Id,
                    runningBalance: balance.QuantityOnHand,
                    runningValue: balance.TotalValue,
                    varianceAmount: varianceStub,
                    notes: $"Landed cost variance stub ({landedCost.LandedCostNumber}) - GL posting pending",
                    userId: userId);
            }
        }

        // Update WarehouseQuantity + InventoryItem average costs to reflect updated balances (best-effort)
        foreach (var itemId in itemIds)
        {
            var warehouseBalances = balanceByKey.Values.Where(b => b.InventoryItemId == itemId && b.WarehouseId == grn.WarehouseId).ToList();
            var whQty = warehouseBalances.Sum(b => b.QuantityOnHand);
            var whVal = warehouseBalances.Sum(b => b.TotalValue);
            var warehouseAvg = whQty > 0 ? whVal / whQty : 0;

            var warehouseQty = await _unitOfWork.Repository<WarehouseQuantity>()
                .FirstOrDefaultAsync(q => q.InventoryItemId == itemId && q.WarehouseId == grn.WarehouseId && !q.IsDeleted);
            if (warehouseQty != null)
            {
                warehouseQty.AverageCost = warehouseAvg;
                warehouseQty.LastMovementDate = DateTime.UtcNow;
                await _unitOfWork.Repository<WarehouseQuantity>().UpdateAsync(warehouseQty);
            }

            // Item-level average across all warehouses (derived from valuation balances)
            var allBalances = await _unitOfWork.Repository<InventoryBalance>()
                .GetQueryable()
                .Where(b => b.InventoryItemId == itemId)
                .AsNoTracking()
                .ToListAsync();
            var totalQty = allBalances.Sum(b => b.QuantityOnHand);
            var totalVal = allBalances.Sum(b => b.TotalValue);
            var itemAvg = totalQty > 0 ? totalVal / totalQty : 0;

            if (inventoryItems.TryGetValue(itemId, out var item))
            {
                item.AverageCost = itemAvg;
                await _unitOfWork.Repository<InventoryItem>().UpdateAsync(item);
            }
        }

        landedCost.Status = "Posted";
        landedCost.PostedById = userId;
        landedCost.PostedDate = DateTime.UtcNow;
        await _landedCostRepository.UpdateAsync(landedCost);

        await _unitOfWork.SaveChangesAsync();
        await PostFinanceAsync(landedCost.Id);
        return true;
    }

    private Task<InventoryFinancePostingResult> PostFinanceAsync(Guid landedCostId)
    {
        if (_financePosting is null)
            throw new InvalidOperationException(
                "Inventory landed-cost Finance posting is not configured. The valuation transaction was rolled back.");
        return _financePosting.PostLandedCostAsync(landedCostId);
    }

    private async Task CreateValueAdjustmentMovementAsync(
        Guid tenantId,
        Guid inventoryItemId,
        Guid warehouseId,
        Guid? locationId,
        decimal valueAdjustment,
        string? referenceNumber,
        Guid? referenceId,
        decimal runningBalance,
        decimal runningValue,
        decimal? varianceAmount,
        string? notes,
        Guid userId)
    {
        var movementNumber = await GenerateInventoryMovementNumberAsync();
        var movement = new InventoryMovement
        {
            TenantId = tenantId,
            MovementNumber = movementNumber,
            InventoryItemId = inventoryItemId,
            WarehouseId = warehouseId,
            LocationId = locationId,
            MovementType = InventoryMovementType.LandedCostRevaluation,
            Direction = valueAdjustment >= 0 ? MovementDirection.In : MovementDirection.Out,
            Quantity = 0,
            UnitCost = 0,
            TotalValue = valueAdjustment,
            MovementDate = DateTime.UtcNow,
            PostingDate = DateTime.UtcNow,
            ReferenceType = ReferenceType.Adjustment,
            ReferenceNumber = referenceNumber,
            ReferenceId = referenceId,
            RunningBalance = runningBalance,
            RunningValue = runningValue,
            VarianceAmount = varianceAmount,
            IsPosted = true,
            IsReversal = false,
            Notes = notes,
            CreatedById = userId,
            PostedById = userId,
            PostedAt = DateTime.UtcNow
        };

        await _unitOfWork.Repository<InventoryMovement>().AddAsync(movement);
    }

    private async Task<string> GenerateInventoryMovementNumberAsync()
    {
        // Keep it simple and deterministic enough for auditing; collisions are extremely unlikely.
        // Format: IMV-YYMM-{6chars}
        var stamp = DateTime.UtcNow.ToString("yyMM");
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        return await Task.FromResult($"IMV-{stamp}-{suffix}");
    }

    public async Task<bool> CancelAsync(Guid landedCostId, string reason, Guid userId)
    {
        var landedCost = await _landedCostRepository.GetByIdAsync(landedCostId)
            ?? throw new ArgumentException($"Landed cost {landedCostId} not found");

        landedCost.Status = "Cancelled";
        landedCost.Notes = string.IsNullOrWhiteSpace(reason) ? landedCost.Notes : reason;
        await _landedCostRepository.UpdateAsync(landedCost);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    private async Task AllocateCostItemAsync(
        LandedCost landedCost,
        LandedCostItem costItem,
        List<GoodsReceiptNoteItem> grnItems,
        Dictionary<Guid, decimal> allocQtyByGrnItem)
    {
        // Replace allocations for this cost item
        var existing = await _landedCostAllocationRepository.FindAsync(a => a.LandedCostId == landedCost.Id && a.LandedCostItemId == costItem.Id);
        await _landedCostAllocationRepository.DeleteRangeAsync(existing);

        var amount = RoundMoney(costItem.AmountInBaseCurrency);
        if (amount <= 0) return;

        var weights = grnItems.Select(i => new
        {
            GrnItem = i,
            Weight = GetBasisWeight(costItem.AllocationMethod, i, allocQtyByGrnItem[i.Id])
        }).ToList();

        var totalWeight = weights.Sum(w => w.Weight);
        if (totalWeight <= 0)
            throw new InvalidOperationException($"Allocation method '{costItem.AllocationMethod}' cannot be applied because all basis values are zero.");

        decimal allocatedSoFar = 0;
        for (var index = 0; index < weights.Count; index++)
        {
            var entry = weights[index];
            var qty = allocQtyByGrnItem[entry.GrnItem.Id];

            var raw = amount * (entry.Weight / totalWeight);
            var allocated = index == weights.Count - 1
                ? RoundMoney(amount - allocatedSoFar)
                : RoundMoney(raw);

            allocatedSoFar += allocated;

            var allocation = new LandedCostAllocation
            {
                TenantId = landedCost.TenantId,
                LandedCostId = landedCost.Id,
                LandedCostItemId = costItem.Id,
                GoodsReceiptNoteItemId = entry.GrnItem.Id,
                InventoryItemId = entry.GrnItem.InventoryItemId,
                Quantity = qty,
                AllocatedAmount = allocated,
                CostPerUnit = qty > 0 ? allocated / qty : 0
            };

            await _landedCostAllocationRepository.AddAsync(allocation);
        }
    }

    private static decimal GetBasisWeight(string method, GoodsReceiptNoteItem item, decimal allocQty)
    {
        if (allocQty <= 0) return 0;

        if (method.Equals("ByQuantity", StringComparison.OrdinalIgnoreCase))
            return allocQty;

        if (method.Equals("Equal", StringComparison.OrdinalIgnoreCase))
            return 1;

        if (method.Equals("ByValue", StringComparison.OrdinalIgnoreCase))
        {
            var value = item.LineValue > 0 ? item.LineValue : item.UnitCost * allocQty;
            return value > 0 ? value : 0;
        }

        if (method.Equals("ByWeight", StringComparison.OrdinalIgnoreCase))
        {
            var unitWeight = item.InventoryItem?.Weight ?? 0;
            return unitWeight > 0 ? unitWeight * allocQty : 0;
        }

        if (method.Equals("ByVolume", StringComparison.OrdinalIgnoreCase))
        {
            var unitVolume = item.InventoryItem?.Volume ?? 0;
            return unitVolume > 0 ? unitVolume * allocQty : 0;
        }

        throw new InvalidOperationException($"Unsupported allocation method '{method}'.");
    }

    private static Dictionary<Guid, decimal> GetAllocatableQuantities(GoodsReceiptNote grn)
    {
        var dict = new Dictionary<Guid, decimal>();
        foreach (var item in grn.Items)
        {
            var qty = item.AcceptedQuantity > 0 ? item.AcceptedQuantity : item.ReceivedQuantity;
            if (qty < 0) qty = 0;
            dict[item.Id] = qty;
        }
        return dict;
    }

    private async Task SoftDeleteAllocationsAsync(Guid landedCostId)
    {
        var allocations = await _landedCostAllocationRepository.GetByLandedCostAsync(landedCostId);
        await _landedCostAllocationRepository.DeleteRangeAsync(allocations);
    }

    private async Task RecalculateHeaderTotalsAsync(Guid landedCostId)
    {
        var landedCost = await _landedCostRepository.GetWithDetailsAsync(landedCostId);
        if (landedCost == null) return;

        var items = landedCost.Items.Where(i => !i.IsDeleted).ToList();
        var allocations = landedCost.Allocations.Where(a => !a.IsDeleted).ToList();

        landedCost.TotalCost = RoundMoney(items.Sum(i => i.AmountInBaseCurrency));
        landedCost.AllocatedAmount = RoundMoney(allocations.Sum(a => a.AllocatedAmount));
        landedCost.UnallocatedAmount = RoundMoney(landedCost.TotalCost - landedCost.AllocatedAmount);

        await _landedCostRepository.UpdateAsync(landedCost);
    }

    private async Task<string?> ResolveSupplierNameAsync(Guid? supplierId)
    {
        if (supplierId == null || supplierId == Guid.Empty) return null;
        var partner = await _businessPartnerRepository.GetByIdAsync(supplierId.Value);
        return partner?.PartnerName;
    }

    private static void EnsureAllowedAllocationMethod(string? method)
    {
        if (string.IsNullOrWhiteSpace(method))
            throw new ArgumentException("AllocationMethod is required");

        if (!AllowedAllocationMethods.Contains(method))
            throw new ArgumentException($"Unsupported AllocationMethod '{method}'. Allowed: {string.Join(", ", AllowedAllocationMethods.OrderBy(x => x))}");
    }

    private async Task<string> GenerateLandedCostNumberAsync()
    {
        var yearPrefix = DateTime.UtcNow.ToString("yy");
        var monthPrefix = DateTime.UtcNow.ToString("MM");
        var sequence = await GetNextSequenceAsync();
        return $"LC{yearPrefix}{monthPrefix}{sequence:D4}";
    }

    private Task<int> GetNextSequenceAsync()
    {
        return Task.FromResult(new Random().Next(1, 9999));
    }

    private static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static LandedCostDto MapToDto(LandedCost landedCost)
    {
        return new LandedCostDto
        {
            Id = landedCost.Id,
            LandedCostNumber = landedCost.LandedCostNumber,
            GoodsReceiptNoteId = landedCost.GoodsReceiptNoteId,
            GRNNumber = landedCost.GRNNumber ?? string.Empty,
            Status = landedCost.Status,
            TotalCostAmount = landedCost.TotalCost,
            AllocatedAmount = landedCost.AllocatedAmount,
            Currency = landedCost.Currency,
            Notes = landedCost.Notes,
            CreatedAtFormatted = landedCost.CreatedAt.ToString("yyyy-MM-dd HH:mm")
        };
    }

    private static LandedCostDetailDto MapToDetailDto(LandedCost landedCost, GoodsReceiptNote? grn)
    {
        var grnItems = grn?.Items?.ToDictionary(i => i.Id, i => i) ?? new Dictionary<Guid, GoodsReceiptNoteItem>();
        var allocations = landedCost.Allocations.Where(a => !a.IsDeleted).ToList();
        var total = RoundMoney(landedCost.TotalCost);

        var allocationDtos = allocations
            .GroupBy(a => a.GoodsReceiptNoteItemId)
            .Select(g =>
            {
                var grnItem = grnItems.TryGetValue(g.Key, out var i) ? i : null;
                var totalAllocated = RoundMoney(g.Sum(a => a.AllocatedAmount));
                var qty = grnItem != null ? (grnItem.AcceptedQuantity > 0 ? grnItem.AcceptedQuantity : grnItem.ReceivedQuantity) : 0;
                var perUnit = qty > 0 ? totalAllocated / qty : 0;

                return new LandedCostAllocationDto
                {
                    // Allocations are aggregated per GRN line for display.
                    // Use the GRN line id as a stable unique key for the UI.
                    Id = g.Key,
                    GRNItemId = g.Key,
                    ItemCode = grnItem?.ItemCode ?? string.Empty,
                    ItemName = grnItem?.ItemName ?? string.Empty,
                    AllocatedAmount = totalAllocated,
                    AllocationPercent = total > 0 ? RoundMoney((totalAllocated / total) * 100m) : 0,
                    NewUnitCost = (grnItem?.UnitCost ?? 0) + perUnit
                };
            })
            .ToList();

        return new LandedCostDetailDto
        {
            Id = landedCost.Id,
            LandedCostNumber = landedCost.LandedCostNumber,
            GoodsReceiptNoteId = landedCost.GoodsReceiptNoteId,
            GRNNumber = landedCost.GRNNumber ?? string.Empty,
            Status = landedCost.Status,
            TotalCostAmount = landedCost.TotalCost,
            AllocatedAmount = landedCost.AllocatedAmount,
            Currency = landedCost.Currency,
            Notes = landedCost.Notes,
            ApprovedByName = landedCost.ApprovedBy?.FullName,
            ApprovedDate = landedCost.ApprovedDate,
            CreatedAtFormatted = landedCost.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
            CostItems = landedCost.Items
                .Where(i => !i.IsDeleted)
                .Select(i => new LandedCostItemDto
                {
                    Id = i.Id,
                    CostType = i.CostType,
                    Description = i.Description ?? string.Empty,
                    Amount = i.Amount,
                    Currency = i.Currency,
                    ExchangeRate = i.ExchangeRate,
                    AmountInBaseCurrency = i.AmountInBaseCurrency,
                    AllocationMethod = i.AllocationMethod,
                    SupplierId = i.SupplierId,
                    SupplierName = i.SupplierName,
                    ReferenceNumber = i.ReferenceNumber
                })
                .ToList(),
            Allocations = allocationDtos
        };
    }
}
