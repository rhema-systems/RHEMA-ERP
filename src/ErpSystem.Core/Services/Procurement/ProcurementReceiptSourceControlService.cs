using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public sealed class ProcurementReceiptSourceControlService :
    IProcurementReceiptSourceControlService
{
    private const string EventType = "ProcurementReceiptSourceControl";
    private const string RuleCode = "RCV-001";
    private const string RuleVersion = "TDC-0501";
    private static readonly IReadOnlyList<string> DecisionKeys =
        Enumerable.Range(1, 14).Select(value => $"DEC-{value:000}").ToArray();
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementAccessControlService _accessControl;
    private readonly IProcurementPurchaseOrderSourceService _purchaseOrderSources;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly INotificationTopicPublisher _notifications;
    private readonly ILogger<ProcurementReceiptSourceControlService> _logger;
    private readonly Dictionary<(Guid ReferenceId, Guid InventoryItemId), decimal>
        _pendingInventoryPostingQuantities = new();

    public ProcurementReceiptSourceControlService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService accessControl,
        IProcurementPurchaseOrderSourceService purchaseOrderSources,
        IProcurementControlEventService controlEvents,
        INotificationTopicPublisher notifications,
        ILogger<ProcurementReceiptSourceControlService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _accessControl = accessControl;
        _purchaseOrderSources = purchaseOrderSources;
        _controlEvents = controlEvents;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<ProcurementReceiptSourceReadinessDto> GetReadinessAsync(
        Guid purchaseOrderId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureTenant();
        var purchaseOrder = await LoadPurchaseOrderAsync(
            purchaseOrderId, tracking: false, cancellationToken);
        var correlation = NormalizeCorrelation(correlationId);
        await EnsureCapabilityAsync(
            "procurement.inventory.read",
            purchaseOrder,
            correlation,
            cancellationToken);

        return await EvaluateAsync(
            purchaseOrder,
            requestLines: null,
            existingReceipt: false,
            excludedPurchaseOrderReceiptId: null,
            excludedGoodsReceiptNoteId: null,
            cancellationToken);
    }

    public async Task<ProcurementReceiptSourceSnapshot> EnforceCreateAsync(
        PurchaseOrder purchaseOrder,
        IReadOnlyCollection<ProcurementReceiptSourceLineRequest> lines,
        string receiptKind,
        Guid receiptId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsurePurchaseOrder(purchaseOrder);
        var correlation = NormalizeCorrelation(correlationId);
        var action = string.Equals(
            receiptKind,
            "GoodsReceiptNote",
            StringComparison.OrdinalIgnoreCase)
            ? "CreateGoodsReceiptNote"
            : "CreatePurchaseOrderReceipt";
        await EnsureCapabilityAsync(
            "procurement.inventory.receive",
            purchaseOrder,
            correlation,
            cancellationToken);

        var readiness = await EvaluateAsync(
            purchaseOrder,
            lines,
            existingReceipt: false,
            excludedPurchaseOrderReceiptId: null,
            excludedGoodsReceiptNoteId: null,
            cancellationToken);
        EnsureAllowed(readiness);
        var snapshot = BuildSnapshot(readiness, action, receiptId, correlation);
        await RecordAllowedAsync(
            snapshot,
            action,
            receiptKind,
            receiptId,
            correlation,
            cancellationToken);
        await PublishAsync(
            snapshot,
            action,
            receiptKind,
            receiptId,
            cancellationToken);
        return snapshot;
    }

    public async Task<ProcurementReceiptSourceSnapshot>
        RevalidatePurchaseOrderReceiptAsync(
            Guid receiptId,
            string action,
            string correlationId,
            CancellationToken cancellationToken = default)
    {
        EnsureTenant();
        var receipt = await _unitOfWork.Repository<PurchaseOrderReceipt>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.Id == receiptId &&
                !item.IsDeleted)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ProcurementReceiptSourceNotFoundException(
                "RCV_RECEIPT_NOT_FOUND",
                "The purchase-order receipt was not found in the current tenant.");
        var purchaseOrder = await LoadPurchaseOrderAsync(
            receipt.PurchaseOrderId, tracking: false, cancellationToken);
        var correlation = NormalizeCorrelation(correlationId);
        var normalizedAction = NormalizeAction(action);
        var permission = ProcurementPurchaseOrderSodRules.IsReceiptAction(normalizedAction)
            ? ProcurementPurchaseOrderSodRules.RequiredPermissionForReceiptAction(normalizedAction)
            : "procurement.inventory.receive";
        await EnsureCapabilityAsync(
            permission,
            purchaseOrder,
            correlation,
            cancellationToken);
        var lines = await _unitOfWork.Repository<PurchaseOrderReceiptItem>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.ReceiptId == receipt.Id &&
                !item.IsDeleted)
            .AsNoTracking()
            .Select(item => new ProcurementReceiptSourceLineRequest
            {
                PurchaseOrderItemId = item.PurchaseOrderItemId,
                ReceivedQuantity = item.ReceivedQuantity
            })
            .ToListAsync(cancellationToken);
        var readiness = await EvaluateAsync(
            purchaseOrder,
            lines,
            existingReceipt: true,
            excludedPurchaseOrderReceiptId: receipt.Id,
            excludedGoodsReceiptNoteId: null,
            cancellationToken);
        EnsureAllowed(readiness);
        var snapshot = BuildSnapshot(
            readiness,
            normalizedAction,
            receipt.Id,
            correlation);
        await RecordAllowedAsync(
            snapshot,
            NormalizeAction(action),
            "PurchaseOrderReceipt",
            receipt.Id,
            correlation,
            cancellationToken);
        return snapshot;
    }

    public async Task<ProcurementReceiptSourceSnapshot>
        RevalidateGoodsReceiptNoteAsync(
            Guid goodsReceiptNoteId,
            string action,
            string correlationId,
            CancellationToken cancellationToken = default)
    {
        EnsureTenant();
        var goodsReceiptNote = await _unitOfWork.Repository<GoodsReceiptNote>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.Id == goodsReceiptNoteId &&
                !item.IsDeleted)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ProcurementReceiptSourceNotFoundException(
                "RCV_GRN_NOT_FOUND",
                "The goods receipt note was not found in the current tenant.");

        if (goodsReceiptNote.PurchaseOrderReceiptId.HasValue)
        {
            return await RevalidatePurchaseOrderReceiptAsync(
                goodsReceiptNote.PurchaseOrderReceiptId.Value,
                action,
                correlationId,
                cancellationToken);
        }

        if (!goodsReceiptNote.PurchaseOrderId.HasValue ||
            goodsReceiptNote.PurchaseOrderId.Value == Guid.Empty)
        {
            throw new ProcurementReceiptSourceValidationException(
                "RCV_SOURCE_REQUIRED",
                "A goods receipt note must be linked to a governed purchase order or procurement receipt.");
        }

        var purchaseOrder = await LoadPurchaseOrderAsync(
            goodsReceiptNote.PurchaseOrderId.Value,
            tracking: false,
            cancellationToken);
        var correlation = NormalizeCorrelation(correlationId);
        await EnsureCapabilityAsync(
            "procurement.inventory.receive",
            purchaseOrder,
            correlation,
            cancellationToken);
        var lines = await _unitOfWork.Repository<GoodsReceiptNoteItem>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.GoodsReceiptNoteId == goodsReceiptNote.Id &&
                !item.IsDeleted)
            .AsNoTracking()
            .Select(item => new ProcurementReceiptSourceLineRequest
            {
                PurchaseOrderItemId = item.PurchaseOrderItemId ?? Guid.Empty,
                InventoryItemId = item.InventoryItemId,
                ReceivedQuantity = item.ReceivedQuantity
            })
            .ToListAsync(cancellationToken);
        var readiness = await EvaluateAsync(
            purchaseOrder,
            lines,
            existingReceipt: true,
            excludedPurchaseOrderReceiptId: null,
            excludedGoodsReceiptNoteId: goodsReceiptNote.Id,
            cancellationToken);
        EnsureAllowed(readiness);
        var normalizedAction = NormalizeAction(action);
        var snapshot = BuildSnapshot(
            readiness,
            normalizedAction,
            goodsReceiptNote.Id,
            correlation);
        await RecordAllowedAsync(
            snapshot,
            normalizedAction,
            "GoodsReceiptNote",
            goodsReceiptNote.Id,
            correlation,
            cancellationToken);
        return snapshot;
    }

    public async Task EnforceInventoryPostingAsync(
        ReferenceType referenceType,
        Guid? referenceId,
        Guid inventoryItemId,
        decimal quantity,
        string correlationId,
        string authorizationAction = "AuthorizeInventoryPosting",
        CancellationToken cancellationToken = default)
    {
        EnsureTenant();
        var correlation = NormalizeCorrelation(correlationId);
        if (referenceType != ReferenceType.PO)
        {
            throw new ProcurementReceiptSourceValidationException(
                "RCV_DIRECT_STOCK_SOURCE_FORBIDDEN",
                "Purchase-receipt inventory can be posted only from a governed purchase-order receipt or GRN. Vendor invoices and other direct references cannot create stock.");
        }

        if (!referenceId.HasValue ||
            referenceId.Value == Guid.Empty ||
            inventoryItemId == Guid.Empty ||
            quantity <= 0)
        {
            throw new ProcurementReceiptSourceValidationException(
                "RCV_STOCK_POSTING_INPUT_INVALID",
                "A governed receipt reference, inventory item, and positive quantity are required for stock posting.");
        }

        var authorizedQuantity = 0m;
        var receipt = await _unitOfWork.Repository<PurchaseOrderReceipt>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.Id == referenceId.Value &&
                !item.IsDeleted)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (receipt != null)
        {
            await RevalidatePurchaseOrderReceiptAsync(
                receipt.Id,
                authorizationAction,
                correlation,
                cancellationToken);
            var receiptLines = await (
                    from line in _unitOfWork
                        .Repository<PurchaseOrderReceiptItem>()
                        .GetQueryable(item =>
                            item.TenantId == _currentUser.TenantId &&
                            item.ReceiptId == receipt.Id &&
                            !item.IsDeleted)
                    join purchaseOrderItem in _unitOfWork
                        .Repository<PurchaseOrderItem>()
                        .GetQueryable(item =>
                            item.TenantId == _currentUser.TenantId &&
                            item.InventoryItemId == inventoryItemId &&
                            !item.IsDeleted)
                        on line.PurchaseOrderItemId equals
                        purchaseOrderItem.Id
                    select new
                    {
                        line.AcceptedQuantity,
                        line.ItemUnitOfMeasureId
                    })
                .AsNoTracking()
                .ToListAsync(cancellationToken);
            foreach (var line in receiptLines)
            {
                var conversion = 1m;
                if (line.ItemUnitOfMeasureId.HasValue)
                {
                    conversion = await _unitOfWork
                        .Repository<ItemUnitOfMeasure>()
                        .GetQueryable(item =>
                            item.TenantId == _currentUser.TenantId &&
                            item.Id == line.ItemUnitOfMeasureId.Value &&
                            !item.IsDeleted)
                        .AsNoTracking()
                        .Select(item => item.ConversionToBase)
                        .SingleOrDefaultAsync(cancellationToken);
                    if (conversion <= 0)
                        conversion = 1m;
                }

                authorizedQuantity +=
                    line.AcceptedQuantity * conversion;
            }
        }
        else
        {
            var goodsReceiptNote = await _unitOfWork
                .Repository<GoodsReceiptNote>()
                .GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.Id == referenceId.Value &&
                    !item.IsDeleted)
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);
            if (goodsReceiptNote == null)
            {
                throw new ProcurementReceiptSourceValidationException(
                    "RCV_STOCK_SOURCE_NOT_FOUND",
                    "The inventory posting reference is not a governed receipt in the current tenant.");
            }

            await RevalidateGoodsReceiptNoteAsync(
                goodsReceiptNote.Id,
                "AuthorizeInventoryPosting",
                correlation,
                cancellationToken);
            authorizedQuantity = await _unitOfWork
                .Repository<GoodsReceiptNoteItem>()
                .GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.GoodsReceiptNoteId ==
                    goodsReceiptNote.Id &&
                    item.InventoryItemId == inventoryItemId &&
                    !item.IsDeleted)
                .AsNoTracking()
                .SumAsync(
                    item => item.AcceptedQuantity,
                    cancellationToken);
        }

        var alreadyPosted = await _unitOfWork
            .Repository<InventoryMovement>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.ReferenceId == referenceId.Value &&
                item.InventoryItemId == inventoryItemId &&
                item.MovementType ==
                InventoryMovementType.PurchaseReceipt &&
                item.Direction == MovementDirection.In &&
                !item.IsReversal &&
                !item.IsDeleted)
            .AsNoTracking()
            .SumAsync(item => item.Quantity, cancellationToken);
        var key = (referenceId.Value, inventoryItemId);
        var pending = _pendingInventoryPostingQuantities
            .GetValueOrDefault(key);
        if (authorizedQuantity <= 0 ||
            alreadyPosted + pending + quantity >
            authorizedQuantity)
        {
            throw new ProcurementReceiptSourceValidationException(
                "RCV_STOCK_QUANTITY_EXCEEDED",
                $"Stock posting quantity would exceed the governed accepted receipt quantity {authorizedQuantity:0.####}.");
        }

        _pendingInventoryPostingQuantities[key] =
            pending + quantity;
    }

    public void ResetInventoryPostingAttempt() =>
        _pendingInventoryPostingQuantities.Clear();

    public async Task RecordDeniedAsync(
        Guid? purchaseOrderId,
        string sourceReference,
        string action,
        string code,
        string message,
        string correlationId,
        object? inputValues = null,
        CancellationToken cancellationToken = default)
    {
        EnsureTenant();
        var correlation = NormalizeCorrelation(correlationId);
        await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create(
                "receipt-source-denied",
                _currentUser.TenantId,
                purchaseOrderId,
                NormalizeAction(action),
                code,
                correlation),
            EventType = EventType,
            Action = NormalizeAction(action),
            Result = ProcurementControlEventResult.Denied,
            RuleCode = RuleCode,
            RuleVersion = RuleVersion,
            DecisionKeys = DecisionKeys.ToList(),
            SourceType = "PurchaseOrder",
            SourceId = purchaseOrderId,
            SourceReference = string.IsNullOrWhiteSpace(sourceReference)
                ? purchaseOrderId?.ToString() ?? "missing"
                : sourceReference.Trim(),
            Reason = message,
            InputValues = inputValues,
            ResultValues = new { Allowed = false, Code = code },
            CorrelationId = correlation,
            CausationId = correlation,
            OccurredAtUtc = DateTime.UtcNow
        }, cancellationToken);
    }

    private async Task<ProcurementReceiptSourceReadinessDto> EvaluateAsync(
        PurchaseOrder purchaseOrder,
        IReadOnlyCollection<ProcurementReceiptSourceLineRequest>? requestLines,
        bool existingReceipt,
        Guid? excludedPurchaseOrderReceiptId,
        Guid? excludedGoodsReceiptNoteId,
        CancellationToken cancellationToken)
    {
        var evaluatedAt = DateTime.UtcNow;
        var tolerance = ProcurementReceiptSourceRules.NormalizeTolerance(
            purchaseOrder.TolerancePercent);
        var sourceValid = true;
        var sourceCode = "RCV_SOURCE_VALID";
        var sourceMessage =
            "The purchase order retains its immutable approved source lineage.";

        try
        {
            await _purchaseOrderSources.EvaluateCurrentAsync(
                purchaseOrder,
                cancellationToken);
        }
        catch (ProcurementPurchaseOrderSourceAuthorizationException exception)
        {
            throw new ProcurementReceiptSourceAuthorizationException(
                exception.Message);
        }
        catch (ProcurementPurchaseOrderSourceValidationException exception)
        {
            sourceValid = false;
            sourceCode = exception.Code;
            sourceMessage = exception.Message;
        }

        if (!ProcurementReceiptSourceRules.IsReceivableStatus(
                purchaseOrder.Status,
                existingReceipt))
        {
            sourceValid = false;
            sourceCode = "RCV_PO_STATUS_NOT_RECEIVABLE";
            sourceMessage =
                $"Purchase order {purchaseOrder.OrderNumber} cannot be received in status {purchaseOrder.Status}.";
        }

        var purchaseOrderItems = await _unitOfWork.Repository<PurchaseOrderItem>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.PurchaseOrderId == purchaseOrder.Id &&
                !item.IsDeleted)
            .AsNoTracking()
            .Include(item => item.InventoryItem)
            .ToListAsync(cancellationToken);
        var purchaseOrderItemsById = purchaseOrderItems.ToDictionary(item => item.Id);

        var requested = requestLines?.ToList();
        if (requested is { Count: > 0 })
        {
            var duplicate = requested
                .GroupBy(item => item.PurchaseOrderItemId)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicate != null)
            {
                throw new ProcurementReceiptSourceValidationException(
                    "RCV_DUPLICATE_LINE",
                    $"Purchase-order line {duplicate.Key} occurs more than once in the receipt request.");
            }
        }

        var purchaseReceiptTotals = await (
                from line in _unitOfWork.Repository<PurchaseOrderReceiptItem>()
                    .GetQueryable(item =>
                        item.TenantId == _currentUser.TenantId &&
                        !item.IsDeleted)
                join receipt in _unitOfWork.Repository<PurchaseOrderReceipt>()
                    .GetQueryable(item =>
                        item.TenantId == _currentUser.TenantId &&
                        item.PurchaseOrderId == purchaseOrder.Id &&
                        !item.IsDeleted &&
                        item.Id != excludedPurchaseOrderReceiptId &&
                        item.Status != "Rejected")
                    on line.ReceiptId equals receipt.Id
                group line by line.PurchaseOrderItemId
                into grouped
                select new
                {
                    PurchaseOrderItemId = grouped.Key,
                    // TDC-0502: independently rejected quantities no longer
                    // consume PO receipt capacity, allowing an evidenced
                    // replacement without weakening the original source cap.
                    Quantity = grouped.Sum(item =>
                        item.ReceivedQuantity - item.RejectedQuantity)
                })
            .AsNoTracking()
            .ToDictionaryAsync(
                item => item.PurchaseOrderItemId,
                item => item.Quantity,
                cancellationToken);

        var goodsReceiptTotals = await (
                from line in _unitOfWork.Repository<GoodsReceiptNoteItem>()
                    .GetQueryable(item =>
                        item.TenantId == _currentUser.TenantId &&
                        !item.IsDeleted &&
                        item.PurchaseOrderItemId.HasValue)
                join receipt in _unitOfWork.Repository<GoodsReceiptNote>()
                    .GetQueryable(item =>
                        item.TenantId == _currentUser.TenantId &&
                        item.PurchaseOrderId == purchaseOrder.Id &&
                        !item.IsDeleted &&
                        !item.PurchaseOrderReceiptId.HasValue &&
                        item.Id != excludedGoodsReceiptNoteId &&
                        item.Status != GRNStatus.Rejected &&
                        item.Status != GRNStatus.Cancelled)
                    on line.GoodsReceiptNoteId equals receipt.Id
                group line by line.PurchaseOrderItemId!.Value
                into grouped
                select new
                {
                    PurchaseOrderItemId = grouped.Key,
                    Quantity = grouped.Sum(item =>
                        item.ReceivedQuantity - item.RejectedQuantity)
                })
            .AsNoTracking()
            .ToDictionaryAsync(
                item => item.PurchaseOrderItemId,
                item => item.Quantity,
                cancellationToken);

        var sourceLines = requested is { Count: > 0 }
            ? requested
            : purchaseOrderItems.Select(item =>
                new ProcurementReceiptSourceLineRequest
                {
                    PurchaseOrderItemId = item.Id,
                    InventoryItemId = item.InventoryItemId,
                    ReceivedQuantity = 0
                }).ToList();
        var lines = new List<ProcurementReceiptSourceLineDto>();
        foreach (var request in sourceLines)
        {
            if (!purchaseOrderItemsById.TryGetValue(
                    request.PurchaseOrderItemId,
                    out var purchaseOrderItem))
            {
                lines.Add(new ProcurementReceiptSourceLineDto
                {
                    PurchaseOrderItemId = request.PurchaseOrderItemId,
                    InventoryItemId = request.InventoryItemId,
                    RequestedQuantity = request.ReceivedQuantity,
                    Allowed = false,
                    Code = "RCV_PO_LINE_NOT_FOUND",
                    Message =
                        "The receipt line does not belong to the governed purchase order.",
                    IntegrityHash = ProcurementReceiptSourceRules.Hash(
                        purchaseOrder.Id,
                        request.PurchaseOrderItemId,
                        request.ReceivedQuantity,
                        "missing")
                });
                continue;
            }

            var previous =
                purchaseReceiptTotals.GetValueOrDefault(purchaseOrderItem.Id) +
                goodsReceiptTotals.GetValueOrDefault(purchaseOrderItem.Id);
            var capacity = ProcurementReceiptSourceRules.CalculateCapacity(
                purchaseOrderItem.OrderedQuantity,
                previous,
                tolerance);
            var decision = ProcurementReceiptSourceRules.EvaluateLine(
                requestLines == null ? Math.Min(1m, capacity.RemainingQuantity) : request.ReceivedQuantity,
                capacity.RemainingQuantity);
            if (requestLines == null)
            {
                decision = capacity.RemainingQuantity > 0
                    ? (
                        true,
                        "RCV_LINE_CAPACITY_AVAILABLE",
                        "Receipt capacity remains available for this purchase-order line.")
                    : (
                        false,
                        "RCV_LINE_FULLY_RECEIPTED",
                        "No governed receipt capacity remains for this purchase-order line.");
            }

            if (request.InventoryItemId.HasValue &&
                request.InventoryItemId.Value != Guid.Empty &&
                request.InventoryItemId != purchaseOrderItem.InventoryItemId)
            {
                decision = (
                    false,
                    "RCV_INVENTORY_ITEM_MISMATCH",
                    "The receipt inventory item does not match the governed purchase-order line.");
            }

            lines.Add(new ProcurementReceiptSourceLineDto
            {
                PurchaseOrderItemId = purchaseOrderItem.Id,
                InventoryItemId = purchaseOrderItem.InventoryItemId,
                ItemCode = purchaseOrderItem.InventoryItem?.ItemCode ??
                           purchaseOrderItem.BusinessPartnerItemCode ??
                           string.Empty,
                ItemName = purchaseOrderItem.InventoryItem?.Name ??
                           purchaseOrderItem.ItemDescription ??
                           string.Empty,
                UnitOfMeasure = purchaseOrderItem.UnitOfMeasure,
                OrderedQuantity = purchaseOrderItem.OrderedQuantity,
                PreviouslyReceiptedQuantity = previous,
                ToleranceQuantity = capacity.ToleranceQuantity,
                MaximumReceivableQuantity = capacity.MaximumQuantity,
                RemainingQuantity = capacity.RemainingQuantity,
                RequestedQuantity = request.ReceivedQuantity,
                Allowed = decision.Allowed,
                Code = decision.Code,
                Message = decision.Message,
                IntegrityHash = ProcurementReceiptSourceRules.Hash(
                    purchaseOrder.Id,
                    purchaseOrderItem.Id,
                    purchaseOrderItem.InventoryItemId,
                    purchaseOrderItem.UnitOfMeasure,
                    purchaseOrderItem.OrderedQuantity,
                    previous,
                    tolerance,
                    capacity.MaximumQuantity,
                    capacity.RemainingQuantity,
                    request.ReceivedQuantity)
            });
        }

        var requestedLinesValid =
            requestLines == null ||
            requestLines.Count > 0 && lines.Count == requestLines.Count &&
            lines.All(item => item.Allowed);
        var canReceive =
            sourceValid &&
            requestedLinesValid &&
            lines.Any(item => requestLines == null
                ? item.RemainingQuantity > 0
                : item.RequestedQuantity > 0);
        var code = canReceive
            ? "RCV_SOURCE_READY"
            : !sourceValid
                ? sourceCode
                : requestLines is { Count: 0 }
                    ? "RCV_LINES_REQUIRED"
                    : lines.FirstOrDefault(item => !item.Allowed)?.Code ??
                      "RCV_NO_CAPACITY";
        var message = canReceive
            ? "The approved purchase-order source and all receipt-line capacities are valid."
            : !sourceValid
                ? sourceMessage
                : requestLines is { Count: 0 }
                    ? "At least one positive receipt line is required."
                    : lines.FirstOrDefault(item => !item.Allowed)?.Message ??
                      "No governed receipt capacity remains.";

        return new ProcurementReceiptSourceReadinessDto
        {
            PurchaseOrderId = purchaseOrder.Id,
            OrderNumber = purchaseOrder.OrderNumber,
            PurchaseOrderStatus = purchaseOrder.Status,
            SourceType = purchaseOrder.ProcurementSourceType,
            SourceId = purchaseOrder.ProcurementSourceId,
            SourceReference = purchaseOrder.ProcurementSourceReference ??
                              string.Empty,
            SourceIntegrityHash = purchaseOrder.SourceIntegrityHash ??
                                  string.Empty,
            TolerancePercent = tolerance,
            SourceValid = sourceValid,
            CanReceive = canReceive,
            Code = code,
            Message = message,
            EvaluatedAtUtc = evaluatedAt,
            DecisionKeys = DecisionKeys,
            Lines = lines,
            RequiredActions = canReceive
                ? Array.Empty<string>()
                : new[] { message }
        };
    }

    private static ProcurementReceiptSourceSnapshot BuildSnapshot(
        ProcurementReceiptSourceReadinessDto readiness,
        string action,
        Guid receiptId,
        string correlationId)
    {
        var snapshotJson = JsonSerializer.Serialize(new
        {
            SchemaVersion = 1,
            RuleCode,
            RuleVersion,
            Action = NormalizeAction(action),
            ReceiptId = receiptId,
            readiness.PurchaseOrderId,
            readiness.OrderNumber,
            readiness.PurchaseOrderStatus,
            readiness.SourceType,
            readiness.SourceId,
            readiness.SourceReference,
            readiness.SourceIntegrityHash,
            readiness.TolerancePercent,
            readiness.EvaluatedAtUtc,
            readiness.DecisionKeys,
            readiness.Lines,
            CorrelationId = correlationId
        }, JsonOptions);
        var integrityHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(snapshotJson)));
        return new ProcurementReceiptSourceSnapshot
        {
            SnapshotJson = snapshotJson,
            IntegrityHash = integrityHash,
            TolerancePercent = readiness.TolerancePercent,
            ValidatedAtUtc = readiness.EvaluatedAtUtc,
            Readiness = readiness
        };
    }

    private async Task RecordAllowedAsync(
        ProcurementReceiptSourceSnapshot snapshot,
        string action,
        string receiptKind,
        Guid receiptId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var readiness = snapshot.Readiness;
        await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create(
                "receipt-source",
                _currentUser.TenantId,
                receiptKind,
                receiptId,
                NormalizeAction(action),
                correlationId),
            EventType = EventType,
            Action = NormalizeAction(action),
            Result = ProcurementControlEventResult.Allowed,
            RuleCode = RuleCode,
            RuleVersion = RuleVersion,
            DecisionKeys = DecisionKeys.ToList(),
            SourceType = receiptKind,
            SourceId = receiptId,
            SourceReference = readiness.OrderNumber,
            Reason = readiness.Message,
            InputValues = new
            {
                readiness.PurchaseOrderId,
                readiness.SourceType,
                readiness.SourceId,
                readiness.SourceReference,
                readiness.TolerancePercent,
                LineCount = readiness.Lines.Count,
                RequestedQuantity = readiness.Lines.Sum(item => item.RequestedQuantity)
            },
            ResultValues = new
            {
                Allowed = true,
                readiness.Code,
                snapshot.IntegrityHash,
                readiness.SourceIntegrityHash
            },
            After = readiness.Lines.Select(item => new
            {
                item.PurchaseOrderItemId,
                item.PreviouslyReceiptedQuantity,
                item.RequestedQuantity,
                item.RemainingQuantity,
                item.MaximumReceivableQuantity,
                item.IntegrityHash
            }),
            CorrelationId = correlationId,
            CausationId = correlationId,
            OccurredAtUtc = readiness.EvaluatedAtUtc
        }, cancellationToken);
    }

    private async Task PublishAsync(
        ProcurementReceiptSourceSnapshot snapshot,
        string action,
        string receiptKind,
        Guid receiptId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _notifications.PublishAsync(new NotificationTopicEvent
            {
                TenantId = _currentUser.TenantId,
                TopicKey = "procurement.receipt-source.allowed",
                NotificationType = EventType,
                EntityType = receiptKind,
                EntityId = receiptId,
                TriggeredByUserId = _currentUser.UserId,
                Data = new Dictionary<string, object>
                {
                    ["purchaseOrderId"] = snapshot.Readiness.PurchaseOrderId,
                    ["orderNumber"] = snapshot.Readiness.OrderNumber,
                    ["action"] = NormalizeAction(action),
                    ["integrityHash"] = snapshot.IntegrityHash
                }
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to publish receipt-source notification for {ReceiptId}",
                receiptId);
        }
    }

    private async Task EnsureCapabilityAsync(
        string permission,
        PurchaseOrder purchaseOrder,
        string correlationId,
        CancellationToken cancellationToken)
    {
        try
        {
            var decision = await _accessControl.EnforceCapabilityAsync(
                new ProcurementAccessCapabilityRequest
                {
                    PermissionCode = permission,
                    SourceType = EventType,
                    SourceReference = purchaseOrder.OrderNumber,
                    WarehouseId = purchaseOrder.DeliveryWarehouseId
                },
                correlationId,
                cancellationToken);
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

    private async Task<PurchaseOrder> LoadPurchaseOrderAsync(
        Guid purchaseOrderId,
        bool tracking,
        CancellationToken cancellationToken)
    {
        var query = _unitOfWork.Repository<PurchaseOrder>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.Id == purchaseOrderId &&
                !item.IsDeleted);
        if (!tracking)
            query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(cancellationToken)
            ?? throw new ProcurementReceiptSourceNotFoundException(
                "RCV_PURCHASE_ORDER_NOT_FOUND",
                "The purchase order was not found in the current tenant.");
    }

    private void EnsurePurchaseOrder(PurchaseOrder purchaseOrder)
    {
        EnsureTenant();
        if (purchaseOrder.TenantId != _currentUser.TenantId ||
            purchaseOrder.IsDeleted)
        {
            throw new ProcurementReceiptSourceAuthorizationException(
                "The purchase order is not available in the current tenant.");
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
                "Supplier portal users cannot access internal receiving controls.");
        }
    }

    private static void EnsureAllowed(
        ProcurementReceiptSourceReadinessDto readiness)
    {
        if (!readiness.CanReceive)
        {
            throw new ProcurementReceiptSourceValidationException(
                readiness.Code,
                readiness.Message,
                readiness);
        }
    }

    private static string NormalizeCorrelation(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? Guid.NewGuid().ToString("N")
            : value.Trim().Length <= 100
                ? value.Trim()
                : value.Trim()[..100];

    private static string NormalizeAction(string value)
    {
        var normalized = string.IsNullOrWhiteSpace(value)
            ? "Validate"
            : value.Trim();
        return normalized.Length <= 100 ? normalized : normalized[..100];
    }
}
