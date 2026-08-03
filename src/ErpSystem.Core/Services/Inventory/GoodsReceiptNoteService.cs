using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using Microsoft.EntityFrameworkCore;
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
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementAccessControlService _accessControl;
    private readonly IProcurementPurchaseOrderSodService _purchaseOrderSod;
    private readonly IProcurementReceiptSourceControlService _receiptSourceControl;
    private readonly IProcurementReceiptInspectionService _receiptInspection;
    private readonly IProcurementReceiptDocumentService _receiptDocuments;
    private readonly IInventoryTrackingControlService _trackingControls;
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
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService accessControl,
        IProcurementPurchaseOrderSodService purchaseOrderSod,
        IProcurementReceiptSourceControlService receiptSourceControl,
        IProcurementReceiptInspectionService receiptInspection,
        IProcurementReceiptDocumentService receiptDocuments,
        IInventoryTrackingControlService trackingControls,
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
        _currentUser = currentUser;
        _accessControl = accessControl;
        _purchaseOrderSod = purchaseOrderSod;
        _receiptSourceControl = receiptSourceControl;
        _receiptInspection = receiptInspection;
        _receiptDocuments = receiptDocuments;
        _trackingControls = trackingControls;
        _logger = logger;
    }

    public async Task<IEnumerable<GoodsReceiptNoteDto>> GetAllAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var start = fromDate ?? DateTime.UtcNow.AddMonths(-3);
        var end = toDate ?? DateTime.UtcNow;
        var grns = await GrnQuery(includeItems: true)
            .Where(item => item.ReceiptDate >= start && item.ReceiptDate <= end)
            .OrderByDescending(item => item.ReceiptDate)
            .ToListAsync();
        return (await FilterReadableAsync(grns)).Select(MapToDto);
    }

    public async Task<IEnumerable<GoodsReceiptNoteDto>> GetByWarehouseAsync(Guid warehouseId)
    {
        var grns = await GrnQuery(includeItems: true)
            .Where(item => item.WarehouseId == warehouseId)
            .OrderByDescending(item => item.ReceiptDate)
            .ToListAsync();
        return (await FilterReadableAsync(grns)).Select(MapToDto);
    }

    public async Task<IEnumerable<GoodsReceiptNoteDto>> GetBySupplierAsync(Guid supplierId)
    {
        var grns = await GrnQuery(includeItems: true)
            .Where(item => item.SupplierId == supplierId)
            .OrderByDescending(item => item.ReceiptDate)
            .ToListAsync();
        return (await FilterReadableAsync(grns)).Select(MapToDto);
    }

    public async Task<IEnumerable<GoodsReceiptNoteDto>> GetByPurchaseOrderAsync(Guid purchaseOrderId)
    {
        var grns = await GrnQuery(includeItems: true)
            .Where(item => item.PurchaseOrderId == purchaseOrderId)
            .OrderByDescending(item => item.ReceiptDate)
            .ToListAsync();
        return (await FilterReadableAsync(grns)).Select(MapToDto);
    }

    public async Task<GoodsReceiptNoteDetailDto?> GetByIdAsync(Guid id)
    {
        var grn = await GrnQuery(includeItems: true)
            .SingleOrDefaultAsync(item => item.Id == id);
        return grn != null && await CanReadAsync(grn)
            ? MapToDetailDto(grn)
            : null;
    }

    public async Task<GoodsReceiptNoteDetailDto?> GetByGRNNumberAsync(string grnNumber)
    {
        var grn = await GrnQuery(includeItems: true)
            .SingleOrDefaultAsync(item => item.GRNNumber == grnNumber);
        return grn != null && await CanReadAsync(grn)
            ? MapToDetailDto(grn)
            : null;
    }

    public Task<GoodsReceiptNoteDto> CreateAsync(
        CreateGoodsReceiptNoteDto dto,
        Guid userId) =>
        _unitOfWork.HasActiveTransaction
            ? CreateCoreAsync(dto, userId)
            : _unitOfWork.ExecuteInStrategyAsync(
                () => CreateCoreAsync(dto, userId));

    private async Task<GoodsReceiptNoteDto> CreateCoreAsync(
        CreateGoodsReceiptNoteDto dto,
        Guid userId)
    {
        EnsureTenant();
        if (!dto.PurchaseOrderId.HasValue ||
            dto.PurchaseOrderId.Value == Guid.Empty)
        {
            throw new ProcurementReceiptSourceValidationException(
                "RCV_SOURCE_REQUIRED",
                "A governed purchase order is required to create a goods receipt note.");
        }

        var correlationId = NormalizeCorrelation(dto.IdempotencyKey);
        var idempotencyKey = string.IsNullOrWhiteSpace(dto.IdempotencyKey)
            ? correlationId
            : dto.IdempotencyKey.Trim();
        if (idempotencyKey.Length > 100)
        {
            throw new ProcurementReceiptSourceValidationException(
                "RCV_IDEMPOTENCY_KEY_INVALID",
                "Idempotency key cannot exceed 100 characters.");
        }

        var ownsTransaction = false;
        try
        {
            if (!_unitOfWork.HasActiveTransaction)
            {
                await _unitOfWork.BeginTransactionAsync(
                    IsolationLevel.Serializable);
                ownsTransaction = true;
            }

            var purchaseOrder = await _unitOfWork.Repository<PurchaseOrder>()
                .GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.Id == dto.PurchaseOrderId.Value &&
                    !item.IsDeleted)
                .Include(item => item.BusinessPartner)
                .SingleOrDefaultAsync()
                ?? throw new ProcurementReceiptSourceNotFoundException(
                    "RCV_PURCHASE_ORDER_NOT_FOUND",
                    "The purchase order was not found in the current tenant.");
            await _purchaseOrderSod.EnforceReceiptActionAsync(
                purchaseOrder,
                ProcurementPurchaseOrderSodRules.CreateGoodsReceiptNote,
                correlationId);
            var requestHash = BuildIdempotencyRequestHash(
                dto,
                purchaseOrder.BusinessPartnerId);

            var existing = await _unitOfWork.Repository<GoodsReceiptNote>()
                .GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.PurchaseOrderId == purchaseOrder.Id &&
                    item.IdempotencyKey == idempotencyKey &&
                    !item.IsDeleted)
                .AsNoTracking()
                .SingleOrDefaultAsync();
            if (existing != null)
            {
                if (string.IsNullOrWhiteSpace(existing.IdempotencyRequestHash) ||
                    !string.Equals(
                        existing.IdempotencyRequestHash,
                        requestHash,
                        StringComparison.Ordinal))
                    throw new ProcurementReceiptSourceValidationException(
                        "RCV_IDEMPOTENCY_PAYLOAD_MISMATCH",
                        "The idempotency key is already bound to a different goods-receipt payload.");
                return MapToDto(existing);
            }

            var warehouse = await _unitOfWork.Repository<Warehouse>()
                .GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.Id == dto.WarehouseId &&
                    !item.IsDeleted)
                .SingleOrDefaultAsync()
                ?? throw new ProcurementReceiptSourceValidationException(
                    "RCV_WAREHOUSE_NOT_FOUND",
                    "The receiving warehouse was not found in the current tenant.");
            if (dto.SupplierId.HasValue &&
                dto.SupplierId.Value != Guid.Empty &&
                dto.SupplierId.Value != purchaseOrder.BusinessPartnerId)
            {
                throw new ProcurementReceiptSourceValidationException(
                    "RCV_SUPPLIER_MISMATCH",
                    "The goods receipt supplier does not match the governed purchase order.");
            }

            if (dto.ReceivingLocationId.HasValue &&
                dto.ReceivingLocationId.Value != Guid.Empty)
            {
                var locationIsValid = await _unitOfWork
                    .Repository<WarehouseLocation>()
                    .GetQueryable(item =>
                        item.TenantId == _currentUser.TenantId &&
                        item.Id == dto.ReceivingLocationId.Value &&
                        item.InventoryWarehouseId == dto.WarehouseId &&
                        !item.IsDeleted)
                    .AnyAsync();
                if (!locationIsValid)
                {
                    throw new ProcurementReceiptSourceValidationException(
                        "RCV_LOCATION_INVALID",
                        "The receiving location does not belong to the selected warehouse.");
                }
            }

            var requestedLocations = dto.Items
                .Select(item => item.StorageLocationId ?? dto.ReceivingLocationId)
                .Distinct()
                .ToList();
            if (requestedLocations.Count == 0)
                requestedLocations.Add(dto.ReceivingLocationId);
            foreach (var locationId in requestedLocations)
            {
                await EnsureCapabilityAsync(
                    "procurement.inventory.receive",
                    $"{idempotencyKey}:create",
                    dto.WarehouseId,
                    locationId,
                    requireLocationScope: true);
            }

            var grnId = Guid.NewGuid();
            var governedReceiptId = Guid.NewGuid();
            var sourceSnapshot =
                await _receiptSourceControl.EnforceCreateAsync(
                    purchaseOrder,
                    dto.Items.Select(item =>
                        new ProcurementReceiptSourceLineRequest
                        {
                            PurchaseOrderItemId =
                                item.PurchaseOrderItemId,
                            InventoryItemId = item.InventoryItemId,
                            ReceivedQuantity =
                                item.ReceivedQuantity
                        }).ToList(),
                    "PurchaseOrderReceipt",
                    governedReceiptId,
                    correlationId);
            var purchaseOrderItems = await _unitOfWork
                .Repository<PurchaseOrderItem>()
                .GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.PurchaseOrderId == purchaseOrder.Id &&
                    dto.Items.Select(request =>
                            request.PurchaseOrderItemId)
                        .Contains(item.Id) &&
                    !item.IsDeleted)
                .AsNoTracking()
                .ToDictionaryAsync(item => item.Id);
            foreach (var itemDto in dto.Items)
            {
                if (!purchaseOrderItems.TryGetValue(itemDto.PurchaseOrderItemId,
                        out var purchaseOrderItem) ||
                    purchaseOrderItem.InventoryItemId != itemDto.InventoryItemId)
                {
                    throw new ProcurementReceiptSourceValidationException(
                        "RCV_INVENTORY_ITEM_MISMATCH",
                        "The goods receipt item does not match its governed purchase-order line.");
                }

                var stockLocationId = itemDto.StorageLocationId ??
                                      dto.ReceivingLocationId;
                if (!stockLocationId.HasValue ||
                    stockLocationId.Value == Guid.Empty)
                {
                    throw new ProcurementReceiptSourceValidationException(
                        "RCV_STOCK_LOCATION_REQUIRED",
                        "Every governed goods-receipt line requires a tenant-valid storage location before inspection approval.");
                }

                var stockLocationIsValid = await _unitOfWork
                    .Repository<WarehouseLocation>()
                    .GetQueryable(item =>
                        item.TenantId == _currentUser.TenantId &&
                        item.Id == stockLocationId.Value &&
                        item.InventoryWarehouseId == dto.WarehouseId &&
                        !item.IsDeleted)
                    .AnyAsync();
                if (!stockLocationIsValid)
                {
                    throw new ProcurementReceiptSourceValidationException(
                        "RCV_LOCATION_INVALID",
                        "Every governed goods-receipt line location must belong to the selected warehouse and tenant.");
                }
            }

            // TDC-0502: the Inventory GRN is a projection of the same governed
            // procurement receipt. It is not a second inspection or stock-posting
            // authority.
            var receiptIdempotencyKey =
                BuildGovernedReceiptIdempotencyKey(idempotencyKey);
            var governedReceipt = new PurchaseOrderReceipt
            {
                Id = governedReceiptId,
                TenantId = _currentUser.TenantId,
                PurchaseOrderId = purchaseOrder.Id,
                ReceiptNumber = $"POR-GRN-{governedReceiptId:N}",
                ReceiptDate = DateTime.UtcNow,
                DeliveryNote = dto.DeliveryNoteNumber,
                Status = "Pending Inspection",
                ReceivedById = _currentUser.UserId,
                Notes = dto.Notes,
                RequiresInspection = true,
                InspectionResult = "Pending",
                IdempotencyKey = receiptIdempotencyKey,
                CorrelationId = correlationId,
                ReceiptTolerancePercent = sourceSnapshot.TolerancePercent,
                ReceiptSourceSnapshotJson = sourceSnapshot.SnapshotJson,
                ReceiptSourceIntegrityHash = sourceSnapshot.IntegrityHash,
                ReceiptSourceValidatedAtUtc = sourceSnapshot.ValidatedAtUtc,
                CreatedAt = DateTime.UtcNow,
                CreatedById = _currentUser.UserId
            };
            await _unitOfWork.Repository<PurchaseOrderReceipt>().AddAsync(governedReceipt);

            foreach (var itemDto in dto.Items)
            {
                var purchaseOrderItem = purchaseOrderItems[itemDto.PurchaseOrderItemId];
                var sourceLine = sourceSnapshot.Readiness.Lines.Single(line =>
                    line.PurchaseOrderItemId == itemDto.PurchaseOrderItemId);
                await _unitOfWork.Repository<PurchaseOrderReceiptItem>().AddAsync(
                    new PurchaseOrderReceiptItem
                    {
                        TenantId = _currentUser.TenantId,
                        ReceiptId = governedReceiptId,
                        PurchaseOrderItemId = purchaseOrderItem.Id,
                        ReceivedQuantity = itemDto.ReceivedQuantity,
                        AcceptedQuantity = 0,
                        RejectedQuantity = 0,
                        OrderedQuantitySnapshot = purchaseOrderItem.OrderedQuantity,
                        PreviouslyReceiptedQuantitySnapshot = sourceLine.PreviouslyReceiptedQuantity,
                        ToleranceQuantitySnapshot = sourceLine.ToleranceQuantity,
                        MaximumReceivableQuantitySnapshot = sourceLine.MaximumReceivableQuantity,
                        RemainingQuantityBeforeReceiptSnapshot = sourceLine.RemainingQuantity,
                        ReceiptLineIntegrityHash = sourceLine.IntegrityHash,
                        UnitOfMeasure = purchaseOrderItem.UnitOfMeasure,
                        ItemUnitOfMeasureId = purchaseOrderItem.ItemUnitOfMeasureId,
                        LocationId = itemDto.StorageLocationId ?? dto.ReceivingLocationId,
                        LotNumber = itemDto.LotNumber,
                        BatchNumber = itemDto.BatchNumber,
                        SerialNumber = itemDto.SerialNumber,
                        ManufactureDate = itemDto.ManufactureDate,
                        ExpirationDate = itemDto.ExpiryDate,
                        QualityStatus = "Pending",
                        Notes = itemDto.Notes,
                        CreatedAt = DateTime.UtcNow,
                        CreatedById = _currentUser.UserId
                    });
            }
            await _unitOfWork.SaveChangesAsync();

            var grn = new GoodsReceiptNote
            {
                Id = grnId,
                TenantId = _currentUser.TenantId,
                // Replaced atomically with the configured DEC-013 GRN/MRN
                // number before this transaction commits.
                GRNNumber = $"PENDING-{grnId:N}",
                PurchaseOrderReceiptId = governedReceiptId,
                ReceiptDate = DateTime.UtcNow,
                WarehouseId = dto.WarehouseId,
                SupplierId = purchaseOrder.BusinessPartnerId,
                SupplierName =
                    purchaseOrder.BusinessPartner?.PartnerName,
                PurchaseOrderId = purchaseOrder.Id,
                PurchaseOrderNumber = purchaseOrder.OrderNumber,
                ReceivingLocationId = dto.ReceivingLocationId,
                DeliveryNoteNumber = dto.DeliveryNoteNumber,
                VehicleNumber = dto.VehicleNumber,
                DriverName = dto.DriverName,
                RequiresInspection = true,
                Status = GRNStatus.PendingInspection,
                Notes = dto.Notes,
                ReceivedById = _currentUser.UserId,
                IdempotencyKey = idempotencyKey,
                IdempotencyRequestHash = requestHash,
                CorrelationId = correlationId,
                ReceiptTolerancePercent =
                    sourceSnapshot.TolerancePercent,
                ReceiptSourceSnapshotJson =
                    sourceSnapshot.SnapshotJson,
                ReceiptSourceIntegrityHash =
                    sourceSnapshot.IntegrityHash,
                ReceiptSourceValidatedAtUtc =
                    sourceSnapshot.ValidatedAtUtc
            };
            await _grnRepository.AddAsync(grn);
            var projectedTotalReceived = 0m;

            foreach (var itemDto in dto.Items)
            {
                if (!purchaseOrderItems.TryGetValue(
                        itemDto.PurchaseOrderItemId,
                        out var purchaseOrderItem) ||
                    purchaseOrderItem.InventoryItemId !=
                    itemDto.InventoryItemId)
                {
                    throw new ProcurementReceiptSourceValidationException(
                        "RCV_INVENTORY_ITEM_MISMATCH",
                        "The goods receipt item does not match its governed purchase-order line.");
                }

                var item = await _unitOfWork.Repository<InventoryItem>()
                    .GetQueryable(value =>
                        value.TenantId == _currentUser.TenantId &&
                        value.Id == itemDto.InventoryItemId &&
                        !value.IsDeleted)
                    .SingleOrDefaultAsync()
                    ?? throw new ProcurementReceiptSourceValidationException(
                        "RCV_INVENTORY_ITEM_NOT_FOUND",
                        "The inventory item was not found in the current tenant.");
                if (itemDto.StorageLocationId.HasValue &&
                    itemDto.StorageLocationId.Value != Guid.Empty)
                {
                    var storageIsValid = await _unitOfWork
                        .Repository<WarehouseLocation>()
                        .GetQueryable(value =>
                            value.TenantId == _currentUser.TenantId &&
                            value.Id ==
                            itemDto.StorageLocationId.Value &&
                            value.InventoryWarehouseId ==
                            dto.WarehouseId &&
                            !value.IsDeleted)
                        .AnyAsync();
                    if (!storageIsValid)
                    {
                        throw new ProcurementReceiptSourceValidationException(
                            "RCV_STORAGE_LOCATION_INVALID",
                            "A receipt-line storage location does not belong to the selected warehouse.");
                    }
                }

                var sourceLine = sourceSnapshot.Readiness.Lines
                    .Single(line =>
                        line.PurchaseOrderItemId ==
                        itemDto.PurchaseOrderItemId);
                var unitCost = itemDto.UnitCost > 0
                    ? itemDto.UnitCost
                    : purchaseOrderItem.LandedUnitCost > 0
                        ? purchaseOrderItem.LandedUnitCost
                        : purchaseOrderItem.UnitPrice > 0
                            ? purchaseOrderItem.UnitPrice
                            : item.StandardCost;
                var conversion = 1m;
                if (purchaseOrderItem.ItemUnitOfMeasureId.HasValue)
                {
                    conversion = await _unitOfWork.Repository<ItemUnitOfMeasure>()
                        .GetQueryable(value =>
                            value.TenantId == _currentUser.TenantId &&
                            value.Id == purchaseOrderItem.ItemUnitOfMeasureId.Value &&
                            !value.IsDeleted)
                        .AsNoTracking()
                        .Select(value => value.ConversionToBase)
                        .SingleOrDefaultAsync();
                    if (conversion <= 0) conversion = 1m;
                }
                var grnItem = new GoodsReceiptNoteItem
                {
                    TenantId = _currentUser.TenantId,
                    GoodsReceiptNoteId = grn.Id,
                    PurchaseOrderItemId =
                        purchaseOrderItem.Id,
                    InventoryItemId = itemDto.InventoryItemId,
                    ItemCode = item.ItemCode,
                    ItemName = item.Name,
                    OrderedQuantity =
                        purchaseOrderItem.OrderedQuantity * conversion,
                    ReceivedQuantity = itemDto.ReceivedQuantity * conversion,
                    PreviouslyReceiptedQuantitySnapshot =
                        sourceLine.PreviouslyReceiptedQuantity * conversion,
                    ToleranceQuantitySnapshot =
                        sourceLine.ToleranceQuantity * conversion,
                    MaximumReceivableQuantitySnapshot =
                        sourceLine.MaximumReceivableQuantity * conversion,
                    RemainingQuantityBeforeReceiptSnapshot =
                        sourceLine.RemainingQuantity * conversion,
                    ReceiptLineIntegrityHash =
                        sourceLine.IntegrityHash,
                    AcceptedQuantity = 0,
                    RejectedQuantity = 0,
                    UnitOfMeasure = item.UnitOfMeasure,
                    UnitCost = unitCost / conversion,
                    LineValue =
                        itemDto.ReceivedQuantity * unitCost,
                    LotNumber = itemDto.LotNumber,
                    BatchNumber = itemDto.BatchNumber,
                    SerialNumber = itemDto.SerialNumber,
                    ManufactureDate = itemDto.ManufactureDate,
                    ExpiryDate = itemDto.ExpiryDate,
                    InventoryTrackingExceptionId = itemDto.InventoryTrackingExceptionId,
                    StorageLocationId =
                        itemDto.StorageLocationId ?? dto.ReceivingLocationId,
                    InspectionResult = InspectionResult.Pending,
                    Notes = itemDto.Notes
                };
                await _grnItemRepository.AddAsync(grnItem);
                projectedTotalReceived += grnItem.ReceivedQuantity;
            }

            grn.TotalItems = dto.Items.Count;
            grn.TotalQuantityReceived = projectedTotalReceived;
            await _grnRepository.UpdateAsync(grn);
            await _unitOfWork.SaveChangesAsync();
            await _receiptInspection.InitializeAsync(
                governedReceiptId,
                correlationId);
            var documentOverview = await _receiptDocuments.EnsureAsync(
                governedReceiptId,
                correlationId);
            var primaryDocument = documentOverview.Documents
                .FirstOrDefault(item => item.DocumentKind == ProcurementReceiptDocumentKind.Grn)
                ?? documentOverview.Documents.FirstOrDefault()
                ?? throw new ProcurementReceiptDocumentValidationException(
                    "RCV_DOCUMENT_REGISTER_MISSING",
                    "DEC-013 did not produce a receipt document for the governed GRN.");
            grn.GRNNumber = primaryDocument.DocumentNumber;
            await _grnRepository.UpdateAsync(grn);
            await _unitOfWork.SaveChangesAsync();
            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            _logger.LogInformation(
                "Created governed GRN {GRNNumber} for warehouse {Warehouse}",
                grn.GRNNumber,
                warehouse.Name);
            return MapToDto(grn);
        }
        catch (ProcurementReceiptSourceValidationException exception)
        {
            if (ownsTransaction && _unitOfWork.HasActiveTransaction)
                await _unitOfWork.RollbackAsync();

            await RecordCreateDenialAsync(
                dto,
                correlationId,
                exception.Code,
                exception.Message,
                exception.Readiness);
            throw;
        }
        catch (ProcurementReceiptSourceNotFoundException exception)
        {
            if (ownsTransaction && _unitOfWork.HasActiveTransaction)
                await _unitOfWork.RollbackAsync();

            await RecordCreateDenialAsync(
                dto,
                correlationId,
                exception.Code,
                exception.Message);
            throw;
        }
        catch (DbUpdateException exception)
        {
            if (ownsTransaction && _unitOfWork.HasActiveTransaction)
                await _unitOfWork.RollbackAsync();

            const string code = "RCV_DATABASE_HARD_STOP";
            var message = exception.InnerException?.Message ??
                          exception.Message;
            await RecordCreateDenialAsync(
                dto,
                correlationId,
                code,
                message);
            throw new ProcurementReceiptSourceValidationException(
                code,
                "The database rejected the receipt because its governed source, line capacity, or idempotency constraint was no longer valid.");
        }
        finally
        {
            if (ownsTransaction && _unitOfWork.HasActiveTransaction)
                await _unitOfWork.RollbackAsync();
        }
    }

    internal static string BuildGovernedReceiptIdempotencyKey(string sourceKey)
    {
        var normalized = sourceKey.Trim();
        var hash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))
            .ToLowerInvariant();
        return $"grn:{hash}";
    }

    internal static string BuildIdempotencyRequestHash(
        CreateGoodsReceiptNoteDto request,
        Guid effectiveSupplierId)
    {
        var canonical = new
        {
            PurchaseOrderId = request.PurchaseOrderId.GetValueOrDefault(),
            request.WarehouseId,
            SupplierId = request.SupplierId is { } supplierId && supplierId != Guid.Empty
                ? supplierId
                : effectiveSupplierId,
            ReceivingLocationId = request.ReceivingLocationId == Guid.Empty
                ? null
                : request.ReceivingLocationId,
            DeliveryNoteNumber = NormalizeReplayText(request.DeliveryNoteNumber),
            VehicleNumber = NormalizeReplayText(request.VehicleNumber),
            DriverName = NormalizeReplayText(request.DriverName),
            request.RequiresInspection,
            Notes = NormalizeReplayText(request.Notes),
            Items = request.Items
                .Select(item => new
                {
                    item.PurchaseOrderItemId,
                    item.InventoryItemId,
                    item.ReceivedQuantity,
                    item.UnitCost,
                    LotNumber = NormalizeReplayText(item.LotNumber),
                    SerialNumber = NormalizeReplayText(item.SerialNumber),
                    ExpiryDate = NormalizeReplayDate(item.ExpiryDate),
                    StorageLocationId = item.StorageLocationId == Guid.Empty
                        ? null
                        : item.StorageLocationId,
                    Notes = NormalizeReplayText(item.Notes)
                })
                .OrderBy(item => item.PurchaseOrderItemId)
                .ThenBy(item => item.InventoryItemId)
                .ThenBy(item => item.StorageLocationId)
                .ThenBy(item => item.LotNumber, StringComparer.Ordinal)
                .ThenBy(item => item.SerialNumber, StringComparer.Ordinal)
                .ThenBy(item => item.ExpiryDate)
                .ThenBy(item => item.ReceivedQuantity)
                .ThenBy(item => item.UnitCost)
                .ThenBy(item => item.Notes, StringComparer.Ordinal)
                .ToArray()
        };
        var json = JsonSerializer.Serialize(canonical, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(json)))
            .ToLowerInvariant();
    }

    private static string? NormalizeReplayText(string? value) => value;

    private static DateTime? NormalizeReplayDate(DateTime? value) =>
        value?.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc),
            _ => value?.ToUniversalTime()
        };

    public async Task<bool> SubmitForInspectionAsync(Guid grnId, Guid userId)
    {
        var grn = await LoadGrnAsync(grnId);
        await EnsureCapabilityAsync(
            "procurement.inventory.receive",
            grnId.ToString(),
            grn.WarehouseId,
            grn.ReceivingLocationId,
            requireLocationScope: true);
        EnsureLegacyInspectionIsNotUsed(grn);

        if (grn.Status != GRNStatus.Draft)
            throw new InvalidOperationException($"GRN must be in Draft status to submit for inspection");

        grn.Status = GRNStatus.PendingInspection;
        grn.RequiresInspection = true;
        await _grnRepository.UpdateAsync(grn);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("GRN {GRNNumber} submitted for inspection", grn.GRNNumber);
        return true;
    }

    public async Task<bool> UpdateInspectionResultAsync(
        Guid grnId,
        UpdateGRNInspectionDto dto,
        Guid userId)
    {
        var grn = await LoadGrnAsync(grnId);
        await EnsureCapabilityAsync(
            "procurement.inventory.receive",
            grnId.ToString(),
            grn.WarehouseId,
            grn.ReceivingLocationId,
            requireLocationScope: true);
        EnsureLegacyInspectionIsNotUsed(grn);
        var grnItem = await _unitOfWork
            .Repository<GoodsReceiptNoteItem>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.GoodsReceiptNoteId == grnId &&
                item.Id == dto.GRNItemId &&
                !item.IsDeleted)
            .SingleOrDefaultAsync()
            ?? throw new ProcurementReceiptSourceNotFoundException(
                "RCV_GRN_LINE_NOT_FOUND",
                "The goods receipt line was not found in the current tenant.");

        if (dto.AcceptedQuantity < 0 ||
            dto.RejectedQuantity < 0 ||
            dto.AcceptedQuantity + dto.RejectedQuantity >
            grnItem.ReceivedQuantity)
        {
            throw new ProcurementReceiptSourceValidationException(
                "RCV_INSPECTION_QUANTITY_INVALID",
                "Accepted and rejected quantities must be non-negative and cannot exceed the governed received quantity.");
        }

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
        var grn = await GrnQuery(includeItems: true)
            .SingleOrDefaultAsync(item => item.Id == grnId)
            ?? throw new ProcurementReceiptSourceNotFoundException(
                "RCV_GRN_NOT_FOUND",
                "The goods receipt note was not found in the current tenant.");
        await EnsureCapabilityAsync(
            "procurement.inventory.receive",
            grnId.ToString(),
            grn.WarehouseId,
            grn.ReceivingLocationId,
            requireLocationScope: true);
        EnsureLegacyInspectionIsNotUsed(grn);

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

    public Task<bool> PostToInventoryAsync(Guid grnId, Guid userId) =>
        _unitOfWork.HasActiveTransaction
            ? PostToInventoryCoreAsync(grnId, userId)
            : _unitOfWork.ExecuteInStrategyAsync(
                () => PostToInventoryCoreAsync(grnId, userId));

    public async Task ApplyScanMetadataAsync(
        Guid grnId,
        IReadOnlyList<InventoryTransactionScanLineDto> lines,
        Guid userId)
    {
        EnsureTenant();
        var grn = await GrnQuery(includeItems: true).SingleOrDefaultAsync(item => item.Id == grnId)
            ?? throw new ProcurementReceiptSourceNotFoundException("RCV_GRN_NOT_FOUND", "The goods receipt note was not found in the current tenant.");
        var requestedLocations = lines.Select(line => line.LocationId ?? grn.ReceivingLocationId).Distinct().ToList();
        if (requestedLocations.Count == 0)
            requestedLocations.Add(grn.ReceivingLocationId);
        foreach (var locationId in requestedLocations)
        {
            await EnsureCapabilityAsync(
                "procurement.inventory.receive",
                grnId.ToString(),
                grn.WarehouseId,
                locationId,
                requireLocationScope: true);
        }
        if (grn.Status == GRNStatus.Cancelled || grn.StockUpdated)
            throw new InvalidOperationException("Scanned receipt metadata can only be applied before inventory posting.");

        foreach (var scan in lines)
        {
            var item = grn.Items.SingleOrDefault(value => value.Id == scan.DocumentLineId && value.InventoryItemId == scan.InventoryItemId)
                ?? throw new ArgumentException($"GRN line {scan.DocumentLineId} was not found for the scanned item.");
            var expected = item.AcceptedQuantity > 0 ? item.AcceptedQuantity : item.ReceivedQuantity;
            if (scan.BaseQuantity != expected)
                throw new InvalidOperationException($"The complete accepted quantity for {item.ItemCode} must be scanned before receipt posting.");
            if (scan.LocationId.HasValue)
            {
                var location = await _unitOfWork.Repository<WarehouseLocation>().GetQueryable(value =>
                        value.TenantId == _currentUser.TenantId && value.Id == scan.LocationId.Value && !value.IsDeleted && value.IsActive)
                    .AsNoTracking().SingleOrDefaultAsync()
                    ?? throw new ArgumentException("The scanned receipt location was not found in the current tenant.");
                var effectiveWarehouseId = location.IsConsignmentBin && location.ConsignmentWarehouseId.HasValue
                    ? location.ConsignmentWarehouseId.Value
                    : location.WarehouseId;
                if (effectiveWarehouseId != grn.WarehouseId)
                    throw new InvalidOperationException("The scanned receipt location does not belong to the GRN warehouse.");
            }
            item.StorageLocationId = scan.LocationId ?? item.StorageLocationId;
            item.LotNumber = string.IsNullOrWhiteSpace(scan.LotNumber) ? item.LotNumber : scan.LotNumber.Trim();
            item.BatchNumber = string.IsNullOrWhiteSpace(scan.BatchNumber) ? item.BatchNumber : scan.BatchNumber.Trim();
            item.SerialNumber = string.IsNullOrWhiteSpace(scan.SerialNumber) ? item.SerialNumber : scan.SerialNumber.Trim();
            item.ManufactureDate = scan.ManufactureDate ?? item.ManufactureDate;
            item.ExpiryDate = scan.ExpiryDate ?? item.ExpiryDate;
            item.InventoryTrackingExceptionId = scan.InventoryTrackingExceptionId ?? item.InventoryTrackingExceptionId;
            item.UpdatedAt = DateTime.UtcNow;
            item.LastModifiedById = userId;
            await _grnItemRepository.UpdateAsync(item);
        }
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task<bool> PostToInventoryCoreAsync(
        Guid grnId,
        Guid userId)
    {
        EnsureTenant();
        var correlationId = NormalizeCorrelation(null);
        var ownsTransaction = false;
        try
        {
            if (!_unitOfWork.HasActiveTransaction)
            {
                await _unitOfWork.BeginTransactionAsync(
                    IsolationLevel.Serializable);
                ownsTransaction = true;
            }

            var grn = await GrnQuery(includeItems: true)
                .SingleOrDefaultAsync(item => item.Id == grnId)
                ?? throw new ProcurementReceiptSourceNotFoundException(
                    "RCV_GRN_NOT_FOUND",
                    "The goods receipt note was not found in the current tenant.");
            if (!grn.PurchaseOrderId.HasValue ||
                grn.PurchaseOrderId.Value == Guid.Empty)
            {
                throw new ProcurementReceiptSourceValidationException(
                    "RCV_GRN_PURCHASE_ORDER_REQUIRED",
                    "A governed purchase order is required before GRN stock posting.");
            }
            var purchaseOrder = await _unitOfWork.Repository<PurchaseOrder>()
                .GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.Id == grn.PurchaseOrderId.Value &&
                    !item.IsDeleted)
                .SingleOrDefaultAsync()
                ?? throw new ProcurementReceiptSourceNotFoundException(
                    "RCV_PURCHASE_ORDER_NOT_FOUND",
                    "The governed GRN purchase order was not found in the current tenant.");
            await _purchaseOrderSod.EnforceReceiptActionAsync(
                purchaseOrder,
                ProcurementPurchaseOrderSodRules.PostGoodsReceiptNoteToInventory,
                correlationId);
            if (grn.PurchaseOrderReceiptId.HasValue &&
                grn.Status == GRNStatus.StockUpdated &&
                grn.StockUpdated)
                return true;

            EnsureLegacyInspectionIsNotUsed(grn);
            if (grn.Status == GRNStatus.StockUpdated)
                throw new InvalidOperationException("GRN already posted to inventory");
            if (grn.RequiresInspection &&
                grn.Status != GRNStatus.Accepted &&
                grn.Status != GRNStatus.PartiallyAccepted)
            {
                throw new InvalidOperationException(
                    "GRN inspection must be completed before posting");
            }

            var sourceSnapshot =
                await _receiptSourceControl.RevalidateGoodsReceiptNoteAsync(
                    grn.Id,
                    "PostGoodsReceiptNoteToInventory",
                    correlationId);
            grn.ReceiptTolerancePercent =
                sourceSnapshot.TolerancePercent;
            grn.ReceiptSourceSnapshotJson =
                sourceSnapshot.SnapshotJson;
            grn.ReceiptSourceIntegrityHash =
                sourceSnapshot.IntegrityHash;
            grn.ReceiptSourceValidatedAtUtc =
                sourceSnapshot.ValidatedAtUtc;

            foreach (var item in grn.Items.Where(value =>
                         value.AcceptedQuantity > 0))
            {
                await _trackingControls.StageEventAsync(new InventoryTrackingMutationRequest
                {
                    InventoryItemId = item.InventoryItemId,
                    WarehouseId = grn.WarehouseId,
                    LocationId = item.StorageLocationId,
                    Direction = InventoryTrackingDirection.Receipt,
                    Quantity = item.AcceptedQuantity,
                    ReferenceType = "GoodsReceiptNote",
                    ReferenceNumber = grn.GRNNumber,
                    ReferenceId = grn.Id,
                    ReferenceLineId = item.Id,
                    EventKey = $"grn:{grn.Id:N}:{item.Id:N}:receipt",
                    LotNumber = item.LotNumber,
                    BatchNumber = item.BatchNumber,
                    SerialNumber = item.SerialNumber,
                    ManufactureDate = item.ManufactureDate,
                    ExpiryDate = item.ExpiryDate,
                    TrackingExceptionId = item.InventoryTrackingExceptionId,
                    CorrelationId = correlationId
                });
                var warehouseQty = await _unitOfWork
                    .Repository<WarehouseQuantity>()
                    .GetQueryable(value =>
                        value.TenantId == _currentUser.TenantId &&
                        value.WarehouseId == grn.WarehouseId &&
                        value.InventoryItemId ==
                        item.InventoryItemId &&
                        !value.IsDeleted)
                    .SingleOrDefaultAsync();
                if (warehouseQty == null)
                {
                    warehouseQty = new WarehouseQuantity
                    {
                        TenantId = _currentUser.TenantId,
                        WarehouseId = grn.WarehouseId,
                        InventoryItemId = item.InventoryItemId,
                        CurrentStock = 0,
                        AvailableStock = 0
                    };
                    await _warehouseQuantityRepository.AddAsync(
                        warehouseQty);
                }

                warehouseQty.CurrentStock +=
                    item.AcceptedQuantity;
                warehouseQty.AvailableStock +=
                    item.AcceptedQuantity;
                warehouseQty.LastMovementDate = DateTime.UtcNow;
                await _warehouseQuantityRepository.UpdateAsync(
                    warehouseQty);

                var invItem = await _unitOfWork
                    .Repository<InventoryItem>()
                    .GetQueryable(value =>
                        value.TenantId == _currentUser.TenantId &&
                        value.Id == item.InventoryItemId &&
                        !value.IsDeleted)
                    .SingleOrDefaultAsync()
                    ?? throw new ProcurementReceiptSourceValidationException(
                        "RCV_INVENTORY_ITEM_NOT_FOUND",
                        "A governed GRN inventory item is not available in the current tenant.");
                invItem.CurrentStock += item.AcceptedQuantity;
                invItem.AvailableStock += item.AcceptedQuantity;
                invItem.LastPurchaseDate = DateTime.UtcNow;
                invItem.LastPurchaseCost = item.UnitCost;
                await _itemRepository.UpdateAsync(invItem);

                var costLayer = new InventoryCostLayer
                {
                    TenantId = _currentUser.TenantId,
                    InventoryItemId = item.InventoryItemId,
                    WarehouseId = grn.WarehouseId,
                    LayerNumber =
                        $"CL-{grn.GRNNumber}-{item.Id.ToString()[..8]}",
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

                var movement = new StockMovement
                {
                    TenantId = _currentUser.TenantId,
                    InventoryItemId = item.InventoryItemId,
                    MovementType = "Receipt",
                    Quantity = item.AcceptedQuantity,
                    UnitCost = item.UnitCost,
                    TotalValue =
                        item.AcceptedQuantity * item.UnitCost,
                    ReferenceType = ReferenceType.PO,
                    ReferenceNumber = grn.GRNNumber,
                    ReferenceId = grn.Id,
                    WarehouseId = grn.WarehouseId,
                    LocationId = item.StorageLocationId,
                    LotNumber = item.LotNumber,
                    BatchNumber = item.BatchNumber,
                    SerialNumber = item.SerialNumber,
                    ManufactureDate = item.ManufactureDate,
                    ExpirationDate = item.ExpiryDate,
                    InventoryTrackingExceptionId = item.InventoryTrackingExceptionId,
                    Notes =
                        $"Received from GRN {grn.GRNNumber}",
                    ProcessedById = _currentUser.UserId,
                    RunningBalance = invItem.CurrentStock
                };
                await _stockMovementRepository.AddAsync(movement);
                await _grnItemRepository.UpdateAsync(item);
            }

            grn.Status = GRNStatus.StockUpdated;
            grn.StockUpdated = true;
            grn.StockUpdatedAt = DateTime.UtcNow;
            grn.UpdatedAt = DateTime.UtcNow;
            grn.UpdatedBy = _currentUser.FullName;
            grn.LastModifiedById = _currentUser.UserId;
            grn.TotalValue = grn.Items.Sum(item =>
                item.AcceptedQuantity * item.UnitCost);
            await _grnRepository.UpdateAsync(grn);
            await _unitOfWork.SaveChangesAsync();
            if (ownsTransaction)
                await _unitOfWork.CommitAsync();

            _logger.LogInformation(
                "Governed GRN {GRNNumber} posted to inventory",
                grn.GRNNumber);
            return true;
        }
        finally
        {
            if (ownsTransaction && _unitOfWork.HasActiveTransaction)
                await _unitOfWork.RollbackAsync();
        }
    }

    public async Task<bool> CancelAsync(Guid grnId, string reason, Guid userId)
    {
        var grn = await LoadGrnAsync(grnId);
        await EnsureCapabilityAsync(
            "procurement.inventory.receive",
            grnId.ToString(),
            grn.WarehouseId,
            grn.ReceivingLocationId,
            requireLocationScope: true);

        if (grn.Status == GRNStatus.StockUpdated)
            throw new InvalidOperationException("Cannot cancel a posted GRN");
        if (grn.PurchaseOrderReceiptId.HasValue)
            throw new ProcurementReceiptSourceValidationException(
                "RCV_INSPECTION_LIFECYCLE_REQUIRED",
                "This GRN is a projection of a governed procurement receipt and cannot be cancelled independently; use the linked receipt-inspection lifecycle.");

        grn.Status = GRNStatus.Cancelled;
        grn.Notes = $"{grn.Notes}\nCancelled: {reason}";
        await _grnRepository.UpdateAsync(grn);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("GRN {GRNNumber} cancelled: {Reason}", grn.GRNNumber, reason);
        return true;
    }

    public async Task<IEnumerable<GoodsReceiptNoteDto>> GetPendingInspectionAsync()
    {
        var grns = await GrnQuery(includeItems: true)
            .Where(item =>
                item.Status == GRNStatus.PendingInspection ||
                item.Status == GRNStatus.InspectionInProgress)
            .OrderBy(item => item.ReceiptDate)
            .ToListAsync();
        return (await FilterReadableAsync(grns)).Select(MapToDto);
    }

    #region Private Methods

    private IQueryable<GoodsReceiptNote> GrnQuery(
        bool includeItems = false)
    {
        EnsureTenant();
        IQueryable<GoodsReceiptNote> query = _unitOfWork
            .Repository<GoodsReceiptNote>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                !item.IsDeleted)
            .Include(item => item.Warehouse)
            .Include(item => item.ReceivingLocation)
            .Include(item => item.ReceivedBy)
            .Include(item => item.InspectedBy);
        if (includeItems)
        {
            query = query
                .Include(item => item.Items)
                    .ThenInclude(item => item.InventoryItem)
                .Include(item => item.Items)
                    .ThenInclude(item => item.StorageLocation);
        }

        return query;
    }

    private async Task<GoodsReceiptNote> LoadGrnAsync(Guid grnId) =>
        await _unitOfWork.Repository<GoodsReceiptNote>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.Id == grnId &&
                !item.IsDeleted)
            .SingleOrDefaultAsync()
        ?? throw new ProcurementReceiptSourceNotFoundException(
            "RCV_GRN_NOT_FOUND",
            "The goods receipt note was not found in the current tenant.");

    private static void EnsureLegacyInspectionIsNotUsed(GoodsReceiptNote grn)
    {
        throw new ProcurementReceiptSourceValidationException(
            "RCV_INSPECTION_LIFECYCLE_REQUIRED",
            grn.PurchaseOrderReceiptId.HasValue
                ? "This GRN is governed by the shared procurement receipt-inspection lifecycle; use the linked receipt inspection control."
                : "Historical GRNs are read-only and cannot bypass the shared procurement receipt-inspection lifecycle.");
    }

    private async Task EnsureCapabilityAsync(
        string permission,
        string sourceReference,
        Guid? warehouseId = null,
        Guid? locationId = null,
        bool requireLocationScope = false)
    {
        EnsureTenant();
        try
        {
            var decision = await _accessControl.EnforceCapabilityAsync(
                new ProcurementAccessCapabilityRequest
                {
                    PermissionCode = permission,
                    SourceType = "ProcurementReceiptSourceControl",
                    SourceReference = sourceReference,
                    WarehouseId = warehouseId,
                    LocationId = locationId,
                    RequireLocationScope = requireLocationScope
                },
                NormalizeCorrelation(null));
            if (!decision.Allowed)
            {
                throw new ProcurementReceiptSourceAuthorizationException(
                    decision.Message);
            }
        }
        catch (ProcurementAccessAuthorizationException exception)
        {
            throw new ProcurementReceiptSourceAuthorizationException(
                exception.Message);
        }
        catch (ProcurementAccessValidationException exception)
        {
            throw new ProcurementReceiptSourceAuthorizationException(
                exception.Message);
        }
    }

    private async Task<IReadOnlyList<GoodsReceiptNote>> FilterReadableAsync(
        IEnumerable<GoodsReceiptNote> grns)
    {
        var readable = new List<GoodsReceiptNote>();
        foreach (var grn in grns)
        {
            if (await CanReadAsync(grn))
                readable.Add(grn);
        }

        return readable;
    }

    private async Task<bool> CanReadAsync(GoodsReceiptNote grn)
    {
        try
        {
            var locationIds = grn.Items
                .Where(item => !item.IsDeleted)
                .Select(item => item.StorageLocationId)
                .Append(grn.ReceivingLocationId)
                .Distinct()
                .ToList();
            if (locationIds.Count == 0)
                locationIds.Add(null);

            foreach (var locationId in locationIds)
            {
                var decision = await _accessControl.CheckCapabilityAsync(
                    new ProcurementAccessCapabilityRequest
                    {
                        PermissionCode = "procurement.inventory.read",
                        SourceType = "GoodsReceiptNote",
                        SourceReference = grn.GRNNumber,
                        WarehouseId = grn.WarehouseId,
                        LocationId = locationId,
                        RequireLocationScope = true
                    },
                    NormalizeCorrelation(null));
                if (!decision.Allowed)
                    return false;
            }

            return true;
        }
        catch (ProcurementAccessAuthorizationException)
        {
            return false;
        }
        catch (ProcurementAccessValidationException)
        {
            return false;
        }
    }

    private void EnsureTenant()
    {
        if (!_currentUser.IsAuthenticated ||
            _currentUser.TenantId == Guid.Empty ||
            _currentUser.UserId == Guid.Empty)
        {
            throw new ProcurementReceiptSourceAuthorizationException(
                "An authenticated tenant context is required.");
        }

        if (_currentUser.IsExternalUser)
        {
            throw new ProcurementReceiptSourceAuthorizationException(
                "Supplier portal users cannot access internal goods receipt controls.");
        }
    }

    private static string NormalizeCorrelation(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? Guid.NewGuid().ToString("N")
            : value.Trim().Length <= 100
                ? value.Trim()
                : value.Trim()[..100];

    private async Task RecordCreateDenialAsync(
        CreateGoodsReceiptNoteDto dto,
        string correlationId,
        string code,
        string message,
        object? readiness = null)
    {
        try
        {
            await _receiptSourceControl.RecordDeniedAsync(
                dto.PurchaseOrderId,
                dto.PurchaseOrderId?.ToString() ?? "missing",
                "CreateGoodsReceiptNote",
                code,
                message,
                correlationId,
                new
                {
                    dto.WarehouseId,
                    dto.SupplierId,
                    Lines = dto.Items.Select(item => new
                    {
                        item.PurchaseOrderItemId,
                        item.InventoryItemId,
                        item.ReceivedQuantity
                    }),
                    Readiness = readiness
                });
        }
        catch (Exception auditException)
        {
            _logger.LogError(
                auditException,
                "Failed to append denied governed-GRN audit event for {PurchaseOrderId}",
                dto.PurchaseOrderId);
        }
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
            ReceiptTolerancePercent =
                grn.ReceiptTolerancePercent,
            ReceiptSourceIntegrityHash =
                grn.ReceiptSourceIntegrityHash,
            ReceiptSourceValidatedAtUtc =
                grn.ReceiptSourceValidatedAtUtc,
            RowVersion = Convert.ToBase64String(grn.RowVersion),
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
            ReceiptTolerancePercent =
                grn.ReceiptTolerancePercent,
            ReceiptSourceIntegrityHash =
                grn.ReceiptSourceIntegrityHash,
            ReceiptSourceValidatedAtUtc =
                grn.ReceiptSourceValidatedAtUtc,
            RowVersion = Convert.ToBase64String(grn.RowVersion),
            CreatedAtFormatted = grn.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
            ReceivingLocationId = grn.ReceivingLocationId,
            ReceivingLocationName = grn.ReceivingLocation?.LocationCode,
            InspectedByName = grn.InspectedBy?.FullName,
            InspectionNotes = grn.InspectionNotes,
            Items = grn.Items.Select(i => new GoodsReceiptNoteItemDto
            {
                Id = i.Id,
                InventoryItemId = i.InventoryItemId,
                OrderedQuantity = i.OrderedQuantity,
                ItemCode = i.InventoryItem?.ItemCode ?? string.Empty,
                ItemName = i.InventoryItem?.Name ?? string.Empty,
                ReceivedQuantity = i.ReceivedQuantity,
                AcceptedQuantity = i.AcceptedQuantity,
                RejectedQuantity = i.RejectedQuantity,
                UnitOfMeasure = i.UnitOfMeasure ?? string.Empty,
                UnitCost = i.UnitCost,
                TotalCost = i.LineValue,
                LotNumber = i.LotNumber,
                BatchNumber = i.BatchNumber,
                SerialNumber = i.SerialNumber,
                ManufactureDate = i.ManufactureDate,
                ExpiryDate = i.ExpiryDate,
                InventoryTrackingExceptionId = i.InventoryTrackingExceptionId,
                InspectionResult = i.InspectionResult,
                InspectionNotes = i.InspectionNotes,
                StorageLocationId = i.StorageLocationId,
                StorageLocationName = i.StorageLocation?.LocationCode
            }).ToList()
        };
    }

    #endregion
}

