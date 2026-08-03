using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Notifications;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
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

public sealed class InventoryReplenishmentService : IInventoryReplenishmentService
{
    private const string EntityType = "InventoryReplenishmentRecommendation";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] ClosedPurchaseOrderStatuses = ["Cancelled", "Canceled", "Rejected", "Received", "Closed"];
    private static readonly string[] OpenRequisitionStatuses = ["Draft", "Submitted", "Pending Approval", "Approved"];

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementAccessControlService _access;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly INotificationService _notifications;
    private readonly IWorkflowIntegrationService _workflow;
    private readonly IProcurementRequisitionLinkageService _linkage;
    private readonly IPurchaseRequisitionRepository _requisitions;
    private readonly IPurchaseRequisitionItemRepository _requisitionItems;
    private readonly ILogger<InventoryReplenishmentService> _logger;

    public InventoryReplenishmentService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService access,
        IProcurementControlEventService controlEvents,
        INotificationService notifications,
        IWorkflowIntegrationService workflow,
        IProcurementRequisitionLinkageService linkage,
        IPurchaseRequisitionRepository requisitions,
        IPurchaseRequisitionItemRepository requisitionItems,
        ILogger<InventoryReplenishmentService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _access = access;
        _controlEvents = controlEvents;
        _notifications = notifications;
        _workflow = workflow;
        _linkage = linkage;
        _requisitions = requisitions;
        _requisitionItems = requisitionItems;
        _logger = logger;
    }

    private IGenericRepository<InventoryReplenishmentRecommendation> Recommendations =>
        _unitOfWork.Repository<InventoryReplenishmentRecommendation>();

    public async Task<IReadOnlyList<InventoryReplenishmentRecommendationDto>> GetAsync(
        Guid? warehouseId,
        InventoryReplenishmentRecommendationStatus? status,
        int take,
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        take = Math.Clamp(take, 1, 500);
        var query = FullQuery().Where(value => value.TenantId == _currentUser.TenantId);
        if (warehouseId.HasValue) query = query.Where(value => value.WarehouseId == warehouseId.Value);
        if (status.HasValue) query = query.Where(value => value.Status == status.Value);
        var values = await query.AsNoTracking().OrderByDescending(value => value.GeneratedAtUtc)
            .ThenBy(value => value.RecommendationNumber).Take(take).ToListAsync(cancellationToken);
        var result = new List<InventoryReplenishmentRecommendationDto>(values.Count);
        foreach (var value in values) result.Add(await MapAsync(value, cancellationToken));
        return result;
    }

    public async Task<InventoryReplenishmentRecommendationDto> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        var value = await FullQuery().AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == id && item.TenantId == _currentUser.TenantId, cancellationToken)
            ?? throw new InventoryReplenishmentNotFoundException(
                "The replenishment recommendation was not found in the current tenant.");
        return await MapAsync(value, cancellationToken);
    }

    public Task<IReadOnlyList<InventoryReplenishmentRecommendationDto>> GenerateAsync(
        GenerateInventoryReplenishmentRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        if (request.WarehouseId == Guid.Empty || request.DemandWindowDays is < 7 or > 365)
            throw Error("INV_REPLENISHMENT_REQUEST_INVALID",
                "An active warehouse and a demand window between 7 and 365 days are required.");
        return ExecuteMutationAsync<IReadOnlyList<InventoryReplenishmentRecommendationDto>>(async () =>
        {
            var tenantId = _currentUser.TenantId;
            var key = Required(request.IdempotencyKey, 80, "Idempotency key");
            var correlation = Correlation(request.CorrelationId);
            await RequireCapabilityAsync("procurement.requisition.create", request.WarehouseId,
                "generate", correlation, cancellationToken);
            await _unitOfWork.AcquireTransactionLockAsync(
                $"inventory-replenishment:{tenantId:N}:{request.WarehouseId:N}", cancellationToken);

            var warehouse = await _unitOfWork.Repository<Warehouse>().GetQueryable(value =>
                    value.Id == request.WarehouseId && value.TenantId == tenantId && !value.IsDeleted && value.IsActive)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new InventoryReplenishmentNotFoundException(
                    "The active replenishment warehouse was not found in the current tenant.");
            var now = DateTime.UtcNow;
            var from = now.AddDays(-request.DemandWindowDays);
            var quantities = await _unitOfWork.Repository<WarehouseQuantity>().GetQueryable(value =>
                    value.TenantId == tenantId && value.WarehouseId == warehouse.Id && !value.IsDeleted &&
                    !value.InventoryItem.IsDeleted && value.InventoryItem.Status == ItemStatus.Active)
                .Include(value => value.InventoryItem).OrderBy(value => value.InventoryItem.ItemCode)
                .ToListAsync(cancellationToken);
            if (quantities.Count == 0) return Array.Empty<InventoryReplenishmentRecommendationDto>();

            var itemIds = quantities.Select(value => value.InventoryItemId).Distinct().ToList();
            var demandRows = await _unitOfWork.Repository<StockMovement>().GetQueryable(value =>
                    value.TenantId == tenantId && value.WarehouseId == warehouse.Id && itemIds.Contains(value.InventoryItemId) &&
                    value.MovementDate >= from && value.MovementDate <= now && !value.IsDeleted)
                .AsNoTracking().Select(value => new { value.InventoryItemId, value.MovementType, value.Quantity })
                .ToListAsync(cancellationToken);
            var demand = demandRows.Where(value => IsOutbound(value.MovementType))
                .GroupBy(value => value.InventoryItemId)
                .ToDictionary(group => group.Key, group => group.Sum(value => Math.Abs(value.Quantity)));

            var poRows = await _unitOfWork.Repository<PurchaseOrderItem>().GetQueryable(value =>
                    value.TenantId == tenantId && value.InventoryItemId.HasValue && itemIds.Contains(value.InventoryItemId.Value) &&
                    !value.IsDeleted && !value.PurchaseOrder.IsDeleted &&
                    !ClosedPurchaseOrderStatuses.Contains(value.PurchaseOrder.Status) &&
                    (value.WarehouseId == warehouse.Id ||
                     (value.WarehouseId == null && value.PurchaseOrder.DeliveryWarehouseId == warehouse.Id)))
                .AsNoTracking().Select(value => new
                {
                    ItemId = value.InventoryItemId!.Value,
                    Remaining = value.RemainingQuantity > 0
                        ? value.RemainingQuantity
                        : Math.Max(0, value.OrderedQuantity - value.ReceivedQuantity)
                }).ToListAsync(cancellationToken);
            var onOrder = poRows.GroupBy(value => value.ItemId)
                .ToDictionary(group => group.Key, group => group.Sum(value => value.Remaining));

            var prRows = await _unitOfWork.Repository<PurchaseRequisitionItem>().GetQueryable(value =>
                    value.TenantId == tenantId && value.InventoryItemId.HasValue && itemIds.Contains(value.InventoryItemId.Value) &&
                    !value.IsDeleted && !value.Requisition.IsDeleted &&
                    value.Requisition.DeliveryWarehouseId == warehouse.Id &&
                    value.Requisition.IsAutoGenerated && value.Requisition.GeneratedFrom != null &&
                    value.Requisition.GeneratedFrom.StartsWith("InventoryReplenishmentRecommendation:") &&
                    OpenRequisitionStatuses.Contains(value.Requisition.Status))
                .AsNoTracking().Select(value => new { ItemId = value.InventoryItemId!.Value, value.Quantity })
                .ToListAsync(cancellationToken);
            var openPr = prRows.GroupBy(value => value.ItemId)
                .ToDictionary(group => group.Key, group => group.Sum(value => value.Quantity));

            var suppliers = await _unitOfWork.Repository<ItemSupplier>().GetQueryable(value =>
                    value.TenantId == tenantId && itemIds.Contains(value.InventoryItemId) &&
                    !value.IsDeleted && value.IsActive)
                .AsNoTracking().OrderByDescending(value => value.IsPreferred).ThenBy(value => value.Priority)
                .ThenBy(value => value.Id).ToListAsync(cancellationToken);
            var preferred = suppliers.GroupBy(value => value.InventoryItemId)
                .ToDictionary(group => group.Key, group => group.First());
            var open = await Recommendations.GetQueryable(value => value.TenantId == tenantId &&
                    value.WarehouseId == warehouse.Id && itemIds.Contains(value.InventoryItemId) && !value.IsDeleted &&
                    (value.Status == InventoryReplenishmentRecommendationStatus.Draft ||
                     value.Status == InventoryReplenishmentRecommendationStatus.PendingApproval ||
                     value.Status == InventoryReplenishmentRecommendationStatus.Approved))
                .ToListAsync(cancellationToken);
            var openByItem = open.ToDictionary(value => value.InventoryItemId);
            var createdIds = new List<Guid>();

            foreach (var balance in quantities)
            {
                if (openByItem.TryGetValue(balance.InventoryItemId, out var existing))
                {
                    createdIds.Add(existing.Id);
                    continue;
                }
                preferred.TryGetValue(balance.InventoryItemId, out var supplier);
                var calculation = Calculate(balance, supplier,
                    demand.GetValueOrDefault(balance.InventoryItemId), request.DemandWindowDays,
                    onOrder.GetValueOrDefault(balance.InventoryItemId), openPr.GetValueOrDefault(balance.InventoryItemId),
                    from, now);
                if (calculation.RecommendedQuantity <= 0) continue;

                var itemKey = $"GEN-{Hash(new { key, warehouse.Id, balance.InventoryItemId })[..40]}";
                var payloadHash = Hash(calculation);
                var replay = await Recommendations.GetQueryable(value =>
                        value.TenantId == tenantId && value.IdempotencyKey == itemKey && !value.IsDeleted)
                    .SingleOrDefaultAsync(cancellationToken);
                if (replay is not null)
                {
                    if (!string.Equals(replay.PayloadHash, payloadHash, StringComparison.OrdinalIgnoreCase))
                        throw Error("INV_REPLENISHMENT_IDEMPOTENCY_CONFLICT",
                            "The generation idempotency key identifies a different calculation snapshot.");
                    createdIds.Add(replay.Id);
                    continue;
                }

                var id = Guid.NewGuid();
                var recommendation = new InventoryReplenishmentRecommendation
                {
                    Id = id,
                    TenantId = tenantId,
                    RecommendationNumber = $"IRR-{now:yyyyMMdd}-{id.ToString("N")[..8].ToUpperInvariant()}",
                    WarehouseQuantityId = balance.Id,
                    WarehouseId = warehouse.Id,
                    InventoryItemId = balance.InventoryItemId,
                    ItemSupplierId = supplier?.Id,
                    PreferredSupplierId = supplier?.SupplierId,
                    Status = InventoryReplenishmentRecommendationStatus.Draft,
                    DemandWindowDays = request.DemandWindowDays,
                    DemandFromUtc = from,
                    DemandToUtc = now,
                    DemandQuantity = calculation.DemandQuantity,
                    AverageDailyDemand = calculation.AverageDailyDemand,
                    LeadTimeDays = calculation.LeadTimeDays,
                    SafetyLeadTimeDays = calculation.SafetyLeadTimeDays,
                    CurrentStock = balance.CurrentStock,
                    AvailableStock = balance.AvailableStock,
                    AllocatedStock = balance.AllocatedStock,
                    OnOrderQuantity = calculation.OnOrderQuantity,
                    OpenRecommendationQuantity = calculation.OpenRequisitionQuantity,
                    MinimumLevel = calculation.MinimumLevel,
                    MaximumLevel = calculation.MaximumLevel,
                    ReorderLevel = calculation.ReorderLevel,
                    ReorderQuantity = calculation.ReorderQuantity,
                    SafetyStock = calculation.SafetyStock,
                    MinimumOrderQuantity = calculation.MinimumOrderQuantity,
                    OrderMultiple = calculation.OrderMultiple,
                    LeadTimeDemand = calculation.LeadTimeDemand,
                    ProjectedAvailableAtReceipt = calculation.ProjectedAvailableAtReceipt,
                    RecommendedQuantity = calculation.RecommendedQuantity,
                    EstimatedUnitCost = supplier?.UnitPrice > 0 ? supplier.UnitPrice :
                        balance.AverageCost > 0 ? balance.AverageCost : balance.InventoryItem.AverageCost,
                    RequiredDateUtc = now.AddDays(calculation.LeadTimeDays + calculation.SafetyLeadTimeDays),
                    ValidUntilUtc = now.AddDays(Math.Max(1, Math.Min(7, calculation.LeadTimeDays))),
                    Explanation = calculation.Explanation,
                    CalculationSnapshotJson = JsonSerializer.Serialize(calculation, JsonOptions),
                    CalculationHash = payloadHash,
                    GeneratedById = _currentUser.UserId,
                    GeneratedAtUtc = now,
                    IdempotencyKey = itemKey,
                    PayloadHash = payloadHash,
                    CorrelationId = correlation,
                    CreatedAt = now,
                    UpdatedAt = now,
                    CreatedById = _currentUser.UserId
                };
                await Recommendations.AddAsync(recommendation);
                await AddActionAsync(recommendation, InventoryReplenishmentActionType.Generated, null,
                    recommendation.Status, itemKey, payloadHash, correlation,
                    recommendation.Explanation, _currentUser.UserId, cancellationToken);
                await AddAuditAsync(recommendation, "Generated", calculation, correlation);
                await RecordEventAsync(recommendation, "Generate", recommendation.Explanation, correlation, cancellationToken);
                createdIds.Add(recommendation.Id);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            foreach (var id in createdIds.Distinct())
            {
                var recommendation = await FullQuery().SingleAsync(value => value.Id == id, cancellationToken);
                if (recommendation.AlertNotificationId.HasValue) continue;
                var notificationIds = await NotifyAsync(recommendation, cancellationToken);
                if (notificationIds.Count == 0) continue;
                recommendation.AlertNotificationId = notificationIds[0];
                recommendation.UpdatedAt = DateTime.UtcNow;
                await Recommendations.UpdateAsync(recommendation);
                var notificationHash = Hash(new { recommendation.Id, NotificationIds = notificationIds });
                await AddActionAsync(recommendation, InventoryReplenishmentActionType.AlertSent,
                    recommendation.Status, recommendation.Status, $"ALERT-{recommendation.Id:N}", notificationHash,
                    correlation, $"Central alert sent to {notificationIds.Count} responsible actor(s).",
                    _currentUser.UserId, cancellationToken);
            }
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            var result = new List<InventoryReplenishmentRecommendationDto>();
            foreach (var id in createdIds.Distinct()) result.Add(await LoadDtoAsync(id, cancellationToken));
            return result;
        }, cancellationToken);
    }

    public Task<InventoryReplenishmentRecommendationDto> SubmitAsync(
        Guid id,
        SubmitInventoryReplenishmentRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        return ExecuteMutationAsync(async () =>
        {
            var correlation = Correlation(request.CorrelationId);
            var key = Required(request.IdempotencyKey, 100, "Idempotency key");
            var reason = Required(request.Reason, 1000, "Submission reason");
            var payloadHash = Hash(new { id, reason });
            var value = await LoadForMutationAsync(id, request.RowVersion, cancellationToken);
            if (await ReplayActionAsync(value.Id, key, payloadHash, cancellationToken))
                return await LoadDtoAsync(value.Id, cancellationToken);
            if (value.Status != InventoryReplenishmentRecommendationStatus.Draft)
                throw Error("INV_REPLENISHMENT_STATUS_INVALID", "Only a Draft recommendation can be submitted.");
            EnsureCurrent(value);
            await RequireCapabilityAsync("procurement.requisition.create", value.WarehouseId,
                value.RecommendationNumber, correlation, cancellationToken);
            WorkflowIntegrationResult result;
            try { result = await _workflow.SubmitAsync(EntityType, value.Id); }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Replenishment workflow submission failed for {Recommendation}", value.Id);
                throw Error("INV_REPLENISHMENT_WORKFLOW_UNAVAILABLE",
                    "An active shared approval workflow is required before the recommendation can advance.");
            }
            if (!result.ExecutionResult.Success || !result.ExecutionResult.WorkflowInstanceId.HasValue)
                throw Error("INV_REPLENISHMENT_WORKFLOW_UNAVAILABLE",
                    result.ExecutionResult.Message ?? "The shared approval workflow could not be started.");
            if (result.Outcome == WorkflowOutcome.Approved)
                throw Error("INV_REPLENISHMENT_INDEPENDENT_APPROVAL_REQUIRED",
                    "The workflow completed without an independent approver; configure at least one approval step.");
            var previous = value.Status;
            value.Status = InventoryReplenishmentRecommendationStatus.PendingApproval;
            value.WorkflowInstanceId = result.ExecutionResult.WorkflowInstanceId;
            value.SubmittedById = _currentUser.UserId;
            value.SubmittedAtUtc = DateTime.UtcNow;
            value.UpdatedAt = DateTime.UtcNow;
            value.LastModifiedById = _currentUser.UserId;
            await Recommendations.UpdateAsync(value);
            await AddActionAsync(value, InventoryReplenishmentActionType.Submitted, previous, value.Status,
                key, payloadHash, correlation, reason, _currentUser.UserId, cancellationToken);
            await AddAuditAsync(value, "Submitted", new { value.WorkflowInstanceId, reason }, correlation);
            await RecordEventAsync(value, "Submit", reason, correlation, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return await LoadDtoAsync(value.Id, cancellationToken);
        }, cancellationToken);
    }

    public Task<InventoryReplenishmentRecommendationDto> DecideAsync(
        Guid id,
        DecideInventoryReplenishmentRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        return ExecuteMutationAsync(async () =>
        {
            var correlation = Correlation(request.CorrelationId);
            var key = Required(request.IdempotencyKey, 100, "Idempotency key");
            var comment = Required(request.Comment, 1000, "Decision comment");
            var payloadHash = Hash(new { id, request.Approved, comment });
            var value = await LoadForMutationAsync(id, request.RowVersion, cancellationToken);
            if (await ReplayActionAsync(value.Id, key, payloadHash, cancellationToken))
                return await LoadDtoAsync(value.Id, cancellationToken);
            if (value.Status != InventoryReplenishmentRecommendationStatus.PendingApproval ||
                !value.WorkflowInstanceId.HasValue)
                throw Error("INV_REPLENISHMENT_STATUS_INVALID",
                    "Only a recommendation pending shared-workflow approval can be decided.");
            if (value.GeneratedById == _currentUser.UserId || value.SubmittedById == _currentUser.UserId)
                throw Error("INV_REPLENISHMENT_SOD_VIOLATION",
                    "The generator or submitter cannot approve or reject the same recommendation.");
            await RequireCapabilityAsync("procurement.requisition.approve", value.WarehouseId,
                value.RecommendationNumber, correlation, cancellationToken);
            if (!await _workflow.CanUserApproveAsync(EntityType, value.Id, _currentUser.UserId))
                throw new InventoryReplenishmentAuthorizationException(
                    "The current user is not the assigned approver for this workflow step.");
            WorkflowIntegrationResult result;
            try
            {
                result = await _workflow.ProcessApprovalAsync(EntityType, value.Id, _currentUser.UserId,
                    request.Approved ? "Approve" : "Reject", comment);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Replenishment workflow decision failed for {Recommendation}", value.Id);
                throw Error("INV_REPLENISHMENT_WORKFLOW_DECISION_FAILED",
                    "The shared workflow could not record the decision.");
            }
            if (!result.ExecutionResult.Success)
                throw Error("INV_REPLENISHMENT_WORKFLOW_DECISION_FAILED",
                    result.ExecutionResult.Message ?? "The shared workflow could not record the decision.");
            if (!request.Approved && result.Outcome != WorkflowOutcome.Rejected)
                throw Error("INV_REPLENISHMENT_WORKFLOW_OUTCOME_INVALID",
                    "A rejection is terminal only when the shared workflow reports a rejected outcome.");
            var previous = value.Status;
            InventoryReplenishmentActionType action;
            if (result.Outcome == WorkflowOutcome.Approved)
            {
                value.Status = InventoryReplenishmentRecommendationStatus.Approved;
                action = InventoryReplenishmentActionType.Approved;
            }
            else if (result.Outcome == WorkflowOutcome.Rejected)
            {
                value.Status = InventoryReplenishmentRecommendationStatus.Rejected;
                action = InventoryReplenishmentActionType.Rejected;
            }
            else
            {
                value.Status = InventoryReplenishmentRecommendationStatus.PendingApproval;
                action = InventoryReplenishmentActionType.ApprovalProgressed;
            }
            value.DecidedById = result.Outcome == WorkflowOutcome.Pending ? null : _currentUser.UserId;
            value.DecidedAtUtc = result.Outcome == WorkflowOutcome.Pending ? null : DateTime.UtcNow;
            value.DecisionComment = comment;
            value.UpdatedAt = DateTime.UtcNow;
            value.LastModifiedById = _currentUser.UserId;
            await Recommendations.UpdateAsync(value);
            await AddActionAsync(value, action, previous, value.Status, key, payloadHash,
                correlation, comment, _currentUser.UserId, cancellationToken);
            await AddAuditAsync(value, action.ToString(), new { result.Outcome, comment }, correlation);
            await RecordEventAsync(value, action.ToString(), comment, correlation, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return await LoadDtoAsync(value.Id, cancellationToken);
        }, cancellationToken);
    }

    public Task<InventoryReplenishmentRecommendationDto> ConvertToPurchaseRequisitionAsync(
        Guid id,
        ConvertInventoryReplenishmentRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        return ExecuteMutationAsync(async () =>
        {
            var correlation = Correlation(request.CorrelationId);
            var key = Required(request.IdempotencyKey, 100, "Idempotency key");
            var justification = Required(request.Justification, 2000, "Purchase justification");
            var payloadHash = Hash(new { id, request.Department, request.CostCenter, justification });
            var value = await LoadForMutationAsync(id, request.RowVersion, cancellationToken);
            if (await ReplayActionAsync(value.Id, key, payloadHash, cancellationToken))
                return await LoadDtoAsync(value.Id, cancellationToken);
            if (value.Status != InventoryReplenishmentRecommendationStatus.Approved)
                throw Error("INV_REPLENISHMENT_STATUS_INVALID",
                    "Only an independently approved recommendation can create a purchase requisition.");
            EnsureCurrent(value);
            await RequireCapabilityAsync("procurement.requisition.create", value.WarehouseId,
                value.RecommendationNumber, correlation, cancellationToken);
            await _unitOfWork.AcquireTransactionLockAsync(
                $"inventory-replenishment-convert:{value.TenantId:N}:{value.WarehouseId:N}:{value.InventoryItemId:N}",
                cancellationToken);

            var current = await _unitOfWork.Repository<WarehouseQuantity>().GetQueryable(item =>
                    item.Id == value.WarehouseQuantityId && item.TenantId == value.TenantId && !item.IsDeleted)
                .SingleAsync(cancellationToken);
            var currentOnOrder = await CurrentOnOrderAsync(value, cancellationToken);
            var currentOpenPr = await CurrentOpenRequisitionAsync(value, cancellationToken);
            var oldPosition = value.AvailableStock + value.OnOrderQuantity + value.OpenRecommendationQuantity;
            var newPosition = current.AvailableStock + currentOnOrder + currentOpenPr;
            var reducedNeed = value.RecommendedQuantity - Math.Max(0, newPosition - oldPosition);
            var quantity = RoundUp(Math.Min(value.RecommendedQuantity, Math.Max(0, reducedNeed)), value.OrderMultiple);
            if (quantity <= 0 || newPosition > value.ReorderLevel && newPosition >= value.MinimumLevel &&
                newPosition >= value.LeadTimeDemand + value.SafetyStock)
                throw Error("INV_REPLENISHMENT_REVALIDATION_REQUIRED",
                    "Current stock and open supply no longer support the approved quantity; regenerate the recommendation.");

            var number = await _requisitions.GenerateRequisitionNumberAsync();
            var now = DateTime.UtcNow;
            var requisition = new PurchaseRequisition
            {
                Id = Guid.NewGuid(),
                TenantId = value.TenantId,
                RequisitionNumber = number,
                RequisitionDate = now,
                RequestedById = _currentUser.UserId,
                RequiredDate = value.RequiredDateUtc,
                Status = "Draft",
                Priority = value.ProjectedAvailableAtReceipt < value.SafetyStock ? "High" : "Normal",
                Department = Optional(request.Department, 100) ?? "Stores",
                CostCenter = Optional(request.CostCenter, 100),
                Justification = justification,
                Notes = $"Generated from approved {value.RecommendationNumber}; calculation {value.CalculationHash}.",
                RequisitionType = PurchaseRequisitionType.StockReplenishment,
                DeliveryWarehouseId = value.WarehouseId,
                IsAutoGenerated = true,
                GeneratedFrom = $"InventoryReplenishmentRecommendation:{value.Id:N}",
                PreferredBusinessPartnerId = value.PreferredSupplierId,
                Currency = value.ItemSupplier?.Currency ?? "GHS",
                TotalAmount = quantity * value.EstimatedUnitCost,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedById = _currentUser.UserId
            };
            await _linkage.PrepareAsync(requisition, new SavePurchaseRequisitionLinkageRequest
            {
                ProcurementCategory = ProcurementCategoryClass.Goods,
                CostCenter = requisition.CostCenter,
                RequisitionType = PurchaseRequisitionType.StockReplenishment
            }, correlation, cancellationToken);
            await _requisitions.CreateRequisitionAsync(requisition);
            await _requisitionItems.CreateItemAsync(new PurchaseRequisitionItem
            {
                Id = Guid.NewGuid(),
                TenantId = value.TenantId,
                RequisitionId = requisition.Id,
                InventoryItemId = value.InventoryItemId,
                ItemDescription = value.InventoryItem.Name,
                Quantity = quantity,
                UnitOfMeasure = value.InventoryItem.UnitOfMeasure,
                EstimatedUnitPrice = value.EstimatedUnitCost,
                LineTotal = quantity * value.EstimatedUnitCost,
                RequiredDate = value.RequiredDateUtc,
                PreferredBusinessPartnerId = value.PreferredSupplierId,
                Notes = $"Approved replenishment {value.RecommendationNumber}; demand window {value.DemandWindowDays} days.",
                Specifications = $"Calculation hash: {value.CalculationHash}",
                Status = "Pending",
                CreatedAt = now,
                UpdatedAt = now,
                CreatedById = _currentUser.UserId
            });
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _linkage.RecordMutationAsync(requisition, "Created", null, correlation,
                $"Draft Stock Replenishment PR generated from approved {value.RecommendationNumber}.", cancellationToken);

            var previous = value.Status;
            value.Status = InventoryReplenishmentRecommendationStatus.ConvertedToRequisition;
            value.PurchaseRequisitionId = requisition.Id;
            value.PurchaseRequisitionNumber = requisition.RequisitionNumber;
            value.ConvertedAtUtc = now;
            value.UpdatedAt = now;
            value.LastModifiedById = _currentUser.UserId;
            await Recommendations.UpdateAsync(value);
            await AddActionAsync(value, InventoryReplenishmentActionType.RequisitionCreated, previous,
                value.Status, key, payloadHash, correlation,
                $"Created Draft {requisition.RequisitionNumber} for {quantity} {value.InventoryItem.UnitOfMeasure}; existing PR submission controls remain mandatory.",
                _currentUser.UserId, cancellationToken);
            await AddAuditAsync(value, "RequisitionCreated",
                new { requisition.Id, requisition.RequisitionNumber, quantity, newPosition }, correlation);
            await RecordEventAsync(value, "CreateRequisition",
                $"Created governed Draft {requisition.RequisitionNumber}; no submission or approval was bypassed.",
                correlation, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return await LoadDtoAsync(value.Id, cancellationToken);
        }, cancellationToken);
    }

    private IQueryable<InventoryReplenishmentRecommendation> FullQuery() =>
        Recommendations.GetQueryable(value => !value.IsDeleted)
            .Include(value => value.Warehouse)
            .Include(value => value.WarehouseQuantity)
            .Include(value => value.InventoryItem)
            .Include(value => value.ItemSupplier)
            .Include(value => value.GeneratedBy)
            .Include(value => value.SubmittedBy)
            .Include(value => value.DecidedBy)
            .Include(value => value.Actions.OrderBy(action => action.Sequence))
                .ThenInclude(action => action.ActorUser);

    private async Task<InventoryReplenishmentRecommendation> LoadForMutationAsync(
        Guid id,
        string rowVersion,
        CancellationToken cancellationToken)
    {
        await _unitOfWork.AcquireTransactionLockAsync(
            $"inventory-replenishment-row:{_currentUser.TenantId:N}:{id:N}", cancellationToken);
        var value = await FullQuery().SingleOrDefaultAsync(item =>
            item.Id == id && item.TenantId == _currentUser.TenantId, cancellationToken)
            ?? throw new InventoryReplenishmentNotFoundException(
                "The replenishment recommendation was not found in the current tenant.");
        EnsureRowVersion(value.RowVersion, rowVersion);
        return value;
    }

    private async Task<InventoryReplenishmentRecommendationDto> LoadDtoAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var value = await FullQuery().AsNoTracking().SingleAsync(item =>
            item.Id == id && item.TenantId == _currentUser.TenantId, cancellationToken);
        return await MapAsync(value, cancellationToken);
    }

    private async Task<InventoryReplenishmentRecommendationDto> MapAsync(
        InventoryReplenishmentRecommendation value,
        CancellationToken cancellationToken)
    {
        string? supplierName = null;
        if (value.PreferredSupplierId.HasValue)
            supplierName = await _unitOfWork.Repository<BusinessPartner>().GetQueryable(item =>
                    item.Id == value.PreferredSupplierId.Value && item.TenantId == value.TenantId && !item.IsDeleted)
                .AsNoTracking().Select(item => item.PartnerName).SingleOrDefaultAsync(cancellationToken);
        return new InventoryReplenishmentRecommendationDto
        {
            Id = value.Id,
            RecommendationNumber = value.RecommendationNumber,
            WarehouseId = value.WarehouseId,
            WarehouseCode = value.Warehouse.Code,
            WarehouseName = value.Warehouse.Name,
            InventoryItemId = value.InventoryItemId,
            ItemCode = value.InventoryItem.ItemCode,
            ItemName = value.InventoryItem.Name,
            UnitOfMeasure = value.InventoryItem.UnitOfMeasure,
            PreferredSupplierId = value.PreferredSupplierId,
            PreferredSupplierName = supplierName,
            Status = value.Status,
            DemandWindowDays = value.DemandWindowDays,
            DemandFromUtc = value.DemandFromUtc,
            DemandToUtc = value.DemandToUtc,
            DemandQuantity = value.DemandQuantity,
            AverageDailyDemand = value.AverageDailyDemand,
            LeadTimeDays = value.LeadTimeDays,
            SafetyLeadTimeDays = value.SafetyLeadTimeDays,
            CurrentStock = value.CurrentStock,
            AvailableStock = value.AvailableStock,
            AllocatedStock = value.AllocatedStock,
            OnOrderQuantity = value.OnOrderQuantity,
            OpenRecommendationQuantity = value.OpenRecommendationQuantity,
            MinimumLevel = value.MinimumLevel,
            MaximumLevel = value.MaximumLevel,
            ReorderLevel = value.ReorderLevel,
            ReorderQuantity = value.ReorderQuantity,
            SafetyStock = value.SafetyStock,
            MinimumOrderQuantity = value.MinimumOrderQuantity,
            OrderMultiple = value.OrderMultiple,
            LeadTimeDemand = value.LeadTimeDemand,
            ProjectedAvailableAtReceipt = value.ProjectedAvailableAtReceipt,
            RecommendedQuantity = value.RecommendedQuantity,
            EstimatedUnitCost = value.EstimatedUnitCost,
            RequiredDateUtc = value.RequiredDateUtc,
            ValidUntilUtc = value.ValidUntilUtc,
            Explanation = value.Explanation,
            CalculationHash = value.CalculationHash,
            GeneratedById = value.GeneratedById,
            GeneratedByName = UserName(value.GeneratedBy),
            GeneratedAtUtc = value.GeneratedAtUtc,
            WorkflowInstanceId = value.WorkflowInstanceId,
            PurchaseRequisitionId = value.PurchaseRequisitionId,
            PurchaseRequisitionNumber = value.PurchaseRequisitionNumber,
            RowVersion = Convert.ToBase64String(value.RowVersion),
            Actions = value.Actions.OrderBy(action => action.Sequence).Select(action =>
                new InventoryReplenishmentActionDto
                {
                    Sequence = action.Sequence,
                    ActionType = action.ActionType,
                    PreviousStatus = action.PreviousStatus,
                    NewStatus = action.NewStatus,
                    ActorUserId = action.ActorUserId,
                    ActorName = UserName(action.ActorUser),
                    OccurredAtUtc = action.OccurredAtUtc,
                    Reason = action.Reason,
                    IntegrityHash = action.IntegrityHash
                }).ToList()
        };
    }

    private async Task<List<Guid>> NotifyAsync(
        InventoryReplenishmentRecommendation recommendation,
        CancellationToken cancellationToken)
    {
        var recipients = await _unitOfWork.Repository<ProcurementResponsibilityAssignment>()
            .GetQueryable(value => value.TenantId == recommendation.TenantId && !value.IsDeleted && value.IsActive &&
                value.EffectiveFrom <= DateTime.UtcNow && (value.EffectiveTo == null || value.EffectiveTo >= DateTime.UtcNow) &&
                (value.WarehouseScopeMode == ProcurementWarehouseScopeMode.All ||
                 value.Warehouses.Any(link => !link.IsDeleted && link.WarehouseId == recommendation.WarehouseId)))
            .AsNoTracking().Select(value => value.UserId).Distinct().Take(25).ToListAsync(cancellationToken);
        recipients.Add(_currentUser.UserId);
        var result = new List<Guid>();
        foreach (var recipient in recipients.Distinct())
        {
            var created = await _notifications.CreateNotificationAsync(new CreateNotificationDto
            {
                RecipientId = recipient,
                Type = "InventoryReplenishmentRequired",
                Title = $"Replenishment required: {recommendation.InventoryItem.ItemCode}",
                Message = recommendation.Explanation,
                Priority = recommendation.ProjectedAvailableAtReceipt < recommendation.SafetyStock ? "High" : "Normal",
                EntityType = EntityType,
                EntityId = recommendation.Id,
                ActionUrl = "/inventory/replenishment",
                Metadata = new Dictionary<string, object>
                {
                    ["warehouseId"] = recommendation.WarehouseId,
                    ["inventoryItemId"] = recommendation.InventoryItemId,
                    ["recommendedQuantity"] = recommendation.RecommendedQuantity,
                    ["calculationHash"] = recommendation.CalculationHash
                }
            }, _currentUser.UserId, recommendation.TenantId);
            result.Add(created.Id);
        }
        return result;
    }

    private async Task AddActionAsync(
        InventoryReplenishmentRecommendation recommendation,
        InventoryReplenishmentActionType actionType,
        InventoryReplenishmentRecommendationStatus? previousStatus,
        InventoryReplenishmentRecommendationStatus newStatus,
        string key,
        string payloadHash,
        string correlation,
        string? reason,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        var last = await _unitOfWork.Repository<InventoryReplenishmentAction>().GetQueryable(value =>
                value.TenantId == recommendation.TenantId && value.RecommendationId == recommendation.Id && !value.IsDeleted)
            .OrderByDescending(value => value.Sequence).FirstOrDefaultAsync(cancellationToken);
        var action = new InventoryReplenishmentAction
        {
            TenantId = recommendation.TenantId,
            RecommendationId = recommendation.Id,
            Sequence = (last?.Sequence ?? 0) + 1,
            ActionType = actionType,
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            ActorUserId = actorId,
            OccurredAtUtc = DateTime.UtcNow,
            IdempotencyKey = Required(key, 100, "Action idempotency key"),
            PayloadHash = payloadHash,
            CorrelationId = correlation,
            Reason = Optional(reason, 1000),
            PreviousHash = last?.IntegrityHash,
            CreatedAt = DateTime.UtcNow,
            CreatedById = actorId
        };
        action.IntegrityHash = Hash(new { action.RecommendationId, action.Sequence, action.ActionType,
            action.PreviousStatus, action.NewStatus, action.ActorUserId, action.OccurredAtUtc,
            action.PayloadHash, action.CorrelationId, action.Reason, action.PreviousHash });
        await _unitOfWork.Repository<InventoryReplenishmentAction>().AddAsync(action);
    }

    private async Task<bool> ReplayActionAsync(
        Guid recommendationId,
        string key,
        string payloadHash,
        CancellationToken cancellationToken)
    {
        var action = await _unitOfWork.Repository<InventoryReplenishmentAction>().GetQueryable(value =>
                value.RecommendationId == recommendationId && value.IdempotencyKey == key && !value.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (action is null) return false;
        if (!string.Equals(action.PayloadHash, payloadHash, StringComparison.OrdinalIgnoreCase))
            throw Error("INV_REPLENISHMENT_IDEMPOTENCY_CONFLICT",
                "The idempotency key already identifies a different recommendation action.");
        return true;
    }

    private async Task RequireCapabilityAsync(
        string permission,
        Guid warehouseId,
        string reference,
        string correlation,
        CancellationToken cancellationToken)
    {
        var result = await _access.EnforceCapabilityAsync(new ProcurementAccessCapabilityRequest
        {
            PermissionCode = permission,
            WarehouseId = warehouseId,
            RequireLocationScope = false,
            SourceType = EntityType,
            SourceReference = reference
        }, correlation, cancellationToken);
        if (!result.Allowed) throw new InventoryReplenishmentAuthorizationException(result.Message);
    }

    private async Task RecordEventAsync(
        InventoryReplenishmentRecommendation value,
        string action,
        string reason,
        string correlation,
        CancellationToken cancellationToken) =>
        await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create("inventory-replenishment", value.TenantId,
                value.Id, action, value.Status, value.CalculationHash),
            EventType = EntityType,
            Action = action,
            Result = ProcurementControlEventResult.Allowed,
            RuleCode = "TDC-0612",
            RuleVersion = "1",
            DecisionKeys = Enumerable.Range(1, 14).Select(number => $"DEC-{number:000}").ToList(),
            SourceType = "WarehouseQuantity",
            SourceId = value.WarehouseQuantityId,
            SourceReference = value.RecommendationNumber,
            Reason = reason,
            InputValues = new { value.WarehouseId, value.InventoryItemId, value.DemandWindowDays,
                value.AvailableStock, value.OnOrderQuantity, value.OpenRecommendationQuantity,
                value.ReorderLevel, value.SafetyStock, value.LeadTimeDays },
            ResultValues = new { value.Status, value.RecommendedQuantity, value.CalculationHash,
                value.WorkflowInstanceId, value.PurchaseRequisitionId },
            CorrelationId = correlation,
            OccurredAtUtc = DateTime.UtcNow
        }, cancellationToken);

    private async Task AddAuditAsync(
        InventoryReplenishmentRecommendation value,
        string action,
        object payload,
        string correlation) =>
        await _unitOfWork.Repository<AuditLog>().AddAsync(new AuditLog
        {
            TenantId = value.TenantId,
            UserId = _currentUser.UserId,
            Username = string.IsNullOrWhiteSpace(_currentUser.Username) ? "Inventory replenishment" : _currentUser.Username,
            Action = $"InventoryReplenishment.{action}",
            Resource = EntityType,
            ResourceId = value.Id.ToString(),
            NewValues = JsonSerializer.Serialize(payload, JsonOptions),
            IpAddress = "system",
            UserAgent = correlation,
            Timestamp = DateTime.UtcNow
        });

    private async Task<decimal> CurrentOnOrderAsync(
        InventoryReplenishmentRecommendation value,
        CancellationToken cancellationToken) =>
        (await _unitOfWork.Repository<PurchaseOrderItem>().GetQueryable(line =>
                line.TenantId == value.TenantId && line.InventoryItemId == value.InventoryItemId && !line.IsDeleted &&
                !line.PurchaseOrder.IsDeleted && !ClosedPurchaseOrderStatuses.Contains(line.PurchaseOrder.Status) &&
                (line.WarehouseId == value.WarehouseId ||
                 (line.WarehouseId == null && line.PurchaseOrder.DeliveryWarehouseId == value.WarehouseId)))
            .AsNoTracking().Select(line => line.RemainingQuantity > 0
                ? line.RemainingQuantity : Math.Max(0, line.OrderedQuantity - line.ReceivedQuantity))
            .ToListAsync(cancellationToken)).Sum();

    private async Task<decimal> CurrentOpenRequisitionAsync(
        InventoryReplenishmentRecommendation value,
        CancellationToken cancellationToken) =>
        (await _unitOfWork.Repository<PurchaseRequisitionItem>().GetQueryable(line =>
                line.TenantId == value.TenantId && line.InventoryItemId == value.InventoryItemId && !line.IsDeleted &&
                !line.Requisition.IsDeleted && line.Requisition.DeliveryWarehouseId == value.WarehouseId &&
                line.Requisition.IsAutoGenerated && line.Requisition.GeneratedFrom != null &&
                line.Requisition.GeneratedFrom.StartsWith("InventoryReplenishmentRecommendation:") &&
                OpenRequisitionStatuses.Contains(line.Requisition.Status))
            .AsNoTracking().Select(line => line.Quantity).ToListAsync(cancellationToken)).Sum();

    private static Calculation Calculate(
        WarehouseQuantity balance,
        ItemSupplier? supplier,
        decimal demandQuantity,
        int demandWindowDays,
        decimal onOrder,
        decimal openRequisition,
        DateTime from,
        DateTime to)
    {
        var item = balance.InventoryItem;
        var averageDaily = demandWindowDays <= 0 ? 0 : demandQuantity / demandWindowDays;
        var leadTime = supplier?.LeadTimeDays > 0 ? supplier.LeadTimeDays : Math.Max(0, item.LeadTimeDays);
        var safetyLead = Math.Max(0, item.SafetyLeadTimeDays);
        var leadDemand = averageDaily * (leadTime + safetyLead);
        var minimum = Math.Max(0, item.MinimumLevel);
        var safety = Math.Max(0, item.SafetyStock);
        var reorder = new[] { balance.ReorderLevel, item.ReorderLevel, minimum, leadDemand + safety }.Max();
        var maximum = new[] { balance.MaxStock, item.MaximumLevel, reorder }.Max();
        var reorderQuantity = Math.Max(0, item.ReorderQuantity);
        var minimumOrder = Math.Max(1, supplier?.MinimumOrderQuantity ?? 1);
        var multiple = Math.Max(1, supplier?.OrderMultiple ?? 1);
        var position = balance.AvailableStock + onOrder + openRequisition;
        var projected = position - leadDemand;
        if (position > reorder || (reorder <= 0 && demandQuantity <= 0))
            return new Calculation(demandWindowDays, from, to, demandQuantity, averageDaily, leadTime,
                safetyLead, minimum, maximum, reorder, reorderQuantity, safety, minimumOrder, multiple,
                onOrder, openRequisition, leadDemand, projected, 0,
                $"No order: inventory position {position:0.####} is above reorder point {reorder:0.####}.");
        var demandTarget = leadDemand + safety + reorderQuantity;
        var target = Math.Max(maximum, Math.Max(reorder, demandTarget));
        var required = Math.Max(0, target - position);
        var recommended = RoundUp(Math.Max(required, Math.Max(reorderQuantity, minimumOrder)), multiple);
        var explanation =
            $"Order {recommended:0.####}: available {balance.AvailableStock:0.####} + on-order {onOrder:0.####} + open replenishment PR {openRequisition:0.####} = position {position:0.####}; " +
            $"{demandQuantity:0.####} outbound demand over {demandWindowDays} days ({averageDaily:0.####}/day) gives lead/safety demand {leadDemand:0.####}; " +
            $"reorder point {reorder:0.####}, safety stock {safety:0.####}, target {target:0.####}, MOQ {minimumOrder:0.####}, multiple {multiple:0.####}.";
        return new Calculation(demandWindowDays, from, to, demandQuantity, averageDaily, leadTime,
            safetyLead, minimum, maximum, reorder, reorderQuantity, safety, minimumOrder, multiple,
            onOrder, openRequisition, leadDemand, projected, recommended, explanation);
    }

    private async Task<T> ExecuteMutationAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        if (_unitOfWork.HasActiveTransaction) return await action();
        try
        {
            return await _unitOfWork.ExecuteInStrategyAsync(async () =>
            {
                await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                try
                {
                    var result = await action();
                    await _unitOfWork.CommitAsync(cancellationToken);
                    return result;
                }
                catch
                {
                    if (_unitOfWork.HasActiveTransaction) await _unitOfWork.RollbackAsync(cancellationToken);
                    _unitOfWork.ClearTrackedChanges();
                    throw;
                }
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            throw Error("INV_REPLENISHMENT_CONCURRENCY_CONFLICT",
                "The recommendation changed after it was loaded. Refresh and retry.");
        }
    }

    private void EnsureActor()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty || _currentUser.TenantId == Guid.Empty)
            throw new InventoryReplenishmentAuthorizationException("An authenticated tenant actor is required.");
    }

    private static void EnsureCurrent(InventoryReplenishmentRecommendation value)
    {
        if (value.ValidUntilUtc <= DateTime.UtcNow)
            throw Error("INV_REPLENISHMENT_EXPIRED",
                "The calculation has expired and must be regenerated from current stock and demand.");
    }

    private static void EnsureRowVersion(byte[] actual, string supplied)
    {
        byte[] expected;
        try { expected = Convert.FromBase64String(supplied ?? string.Empty); }
        catch (FormatException) { throw Error("INV_REPLENISHMENT_ROW_VERSION_INVALID", "A valid row version is required."); }
        if (expected.Length == 0 || !actual.SequenceEqual(expected))
            throw Error("INV_REPLENISHMENT_CONCURRENCY_CONFLICT",
                "The recommendation changed after it was loaded. Refresh and retry.");
    }

    private static bool IsOutbound(string value)
    {
        var normalized = value.Trim().Replace("_", "-").ToUpperInvariant();
        return normalized.Contains("ISSUE") || normalized.Contains("SALE") ||
               normalized.Contains("CONSUMPTION") || normalized.Contains("WASTE") ||
               normalized.Contains("TRANSFER-OUT") || normalized.Contains("ADJUSTMENT-");
    }

    private static decimal RoundUp(decimal quantity, decimal multiple) =>
        quantity <= 0 ? 0 : Math.Ceiling(quantity / Math.Max(1, multiple)) * Math.Max(1, multiple);
    private static InventoryReplenishmentControlException Error(string code, string message) => new(code, message);
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions))));
    private static string Required(string? value, int max, string label) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= max
            ? value.Trim()
            : throw Error("INV_REPLENISHMENT_VALUE_REQUIRED", $"{label} is required and must not exceed {max} characters.");
    private static string? Optional(string? value, int max) => string.IsNullOrWhiteSpace(value)
        ? null : value.Trim().Length <= max ? value.Trim()
        : throw Error("INV_REPLENISHMENT_VALUE_TOO_LONG", $"The value must not exceed {max} characters.");
    private static string Correlation(string? value) => string.IsNullOrWhiteSpace(value)
        ? Guid.NewGuid().ToString("N") : value.Trim().Length <= 100 ? value.Trim() : value.Trim()[..100];
    private static string UserName(ApplicationUser? value) => value is null ? "Unknown user" :
        string.IsNullOrWhiteSpace(value.FirstName + value.LastName) ? value.UserName ?? value.Id.ToString() :
        $"{value.FirstName} {value.LastName}".Trim();

    private sealed record Calculation(
        int DemandWindowDays,
        DateTime DemandFromUtc,
        DateTime DemandToUtc,
        decimal DemandQuantity,
        decimal AverageDailyDemand,
        int LeadTimeDays,
        int SafetyLeadTimeDays,
        decimal MinimumLevel,
        decimal MaximumLevel,
        decimal ReorderLevel,
        decimal ReorderQuantity,
        decimal SafetyStock,
        decimal MinimumOrderQuantity,
        decimal OrderMultiple,
        decimal OnOrderQuantity,
        decimal OpenRequisitionQuantity,
        decimal LeadTimeDemand,
        decimal ProjectedAvailableAtReceipt,
        decimal RecommendedQuantity,
        string Explanation);
}
