using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Inventory;

/// <summary>
/// The single Maintenance adapter over InventoryAllocation. It owns every stock mutation for
/// work-order parts while the Maintenance module remains the owner of the work-order aggregate.
/// </summary>
public sealed class InventoryWorkOrderReservationService : IInventoryWorkOrderReservationService
{
    private const string AllocationType = "WorkOrder";
    private const string Active = "Active";
    private const string Partial = "PartiallyConsumed";
    private const string Consumed = "Consumed";
    private const string Cancelled = "Cancelled";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IUnitOfWork unitOfWork;
    private readonly ICurrentUserProvider currentUser;
    private readonly IProcurementAccessControlService access;
    private readonly IProcurementControlEventService controlEvents;
    private readonly IInventoryNegativeStockControlService negativeStockControls;

    public InventoryWorkOrderReservationService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService access,
        IProcurementControlEventService controlEvents,
        IInventoryNegativeStockControlService negativeStockControls)
    {
        this.unitOfWork = unitOfWork;
        this.currentUser = currentUser;
        this.access = access;
        this.controlEvents = controlEvents;
        this.negativeStockControls = negativeStockControls;
    }

    private IQueryable<InventoryAllocation> Allocations =>
        unitOfWork.Repository<InventoryAllocation>().GetQueryable(value =>
            value.TenantId == currentUser.TenantId && value.AllocationType == AllocationType && !value.IsDeleted);

    public Task<WorkOrderPartDto> CreateAndReserveAsync(
        CreateWorkOrderPartDto request,
        string idempotencyKey,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        return ExecuteMutationAsync(async () =>
        {
            if (request.WorkOrderId == Guid.Empty || request.InventoryItemId == Guid.Empty ||
                request.WarehouseId == Guid.Empty || !request.WarehouseLocationId.HasValue ||
                request.WarehouseLocationId == Guid.Empty || request.QuantityRequired <= 0)
                throw Error("INV_WORK_ORDER_RESERVATION_REQUEST_INVALID",
                    "Work order, inventory item, warehouse, exact warehouse location and positive quantity are required.");

            var key = Required(idempotencyKey, 100, "Idempotency key");
            var correlation = Correlation(correlationId);
            var payloadHash = Hash(new
            {
                request.WorkOrderId,
                request.InventoryItemId,
                request.WarehouseId,
                request.WarehouseLocationId,
                request.QuantityRequired,
                request.UnitCost,
                SerialNumber = Optional(request.SerialNumber, 100),
                LotNumber = Optional(request.LotNumber, 100),
                Notes = Optional(request.Notes, 1000)
            });
            var replay = await Allocations.AsNoTracking().SingleOrDefaultAsync(value =>
                value.IdempotencyKey == key, cancellationToken);
            if (replay is not null)
            {
                if (!string.Equals(replay.PayloadHash, payloadHash, StringComparison.OrdinalIgnoreCase))
                    throw Error("INV_WORK_ORDER_RESERVATION_IDEMPOTENCY_CONFLICT",
                        "The idempotency key already identifies a different work-order reservation.");
                return await LoadPartDtoByAllocationAsync(replay.Id, cancellationToken);
            }

            await unitOfWork.AcquireTransactionLockAsync(
                $"inventory-work-order-part:create:{currentUser.TenantId:N}:{request.WorkOrderId:N}:{key}", cancellationToken);
            var workOrder = await unitOfWork.Repository<WorkOrder>().GetQueryable(value =>
                    value.Id == request.WorkOrderId && value.TenantId == currentUser.TenantId && !value.IsDeleted)
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
                ?? throw new InventoryWorkOrderReservationNotFoundException(
                    "The work order was not found in the current tenant.");
            if (workOrder.Status is "Completed" or "Cancelled" or "Closed")
                throw Error("INV_WORK_ORDER_RESERVATION_WORK_ORDER_TERMINAL",
                    "Parts cannot be reserved for a completed, cancelled or closed work order.");

            var scope = await LoadStockScopeAsync(request.InventoryItemId, request.WarehouseId,
                request.WarehouseLocationId.Value, request.WorkOrderId, request.QuantityRequired, request.NegativeStockOverrideId,
                workOrder.WorkOrderNumber, correlation, cancellationToken);
            var now = DateTime.UtcNow;
            var part = new WorkOrderPart
            {
                Id = Guid.NewGuid(), TenantId = currentUser.TenantId, WorkOrderId = workOrder.Id,
                InventoryItemId = scope.Item.Id, ItemCode = scope.Item.ItemCode, ItemName = scope.Item.Name,
                Description = scope.Item.Description, QuantityRequired = request.QuantityRequired,
                QuantityAllocated = request.QuantityRequired, UnitCost = request.UnitCost,
                TotalCost = request.QuantityRequired * request.UnitCost, WarehouseLocationId = scope.Location.Id,
                SerialNumber = Optional(request.SerialNumber, 100), LotNumber = Optional(request.LotNumber, 100),
                Notes = Optional(request.Notes, 1000), Status = "Allocated", AllocatedAt = now,
                CreatedAt = now, CreatedById = currentUser.UserId
            };
            var allocation = new InventoryAllocation
            {
                Id = Guid.NewGuid(), TenantId = currentUser.TenantId, InventoryItemId = scope.Item.Id,
                WarehouseId = scope.Warehouse.Id, LocationId = scope.Location.Id, AllocationType = AllocationType,
                ReferenceNumber = workOrder.WorkOrderNumber, ReferenceId = workOrder.Id,
                AllocatedQuantity = request.QuantityRequired, RemainingQuantity = request.QuantityRequired,
                AllocationDate = now, RequiredDate = Utc(workOrder.RequestedStartDate), Status = Active,
                SerialNumber = part.SerialNumber, LotNumber = part.LotNumber, Notes = part.Notes,
                AllocatedById = currentUser.UserId, IdempotencyKey = key, PayloadHash = payloadHash,
                CorrelationId = correlation, CreatedAt = now, CreatedById = currentUser.UserId
            };
            part.AllocationId = allocation.Id;
            await unitOfWork.Repository<InventoryAllocation>().AddAsync(allocation);
            await unitOfWork.Repository<WorkOrderPart>().AddAsync(part);
            await ApplyReserveDeltaAsync(scope, request.QuantityRequired, cancellationToken);
            await AddMovementAsync(scope, allocation, "Allocation", request.QuantityRequired, cancellationToken);
            await AddActionAsync(allocation, part, "Reserve", null, Active, request.QuantityRequired,
                null, allocation.RequiredDate, key, payloadHash, correlation, "Work-order part reserved.", cancellationToken);
            await AddAuditAndEventAsync(allocation, part, "Reserve", request.QuantityRequired,
                "Work-order part reserved.", correlation, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await negativeStockControls.ClearMutationContextAsync(cancellationToken);
            return Map(part, allocation, scope.Warehouse, scope.Location, scope.Item);
        }, cancellationToken);
    }

    public Task<WorkOrderPartDto> UpdateAsync(Guid partId, UpdateWorkOrderPartDto request,
        string idempotencyKey, string correlationId, CancellationToken cancellationToken = default)
    {
        EnsureActor();
        return ExecuteMutationAsync(async () =>
        {
            var key = Required(idempotencyKey, 100, "Idempotency key");
            var correlation = Correlation(correlationId);
            var (part, allocation) = await LoadForMutationAsync(partId, cancellationToken);
            await RequireCapabilityAsync(allocation, correlation, cancellationToken);
            var payloadHash = Hash(new { partId, request.QuantityRequired, request.QuantityUsed,
                request.QuantityReturned, request.UnitCost, request.WarehouseLocationId,
                SerialNumber = Optional(request.SerialNumber, 100), LotNumber = Optional(request.LotNumber, 100),
                Notes = Optional(request.Notes, 1000) });
            if (await IsReplayAsync(allocation.Id, key, payloadHash, cancellationToken))
                return await LoadPartDtoAsync(part.Id, cancellationToken);
            if (request.QuantityRequired <= 0 || request.QuantityUsed < part.QuantityUsed ||
                request.QuantityReturned < part.QuantityReturned ||
                request.QuantityUsed + request.QuantityReturned > request.QuantityRequired)
                throw Error("INV_WORK_ORDER_RESERVATION_QUANTITY_INVALID",
                    "Required quantity must remain positive; used or returned quantity cannot decrease or exceed the required quantity.");

            var targetLocationId = request.WarehouseLocationId ?? allocation.LocationId
                ?? throw Error("INV_WORK_ORDER_RESERVATION_LOCATION_REQUIRED", "An exact warehouse location is required.");
            var oldRequired = part.QuantityRequired;
            var oldRemaining = allocation.RemainingQuantity;
            var newRemaining = request.QuantityRequired - request.QuantityUsed - request.QuantityReturned;
            var consumeDelta = request.QuantityUsed - part.QuantityUsed;
            var reservedBeforeConsumption = newRemaining + consumeDelta;
            var oldScope = await LoadStockScopeAsync(part.InventoryItemId, allocation.WarehouseId,
                allocation.LocationId!.Value, allocation.ReferenceId!.Value, 0, null, allocation.ReferenceNumber ?? "work-order", correlation,
                cancellationToken, false);
            var targetScope = targetLocationId == allocation.LocationId
                ? oldScope
                : await LoadStockScopeAsync(part.InventoryItemId, allocation.WarehouseId, targetLocationId,
                    allocation.ReferenceId!.Value, reservedBeforeConsumption, request.NegativeStockOverrideId, allocation.ReferenceNumber ?? "work-order",
                    correlation, cancellationToken);

            if (targetLocationId != allocation.LocationId)
            {
                await ApplyReserveDeltaAsync(oldScope, -oldRemaining, cancellationToken);
                await ApplyReserveDeltaAsync(targetScope, reservedBeforeConsumption, cancellationToken);
                allocation.LocationId = targetLocationId;
                part.WarehouseLocationId = targetLocationId;
            }
            else
            {
                var reserveDelta = reservedBeforeConsumption - oldRemaining;
                if (reserveDelta > 0)
                    await EnsureAvailableAsync(targetScope, reserveDelta, request.NegativeStockOverrideId,
                        allocation.ReferenceId!.Value, allocation.ReferenceNumber ?? "work-order", correlation, part.Id, cancellationToken);
                await ApplyReserveDeltaAsync(targetScope, reserveDelta, cancellationToken);
            }

            if (consumeDelta > 0)
                await ApplyConsumptionAsync(targetScope, allocation, consumeDelta, request.NegativeStockOverrideId,
                    correlation, part.Id, reservedBeforeConsumption, cancellationToken);
            var previousStatus = allocation.Status;
            allocation.AllocatedQuantity = request.QuantityRequired;
            allocation.ConsumedQuantity = request.QuantityUsed;
            allocation.RemainingQuantity = newRemaining;
            allocation.Status = newRemaining == 0 ? (request.QuantityUsed > 0 ? Consumed : Cancelled)
                : request.QuantityUsed > 0 ? Partial : Active;
            allocation.SerialNumber = Optional(request.SerialNumber, 100);
            allocation.LotNumber = Optional(request.LotNumber, 100);
            allocation.Notes = Optional(request.Notes, 1000);
            allocation.UpdatedAt = DateTime.UtcNow;
            allocation.LastModifiedById = currentUser.UserId;
            part.QuantityRequired = request.QuantityRequired;
            part.QuantityAllocated = request.QuantityRequired;
            part.QuantityUsed = request.QuantityUsed;
            part.QuantityReturned = request.QuantityReturned;
            part.UnitCost = request.UnitCost;
            part.TotalCost = request.QuantityRequired * request.UnitCost;
            part.SerialNumber = allocation.SerialNumber;
            part.LotNumber = allocation.LotNumber;
            part.Notes = allocation.Notes;
            part.Status = PartStatus(allocation, part);
            part.UsedAt = request.QuantityUsed > 0 ? DateTime.UtcNow : part.UsedAt;
            part.UpdatedAt = DateTime.UtcNow;
            part.LastModifiedById = currentUser.UserId;
            await unitOfWork.Repository<InventoryAllocation>().UpdateAsync(allocation);
            await unitOfWork.Repository<WorkOrderPart>().UpdateAsync(part);
            await AddActionAsync(allocation, part, "Update", previousStatus, allocation.Status,
                Math.Abs(request.QuantityRequired - oldRequired) + consumeDelta, null, allocation.RequiredDate,
                key, payloadHash, correlation, "Work-order reservation and consumption synchronized.", cancellationToken);
            await AddAuditAndEventAsync(allocation, part, "Update", consumeDelta,
                "Work-order reservation and consumption synchronized.", correlation, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await negativeStockControls.ClearMutationContextAsync(cancellationToken);
            return Map(part, allocation, targetScope.Warehouse, targetScope.Location, targetScope.Item);
        }, cancellationToken);
    }

    public Task DeleteAndReleaseAsync(Guid partId, string reason, string idempotencyKey, string correlationId,
        CancellationToken cancellationToken = default) => ReleaseAsync(partId, reason, idempotencyKey,
        correlationId, deletePart: true, cancellationToken);

    public async Task<WorkOrderPartDto> ReturnUnusedAsync(Guid partId, string idempotencyKey,
        string correlationId, CancellationToken cancellationToken = default)
    {
        await ReleaseAsync(partId, "Unused work-order parts returned.", idempotencyKey, correlationId,
            deletePart: false, cancellationToken);
        return await LoadPartDtoAsync(partId, cancellationToken);
    }

    public Task<WorkOrderPartDto> RetryAsync(Guid partId, string idempotencyKey, string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        return ExecuteMutationAsync(async () =>
        {
            var part = await unitOfWork.Repository<WorkOrderPart>().GetQueryable(value =>
                    value.Id == partId && value.TenantId == currentUser.TenantId && !value.IsDeleted)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new InventoryWorkOrderReservationNotFoundException("The work-order part was not found.");
            if (part.AllocationId.HasValue)
                return await LoadPartDtoAsync(partId, cancellationToken);
            if (!part.WarehouseLocationId.HasValue)
                throw Error("INV_WORK_ORDER_RESERVATION_RETRY_LOCATION_REQUIRED",
                    "Select an exact warehouse location before retrying this reservation.");
            var location = await unitOfWork.Repository<WarehouseLocation>().GetQueryable(value =>
                    value.Id == part.WarehouseLocationId && value.TenantId == currentUser.TenantId &&
                    !value.IsDeleted && value.IsActive)
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
                ?? throw new InventoryWorkOrderReservationNotFoundException("The reservation location was not found.");
            return await CreateAllocationForExistingPartAsync(part, location.InventoryWarehouseId, location.Id,
                null, Required(idempotencyKey, 100, "Idempotency key"), Correlation(correlationId), cancellationToken);
        }, cancellationToken);
    }

    public Task<InventoryWorkOrderReservationResultDto> ReserveForScheduleAsync(Guid workOrderId,
        DateTime? requiredDate, string idempotencyKey, string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        return ExecuteMutationAsync(async () =>
        {
            var parts = await unitOfWork.Repository<WorkOrderPart>().GetQueryable(value =>
                    value.WorkOrderId == workOrderId && value.TenantId == currentUser.TenantId && !value.IsDeleted)
                .OrderBy(value => value.Id).ToListAsync(cancellationToken);
            var affected = 0;
            foreach (var part in parts.Where(value => !value.AllocationId.HasValue))
            {
                if (!part.WarehouseLocationId.HasValue)
                    throw Error("INV_WORK_ORDER_RESERVATION_SCHEDULE_LOCATION_REQUIRED",
                        $"Select an exact warehouse location for part {part.ItemCode} before scheduling.");
                var location = await unitOfWork.Repository<WarehouseLocation>().GetQueryable(value =>
                        value.Id == part.WarehouseLocationId && value.TenantId == currentUser.TenantId &&
                        !value.IsDeleted && value.IsActive)
                    .AsNoTracking().SingleAsync(cancellationToken);
                await CreateAllocationForExistingPartAsync(part, location.InventoryWarehouseId, location.Id,
                    requiredDate, $"{Required(idempotencyKey, 80, "Idempotency key")}:{part.Id:N}",
                    Correlation(correlationId), cancellationToken);
                affected++;
            }
            return new InventoryWorkOrderReservationResultDto
            { WorkOrderId = workOrderId, AffectedParts = affected, RequiredDate = Utc(requiredDate) };
        }, cancellationToken);
    }

    public Task<InventoryWorkOrderReservationResultDto> RescheduleAsync(Guid workOrderId, DateTime? requiredDate,
        string reason, string idempotencyKey, string correlationId, CancellationToken cancellationToken = default)
    {
        EnsureActor();
        return ExecuteMutationAsync(async () =>
        {
            var key = Required(idempotencyKey, 60, "Idempotency key");
            var correlation = Correlation(correlationId);
            var allocations = await Allocations.Where(value => value.ReferenceId == workOrderId &&
                    (value.Status == Active || value.Status == Partial))
                .OrderBy(value => value.Id).ToListAsync(cancellationToken);
            var affected = 0;
            foreach (var allocation in allocations)
            {
                var part = await unitOfWork.Repository<WorkOrderPart>().GetQueryable(value =>
                        value.AllocationId == allocation.Id && value.TenantId == currentUser.TenantId && !value.IsDeleted)
                    .SingleAsync(cancellationToken);
                await RequireCapabilityAsync(allocation, correlation, cancellationToken);
                var actionKey = $"{key}:{part.Id:N}";
                var payloadHash = Hash(new { workOrderId, part.Id, RequiredDate = Utc(requiredDate), Reason = Required(reason, 1000, "Reason") });
                if (await IsReplayAsync(allocation.Id, actionKey, payloadHash, cancellationToken)) continue;
                var previousDate = allocation.RequiredDate;
                allocation.RequiredDate = Utc(requiredDate);
                allocation.UpdatedAt = DateTime.UtcNow;
                allocation.LastModifiedById = currentUser.UserId;
                await unitOfWork.Repository<InventoryAllocation>().UpdateAsync(allocation);
                await AddActionAsync(allocation, part, "Reschedule", allocation.Status, allocation.Status, 0,
                    previousDate, allocation.RequiredDate, actionKey, payloadHash, correlation,
                    Required(reason, 1000, "Reason"), cancellationToken);
                await AddAuditAndEventAsync(allocation, part, "Reschedule", 0, reason, correlation, cancellationToken);
                affected++;
            }
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return new InventoryWorkOrderReservationResultDto
            { WorkOrderId = workOrderId, AffectedParts = affected, RequiredDate = Utc(requiredDate) };
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<InventoryWorkOrderReservationActionDto>> GetActionsAsync(Guid partId,
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        var part = await unitOfWork.Repository<WorkOrderPart>().GetQueryable(value =>
                value.Id == partId && value.TenantId == currentUser.TenantId && !value.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw new InventoryWorkOrderReservationNotFoundException("The work-order part was not found.");
        if (!part.AllocationId.HasValue) return [];
        var allocation = await Allocations.AsNoTracking().SingleAsync(value => value.Id == part.AllocationId, cancellationToken);
        var decision = await access.CheckCapabilityAsync(AccessRequest("procurement.inventory.read", allocation),
            $"work-order-reservation-read:{part.Id:N}", cancellationToken);
        if (!decision.Allowed) throw new InventoryWorkOrderReservationAuthorizationException(decision.Message);
        return await unitOfWork.Repository<InventoryWorkOrderReservationAction>().GetQueryable(value =>
                value.TenantId == currentUser.TenantId && value.WorkOrderPartId == part.Id && !value.IsDeleted)
            .OrderBy(value => value.Sequence).AsNoTracking().Select(value => new InventoryWorkOrderReservationActionDto
            {
                Id = value.Id, InventoryAllocationId = value.InventoryAllocationId,
                WorkOrderPartId = value.WorkOrderPartId, Sequence = value.Sequence,
                ActionType = value.ActionType, PreviousStatus = value.PreviousStatus,
                NewStatus = value.NewStatus, Quantity = value.Quantity,
                PreviousRequiredDate = value.PreviousRequiredDate, NewRequiredDate = value.NewRequiredDate,
                Reason = value.Reason, OccurredAtUtc = value.OccurredAtUtc, CorrelationId = value.CorrelationId
            }).ToListAsync(cancellationToken);
    }

    private Task ReleaseAsync(Guid partId, string reason, string idempotencyKey, string correlationId,
        bool deletePart, CancellationToken cancellationToken)
    {
        EnsureActor();
        return ExecuteMutationAsync(async () =>
        {
            var key = Required(idempotencyKey, 100, "Idempotency key");
            var correlation = Correlation(correlationId);
            var payloadHash = Hash(new { partId, deletePart, Reason = Required(reason, 1000, "Reason") });
            var prior = await unitOfWork.Repository<InventoryWorkOrderReservationAction>().GetQueryable(value =>
                    value.TenantId == currentUser.TenantId && value.WorkOrderPartId == partId &&
                    value.IdempotencyKey == key && !value.IsDeleted)
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            if (prior is not null)
            {
                if (!string.Equals(prior.PayloadHash, payloadHash, StringComparison.OrdinalIgnoreCase))
                    throw Error("INV_WORK_ORDER_RESERVATION_IDEMPOTENCY_CONFLICT",
                        "The idempotency key already identifies another reservation action.");
                return true;
            }
            var (part, allocation) = await LoadForMutationAsync(partId, cancellationToken);
            await RequireCapabilityAsync(allocation, correlation, cancellationToken);
            if (await IsReplayAsync(allocation.Id, key, payloadHash, cancellationToken)) return true;
            if (allocation.Status is not (Active or Partial))
                throw Error("INV_WORK_ORDER_RESERVATION_TERMINAL", "Only an active reservation can be released.");
            var scope = await LoadStockScopeAsync(part.InventoryItemId, allocation.WarehouseId,
                allocation.LocationId!.Value, allocation.ReferenceId!.Value, 0, null, allocation.ReferenceNumber ?? "work-order", correlation,
                cancellationToken, false);
            var quantity = allocation.RemainingQuantity;
            await ApplyReserveDeltaAsync(scope, -quantity, cancellationToken);
            var previousStatus = allocation.Status;
            allocation.RemainingQuantity = 0;
            allocation.Status = Cancelled;
            allocation.UpdatedAt = DateTime.UtcNow;
            allocation.LastModifiedById = currentUser.UserId;
            part.QuantityReturned += quantity;
            part.Status = deletePart ? "Cancelled" : "Returned";
            part.UpdatedAt = DateTime.UtcNow;
            part.LastModifiedById = currentUser.UserId;
            await unitOfWork.Repository<InventoryAllocation>().UpdateAsync(allocation);
            await AddActionAsync(allocation, part, deletePart ? "DeleteRelease" : "Return", previousStatus,
                Cancelled, quantity, allocation.RequiredDate, allocation.RequiredDate, key, payloadHash,
                correlation, reason, cancellationToken);
            await AddAuditAndEventAsync(allocation, part, deletePart ? "DeleteRelease" : "Return",
                quantity, reason, correlation, cancellationToken);
            if (deletePart)
            {
                part.IsDeleted = true;
                part.DeletedAt = DateTime.UtcNow;
                part.DeletedBy = currentUser.Username;
            }
            await unitOfWork.Repository<WorkOrderPart>().UpdateAsync(part);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }, cancellationToken);
    }

    private async Task<WorkOrderPartDto> CreateAllocationForExistingPartAsync(WorkOrderPart part,
        Guid warehouseId, Guid locationId, DateTime? requiredDate, string key, string correlation,
        CancellationToken cancellationToken)
    {
        var payloadHash = Hash(new { part.Id, warehouseId, locationId, part.QuantityRequired, RequiredDate = Utc(requiredDate) });
        var replay = await Allocations.AsNoTracking().SingleOrDefaultAsync(value => value.IdempotencyKey == key,
            cancellationToken);
        if (replay is not null)
        {
            if (!string.Equals(replay.PayloadHash, payloadHash, StringComparison.OrdinalIgnoreCase))
                throw Error("INV_WORK_ORDER_RESERVATION_IDEMPOTENCY_CONFLICT", "The retry key identifies another payload.");
            return await LoadPartDtoByAllocationAsync(replay.Id, cancellationToken);
        }
        var workOrder = await unitOfWork.Repository<WorkOrder>().GetQueryable(value =>
                value.Id == part.WorkOrderId && value.TenantId == currentUser.TenantId && !value.IsDeleted)
            .AsNoTracking().SingleAsync(cancellationToken);
        var scope = await LoadStockScopeAsync(part.InventoryItemId, warehouseId, locationId,
            workOrder.Id, part.QuantityRequired, null, workOrder.WorkOrderNumber, correlation, cancellationToken);
        var allocation = new InventoryAllocation
        {
            Id = Guid.NewGuid(), TenantId = currentUser.TenantId, InventoryItemId = part.InventoryItemId,
            WarehouseId = warehouseId, LocationId = locationId, AllocationType = AllocationType,
            ReferenceNumber = workOrder.WorkOrderNumber, ReferenceId = workOrder.Id,
            AllocatedQuantity = part.QuantityRequired, RemainingQuantity = part.QuantityRequired,
            AllocationDate = DateTime.UtcNow, RequiredDate = Utc(requiredDate ?? workOrder.RequestedStartDate),
            Status = Active, AllocatedById = currentUser.UserId, IdempotencyKey = key,
            PayloadHash = payloadHash, CorrelationId = correlation, CreatedById = currentUser.UserId
        };
        part.AllocationId = allocation.Id;
        part.QuantityAllocated = part.QuantityRequired;
        part.Status = "Allocated";
        part.AllocatedAt = DateTime.UtcNow;
        part.UpdatedAt = DateTime.UtcNow;
        await unitOfWork.Repository<InventoryAllocation>().AddAsync(allocation);
        await unitOfWork.Repository<WorkOrderPart>().UpdateAsync(part);
        await ApplyReserveDeltaAsync(scope, part.QuantityRequired, cancellationToken);
        await AddMovementAsync(scope, allocation, "Allocation", part.QuantityRequired, cancellationToken);
        await AddActionAsync(allocation, part, "RetryReserve", null, Active, part.QuantityRequired,
            null, allocation.RequiredDate, key, payloadHash, correlation, "Reservation created or retried.", cancellationToken);
        await AddAuditAndEventAsync(allocation, part, "RetryReserve", part.QuantityRequired,
            "Reservation created or retried.", correlation, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(part, allocation, scope.Warehouse, scope.Location, scope.Item);
    }

    private async Task<(WorkOrderPart Part, InventoryAllocation Allocation)> LoadForMutationAsync(Guid partId,
        CancellationToken cancellationToken)
    {
        await unitOfWork.AcquireTransactionLockAsync(
            $"inventory-work-order-part:{currentUser.TenantId:N}:{partId:N}", cancellationToken);
        var part = await unitOfWork.Repository<WorkOrderPart>().GetQueryable(value =>
                value.Id == partId && value.TenantId == currentUser.TenantId && !value.IsDeleted)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InventoryWorkOrderReservationNotFoundException("The work-order part was not found.");
        if (!part.AllocationId.HasValue)
            throw Error("INV_WORK_ORDER_RESERVATION_MISSING", "The work-order part has no inventory reservation. Retry it first.");
        var allocation = await Allocations.SingleOrDefaultAsync(value => value.Id == part.AllocationId.Value,
            cancellationToken)
            ?? throw Error("INV_WORK_ORDER_RESERVATION_LINEAGE_BROKEN",
                "The work-order part reservation could not be found in the current tenant.");
        if (allocation.ReferenceId != part.WorkOrderId || allocation.InventoryItemId != part.InventoryItemId)
            throw Error("INV_WORK_ORDER_RESERVATION_LINEAGE_BROKEN",
                "The work-order part and inventory reservation lineage do not match.");
        return (part, allocation);
    }

    private async Task<StockScope> LoadStockScopeAsync(Guid inventoryItemId, Guid warehouseId, Guid locationId,
        Guid referenceId, decimal requiredAvailable, Guid? overrideId, string reference, string correlation,
        CancellationToken cancellationToken, bool enforceCapability = true)
    {
        if (enforceCapability)
        {
            var decision = await access.EnforceCapabilityAsync(new ProcurementAccessCapabilityRequest
            {
                PermissionCode = "procurement.inventory.issue", WarehouseId = warehouseId,
                LocationId = locationId, RequireLocationScope = true,
                SourceType = "MaintenanceWorkOrder", SourceReference = reference
            }, correlation, cancellationToken);
            if (!decision.Allowed) throw new InventoryWorkOrderReservationAuthorizationException(decision.Message);
        }
        await unitOfWork.AcquireTransactionLockAsync(
            $"inventory-stock:{currentUser.TenantId:N}:{warehouseId:N}:{inventoryItemId:N}", cancellationToken);
        var location = await unitOfWork.Repository<WarehouseLocation>().GetQueryable(value =>
                value.Id == locationId && value.TenantId == currentUser.TenantId && !value.IsDeleted && value.IsActive &&
                (value.WarehouseId == warehouseId || (value.IsConsignmentBin && value.ConsignmentWarehouseId == warehouseId)))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InventoryWorkOrderReservationNotFoundException(
                "The active warehouse location was not found in the selected warehouse.");
        var warehouse = await unitOfWork.Repository<Warehouse>().GetQueryable(value =>
                value.Id == warehouseId && value.TenantId == currentUser.TenantId && !value.IsDeleted && value.IsActive)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InventoryWorkOrderReservationNotFoundException("The active warehouse was not found.");
        var warehouseQuantity = await unitOfWork.Repository<WarehouseQuantity>().GetQueryable(value =>
                value.TenantId == currentUser.TenantId && value.WarehouseId == warehouseId &&
                value.InventoryItemId == inventoryItemId && !value.IsDeleted)
            .Include(value => value.InventoryItem).SingleOrDefaultAsync(cancellationToken)
            ?? throw Error("INV_WORK_ORDER_RESERVATION_WAREHOUSE_STOCK_MISSING",
                "No warehouse stock balance exists for the selected item.");
        var inventoryLocation = await unitOfWork.Repository<InventoryLocation>().GetQueryable(value =>
                value.TenantId == currentUser.TenantId && value.LocationId == locationId &&
                value.InventoryItemId == inventoryItemId && !value.IsDeleted)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw Error("INV_WORK_ORDER_RESERVATION_LOCATION_STOCK_MISSING",
                "No location stock balance exists for the selected item.");
        var item = warehouseQuantity.InventoryItem;
        if (item.TenantId != currentUser.TenantId || item.IsDeleted || item.Status != ItemStatus.Active)
            throw new InventoryWorkOrderReservationNotFoundException(
                "The active inventory item was not found in the current tenant.");
        var scope = new StockScope(item, warehouse, location, warehouseQuantity, inventoryLocation);
        if (requiredAvailable > 0)
            await EnsureAvailableAsync(scope, requiredAvailable, overrideId, referenceId, reference, correlation, null, cancellationToken);
        return scope;
    }

    private async Task EnsureAvailableAsync(StockScope scope, decimal quantity, Guid? overrideId,
        Guid referenceId, string reference, string correlation, Guid? partId, CancellationToken cancellationToken)
    {
        var authorization = await negativeStockControls.PrepareDecreaseAsync(new InventoryStockDecreaseRequest
        {
            InventoryItemId = scope.Item.Id, WarehouseId = scope.Warehouse.Id, LocationId = scope.Location.Id,
            Quantity = quantity, ReferenceType = "MaintenanceWorkOrderPartReservation",
            ReferenceNumber = reference, ReferenceId = referenceId, ReferenceLineId = partId,
            NegativeStockOverrideId = overrideId, DecreaseCurrentStock = false,
            CheckInventoryItemBalance = !scope.Warehouse.IsConsignmentWarehouse, CorrelationId = correlation
        }, cancellationToken);
        if (!authorization.EmergencyOverrideApplied &&
            (scope.WarehouseQuantity.AvailableStock < quantity || scope.InventoryLocation.AvailableQuantity < quantity ||
             (!scope.Warehouse.IsConsignmentWarehouse && scope.Item.AvailableStock < quantity)))
            throw Error("INV_WORK_ORDER_RESERVATION_STOCK_INSUFFICIENT",
                "The exact warehouse/location balance cannot satisfy the work-order reservation.");
    }

    private async Task ApplyReserveDeltaAsync(StockScope scope, decimal delta, CancellationToken cancellationToken)
    {
        scope.WarehouseQuantity.AllocatedStock += delta;
        scope.WarehouseQuantity.AvailableStock = scope.WarehouseQuantity.CurrentStock - scope.WarehouseQuantity.AllocatedStock;
        scope.InventoryLocation.AllocatedQuantity += delta;
        scope.InventoryLocation.AvailableQuantity = scope.InventoryLocation.Quantity - scope.InventoryLocation.AllocatedQuantity;
        if (scope.WarehouseQuantity.AllocatedStock < 0 || scope.InventoryLocation.AllocatedQuantity < 0)
            throw Error("INV_WORK_ORDER_RESERVATION_ALLOCATION_UNDERFLOW",
                "The operation would release more stock than this work order reserved.");
        if (!scope.Warehouse.IsConsignmentWarehouse)
        {
            scope.Item.AllocatedStock += delta;
            scope.Item.AvailableStock = scope.Item.CurrentStock - scope.Item.AllocatedStock;
            if (scope.Item.AllocatedStock < 0)
                throw Error("INV_WORK_ORDER_RESERVATION_ITEM_UNDERFLOW",
                    "The operation would release more item stock than is allocated.");
            await unitOfWork.Repository<InventoryItem>().UpdateAsync(scope.Item);
        }
        scope.WarehouseQuantity.LastMovementDate = DateTime.UtcNow;
        scope.InventoryLocation.LastMovementDate = DateTime.UtcNow;
        await unitOfWork.Repository<WarehouseQuantity>().UpdateAsync(scope.WarehouseQuantity);
        await unitOfWork.Repository<InventoryLocation>().UpdateAsync(scope.InventoryLocation);
    }

    private async Task ApplyConsumptionAsync(StockScope scope, InventoryAllocation allocation, decimal quantity,
        Guid? overrideId, string correlation, Guid partId, decimal availableReservation,
        CancellationToken cancellationToken)
    {
        if (quantity > availableReservation)
            throw Error("INV_WORK_ORDER_RESERVATION_CONSUMPTION_EXCEEDED",
                "Consumption cannot exceed the remaining reserved quantity.");
        await negativeStockControls.PrepareDecreaseAsync(new InventoryStockDecreaseRequest
        {
            InventoryItemId = scope.Item.Id, WarehouseId = scope.Warehouse.Id, LocationId = scope.Location.Id,
            Quantity = quantity, ReferenceType = "MaintenanceWorkOrderPartConsumption",
            ReferenceNumber = allocation.ReferenceNumber ?? allocation.Id.ToString("N"),
            ReferenceId = allocation.ReferenceId!.Value, ReferenceLineId = partId, NegativeStockOverrideId = overrideId,
            DecreaseAvailableStock = false, CheckInventoryItemBalance = !scope.Warehouse.IsConsignmentWarehouse,
            CorrelationId = correlation
        }, cancellationToken);
        scope.WarehouseQuantity.CurrentStock -= quantity;
        scope.WarehouseQuantity.AllocatedStock -= quantity;
        scope.WarehouseQuantity.AvailableStock = scope.WarehouseQuantity.CurrentStock - scope.WarehouseQuantity.AllocatedStock;
        scope.InventoryLocation.Quantity -= quantity;
        scope.InventoryLocation.AllocatedQuantity -= quantity;
        scope.InventoryLocation.AvailableQuantity = scope.InventoryLocation.Quantity - scope.InventoryLocation.AllocatedQuantity;
        if (!scope.Warehouse.IsConsignmentWarehouse)
        {
            scope.Item.CurrentStock -= quantity;
            scope.Item.AllocatedStock -= quantity;
            scope.Item.AvailableStock = scope.Item.CurrentStock - scope.Item.AllocatedStock;
            await unitOfWork.Repository<InventoryItem>().UpdateAsync(scope.Item);
        }
        await unitOfWork.Repository<WarehouseQuantity>().UpdateAsync(scope.WarehouseQuantity);
        await unitOfWork.Repository<InventoryLocation>().UpdateAsync(scope.InventoryLocation);
        await AddMovementAsync(scope, allocation, "Consumption", -quantity, cancellationToken);
    }

    private async Task AddMovementAsync(StockScope scope, InventoryAllocation allocation, string type,
        decimal quantity, CancellationToken cancellationToken)
    {
        await unitOfWork.Repository<StockMovement>().AddAsync(new StockMovement
        {
            TenantId = currentUser.TenantId, InventoryItemId = scope.Item.Id,
            WarehouseId = scope.Warehouse.Id, LocationId = scope.Location.Id, MovementType = type,
            Quantity = quantity, UnitCost = scope.Item.AverageCost, TotalValue = scope.Item.AverageCost * quantity,
            MovementDate = DateTime.UtcNow, ReferenceType = ReferenceType.WO,
            ReferenceNumber = allocation.ReferenceNumber, ReferenceId = allocation.ReferenceId,
            Notes = $"{type} for maintenance work-order reservation {allocation.Id:N}.",
            ProcessedById = currentUser.UserId, RunningBalance = scope.Item.CurrentStock,
            CreatedById = currentUser.UserId
        });
    }

    private async Task AddActionAsync(InventoryAllocation allocation, WorkOrderPart part, string actionType,
        string? previousStatus, string newStatus, decimal quantity, DateTime? previousRequiredDate,
        DateTime? newRequiredDate, string idempotencyKey, string payloadHash, string correlationId,
        string? reason, CancellationToken cancellationToken)
    {
        var previous = await unitOfWork.Repository<InventoryWorkOrderReservationAction>().GetQueryable(value =>
                value.TenantId == currentUser.TenantId && value.InventoryAllocationId == allocation.Id && !value.IsDeleted)
            .OrderByDescending(value => value.Sequence).FirstOrDefaultAsync(cancellationToken);
        var sequence = (previous?.Sequence ?? 0) + 1;
        var integrityHash = Hash(new { AllocationId = allocation.Id, WorkOrderPartId = part.Id, sequence, actionType, previousStatus, newStatus,
            quantity, previousRequiredDate, newRequiredDate, idempotencyKey, payloadHash, correlationId, reason,
            ActorUserId = currentUser.UserId, PreviousHash = previous?.IntegrityHash });
        await unitOfWork.Repository<InventoryWorkOrderReservationAction>().AddAsync(
            new InventoryWorkOrderReservationAction
            {
                TenantId = currentUser.TenantId, InventoryAllocationId = allocation.Id,
                WorkOrderPartId = part.Id, Sequence = sequence, ActionType = actionType,
                PreviousStatus = previousStatus, NewStatus = newStatus, Quantity = Math.Abs(quantity),
                PreviousRequiredDate = previousRequiredDate, NewRequiredDate = newRequiredDate,
                IdempotencyKey = idempotencyKey, PayloadHash = payloadHash, CorrelationId = correlationId,
                Reason = Optional(reason, 1000), ActorUserId = currentUser.UserId,
                OccurredAtUtc = DateTime.UtcNow, PreviousHash = previous?.IntegrityHash,
                IntegrityHash = integrityHash, CreatedById = currentUser.UserId
            });
    }

    private async Task AddAuditAndEventAsync(InventoryAllocation allocation, WorkOrderPart part, string action,
        decimal quantity, string reason, string correlation, CancellationToken cancellationToken)
    {
        await unitOfWork.Repository<AuditLog>().AddAsync(new AuditLog
        {
            TenantId = currentUser.TenantId, UserId = currentUser.UserId,
            Username = string.IsNullOrWhiteSpace(currentUser.Username) ? currentUser.UserId.ToString() : currentUser.Username,
            Action = $"InventoryWorkOrderReservation.{action}", Resource = "InventoryAllocation",
            ResourceId = allocation.Id.ToString(),
            NewValues = JsonSerializer.Serialize(new { part.Id, part.WorkOrderId, allocation.InventoryItemId,
                allocation.WarehouseId, allocation.LocationId, allocation.Status, allocation.AllocatedQuantity,
                allocation.ConsumedQuantity, allocation.RemainingQuantity, quantity, reason, correlation }, JsonOptions),
            IpAddress = "system", UserAgent = correlation, Timestamp = DateTime.UtcNow
        });
        await controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create("inventory-work-order-reservation", allocation.TenantId,
                allocation.Id, action, allocation.Status, allocation.ConsumedQuantity, allocation.RemainingQuantity),
            EventType = "InventoryWorkOrderReservation", Action = action,
            Result = ProcurementControlEventResult.Allowed, RuleCode = "INV-REQ-FU-002", RuleVersion = "1",
            DecisionKeys = [], SourceType = "MaintenanceWorkOrder", SourceId = allocation.ReferenceId,
            SourceReference = allocation.ReferenceNumber ?? allocation.Id.ToString("N"), Reason = reason,
            InputValues = new { part.Id, allocation.InventoryItemId, allocation.WarehouseId, allocation.LocationId },
            ResultValues = new { allocation.Id, allocation.Status, allocation.AllocatedQuantity,
                allocation.ConsumedQuantity, allocation.RemainingQuantity }, CorrelationId = correlation,
            OccurredAtUtc = DateTime.UtcNow
        }, cancellationToken);
    }

    private async Task RequireCapabilityAsync(InventoryAllocation allocation, string correlation,
        CancellationToken cancellationToken)
    {
        var decision = await access.EnforceCapabilityAsync(AccessRequest("procurement.inventory.issue", allocation),
            correlation, cancellationToken);
        if (!decision.Allowed) throw new InventoryWorkOrderReservationAuthorizationException(decision.Message);
    }

    private static ProcurementAccessCapabilityRequest AccessRequest(string permission, InventoryAllocation allocation) =>
        new()
        {
            PermissionCode = permission, WarehouseId = allocation.WarehouseId,
            LocationId = allocation.LocationId, RequireLocationScope = true,
            SourceType = "MaintenanceWorkOrder", SourceReference = allocation.ReferenceNumber ?? allocation.Id.ToString("N")
        };

    private async Task<bool> IsReplayAsync(Guid allocationId, string key, string hash,
        CancellationToken cancellationToken)
    {
        var action = await unitOfWork.Repository<InventoryWorkOrderReservationAction>().GetQueryable(value =>
                value.TenantId == currentUser.TenantId && value.InventoryAllocationId == allocationId &&
                value.IdempotencyKey == key && !value.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (action is null) return false;
        if (!string.Equals(action.PayloadHash, hash, StringComparison.OrdinalIgnoreCase))
            throw Error("INV_WORK_ORDER_RESERVATION_IDEMPOTENCY_CONFLICT",
                "The idempotency key already identifies another reservation action.");
        return true;
    }

    private async Task<WorkOrderPartDto> LoadPartDtoByAllocationAsync(Guid allocationId,
        CancellationToken cancellationToken)
    {
        var part = await unitOfWork.Repository<WorkOrderPart>().GetQueryable(value =>
                value.TenantId == currentUser.TenantId && value.AllocationId == allocationId && !value.IsDeleted)
            .AsNoTracking().SingleAsync(cancellationToken);
        return await LoadPartDtoAsync(part.Id, cancellationToken);
    }

    private async Task<WorkOrderPartDto> LoadPartDtoAsync(Guid partId, CancellationToken cancellationToken)
    {
        var part = await unitOfWork.Repository<WorkOrderPart>().GetQueryable(value =>
                value.Id == partId && value.TenantId == currentUser.TenantId && !value.IsDeleted)
            .Include(value => value.InventoryItem).Include(value => value.WarehouseLocation)
            .AsNoTracking().SingleAsync(cancellationToken);
        InventoryAllocation? allocation = null;
        Warehouse? warehouse = null;
        if (part.AllocationId.HasValue)
        {
            allocation = await Allocations.AsNoTracking().SingleAsync(value => value.Id == part.AllocationId, cancellationToken);
            warehouse = await unitOfWork.Repository<Warehouse>().GetQueryable(value =>
                    value.Id == allocation.WarehouseId && value.TenantId == currentUser.TenantId)
                .AsNoTracking().SingleAsync(cancellationToken);
        }
        return Map(part, allocation, warehouse, part.WarehouseLocation, part.InventoryItem);
    }

    private static WorkOrderPartDto Map(WorkOrderPart part, InventoryAllocation? allocation,
        Warehouse? warehouse, WarehouseLocation? location, InventoryItem? item) => new()
    {
        Id = part.Id, WorkOrderId = part.WorkOrderId, InventoryItemId = part.InventoryItemId,
        ItemCode = part.ItemCode, ItemName = part.ItemName, Description = part.Description ?? item?.Description,
        QuantityRequired = part.QuantityRequired, QuantityAllocated = part.QuantityAllocated,
        QuantityUsed = part.QuantityUsed, QuantityReturned = part.QuantityReturned,
        UnitCost = part.UnitCost, TotalCost = part.TotalCost, WarehouseId = allocation?.WarehouseId,
        WarehouseName = warehouse?.Name, WarehouseLocationId = part.WarehouseLocationId,
        SerialNumber = part.SerialNumber, LotNumber = part.LotNumber,
        WarehouseLocationCode = location?.LocationCode, WarehouseLocationName = location?.Name,
        Status = part.Status, AllocationId = part.AllocationId, AllocatedAt = part.AllocatedAt,
        PickedAt = part.PickedAt, UsedAt = part.UsedAt, Notes = part.Notes,
        CreatedAt = part.CreatedAt, UpdatedAt = part.UpdatedAt
    };

    private async Task<T> ExecuteMutationAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        if (unitOfWork.HasActiveTransaction) return await action();
        try
        {
            return await unitOfWork.ExecuteInStrategyAsync(async () =>
            {
                await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                try
                {
                    var result = await action();
                    await unitOfWork.CommitAsync(cancellationToken);
                    return result;
                }
                catch
                {
                    if (unitOfWork.HasActiveTransaction) await unitOfWork.RollbackAsync(cancellationToken);
                    unitOfWork.ClearTrackedChanges();
                    throw;
                }
            }, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw Error("INV_WORK_ORDER_RESERVATION_CONCURRENCY_CONFLICT",
                "The work-order reservation changed after it was loaded. Refresh and retry.");
        }
    }

    private void EnsureActor()
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId == Guid.Empty || currentUser.TenantId == Guid.Empty ||
            currentUser.IsExternalUser)
            throw new InventoryWorkOrderReservationAuthorizationException(
                "An authenticated internal tenant user is required.");
    }

    private static string PartStatus(InventoryAllocation allocation, WorkOrderPart part) => allocation.Status switch
    {
        Consumed => "Used",
        Cancelled when part.QuantityReturned > 0 => "Returned",
        Cancelled => "Cancelled",
        Partial => "Partial",
        _ => "Allocated"
    };

    private static InventoryWorkOrderReservationException Error(string code, string message) => new(code, message);
    private static string Correlation(string? value) => string.IsNullOrWhiteSpace(value)
        ? $"inventory-work-order-reservation-{Guid.NewGuid():N}" : value.Trim()[..Math.Min(value.Trim().Length, 100)];
    private static string Required(string? value, int max, string name)
    {
        var clean = value?.Trim();
        if (string.IsNullOrWhiteSpace(clean)) throw Error("INV_WORK_ORDER_RESERVATION_REQUIRED", $"{name} is required.");
        if (clean.Length > max) throw Error("INV_WORK_ORDER_RESERVATION_LENGTH", $"{name} cannot exceed {max} characters.");
        return clean;
    }
    private static string? Optional(string? value, int max)
    {
        var clean = value?.Trim();
        if (string.IsNullOrWhiteSpace(clean)) return null;
        return clean[..Math.Min(clean.Length, max)];
    }
    private static DateTime? Utc(DateTime? value) => value.HasValue
        ? value.Value.Kind == DateTimeKind.Utc ? value : value.Value.ToUniversalTime() : null;
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions)))).ToLowerInvariant();

    private sealed record StockScope(InventoryItem Item, Warehouse Warehouse, WarehouseLocation Location,
        WarehouseQuantity WarehouseQuantity, InventoryLocation InventoryLocation);
}
