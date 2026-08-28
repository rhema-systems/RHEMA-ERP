using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public sealed class ProcurementPurchaseOrderSodService :
    IProcurementPurchaseOrderSodService
{
    private const string EventType = "ProcurementPurchaseOrderSod";
    private const string SourceType = "PurchaseOrder";
    private static readonly IReadOnlyList<string> DecisionKeys =
        Enumerable.Range(1, 14).Select(value => $"DEC-{value:000}").ToList();

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementAccessControlService _accessControl;
    private readonly IProcurementSodGuardService _sodGuard;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly INotificationTopicPublisher _notifications;
    private readonly ILogger<ProcurementPurchaseOrderSodService> _logger;

    public ProcurementPurchaseOrderSodService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService accessControl,
        IProcurementSodGuardService sodGuard,
        IProcurementControlEventService controlEvents,
        INotificationTopicPublisher notifications,
        ILogger<ProcurementPurchaseOrderSodService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _accessControl = accessControl;
        _sodGuard = sodGuard;
        _controlEvents = controlEvents;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<ProcurementPurchaseOrderSodReadinessDto> GetReadinessAsync(
        Guid purchaseOrderId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureTenant();
        var purchaseOrder = await LoadAsync(purchaseOrderId, cancellationToken);
        var correlation = NormalizeCorrelation(correlationId);
        await EnsureCapabilityAsync(
            "procurement.records.read",
            purchaseOrder.OrderNumber,
            correlation,
            cancellationToken);
        return await EvaluateAsync(
            purchaseOrder,
            enforceAction: null,
            correlation,
            cancellationToken);
    }

    public async Task<ProcurementPurchaseOrderSodReadinessDto> EnforceApprovalAsync(
        PurchaseOrder purchaseOrder,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsurePurchaseOrder(purchaseOrder);
        var correlation = NormalizeCorrelation(correlationId);
        await EnsureCapabilityAsync(
            "procurement.purchase-order.approve",
            purchaseOrder.OrderNumber,
            correlation,
            cancellationToken);
        var readiness = await EvaluateAsync(
            purchaseOrder,
            "Approve",
            correlation,
            cancellationToken);
        await RecordAsync(readiness, "Approve", correlation, cancellationToken);
        await PublishAsync(readiness, "Approve", cancellationToken);
        if (!readiness.CanApprove)
        {
            throw new ProcurementPurchaseOrderSodBlockedException(
                "PO_SOD_APPROVAL_BLOCKED",
                readiness.Checks.Single(item => item.Key == "approval").Message,
                readiness);
        }
        return readiness;
    }

    public async Task<ProcurementPurchaseOrderSodReadinessDto> EnforceReceiptAsync(
        PurchaseOrder purchaseOrder,
        string correlationId,
        CancellationToken cancellationToken = default) =>
        await EnforceReceiptActionAsync(
            purchaseOrder,
            ProcurementPurchaseOrderSodRules.CreatePurchaseOrderReceipt,
            correlationId,
            cancellationToken);

    public async Task<ProcurementPurchaseOrderSodReadinessDto>
        EnforceReceiptActionAsync(
            PurchaseOrder purchaseOrder,
            string receiptAction,
            string correlationId,
            CancellationToken cancellationToken = default,
            Guid? warehouseId = null)
    {
        EnsurePurchaseOrder(purchaseOrder);
        var action = ProcurementPurchaseOrderSodRules.NormalizeReceiptAction(
            receiptAction);
        var correlation = NormalizeCorrelation(correlationId);
        var effectiveWarehouseId = await ResolveReceiptWarehouseIdAsync(
            purchaseOrder,
            warehouseId,
            cancellationToken);
        await EnsureCapabilityAsync(
            ProcurementPurchaseOrderSodRules.RequiredPermissionForReceiptAction(action),
            purchaseOrder.OrderNumber,
            correlation,
            cancellationToken,
            effectiveWarehouseId);
        var readiness = await EvaluateAsync(
            purchaseOrder,
            "Receive",
            correlation,
            cancellationToken);
        await RecordAsync(readiness, action, correlation, cancellationToken);
        await PublishAsync(readiness, action, cancellationToken);
        if (!readiness.CanReceive)
        {
            throw new ProcurementPurchaseOrderSodBlockedException(
                "PO_SOD_RECEIPT_BLOCKED",
                readiness.Checks.Single(item => item.Key == "receipt").Message,
                readiness);
        }
        return readiness;
    }

    private async Task<Guid?> ResolveReceiptWarehouseIdAsync(
        PurchaseOrder purchaseOrder,
        Guid? requestedWarehouseId,
        CancellationToken cancellationToken)
    {
        if (requestedWarehouseId.HasValue && requestedWarehouseId.Value != Guid.Empty)
            return requestedWarehouseId;
        if (purchaseOrder.DeliveryWarehouseId.HasValue &&
            purchaseOrder.DeliveryWarehouseId.Value != Guid.Empty)
            return purchaseOrder.DeliveryWarehouseId;

        var lineWarehouseId = await _unitOfWork.Repository<PurchaseOrderItem>()
            .GetQueryable(item =>
                item.TenantId == purchaseOrder.TenantId &&
                item.PurchaseOrderId == purchaseOrder.Id &&
                !item.IsDeleted &&
                item.WarehouseId.HasValue &&
                item.WarehouseId.Value != Guid.Empty)
            .Select(item => item.WarehouseId)
            .FirstOrDefaultAsync(cancellationToken);
        if (lineWarehouseId.HasValue && lineWarehouseId.Value != Guid.Empty)
            return lineWarehouseId;

        var receiptLocationId = await _unitOfWork
            .Repository<PurchaseOrderReceiptItem>()
            .GetQueryable(item =>
                item.TenantId == purchaseOrder.TenantId &&
                item.Receipt.PurchaseOrderId == purchaseOrder.Id &&
                !item.IsDeleted &&
                item.LocationId.HasValue &&
                item.LocationId.Value != Guid.Empty)
            .Select(item => item.LocationId)
            .FirstOrDefaultAsync(cancellationToken);
        if (!receiptLocationId.HasValue || receiptLocationId.Value == Guid.Empty)
            return null;

        var location = await _unitOfWork.Repository<WarehouseLocation>()
            .GetQueryable(item =>
                item.TenantId == purchaseOrder.TenantId &&
                item.Id == receiptLocationId.Value &&
                !item.IsDeleted)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        return location?.InventoryWarehouseId;
    }

    public async Task RejectApprovalBypassAsync(
        PurchaseOrder purchaseOrder,
        string attempt,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsurePurchaseOrder(purchaseOrder);
        var normalizedAttempt =
            ProcurementPurchaseOrderSodRules.NormalizeBypassAttempt(attempt);
        var permission = normalizedAttempt is "AwardAutoApprove" or
            "WorkflowAutoApprove" or "FrameworkWorkflowAutoApprove"
                ? "procurement.purchase-order.create"
                : "procurement.purchase-order.approve";
        var correlation = NormalizeCorrelation(correlationId);
        await EnsureCapabilityAsync(
            permission,
            purchaseOrder.OrderNumber,
            correlation,
            cancellationToken);

        var readiness = await EvaluateAsync(
            purchaseOrder,
            "Approve",
            correlation,
            cancellationToken);
        var approval = readiness.Checks.Single(item => item.Key == "approval");
        var code = ProcurementPurchaseOrderSodRules.BypassCode(normalizedAttempt);
        var message = normalizedAttempt == "DirectStatusApprove"
            ? "Purchase orders can be approved only through the assigned shared-workflow approval endpoint."
            : "Purchase-order submission or award conversion cannot complete approval automatically; an independent assigned approver must make the positive decision.";
        var blockedApproval = CopyCheck(
            approval,
            allowed: false,
            code,
            message);
        readiness = CopyReadiness(
            readiness,
            canApprove: false,
            code,
            message,
            readiness.Checks
                .Select(item => item.Key == "approval" ? blockedApproval : item)
                .ToList());

        await RecordAsync(
            readiness,
            normalizedAttempt,
            correlation,
            cancellationToken);
        await PublishAsync(readiness, normalizedAttempt, cancellationToken);
        throw new ProcurementPurchaseOrderSodBlockedException(
            code,
            message,
            readiness);
    }

    private async Task<ProcurementPurchaseOrderSodReadinessDto> EvaluateAsync(
        PurchaseOrder purchaseOrder,
        string? enforceAction,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var participants = await ResolveParticipantsAsync(
            purchaseOrder,
            cancellationToken);
        var approvalParticipants =
            ProcurementPurchaseOrderSodRules.Participants(
                participants.SourceRequesterUserId,
                purchaseOrder.RequestedById,
                purchaseOrder.CreatedById,
                participants.FrameworkCreatorUserId);
        var receiptParticipants =
            ProcurementPurchaseOrderSodRules.Participants(
                purchaseOrder.CreatedById,
                participants.FrameworkCreatorUserId);

        var approval = await EvaluateCheckAsync(
            "approval",
            "Independent PO approval",
            "Approve",
            ProcurementPurchaseOrderSodRules.ApprovalControl,
            approvalParticipants,
            [
                "Source requisition requester",
                "Purchase-order requester",
                "Purchase-order creator"
            ],
            "Requester and creator lineage is incomplete; independent approval cannot be proven.",
            purchaseOrder,
            string.Equals(enforceAction, "Approve", StringComparison.Ordinal),
            correlationId,
            cancellationToken);
        var receipt = await EvaluateCheckAsync(
            "receipt",
            "Independent goods receipt",
            "Receive",
            ProcurementPurchaseOrderSodRules.ReceiptControl,
            receiptParticipants,
            ["Purchase-order creator"],
            "Purchase-order creator lineage is incomplete; independent receipt confirmation cannot be proven.",
            purchaseOrder,
            string.Equals(enforceAction, "Receive", StringComparison.Ordinal),
            correlationId,
            cancellationToken);
        var checks = new[] { approval, receipt };
        var canApprove = approval.Allowed;
        var canReceive = receipt.Allowed;
        return new ProcurementPurchaseOrderSodReadinessDto
        {
            PurchaseOrderId = purchaseOrder.Id,
            OrderNumber = purchaseOrder.OrderNumber,
            Status = purchaseOrder.Status,
            CurrentActorUserId = _currentUser.UserId,
            CanApprove = canApprove,
            CanReceive = canReceive,
            Code = canApprove && canReceive
                ? "PO_SOD_READY"
                : "PO_SOD_RESTRICTED",
            Message = canApprove && canReceive
                ? "The current actor is independent for both PO approval and primary goods-receipt confirmation."
                : "One or more PO segregation-of-duties actions are prohibited for the current actor.",
            EvaluatedAtUtc = DateTime.UtcNow,
            DecisionKeys = DecisionKeys,
            ReceiptActionCoverage =
                ProcurementPurchaseOrderSodRules.ReceiptActionCoverage,
            Checks = checks
        };
    }

    private async Task<ProcurementPurchaseOrderSodCheckDto> EvaluateCheckAsync(
        string key,
        string label,
        string action,
        string controlCode,
        IReadOnlyList<Guid> prohibitedActorUserIds,
        IReadOnlyList<string> participantRoles,
        string missingMessage,
        PurchaseOrder purchaseOrder,
        bool enforce,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (prohibitedActorUserIds.Count == 0)
        {
            return new ProcurementPurchaseOrderSodCheckDto
            {
                Key = key,
                Label = label,
                Action = action,
                ControlCode = controlCode,
                Allowed = false,
                Code = "PO_SOD_LINEAGE_INCOMPLETE",
                Message = missingMessage,
                ParticipantRoles = participantRoles
            };
        }

        var request = new ProcurementSodGuardRequest
        {
            ControlCode = controlCode,
            SourceType = SourceType,
            SourceReference = purchaseOrder.OrderNumber,
            ProhibitedActorUserIds = prohibitedActorUserIds.ToList()
        };
        var decision = enforce
            ? await _sodGuard.EnforceAsync(
                request,
                correlationId,
                cancellationToken)
            : await _sodGuard.CheckAsync(
                request,
                correlationId,
                cancellationToken);
        return new ProcurementPurchaseOrderSodCheckDto
        {
            Key = key,
            Label = label,
            Action = action,
            ControlCode = controlCode,
            Allowed = decision.Allowed,
            Code = decision.Code,
            Message = decision.Message,
            ParticipantRoles = participantRoles,
            ProhibitedActorUserIds = prohibitedActorUserIds,
            PolicySetId = decision.PolicySetId,
            PolicyCode = decision.PolicyCode,
            PolicyVersion = decision.PolicyVersion,
            RuleId = decision.RuleId,
            RuleCode = decision.RuleCode
        };
    }

    private async Task<Participants> ResolveParticipantsAsync(
        PurchaseOrder purchaseOrder,
        CancellationToken cancellationToken)
    {
        Guid? sourceRequester = null;
        if (purchaseOrder.SourceRequisitionId.HasValue)
        {
            sourceRequester = await _unitOfWork.Repository<PurchaseRequisition>()
                .GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId &&
                    item.Id == purchaseOrder.SourceRequisitionId.Value &&
                    !item.IsDeleted)
                .AsNoTracking()
                .Select(item => (Guid?)item.RequestedById)
                .SingleOrDefaultAsync(cancellationToken);
        }

        var frameworkCreator = await _unitOfWork
            .Repository<ProcurementFrameworkCallOff>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.PurchaseOrderId == purchaseOrder.Id &&
                !item.IsDeleted)
            .AsNoTracking()
            .Select(item => (Guid?)item.CreatedByUserId)
            .SingleOrDefaultAsync(cancellationToken);
        return new Participants(sourceRequester, frameworkCreator);
    }

    private async Task RecordAsync(
        ProcurementPurchaseOrderSodReadinessDto readiness,
        string action,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var isReceiptAction =
            ProcurementPurchaseOrderSodRules.IsReceiptAction(action);
        var check = isReceiptAction
            ? readiness.Checks.Single(item => item.Key == "receipt")
            : readiness.Checks.Single(item => item.Key == "approval");
        var allowed = isReceiptAction
            ? readiness.CanReceive
            : readiness.CanApprove;
        await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create(
                "po-sod",
                _currentUser.TenantId,
                readiness.PurchaseOrderId,
                action,
                _currentUser.UserId,
                correlationId),
            EventType = EventType,
            Action = action,
            Result = allowed
                ? ProcurementControlEventResult.Allowed
                : ProcurementControlEventResult.Denied,
            RuleCode = isReceiptAction ? "RCV-004" : "PO-004",
            RuleId = check.RuleId,
            RuleVersion = isReceiptAction ? "TDC-0503" : "TDC-0405",
            DecisionKeys = DecisionKeys.ToList(),
            SourceType = SourceType,
            SourceId = readiness.PurchaseOrderId,
            SourceReference = readiness.OrderNumber,
            Reason = check.Message,
            InputValues = new
            {
                readiness.Status,
                Action = action,
                check.ControlCode,
                check.ParticipantRoles,
                check.ProhibitedActorUserIds,
                readiness.CurrentActorUserId
            },
            ResultValues = new
            {
                Allowed = allowed,
                check.Code,
                readiness.CanApprove,
                readiness.CanReceive,
                check.PolicySetId,
                check.PolicyCode,
                check.PolicyVersion,
                check.RuleCode
            },
            CorrelationId = correlationId,
            CausationId = correlationId,
            OccurredAtUtc = readiness.EvaluatedAtUtc
        }, cancellationToken);
    }

    private async Task PublishAsync(
        ProcurementPurchaseOrderSodReadinessDto readiness,
        string action,
        CancellationToken cancellationToken)
    {
        var allowed = ProcurementPurchaseOrderSodRules.IsReceiptAction(action)
            ? readiness.CanReceive
            : readiness.CanApprove;
        try
        {
            await _notifications.PublishAsync(new NotificationTopicEvent
            {
                TenantId = _currentUser.TenantId,
                TopicKey = allowed
                    ? "procurement.purchase-order.sod-allowed"
                    : "procurement.purchase-order.sod-blocked",
                NotificationType = EventType,
                EntityType = SourceType,
                EntityId = readiness.PurchaseOrderId,
                TriggeredByUserId = _currentUser.UserId,
                Data = new Dictionary<string, object>
                {
                    ["orderNumber"] = readiness.OrderNumber,
                    ["action"] = action,
                    ["code"] = readiness.Code
                }
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to publish PO SOD notification for {PurchaseOrderId}",
                readiness.PurchaseOrderId);
        }
    }

    private async Task EnsureCapabilityAsync(
        string permission,
        string sourceReference,
        string correlationId,
        CancellationToken cancellationToken,
        Guid? warehouseId = null)
    {
        try
        {
            var decision = await _accessControl.EnforceCapabilityAsync(
                new ProcurementAccessCapabilityRequest
                {
                    PermissionCode = permission,
                    SourceType = EventType,
                    SourceReference = sourceReference,
                    WarehouseId = warehouseId
                },
                correlationId,
                cancellationToken);
            if (decision.Allowed)
                return;
            throw new ProcurementPurchaseOrderSodAuthorizationException(
                decision.Message);
        }
        catch (ProcurementAccessAuthorizationException exception)
        {
            throw new ProcurementPurchaseOrderSodAuthorizationException(
                exception.Message);
        }
        catch (ProcurementAccessValidationException exception)
        {
            throw new ProcurementPurchaseOrderSodAuthorizationException(
                exception.Message);
        }
    }

    private async Task<PurchaseOrder> LoadAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        await _unitOfWork.Repository<PurchaseOrder>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                item.Id == id &&
                !item.IsDeleted)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw new ProcurementPurchaseOrderSodNotFoundException(
            "PO_SOD_NOT_FOUND",
            "The purchase order was not found in the current tenant.");

    private void EnsurePurchaseOrder(PurchaseOrder purchaseOrder)
    {
        EnsureTenant();
        if (purchaseOrder.TenantId != _currentUser.TenantId ||
            purchaseOrder.IsDeleted)
        {
            throw new ProcurementPurchaseOrderSodAuthorizationException(
                "The purchase order is not available in the current tenant.");
        }
    }

    private void EnsureTenant()
    {
        if (!_currentUser.IsAuthenticated ||
            _currentUser.TenantId == Guid.Empty ||
            _currentUser.UserId == Guid.Empty)
        {
            throw new ProcurementPurchaseOrderSodAuthorizationException(
                "An authenticated tenant context is required.");
        }
        if (_currentUser.IsExternalUser)
        {
            throw new ProcurementPurchaseOrderSodAuthorizationException(
                "Supplier portal users cannot access purchase-order SOD controls.");
        }
    }

    private static string NormalizeCorrelation(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? Guid.NewGuid().ToString("N")
            : value.Trim();

    private static ProcurementPurchaseOrderSodCheckDto CopyCheck(
        ProcurementPurchaseOrderSodCheckDto source,
        bool allowed,
        string code,
        string message) => new()
    {
        Key = source.Key,
        Label = source.Label,
        Action = source.Action,
        ControlCode = source.ControlCode,
        Allowed = allowed,
        Code = code,
        Message = message,
        ParticipantRoles = source.ParticipantRoles,
        ProhibitedActorUserIds = source.ProhibitedActorUserIds,
        PolicySetId = source.PolicySetId,
        PolicyCode = source.PolicyCode,
        PolicyVersion = source.PolicyVersion,
        RuleId = source.RuleId,
        RuleCode = source.RuleCode
    };

    private static ProcurementPurchaseOrderSodReadinessDto CopyReadiness(
        ProcurementPurchaseOrderSodReadinessDto source,
        bool canApprove,
        string code,
        string message,
        IReadOnlyList<ProcurementPurchaseOrderSodCheckDto> checks) => new()
    {
        PurchaseOrderId = source.PurchaseOrderId,
        OrderNumber = source.OrderNumber,
        Status = source.Status,
        CurrentActorUserId = source.CurrentActorUserId,
        CanApprove = canApprove,
        CanReceive = source.CanReceive,
        Code = code,
        Message = message,
        EvaluatedAtUtc = source.EvaluatedAtUtc,
        DecisionKeys = source.DecisionKeys,
        ReceiptActionCoverage = source.ReceiptActionCoverage,
        Checks = checks
    };

    private sealed record Participants(
        Guid? SourceRequesterUserId,
        Guid? FrameworkCreatorUserId);
}
