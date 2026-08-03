using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Notifications;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Inventory;

public sealed class InventoryProjectReservationService : IInventoryProjectReservationService
{
    private const string AllocationType = "ProjectRequisition";
    private const string ActiveStatus = "Active";
    private const string PartialStatus = "PartiallyFulfilled";
    private const string ConsumedStatus = "Consumed";
    private const string CancelledStatus = "Cancelled";
    private const string ExpiredStatus = "Expired";
    private const string SubstitutedStatus = "Substituted";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementAccessControlService _access;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly INotificationService _notifications;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<InventoryProjectReservationService> _logger;

    public InventoryProjectReservationService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService access,
        IProcurementControlEventService controlEvents,
        INotificationService notifications,
        UserManager<ApplicationUser> userManager,
        ILogger<InventoryProjectReservationService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _access = access;
        _controlEvents = controlEvents;
        _notifications = notifications;
        _userManager = userManager;
        _logger = logger;
    }

    private IQueryable<InventoryAllocation> Allocations =>
        _unitOfWork.Repository<InventoryAllocation>().GetQueryable(value =>
            value.AllocationType == AllocationType && !value.IsDeleted);

    public async Task<IReadOnlyList<InventoryProjectReservationDto>> GetAsync(
        Guid? projectId = null,
        Guid? departmentId = null,
        InventoryProjectReservationStatus? status = null,
        int take = 200,
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        take = Math.Clamp(take, 1, 500);
        var query = FullQuery().Where(value => value.TenantId == _currentUser.TenantId);
        if (projectId.HasValue) query = query.Where(value => value.ProjectId == projectId.Value);
        if (departmentId.HasValue) query = query.Where(value => value.DepartmentId == departmentId.Value);
        if (status.HasValue)
        {
            var statuses = AllocationStatuses(status.Value);
            query = query.Where(value => statuses.Contains(value.Status));
        }
        var values = await query.OrderByDescending(value => value.AllocationDate).ThenBy(value => value.Id)
            .AsNoTracking().ToListAsync(cancellationToken);
        var accessByScope = new Dictionary<(Guid WarehouseId, Guid? LocationId), bool>();
        var result = new List<InventoryProjectReservationDto>(Math.Min(values.Count, take));
        foreach (var value in values)
        {
            var scope = (value.WarehouseId, value.LocationId);
            if (!accessByScope.TryGetValue(scope, out var allowed))
            {
                allowed = await CanReadAsync(value, cancellationToken);
                accessByScope[scope] = allowed;
            }
            if (allowed) result.Add(await MapAsync(value, includeNotifications: false, cancellationToken));
            if (result.Count == take) break;
        }
        return result;
    }

    public async Task<InventoryProjectReservationDto> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        var value = await FullQuery().AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == id && item.TenantId == _currentUser.TenantId, cancellationToken)
            ?? throw new InventoryProjectReservationNotFoundException(
                "The project reservation was not found in the current tenant.");
        if (!await CanReadAsync(value, cancellationToken))
            throw new InventoryProjectReservationAuthorizationException(
                "You are not assigned to read this project reservation's warehouse and location.");
        return await MapAsync(value, includeNotifications: true, cancellationToken);
    }

    public Task<InventoryProjectReservationDto> ReserveAsync(
        CreateInventoryProjectReservationRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        return ExecuteMutationAsync(async () =>
        {
            ValidateCreate(request);
            var tenantId = _currentUser.TenantId;
            var key = Required(request.IdempotencyKey, 100, "Idempotency key");
            var correlation = Correlation(request.CorrelationId);
            var payloadHash = Hash(new
            {
                request.InventoryRequisitionItemId,
                request.LocationId,
                request.Quantity,
                ExpiresAtUtc = Utc(request.ExpiresAtUtc),
                Notes = Optional(request.Notes, 1000)
            });

            var replay = await Allocations.AsNoTracking().SingleOrDefaultAsync(value =>
                value.TenantId == tenantId && value.IdempotencyKey == key, cancellationToken);
            if (replay is not null)
            {
                if (!string.Equals(replay.PayloadHash, payloadHash, StringComparison.OrdinalIgnoreCase))
                    throw Error("INV_PROJECT_RESERVATION_IDEMPOTENCY_CONFLICT",
                        "The idempotency key already identifies a different reservation payload.");
                return await LoadDtoAsync(replay.Id, cancellationToken);
            }

            await _unitOfWork.AcquireTransactionLockAsync(
                $"inventory-project-reservation:{tenantId:N}:{request.InventoryRequisitionItemId:N}", cancellationToken);
            var requisitionLine = await _unitOfWork.Repository<InventoryRequisitionItem>().GetQueryable(value =>
                    value.Id == request.InventoryRequisitionItemId && value.TenantId == tenantId && !value.IsDeleted)
                .Include(value => value.InventoryRequisition)
                .Include(value => value.InventoryItem)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new InventoryProjectReservationNotFoundException(
                    "The approved requisition line was not found in the current tenant.");
            var requisition = requisitionLine.InventoryRequisition;
            if (!requisition.ProjectId.HasValue || requisition.ProjectId.Value == Guid.Empty)
                throw Error("INV_PROJECT_RESERVATION_PROJECT_REQUIRED",
                    "A project reservation requires an approved requisition linked to a project.");
            if (requisition.Status is not (RequisitionStatus.Approved or RequisitionStatus.InProgress or RequisitionStatus.PartiallyIssued))
                throw Error("INV_PROJECT_RESERVATION_APPROVED_SOURCE_REQUIRED",
                    "Only an approved or partially issued project requisition can reserve stock.");
            var remainingApproved = requisitionLine.ApprovedQuantity - requisitionLine.IssuedQuantity;
            if (request.Quantity > remainingApproved)
                throw Error("INV_PROJECT_RESERVATION_APPROVED_QUANTITY_EXCEEDED",
                    "The reservation cannot exceed the remaining approved requisition quantity.");
            if (await Allocations.AnyAsync(value => value.TenantId == tenantId &&
                    value.InventoryRequisitionItemId == requisitionLine.Id &&
                    (value.Status == ActiveStatus || value.Status == PartialStatus), cancellationToken))
                throw Error("INV_PROJECT_RESERVATION_ACTIVE_EXISTS",
                    "The requisition line already has an active project reservation.");

            var expiresAt = Utc(request.ExpiresAtUtc);
            var now = DateTime.UtcNow;
            if (expiresAt <= now)
                throw Error("INV_PROJECT_RESERVATION_EXPIRY_INVALID",
                    "The reservation expiry must be in the future.");
            var scope = await LoadStockScopeAsync(tenantId, requisition.WarehouseId, request.LocationId,
                requisitionLine.InventoryItemId, request.Quantity, correlation, requisition.RequisitionNumber,
                cancellationToken);

            var allocation = new InventoryAllocation
            {
                TenantId = tenantId,
                InventoryItemId = requisitionLine.InventoryItemId,
                WarehouseId = requisition.WarehouseId,
                LocationId = request.LocationId,
                AllocationType = AllocationType,
                ReferenceNumber = requisition.RequisitionNumber,
                ReferenceId = requisition.Id,
                InventoryRequisitionId = requisition.Id,
                InventoryRequisitionItemId = requisitionLine.Id,
                ProjectId = requisition.ProjectId.Value,
                DepartmentId = requisition.DepartmentId,
                AllocatedQuantity = request.Quantity,
                ConsumedQuantity = 0,
                RemainingQuantity = request.Quantity,
                AllocationDate = now,
                RequiredDate = requisition.RequiredDate,
                ExpirationDate = expiresAt,
                Status = ActiveStatus,
                Notes = Optional(request.Notes, 1000),
                AllocatedById = _currentUser.UserId,
                IdempotencyKey = key,
                PayloadHash = payloadHash,
                CorrelationId = correlation,
                CreatedById = _currentUser.UserId,
                CreatedBy = _currentUser.Username
            };
            await _unitOfWork.Repository<InventoryAllocation>().AddAsync(allocation);
            await ApplyAllocationDeltaAsync(scope, request.Quantity, cancellationToken);
            await AddActionAsync(allocation, InventoryProjectReservationActionType.Reserved,
                null, InventoryProjectReservationStatus.Reserved, request.Quantity, key, payloadHash,
                correlation, allocation.Notes, _currentUser.UserId, cancellationToken);
            await AddAuditAsync(tenantId, _currentUser.UserId, "InventoryProjectReservation.Reserved",
                allocation, new { request.Quantity, expiresAt, requisition.ProjectId, requisition.DepartmentId }, correlation);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await NotifyAsync(allocation, requisition, "Reserved", request.Quantity, _currentUser.UserId,
                $"Project stock reserved until {expiresAt:u}.", correlation, cancellationToken);
            await RecordEventAsync(allocation, "Reserve", ProcurementControlEventResult.Allowed,
                _currentUser.UserId, correlation, $"Reserved {request.Quantity:0.####} units for the approved project requisition.",
                system: false, cancellationToken);
            return await LoadDtoAsync(allocation.Id, cancellationToken);
        }, cancellationToken);
    }

    public Task<InventoryProjectReservationDto> ReleaseAsync(
        Guid id,
        ReleaseInventoryProjectReservationRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        return ExecuteMutationAsync(async () =>
        {
            if (request.Quantity <= 0) throw Error("INV_PROJECT_RESERVATION_RELEASE_QUANTITY_INVALID", "Release quantity must be positive.");
            var reason = Required(request.Reason, 1000, "Release reason");
            var key = Required(request.IdempotencyKey, 100, "Idempotency key");
            var correlation = Correlation(request.CorrelationId);
            var allocation = await LoadForMutationAsync(id, request.RowVersion, cancellationToken);
            var payloadHash = Hash(new { id, request.Quantity, reason });
            if (await ReplayActionAsync(allocation.Id, key, payloadHash, cancellationToken))
                return await LoadDtoAsync(id, cancellationToken);
            EnsureOpen(allocation);
            if (request.Quantity > allocation.RemainingQuantity)
                throw Error("INV_PROJECT_RESERVATION_RELEASE_EXCEEDED", "Release quantity exceeds the remaining reservation.");
            await RequireCapabilityAsync(allocation, correlation, cancellationToken);
            var previous = Status(allocation);
            var scope = await LoadStockScopeAsync(allocation.TenantId, allocation.WarehouseId,
                allocation.LocationId!.Value, allocation.InventoryItemId, 0, correlation,
                allocation.ReferenceNumber ?? "project-reservation", cancellationToken);
            await ApplyAllocationDeltaAsync(scope, -request.Quantity, cancellationToken);
            allocation.RemainingQuantity -= request.Quantity;
            allocation.Status = allocation.RemainingQuantity == 0 ? CancelledStatus :
                allocation.ConsumedQuantity > 0 ? PartialStatus : ActiveStatus;
            allocation.UpdatedAt = DateTime.UtcNow;
            allocation.LastModifiedById = _currentUser.UserId;
            await _unitOfWork.Repository<InventoryAllocation>().UpdateAsync(allocation);
            await AddActionAsync(allocation, InventoryProjectReservationActionType.Released,
                previous, Status(allocation), request.Quantity, key, payloadHash, correlation, reason,
                _currentUser.UserId, cancellationToken);
            await AddAuditAsync(allocation.TenantId, _currentUser.UserId,
                "InventoryProjectReservation.Released", allocation,
                new { request.Quantity, allocation.RemainingQuantity, reason }, correlation);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            var requisition = await LoadRequisitionAsync(allocation.InventoryRequisitionId!.Value, allocation.TenantId, cancellationToken);
            await NotifyAsync(allocation, requisition, "Released", request.Quantity, _currentUser.UserId,
                reason, correlation, cancellationToken);
            await RecordEventAsync(allocation, "Release", ProcurementControlEventResult.Allowed,
                _currentUser.UserId, correlation, reason, false, cancellationToken);
            return await LoadDtoAsync(id, cancellationToken);
        }, cancellationToken);
    }

    public Task<InventoryProjectReservationDto> SubstituteAsync(
        Guid id,
        SubstituteInventoryProjectReservationRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        return ExecuteMutationAsync(async () =>
        {
            if (request.ReplacementInventoryItemId == Guid.Empty)
                throw Error("INV_PROJECT_RESERVATION_SUBSTITUTE_ITEM_REQUIRED", "A replacement inventory item is required.");
            var reason = Required(request.Reason, 1000, "Substitution reason");
            var key = Required(request.IdempotencyKey, 100, "Idempotency key");
            var correlation = Correlation(request.CorrelationId);
            var allocation = await LoadForMutationAsync(id, request.RowVersion, cancellationToken);
            var payloadHash = Hash(new { id, request.ReplacementInventoryItemId, request.ExpiresAtUtc, reason });
            var replay = await Allocations.AsNoTracking().SingleOrDefaultAsync(value =>
                value.TenantId == allocation.TenantId && value.IdempotencyKey == key, cancellationToken);
            if (replay is not null)
            {
                if (!string.Equals(replay.PayloadHash, payloadHash, StringComparison.OrdinalIgnoreCase))
                    throw Error("INV_PROJECT_RESERVATION_IDEMPOTENCY_CONFLICT",
                        "The idempotency key already identifies a different substitution.");
                return await LoadDtoAsync(replay.Id, cancellationToken);
            }
            EnsureOpen(allocation);
            if (allocation.ConsumedQuantity > 0)
                throw Error("INV_PROJECT_RESERVATION_SUBSTITUTE_AFTER_FULFILLMENT",
                    "Substitution is allowed only before any reserved quantity is fulfilled.");
            if (allocation.InventoryItemId == request.ReplacementInventoryItemId)
                throw Error("INV_PROJECT_RESERVATION_SUBSTITUTE_SAME_ITEM",
                    "The replacement item must differ from the currently reserved item.");
            await RequireCapabilityAsync(allocation, correlation, cancellationToken);

            var line = await _unitOfWork.Repository<InventoryRequisitionItem>().GetQueryable(value =>
                    value.Id == allocation.InventoryRequisitionItemId && value.TenantId == allocation.TenantId && !value.IsDeleted)
                .SingleAsync(cancellationToken);
            if (line.IssuedQuantity > 0)
                throw Error("INV_PROJECT_RESERVATION_SUBSTITUTE_AFTER_ISSUE",
                    "Substitution is allowed only before the requisition line has been issued.");
            var replacement = await _unitOfWork.Repository<InventoryItem>().GetQueryable(value =>
                    value.Id == request.ReplacementInventoryItemId && value.TenantId == allocation.TenantId &&
                    !value.IsDeleted && value.Status == ItemStatus.Active)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new InventoryProjectReservationNotFoundException(
                    "The replacement inventory item was not found in the current tenant.");
            var expires = request.ExpiresAtUtc.HasValue ? Utc(request.ExpiresAtUtc.Value) : allocation.ExpirationDate!.Value;
            if (expires <= DateTime.UtcNow)
                throw Error("INV_PROJECT_RESERVATION_EXPIRY_INVALID", "The replacement reservation expiry must be in the future.");

            var oldScope = await LoadStockScopeAsync(allocation.TenantId, allocation.WarehouseId,
                allocation.LocationId!.Value, allocation.InventoryItemId, 0, correlation,
                allocation.ReferenceNumber ?? "project-reservation", cancellationToken);
            var newScope = await LoadStockScopeAsync(allocation.TenantId, allocation.WarehouseId,
                allocation.LocationId.Value, replacement.Id, allocation.RemainingQuantity, correlation,
                allocation.ReferenceNumber ?? "project-reservation", cancellationToken);
            var quantity = allocation.RemainingQuantity;
            await ApplyAllocationDeltaAsync(oldScope, -quantity, cancellationToken);
            await ApplyAllocationDeltaAsync(newScope, quantity, cancellationToken);

            allocation.RemainingQuantity = 0;
            allocation.Status = SubstitutedStatus;
            allocation.UpdatedAt = DateTime.UtcNow;
            allocation.LastModifiedById = _currentUser.UserId;
            await _unitOfWork.Repository<InventoryAllocation>().UpdateAsync(allocation);

            line.InventoryItemId = replacement.Id;
            line.ItemCode = replacement.ItemCode;
            line.ItemName = replacement.Name;
            line.UnitCost = replacement.AverageCost;
            line.LineValue = line.IssuedQuantity * line.UnitCost;
            await _unitOfWork.Repository<InventoryRequisitionItem>().UpdateAsync(line);

            var next = new InventoryAllocation
            {
                TenantId = allocation.TenantId,
                InventoryItemId = replacement.Id,
                WarehouseId = allocation.WarehouseId,
                LocationId = allocation.LocationId,
                AllocationType = AllocationType,
                ReferenceNumber = allocation.ReferenceNumber,
                ReferenceId = allocation.ReferenceId,
                InventoryRequisitionId = allocation.InventoryRequisitionId,
                InventoryRequisitionItemId = allocation.InventoryRequisitionItemId,
                ProjectId = allocation.ProjectId,
                DepartmentId = allocation.DepartmentId,
                SubstitutedFromAllocationId = allocation.Id,
                AllocatedQuantity = quantity,
                ConsumedQuantity = 0,
                RemainingQuantity = quantity,
                AllocationDate = DateTime.UtcNow,
                RequiredDate = allocation.RequiredDate,
                ExpirationDate = expires,
                Status = ActiveStatus,
                Notes = reason,
                AllocatedById = _currentUser.UserId,
                IdempotencyKey = key,
                PayloadHash = payloadHash,
                CorrelationId = correlation,
                CreatedById = _currentUser.UserId,
                CreatedBy = _currentUser.Username
            };
            await _unitOfWork.Repository<InventoryAllocation>().AddAsync(next);
            await AddActionAsync(allocation, InventoryProjectReservationActionType.Substituted,
                InventoryProjectReservationStatus.Reserved, InventoryProjectReservationStatus.Substituted,
                quantity, $"{key}:source", payloadHash, correlation, reason, _currentUser.UserId,
                cancellationToken, allocation.InventoryItemId, replacement.Id);
            await AddActionAsync(next, InventoryProjectReservationActionType.Reserved,
                null, InventoryProjectReservationStatus.Reserved, quantity, key, payloadHash, correlation,
                $"Replacement reservation: {reason}", _currentUser.UserId, cancellationToken,
                allocation.InventoryItemId, replacement.Id);
            await AddAuditAsync(allocation.TenantId, _currentUser.UserId,
                "InventoryProjectReservation.Substituted", allocation,
                new { SourceAllocationId = allocation.Id, ReplacementAllocationId = next.Id,
                    PreviousItemId = allocation.InventoryItemId, ReplacementItemId = replacement.Id, quantity, reason }, correlation);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            var requisition = await LoadRequisitionAsync(allocation.InventoryRequisitionId!.Value, allocation.TenantId, cancellationToken);
            await NotifyAsync(next, requisition, "Substituted", quantity, _currentUser.UserId,
                $"{allocation.InventoryItem.ItemCode} replaced by {replacement.ItemCode}: {reason}", correlation, cancellationToken);
            await RecordEventAsync(next, "Substitute", ProcurementControlEventResult.Allowed,
                _currentUser.UserId, correlation, reason, false, cancellationToken);
            return await LoadDtoAsync(next.Id, cancellationToken);
        }, cancellationToken);
    }

    public Task<InventoryProjectReservationFulfillmentResult> FulfillForIssueAsync(
        InventoryProjectReservationFulfillmentRequest request,
        CancellationToken cancellationToken = default) =>
        ExecuteMutationAsync(async () =>
        {
            if (request.Quantity <= 0 || request.ActorUserId == Guid.Empty)
                throw Error("INV_PROJECT_RESERVATION_FULFILLMENT_INVALID", "Positive issue quantity and actor are required.");
            var tenantId = _currentUser.TenantId;
            var allocation = await Allocations.Include(value => value.InventoryItem).SingleOrDefaultAsync(value =>
                value.TenantId == tenantId && value.InventoryRequisitionId == request.InventoryRequisitionId &&
                value.InventoryRequisitionItemId == request.InventoryRequisitionItemId &&
                (value.Status == ActiveStatus || value.Status == PartialStatus), cancellationToken);
            if (allocation is null) return new InventoryProjectReservationFulfillmentResult();
            if (allocation.InventoryItemId != request.InventoryItemId || allocation.WarehouseId != request.WarehouseId ||
                allocation.LocationId != request.LocationId)
                throw Error("INV_PROJECT_RESERVATION_ISSUE_SCOPE_MISMATCH",
                    "The issue must use the exact item, warehouse and location held by the active project reservation.");
            if (allocation.ExpirationDate <= DateTime.UtcNow)
            {
                await ExpireAllocationAsync(allocation, DateTime.UtcNow, request.ActorUserId,
                    $"{request.IdempotencyKey}:expired", request.CorrelationId,
                    "Reservation expired before issue.", false, cancellationToken);
                return new InventoryProjectReservationFulfillmentResult { ReservationId = allocation.Id };
            }
            var key = Required(request.IdempotencyKey, 100, "Idempotency key");
            var correlation = Correlation(request.CorrelationId);
            var quantity = Math.Min(request.Quantity, allocation.RemainingQuantity);
            var payloadHash = Hash(new { allocation.Id, request.InventoryRequisitionId,
                request.InventoryRequisitionItemId, request.InventoryItemId, request.WarehouseId,
                request.LocationId, quantity, request.ActorUserId });
            if (await ReplayActionAsync(allocation.Id, key, payloadHash, cancellationToken))
                return new InventoryProjectReservationFulfillmentResult
                    { ReservationId = allocation.Id, ReservedQuantityApplied = quantity };
            var previous = Status(allocation);
            var scope = await LoadStockScopeAsync(tenantId, allocation.WarehouseId,
                allocation.LocationId!.Value, allocation.InventoryItemId, 0, correlation,
                allocation.ReferenceNumber ?? "project-reservation", cancellationToken);
            await ApplyAllocationDeltaAsync(scope, -quantity, cancellationToken);
            allocation.ConsumedQuantity += quantity;
            allocation.RemainingQuantity -= quantity;
            allocation.Status = allocation.RemainingQuantity == 0 ? ConsumedStatus : PartialStatus;
            allocation.UpdatedAt = DateTime.UtcNow;
            allocation.LastModifiedById = request.ActorUserId;
            await _unitOfWork.Repository<InventoryAllocation>().UpdateAsync(allocation);
            var nextStatus = Status(allocation);
            await AddActionAsync(allocation,
                nextStatus == InventoryProjectReservationStatus.Fulfilled
                    ? InventoryProjectReservationActionType.Fulfilled
                    : InventoryProjectReservationActionType.PartiallyFulfilled,
                previous, nextStatus, quantity, key, payloadHash, correlation,
                "Reserved stock released into the authoritative requisition issue.", request.ActorUserId,
                cancellationToken);
            await AddAuditAsync(tenantId, request.ActorUserId,
                "InventoryProjectReservation.Fulfilled", allocation,
                new { quantity, allocation.ConsumedQuantity, allocation.RemainingQuantity }, correlation);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            var requisition = await LoadRequisitionAsync(request.InventoryRequisitionId, tenantId, cancellationToken);
            await NotifyAsync(allocation, requisition,
                nextStatus == InventoryProjectReservationStatus.Fulfilled ? "Fulfilled" : "PartiallyFulfilled",
                quantity, request.ActorUserId,
                $"{quantity:0.####} reserved units were issued; {allocation.RemainingQuantity:0.####} remain reserved.",
                correlation, cancellationToken);
            await RecordEventAsync(allocation, "Fulfill", ProcurementControlEventResult.Allowed,
                request.ActorUserId, correlation, "Reserved stock fulfilled through the requisition issue owner.",
                false, cancellationToken);
            return new InventoryProjectReservationFulfillmentResult
                { ReservationId = allocation.Id, ReservedQuantityApplied = quantity };
        }, cancellationToken);

    public Task ReleaseForCancelledRequisitionAsync(
        Guid requisitionId,
        Guid actorUserId,
        string reason,
        string correlationId,
        CancellationToken cancellationToken = default) =>
        ExecuteMutationAsync(async () =>
        {
            var tenantId = _currentUser.TenantId;
            var active = await Allocations.Where(value => value.TenantId == tenantId &&
                    value.InventoryRequisitionId == requisitionId &&
                    (value.Status == ActiveStatus || value.Status == PartialStatus))
                .ToListAsync(cancellationToken);
            foreach (var allocation in active)
            {
                await ReleaseRemainingAsync(allocation, actorUserId, CancelledStatus,
                    InventoryProjectReservationActionType.Released,
                    $"cancel:{requisitionId:N}:{allocation.Id:N}", correlationId,
                    string.IsNullOrWhiteSpace(reason) ? "Requisition cancelled." : reason,
                    false, cancellationToken);
            }
            return true;
        }, cancellationToken);

    public async Task<InventoryProjectReservationExpiryResult> ExpireDueAsync(
        Guid tenantId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant is required.", nameof(tenantId));
        var now = Utc(nowUtc);
        return await ExecuteMutationAsync(async () =>
        {
            await _unitOfWork.AcquireTransactionLockAsync(
                $"inventory-project-reservation-expiry:{tenantId:N}", cancellationToken);
            var due = await Allocations.Where(value => value.TenantId == tenantId &&
                    (value.Status == ActiveStatus || value.Status == PartialStatus) &&
                    value.ExpirationDate <= now)
                .OrderBy(value => value.ExpirationDate).ThenBy(value => value.Id)
                .ToListAsync(cancellationToken);
            var result = new InventoryProjectReservationExpiryResult();
            foreach (var allocation in due)
            {
                var actorId = allocation.AllocatedById ?? await ResolveSystemActorAsync(tenantId, cancellationToken);
                await ExpireAllocationAsync(allocation, now, actorId,
                    $"expiry:{allocation.Id:N}:{now:yyyyMMddHH}",
                    $"expiry-{tenantId:N}-{now:yyyyMMddHH}", "Reservation reached its expiry.",
                    true, cancellationToken);
                result.ExpiredCount++;
                result.ReservationIds.Add(allocation.Id);
            }
            return result;
        }, cancellationToken);
    }

    private async Task ExpireAllocationAsync(
        InventoryAllocation allocation,
        DateTime now,
        Guid actorId,
        string key,
        string correlation,
        string reason,
        bool system,
        CancellationToken cancellationToken)
    {
        if (allocation.RemainingQuantity <= 0) return;
        await ReleaseRemainingAsync(allocation, actorId, ExpiredStatus,
            InventoryProjectReservationActionType.Expired, key, correlation, reason, system, cancellationToken);
    }

    private async Task ReleaseRemainingAsync(
        InventoryAllocation allocation,
        Guid actorId,
        string terminalStatus,
        InventoryProjectReservationActionType actionType,
        string key,
        string correlation,
        string reason,
        bool system,
        CancellationToken cancellationToken)
    {
        var quantity = allocation.RemainingQuantity;
        if (quantity <= 0) return;
        var previous = Status(allocation);
        var scope = await LoadStockScopeAsync(allocation.TenantId, allocation.WarehouseId,
            allocation.LocationId!.Value, allocation.InventoryItemId, 0, Correlation(correlation),
            allocation.ReferenceNumber ?? "project-reservation", cancellationToken, enforceCapability: false);
        await ApplyAllocationDeltaAsync(scope, -quantity, cancellationToken);
        allocation.RemainingQuantity = 0;
        allocation.Status = terminalStatus;
        allocation.UpdatedAt = DateTime.UtcNow;
        allocation.LastModifiedById = actorId;
        await _unitOfWork.Repository<InventoryAllocation>().UpdateAsync(allocation);
        var payloadHash = Hash(new { allocation.Id, quantity, terminalStatus, reason });
        await AddActionAsync(allocation, actionType, previous, Status(allocation), quantity,
            Required(key, 100, "Idempotency key"), payloadHash, Correlation(correlation), reason,
            actorId, cancellationToken);
        await AddAuditAsync(allocation.TenantId, actorId,
            $"InventoryProjectReservation.{actionType}", allocation,
            new { quantity, terminalStatus, reason }, correlation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var requisition = await LoadRequisitionAsync(allocation.InventoryRequisitionId!.Value,
            allocation.TenantId, cancellationToken);
        await NotifyAsync(allocation, requisition, actionType.ToString(), quantity, actorId,
            reason, correlation, cancellationToken);
        await RecordEventAsync(allocation, actionType.ToString(), ProcurementControlEventResult.Allowed,
            actorId, correlation, reason, system, cancellationToken);
    }

    private async Task<StockScope> LoadStockScopeAsync(
        Guid tenantId,
        Guid warehouseId,
        Guid locationId,
        Guid inventoryItemId,
        decimal requiredAvailable,
        string correlation,
        string reference,
        CancellationToken cancellationToken,
        bool enforceCapability = true)
    {
        if (enforceCapability)
        {
            var decision = await _access.EnforceCapabilityAsync(new ProcurementAccessCapabilityRequest
            {
                PermissionCode = "procurement.inventory.issue",
                WarehouseId = warehouseId,
                LocationId = locationId,
                RequireLocationScope = true,
                SourceType = "InventoryProjectReservation",
                SourceReference = reference
            }, correlation, cancellationToken);
            if (!decision.Allowed) throw new InventoryProjectReservationAuthorizationException(decision.Message);
        }

        await _unitOfWork.AcquireTransactionLockAsync(
            $"inventory-stock:{tenantId:N}:{warehouseId:N}:{inventoryItemId:N}", cancellationToken);
        var location = await _unitOfWork.Repository<WarehouseLocation>().GetQueryable(value =>
                value.Id == locationId && value.TenantId == tenantId && !value.IsDeleted && value.IsActive &&
                (value.WarehouseId == warehouseId ||
                 (value.IsConsignmentBin && value.ConsignmentWarehouseId == warehouseId)))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InventoryProjectReservationNotFoundException(
                "The active reservation location was not found in the selected warehouse.");
        var warehouse = await _unitOfWork.Repository<Warehouse>().GetQueryable(value =>
                value.Id == warehouseId && value.TenantId == tenantId && !value.IsDeleted && value.IsActive)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InventoryProjectReservationNotFoundException(
                "The active reservation warehouse was not found in the current tenant.");
        var warehouseQuantity = await _unitOfWork.Repository<WarehouseQuantity>().GetQueryable(value =>
                value.TenantId == tenantId && value.WarehouseId == warehouseId &&
                value.InventoryItemId == inventoryItemId && !value.IsDeleted)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw Error("INV_PROJECT_RESERVATION_WAREHOUSE_STOCK_MISSING",
                "No warehouse stock balance exists for the requested project item.");
        var inventoryLocation = await _unitOfWork.Repository<InventoryLocation>().GetQueryable(value =>
                value.TenantId == tenantId && value.LocationId == locationId &&
                value.InventoryItemId == inventoryItemId && !value.IsDeleted)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw Error("INV_PROJECT_RESERVATION_LOCATION_STOCK_MISSING",
                "No location stock balance exists for the requested project item.");
        var item = await _unitOfWork.Repository<InventoryItem>().GetQueryable(value =>
                value.Id == inventoryItemId && value.TenantId == tenantId && !value.IsDeleted &&
                value.Status == ItemStatus.Active)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InventoryProjectReservationNotFoundException(
                "The requested inventory item was not found in the current tenant.");
        if (requiredAvailable > 0 && (warehouseQuantity.AvailableStock < requiredAvailable ||
            inventoryLocation.AvailableQuantity < requiredAvailable ||
            (!warehouse.IsConsignmentWarehouse && item.AvailableStock < requiredAvailable)))
            throw Error("INV_PROJECT_RESERVATION_STOCK_INSUFFICIENT",
                "The exact warehouse/location balance cannot satisfy the requested reservation.");
        return new StockScope(item, warehouse, location, warehouseQuantity, inventoryLocation);
    }

    private async Task ApplyAllocationDeltaAsync(
        StockScope scope,
        decimal allocatedDelta,
        CancellationToken cancellationToken)
    {
        scope.WarehouseQuantity.AllocatedStock += allocatedDelta;
        scope.WarehouseQuantity.AvailableStock = scope.WarehouseQuantity.CurrentStock - scope.WarehouseQuantity.AllocatedStock;
        scope.InventoryLocation.AllocatedQuantity += allocatedDelta;
        scope.InventoryLocation.AvailableQuantity = scope.InventoryLocation.Quantity - scope.InventoryLocation.AllocatedQuantity;
        if (scope.WarehouseQuantity.AllocatedStock < 0 || scope.InventoryLocation.AllocatedQuantity < 0)
            throw Error("INV_PROJECT_RESERVATION_ALLOCATION_UNDERFLOW",
                "The project reservation would release more stock than is allocated.");
        await _unitOfWork.Repository<WarehouseQuantity>().UpdateAsync(scope.WarehouseQuantity);
        await _unitOfWork.Repository<InventoryLocation>().UpdateAsync(scope.InventoryLocation);
        if (!scope.Warehouse.IsConsignmentWarehouse)
        {
            scope.Item.AllocatedStock += allocatedDelta;
            scope.Item.AvailableStock = scope.Item.CurrentStock - scope.Item.AllocatedStock;
            if (scope.Item.AllocatedStock < 0)
                throw Error("INV_PROJECT_RESERVATION_ITEM_ALLOCATION_UNDERFLOW",
                    "The project reservation would release more item stock than is allocated.");
            await _unitOfWork.Repository<InventoryItem>().UpdateAsync(scope.Item);
        }
        await Task.CompletedTask;
    }

    private async Task<InventoryAllocation> LoadForMutationAsync(
        Guid id,
        string rowVersion,
        CancellationToken cancellationToken)
    {
        await _unitOfWork.AcquireTransactionLockAsync(
            $"inventory-project-reservation:{_currentUser.TenantId:N}:{id:N}", cancellationToken);
        var value = await FullQuery().SingleOrDefaultAsync(item =>
            item.Id == id && item.TenantId == _currentUser.TenantId, cancellationToken)
            ?? throw new InventoryProjectReservationNotFoundException(
                "The project reservation was not found in the current tenant.");
        EnsureRowVersion(value.RowVersion, rowVersion);
        return value;
    }

    private async Task RequireCapabilityAsync(
        InventoryAllocation allocation,
        string correlation,
        CancellationToken cancellationToken)
    {
        var decision = await _access.EnforceCapabilityAsync(new ProcurementAccessCapabilityRequest
        {
            PermissionCode = "procurement.inventory.issue",
            WarehouseId = allocation.WarehouseId,
            LocationId = allocation.LocationId,
            RequireLocationScope = true,
            SourceType = "InventoryProjectReservation",
            SourceReference = allocation.ReferenceNumber ?? allocation.Id.ToString("N")
        }, correlation, cancellationToken);
        if (!decision.Allowed) throw new InventoryProjectReservationAuthorizationException(decision.Message);
    }

    private async Task<bool> CanReadAsync(
        InventoryAllocation allocation,
        CancellationToken cancellationToken)
    {
        var decision = await _access.CheckCapabilityAsync(new ProcurementAccessCapabilityRequest
        {
            PermissionCode = "procurement.inventory.read",
            WarehouseId = allocation.WarehouseId,
            LocationId = allocation.LocationId,
            RequireLocationScope = true,
            SourceType = "InventoryProjectReservation",
            SourceReference = allocation.ReferenceNumber ?? allocation.Id.ToString("N")
        }, $"inventory-project-reservation-read:{allocation.Id:N}", cancellationToken);
        return decision.Allowed;
    }

    private async Task AddActionAsync(
        InventoryAllocation allocation,
        InventoryProjectReservationActionType actionType,
        InventoryProjectReservationStatus? previousStatus,
        InventoryProjectReservationStatus newStatus,
        decimal quantity,
        string idempotencyKey,
        string payloadHash,
        string correlation,
        string? reason,
        Guid actorId,
        CancellationToken cancellationToken,
        Guid? previousItemId = null,
        Guid? newItemId = null,
        Guid? notificationId = null)
    {
        var last = await _unitOfWork.Repository<InventoryProjectReservationAction>().GetQueryable(value =>
                value.TenantId == allocation.TenantId && value.InventoryAllocationId == allocation.Id && !value.IsDeleted)
            .OrderByDescending(value => value.Sequence).FirstOrDefaultAsync(cancellationToken);
        var action = new InventoryProjectReservationAction
        {
            TenantId = allocation.TenantId,
            InventoryAllocationId = allocation.Id,
            Sequence = (last?.Sequence ?? 0) + 1,
            ActionType = actionType,
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            Quantity = quantity,
            PreviousInventoryItemId = previousItemId,
            NewInventoryItemId = newItemId,
            NotificationId = notificationId,
            ActorUserId = actorId,
            OccurredAtUtc = DateTime.UtcNow,
            IdempotencyKey = Required(idempotencyKey, 100, "Action idempotency key"),
            PayloadHash = payloadHash,
            CorrelationId = Correlation(correlation),
            Reason = Optional(reason, 1000),
            PreviousHash = last?.IntegrityHash,
            CreatedById = actorId
        };
        action.IntegrityHash = Hash(new { action.InventoryAllocationId, action.Sequence, action.ActionType,
            action.PreviousStatus, action.NewStatus, action.Quantity, action.PreviousInventoryItemId,
            action.NewInventoryItemId, action.NotificationId, action.ActorUserId, action.OccurredAtUtc,
            action.IdempotencyKey, action.PayloadHash, action.CorrelationId, action.Reason, action.PreviousHash });
        await _unitOfWork.Repository<InventoryProjectReservationAction>().AddAsync(action);
    }

    private async Task NotifyAsync(
        InventoryAllocation allocation,
        InventoryRequisition requisition,
        string eventName,
        decimal quantity,
        Guid actorId,
        string message,
        string correlation,
        CancellationToken cancellationToken)
    {
        var recipients = new HashSet<Guid>();
        if (requisition.RequestedById.HasValue) recipients.Add(requisition.RequestedById.Value);
        var project = await _unitOfWork.Repository<Project>().GetQueryable(value =>
                value.Id == allocation.ProjectId && value.TenantId == allocation.TenantId && !value.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (project?.ProjectManagerId is { } managerId) recipients.Add(managerId);
        if (project?.SponsorId is { } sponsorId) recipients.Add(sponsorId);
        var memberIds = await _unitOfWork.Repository<ProjectMember>().GetQueryable(value =>
                value.ProjectId == allocation.ProjectId && value.TenantId == allocation.TenantId &&
                !value.IsDeleted && value.IsActive)
            .Select(value => value.UserId).ToListAsync(cancellationToken);
        recipients.UnionWith(memberIds);
        var departmentHeadEmployeeId = await _unitOfWork.Repository<Department>().GetQueryable(value =>
                value.Id == allocation.DepartmentId && value.TenantId == allocation.TenantId &&
                !value.IsDeleted && value.IsActive)
            .Select(value => value.DepartmentHeadId).SingleOrDefaultAsync(cancellationToken);
        if (departmentHeadEmployeeId.HasValue)
        {
            var headUserId = await _userManager.Users.Where(value =>
                    value.TenantId == allocation.TenantId && value.IsActive &&
                    value.EmployeeId == departmentHeadEmployeeId.Value)
                .Select(value => value.Id).FirstOrDefaultAsync(cancellationToken);
            if (headUserId != Guid.Empty) recipients.Add(headUserId);
        }
        var activeRecipients = await _userManager.Users.Where(value =>
                value.TenantId == allocation.TenantId && value.IsActive && recipients.Contains(value.Id))
            .Select(value => value.Id).ToListAsync(cancellationToken);
        foreach (var recipientId in activeRecipients.Distinct())
        {
            var created = await _notifications.CreateNotificationAsync(new CreateNotificationDto
            {
                RecipientId = recipientId,
                Type = $"InventoryProjectReservation{eventName}",
                Title = $"Project stock reservation {eventName}",
                Message = $"{allocation.ReferenceNumber}: {quantity:0.####} {allocation.InventoryItem?.ItemCode ?? "item"}. {message}",
                Priority = eventName is "Expired" or "Released" ? "High" : "Normal",
                EntityType = "InventoryProjectReservation",
                EntityId = allocation.Id,
                ActionUrl = $"/inventory/project-reservations?reservationId={allocation.Id}",
                Metadata = new Dictionary<string, object>
                {
                    ["projectId"] = allocation.ProjectId!.Value,
                    ["departmentId"] = allocation.DepartmentId!.Value,
                    ["requisitionId"] = allocation.InventoryRequisitionId!.Value,
                    ["event"] = eventName,
                    ["correlationId"] = Correlation(correlation)
                }
            }, actorId, allocation.TenantId);
            var notificationHash = Hash(new { AllocationId = allocation.Id, NotificationId = created.Id,
                recipientId, eventName, quantity });
            await AddActionAsync(allocation, InventoryProjectReservationActionType.NotificationCreated,
                Status(allocation), Status(allocation), 0,
                $"notify:{created.Id:N}", notificationHash,
                correlation, $"Central in-app notification created for {eventName}.", actorId,
                cancellationToken, notificationId: created.Id);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task RecordEventAsync(
        InventoryAllocation allocation,
        string action,
        ProcurementControlEventResult result,
        Guid actorId,
        string correlation,
        string reason,
        bool system,
        CancellationToken cancellationToken)
    {
        var request = new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create("inventory-project-reservation", allocation.TenantId,
                allocation.Id, action, allocation.Status, allocation.RemainingQuantity),
            EventType = "InventoryProjectReservation",
            Action = action,
            Result = result,
            RuleCode = "TDC-0611",
            RuleVersion = "1",
            DecisionKeys = Enumerable.Range(1, 14).Select(value => $"DEC-{value:000}").ToList(),
            SourceType = "InventoryRequisition",
            SourceId = allocation.InventoryRequisitionId,
            SourceReference = allocation.ReferenceNumber ?? allocation.Id.ToString("N"),
            Reason = reason,
            InputValues = new { allocation.ProjectId, allocation.DepartmentId,
                allocation.InventoryItemId, allocation.WarehouseId, allocation.LocationId },
            ResultValues = new { allocation.Id, allocation.Status, allocation.AllocatedQuantity,
                allocation.ConsumedQuantity, allocation.RemainingQuantity },
            CorrelationId = Correlation(correlation),
            OccurredAtUtc = DateTime.UtcNow
        };
        if (system)
            await _controlEvents.RecordSystemAsync(allocation.TenantId,
                "Inventory project reservation scheduler", request, cancellationToken);
        else
            await _controlEvents.RecordAsync(request, cancellationToken);
    }

    private async Task AddAuditAsync(
        Guid tenantId,
        Guid actorId,
        string action,
        InventoryAllocation allocation,
        object values,
        string correlation)
    {
        await _unitOfWork.Repository<AuditLog>().AddAsync(new AuditLog
        {
            TenantId = tenantId,
            UserId = actorId,
            Username = actorId == _currentUser.UserId && !string.IsNullOrWhiteSpace(_currentUser.Username)
                ? _currentUser.Username : "Inventory reservation system",
            Action = action,
            Resource = "InventoryAllocation",
            ResourceId = allocation.Id.ToString(),
            NewValues = JsonSerializer.Serialize(values, JsonOptions),
            IpAddress = "system",
            UserAgent = Correlation(correlation),
            Timestamp = DateTime.UtcNow
        });
    }

    private IQueryable<InventoryAllocation> FullQuery() => Allocations
        .Include(value => value.InventoryItem)
        .Include(value => value.Warehouse)
        .Include(value => value.Location)
        .Include(value => value.InventoryRequisition)
        .Include(value => value.InventoryRequisitionItem)
        .Include(value => value.Project)
        .Include(value => value.AllocatedBy)
        .Include(value => value.SubstitutedFromAllocation)
        .Include(value => value.SubstitutedByAllocation)
        .Include(value => value.ProjectReservationActions.OrderBy(action => action.Sequence))
            .ThenInclude(action => action.ActorUser);

    private async Task<InventoryProjectReservationDto> LoadDtoAsync(Guid id, CancellationToken cancellationToken)
    {
        var allocation = await FullQuery().AsNoTracking().SingleAsync(value => value.Id == id, cancellationToken);
        return await MapAsync(allocation, includeNotifications: true, cancellationToken);
    }

    private async Task<InventoryProjectReservationDto> MapAsync(
        InventoryAllocation value,
        bool includeNotifications,
        CancellationToken cancellationToken)
    {
        var actions = value.ProjectReservationActions.OrderBy(action => action.Sequence).ToList();
        var dto = new InventoryProjectReservationDto
        {
            Id = value.Id,
            InventoryRequisitionId = value.InventoryRequisitionId!.Value,
            InventoryRequisitionItemId = value.InventoryRequisitionItemId!.Value,
            RequisitionNumber = value.ReferenceNumber ?? value.InventoryRequisition?.RequisitionNumber ?? string.Empty,
            ProjectId = value.ProjectId!.Value,
            ProjectCode = value.Project?.ProjectCode ?? value.InventoryRequisition?.ProjectCode ?? string.Empty,
            ProjectTitle = value.Project?.Title ?? string.Empty,
            DepartmentId = value.DepartmentId!.Value,
            DepartmentName = value.InventoryRequisition?.DepartmentName ?? string.Empty,
            WarehouseId = value.WarehouseId,
            WarehouseName = value.Warehouse?.Name ?? string.Empty,
            LocationId = value.LocationId!.Value,
            LocationCode = value.Location?.LocationCode ?? string.Empty,
            InventoryItemId = value.InventoryItemId,
            ItemCode = value.InventoryItem?.ItemCode ?? string.Empty,
            ItemName = value.InventoryItem?.Name ?? string.Empty,
            ReservedQuantity = value.AllocatedQuantity,
            FulfilledQuantity = value.ConsumedQuantity,
            ReleasedQuantity = value.AllocatedQuantity - value.ConsumedQuantity - value.RemainingQuantity,
            RemainingQuantity = value.RemainingQuantity,
            Status = Status(value),
            ReservedAtUtc = value.AllocationDate,
            ExpiresAtUtc = value.ExpirationDate!.Value,
            FulfilledAtUtc = actions.Where(action => action.ActionType == InventoryProjectReservationActionType.Fulfilled)
                .Select(action => (DateTime?)action.OccurredAtUtc).LastOrDefault(),
            ReleasedAtUtc = actions.Where(action => action.ActionType is InventoryProjectReservationActionType.Released or
                    InventoryProjectReservationActionType.Expired or InventoryProjectReservationActionType.Substituted)
                .Select(action => (DateTime?)action.OccurredAtUtc).LastOrDefault(),
            ReservedById = value.AllocatedById!.Value,
            ReservedByName = UserName(value.AllocatedBy),
            SubstitutedFromReservationId = value.SubstitutedFromAllocationId,
            SubstitutedByReservationId = value.SubstitutedByAllocation?.Id,
            Notes = value.Notes,
            RowVersion = Convert.ToBase64String(value.RowVersion),
            Actions = actions.Select(action => new InventoryProjectReservationActionDto
            {
                Id = action.Id,
                Sequence = action.Sequence,
                ActionType = action.ActionType,
                PreviousStatus = action.PreviousStatus,
                NewStatus = action.NewStatus,
                Quantity = action.Quantity,
                PreviousInventoryItemId = action.PreviousInventoryItemId,
                NewInventoryItemId = action.NewInventoryItemId,
                NotificationId = action.NotificationId,
                ActorUserId = action.ActorUserId,
                ActorName = UserName(action.ActorUser),
                OccurredAtUtc = action.OccurredAtUtc,
                Reason = action.Reason,
                IntegrityHash = action.IntegrityHash
            }).ToList()
        };
        if (includeNotifications)
        {
            var notifications = await _unitOfWork.Repository<Notification>().GetQueryable(item =>
                    item.TenantId == value.TenantId && item.EntityType == "InventoryProjectReservation" &&
                    item.EntityId == value.Id && !item.IsDeleted)
                .AsNoTracking().OrderByDescending(item => item.ScheduledFor).ToListAsync(cancellationToken);
            var recipientIds = notifications.Select(item => item.RecipientId).Distinct().ToList();
            var names = await _userManager.Users.Where(item => recipientIds.Contains(item.Id))
                .AsNoTracking().ToDictionaryAsync(item => item.Id, item => item.FirstName + " " + item.LastName,
                    cancellationToken);
            dto.Notifications = notifications.Select(item => new InventoryProjectReservationNotificationDto
            {
                Id = item.Id,
                RecipientId = item.RecipientId,
                RecipientName = names.GetValueOrDefault(item.RecipientId, "Unknown user"),
                NotificationType = item.NotificationType,
                Title = item.Title,
                Message = item.Message,
                Status = item.Status,
                IsRead = item.IsRead,
                ScheduledFor = item.ScheduledFor
            }).ToList();
        }
        return dto;
    }

    private async Task<InventoryRequisition> LoadRequisitionAsync(
        Guid id,
        Guid tenantId,
        CancellationToken cancellationToken) =>
        await _unitOfWork.Repository<InventoryRequisition>().GetQueryable(value =>
                value.Id == id && value.TenantId == tenantId && !value.IsDeleted)
            .AsNoTracking().SingleAsync(cancellationToken);

    private async Task<bool> ReplayActionAsync(
        Guid allocationId,
        string key,
        string payloadHash,
        CancellationToken cancellationToken)
    {
        var action = await _unitOfWork.Repository<InventoryProjectReservationAction>().GetQueryable(value =>
                value.InventoryAllocationId == allocationId && value.IdempotencyKey == key && !value.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (action is null) return false;
        if (!string.Equals(action.PayloadHash, payloadHash, StringComparison.OrdinalIgnoreCase))
            throw Error("INV_PROJECT_RESERVATION_IDEMPOTENCY_CONFLICT",
                "The idempotency key already identifies a different reservation action.");
        return true;
    }

    private async Task<Guid> ResolveSystemActorAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var userId = await _unitOfWork.Repository<UserTenant>().GetQueryable(value =>
                value.TenantId == tenantId && !value.IsDeleted && value.Status == UserTenantStatus.Active &&
                (value.ExpiresAt == null || value.ExpiresAt > DateTime.UtcNow) && value.User.IsActive)
            .OrderBy(value => value.User.UserName).Select(value => value.UserId)
            .FirstOrDefaultAsync(cancellationToken);
        if (userId == Guid.Empty)
            throw Error("INV_PROJECT_RESERVATION_SYSTEM_ACTOR_MISSING",
                "Expiry requires an active tenant user for immutable actor lineage.");
        return userId;
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
            throw Error("INV_PROJECT_RESERVATION_CONCURRENCY_CONFLICT",
                "The reservation changed after it was loaded. Refresh and retry.");
        }
    }

    private static void ValidateCreate(CreateInventoryProjectReservationRequest request)
    {
        if (request.InventoryRequisitionItemId == Guid.Empty || request.LocationId == Guid.Empty || request.Quantity <= 0)
            throw Error("INV_PROJECT_RESERVATION_REQUEST_INVALID",
                "Approved requisition line, exact location and positive quantity are required.");
    }

    private static void EnsureOpen(InventoryAllocation allocation)
    {
        if (allocation.Status is not (ActiveStatus or PartialStatus) || allocation.RemainingQuantity <= 0)
            throw Error("INV_PROJECT_RESERVATION_TERMINAL",
                "Only an active reservation with remaining quantity can be changed.");
    }

    private void EnsureActor()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty || _currentUser.TenantId == Guid.Empty)
            throw new InventoryProjectReservationAuthorizationException("An authenticated tenant actor is required.");
    }

    private static void EnsureRowVersion(byte[] actual, string supplied)
    {
        byte[] expected;
        try { expected = Convert.FromBase64String(supplied ?? string.Empty); }
        catch (FormatException) { throw Error("INV_PROJECT_RESERVATION_ROW_VERSION_INVALID", "A valid row version is required."); }
        if (expected.Length == 0 || !actual.SequenceEqual(expected))
            throw Error("INV_PROJECT_RESERVATION_CONCURRENCY_CONFLICT",
                "The reservation changed after it was loaded. Refresh and retry.");
    }

    private static InventoryProjectReservationStatus Status(InventoryAllocation allocation) => allocation.Status switch
    {
        ActiveStatus => allocation.ConsumedQuantity > 0
            ? InventoryProjectReservationStatus.PartiallyFulfilled
            : InventoryProjectReservationStatus.Reserved,
        PartialStatus => InventoryProjectReservationStatus.PartiallyFulfilled,
        ConsumedStatus => InventoryProjectReservationStatus.Fulfilled,
        CancelledStatus => InventoryProjectReservationStatus.Released,
        ExpiredStatus => InventoryProjectReservationStatus.Expired,
        SubstitutedStatus => InventoryProjectReservationStatus.Substituted,
        _ => throw Error("INV_PROJECT_RESERVATION_STATUS_INVALID", $"Unknown project allocation status '{allocation.Status}'.")
    };

    private static string[] AllocationStatuses(InventoryProjectReservationStatus status) => status switch
    {
        InventoryProjectReservationStatus.Reserved => new[] { ActiveStatus },
        InventoryProjectReservationStatus.PartiallyFulfilled => new[] { PartialStatus },
        InventoryProjectReservationStatus.Fulfilled => new[] { ConsumedStatus },
        InventoryProjectReservationStatus.Released => new[] { CancelledStatus },
        InventoryProjectReservationStatus.Expired => new[] { ExpiredStatus },
        InventoryProjectReservationStatus.Substituted => new[] { SubstitutedStatus },
        _ => Array.Empty<string>()
    };

    private static InventoryProjectReservationControlException Error(string code, string message) => new(code, message);
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions))));
    private static string Required(string? value, int max, string label) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= max
            ? value.Trim()
            : throw Error("INV_PROJECT_RESERVATION_VALUE_REQUIRED", $"{label} is required and must not exceed {max} characters.");
    private static string? Optional(string? value, int max) => string.IsNullOrWhiteSpace(value)
        ? null : value.Trim().Length <= max ? value.Trim()
        : throw Error("INV_PROJECT_RESERVATION_VALUE_TOO_LONG", $"The value must not exceed {max} characters.");
    private static string Correlation(string? value) => string.IsNullOrWhiteSpace(value)
        ? Guid.NewGuid().ToString("N") : value.Trim().Length <= 100 ? value.Trim() : value.Trim()[..100];
    private static DateTime Utc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    private static string UserName(ApplicationUser? value) => value is null ? "Unknown user" :
        string.IsNullOrWhiteSpace(value.FirstName + value.LastName) ? value.UserName ?? value.Id.ToString() :
        $"{value.FirstName} {value.LastName}".Trim();

    private sealed record StockScope(
        InventoryItem Item,
        Warehouse Warehouse,
        WarehouseLocation Location,
        WarehouseQuantity WarehouseQuantity,
        InventoryLocation InventoryLocation);
}
