using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;
using ErpSystem.Shared;

namespace ErpSystem.Core.Services.Inventory;

/// <summary>
/// Directs warehouse work over the existing receipt, requisition and transfer owners.
/// This register never posts stock itself: picking delegates to the requisition owner and
/// replenishment delegates to the ordinary same-warehouse transfer lifecycle.
/// </summary>
public sealed class InventoryDirectedOperationService : IInventoryDirectedOperationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementAccessControlService _access;
    private readonly IInventoryRequisitionService _requisitions;
    private readonly IInventoryTransferService _transfers;

    public InventoryDirectedOperationService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService access,
        IInventoryRequisitionService requisitions,
        IInventoryTransferService transfers)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _access = access;
        _requisitions = requisitions;
        _transfers = transfers;
    }

    private Guid TenantId => _currentUser.TenantId;
    private Guid UserId => _currentUser.UserId;
    private IQueryable<InventoryDirectedTask> Tasks => _unitOfWork.Repository<InventoryDirectedTask>().GetQueryable();
    private IQueryable<InventoryDirectedTaskAction> Actions => _unitOfWork.Repository<InventoryDirectedTaskAction>().GetQueryable();
    private IQueryable<Warehouse> Warehouses => _unitOfWork.Repository<Warehouse>().GetQueryable();
    private IQueryable<WarehouseLocation> Locations => _unitOfWork.Repository<WarehouseLocation>().GetQueryable();
    private IQueryable<InventoryLocation> InventoryLocations => _unitOfWork.Repository<InventoryLocation>().GetQueryable();
    private IQueryable<InventoryItem> Items => _unitOfWork.Repository<InventoryItem>().GetQueryable();

    public async Task<IReadOnlyList<InventoryDirectedAssigneeDto>> GetAssigneesAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        var now = DateTime.UtcNow;
        return await _unitOfWork.Repository<UserTenant>().GetQueryable().AsNoTracking()
            .Where(value => value.TenantId == TenantId && !value.IsDeleted &&
                            value.Status == UserTenantStatus.Active &&
                            (!value.ExpiresAt.HasValue || value.ExpiresAt > now) && value.User.IsActive &&
                            !value.User.UserRoles.Any(role => role.Role.Name == Constants.Roles.ExternalUser))
            .OrderBy(value => value.User.FirstName).ThenBy(value => value.User.LastName).ThenBy(value => value.User.UserName)
            .Select(value => new InventoryDirectedAssigneeDto
            {
                UserId = value.UserId,
                Username = value.User.UserName ?? value.User.Email ?? value.UserId.ToString(),
                DisplayName = (value.User.FirstName + " " + value.User.LastName).Trim()
            }).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InventoryDirectedSuggestionDto>> GetSuggestionsAsync(
        Guid warehouseId,
        InventoryDirectedTaskType? taskType = null,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        take = Math.Clamp(take, 1, 250);
        var warehouse = await Warehouses.AsNoTracking().SingleOrDefaultAsync(value =>
            value.TenantId == TenantId && value.Id == warehouseId && !value.IsDeleted && value.IsActive,
            cancellationToken) ?? throw new InventoryDirectedOperationNotFoundException(
                "The active warehouse was not found in the current tenant.");

        var candidates = new List<InventoryDirectedSuggestionDto>();
        if (!taskType.HasValue || taskType == InventoryDirectedTaskType.PutAway)
        {
            candidates.AddRange(await BuildReceiptPutAwaySuggestionsAsync(warehouse, cancellationToken));
            candidates.AddRange(await BuildQuarantinePutAwaySuggestionsAsync(warehouse, cancellationToken));
        }
        if (!taskType.HasValue || taskType == InventoryDirectedTaskType.Picking)
            candidates.AddRange(await BuildPickingSuggestionsAsync(warehouse, cancellationToken));
        if (!taskType.HasValue || taskType == InventoryDirectedTaskType.Replenishment)
            candidates.AddRange(await BuildReplenishmentSuggestionsAsync(warehouse, cancellationToken));

        if (candidates.Count == 0) return Array.Empty<InventoryDirectedSuggestionDto>();
        var suggestionKeys = candidates.Select(candidate => candidate.SuggestionKey).ToList();
        var existingKeys = await Tasks.AsNoTracking()
            .Where(value => value.TenantId == TenantId && !value.IsDeleted && suggestionKeys.Contains(value.SuggestionKey))
            .Select(value => value.SuggestionKey).ToListAsync(cancellationToken);
        var existing = existingKeys.ToHashSet(StringComparer.Ordinal);
        var activeClaims = (await Tasks.AsNoTracking()
                .Where(value => value.TenantId == TenantId && !value.IsDeleted &&
                                value.Status != InventoryDirectedTaskStatus.Completed &&
                                value.Status != InventoryDirectedTaskStatus.Cancelled)
                .Select(value => new { value.TaskType, value.SourceDocumentType, value.SourceDocumentId, value.SourceLineId })
                .ToListAsync(cancellationToken))
            .Select(value => ClaimKey(value.TaskType, value.SourceDocumentType, value.SourceDocumentId, value.SourceLineId))
            .ToHashSet(StringComparer.Ordinal);

        var authorized = new List<InventoryDirectedSuggestionDto>();
        foreach (var candidate in candidates
                     .Where(value => !existing.Contains(value.SuggestionKey) &&
                                     !activeClaims.Contains(ClaimKey(value.TaskType, value.SourceDocumentType,
                                         value.SourceDocumentId, value.SourceLineId)))
                     .OrderBy(value => value.TaskType)
                     .ThenBy(value => value.IsQuarantine ? 0 : 1)
                     .ThenBy(value => value.SourceReference)
                     .ThenBy(value => value.SourceLocationCode)
                     .ThenBy(value => value.DestinationLocationCode))
        {
            if (await CanAccessSuggestionAsync(candidate, cancellationToken))
                authorized.Add(candidate);
            if (authorized.Count == take) break;
        }

        return authorized;
    }

    public async Task<IReadOnlyList<InventoryDirectedTaskDto>> GetTasksAsync(
        Guid? warehouseId = null,
        InventoryDirectedTaskType? taskType = null,
        InventoryDirectedTaskStatus? status = null,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        take = Math.Clamp(take, 1, 250);
        var query = Tasks.AsNoTracking().Where(value => value.TenantId == TenantId && !value.IsDeleted);
        if (warehouseId.HasValue) query = query.Where(value => value.WarehouseId == warehouseId);
        if (taskType.HasValue) query = query.Where(value => value.TaskType == taskType);
        if (status.HasValue) query = query.Where(value => value.Status == status);

        var result = new List<InventoryDirectedTaskDto>(take);
        var offset = 0;
        var pageSize = Math.Max(take, 50);
        while (result.Count < take)
        {
            var ids = await query.OrderByDescending(value => value.AssignedAtUtc).ThenByDescending(value => value.Id)
                .Select(value => value.Id).Skip(offset).Take(pageSize).ToListAsync(cancellationToken);
            foreach (var id in ids)
            {
                var task = await LoadTaskAsync(id, tracked: false, cancellationToken);
                if (task is not null && await CanAccessTaskAsync(task, cancellationToken)) result.Add(Map(task));
                if (result.Count == take) break;
            }
            offset += ids.Count;
            if (ids.Count < pageSize) break;
        }
        return result;
    }

    public async Task<InventoryDirectedTaskDto> GetTaskAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureActor();
        var task = await LoadTaskAsync(id, tracked: false, cancellationToken)
                   ?? throw new InventoryDirectedOperationNotFoundException("The directed task was not found in the current tenant.");
        if (!await CanAccessTaskAsync(task, cancellationToken))
            throw new InventoryDirectedOperationAuthorizationException("You do not have location access to this directed task.");
        return Map(task);
    }

    public async Task<InventoryDirectedTaskDto> CreateTaskAsync(
        CreateInventoryDirectedTaskRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        if (request.WarehouseId == Guid.Empty)
            throw new InventoryDirectedOperationConflictException("INV_DIRECTED_WAREHOUSE_REQUIRED", "WarehouseId is required.");
        var key = NormalizeRequired(request.IdempotencyKey, 100, "IdempotencyKey");
        var suggestionKey = NormalizeHash(request.SuggestionKey, "SuggestionKey");
        var reason = NormalizeRequired(request.Reason, 1000, "Reason");
        var payloadHash = Hash(new
        {
            request.WarehouseId,
            SuggestionKey = suggestionKey,
            request.AssignedToUserId,
            request.DueAtUtc,
            Reason = reason,
            Notes = NormalizeOptional(request.Notes, 2000)
        });

        var replay = await Tasks.AsNoTracking().SingleOrDefaultAsync(value =>
            value.TenantId == TenantId && value.IdempotencyKey == key && !value.IsDeleted, cancellationToken);
        if (replay is not null)
        {
            if (!string.Equals(replay.PayloadHash, payloadHash, StringComparison.Ordinal))
                throw new InventoryDirectedOperationConflictException("INV_DIRECTED_IDEMPOTENCY_CONFLICT",
                    "The idempotency key was already used with a different task payload.");
            return await GetTaskAsync(replay.Id, cancellationToken);
        }

        Guid createdId = Guid.Empty;
        try
        {
            await _unitOfWork.ExecuteInStrategyAsync(async () =>
            {
                await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                try
                {
                    await _unitOfWork.AcquireTransactionLockAsync($"inventory-directed:create:{TenantId:N}", cancellationToken);
                    var concurrentReplay = await Tasks.SingleOrDefaultAsync(value =>
                        value.TenantId == TenantId && value.IdempotencyKey == key && !value.IsDeleted, cancellationToken);
                    if (concurrentReplay is not null)
                    {
                        if (!string.Equals(concurrentReplay.PayloadHash, payloadHash, StringComparison.Ordinal))
                            throw new InventoryDirectedOperationConflictException("INV_DIRECTED_IDEMPOTENCY_CONFLICT",
                                "The idempotency key was already used with a different task payload.");
                        createdId = concurrentReplay.Id;
                        await _unitOfWork.CommitAsync(cancellationToken);
                        return;
                    }

                    var suggestion = (await GetSuggestionsAsync(request.WarehouseId, null, 250, cancellationToken))
                        .SingleOrDefault(value => value.SuggestionKey == suggestionKey)
                        ?? throw new InventoryDirectedOperationConflictException("INV_DIRECTED_SUGGESTION_STALE",
                            "The directed suggestion is no longer current or accessible. Refresh the work queue.");
                    var assigneeId = request.AssignedToUserId.GetValueOrDefault(UserId);
                    await EnsureActiveTenantUserAsync(assigneeId, cancellationToken);
                    var now = DateTime.UtcNow;
                    var task = new InventoryDirectedTask
                    {
                        TenantId = TenantId,
                        TaskNumber = await GenerateTaskNumberAsync(now, cancellationToken),
                        TaskType = suggestion.TaskType,
                        Status = InventoryDirectedTaskStatus.Assigned,
                        WarehouseId = suggestion.WarehouseId,
                        InventoryItemId = suggestion.InventoryItemId,
                        SourceLocationId = suggestion.SourceLocationId,
                        DestinationLocationId = suggestion.DestinationLocationId,
                        Quantity = suggestion.Quantity,
                        SourceDocumentType = suggestion.SourceDocumentType,
                        SourceDocumentId = suggestion.SourceDocumentId,
                        SourceLineId = suggestion.SourceLineId,
                        SourceReference = suggestion.SourceReference,
                        SuggestionKey = suggestion.SuggestionKey,
                        IdempotencyKey = key,
                        PayloadHash = payloadHash,
                        CapacitySnapshotJson = JsonSerializer.Serialize(suggestion.DestinationCapacity),
                        IsQuarantine = suggestion.IsQuarantine,
                        AssignedToUserId = assigneeId,
                        CreatedByUserId = UserId,
                        AssignedAtUtc = now,
                        DueAtUtc = request.DueAtUtc?.ToUniversalTime(),
                        Reason = reason,
                        Notes = NormalizeOptional(request.Notes, 2000),
                        CorrelationId = NormalizeCorrelation(correlationId)
                    };
                    task.IntegrityHash = TaskIntegrity(task);
                    await _unitOfWork.Repository<InventoryDirectedTask>().AddAsync(task);
                    await AddActionAsync(task, InventoryDirectedTaskActionType.Created, task.Status,
                        "Directed task created from a server-derived suggestion.", new { suggestion.SuggestionKey }, correlationId);
                    await AddAuditAsync("Create", task, null, new { task.TaskNumber, task.TaskType, task.Status }, correlationId);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    await _unitOfWork.CommitAsync(cancellationToken);
                    createdId = task.Id;
                }
                catch
                {
                    await _unitOfWork.RollbackAsync(cancellationToken);
                    _unitOfWork.ClearTrackedChanges();
                    throw;
                }
            }, cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            throw new InventoryDirectedOperationConflictException("INV_DIRECTED_CREATE_CONFLICT",
                $"The directed task could not be created because its source was claimed concurrently: {exception.GetBaseException().Message}");
        }
        return await GetTaskAsync(createdId, cancellationToken);
    }

    public Task<InventoryDirectedTaskDto> StartTaskAsync(
        Guid id,
        StartInventoryDirectedTaskRequest request,
        string correlationId,
        CancellationToken cancellationToken = default) => MutateAsync(
            id, request.RowVersion, correlationId, cancellationToken,
            async task =>
            {
                EnsureAssignedActor(task);
                if (task.Status != InventoryDirectedTaskStatus.Assigned)
                    throw StatusConflict(task, "started");
                var before = new { task.Status, task.StartedAtUtc };
                task.Status = InventoryDirectedTaskStatus.InProgress;
                task.StartedByUserId = UserId;
                task.StartedAtUtc = DateTime.UtcNow;
                task.IntegrityHash = TaskIntegrity(task);
                await AddActionAsync(task, InventoryDirectedTaskActionType.Started, task.Status,
                    NormalizeRequired(request.Comment, 1000, "Comment"), null, correlationId);
                await AddAuditAsync("Start", task, before, new { task.Status, task.StartedAtUtc }, correlationId);
            });

    public async Task<InventoryDirectedTaskDto> ConfirmTaskAsync(
        Guid id,
        ConfirmInventoryDirectedTaskRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        var normalizedComment = NormalizeRequired(request.Comment, 1000, "Comment");
        return await MutateAsync(id, request.RowVersion, correlationId, cancellationToken, async task =>
        {
            EnsureAssignedActor(task);
            if (task.Status is not (InventoryDirectedTaskStatus.Assigned or InventoryDirectedTaskStatus.InProgress))
                throw StatusConflict(task, "confirmed");
            await ValidateTaskSourceAsync(task, cancellationToken);
            if (task.DestinationLocationId.HasValue)
            {
                var capacity = await GetCapacityAsync(task.DestinationLocationId.Value, task.InventoryItemId,
                    task.TaskType == InventoryDirectedTaskType.PutAway && !task.IsQuarantine ? 0m : task.Quantity,
                    cancellationToken);
                EnsureCapacity(task, capacity);
                task.CapacitySnapshotJson = JsonSerializer.Serialize(capacity);
            }

            var before = new { task.Status, task.LinkedInventoryTransferId };
            if (task.TaskType == InventoryDirectedTaskType.Picking)
            {
                var issued = await _requisitions.IssueAsync(task.SourceDocumentId, new IssueRequisitionDto
                {
                    IdempotencyKey = $"directed:{task.Id:N}",
                    CorrelationId = correlationId,
                    Items = new List<IssueRequisitionItemDto>
                    {
                        new()
                        {
                            ItemId = task.SourceLineId,
                            IssuedQuantity = task.Quantity,
                            LocationId = task.SourceLocationId,
                            LotNumber = NormalizeOptional(request.LotNumber, 100),
                            BatchNumber = NormalizeOptional(request.BatchNumber, 100),
                            SerialNumber = NormalizeOptional(request.SerialNumber, 100),
                            ManufactureDate = request.ManufactureDate,
                            ExpiryDate = request.ExpiryDate,
                            InventoryTrackingExceptionId = request.InventoryTrackingExceptionId
                        }
                    },
                    Notes = $"Directed task {task.TaskNumber}: {normalizedComment}"
                });
                if (!issued)
                    throw new InventoryDirectedOperationConflictException("INV_DIRECTED_PICK_FAILED",
                        "The requisition owner did not confirm the pick.");
                task.Status = InventoryDirectedTaskStatus.Completed;
                task.CompletedByUserId = UserId;
                task.CompletedAtUtc = DateTime.UtcNow;
                await AddActionAsync(task, InventoryDirectedTaskActionType.PickConfirmed, task.Status,
                    normalizedComment, new { task.SourceDocumentId, task.SourceLineId, task.Quantity }, correlationId);
            }
            else if (task.TaskType == InventoryDirectedTaskType.Replenishment)
            {
                var transfer = await _transfers.CreateAsync(new CreateInventoryTransferDto
                {
                    SourceWarehouseId = task.WarehouseId,
                    DestinationWarehouseId = task.WarehouseId,
                    TransferType = TransferType.Replenishment,
                    RequiredDate = task.DueAtUtc,
                    Reason = task.Reason,
                    Notes = $"Created by directed task {task.TaskNumber}: {normalizedComment}",
                    Items = new List<CreateInventoryTransferItemDto>
                    {
                        new()
                        {
                            InventoryItemId = task.InventoryItemId,
                            RequestedQuantity = task.Quantity,
                            SourceLocationId = task.SourceLocationId,
                            DestinationLocationId = task.DestinationLocationId,
                            LotNumber = NormalizeOptional(request.LotNumber, 100),
                            BatchNumber = NormalizeOptional(request.BatchNumber, 100),
                            SerialNumber = NormalizeOptional(request.SerialNumber, 100),
                            ManufactureDate = request.ManufactureDate,
                            ExpiryDate = request.ExpiryDate,
                            InventoryTrackingExceptionId = request.InventoryTrackingExceptionId
                        }
                    }
                }, UserId);
                task.LinkedInventoryTransferId = transfer.Id;
                task.Status = InventoryDirectedTaskStatus.AwaitingStockMove;
                await AddActionAsync(task, InventoryDirectedTaskActionType.TransferCreated, task.Status,
                    normalizedComment, new { InventoryTransferId = transfer.Id, transfer.TransferNumber }, correlationId);
            }
            else
            {
                task.Status = InventoryDirectedTaskStatus.Completed;
                task.CompletedByUserId = UserId;
                task.CompletedAtUtc = DateTime.UtcNow;
                await AddActionAsync(task, InventoryDirectedTaskActionType.PlacementConfirmed, task.Status,
                    normalizedComment, new { task.SourceLocationId, task.DestinationLocationId, task.Quantity }, correlationId);
            }
            task.IntegrityHash = TaskIntegrity(task);
            await AddAuditAsync("Confirm", task, before,
                new { task.Status, task.CompletedAtUtc, task.LinkedInventoryTransferId }, correlationId);
        });
    }

    public async Task<InventoryDirectedTaskDto> ReconcileTaskAsync(
        Guid id,
        ReconcileInventoryDirectedTaskRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        return await MutateAsync(id, request.RowVersion, correlationId, cancellationToken, async task =>
        {
            EnsureAssignedActor(task);
            if (task.Status != InventoryDirectedTaskStatus.AwaitingStockMove || !task.LinkedInventoryTransferId.HasValue)
                throw StatusConflict(task, "reconciled");
            var transfer = await _unitOfWork.Repository<InventoryTransfer>().GetQueryable().AsNoTracking()
                .SingleOrDefaultAsync(value => value.TenantId == TenantId &&
                    value.Id == task.LinkedInventoryTransferId && !value.IsDeleted, cancellationToken)
                ?? throw new InventoryDirectedOperationConflictException("INV_DIRECTED_TRANSFER_MISSING",
                    "The linked replenishment transfer was not found.");
            var before = new { task.Status, TransferStatus = transfer.Status };
            if (transfer.Status == TransferStatus.Completed)
            {
                task.Status = InventoryDirectedTaskStatus.Completed;
                task.CompletedByUserId = UserId;
                task.CompletedAtUtc = DateTime.UtcNow;
                await AddActionAsync(task, InventoryDirectedTaskActionType.TransferCompleted, task.Status,
                    "The linked replenishment transfer completed.", new { transfer.Id, transfer.TransferNumber }, correlationId);
            }
            else if (transfer.Status is TransferStatus.Cancelled or TransferStatus.Rejected)
            {
                task.Status = InventoryDirectedTaskStatus.Cancelled;
                task.CancelledAtUtc = DateTime.UtcNow;
                task.CancellationReason = $"Linked transfer {transfer.TransferNumber} ended as {transfer.Status}.";
                await AddActionAsync(task, InventoryDirectedTaskActionType.Cancelled, task.Status,
                    task.CancellationReason, new { transfer.Id, transfer.TransferNumber, transfer.Status }, correlationId);
            }
            else
            {
                throw new InventoryDirectedOperationConflictException("INV_DIRECTED_TRANSFER_PENDING",
                    $"The linked transfer is {transfer.Status}; complete or terminate it before reconciliation.");
            }
            task.IntegrityHash = TaskIntegrity(task);
            await AddAuditAsync("Reconcile", task, before,
                new { task.Status, task.CompletedAtUtc, task.CancelledAtUtc }, correlationId);
        });
    }

    public async Task<InventoryDirectedTaskDto> CancelTaskAsync(
        Guid id,
        CancelInventoryDirectedTaskRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        return await MutateAsync(id, request.RowVersion, correlationId, cancellationToken, async task =>
        {
            EnsureAssignedActor(task);
            if (task.Status is InventoryDirectedTaskStatus.Completed or InventoryDirectedTaskStatus.Cancelled)
                throw StatusConflict(task, "cancelled");
            if (task.Status == InventoryDirectedTaskStatus.AwaitingStockMove)
                throw new InventoryDirectedOperationConflictException("INV_DIRECTED_TRANSFER_ACTIVE",
                    "Cancel or reject the linked stock transfer, then reconcile this task.");
            var before = new { task.Status };
            task.Status = InventoryDirectedTaskStatus.Cancelled;
            task.CancelledAtUtc = DateTime.UtcNow;
            task.CancellationReason = NormalizeRequired(request.Reason, 1000, "Reason");
            task.IntegrityHash = TaskIntegrity(task);
            await AddActionAsync(task, InventoryDirectedTaskActionType.Cancelled, task.Status,
                task.CancellationReason, null, correlationId);
            await AddAuditAsync("Cancel", task, before, new { task.Status, task.CancellationReason }, correlationId);
        });
    }

    private async Task<InventoryDirectedTaskDto> MutateAsync(
        Guid id,
        string rowVersion,
        string correlationId,
        CancellationToken cancellationToken,
        Func<InventoryDirectedTask, Task> mutation)
    {
        InventoryDirectedTaskDto? result = null;
        try
        {
            await _unitOfWork.ExecuteInStrategyAsync(async () =>
            {
                await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                try
                {
                    await _unitOfWork.AcquireTransactionLockAsync($"inventory-directed:task:{TenantId:N}:{id:N}", cancellationToken);
                    var task = await LoadTaskAsync(id, tracked: true, cancellationToken)
                               ?? throw new InventoryDirectedOperationNotFoundException("The directed task was not found in the current tenant.");
                    EnsureRowVersion(task, rowVersion);
                    if (!await CanAccessTaskAsync(task, cancellationToken))
                        throw new InventoryDirectedOperationAuthorizationException("You do not have location access to this directed task.");
                    await mutation(task);
                    task.UpdatedAt = DateTime.UtcNow;
                    task.LastModifiedById = UserId;
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    await _unitOfWork.CommitAsync(cancellationToken);
                    result = Map(task);
                }
                catch
                {
                    await _unitOfWork.RollbackAsync(cancellationToken);
                    _unitOfWork.ClearTrackedChanges();
                    throw;
                }
            }, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InventoryDirectedOperationConflictException("INV_DIRECTED_CONCURRENCY_CONFLICT",
                "The directed task changed after it was loaded. Refresh and retry.");
        }
        return result!;
    }

    private async Task<List<InventoryDirectedSuggestionDto>> BuildReceiptPutAwaySuggestionsAsync(
        Warehouse warehouse,
        CancellationToken cancellationToken)
    {
        var rows = await _unitOfWork.Repository<GoodsReceiptNoteItem>().GetQueryable().AsNoTracking()
            .Where(line => line.TenantId == TenantId && !line.IsDeleted && line.AcceptedQuantity > 0 &&
                           line.StorageLocationId.HasValue && line.GoodsReceiptNote.TenantId == TenantId &&
                           !line.GoodsReceiptNote.IsDeleted && line.GoodsReceiptNote.WarehouseId == warehouse.Id &&
                           line.GoodsReceiptNote.StockUpdated && line.GoodsReceiptNote.ReceivingLocationId.HasValue &&
                           line.GoodsReceiptNote.ReceivingLocationId != line.StorageLocationId &&
                           !line.InventoryItem.IsDeleted)
            .Select(line => new CandidateRow(
                line.InventoryItemId, line.InventoryItem.ItemCode, line.InventoryItem.Name,
                line.GoodsReceiptNote.ReceivingLocationId, line.GoodsReceiptNote.ReceivingLocation!.LocationCode,
                line.StorageLocationId, line.StorageLocation!.LocationCode,
                line.AcceptedQuantity, "GoodsReceiptNote", line.GoodsReceiptNoteId, line.Id,
                line.GoodsReceiptNote.GRNNumber, false,
                $"Move accepted receipt quantity from receiving to storage bin {line.StorageLocation.LocationCode}."))
            .ToListAsync(cancellationToken);
        return await FinalizeSuggestionsAsync(warehouse, InventoryDirectedTaskType.PutAway, rows, cancellationToken);
    }

    private async Task<List<InventoryDirectedSuggestionDto>> BuildQuarantinePutAwaySuggestionsAsync(
        Warehouse warehouse,
        CancellationToken cancellationToken)
    {
        var allowed = new[]
        {
            ProcurementReceiptInspectionStatus.Approved,
            ProcurementReceiptInspectionStatus.QualityHold,
            ProcurementReceiptInspectionStatus.ReturnPending,
            ProcurementReceiptInspectionStatus.ReplacementPending,
            ProcurementReceiptInspectionStatus.ClosureReady,
            ProcurementReceiptInspectionStatus.Closed
        };
        var rows = await _unitOfWork.Repository<ProcurementReceiptInspectionLine>().GetQueryable().AsNoTracking()
            .Where(line => line.TenantId == TenantId && !line.IsDeleted && line.RejectedQuantity > 0 &&
                           line.QuarantineLocationId.HasValue && line.QuarantineLocationId != Guid.Empty &&
                           allowed.Contains(line.InspectionCase.Status) && !line.InspectionCase.IsDeleted &&
                           line.QuarantineLocationId != null)
            .Join(Locations.AsNoTracking().Where(location => location.TenantId == TenantId && !location.IsDeleted &&
                                                              location.WarehouseId == warehouse.Id),
                line => line.QuarantineLocationId, location => (Guid?)location.Id, (line, location) => new { line, location })
            .Join(_unitOfWork.Repository<PurchaseOrderReceiptItem>().GetQueryable().AsNoTracking(),
                value => value.line.PurchaseOrderReceiptItemId, receiptLine => receiptLine.Id,
                (value, receiptLine) => new { value.line, value.location, receiptLine })
            .Join(_unitOfWork.Repository<PurchaseOrderItem>().GetQueryable().AsNoTracking()
                    .Where(line => line.InventoryItemId.HasValue),
                value => value.receiptLine.PurchaseOrderItemId, poLine => poLine.Id,
                (value, poLine) => new { value.line, value.location, value.receiptLine, poLine })
            .Join(Items.AsNoTracking(), value => value.poLine.InventoryItemId!.Value, item => item.Id,
                (value, item) => new CandidateRow(
                    item.Id, item.ItemCode, item.Name,
                    value.receiptLine.LocationId, null,
                    value.location.Id, value.location.LocationCode,
                    value.line.RejectedQuantity, "ProcurementReceiptInspection", value.line.InspectionCaseId,
                    value.line.Id, $"INS-{value.line.InspectionCaseId.ToString().Substring(0, 8)}", true,
                    $"Place rejected receipt quantity in quarantine bin {value.location.LocationCode}."))
            .ToListAsync(cancellationToken);
        return await FinalizeSuggestionsAsync(warehouse, InventoryDirectedTaskType.PutAway, rows, cancellationToken);
    }

    private async Task<List<InventoryDirectedSuggestionDto>> BuildPickingSuggestionsAsync(
        Warehouse warehouse,
        CancellationToken cancellationToken)
    {
        var statuses = new[] { RequisitionStatus.Approved, RequisitionStatus.InProgress, RequisitionStatus.PartiallyIssued };
        var lines = await _unitOfWork.Repository<InventoryRequisitionItem>().GetQueryable().AsNoTracking()
            .Where(line => line.TenantId == TenantId && !line.IsDeleted &&
                           line.ApprovedQuantity > line.IssuedQuantity &&
                           line.InventoryRequisition.TenantId == TenantId &&
                           !line.InventoryRequisition.IsDeleted && line.InventoryRequisition.WarehouseId == warehouse.Id &&
                           statuses.Contains(line.InventoryRequisition.Status) && !line.InventoryItem.IsDeleted)
            .Select(line => new
            {
                Line = line,
                Remaining = line.ApprovedQuantity - line.IssuedQuantity,
                line.InventoryRequisition.RequisitionNumber,
                line.InventoryItem.ItemCode,
                ItemName = line.InventoryItem.Name
            }).ToListAsync(cancellationToken);
        var stock = await InventoryLocations.AsNoTracking()
            .Where(value => value.TenantId == TenantId && !value.IsDeleted && value.AvailableQuantity > 0 &&
                            value.Location.TenantId == TenantId && !value.Location.IsDeleted &&
                            value.Location.WarehouseId == warehouse.Id && value.Location.IsActive &&
                            value.Location.IsPickingLocation && !value.Location.IsQuarantineLocation &&
                            !value.Location.IsDamageLocation && !value.Location.IsInTransitLocation)
            .Select(value => new
            {
                value.InventoryItemId, value.LocationId, value.Location.LocationCode,
                value.Location.PickSequence, value.AvailableQuantity
            }).ToListAsync(cancellationToken);
        var rows = new List<CandidateRow>();
        foreach (var line in lines)
        {
            var remaining = line.Remaining;
            foreach (var location in stock.Where(value => value.InventoryItemId == line.Line.InventoryItemId)
                         .OrderBy(value => value.PickSequence).ThenBy(value => value.LocationCode))
            {
                if (remaining <= 0) break;
                var quantity = Math.Min(remaining, location.AvailableQuantity);
                if (quantity <= 0) continue;
                rows.Add(new CandidateRow(line.Line.InventoryItemId, line.ItemCode, line.ItemName,
                    location.LocationId, location.LocationCode, null, null, quantity,
                    "InventoryRequisition", line.Line.InventoryRequisitionId, line.Line.Id,
                    line.RequisitionNumber, false,
                    $"Pick {quantity:0.####} from bin {location.LocationCode} for the approved requisition."));
                remaining -= quantity;
            }
        }
        return await FinalizeSuggestionsAsync(warehouse, InventoryDirectedTaskType.Picking, rows, cancellationToken);
    }

    private async Task<List<InventoryDirectedSuggestionDto>> BuildReplenishmentSuggestionsAsync(
        Warehouse warehouse,
        CancellationToken cancellationToken)
    {
        var locationStock = await InventoryLocations.AsNoTracking()
            .Where(value => value.TenantId == TenantId && !value.IsDeleted &&
                            value.Location.TenantId == TenantId && !value.Location.IsDeleted &&
                            value.Location.WarehouseId == warehouse.Id && value.Location.IsActive &&
                            !value.InventoryItem.IsDeleted)
            .Select(value => new
            {
                value.Id, value.InventoryItemId, value.AvailableQuantity,
                value.InventoryItem.ItemCode, ItemName = value.InventoryItem.Name,
                value.InventoryItem.ReorderLevel, value.InventoryItem.ReorderQuantity,
                value.LocationId, value.Location.LocationCode, value.Location.PickSequence,
                value.Location.IsPickingLocation, value.Location.IsQuarantineLocation,
                value.Location.IsDamageLocation, value.Location.IsInTransitLocation,
                value.Location.IsReceivingLocation
            }).ToListAsync(cancellationToken);
        var rows = new List<CandidateRow>();
        foreach (var destination in locationStock.Where(value => value.IsPickingLocation &&
                     !value.IsQuarantineLocation && !value.IsDamageLocation && !value.IsInTransitLocation &&
                     value.ReorderLevel > 0 && value.AvailableQuantity < value.ReorderLevel)
                 .OrderBy(value => value.PickSequence).ThenBy(value => value.LocationCode))
        {
            var source = locationStock.Where(value => value.InventoryItemId == destination.InventoryItemId &&
                                                       value.LocationId != destination.LocationId &&
                                                       !value.IsPickingLocation && !value.IsQuarantineLocation &&
                                                       !value.IsDamageLocation && !value.IsInTransitLocation &&
                                                       value.AvailableQuantity > 0)
                .OrderByDescending(value => value.IsReceivingLocation)
                .ThenByDescending(value => value.AvailableQuantity)
                .ThenBy(value => value.LocationCode).FirstOrDefault();
            if (source is null) continue;
            var requested = Math.Max(destination.ReorderQuantity, destination.ReorderLevel - destination.AvailableQuantity);
            var quantity = Math.Min(requested, source.AvailableQuantity);
            if (quantity <= 0) continue;
            rows.Add(new CandidateRow(destination.InventoryItemId, destination.ItemCode, destination.ItemName,
                source.LocationId, source.LocationCode, destination.LocationId, destination.LocationCode,
                quantity, "InventoryLocationReplenishment", destination.Id, source.Id,
                destination.LocationCode, false,
                $"Replenish picking bin {destination.LocationCode} from reserve bin {source.LocationCode}."));
        }
        return await FinalizeSuggestionsAsync(warehouse, InventoryDirectedTaskType.Replenishment, rows, cancellationToken);
    }

    private async Task<List<InventoryDirectedSuggestionDto>> FinalizeSuggestionsAsync(
        Warehouse warehouse,
        InventoryDirectedTaskType taskType,
        IEnumerable<CandidateRow> rows,
        CancellationToken cancellationToken)
    {
        var result = new List<InventoryDirectedSuggestionDto>();
        foreach (var row in rows)
        {
            InventoryLocationCapacityDto? capacity = null;
            if (row.DestinationLocationId.HasValue)
            {
                capacity = await GetCapacityAsync(row.DestinationLocationId.Value, row.InventoryItemId,
                    taskType == InventoryDirectedTaskType.PutAway && !row.IsQuarantine ? 0m : row.Quantity,
                    cancellationToken);
                if (!capacity.HasCapacity || row.IsQuarantine != capacity.IsQuarantineLocation) continue;
            }
            var suggestion = new InventoryDirectedSuggestionDto
            {
                TaskType = taskType,
                WarehouseId = warehouse.Id,
                WarehouseCode = warehouse.Code,
                InventoryItemId = row.InventoryItemId,
                ItemCode = row.ItemCode,
                ItemName = row.ItemName,
                SourceLocationId = row.SourceLocationId,
                SourceLocationCode = row.SourceLocationCode,
                DestinationLocationId = row.DestinationLocationId,
                DestinationLocationCode = row.DestinationLocationCode,
                Quantity = row.Quantity,
                SourceDocumentType = row.SourceDocumentType,
                SourceDocumentId = row.SourceDocumentId,
                SourceLineId = row.SourceLineId,
                SourceReference = row.SourceReference,
                IsQuarantine = row.IsQuarantine,
                Explanation = row.Explanation,
                DestinationCapacity = capacity
            };
            suggestion.SuggestionKey = Hash(new
            {
                TenantId,
                suggestion.TaskType,
                suggestion.WarehouseId,
                suggestion.InventoryItemId,
                suggestion.SourceLocationId,
                suggestion.DestinationLocationId,
                suggestion.Quantity,
                suggestion.SourceDocumentType,
                suggestion.SourceDocumentId,
                suggestion.SourceLineId,
                suggestion.IsQuarantine
            });
            result.Add(suggestion);
        }
        return result;
    }

    private async Task<InventoryLocationCapacityDto> GetCapacityAsync(
        Guid locationId,
        Guid incomingItemId,
        decimal quantity,
        CancellationToken cancellationToken)
    {
        var location = await Locations.AsNoTracking().SingleOrDefaultAsync(value =>
            value.TenantId == TenantId && value.Id == locationId && !value.IsDeleted, cancellationToken)
            ?? throw new InventoryDirectedOperationNotFoundException("The destination location was not found in the current tenant.");
        var incomingItem = await Items.AsNoTracking().SingleOrDefaultAsync(value =>
            value.TenantId == TenantId && value.Id == incomingItemId && !value.IsDeleted, cancellationToken)
            ?? throw new InventoryDirectedOperationNotFoundException("The inventory item was not found in the current tenant.");
        var balances = await InventoryLocations.AsNoTracking()
            .Where(value => value.TenantId == TenantId && !value.IsDeleted && value.LocationId == locationId &&
                            value.Quantity > 0 && !value.InventoryItem.IsDeleted)
            .Select(value => new
            {
                value.InventoryItemId,
                value.Quantity,
                Weight = value.InventoryItem.Weight ?? value.InventoryItem.ShippingWeight,
                Volume = value.InventoryItem.Volume ?? 0m
            }).ToListAsync(cancellationToken);
        var usedWeight = balances.Sum(value => value.Quantity * value.Weight);
        var usedVolume = balances.Sum(value => value.Quantity * value.Volume);
        var usedSlots = balances.Select(value => value.InventoryItemId).Distinct().Count();
        var incomingWeight = quantity * (incomingItem.Weight ?? incomingItem.ShippingWeight);
        var incomingVolume = quantity * (incomingItem.Volume ?? 0m);
        var incomingSlots = balances.Any(value => value.InventoryItemId == incomingItemId) ? 0 : 1;
        var issues = new List<string>();
        if (!location.IsActive) issues.Add("The destination location is inactive.");
        if (location.DedicatedItemId.HasValue && location.DedicatedItemId != incomingItemId)
            issues.Add("The destination location is dedicated to another inventory item.");
        if (location.MaxWeight.HasValue && usedWeight + incomingWeight > location.MaxWeight.Value)
            issues.Add("The destination maximum weight would be exceeded.");
        if (location.MaxVolume.HasValue && usedVolume + incomingVolume > location.MaxVolume.Value)
            issues.Add("The destination maximum volume would be exceeded.");
        if (location.MaxItems.HasValue && usedSlots + incomingSlots > location.MaxItems.Value)
            issues.Add("The destination maximum distinct-item capacity would be exceeded.");
        return new InventoryLocationCapacityDto
        {
            LocationId = location.Id,
            LocationCode = location.LocationCode,
            LocationName = location.Name ?? location.LocationCode,
            IsActive = location.IsActive,
            IsPickingLocation = location.IsPickingLocation,
            IsReceivingLocation = location.IsReceivingLocation,
            IsQuarantineLocation = location.IsQuarantineLocation,
            UsedWeight = usedWeight,
            MaxWeight = location.MaxWeight,
            UsedVolume = usedVolume,
            MaxVolume = location.MaxVolume,
            UsedItemSlots = usedSlots,
            MaxItemSlots = location.MaxItems,
            IncomingWeight = incomingWeight,
            IncomingVolume = incomingVolume,
            IncomingItemSlots = incomingSlots,
            HasCapacity = issues.Count == 0,
            CapacityIssues = issues
        };
    }

    private async Task ValidateTaskSourceAsync(InventoryDirectedTask task, CancellationToken cancellationToken)
    {
        var locationIds = new[] { task.SourceLocationId, task.DestinationLocationId }
            .Where(value => value.HasValue).Select(value => value!.Value).Distinct().ToList();
        var validLocationCount = await Locations.AsNoTracking().CountAsync(value =>
            value.TenantId == TenantId && !value.IsDeleted && value.WarehouseId == task.WarehouseId &&
            locationIds.Contains(value.Id), cancellationToken);
        if (validLocationCount != locationIds.Count)
            throw new InventoryDirectedOperationConflictException("INV_DIRECTED_LOCATION_STALE",
                "A directed source or destination bin no longer belongs to the task warehouse.");

        if (task.TaskType == InventoryDirectedTaskType.PutAway && task.SourceDocumentType == "GoodsReceiptNote")
        {
            var current = await _unitOfWork.Repository<GoodsReceiptNoteItem>().GetQueryable().AsNoTracking()
                .Where(value => value.TenantId == TenantId && value.Id == task.SourceLineId &&
                                value.GoodsReceiptNoteId == task.SourceDocumentId && !value.IsDeleted &&
                                value.GoodsReceiptNote.StockUpdated && !value.GoodsReceiptNote.IsDeleted)
                .Select(value => new { value.AcceptedQuantity, value.StorageLocationId,
                    value.GoodsReceiptNote.ReceivingLocationId }).SingleOrDefaultAsync(cancellationToken);
            if (current is null || current.AcceptedQuantity < task.Quantity ||
                current.ReceivingLocationId != task.SourceLocationId || current.StorageLocationId != task.DestinationLocationId)
                throw new InventoryDirectedOperationConflictException("INV_DIRECTED_SOURCE_STALE",
                    "The accepted receipt placement no longer matches this directed task.");
        }
        if (task.TaskType == InventoryDirectedTaskType.PutAway && task.SourceDocumentType == "ProcurementReceiptInspection")
        {
            var current = await _unitOfWork.Repository<ProcurementReceiptInspectionLine>().GetQueryable().AsNoTracking()
                .Where(value => value.TenantId == TenantId && value.Id == task.SourceLineId &&
                                value.InspectionCaseId == task.SourceDocumentId && !value.IsDeleted)
                .Select(value => new { value.RejectedQuantity, value.QuarantineLocationId,
                    value.InspectionCase.Status }).SingleOrDefaultAsync(cancellationToken);
            if (current is null || current.RejectedQuantity < task.Quantity ||
                current.QuarantineLocationId != task.DestinationLocationId ||
                current.Status is ProcurementReceiptInspectionStatus.Draft or
                    ProcurementReceiptInspectionStatus.PendingApproval or
                    ProcurementReceiptInspectionStatus.Rejected or
                    ProcurementReceiptInspectionStatus.RevalidationFailed or
                    ProcurementReceiptInspectionStatus.Cancelled)
                throw new InventoryDirectedOperationConflictException("INV_DIRECTED_SOURCE_STALE",
                    "The rejected inspection placement no longer matches this directed task.");
        }
        if (task.TaskType == InventoryDirectedTaskType.Picking)
        {
            var line = await _unitOfWork.Repository<InventoryRequisitionItem>().GetQueryable().AsNoTracking()
                .SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == task.SourceLineId &&
                    value.InventoryRequisitionId == task.SourceDocumentId && !value.IsDeleted, cancellationToken)
                ?? throw new InventoryDirectedOperationConflictException("INV_DIRECTED_SOURCE_MISSING", "The requisition line no longer exists.");
            if (line.ApprovedQuantity - line.IssuedQuantity < task.Quantity)
                throw new InventoryDirectedOperationConflictException("INV_DIRECTED_SOURCE_STALE", "The remaining approved requisition quantity is below the directed pick quantity.");
        }
        if (task.TaskType is InventoryDirectedTaskType.Picking or InventoryDirectedTaskType.Replenishment)
        {
            var available = await InventoryLocations.AsNoTracking()
                .Where(value => value.TenantId == TenantId && !value.IsDeleted &&
                                value.InventoryItemId == task.InventoryItemId && value.LocationId == task.SourceLocationId)
                .Select(value => (decimal?)value.AvailableQuantity).SingleOrDefaultAsync(cancellationToken) ?? 0m;
            if (available < task.Quantity)
                throw new InventoryDirectedOperationConflictException("INV_DIRECTED_STOCK_STALE", "The source bin no longer has enough available stock.");
        }
        if (task.TaskType == InventoryDirectedTaskType.Replenishment)
        {
            var sourceMatches = await InventoryLocations.AsNoTracking().AnyAsync(value =>
                value.TenantId == TenantId && !value.IsDeleted && value.Id == task.SourceLineId &&
                value.LocationId == task.SourceLocationId && value.InventoryItemId == task.InventoryItemId,
                cancellationToken);
            var destinationMatches = await InventoryLocations.AsNoTracking().AnyAsync(value =>
                value.TenantId == TenantId && !value.IsDeleted && value.Id == task.SourceDocumentId &&
                value.LocationId == task.DestinationLocationId && value.InventoryItemId == task.InventoryItemId,
                cancellationToken);
            if (!sourceMatches || !destinationMatches)
                throw new InventoryDirectedOperationConflictException("INV_DIRECTED_SOURCE_STALE",
                    "The replenishment bin balances no longer match this directed task.");
        }
    }

    private static void EnsureCapacity(InventoryDirectedTask task, InventoryLocationCapacityDto capacity)
    {
        if (!capacity.HasCapacity)
            throw new InventoryDirectedOperationConflictException("INV_DIRECTED_CAPACITY_EXCEEDED",
                string.Join(" ", capacity.CapacityIssues));
        if (task.IsQuarantine && !capacity.IsQuarantineLocation)
            throw new InventoryDirectedOperationConflictException("INV_DIRECTED_QUARANTINE_REQUIRED",
                "Rejected receipt stock must be placed in a quarantine location.");
        if (!task.IsQuarantine && capacity.IsQuarantineLocation)
            throw new InventoryDirectedOperationConflictException("INV_DIRECTED_QUARANTINE_MISMATCH",
                "Accepted or replenishment stock cannot be placed in a quarantine location.");
    }

    private async Task<bool> CanAccessSuggestionAsync(InventoryDirectedSuggestionDto suggestion, CancellationToken cancellationToken)
    {
        var permission = PermissionFor(suggestion.TaskType);
        if (suggestion.SourceLocationId.HasValue &&
            !await CanAccessLocationAsync(permission, suggestion.WarehouseId, suggestion.SourceLocationId.Value,
                suggestion.SourceReference, cancellationToken)) return false;
        if (suggestion.DestinationLocationId.HasValue &&
            !await CanAccessLocationAsync(permission, suggestion.WarehouseId, suggestion.DestinationLocationId.Value,
                suggestion.SourceReference, cancellationToken)) return false;
        return suggestion.SourceLocationId.HasValue || suggestion.DestinationLocationId.HasValue
            ? true
            : await CanAccessLocationAsync(permission, suggestion.WarehouseId, null, suggestion.SourceReference, cancellationToken);
    }

    private Task<bool> CanAccessTaskAsync(InventoryDirectedTask task, CancellationToken cancellationToken) =>
        CanAccessSuggestionAsync(new InventoryDirectedSuggestionDto
        {
            TaskType = task.TaskType,
            WarehouseId = task.WarehouseId,
            SourceLocationId = task.SourceLocationId,
            DestinationLocationId = task.DestinationLocationId,
            SourceReference = task.TaskNumber
        }, cancellationToken);

    private async Task<bool> CanAccessLocationAsync(
        string permission,
        Guid warehouseId,
        Guid? locationId,
        string reference,
        CancellationToken cancellationToken)
    {
        try
        {
            var decision = await _access.CheckCapabilityAsync(new ProcurementAccessCapabilityRequest
            {
                PermissionCode = permission,
                WarehouseId = warehouseId,
                LocationId = locationId,
                RequireLocationScope = true,
                SourceType = "InventoryDirectedOperation",
                SourceReference = reference
            }, Guid.NewGuid().ToString("N"), cancellationToken);
            return decision.Allowed;
        }
        catch (Exception exception) when (exception is ProcurementAccessAuthorizationException or ProcurementAccessValidationException)
        {
            return false;
        }
    }

    private async Task<InventoryDirectedTask?> LoadTaskAsync(Guid id, bool tracked, CancellationToken cancellationToken)
    {
        IQueryable<InventoryDirectedTask> query = Tasks
            .Where(value => value.TenantId == TenantId && value.Id == id && !value.IsDeleted)
            .Include(value => value.Warehouse).Include(value => value.InventoryItem)
            .Include(value => value.SourceLocation).Include(value => value.DestinationLocation)
            .Include(value => value.AssignedToUser).Include(value => value.Actions);
        if (!tracked) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    private async Task AddActionAsync(
        InventoryDirectedTask task,
        InventoryDirectedTaskActionType actionType,
        InventoryDirectedTaskStatus status,
        string comment,
        object? payload,
        string correlationId)
    {
        var sequence = await Actions.CountAsync(value => value.TenantId == TenantId && value.TaskId == task.Id && !value.IsDeleted) +
                       task.Actions.Count(value => value.Id != Guid.Empty && value.CreatedAt == default) + 1;
        var previousHash = task.Actions.OrderByDescending(value => value.Sequence).Select(value => value.IntegrityHash).FirstOrDefault()
                           ?? await Actions.AsNoTracking().Where(value => value.TenantId == TenantId && value.TaskId == task.Id && !value.IsDeleted)
                               .OrderByDescending(value => value.Sequence).Select(value => value.IntegrityHash).FirstOrDefaultAsync()
                           ?? string.Empty;
        var now = DateTime.UtcNow;
        var normalizedCorrelation = NormalizeCorrelation(correlationId);
        var payloadJson = JsonSerializer.Serialize(payload ?? new { });
        var action = new InventoryDirectedTaskAction
        {
            TenantId = TenantId,
            TaskId = task.Id,
            Sequence = sequence,
            ActionType = actionType,
            StatusAfter = status,
            ActorUserId = UserId,
            ActorName = string.IsNullOrWhiteSpace(_currentUser.FullName) ? _currentUser.Username : _currentUser.FullName,
            OccurredAtUtc = now,
            Comment = comment,
            PayloadJson = payloadJson,
            CorrelationId = normalizedCorrelation
        };
        action.IntegrityHash = Hash(new { previousHash, action.TaskId, action.Sequence, action.ActionType,
            action.StatusAfter, action.ActorUserId, action.OccurredAtUtc, action.Comment, action.PayloadJson, action.CorrelationId });
        task.Actions.Add(action);
        await _unitOfWork.Repository<InventoryDirectedTaskAction>().AddAsync(action);
    }

    private async Task AddAuditAsync(string action, InventoryDirectedTask task, object? before, object? after, string correlationId)
    {
        await _unitOfWork.Repository<AuditLog>().AddAsync(new AuditLog
        {
            TenantId = TenantId,
            UserId = UserId,
            Username = string.IsNullOrWhiteSpace(_currentUser.Username) ? "Unknown" : _currentUser.Username,
            Action = action,
            Resource = "InventoryDirectedOperation",
            ResourceId = task.Id.ToString(),
            OldValues = before is null ? null : JsonSerializer.Serialize(before),
            NewValues = after is null ? null : JsonSerializer.Serialize(after),
            IpAddress = "Service",
            UserAgent = $"Correlation:{NormalizeCorrelation(correlationId)}",
            Timestamp = DateTime.UtcNow
        });
    }

    private async Task EnsureActiveTenantUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var active = await _unitOfWork.Repository<UserTenant>().GetQueryable().AsNoTracking()
            .AnyAsync(value => value.TenantId == TenantId && value.UserId == userId && !value.IsDeleted &&
                               value.Status == UserTenantStatus.Active &&
                               (!value.ExpiresAt.HasValue || value.ExpiresAt > now) && value.User.IsActive &&
                               !value.User.UserRoles.Any(role => role.Role.Name == Constants.Roles.ExternalUser),
                cancellationToken);
        if (!active)
            throw new InventoryDirectedOperationConflictException("INV_DIRECTED_ASSIGNEE_INVALID",
                "The assignee is not an active internal user in the current tenant.");
    }

    private async Task<string> GenerateTaskNumberAsync(DateTime now, CancellationToken cancellationToken)
    {
        var prefix = $"DWO-{now:yyyyMMdd}-";
        var count = await Tasks.CountAsync(value => value.TenantId == TenantId && value.TaskNumber.StartsWith(prefix), cancellationToken);
        return $"{prefix}{count + 1:D5}";
    }

    private static InventoryDirectedTaskDto Map(InventoryDirectedTask task) => new()
    {
        Id = task.Id,
        TaskNumber = task.TaskNumber,
        TaskType = task.TaskType,
        Status = task.Status,
        WarehouseId = task.WarehouseId,
        WarehouseCode = task.Warehouse.Code,
        InventoryItemId = task.InventoryItemId,
        ItemCode = task.InventoryItem.ItemCode,
        ItemName = task.InventoryItem.Name,
        SourceLocationId = task.SourceLocationId,
        SourceLocationCode = task.SourceLocation?.LocationCode,
        DestinationLocationId = task.DestinationLocationId,
        DestinationLocationCode = task.DestinationLocation?.LocationCode,
        Quantity = task.Quantity,
        SourceDocumentType = task.SourceDocumentType,
        SourceDocumentId = task.SourceDocumentId,
        SourceLineId = task.SourceLineId,
        SourceReference = task.SourceReference,
        IsQuarantine = task.IsQuarantine,
        AssignedToUserId = task.AssignedToUserId,
        AssignedToName = string.IsNullOrWhiteSpace(task.AssignedToUser.FullName)
            ? task.AssignedToUser.UserName ?? task.AssignedToUserId.ToString()
            : task.AssignedToUser.FullName,
        LinkedInventoryTransferId = task.LinkedInventoryTransferId,
        AssignedAtUtc = task.AssignedAtUtc,
        DueAtUtc = task.DueAtUtc,
        StartedAtUtc = task.StartedAtUtc,
        CompletedAtUtc = task.CompletedAtUtc,
        Reason = task.Reason,
        Notes = task.Notes,
        RowVersion = Convert.ToBase64String(task.RowVersion),
        Actions = task.Actions.OrderBy(value => value.Sequence).Select(value => new InventoryDirectedTaskActionDto
        {
            Id = value.Id,
            Sequence = value.Sequence,
            ActionType = value.ActionType,
            StatusAfter = value.StatusAfter,
            ActorUserId = value.ActorUserId,
            ActorName = value.ActorName,
            OccurredAtUtc = value.OccurredAtUtc,
            Comment = value.Comment,
            CorrelationId = value.CorrelationId,
            IntegrityHash = value.IntegrityHash
        }).ToList()
    };

    private void EnsureActor()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.IsExternalUser || TenantId == Guid.Empty || UserId == Guid.Empty)
            throw new InventoryDirectedOperationAuthorizationException(
                "An authenticated internal tenant user is required for directed warehouse operations.");
    }

    private void EnsureAssignedActor(InventoryDirectedTask task)
    {
        if (task.AssignedToUserId != UserId && !_currentUser.HasRole("TenantAdmin") && !_currentUser.HasRole("SuperAdmin") && !_currentUser.HasRole("Admin"))
            throw new InventoryDirectedOperationAuthorizationException("Only the assignee or a tenant administrator can mutate this directed task.");
    }

    private static void EnsureRowVersion(InventoryDirectedTask task, string encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded))
            throw new InventoryDirectedOperationConflictException("INV_DIRECTED_ROW_VERSION_REQUIRED", "RowVersion is required.");
        byte[] supplied;
        try { supplied = Convert.FromBase64String(encoded); }
        catch (FormatException)
        {
            throw new InventoryDirectedOperationConflictException("INV_DIRECTED_ROW_VERSION_INVALID", "RowVersion is invalid.");
        }
        if (!task.RowVersion.SequenceEqual(supplied))
            throw new InventoryDirectedOperationConflictException("INV_DIRECTED_CONCURRENCY_CONFLICT",
                "The directed task changed after it was loaded. Refresh and retry.");
    }

    private static InventoryDirectedOperationConflictException StatusConflict(InventoryDirectedTask task, string action) =>
        new("INV_DIRECTED_STATUS_CONFLICT", $"Task {task.TaskNumber} cannot be {action} while it is {task.Status}.");

    private static string PermissionFor(InventoryDirectedTaskType type) => type switch
    {
        InventoryDirectedTaskType.PutAway => "procurement.inventory.receive",
        InventoryDirectedTaskType.Picking => "procurement.inventory.issue",
        InventoryDirectedTaskType.Replenishment => "procurement.inventory.transfer",
        _ => throw new InventoryDirectedOperationConflictException("INV_DIRECTED_TYPE_UNSUPPORTED", "The directed task type is not supported.")
    };

    private static string ClaimKey(InventoryDirectedTaskType type, string sourceType, Guid sourceId, Guid lineId) =>
        $"{(int)type}:{sourceType}:{sourceId:N}:{lineId:N}";

    private static string TaskIntegrity(InventoryDirectedTask task) => Hash(new
    {
        task.Id,
        task.TenantId,
        task.TaskNumber,
        task.TaskType,
        task.Status,
        task.WarehouseId,
        task.InventoryItemId,
        task.SourceLocationId,
        task.DestinationLocationId,
        task.Quantity,
        task.SourceDocumentType,
        task.SourceDocumentId,
        task.SourceLineId,
        task.SuggestionKey,
        task.AssignedToUserId,
        task.LinkedInventoryTransferId,
        task.AssignedAtUtc,
        task.StartedAtUtc,
        task.CompletedAtUtc,
        task.CancelledAtUtc,
        task.CancellationReason,
        task.CorrelationId
    });

    private static string Hash(object value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)))).ToLowerInvariant();

    private static string NormalizeHash(string value, string field)
    {
        var normalized = NormalizeRequired(value, 64, field).ToLowerInvariant();
        if (normalized.Length != 64 || normalized.Any(character => !Uri.IsHexDigit(character)))
            throw new InventoryDirectedOperationConflictException("INV_DIRECTED_HASH_INVALID", $"{field} must be a 64-character hexadecimal hash.");
        return normalized;
    }

    private static string NormalizeRequired(string? value, int maxLength, string field)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
            throw new InventoryDirectedOperationConflictException("INV_DIRECTED_FIELD_REQUIRED", $"{field} is required.");
        if (normalized.Length > maxLength)
            throw new InventoryDirectedOperationConflictException("INV_DIRECTED_FIELD_TOO_LONG", $"{field} cannot exceed {maxLength} characters.");
        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized)) return null;
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string NormalizeCorrelation(string correlationId) =>
        string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId.Trim()[..Math.Min(100, correlationId.Trim().Length)];

    private sealed record CandidateRow(
        Guid InventoryItemId,
        string ItemCode,
        string ItemName,
        Guid? SourceLocationId,
        string? SourceLocationCode,
        Guid? DestinationLocationId,
        string? DestinationLocationCode,
        decimal Quantity,
        string SourceDocumentType,
        Guid SourceDocumentId,
        Guid SourceLineId,
        string SourceReference,
        bool IsQuarantine,
        string Explanation);
}
