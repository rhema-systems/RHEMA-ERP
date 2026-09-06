using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;

namespace ErpSystem.Core.Interfaces.Inventory;

/// <summary>
/// Main inventory management service interface
/// Used by Maintenance, Production, Sales, and other modules
/// </summary>
public interface IInventoryManagementService
{
    // Item Management
    Task<IEnumerable<InventoryItemDto>> GetMaintenancePartsAsync(string? searchTerm = null);
    Task<InventoryItemDetailDto?> GetInventoryItemDetailAsync(Guid itemId);

    // Stock Allocation
    Task<InventoryAllocationDto> AllocateForWorkOrderAsync(AllocateInventoryDto request);
    Task<bool> ConsumeAllocatedInventoryAsync(Guid allocationId, decimal quantity, Guid userId);
    Task<bool> ReleaseAllocationAsync(Guid allocationId, Guid userId);

    // Stock Availability
    Task<StockAvailabilityDto> CheckStockAvailabilityAsync(Guid inventoryItemId, decimal requiredQuantity);
    Task<IEnumerable<ReorderRequiredDto>> GetItemsRequiringReorderAsync();
}

/// <summary>
/// The authoritative tenant-scoped resolver and uniqueness guard for primary,
/// alternate, QR, and item-unit identifiers.
/// </summary>
public interface IInventoryItemIdentifierService
{
    string? Normalize(string? identifier);
    Task ValidateItemIdentifiersAsync(
        Guid tenantId,
        Guid? inventoryItemId,
        string? barcode,
        string? alternateBarcode,
        string? qrCode,
        CancellationToken cancellationToken = default);
    Task ValidateUnitIdentifierAsync(
        Guid tenantId,
        Guid inventoryItemId,
        Guid? itemUnitOfMeasureId,
        string? barcode,
        CancellationToken cancellationToken = default);
    Task<InventoryIdentifierMatchDto?> ResolveAsync(
        Guid tenantId,
        string identifier,
        CancellationToken cancellationToken = default);
}

public sealed class InventoryIdentifierConflictException : InvalidOperationException
{
    public InventoryIdentifierConflictException(string identifier, string message) : base(message)
    {
        Identifier = identifier;
    }

    public string Identifier { get; }
}

/// <summary>
/// Warehouse management service interface
/// </summary>
public interface IWarehouseManagementService
{
    Task<IEnumerable<WarehouseDto>> GetWarehousesAsync();
    Task<WarehouseDto?> GetWarehouseByIdAsync(Guid warehouseId);
    Task<IEnumerable<WarehouseLocationDto>> GetWarehouseLocationsAsync(Guid warehouseId);
    Task<WarehouseLocationDto?> GetLocationByIdAsync(Guid locationId);
}

/// <summary>
/// Purchase order management service interface
/// </summary>
public interface IPurchaseOrderManagementService
{
    Task<IEnumerable<PurchaseOrderSummaryDto>> GetPurchaseOrdersAsync();
    Task<PurchaseOrderDetailDto?> GetPurchaseOrderByIdAsync(Guid purchaseOrderId);
    Task<PurchaseOrderDto> CreatePurchaseOrderAsync(CreatePurchaseOrderDto request);
    Task<bool> ApprovePurchaseOrderAsync(Guid purchaseOrderId, Guid approvedById);
    Task<PurchaseOrderReceiptDto> ReceivePurchaseOrderAsync(ReceivePurchaseOrderDto request);
}

/// <summary>
/// Stock movement tracking service interface
/// </summary>
public interface IStockMovementService
{
    Task<IEnumerable<StockMovementDto>> GetStockMovementsAsync(Guid? inventoryItemId = null, DateTime? fromDate = null, DateTime? toDate = null);
    Task<StockMovementDto> CreateStockMovementAsync(CreateStockMovementDto request);
    Task<IEnumerable<StockMovementDto>> GetMovementsByReferenceAsync(string referenceType, Guid referenceId);
}

/// <summary>
/// Unit of Measure management service interface
/// </summary>
public interface IUnitOfMeasureService
{
    Task<IEnumerable<UnitOfMeasureDto>> GetAllUnitsAsync();
    Task<IEnumerable<UnitOfMeasureDto>> GetActiveUnitsAsync();
    Task<UnitOfMeasureDto?> GetByIdAsync(Guid id);
    Task<UnitOfMeasureDto?> GetByCodeAsync(string code);
    Task<UnitOfMeasureDto> CreateAsync(CreateUnitOfMeasureDto dto);
    Task<UnitOfMeasureDto> UpdateAsync(Guid id, UpdateUnitOfMeasureDto dto);
    Task<bool> DeleteAsync(Guid id);

    // Conversions
    Task<IEnumerable<UnitOfMeasureConversionDto>> GetConversionsAsync(Guid unitId);
    Task<UnitOfMeasureConversionDto> CreateConversionAsync(CreateUnitOfMeasureConversionDto dto);
    Task<decimal> ConvertQuantityAsync(Guid fromUnitId, Guid toUnitId, decimal quantity);

    // Item-specific UOMs
    Task<IEnumerable<ItemUnitOfMeasureDto>> GetItemUnitsAsync(Guid inventoryItemId);
    Task<ItemUnitOfMeasureDto> AddItemUnitAsync(CreateItemUnitOfMeasureDto dto);
    Task<bool> RemoveItemUnitAsync(Guid itemUnitId);
}

/// <summary>
/// Goods Receipt Note (GRN) management service interface
/// </summary>
public interface IGoodsReceiptNoteService
{
    Task<IEnumerable<GoodsReceiptNoteDto>> GetAllAsync(DateTime? fromDate = null, DateTime? toDate = null);
    Task<IEnumerable<GoodsReceiptNoteDto>> GetByWarehouseAsync(Guid warehouseId);
    Task<IEnumerable<GoodsReceiptNoteDto>> GetBySupplierAsync(Guid supplierId);
    Task<IEnumerable<GoodsReceiptNoteDto>> GetByPurchaseOrderAsync(Guid purchaseOrderId);
    Task<GoodsReceiptNoteDetailDto?> GetByIdAsync(Guid id);
    Task<GoodsReceiptNoteDetailDto?> GetByGRNNumberAsync(string grnNumber);
    Task<GoodsReceiptNoteDto> CreateAsync(CreateGoodsReceiptNoteDto dto, Guid userId);
    Task<bool> SubmitForInspectionAsync(Guid grnId, Guid userId);
    Task<bool> UpdateInspectionResultAsync(Guid grnId, UpdateGRNInspectionDto dto, Guid userId);
    Task<bool> CompleteInspectionAsync(Guid grnId, Guid userId);
    Task<bool> PostToInventoryAsync(Guid grnId, Guid userId);
    Task ApplyScanMetadataAsync(Guid grnId, IReadOnlyList<InventoryTransactionScanLineDto> lines, Guid userId);
    Task<bool> CancelAsync(Guid grnId, string reason, Guid userId);
    Task<IEnumerable<GoodsReceiptNoteDto>> GetPendingInspectionAsync();
}

/// <summary>
/// Inventory Transfer management service interface
/// </summary>
public interface IInventoryTransferService
{
    Task<IEnumerable<InventoryTransferDto>> GetAllAsync(DateTime? fromDate = null, DateTime? toDate = null);
    Task<IEnumerable<InventoryTransferDto>> GetByWarehouseAsync(Guid warehouseId, bool isSource = true);
    Task<IEnumerable<InventoryTransferDto>> GetInTransitAsync();
    Task<IEnumerable<InventoryTransferDto>> GetPendingApprovalAsync();
    Task<InventoryTransferDetailDto?> GetByIdAsync(Guid id);
    Task<InventoryTransferDetailDto?> GetByTransferNumberAsync(string transferNumber);
    Task<InventoryTransferDto> CreateAsync(CreateInventoryTransferDto dto, Guid userId);
    Task<InventoryTransferDto> UpdateAsync(Guid transferId, UpdateInventoryTransferDto dto, Guid userId);
    Task<bool> SubmitForApprovalAsync(Guid transferId, Guid userId);
    Task<bool> ApproveAsync(Guid transferId, Guid userId, string? comments = null);
    Task<bool> RejectAsync(Guid transferId, string reason, Guid userId, string? comments = null);
    Task<bool> ShipAsync(Guid transferId, Guid userId, string? trackingNumber = null, Dictionary<Guid, decimal>? shippedItems = null, InventoryTransferMutationContext? control = null);
    
    /// <summary>
    /// Ships a transfer with shipping costs and cost allocation options.
    /// </summary>
    /// <param name="transferId">Transfer ID</param>
    /// <param name="userId">User performing the shipment</param>
    /// <param name="costsDto">Shipping costs and allocation options</param>
    /// <returns>True if successful</returns>
    Task<bool> ShipWithCostsAsync(Guid transferId, Guid userId, ShipTransferWithCostsDto costsDto);
    
    /// <summary>
    /// Saves shipping costs and details without shipping the transfer.
    /// Allows preparing shipment information before actually shipping.
    /// </summary>
    /// <param name="transferId">Transfer ID</param>
    /// <param name="userId">User saving the costs</param>
    /// <param name="costsDto">Shipping costs and allocation options</param>
    /// <returns>True if successful</returns>
    Task<bool> SaveShippingCostsAsync(Guid transferId, Guid userId, ShipTransferWithCostsDto costsDto);
    
    Task<bool> ReceiveAsync(Guid transferId, Guid userId, List<InventoryTransferItemDto>? receivedItems = null, InventoryTransferMutationContext? control = null);
    Task<bool> ResolveDiscrepanciesAsync(Guid transferId, Guid userId, ResolveInventoryTransferDiscrepancyRequest request);
    Task<bool> CloseAsync(Guid transferId, Guid userId, CloseInventoryTransferRequest request);
    Task ApplyScanMetadataAsync(Guid transferId, InventoryScanOperation operation, IReadOnlyList<InventoryTransactionScanLineDto> lines, Guid userId);
    Task<bool> CancelAsync(Guid transferId, string reason, Guid userId, InventoryTransferMutationContext? control = null);
    
    /// <summary>
    /// Reverses a shipment that is in transit (not yet received).
    /// Reinstates quantities to the source warehouse and resets shipped quantities.
    /// </summary>
    Task<bool> ReverseShipmentAsync(Guid transferId, string reason, Guid userId, InventoryTransferMutationContext? control = null);

    // Item management methods
    Task<InventoryTransferItemDto> AddItemAsync(Guid transferId, AddTransferItemDto dto, Guid userId);
    Task<InventoryTransferItemDto> UpdateItemAsync(Guid transferId, Guid itemId, UpdateTransferItemDto dto, Guid userId);
    Task<bool> RemoveItemAsync(Guid transferId, Guid itemId, Guid userId);
}

/// <summary>
/// Physical Count management service interface
/// </summary>
public interface IPhysicalCountService
{
    // Query operations
    Task<IEnumerable<PhysicalCountDto>> GetAllAsync(DateTime? fromDate = null, DateTime? toDate = null);
    Task<IEnumerable<PhysicalCountDto>> GetFilteredAsync(PhysicalCountFilterDto filter);
    Task<IEnumerable<PhysicalCountDto>> GetByWarehouseAsync(Guid warehouseId);
    Task<IEnumerable<PhysicalCountDto>> GetInProgressAsync();
    Task<PhysicalCountDetailDto?> GetByIdAsync(Guid id);
    Task<PhysicalCountDetailDto?> GetByCountNumberAsync(string countNumber);
    Task<IEnumerable<PhysicalCountItemDto>> GetItemsWithVarianceAsync(Guid countId);

    // CRUD operations
    Task<PhysicalCountDto> CreateAsync(CreatePhysicalCountDto dto, Guid userId);
    Task<PhysicalCountDto> UpdateAsync(Guid countId, UpdatePhysicalCountDto dto, Guid userId);
    Task<bool> StartCountAsync(Guid countId, Guid userId);
    Task<bool> CancelAsync(Guid countId, string reason, Guid userId);

    // Count item operations
    Task<PhysicalCountItemDto> AddCountItemAsync(Guid countId, AddCountItemDto dto, Guid userId);
    Task<bool> RemoveCountItemAsync(Guid countItemId, Guid userId);
    Task<bool> RecordCountItemAsync(RecordCountItemDto dto, Guid userId);
    Task<bool> RecordCountItemsAsync(IEnumerable<RecordCountItemDto> items, Guid userId);

    // Workflow operations
    Task<bool> CompleteCountAsync(Guid countId, Guid userId);
    Task<bool> ApproveVariancesAsync(Guid countId, Guid userId);
    Task<bool> RejectVariancesAsync(Guid countId, string reason, Guid userId);
    Task<bool> PostAdjustmentsAsync(Guid countId, Guid userId);
    Task<bool> RecordRecountAsync(Guid countId, Guid userId, RecordPhysicalCountRecountRequest request);
    Task<bool> ApproveStoresAsync(Guid countId, Guid userId, PhysicalCountDecisionRequest request);
    Task<bool> ApproveFinanceAsync(Guid countId, Guid userId, PhysicalCountDecisionRequest request);
    Task<bool> AttestAuditAsync(Guid countId, Guid userId, PhysicalCountDecisionRequest request);
    Task<bool> PostControlledAdjustmentsAsync(Guid countId, Guid userId, PhysicalCountMutationRequest request);

    // ABC scheduling composes the existing procurement-calendar occurrence owner.
    Task<IReadOnlyList<InventoryCycleCountScheduleDto>> GetCycleCountSchedulesAsync();
    Task<InventoryCycleCountScheduleDto> SaveCycleCountScheduleAsync(Guid? scheduleId, SaveInventoryCycleCountScheduleRequest request, Guid userId);
    Task<CycleCountGenerationResultDto> GenerateDueCycleCountsAsync(Guid tenantId, DateTime nowUtc, Guid? requestedById = null);

    // Export/Import operations
    Task<PhysicalCountExportDto> ExportCountSheetAsync(Guid countId);
    Task<ImportCountResultDto> ImportCountSheetAsync(Guid countId, IEnumerable<ImportCountItemDto> items, Guid userId);
    Task<VarianceReportDto> GetVarianceReportAsync(Guid countId);
}

/// <summary>
/// Inventory Valuation service interface
/// </summary>
public interface IInventoryValuationService
{
    /// <summary>
    /// Clears request-scoped valuation state before an execution-strategy attempt.
    /// A retry must never reuse entities that were detached by a rolled-back attempt.
    /// </summary>
    void ResetProcessingAttempt();

    /// <summary>Restores (or reverses) the captured original issue value of a governed Store Return Voucher line.</summary>
    Task<decimal> ProcessReturnAsync(Guid returnVoucherLineId, bool reverse = false);

    Task<InventoryValuationSummaryDto> GetItemValuationAsync(Guid inventoryItemId);
    Task<IEnumerable<InventoryCostLayerDto>> GetCostLayersAsync(Guid inventoryItemId, Guid? warehouseId = null);
    Task<decimal> GetInventoryValueAsync(Guid? warehouseId = null, Guid? categoryId = null);
    Task<decimal> CalculateWeightedAverageCostAsync(Guid inventoryItemId);
    Task<decimal> GetFIFOCostAsync(Guid inventoryItemId, decimal quantity);
    Task<decimal> GetLIFOCostAsync(Guid inventoryItemId, decimal quantity);
    Task RecalculateCostLayersAsync(Guid inventoryItemId);
    
    /// <summary>
    /// Processes an inventory receipt based on the item's valuation method
    /// </summary>
    Task<decimal> ProcessReceiptAsync(
        Guid inventoryItemId,
        Guid warehouseId,
        Guid? locationId,
        decimal quantity,
        decimal unitCost,
        ErpSystem.Core.Enums.ReferenceType referenceType,
        string? referenceNumber,
        Guid? referenceId,
        string? lotNumber = null,
        string? serialNumber = null,
        DateTime? expirationDate = null,
        string authorizationAction = "AuthorizeInventoryPosting");

    /// <summary>
    /// Processes an inventory issue based on the item's valuation method
    /// </summary>
    Task<decimal> ProcessIssueAsync(
        Guid inventoryItemId,
        Guid warehouseId,
        Guid? locationId,
        decimal quantity,
        ErpSystem.Core.Entities.Inventory.InventoryMovementType movementType,
        ErpSystem.Core.Enums.ReferenceType referenceType,
        string? referenceNumber,
        Guid? referenceId,
        string? lotNumber = null,
        string? serialNumber = null);

    /// <summary>
    /// Posts an approved adjustment to the authoritative inventory subledger.
    /// Opening quantity supports lazy adoption of historical exact-bin stock.
    /// </summary>
    Task<decimal> ProcessAdjustmentAsync(
        Guid inventoryItemId,
        Guid warehouseId,
        Guid locationId,
        decimal quantityDelta,
        decimal unitCost,
        decimal openingQuantity,
        bool allowNegative,
        string? referenceNumber,
        Guid referenceId,
        string? lotNumber = null,
        string? serialNumber = null,
        DateTime? expirationDate = null,
        Guid? reversalSourceId = null);
}

/// <summary>
/// Landed Cost management service interface
/// </summary>
public interface ILandedCostService
{
    Task<IEnumerable<LandedCostDto>> GetAllAsync();
    Task<IEnumerable<LandedCostDto>> GetByGRNAsync(Guid grnId);
    Task<LandedCostDetailDto?> GetByIdAsync(Guid id);
    Task<LandedCostDto> CreateAsync(CreateLandedCostDto dto, Guid userId);
    Task<LandedCostDto> InitializeFromPurchaseOrderPlanAsync(Guid goodsReceiptNoteId, Guid userId);
    Task<bool> AllocateCostsAsync(Guid landedCostId, Guid userId);
    Task<bool> SetManualAllocationsAsync(Guid landedCostId, Guid landedCostItemId, SetManualLandedCostAllocationsDto dto, Guid userId);
    Task<bool> ApproveAsync(Guid landedCostId, Guid userId);
    Task<bool> PostToInventoryAsync(Guid landedCostId, Guid userId);
    Task<bool> CancelAsync(Guid landedCostId, string reason, Guid userId);
}

/// <summary>
/// Purchase Return management service interface
/// </summary>
public interface IPurchaseReturnService
{
    Task<IEnumerable<PurchaseReturnDto>> GetAllAsync(DateTime? fromDate = null, DateTime? toDate = null);
    Task<IEnumerable<PurchaseReturnDto>> GetBySupplierAsync(Guid supplierId);
    Task<IEnumerable<PurchaseReturnDto>> GetByWarehouseAsync(Guid warehouseId);
    Task<IEnumerable<PurchaseReturnDto>> GetPendingApprovalAsync();
    Task<PurchaseReturnDetailDto?> GetByIdAsync(Guid id);
    Task<PurchaseReturnDetailDto?> GetByReturnNumberAsync(string returnNumber);
    Task<PurchaseReturnDto> CreateAsync(CreatePurchaseReturnDto dto, Guid userId);
    Task<bool> SubmitForApprovalAsync(Guid returnId, Guid userId);
    Task<bool> ApproveAsync(Guid returnId, Guid userId);
    Task<bool> RejectAsync(Guid returnId, string reason, Guid userId);
    Task<bool> ShipAsync(Guid returnId, Guid userId, string? trackingNumber = null);
    Task<bool> RecordCreditNoteAsync(Guid returnId, string creditNoteNumber, decimal amount, Guid userId);
    Task<bool> CancelAsync(Guid returnId, string reason, Guid userId);
}

/// <summary>
/// Item Supplier management service interface
/// </summary>
public interface IItemSupplierService
{
    Task<IEnumerable<ItemSupplierDto>> GetByItemAsync(Guid inventoryItemId);
    Task<IEnumerable<ItemSupplierDto>> GetBySupplierAsync(Guid supplierId);
    Task<ItemSupplierDto?> GetPreferredSupplierAsync(Guid inventoryItemId);
    Task<ItemSupplierDto> CreateAsync(CreateItemSupplierDto dto);
    Task<ItemSupplierDto> UpdateAsync(Guid id, UpdateItemSupplierDto dto);
    Task<bool> SetAsPreferredAsync(Guid itemSupplierId);
    Task<bool> DeleteAsync(Guid id);
}

/// <summary>
/// Inventory Requisition management service interface
/// </summary>
public interface IInventoryRequisitionService
{
    Task<IEnumerable<InventoryRequisitionDto>> GetAllAsync(DateTime? fromDate = null, DateTime? toDate = null);
    Task<IEnumerable<InventoryRequisitionDto>> GetByProjectAsync(Guid projectId);
    Task<IEnumerable<InventoryRequisitionDto>> GetByWarehouseAsync(Guid warehouseId);
    Task<IEnumerable<InventoryRequisitionDto>> GetByDepartmentAsync(Guid departmentId);
    Task<IEnumerable<InventoryRequisitionDto>> GetPendingApprovalAsync();
    Task<IEnumerable<InventoryRequisitionDto>> GetPendingIssueAsync();
    Task<InventoryRequisitionDetailDto?> GetByIdAsync(Guid id);
    Task<InventoryRequisitionDetailDto?> GetByRequisitionNumberAsync(string requisitionNumber);
    Task<InventoryRequisitionDetailDto> CreateAsync(CreateInventoryRequisitionDto dto);
    Task<InventoryRequisitionDetailDto> UpdateAsync(Guid id, UpdateInventoryRequisitionDto dto);
    Task<bool> SubmitAsync(Guid id);
    Task<bool> ApproveAsync(Guid id, string? notes = null);
    Task<bool> RejectAsync(Guid id, string reason);
    Task<bool> IssueAsync(Guid id, IssueRequisitionDto dto);
    Task<IReadOnlyList<InventoryIssueReceiverDto>> GetIssueReceiversAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryIssueVoucherDto>> GetIssueVouchersAsync(Guid requisitionId, CancellationToken cancellationToken = default);
    Task<InventoryIssueVoucherDto?> GetIssueVoucherAsync(Guid voucherId, CancellationToken cancellationToken = default);
    Task<InventoryIssueVoucherDto> AcknowledgeIssueVoucherAsync(Guid voucherId, AcknowledgeInventoryIssueVoucherRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<bool> ReturnAsync(Guid id, ReturnRequisitionDto dto);
    Task<bool> CompleteAsync(Guid id);
    Task<bool> CancelAsync(Guid id, string reason);
    Task<InventoryRequisitionItemDto> AddItemAsync(Guid requisitionId, AddRequisitionItemDto dto);
    Task<InventoryRequisitionItemDto> UpdateItemAsync(Guid requisitionId, Guid itemId, UpdateRequisitionItemDto dto);
    Task<bool> RemoveItemAsync(Guid requisitionId, Guid itemId);
}

/// <summary>
/// Reusable TDC item-master normalization and validation boundary used by direct
/// create/edit, bulk import, and staged maker-checker application.
/// </summary>
public interface IInventoryItemProfileService
{
    Task NormalizeAndValidateAsync(
        InventoryItem item,
        Guid? existingItemId,
        CancellationToken cancellationToken = default);
}

public sealed class InventoryItemProfileValidationException : InvalidOperationException
{
    public InventoryItemProfileValidationException(string code, string message) : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

public sealed class InventoryIssueControlException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class InventoryIssueAuthorizationException(string message) : Exception(message);

public sealed class InventoryIssueNotFoundException(string message) : Exception(message);
