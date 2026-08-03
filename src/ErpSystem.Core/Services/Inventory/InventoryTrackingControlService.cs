using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Inventory;

public sealed class InventoryTrackingControlService : IInventoryTrackingControlService
{
    private static readonly HashSet<string> ExceptionEligibleCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "INV_TRACKING_EXPIRED",
        "INV_TRACKING_MINIMUM_SHELF_LIFE",
        "INV_TRACKING_METADATA_MISMATCH",
        "INV_TRACKING_FIFO_VIOLATION"
    };

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementAccessControlService _access;
    private readonly List<InventoryTraceabilityEvent> _pendingEvents = new();

    public InventoryTrackingControlService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService access)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _access = access;
    }

    private Guid TenantId => _currentUser.TenantId;
    private Guid UserId => _currentUser.UserId;
    private IQueryable<InventoryTraceabilityEvent> Events => _unitOfWork.Repository<InventoryTraceabilityEvent>().GetQueryable();
    private IQueryable<InventoryTrackingException> Exceptions => _unitOfWork.Repository<InventoryTrackingException>().GetQueryable();

    public async Task<InventoryTrackingRequirementsDto> GetRequirementsAsync(
        Guid inventoryItemId,
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        var item = await _unitOfWork.Repository<InventoryItem>()
            .GetQueryable(value => value.TenantId == TenantId && value.Id == inventoryItemId && !value.IsDeleted)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InventoryTrackingControlException("INV_TRACKING_ITEM_NOT_FOUND", "The inventory item was not found in the current tenant.");

        var categories = await _unitOfWork.Repository<InventoryCategory>()
            .GetQueryable(value => value.TenantId == TenantId && !value.IsDeleted)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var byId = categories.ToDictionary(value => value.Id);
        var lineage = new List<InventoryCategory>();
        var visited = new HashSet<Guid>();
        var categoryId = item.CategoryId;
        while (byId.TryGetValue(categoryId, out var category) && visited.Add(category.Id))
        {
            lineage.Add(category);
            if (!category.ParentCategoryId.HasValue) break;
            categoryId = category.ParentCategoryId.Value;
        }

        var leaf = lineage.FirstOrDefault()
            ?? throw new InventoryTrackingControlException("INV_TRACKING_CATEGORY_NOT_FOUND", "The item's inventory category was not found in the current tenant.");
        return new InventoryTrackingRequirementsDto
        {
            InventoryItemId = item.Id,
            ItemCode = item.ItemCode,
            ItemName = item.Name,
            CategoryId = leaf.Id,
            CategoryName = leaf.Name,
            RequiresLot = item.IsLotTracked || lineage.Any(value => value.DefaultLotTracking),
            RequiresBatch = item.IsBatchTracked || lineage.Any(value => value.DefaultBatchTracking),
            RequiresSerial = item.IsSerialTracked || lineage.Any(value => value.DefaultSerialTracking),
            RequiresManufactureDate = item.IsManufactureDateTracked || lineage.Any(value => value.DefaultManufactureDateTracking),
            RequiresExpiryDate = item.IsExpirationTracked || lineage.Any(value => value.DefaultExpirationTracking),
            EnforcesFifoIssue = lineage.Any(value => value.EnforceFifoIssue),
            MinimumShelfLifeDays = Math.Max(item.ShelfLifeDays ?? 0, lineage.Max(value => value.MinimumShelfLifeDays)),
            CategoryLineage = lineage.Select(value => value.Id).ToList()
        };
    }

    public async Task ValidateAsync(InventoryTrackingMutationRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateCoreAsync(request, stage: false, cancellationToken);
    }

    public async Task StageEventAsync(InventoryTrackingMutationRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateCoreAsync(request, stage: true, cancellationToken);
    }

    private async Task ValidateCoreAsync(
        InventoryTrackingMutationRequest request,
        bool stage,
        CancellationToken cancellationToken)
    {
        EnsureActor();
        Normalize(request);
        if (request.Quantity <= 0)
            throw new InventoryTrackingControlException("INV_TRACKING_QUANTITY_INVALID", "Tracking quantity must be greater than zero.");
        if (request.WarehouseId == Guid.Empty || request.ReferenceId == Guid.Empty || string.IsNullOrWhiteSpace(request.ReferenceType))
            throw new InventoryTrackingControlException("INV_TRACKING_REFERENCE_INVALID", "Warehouse and transaction reference are required for tracking enforcement.");

        await RequireCapabilityAsync(PermissionFor(request.Direction), request.WarehouseId,
            $"{request.ReferenceType}:{request.ReferenceId:N}", cancellationToken, request.LocationId);

        var requirements = await GetRequirementsAsync(request.InventoryItemId, cancellationToken);
        var item = await _unitOfWork.Repository<InventoryItem>()
            .GetQueryable(value => value.TenantId == TenantId && value.Id == request.InventoryItemId && !value.IsDeleted)
            .AsNoTracking().SingleAsync(cancellationToken);
        await EnsureWarehouseAndLocationAsync(request.WarehouseId, request.LocationId, cancellationToken);

        Require(request.LotNumber, requirements.RequiresLot, "INV_TRACKING_LOT_REQUIRED", "A lot number is required by the item's category tracking policy.");
        Require(request.BatchNumber, requirements.RequiresBatch, "INV_TRACKING_BATCH_REQUIRED", "A batch number is required by the item's category tracking policy.");
        Require(request.SerialNumber, requirements.RequiresSerial, "INV_TRACKING_SERIAL_REQUIRED", "A serial number is required by the item's category tracking policy.");
        if (requirements.RequiresSerial && request.Quantity != 1m)
            throw new InventoryTrackingControlException("INV_TRACKING_SERIAL_QUANTITY_INVALID", "Serial-tracked inventory must be transacted as one unit per tracking line.");

        var inbound = IsInbound(request.Direction);
        var initialReceipt = request.Direction is InventoryTrackingDirection.Receipt or InventoryTrackingDirection.AdjustmentIn;
        if (initialReceipt)
        {
            if (requirements.RequiresManufactureDate && !request.ManufactureDate.HasValue)
                throw new InventoryTrackingControlException("INV_TRACKING_MANUFACTURE_DATE_REQUIRED", "A manufacture date is required by the item's category tracking policy.");
            if (requirements.RequiresExpiryDate && !request.ExpiryDate.HasValue)
                throw new InventoryTrackingControlException("INV_TRACKING_EXPIRY_DATE_REQUIRED", "An expiry date is required by the item's category tracking policy.");
        }

        var violations = new List<string>();
        var now = DateTime.UtcNow;
        if (request.ManufactureDate.HasValue && request.ManufactureDate.Value.ToUniversalTime() > now)
            throw new InventoryTrackingControlException("INV_TRACKING_MANUFACTURE_DATE_INVALID", "Manufacture date cannot be in the future.");
        if (request.ManufactureDate.HasValue && request.ExpiryDate.HasValue &&
            request.ManufactureDate.Value.ToUniversalTime() >= request.ExpiryDate.Value.ToUniversalTime())
            throw new InventoryTrackingControlException("INV_TRACKING_DATES_INVALID", "Expiry date must be later than manufacture date.");
        if (request.Direction != InventoryTrackingDirection.TransferIn &&
            request.ExpiryDate.HasValue && request.ExpiryDate.Value.ToUniversalTime() <= now)
            violations.Add("INV_TRACKING_EXPIRED");
        if (request.Direction != InventoryTrackingDirection.TransferIn &&
            request.ExpiryDate.HasValue && requirements.MinimumShelfLifeDays > 0 &&
            request.ExpiryDate.Value.ToUniversalTime() < now.Date.AddDays(requirements.MinimumShelfLifeDays))
            violations.Add("INV_TRACKING_MINIMUM_SHELF_LIFE");

        if (requirements.EnforcesFifoIssue && item.ValuationMethod != ValuationMethod.FIFO)
            throw new InventoryTrackingControlException("INV_TRACKING_FIFO_CONFIGURATION_INVALID", "The category enforces FIFO issue, but the item is not configured for FIFO valuation.");

        var persisted = await Events
            .Where(value => value.TenantId == TenantId && value.InventoryItemId == request.InventoryItemId && !value.IsDeleted)
            .AsNoTracking()
            .OrderBy(value => value.OccurredAtUtc)
            .ToListAsync(cancellationToken);
        var pending = _pendingEvents.Where(value => value.InventoryItemId == request.InventoryItemId).ToList();
        var existingEvent = stage && !string.IsNullOrWhiteSpace(request.EventKey)
            ? persisted.SingleOrDefault(value => value.EventKey == request.EventKey) ??
              pending.SingleOrDefault(value => value.EventKey == request.EventKey)
            : null;
        // A retried transaction must be evaluated against the state immediately before
        // its own event. Otherwise its already-staged receipt looks like a duplicate
        // serial and its already-staged issue reduces availability a second time.
        var allEvents = persisted.Concat(pending)
            .Where(value => existingEvent is null || value.Id != existingEvent.Id)
            .ToList();

        if (requirements.RequiresSerial && initialReceipt)
        {
            var currentSerial = Balance(allEvents.Where(value => Same(value.SerialNumber, request.SerialNumber)));
            if (currentSerial > 0)
                throw new InventoryTrackingControlException("INV_TRACKING_SERIAL_DUPLICATE", "The serial number is already on hand in this tenant.");
        }

        // Historical, genuinely untracked stock predates the canonical trace ledger. Do not
        // make ordinary (non lot/batch/serial/FIFO) stock unusable merely because TDC-0603
        // deliberately did not synthesize inbound provenance. As soon as tracking metadata or
        // a tracking policy applies, canonical history remains mandatory.
        var requiresCanonicalHistory = requirements.RequiresLot ||
            requirements.RequiresBatch ||
            requirements.RequiresSerial ||
            requirements.RequiresManufactureDate ||
            requirements.RequiresExpiryDate ||
            requirements.EnforcesFifoIssue ||
            !string.IsNullOrWhiteSpace(request.LotNumber) ||
            !string.IsNullOrWhiteSpace(request.BatchNumber) ||
            !string.IsNullOrWhiteSpace(request.SerialNumber) ||
            request.ManufactureDate.HasValue ||
            request.ExpiryDate.HasValue;

        if (!initialReceipt && requiresCanonicalHistory)
        {
            var canonical = allEvents.FirstOrDefault(value => IsInbound(value.Direction) && TrackingKeyMatches(value, request));
            if (canonical is null)
                throw new InventoryTrackingControlException("INV_TRACKING_LOT_NOT_FOUND", "The requested lot, batch, or serial has no traceable inbound history.");

            if (request.ManufactureDate.HasValue && canonical.ManufactureDate.HasValue &&
                request.ManufactureDate.Value.ToUniversalTime() != canonical.ManufactureDate.Value.ToUniversalTime())
                violations.Add("INV_TRACKING_METADATA_MISMATCH");
            if (request.ExpiryDate.HasValue && canonical.ExpiryDate.HasValue &&
                request.ExpiryDate.Value.ToUniversalTime() != canonical.ExpiryDate.Value.ToUniversalTime())
                violations.Add("INV_TRACKING_METADATA_MISMATCH");
            request.ManufactureDate ??= canonical.ManufactureDate;
            request.ExpiryDate ??= canonical.ExpiryDate;
            request.BatchNumber ??= canonical.BatchNumber;
            if (request.Direction != InventoryTrackingDirection.TransferIn &&
                request.ExpiryDate.HasValue && request.ExpiryDate.Value.ToUniversalTime() <= now)
                violations.Add("INV_TRACKING_EXPIRED");
            if (request.Direction != InventoryTrackingDirection.TransferIn &&
                request.ExpiryDate.HasValue && requirements.MinimumShelfLifeDays > 0 &&
                request.ExpiryDate.Value.ToUniversalTime() < now.Date.AddDays(requirements.MinimumShelfLifeDays))
                violations.Add("INV_TRACKING_MINIMUM_SHELF_LIFE");
        }

        if (!inbound && requiresCanonicalHistory)
        {
            var scoped = allEvents.Where(value => value.WarehouseId == request.WarehouseId &&
                (!request.LocationId.HasValue || value.LocationId == request.LocationId) && TrackingKeyMatches(value, request));
            var available = Balance(scoped);
            if (available < request.Quantity)
                throw new InventoryTrackingControlException("INV_TRACKING_LOT_INSUFFICIENT", $"The selected tracked stock has {available.ToString(CultureInfo.InvariantCulture)} units available, below the requested {request.Quantity.ToString(CultureInfo.InvariantCulture)}.");

            if (requirements.EnforcesFifoIssue)
            {
                var openGroups = allEvents
                    .Where(value => value.WarehouseId == request.WarehouseId && (!request.LocationId.HasValue || value.LocationId == request.LocationId))
                    .GroupBy(TrackingKey)
                    .Select(group => new { Key = group.Key, Balance = Balance(group), FirstReceipt = group.Where(value => IsInbound(value.Direction)).Min(value => (DateTime?)value.OccurredAtUtc) })
                    .Where(value => value.Balance > 0 && value.FirstReceipt.HasValue)
                    .OrderBy(value => value.FirstReceipt)
                    .ThenBy(value => value.Key, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (openGroups is not null && !string.Equals(openGroups.Key, TrackingKey(request), StringComparison.Ordinal))
                    violations.Add("INV_TRACKING_FIFO_VIOLATION");
            }
        }

        violations = violations.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        InventoryTrackingException? approvedException = null;
        if (violations.Count > 0)
        {
            if (violations.Any(value => !ExceptionEligibleCodes.Contains(value)))
                throw new InventoryTrackingControlException(violations[0], MessageFor(violations[0]));
            approvedException = await ValidateExceptionAsync(request, violations, cancellationToken);
        }

        if (!stage) return;
        if (string.IsNullOrWhiteSpace(request.EventKey))
            throw new InventoryTrackingControlException("INV_TRACKING_EVENT_KEY_REQUIRED", "A stable transaction event key is required.");
        var payloadHash = HashPayload(request);
        if (existingEvent is not null)
        {
            if (!string.Equals(existingEvent.PayloadHash, payloadHash, StringComparison.Ordinal))
                throw new InventoryTrackingControlException("INV_TRACKING_IDEMPOTENCY_CONFLICT", "The tracking event key was already used with a different payload.");
            return;
        }

        var trackingEvent = new InventoryTraceabilityEvent
        {
            TenantId = TenantId,
            InventoryItemId = request.InventoryItemId,
            WarehouseId = request.WarehouseId,
            LocationId = request.LocationId,
            Direction = request.Direction,
            Quantity = request.Quantity,
            ReferenceType = request.ReferenceType,
            ReferenceNumber = request.ReferenceNumber,
            ReferenceId = request.ReferenceId,
            ReferenceLineId = request.ReferenceLineId,
            EventKey = request.EventKey,
            LotNumber = request.LotNumber,
            BatchNumber = request.BatchNumber,
            SerialNumber = request.SerialNumber,
            ManufactureDate = Utc(request.ManufactureDate),
            ExpiryDate = Utc(request.ExpiryDate),
            TrackingExceptionId = approvedException?.Id,
            ActorUserId = UserId,
            OccurredAtUtc = now,
            CorrelationId = string.IsNullOrWhiteSpace(request.CorrelationId) ? request.EventKey : request.CorrelationId,
            PayloadHash = payloadHash
        };
        await _unitOfWork.Repository<InventoryTraceabilityEvent>().AddAsync(trackingEvent);
        _pendingEvents.Add(trackingEvent);
        if (approvedException is not null)
        {
            approvedException.ConsumedAtUtc = now;
            approvedException.ConsumedByReferenceId = request.ReferenceId;
            approvedException.ConsumedByEventId = trackingEvent.Id;
        }
        await AddAuditAsync("InventoryTracking.Enforced", trackingEvent.Id, null, new
        {
            request.InventoryItemId, request.WarehouseId, request.Direction, request.Quantity,
            request.ReferenceType, request.ReferenceId, request.LotNumber, request.BatchNumber,
            request.SerialNumber, request.ManufactureDate, request.ExpiryDate,
            TrackingExceptionId = approvedException?.Id, Requirements = requirements
        }, trackingEvent.CorrelationId);
    }

    public async Task<InventoryTrackingExceptionDto> RegisterApprovedExceptionAsync(
        RegisterInventoryTrackingExceptionRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        await RequireCapabilityAsync("procurement.inventory.adjust.request", request.WarehouseId,
            $"tracking-exception:{request.ReferenceId:N}", cancellationToken, request.LocationId);
        await GetRequirementsAsync(request.InventoryItemId, cancellationToken);
        await EnsureWarehouseAndLocationAsync(request.WarehouseId, request.LocationId, cancellationToken);
        var codes = request.ExceptionCodes.Select(NormalizeCode).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).ToList();
        if (codes.Count == 0 || codes.Any(value => !ExceptionEligibleCodes.Contains(value)))
            throw new InventoryTrackingControlException("INV_TRACKING_EXCEPTION_CODE_INVALID", "Only expiry, minimum-shelf-life, metadata-mismatch, and FIFO violations may use a tracking exception.");
        var now = DateTime.UtcNow;
        var expires = request.ExpiresAtUtc.ToUniversalTime();
        if (expires <= now || expires > now.AddDays(90))
            throw new InventoryTrackingControlException("INV_TRACKING_EXCEPTION_EXPIRY_INVALID", "The exception must expire within the next 90 days.");

        var workflow = await _unitOfWork.Repository<WorkflowInstance>().GetQueryable(value =>
                value.TenantId == TenantId && value.Id == request.WorkflowInstanceId && !value.IsDeleted)
            .AsNoTracking().Include(value => value.WorkflowDefinition).ThenInclude(value => value.EntityType)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InventoryTrackingControlException("INV_TRACKING_EXCEPTION_WORKFLOW_NOT_FOUND", "The shared workflow instance was not found in the current tenant.");
        if (workflow.Status != WorkflowInstanceStatus.Completed || workflow.EntityId != request.ReferenceId)
            throw new InventoryTrackingControlException("INV_TRACKING_EXCEPTION_WORKFLOW_INVALID", "The shared workflow must be completed and bound to the exact transaction reference.");
        if (!workflow.WorkflowDefinition.EntityType.Code.Contains("EXCEPTION", StringComparison.OrdinalIgnoreCase))
            throw new InventoryTrackingControlException("INV_TRACKING_EXCEPTION_WORKFLOW_TYPE_INVALID", "The completed workflow is not an exception-approval workflow.");

        var approvals = await _unitOfWork.Repository<WorkflowApproval>().GetQueryable(value =>
                value.TenantId == TenantId && !value.IsDeleted &&
                value.StepInstance.WorkflowInstanceId == workflow.Id && value.Status == WorkflowApprovalStatus.Approved)
            .AsNoTracking().OrderByDescending(value => value.ProcessedDate).ToListAsync(cancellationToken);
        var approval = approvals.FirstOrDefault(value => (value.ProcessedById ?? value.ApproverId).HasValue &&
            (value.ProcessedById ?? value.ApproverId) != workflow.InitiatedById)
            ?? throw new InventoryTrackingControlException("INV_TRACKING_EXCEPTION_SOD_INVALID", "The exception requires a completed approval by an actor independent from the workflow initiator.");
        var approvedById = approval.ProcessedById ?? approval.ApproverId!.Value;

        var evidence = await (
                from document in _unitOfWork.Repository<WorkflowEvidenceDocument>().GetQueryable(value =>
                        value.TenantId == TenantId && value.Id == request.WorkflowEvidenceDocumentId && !value.IsDeleted && value.IsCurrent)
                    .AsNoTracking()
                join step in _unitOfWork.Repository<WorkflowStepInstance>().GetQueryable(value =>
                        value.TenantId == TenantId && !value.IsDeleted)
                    .AsNoTracking()
                    on document.StepInstanceId equals step.Id
                where step.WorkflowInstanceId == workflow.Id
                select document)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InventoryTrackingControlException("INV_TRACKING_EXCEPTION_EVIDENCE_NOT_FOUND", "Current shared-workflow evidence was not found for this workflow.");
        if (evidence.VerificationStatus != WorkflowEvidenceVerificationStatus.Verified ||
            evidence.MalwareScanStatus != WorkflowMalwareScanStatus.Clean ||
            (evidence.ExpiryDate.HasValue && evidence.ExpiryDate.Value.ToUniversalTime() <= now))
            throw new InventoryTrackingControlException("INV_TRACKING_EXCEPTION_EVIDENCE_INVALID", "Exception evidence must be current, verified, malware-clean, and unexpired.");
        if (approvedById == UserId)
            throw new InventoryTrackingControlException("INV_TRACKING_EXCEPTION_REGISTRAR_SOD", "The workflow approver cannot register their own tracking exception for consumption.");
        if (await Exceptions.AnyAsync(value => value.TenantId == TenantId && value.WorkflowInstanceId == workflow.Id, cancellationToken))
            throw new InventoryTrackingControlException("INV_TRACKING_EXCEPTION_WORKFLOW_REUSED", "This workflow has already authorized a tracking exception.");

        var entity = new InventoryTrackingException
        {
            TenantId = TenantId,
            InventoryItemId = request.InventoryItemId,
            WarehouseId = request.WarehouseId,
            LocationId = request.LocationId,
            ReferenceId = request.ReferenceId,
            ReferenceLineId = request.ReferenceLineId,
            ReferenceType = request.ReferenceType.Trim(),
            ReferenceNumber = request.ReferenceNumber.Trim(),
            Reason = request.Reason.Trim(),
            ExceptionCodesJson = JsonSerializer.Serialize(codes),
            LotNumber = NormalizeValue(request.LotNumber),
            BatchNumber = NormalizeValue(request.BatchNumber),
            SerialNumber = NormalizeValue(request.SerialNumber),
            WorkflowInstanceId = workflow.Id,
            WorkflowEvidenceDocumentId = evidence.Id,
            RequestedById = workflow.InitiatedById,
            ApprovedById = approvedById,
            ApprovedAtUtc = approval.ProcessedDate?.ToUniversalTime() ?? workflow.CompletedDate?.ToUniversalTime() ?? now,
            ExpiresAtUtc = expires
        };
        entity.IntegrityHash = HashException(entity, evidence.Sha256);
        await _unitOfWork.Repository<InventoryTrackingException>().AddAsync(entity);
        await AddAuditAsync("InventoryTrackingException.Registered", entity.Id, null, new
        {
            entity.InventoryItemId, entity.WarehouseId, entity.ReferenceId, Codes = codes,
            entity.WorkflowInstanceId, entity.WorkflowEvidenceDocumentId, entity.ApprovedById,
            entity.ApprovedAtUtc, entity.ExpiresAtUtc, entity.IntegrityHash
        }, correlationId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await MapExceptionAsync(entity.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<InventoryTrackingExceptionDto>> GetExceptionsAsync(int take = 100, CancellationToken cancellationToken = default)
    {
        EnsureActor();
        var readableWarehouseIds = await GetReadableWarehouseIdsAsync("tracking-exceptions", cancellationToken);
        take = Math.Clamp(take, 1, 500);
        var values = await Exceptions.Where(value => value.TenantId == TenantId && !value.IsDeleted &&
                readableWarehouseIds.Contains(value.WarehouseId))
            .AsNoTracking().Include(value => value.InventoryItem).Include(value => value.Warehouse)
            .OrderByDescending(value => value.ApprovedAtUtc).Take(take).ToListAsync(cancellationToken);
        return values.Select(MapException).ToList();
    }

    public async Task<IReadOnlyList<InventoryTraceabilityEventDto>> GetEventsAsync(
        Guid? inventoryItemId = null,
        Guid? warehouseId = null,
        int take = 200,
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        IReadOnlyCollection<Guid> readableWarehouseIds;
        if (warehouseId.HasValue)
        {
            await RequireCapabilityAsync("procurement.inventory.read", warehouseId, "tracking-events", cancellationToken);
            readableWarehouseIds = new[] { warehouseId.Value };
        }
        else
        {
            readableWarehouseIds = await GetReadableWarehouseIdsAsync("tracking-events", cancellationToken);
        }
        take = Math.Clamp(take, 1, 1000);
        var query = Events.Where(value => value.TenantId == TenantId && !value.IsDeleted &&
            readableWarehouseIds.Contains(value.WarehouseId));
        if (inventoryItemId.HasValue) query = query.Where(value => value.InventoryItemId == inventoryItemId.Value);
        var values = await query.AsNoTracking()
            .Include(value => value.InventoryItem)
            .Include(value => value.Warehouse)
            .OrderByDescending(value => value.OccurredAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);
        return values.Select(value => new InventoryTraceabilityEventDto
        {
            Id = value.Id, InventoryItemId = value.InventoryItemId, ItemCode = value.InventoryItem.ItemCode,
            ItemName = value.InventoryItem.Name, WarehouseId = value.WarehouseId, WarehouseName = value.Warehouse.Name,
            Direction = value.Direction.ToString(), Quantity = value.Quantity, ReferenceType = value.ReferenceType,
            ReferenceNumber = value.ReferenceNumber, LotNumber = value.LotNumber, BatchNumber = value.BatchNumber,
            SerialNumber = value.SerialNumber, ManufactureDate = value.ManufactureDate, ExpiryDate = value.ExpiryDate,
            TrackingExceptionId = value.TrackingExceptionId, OccurredAtUtc = value.OccurredAtUtc
        }).ToList();
    }

    private async Task<IReadOnlyCollection<Guid>> GetReadableWarehouseIdsAsync(
        string reference,
        CancellationToken cancellationToken)
    {
        var warehouseIds = await _unitOfWork.Repository<Warehouse>()
            .GetQueryable(value => value.TenantId == TenantId && !value.IsDeleted && value.IsActive)
            .AsNoTracking()
            .Select(value => value.Id)
            .ToListAsync(cancellationToken);
        if (warehouseIds.Count == 0) return warehouseIds;

        var readable = new List<Guid>(warehouseIds.Count);
        foreach (var candidateId in warehouseIds)
        {
            var decision = await _access.CheckCapabilityAsync(new ProcurementAccessCapabilityRequest
            {
                PermissionCode = "procurement.inventory.read",
                WarehouseId = candidateId,
                RequireLocationScope = true,
                SourceType = "InventoryTrackingControl",
                SourceReference = reference
            }, reference, cancellationToken);
            if (decision.Allowed) readable.Add(candidateId);
        }

        if (readable.Count == 0)
            throw new InventoryTrackingAuthorizationException(
                "The current actor has no assigned warehouse granting inventory read access.");
        return readable;
    }

    private async Task<InventoryTrackingException> ValidateExceptionAsync(
        InventoryTrackingMutationRequest request,
        IReadOnlyCollection<string> violations,
        CancellationToken cancellationToken)
    {
        if (!request.TrackingExceptionId.HasValue)
            throw new InventoryTrackingControlException(violations.First(), MessageFor(violations.First()));
        var entity = await Exceptions.SingleOrDefaultAsync(value => value.TenantId == TenantId &&
            value.Id == request.TrackingExceptionId.Value && !value.IsDeleted, cancellationToken)
            ?? throw new InventoryTrackingControlException("INV_TRACKING_EXCEPTION_NOT_FOUND", "The tracking exception was not found in the current tenant.");
        var codes = JsonSerializer.Deserialize<List<string>>(entity.ExceptionCodesJson) ?? new List<string>();
        if (entity.ConsumedAtUtc.HasValue || entity.ExpiresAtUtc <= DateTime.UtcNow || entity.InventoryItemId != request.InventoryItemId ||
            entity.WarehouseId != request.WarehouseId || entity.ReferenceId != request.ReferenceId ||
            (entity.ReferenceLineId.HasValue && entity.ReferenceLineId != request.ReferenceLineId) ||
            (entity.LocationId.HasValue && entity.LocationId != request.LocationId) ||
            !violations.All(value => codes.Contains(value, StringComparer.OrdinalIgnoreCase)) ||
            !OptionalMatch(entity.LotNumber, request.LotNumber) || !OptionalMatch(entity.BatchNumber, request.BatchNumber) ||
            !OptionalMatch(entity.SerialNumber, request.SerialNumber))
            throw new InventoryTrackingControlException("INV_TRACKING_EXCEPTION_SCOPE_INVALID", "The approved exception is expired, consumed, or does not cover the exact item, warehouse, transaction, tracking values, and violation codes.");

        var workflow = await _unitOfWork.Repository<WorkflowInstance>().GetQueryable(value => value.TenantId == TenantId && value.Id == entity.WorkflowInstanceId)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        var evidence = await _unitOfWork.Repository<WorkflowEvidenceDocument>().GetQueryable(value => value.TenantId == TenantId && value.Id == entity.WorkflowEvidenceDocumentId)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (workflow?.Status != WorkflowInstanceStatus.Completed || evidence is null || !evidence.IsCurrent ||
            evidence.VerificationStatus != WorkflowEvidenceVerificationStatus.Verified || evidence.MalwareScanStatus != WorkflowMalwareScanStatus.Clean ||
            !string.Equals(entity.IntegrityHash, HashException(entity, evidence.Sha256), StringComparison.Ordinal))
            throw new InventoryTrackingControlException("INV_TRACKING_EXCEPTION_STALE", "The exception workflow, evidence, or integrity snapshot is no longer valid.");
        return entity;
    }

    private async Task EnsureWarehouseAndLocationAsync(Guid warehouseId, Guid? locationId, CancellationToken cancellationToken)
    {
        var warehouseExists = await _unitOfWork.Repository<Warehouse>().GetQueryable(value =>
            value.TenantId == TenantId && value.Id == warehouseId && !value.IsDeleted).AnyAsync(cancellationToken);
        if (!warehouseExists)
            throw new InventoryTrackingControlException("INV_TRACKING_WAREHOUSE_NOT_FOUND", "The warehouse was not found in the current tenant.");
        if (!locationId.HasValue) return;
        var locationValid = await _unitOfWork.Repository<WarehouseLocation>().GetQueryable(value =>
            value.TenantId == TenantId && value.Id == locationId.Value && !value.IsDeleted &&
            (value.WarehouseId == warehouseId || (value.IsConsignmentBin && value.ConsignmentWarehouseId == warehouseId)))
            .AnyAsync(cancellationToken);
        if (!locationValid)
            throw new InventoryTrackingControlException("INV_TRACKING_LOCATION_INVALID", "The location does not belong to the selected warehouse in the current tenant.");
    }

    private async Task RequireCapabilityAsync(
        string permission,
        Guid? warehouseId,
        string reference,
        CancellationToken cancellationToken,
        Guid? locationId = null)
    {
        var request = new ProcurementAccessCapabilityRequest
        {
            PermissionCode = permission,
            WarehouseId = warehouseId,
            LocationId = locationId,
            RequireLocationScope = true,
            SourceType = "InventoryTrackingControl",
            SourceReference = reference
        };
        var decision = await _access.CheckCapabilityAsync(request, reference, cancellationToken);
        if (!decision.Allowed)
        {
            await _access.EnforceCapabilityAsync(request, reference, cancellationToken);
            throw new InventoryTrackingAuthorizationException(decision.Message);
        }
    }

    private async Task<InventoryTrackingExceptionDto> MapExceptionAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await Exceptions.Where(value => value.TenantId == TenantId && value.Id == id && !value.IsDeleted)
            .AsNoTracking().Include(value => value.InventoryItem).Include(value => value.Warehouse)
            .SingleAsync(cancellationToken);
        return MapException(entity);
    }

    private static InventoryTrackingExceptionDto MapException(InventoryTrackingException value) => new()
    {
        Id = value.Id, InventoryItemId = value.InventoryItemId, ItemCode = value.InventoryItem.ItemCode,
        ItemName = value.InventoryItem.Name, WarehouseId = value.WarehouseId, WarehouseName = value.Warehouse.Name,
        LocationId = value.LocationId, ReferenceId = value.ReferenceId, ReferenceType = value.ReferenceType,
        ReferenceNumber = value.ReferenceNumber, ExceptionCodes = JsonSerializer.Deserialize<List<string>>(value.ExceptionCodesJson) ?? new(),
        Reason = value.Reason, LotNumber = value.LotNumber, BatchNumber = value.BatchNumber, SerialNumber = value.SerialNumber,
        WorkflowInstanceId = value.WorkflowInstanceId, WorkflowEvidenceDocumentId = value.WorkflowEvidenceDocumentId,
        ApprovedById = value.ApprovedById, ApprovedAtUtc = value.ApprovedAtUtc, ExpiresAtUtc = value.ExpiresAtUtc,
        ConsumedAtUtc = value.ConsumedAtUtc, IsAvailable = !value.ConsumedAtUtc.HasValue && value.ExpiresAtUtc > DateTime.UtcNow
    };

    private async Task AddAuditAsync(string action, Guid resourceId, object? before, object? after, string correlationId)
    {
        await _unitOfWork.Repository<AuditLog>().AddAsync(new AuditLog
        {
            TenantId = TenantId, UserId = UserId,
            Username = string.IsNullOrWhiteSpace(_currentUser.Username) ? "Unknown" : _currentUser.Username,
            Action = action, Resource = "InventoryTrackingControl", ResourceId = resourceId.ToString(),
            OldValues = before is null ? null : JsonSerializer.Serialize(before),
            NewValues = after is null ? null : JsonSerializer.Serialize(after),
            IpAddress = "system", UserAgent = correlationId, Timestamp = DateTime.UtcNow
        });
    }

    private void EnsureActor()
    {
        if (!_currentUser.IsAuthenticated || TenantId == Guid.Empty || UserId == Guid.Empty || _currentUser.IsExternalUser)
            throw new InventoryTrackingAuthorizationException("An authenticated internal tenant user is required.");
    }

    private static string PermissionFor(InventoryTrackingDirection direction) => direction switch
    {
        InventoryTrackingDirection.Receipt => "procurement.inventory.receive",
        InventoryTrackingDirection.Issue or InventoryTrackingDirection.Return => "procurement.inventory.issue",
        InventoryTrackingDirection.TransferOut or InventoryTrackingDirection.TransferIn => "procurement.inventory.transfer",
        _ => "procurement.inventory.adjust.request"
    };

    private static void Normalize(InventoryTrackingMutationRequest request)
    {
        request.ReferenceType = request.ReferenceType.Trim();
        request.ReferenceNumber = request.ReferenceNumber.Trim();
        request.EventKey = request.EventKey.Trim();
        request.CorrelationId = request.CorrelationId.Trim();
        request.LotNumber = NormalizeValue(request.LotNumber);
        request.BatchNumber = NormalizeValue(request.BatchNumber);
        request.SerialNumber = NormalizeValue(request.SerialNumber);
        request.ManufactureDate = Utc(request.ManufactureDate);
        request.ExpiryDate = Utc(request.ExpiryDate);
    }

    private static string NormalizeCode(string value) => value.Trim().ToUpperInvariant();
    private static string? NormalizeValue(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    private static DateTime? Utc(DateTime? value) => value.HasValue ? (value.Value.Kind == DateTimeKind.Utc ? value.Value : value.Value.ToUniversalTime()) : null;
    private static bool IsInbound(InventoryTrackingDirection direction) => direction is InventoryTrackingDirection.Receipt or InventoryTrackingDirection.Return or InventoryTrackingDirection.TransferIn or InventoryTrackingDirection.AdjustmentIn;
    private static decimal Balance(IEnumerable<InventoryTraceabilityEvent> values) => values.Sum(value => IsInbound(value.Direction) ? value.Quantity : -value.Quantity);
    private static bool Same(string? left, string? right) => string.Equals(NormalizeValue(left), NormalizeValue(right), StringComparison.Ordinal);
    private static bool OptionalMatch(string? scope, string? actual) => scope is null || Same(scope, actual);
    private static bool TrackingKeyMatches(InventoryTraceabilityEvent value, InventoryTrackingMutationRequest request) =>
        (request.LotNumber is null || Same(value.LotNumber, request.LotNumber)) &&
        (request.BatchNumber is null || Same(value.BatchNumber, request.BatchNumber)) &&
        (request.SerialNumber is null || Same(value.SerialNumber, request.SerialNumber));
    private static string TrackingKey(InventoryTraceabilityEvent value) => $"{NormalizeValue(value.LotNumber) ?? "@"}|{NormalizeValue(value.BatchNumber) ?? "@"}|{NormalizeValue(value.SerialNumber) ?? "@"}";
    private static string TrackingKey(InventoryTrackingMutationRequest value) => $"{value.LotNumber ?? "@"}|{value.BatchNumber ?? "@"}|{value.SerialNumber ?? "@"}";
    private static void Require(string? value, bool required, string code, string message)
    {
        if (required && string.IsNullOrWhiteSpace(value)) throw new InventoryTrackingControlException(code, message);
    }

    private static string MessageFor(string code) => code switch
    {
        "INV_TRACKING_EXPIRED" => "Expired stock cannot be received, issued, returned, or transferred without an exact approved exception.",
        "INV_TRACKING_MINIMUM_SHELF_LIFE" => "The lot does not meet the category's minimum remaining shelf life.",
        "INV_TRACKING_METADATA_MISMATCH" => "The supplied lot metadata does not match its original inbound traceability record.",
        "INV_TRACKING_FIFO_VIOLATION" => "An older eligible stock layer must be consumed first under the category FIFO rule.",
        _ => "Inventory tracking enforcement failed."
    };

    private static string HashPayload(InventoryTrackingMutationRequest request) => Sha256(JsonSerializer.Serialize(new
    {
        request.InventoryItemId, request.WarehouseId, request.LocationId, request.Direction, request.Quantity,
        request.ReferenceType, request.ReferenceNumber, request.ReferenceId, request.ReferenceLineId,
        request.LotNumber, request.BatchNumber, request.SerialNumber, request.ManufactureDate, request.ExpiryDate,
        request.TrackingExceptionId
    }));

    private static string HashException(InventoryTrackingException value, string evidenceSha) => Sha256(JsonSerializer.Serialize(new
    {
        value.InventoryItemId, value.WarehouseId, value.LocationId, value.ReferenceId, value.ReferenceLineId,
        value.ReferenceType, value.ReferenceNumber, value.Reason, value.ExceptionCodesJson, value.LotNumber,
        value.BatchNumber, value.SerialNumber, value.WorkflowInstanceId, value.WorkflowEvidenceDocumentId,
        value.RequestedById, value.ApprovedById, value.ApprovedAtUtc, value.ExpiresAtUtc, EvidenceSha = evidenceSha
    }));

    private static string Sha256(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
