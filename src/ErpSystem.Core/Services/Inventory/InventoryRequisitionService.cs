using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Inventory;

/// <summary>
/// Inventory Requisition management service
/// Handles department requisitions and inventory issues
/// </summary>
public class InventoryRequisitionService : IInventoryRequisitionService
{
    private const string WorkflowEntityType = "InventoryRequisition";

    private readonly IInventoryRequisitionRepository _requisitionRepository;
    private readonly IInventoryRequisitionItemRepository _requisitionItemRepository;
    private readonly IInventoryItemRepository _itemRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IWarehouseLocationRepository _warehouseLocationRepository;
    private readonly IWarehouseQuantityRepository _warehouseQuantityRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly IConsignmentSettlementService _consignmentSettlementService;
    private readonly IProjectRepository _projectRepository;
    private readonly IProjectService _projectService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IWorkflowIntegrationService _workflowIntegrationService;
    private readonly IWorkflowStatusAdapterRegistry _workflowStatusAdapterRegistry;
    private readonly IInventoryTrackingControlService _trackingControls;
    private readonly IInventoryNegativeStockControlService _negativeStockControls;
    private readonly IInventoryProjectReservationService _projectReservations;
    private readonly IProcurementAccessControlService _accessControl;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly IInventoryReturnControlService _returnControls;
    private readonly IInventoryIssueFinanceAssetService _issueFinanceAssets;
    private readonly ILogger<InventoryRequisitionService> _logger;

    public InventoryRequisitionService(
        IInventoryRequisitionRepository requisitionRepository,
        IInventoryRequisitionItemRepository requisitionItemRepository,
        IInventoryItemRepository itemRepository,
        IWarehouseRepository warehouseRepository,
        IWarehouseLocationRepository warehouseLocationRepository,
        IWarehouseQuantityRepository warehouseQuantityRepository,
        IStockMovementRepository stockMovementRepository,
        IConsignmentSettlementService consignmentSettlementService,
        IProjectRepository projectRepository,
        IProjectService projectService,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        IWorkflowIntegrationService workflowIntegrationService,
        IWorkflowStatusAdapterRegistry workflowStatusAdapterRegistry,
        IInventoryTrackingControlService trackingControls,
        IInventoryNegativeStockControlService negativeStockControls,
        IInventoryProjectReservationService projectReservations,
        IProcurementAccessControlService accessControl,
        IProcurementControlEventService controlEvents,
        IInventoryReturnControlService returnControls,
        IInventoryIssueFinanceAssetService issueFinanceAssets,
        ILogger<InventoryRequisitionService> logger)
    {
        _requisitionRepository = requisitionRepository;
        _requisitionItemRepository = requisitionItemRepository;
        _itemRepository = itemRepository;
        _warehouseRepository = warehouseRepository;
        _warehouseLocationRepository = warehouseLocationRepository;
        _warehouseQuantityRepository = warehouseQuantityRepository;
        _stockMovementRepository = stockMovementRepository;
        _consignmentSettlementService = consignmentSettlementService;
        _projectRepository = projectRepository;
        _projectService = projectService;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _workflowIntegrationService = workflowIntegrationService;
        _workflowStatusAdapterRegistry = workflowStatusAdapterRegistry;
        _trackingControls = trackingControls;
        _negativeStockControls = negativeStockControls;
        _projectReservations = projectReservations;
        _accessControl = accessControl;
        _controlEvents = controlEvents;
        _returnControls = returnControls;
        _issueFinanceAssets = issueFinanceAssets;
        _logger = logger;
    }

    public async Task<IEnumerable<InventoryRequisitionDto>> GetAllAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var requisitions = await _requisitionRepository.GetByDateRangeAsync(
            fromDate ?? DateTime.UtcNow.AddMonths(-3),
            toDate ?? DateTime.UtcNow);
        return (await ApplyReadScopeAsync(requisitions)).Select(MapToDto);
    }

    public async Task<IEnumerable<InventoryRequisitionDto>> GetByProjectAsync(Guid projectId)
    {
        var requisitions = await _requisitionRepository.GetByProjectAsync(projectId);
        return (await ApplyReadScopeAsync(requisitions)).Select(MapToDto);
    }

    public async Task<IEnumerable<InventoryRequisitionDto>> GetByWarehouseAsync(Guid warehouseId)
    {
        var requisitions = await _requisitionRepository.GetByWarehouseAsync(warehouseId);
        return (await ApplyReadScopeAsync(requisitions)).Select(MapToDto);
    }

    public async Task<IEnumerable<InventoryRequisitionDto>> GetByDepartmentAsync(Guid departmentId)
    {
        var requisitions = await _requisitionRepository.GetByDepartmentAsync(departmentId);
        return (await ApplyReadScopeAsync(requisitions)).Select(MapToDto);
    }

    public async Task<IEnumerable<InventoryRequisitionDto>> GetPendingApprovalAsync()
    {
        var requisitions = await _requisitionRepository.GetPendingApprovalAsync();
        return (await ApplyReadScopeAsync(requisitions)).Select(MapToDto);
    }

    public async Task<IEnumerable<InventoryRequisitionDto>> GetPendingIssueAsync()
    {
        var requisitions = await _requisitionRepository.GetPendingIssueAsync();
        return (await ApplyReadScopeAsync(requisitions)).Select(MapToDto);
    }

    public async Task<InventoryRequisitionDetailDto?> GetByIdAsync(Guid id)
    {
        var requisition = await _requisitionRepository.GetWithItemsAsync(id);
        return requisition != null && await CanReadRequisitionAsync(requisition)
            ? MapToDetailDto(requisition)
            : null;
    }

    public async Task<InventoryRequisitionDetailDto?> GetByRequisitionNumberAsync(string requisitionNumber)
    {
        var requisition = await _requisitionRepository.GetByRequisitionNumberAsync(requisitionNumber);
        if (requisition == null) return null;
        var fullRequisition = await _requisitionRepository.GetWithItemsAsync(requisition.Id);
        return fullRequisition != null && await CanReadRequisitionAsync(fullRequisition)
            ? MapToDetailDto(fullRequisition)
            : null;
    }

    public async Task<InventoryRequisitionDetailDto> CreateAsync(CreateInventoryRequisitionDto dto)
    {
        var warehouse = await _warehouseRepository.GetByIdAsync(dto.WarehouseId)
            ?? throw new ArgumentException($"Warehouse {dto.WarehouseId} not found");
        var project = await NormalizeProjectReferenceAsync(dto.ProjectId, dto.ProjectCode);

        var requisition = new InventoryRequisition
        {
            RequisitionNumber = await _requisitionRepository.GenerateRequisitionNumberAsync(),
            DepartmentId = dto.DepartmentId,
            DepartmentName = dto.DepartmentName,
            CostCenter = dto.CostCenter,
            WarehouseId = dto.WarehouseId,
            LocationId = dto.LocationId,
            ProjectId = project?.Id,
            ProjectCode = project?.ProjectCode,
            RequisitionType = dto.RequisitionType,
            Priority = dto.Priority,
            Status = RequisitionStatus.Draft,
            RequestDate = DateTime.UtcNow,
            RequiredDate = dto.RequiredDate,
            Purpose = dto.Purpose,
            Notes = dto.Notes,
            RequestedById = _currentUserProvider.UserId,
            TenantId = _currentUserProvider.TenantId
        };

        // Save the requisition first to ensure it exists in the database
        await _requisitionRepository.AddAsync(requisition);
        await _unitOfWork.SaveChangesAsync();

        // Now add items with the persisted requisition ID
        if (dto.Items != null && dto.Items.Any())
        {
            var pendingItems = new List<InventoryRequisitionItem>();
            foreach (var itemDto in dto.Items)
            {
                var item = await _itemRepository.GetByIdAsync(itemDto.InventoryItemId)
                    ?? throw new ArgumentException($"Inventory item {itemDto.InventoryItemId} not found");

                var itemUnitCost = await ResolveDraftUnitCostAsync(item, requisition.WarehouseId,
                    itemDto.LocationId ?? requisition.LocationId, itemDto.RequestedQuantity);

                var requisitionItem = new InventoryRequisitionItem
                {
                    InventoryRequisitionId = requisition.Id,
                    InventoryItemId = itemDto.InventoryItemId,
                    ItemCode = item.ItemCode,
                    ItemName = item.Name,
                    RequestedQuantity = itemDto.RequestedQuantity,
                    ApprovedQuantity = 0,
                    IssuedQuantity = 0,
                    UnitOfMeasure = item.UnitOfMeasure,
                    UnitCost = itemUnitCost,
                    LocationId = itemDto.LocationId,
                    LotNumber = itemDto.LotNumber,
                    BatchNumber = itemDto.BatchNumber,
                    SerialNumber = itemDto.SerialNumber,
                    ManufactureDate = itemDto.ManufactureDate,
                    ExpiryDate = itemDto.ExpiryDate,
                    InventoryTrackingExceptionId = itemDto.InventoryTrackingExceptionId,
                    Notes = itemDto.Notes,
                    TenantId = _currentUserProvider.TenantId
                };

                await _requisitionItemRepository.AddAsync(requisitionItem);
                pendingItems.Add(requisitionItem);
            }

            await UpdateRequisitionTotals(requisition, pendingItems);
            await _unitOfWork.SaveChangesAsync();
        }

        _logger.LogInformation("Created inventory requisition {RequisitionNumber}", requisition.RequisitionNumber);
        return (await GetByIdAsync(requisition.Id))!;
    }

    public async Task<InventoryRequisitionDetailDto> UpdateAsync(Guid id, UpdateInventoryRequisitionDto dto)
    {
        var requisition = await _requisitionRepository.GetByIdAsync(id)
            ?? throw new ArgumentException($"Requisition {id} not found");

        if (requisition.Status != RequisitionStatus.Draft)
            throw new InvalidOperationException("Only requisitions in Draft status can be updated");

        if (dto.DepartmentId.HasValue) requisition.DepartmentId = dto.DepartmentId.Value;
        if (dto.DepartmentName != null) requisition.DepartmentName = dto.DepartmentName;
        if (dto.CostCenter != null) requisition.CostCenter = dto.CostCenter;
        if (dto.WarehouseId.HasValue) requisition.WarehouseId = dto.WarehouseId.Value;
        if (dto.LocationId.HasValue) requisition.LocationId = dto.LocationId;
        if (dto.RequiredDate.HasValue) requisition.RequiredDate = dto.RequiredDate;
        if (dto.Purpose != null) requisition.Purpose = dto.Purpose;
        if (dto.Notes != null) requisition.Notes = dto.Notes;

        if (dto.ProjectId.HasValue || !string.IsNullOrWhiteSpace(dto.ProjectCode))
        {
            var project = await NormalizeProjectReferenceAsync(dto.ProjectId, dto.ProjectCode);
            requisition.ProjectId = project?.Id;
            requisition.ProjectCode = project?.ProjectCode;
        }

        if (dto.WarehouseId.HasValue || dto.LocationId.HasValue)
        {
            var lines = (await _requisitionItemRepository.GetByRequisitionAsync(id)).ToList();
            foreach (var line in lines)
            {
                var item = await _itemRepository.GetByIdAsync(line.InventoryItemId)
                    ?? throw new ArgumentException($"Inventory item {line.InventoryItemId} not found");
                line.UnitCost = await ResolveDraftUnitCostAsync(item, requisition.WarehouseId,
                    line.LocationId ?? requisition.LocationId, line.RequestedQuantity);
                await _requisitionItemRepository.UpdateAsync(line);
            }
            await UpdateRequisitionTotals(requisition, lines);
        }

        await _requisitionRepository.UpdateAsync(requisition);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Updated requisition {RequisitionNumber}", requisition.RequisitionNumber);
        return (await GetByIdAsync(id))!;
    }

    public async Task<bool> SubmitAsync(Guid id)
    {
        var requisition = await _requisitionRepository.GetWithItemsAsync(id)
            ?? throw new ArgumentException($"Requisition {id} not found");

        if (requisition.Status != RequisitionStatus.Draft)
            throw new InvalidOperationException("Only requisitions in Draft status can be submitted");

        if (!requisition.Items.Any())
            throw new InvalidOperationException("Cannot submit a requisition with no items");

        var userId = _currentUserProvider.UserId;
        if (userId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("User not authenticated");
        }

        // Start unified workflow. If no active workflow is configured, this will throw and we keep Draft state.
        var workflowResult = await _workflowIntegrationService.SubmitAsync(WorkflowEntityType, id);
        if (!workflowResult.ExecutionResult.Success)
        {
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to start workflow");
        }

        // If the workflow completes immediately (rare), apply the final approval logic.
        if (workflowResult.Outcome == WorkflowOutcome.Approved)
        {
            await ApplyFinalApprovalAsync(requisition, notes: null);
        }

        var statusAdapter = _workflowStatusAdapterRegistry.GetAdapter(WorkflowEntityType);
        statusAdapter.ApplySubmitOutcome(requisition, workflowResult.Outcome, userId);
        requisition.UpdatedAt = DateTime.UtcNow;
        await _requisitionRepository.UpdateAsync(requisition);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Submitted requisition {RequisitionNumber}", requisition.RequisitionNumber);
        return true;
    }

    public async Task<bool> ApproveAsync(Guid id, string? notes = null)
    {
        var requisition = await _requisitionRepository.GetWithItemsAsync(id)
            ?? throw new ArgumentException($"Requisition {id} not found");

        if (requisition.Status != RequisitionStatus.Submitted)
            throw new InvalidOperationException("Only submitted requisitions can be approved");

        var userId = _currentUserProvider.UserId;
        if (userId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("User not authenticated");
        }

        var canApprove = await _workflowIntegrationService.CanUserApproveAsync(WorkflowEntityType, id, userId);
        if (!canApprove)
        {
            throw new UnauthorizedAccessException("You are not assigned as an approver for the current workflow step");
        }

        var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(
            WorkflowEntityType,
            id,
            userId,
            "Approve",
            notes);

        if (!workflowResult.ExecutionResult.Success)
        {
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to process approval");
        }

        if (workflowResult.Outcome == WorkflowOutcome.Approved)
        {
            await ApplyFinalApprovalAsync(requisition, notes);
        }
        else if (!string.IsNullOrWhiteSpace(notes))
        {
            // Optional: keep the latest approver note on the requisition for quick reference.
            requisition.Notes = notes;
        }

        var statusAdapter = _workflowStatusAdapterRegistry.GetAdapter(WorkflowEntityType);
        statusAdapter.ApplyApprovalOutcome(requisition, workflowResult.Outcome, userId);
        requisition.UpdatedAt = DateTime.UtcNow;

        await _requisitionRepository.UpdateAsync(requisition);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Approved requisition {RequisitionNumber}", requisition.RequisitionNumber);
        return true;
    }

    public async Task<bool> RejectAsync(Guid id, string reason)
    {
        var requisition = await _requisitionRepository.GetByIdAsync(id)
            ?? throw new ArgumentException($"Requisition {id} not found");

        if (requisition.Status != RequisitionStatus.Submitted)
            throw new InvalidOperationException("Only submitted requisitions can be rejected");

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("Rejection comment is required.");
        }

        var userId = _currentUserProvider.UserId;
        if (userId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("User not authenticated");
        }

        var canApprove = await _workflowIntegrationService.CanUserApproveAsync(WorkflowEntityType, id, userId);
        if (!canApprove)
        {
            throw new UnauthorizedAccessException("You are not assigned as an approver for the current workflow step");
        }

        var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(
            WorkflowEntityType,
            id,
            userId,
            "Reject",
            reason);

        if (!workflowResult.ExecutionResult.Success)
        {
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to process rejection");
        }

        // Only stamp the entity as rejected when the workflow outcome is rejected.
        if (workflowResult.Outcome == WorkflowOutcome.Rejected)
        {
            requisition.RejectionReason = reason;
        }

        var statusAdapter = _workflowStatusAdapterRegistry.GetAdapter(WorkflowEntityType);
        statusAdapter.ApplyApprovalOutcome(requisition, workflowResult.Outcome, userId, reason);
        requisition.UpdatedAt = DateTime.UtcNow;

        await _requisitionRepository.UpdateAsync(requisition);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Rejected requisition {RequisitionNumber}: {Reason}", requisition.RequisitionNumber, reason);
        return true;
    }

    private async Task ApplyFinalApprovalAsync(InventoryRequisition requisition, string? notes)
    {
        // Check stock availability for all items at final approval time and stamp approved quantities.
        foreach (var item in requisition.Items)
        {
            var warehouseQty = await _warehouseQuantityRepository.GetByWarehouseAndItemAsync(requisition.WarehouseId, item.InventoryItemId);
            if (warehouseQty == null || warehouseQty.AvailableStock < item.RequestedQuantity)
            {
                throw new InvalidOperationException(
                    $"Insufficient stock for {item.ItemCode}. Available: {warehouseQty?.AvailableStock ?? 0}, Requested: {item.RequestedQuantity}");
            }

            item.ApprovedQuantity = item.RequestedQuantity;
            await _requisitionItemRepository.UpdateAsync(item);
        }

        if (!string.IsNullOrWhiteSpace(notes))
        {
            requisition.Notes = notes;
        }
    }

    public async Task<bool> IssueAsync(Guid id, IssueRequisitionDto dto)
    {
        EnsureIssueActor();
        if (dto.Items.Count == 0)
            throw new InventoryIssueControlException("INV_ISSUE_LINES_REQUIRED", "At least one positive issue line is required.");
        dto.MovementReasonCode = NormalizeMovementReason(dto.MovementReasonCode);

        if (_unitOfWork.HasActiveTransaction)
        {
            await _unitOfWork.AcquireTransactionLockAsync($"inventory-requisition-issue:{_currentUserProvider.TenantId:N}:{id:N}");
            return await IssueCoreAsync(id, dto);
        }

        try
        {
            return await _unitOfWork.ExecuteInStrategyAsync(async () =>
            {
                await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
                try
                {
                    await _unitOfWork.AcquireTransactionLockAsync($"inventory-requisition-issue:{_currentUserProvider.TenantId:N}:{id:N}");
                    var result = await IssueCoreAsync(id, dto);
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
        catch (DbUpdateConcurrencyException)
        {
            throw new InventoryIssueControlException("INV_ISSUE_CONCURRENCY_CONFLICT",
                "The requisition changed after it was loaded. Refresh and retry.");
        }
    }

    private async Task<bool> IssueCoreAsync(Guid id, IssueRequisitionDto dto)
    {
        var requisition = await _requisitionRepository.GetWithItemsAsync(id)
            ?? throw new InventoryIssueNotFoundException($"Requisition {id} was not found in the current tenant.");

        var receiverId = dto.ReceiverUserId.GetValueOrDefault(requisition.RequestedById.GetValueOrDefault());
        if (receiverId == Guid.Empty)
            throw new InventoryIssueControlException("INV_ISSUE_RECEIVER_REQUIRED", "An active internal receiver is required.");
        await EnsureActiveInternalUserAsync(receiverId);

        var normalizedKey = NormalizeRequired(dto.IdempotencyKey, 100, "Idempotency key");
        var correlationId = NormalizeCorrelation(dto.CorrelationId);
        var payloadHash = Hash(new
        {
            requisitionId = id,
            receiverId,
            movementReasonCode = dto.MovementReasonCode,
            notes = NormalizeOptional(dto.Notes, 2000),
            items = dto.Items.OrderBy(item => item.ItemId).Select(item => new
            {
                item.ItemId,
                item.IssuedQuantity,
                item.LocationId,
                LotNumber = NormalizeOptional(item.LotNumber, 100),
                BatchNumber = NormalizeOptional(item.BatchNumber, 100),
                SerialNumber = NormalizeOptional(item.SerialNumber, 100),
                ManufactureDate = Utc(item.ManufactureDate),
                ExpiryDate = Utc(item.ExpiryDate),
                item.InventoryTrackingExceptionId,
                item.NegativeStockOverrideId
            })
        });
        var existingVoucher = await IssueVouchers.AsNoTracking().SingleOrDefaultAsync(value =>
            value.TenantId == _currentUserProvider.TenantId && value.InventoryRequisitionId == id &&
            value.IdempotencyKey == normalizedKey && !value.IsDeleted);
        if (existingVoucher is not null)
        {
            if (!string.Equals(existingVoucher.PayloadHash, payloadHash, StringComparison.OrdinalIgnoreCase))
                throw new InventoryIssueControlException("INV_ISSUE_IDEMPOTENCY_CONFLICT",
                    "The idempotency key already identifies a different issue payload.");
            return true;
        }

        EnsureRowVersion(requisition.RowVersion, dto.RowVersion, "requisition");

        if (requisition.Status != RequisitionStatus.Approved &&
            requisition.Status != RequisitionStatus.InProgress &&
            requisition.Status != RequisitionStatus.PartiallyIssued)
            throw new InventoryIssueControlException("INV_ISSUE_APPROVED_SOURCE_REQUIRED",
                "Only an approved or partially issued requisition can be issued.");

        if (!requisition.RequestedById.HasValue || !requisition.ApprovedById.HasValue)
            throw new InventoryIssueControlException("INV_ISSUE_APPROVAL_LINEAGE_REQUIRED",
                "The requisition must retain both requester and completed-workflow approver lineage.");
        if (requisition.RequestedById == requisition.ApprovedById)
            throw new InventoryIssueControlException("INV_ISSUE_REQUEST_APPROVAL_SOD",
                "The requester and approver must be different users.");
        if (_currentUserProvider.UserId == requisition.RequestedById ||
            _currentUserProvider.UserId == requisition.ApprovedById)
            throw new InventoryIssueAuthorizationException(
                "The issuer must be independent of the requester and approver.");
        if (_currentUserProvider.UserId == receiverId)
            throw new InventoryIssueAuthorizationException("The issuer cannot acknowledge their own handover as receiver.");
        if (!requisition.ProjectId.HasValue && string.IsNullOrWhiteSpace(requisition.CostCenter))
            throw new InventoryIssueControlException("INV_ISSUE_COST_OBJECT_REQUIRED",
                "The approved requisition must reference a project or cost centre before stock can be issued.");

        var warehouse = await _warehouseRepository.GetByIdAsync(requisition.WarehouseId)
            ?? throw new InventoryIssueNotFoundException("The source warehouse was not found in the current tenant.");
        var issuedAt = DateTime.UtcNow;
        var sourceSnapshotJson = JsonSerializer.Serialize(new
        {
            requisition.Id,
            requisition.RequisitionNumber,
            requisition.Status,
            requisition.DepartmentId,
            requisition.DepartmentName,
            requisition.CostCenter,
            requisition.ProjectId,
            requisition.ProjectCode,
            requisition.WarehouseId,
            requisition.LocationId,
            requisition.RequestedById,
            requisition.ApprovedById,
            dto.MovementReasonCode
        });
        var voucher = new InventoryIssueVoucher
        {
            TenantId = _currentUserProvider.TenantId,
            VoucherNumber = await GenerateIssueVoucherNumberAsync(issuedAt),
            InventoryRequisitionId = requisition.Id,
            Status = InventoryIssueVoucherStatus.Issued,
            WarehouseId = requisition.WarehouseId,
            LocationId = requisition.LocationId,
            DepartmentId = requisition.DepartmentId,
            DepartmentName = requisition.DepartmentName,
            CostCenter = NormalizeOptional(requisition.CostCenter, 100),
            ProjectId = requisition.ProjectId,
            ProjectCode = NormalizeOptional(requisition.ProjectCode, 100),
            RequestedById = requisition.RequestedById.Value,
            ApprovedById = requisition.ApprovedById.Value,
            IssuedById = _currentUserProvider.UserId,
            ReceiverUserId = receiverId,
            IssuedAtUtc = issuedAt,
            Notes = NormalizeOptional(dto.Notes, 2000),
            MovementReasonCode = dto.MovementReasonCode,
            IdempotencyKey = normalizedKey,
            PayloadHash = payloadHash,
            CorrelationId = correlationId,
            SourceSnapshotJson = sourceSnapshotJson,
            CreatedById = _currentUserProvider.UserId
        };
        voucher.IntegrityHash = VoucherIntegrity(voucher);
        await _unitOfWork.Repository<InventoryIssueVoucher>().AddAsync(voucher);
        var pendingMovements = new List<StockMovement>();
        var pendingVoucherLines = new List<InventoryIssueVoucherLine>();

        foreach (var issueItem in dto.Items)
        {
            var requisitionItem = requisition.Items.FirstOrDefault(i => i.Id == issueItem.ItemId)
                ?? throw new InventoryIssueControlException("INV_ISSUE_LINE_NOT_FOUND",
                    $"Requisition item {issueItem.ItemId} was not found on the approved requisition.");

            if (issueItem.IssuedQuantity <= 0)
                throw new InventoryIssueControlException("INV_ISSUE_QUANTITY_INVALID", "Issued quantity must be greater than zero.");

            var remainingToIssue = requisitionItem.ApprovedQuantity - requisitionItem.IssuedQuantity;
            if (issueItem.IssuedQuantity > remainingToIssue)
                throw new InventoryIssueControlException("INV_ISSUE_APPROVED_QUANTITY_EXCEEDED",
                    $"Cannot issue more than the remaining approved quantity for {requisitionItem.ItemCode}.");

            var effectiveLocationId = issueItem.LocationId ?? requisition.LocationId;
            var effectiveWarehouseId = requisition.WarehouseId;
            var isConsignmentWarehouse = warehouse.IsConsignmentWarehouse;

            if (effectiveLocationId.HasValue && effectiveLocationId.Value != Guid.Empty)
            {
                var location = await _warehouseLocationRepository.GetByIdAsync(effectiveLocationId.Value);
                if (location == null || location.TenantId != _currentUserProvider.TenantId || location.IsDeleted)
                    throw new InventoryIssueNotFoundException("The issue location was not found in the current tenant.");
                effectiveWarehouseId = location.InventoryWarehouseId;
            }

            if (effectiveWarehouseId != requisition.WarehouseId)
                throw new InventoryIssueControlException("INV_ISSUE_LOCATION_WAREHOUSE_MISMATCH",
                    "Every issue location must belong to the approved requisition warehouse.");

            var accessDecision = await _accessControl.EnforceCapabilityAsync(new ProcurementAccessCapabilityRequest
            {
                PermissionCode = "procurement.inventory.issue",
                WarehouseId = effectiveWarehouseId,
                LocationId = effectiveLocationId,
                RequireLocationScope = true,
                SourceType = "InventoryRequisition",
                SourceReference = requisition.RequisitionNumber
            }, correlationId);
            if (!accessDecision.Allowed)
                throw new InventoryIssueAuthorizationException(accessDecision.Message);

            if (effectiveWarehouseId != requisition.WarehouseId)
            {
                var wh = await _warehouseRepository.GetByIdAsync(effectiveWarehouseId);
                isConsignmentWarehouse = wh?.IsConsignmentWarehouse == true;
            }

            var fulfillmentOrdinal = requisitionItem.TrackingSequence + 1;

            await _projectReservations.FulfillForIssueAsync(new InventoryProjectReservationFulfillmentRequest
            {
                InventoryRequisitionId = requisition.Id,
                InventoryRequisitionItemId = requisitionItem.Id,
                InventoryItemId = requisitionItem.InventoryItemId,
                WarehouseId = effectiveWarehouseId,
                LocationId = effectiveLocationId ?? Guid.Empty,
                Quantity = issueItem.IssuedQuantity,
                ActorUserId = _currentUserProvider.UserId,
                IdempotencyKey = $"issue:{normalizedKey}:{requisitionItem.Id:N}:{fulfillmentOrdinal}",
                CorrelationId = correlationId
            });

            var decreaseAuthorization = await _negativeStockControls.PrepareDecreaseAsync(new InventoryStockDecreaseRequest
            {
                InventoryItemId = requisitionItem.InventoryItemId,
                WarehouseId = effectiveWarehouseId,
                LocationId = effectiveLocationId,
                Quantity = issueItem.IssuedQuantity,
                ReferenceType = "InventoryRequisition",
                ReferenceNumber = requisition.RequisitionNumber,
                ReferenceId = requisition.Id,
                ReferenceLineId = requisitionItem.Id,
                NegativeStockOverrideId = issueItem.NegativeStockOverrideId,
                CorrelationId = correlationId
            });

            // Reload after the tenant/item transaction lock so this check cannot race another consumer.
            var warehouseQty = await _warehouseQuantityRepository.GetByWarehouseAndItemAsync(effectiveWarehouseId, requisitionItem.InventoryItemId);
            if (warehouseQty == null ||
                (warehouseQty.AvailableStock < issueItem.IssuedQuantity && !decreaseAuthorization.EmergencyOverrideApplied))
                throw new InventoryIssueControlException("INV_ISSUE_STOCK_INSUFFICIENT",
                    $"Insufficient available stock for {requisitionItem.ItemCode}.");

            var lotNumber = issueItem.LotNumber ?? requisitionItem.LotNumber;
            var batchNumber = issueItem.BatchNumber ?? requisitionItem.BatchNumber;
            var serialNumber = issueItem.SerialNumber ?? requisitionItem.SerialNumber;
            var manufactureDate = issueItem.ManufactureDate ?? requisitionItem.ManufactureDate;
            var expiryDate = issueItem.ExpiryDate ?? requisitionItem.ExpiryDate;
            var trackingExceptionId = issueItem.InventoryTrackingExceptionId ?? requisitionItem.InventoryTrackingExceptionId;
            var trackingSequence = requisitionItem.TrackingSequence + 1;
            await _trackingControls.StageEventAsync(new InventoryTrackingMutationRequest
            {
                InventoryItemId = requisitionItem.InventoryItemId,
                WarehouseId = effectiveWarehouseId,
                LocationId = effectiveLocationId,
                Direction = InventoryTrackingDirection.Issue,
                Quantity = issueItem.IssuedQuantity,
                ReferenceType = "InventoryRequisition",
                ReferenceNumber = requisition.RequisitionNumber,
                ReferenceId = requisition.Id,
                ReferenceLineId = requisitionItem.Id,
                EventKey = $"requisition:{requisition.Id:N}:{requisitionItem.Id:N}:{trackingSequence}:issue",
                LotNumber = lotNumber,
                BatchNumber = batchNumber,
                SerialNumber = serialNumber,
                ManufactureDate = manufactureDate,
                ExpiryDate = expiryDate,
                TrackingExceptionId = trackingExceptionId,
                CorrelationId = $"requisition:{requisition.Id:N}:issue"
            });

            // Update issued quantity
            requisitionItem.IssuedQuantity += issueItem.IssuedQuantity;
            requisitionItem.TrackingSequence = trackingSequence;
            requisitionItem.LineValue = requisitionItem.IssuedQuantity * requisitionItem.UnitCost;
            if (issueItem.LocationId.HasValue) requisitionItem.LocationId = issueItem.LocationId;
            requisitionItem.LotNumber = lotNumber;
            requisitionItem.BatchNumber = batchNumber;
            requisitionItem.SerialNumber = serialNumber;
            requisitionItem.ManufactureDate = manufactureDate;
            requisitionItem.ExpiryDate = expiryDate;
            requisitionItem.InventoryTrackingExceptionId = trackingExceptionId;

            await _requisitionItemRepository.UpdateAsync(requisitionItem);

            // Deduct from warehouse quantity
            warehouseQty.CurrentStock -= issueItem.IssuedQuantity;
            warehouseQty.AvailableStock -= issueItem.IssuedQuantity;
            await _warehouseQuantityRepository.UpdateAsync(warehouseQty);

            if (effectiveLocationId.HasValue)
            {
                var locationStock = await _unitOfWork.Repository<InventoryLocation>().GetQueryable(value =>
                        value.TenantId == _currentUserProvider.TenantId && !value.IsDeleted &&
                        value.LocationId == effectiveLocationId.Value &&
                        value.InventoryItemId == requisitionItem.InventoryItemId)
                    .SingleOrDefaultAsync()
                    ?? throw new InventoryIssueControlException("INV_ISSUE_LOCATION_STOCK_MISSING",
                        "The selected issue location has no stock balance for this item.");
                if (locationStock.AvailableQuantity < issueItem.IssuedQuantity &&
                    !decreaseAuthorization.EmergencyOverrideApplied)
                    throw new InventoryIssueControlException("INV_ISSUE_LOCATION_STOCK_INSUFFICIENT",
                        $"Insufficient available location stock for {requisitionItem.ItemCode}.");
                locationStock.Quantity -= issueItem.IssuedQuantity;
                locationStock.AvailableQuantity = locationStock.Quantity - locationStock.AllocatedQuantity;
                locationStock.LastMovementDate = DateTime.UtcNow;
                await _unitOfWork.Repository<InventoryLocation>().UpdateAsync(locationStock);
            }

            // Update owned/main inventory item quantities only for non-consignment warehouses/bins.
            if (!isConsignmentWarehouse)
            {
                var inventoryItem = await _itemRepository.GetByIdAsync(requisitionItem.InventoryItemId);
                if (inventoryItem != null)
                {
                    inventoryItem.CurrentStock -= issueItem.IssuedQuantity;
                    inventoryItem.AvailableStock -= issueItem.IssuedQuantity;
                    await _itemRepository.UpdateAsync(inventoryItem);
                }
            }

            if (decreaseAuthorization.EmergencyOverrideApplied)
            {
                // Force the protected balances through the SQL hard stop while the exact
                // transaction-bound override context is active, then clear the pooled session.
                await _unitOfWork.SaveChangesAsync();
                await _negativeStockControls.ClearMutationContextAsync();
            }

            // Create stock movement
            var movement = new StockMovement
            {
                InventoryItemId = requisitionItem.InventoryItemId,
                MovementType = "Issue",
                MovementDate = DateTime.UtcNow,
                Quantity = -issueItem.IssuedQuantity,
                UnitCost = requisitionItem.UnitCost,
                TotalValue = -issueItem.IssuedQuantity * requisitionItem.UnitCost,
                ReferenceType = ReferenceType.Requisition,
                ReferenceNumber = requisition.RequisitionNumber,
                ReferenceId = requisition.Id,
                WarehouseId = effectiveWarehouseId,
                LocationId = effectiveLocationId,
                LotNumber = lotNumber,
                BatchNumber = batchNumber,
                SerialNumber = serialNumber,
                ManufactureDate = manufactureDate,
                ExpirationDate = expiryDate,
                InventoryTrackingExceptionId = trackingExceptionId,
                InventoryIssueVoucherId = voucher.Id,
                ProcessedById = _currentUserProvider.UserId,
                Notes = $"Issued for requisition {requisition.RequisitionNumber}",
                TenantId = _currentUserProvider.TenantId
            };
            pendingMovements.Add(movement);

            var voucherLine = new InventoryIssueVoucherLine
            {
                TenantId = _currentUserProvider.TenantId,
                InventoryIssueVoucherId = voucher.Id,
                InventoryRequisitionItemId = requisitionItem.Id,
                InventoryItemId = requisitionItem.InventoryItemId,
                WarehouseId = effectiveWarehouseId,
                LocationId = effectiveLocationId,
                Quantity = issueItem.IssuedQuantity,
                UnitCost = requisitionItem.UnitCost,
                TotalValue = issueItem.IssuedQuantity * requisitionItem.UnitCost,
                UnitOfMeasure = requisitionItem.UnitOfMeasure,
                LotNumber = lotNumber,
                BatchNumber = batchNumber,
                SerialNumber = serialNumber,
                ManufactureDate = manufactureDate,
                ExpiryDate = expiryDate,
                InventoryTrackingExceptionId = trackingExceptionId,
                CreatedById = _currentUserProvider.UserId
            };
            voucherLine.IntegrityHash = Hash(new { voucherLine.InventoryIssueVoucherId,
                voucherLine.InventoryRequisitionItemId, voucherLine.InventoryItemId, voucherLine.WarehouseId,
                voucherLine.LocationId, voucherLine.Quantity, voucherLine.UnitCost, voucherLine.TotalValue,
                voucherLine.UnitOfMeasure, voucherLine.LotNumber, voucherLine.BatchNumber, voucherLine.SerialNumber,
                voucherLine.ManufactureDate, voucherLine.ExpiryDate, voucherLine.InventoryTrackingExceptionId });
            // Keep the line detached until the requisition's issued quantity is durable inside
            // this transaction. The SQL line guard compares the aggregate voucher quantity to
            // that value, while EF is otherwise free to insert a new line before updating its
            // existing requisition item.
            pendingVoucherLines.Add(voucherLine);

        }

        // Update requisition status
        var allIssued = requisition.Items.All(i => i.IssuedQuantity >= i.ApprovedQuantity);
        var anyIssued = requisition.Items.Any(i => i.IssuedQuantity > 0);

        if (allIssued)
        {
            requisition.Status = RequisitionStatus.Issued;
            requisition.IssuedDate = DateTime.UtcNow;
        }
        else if (anyIssued)
        {
            requisition.Status = RequisitionStatus.PartiallyIssued;
        }
        else
        {
            requisition.Status = RequisitionStatus.InProgress;
        }

        requisition.IssuedById = _currentUserProvider.UserId;
        if (dto.Notes != null) requisition.Notes = dto.Notes;

        await UpdateRequisitionTotals(requisition);
        await _requisitionRepository.UpdateAsync(requisition);

        // Persist the approved-source quantities and balances before inserting voucher lines.
        // This remains inside the same serializable transaction, so any later Finance, asset,
        // stock-movement or evidence failure rolls the entire issue back atomically.
        await _unitOfWork.SaveChangesAsync();
        foreach (var voucherLine in pendingVoucherLines)
        {
            voucher.Lines.Add(voucherLine);
            await _unitOfWork.Repository<InventoryIssueVoucherLine>().AddAsync(voucherLine);
        }
        await AddVoucherActionAsync(voucher, InventoryIssueVoucherActionType.Issued,
            InventoryIssueVoucherStatus.Issued, dto.Notes ?? "Stock issued and handed over for receiver acknowledgement.",
            new { requisition.Id, requisition.RequisitionNumber, ReceiverUserId = receiverId, Lines = voucher.Lines.Count },
            correlationId);
        await AddIssueAuditAsync("InventoryRequisition.Issue", voucher, null,
            new { voucher.VoucherNumber, voucher.Status, voucher.ReceiverUserId, voucher.IssuedAtUtc, Lines = voucher.Lines.Count }, correlationId);
        // Persist voucher-line evidence first inside the same transaction so the SQL issue guard
        // can validate each subsequently inserted stock movement against durable allowed quantity.
        await _unitOfWork.SaveChangesAsync();
        // Finance and Fixed Assets join this same serializable transaction. A posting, custody
        // registration or lineage failure therefore rolls back the issue balances and voucher too.
        await _issueFinanceAssets.PostIssueAsync(voucher.Id);
        foreach (var movement in pendingMovements)
        {
            await _stockMovementRepository.AddAsync(movement);
            await _consignmentSettlementService.TryCreateFromStockMovementAsync(movement);
        }
        await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create("inventory-requisition-issue", voucher.Id, voucher.PayloadHash),
            EventType = "InventoryIssue",
            Action = "Issue",
            Result = ProcurementControlEventResult.Succeeded,
            RuleCode = "TDC-0606",
            RuleVersion = "1",
            DecisionKeys = Enumerable.Range(1, 14).Select(number => $"DEC-{number:D3}").ToList(),
            SourceType = "InventoryIssueVoucher",
            SourceId = voucher.Id,
            SourceReference = voucher.VoucherNumber,
            Reason = voucher.Notes,
            InputValues = new { requisition.Id, requisition.RequisitionNumber, voucher.RequestedById,
                voucher.ApprovedById, voucher.IssuedById, voucher.ReceiverUserId },
            ResultValues = new { voucher.Status, Lines = voucher.Lines.Count, voucher.PayloadHash, voucher.IntegrityHash },
            CorrelationId = correlationId,
            OccurredAtUtc = issuedAt
        });
        await _unitOfWork.SaveChangesAsync();
        if (requisition.ProjectId.HasValue)
        {
            await _projectService.SyncInventoryRequisitionMaterialCostAsync(requisition.Id);
        }

        _logger.LogInformation("Issued items for requisition {RequisitionNumber}", requisition.RequisitionNumber);
        return true;
    }

    public async Task<IReadOnlyList<InventoryIssueReceiverDto>> GetIssueReceiversAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureIssueActor();
        var now = DateTime.UtcNow;
        return await _unitOfWork.Repository<UserTenant>().GetQueryable().AsNoTracking()
            .Where(value => value.TenantId == _currentUserProvider.TenantId && !value.IsDeleted &&
                            value.Status == UserTenantStatus.Active &&
                            (!value.ExpiresAt.HasValue || value.ExpiresAt > now) && value.User.IsActive &&
                            !value.User.UserRoles.Any(role => role.Role.Name == Constants.Roles.ExternalUser))
            .OrderBy(value => value.User.FirstName).ThenBy(value => value.User.LastName).ThenBy(value => value.User.UserName)
            .Select(value => new InventoryIssueReceiverDto
            {
                UserId = value.UserId,
                Username = value.User.UserName ?? value.User.Email ?? value.UserId.ToString(),
                DisplayName = (value.User.FirstName + " " + value.User.LastName).Trim()
            }).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InventoryIssueVoucherDto>> GetIssueVouchersAsync(
        Guid requisitionId,
        CancellationToken cancellationToken = default)
    {
        EnsureIssueActor();
        var requisition = await _requisitionRepository.GetWithItemsAsync(requisitionId)
            ?? throw new InventoryIssueNotFoundException("The requisition was not found in the current tenant.");
        var canReadAll = await CanReadIssueEvidenceAsync(requisition);
        var query = IssueVouchers.AsNoTracking()
            .Where(value => value.TenantId == _currentUserProvider.TenantId &&
                            value.InventoryRequisitionId == requisitionId && !value.IsDeleted);
        if (!canReadAll)
            query = query.Where(value => value.ReceiverUserId == _currentUserProvider.UserId);
        var ids = await query
            .OrderByDescending(value => value.IssuedAtUtc).Select(value => value.Id).ToListAsync(cancellationToken);
        if (!canReadAll && ids.Count == 0)
            throw new InventoryIssueAuthorizationException("You cannot view issue vouchers outside your assigned warehouse location.");
        var result = new List<InventoryIssueVoucherDto>(ids.Count);
        foreach (var voucherId in ids)
        {
            var voucher = await LoadIssueVoucherAsync(voucherId, false, cancellationToken);
            if (voucher is not null) result.Add(MapIssueVoucher(voucher));
        }
        return result;
    }

    public async Task<InventoryIssueVoucherDto?> GetIssueVoucherAsync(
        Guid voucherId,
        CancellationToken cancellationToken = default)
    {
        EnsureIssueActor();
        var voucher = await LoadIssueVoucherAsync(voucherId, false, cancellationToken);
        if (voucher is null) return null;
        if (voucher.ReceiverUserId != _currentUserProvider.UserId &&
            !await CanReadIssueEvidenceAsync(voucher.InventoryRequisition))
            throw new InventoryIssueAuthorizationException("You cannot view this Store Issue Voucher.");
        return MapIssueVoucher(voucher);
    }

    public async Task<InventoryIssueVoucherDto> AcknowledgeIssueVoucherAsync(
        Guid voucherId,
        AcknowledgeInventoryIssueVoucherRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureIssueActor();
        var normalizedCorrelation = NormalizeCorrelation(correlationId);
        var normalizedKey = NormalizeRequired(request.IdempotencyKey, 100, "Idempotency key");
        var normalizedComment = NormalizeRequired(request.Comment, 1000, "Receiver comment");

        return await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try
            {
                await _unitOfWork.AcquireTransactionLockAsync(
                    $"inventory-issue-voucher:{_currentUserProvider.TenantId:N}:{voucherId:N}", cancellationToken);
                var voucher = await LoadIssueVoucherAsync(voucherId, true, cancellationToken)
                    ?? throw new InventoryIssueNotFoundException("The Store Issue Voucher was not found in the current tenant.");
                if (voucher.ReceiverUserId != _currentUserProvider.UserId)
                    throw new InventoryIssueAuthorizationException("Only the designated receiver can acknowledge this handover.");

                var priorAcknowledgement = voucher.Actions.SingleOrDefault(action =>
                    action.ActionType == InventoryIssueVoucherActionType.Acknowledged &&
                    action.PayloadJson.Contains($"\"idempotencyKey\":\"{normalizedKey}\"", StringComparison.Ordinal));
                if (priorAcknowledgement is not null)
                {
                    if (voucher.Status == InventoryIssueVoucherStatus.Acknowledged &&
                        string.Equals(voucher.ReceiverComment, normalizedComment, StringComparison.Ordinal))
                    {
                        await _unitOfWork.CommitAsync(cancellationToken);
                        return MapIssueVoucher(voucher);
                    }
                    throw new InventoryIssueControlException("INV_ISSUE_ACK_IDEMPOTENCY_CONFLICT",
                        "The acknowledgement key already identifies a different receiver response.");
                }

                if (voucher.Status != InventoryIssueVoucherStatus.Issued)
                    throw new InventoryIssueControlException("INV_ISSUE_ACK_STATUS_CONFLICT",
                        "Only an issued voucher awaiting receipt can be acknowledged.");
                EnsureRowVersion(voucher.RowVersion, request.RowVersion, "Store Issue Voucher");
                await EnsureActiveInternalUserAsync(_currentUserProvider.UserId, cancellationToken);

                var before = new { voucher.Status, voucher.AcknowledgedById, voucher.AcknowledgedAtUtc };
                voucher.Status = InventoryIssueVoucherStatus.Acknowledged;
                voucher.AcknowledgedById = _currentUserProvider.UserId;
                voucher.AcknowledgedAtUtc = DateTime.UtcNow;
                voucher.ReceiverComment = normalizedComment;
                voucher.UpdatedAt = DateTime.UtcNow;
                voucher.LastModifiedById = _currentUserProvider.UserId;
                voucher.IntegrityHash = VoucherIntegrity(voucher);
                // The append-only action trigger validates StatusAfter against the
                // durable voucher row. Flush the governed parent first inside this
                // transaction so EF cannot insert the dependent acknowledgement
                // action before the Acknowledged status update.
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await AddVoucherActionAsync(voucher, InventoryIssueVoucherActionType.Acknowledged,
                    voucher.Status, normalizedComment, new { idempotencyKey = normalizedKey }, normalizedCorrelation);
                await AddIssueAuditAsync("InventoryIssueVoucher.Acknowledge", voucher, before,
                    new { voucher.Status, voucher.AcknowledgedById, voucher.AcknowledgedAtUtc, voucher.ReceiverComment },
                    normalizedCorrelation);
                await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
                {
                    EventKey = ProcurementControlEventKey.Create("inventory-issue-acknowledgement", voucher.Id, normalizedKey),
                    EventType = "InventoryIssue",
                    Action = "Acknowledge",
                    Result = ProcurementControlEventResult.Succeeded,
                    RuleCode = "TDC-0606",
                    RuleVersion = "1",
                    DecisionKeys = Enumerable.Range(1, 14).Select(number => $"DEC-{number:D3}").ToList(),
                    SourceType = "InventoryIssueVoucher",
                    SourceId = voucher.Id,
                    SourceReference = voucher.VoucherNumber,
                    Reason = normalizedComment,
                    InputValues = new { voucher.ReceiverUserId, IdempotencyKey = normalizedKey },
                    ResultValues = new { voucher.Status, voucher.AcknowledgedById, voucher.AcknowledgedAtUtc },
                    CorrelationId = normalizedCorrelation,
                    OccurredAtUtc = voucher.AcknowledgedAtUtc.Value
                }, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitAsync(cancellationToken);
                return MapIssueVoucher(voucher);
            }
            catch
            {
                if (_unitOfWork.HasActiveTransaction) await _unitOfWork.RollbackAsync(cancellationToken);
                _unitOfWork.ClearTrackedChanges();
                throw;
            }
        }, cancellationToken);
    }

    public async Task<bool> ReturnAsync(Guid id, ReturnRequisitionDto dto)
    {
        await _returnControls.RequestAsync(id, dto);
        return true;
    }

    public async Task<bool> CompleteAsync(Guid id)
    {
        var requisition = await _requisitionRepository.GetByIdAsync(id)
            ?? throw new ArgumentException($"Requisition {id} not found");

        if (requisition.Status != RequisitionStatus.Issued && requisition.Status != RequisitionStatus.PartiallyIssued)
            throw new InvalidOperationException("Only issued requisitions can be completed");

        requisition.Status = RequisitionStatus.Completed;
        requisition.CompletedDate = DateTime.UtcNow;

        await _requisitionRepository.UpdateAsync(requisition);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Completed requisition {RequisitionNumber}", requisition.RequisitionNumber);
        return true;
    }

    public async Task<bool> CancelAsync(Guid id, string reason)
    {
        if (!_unitOfWork.HasActiveTransaction)
        {
            return await _unitOfWork.ExecuteInStrategyAsync(async () =>
            {
                await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
                try
                {
                    var result = await CancelAsync(id, reason);
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

        await _unitOfWork.AcquireTransactionLockAsync(
            $"inventory-requisition-cancel:{_currentUserProvider.TenantId:N}:{id:N}");
        var requisition = await _requisitionRepository.GetByIdAsync(id)
            ?? throw new ArgumentException($"Requisition {id} not found");

        if (requisition.Status == RequisitionStatus.Completed || requisition.Status == RequisitionStatus.Cancelled)
            throw new InvalidOperationException("Cannot cancel a completed or already cancelled requisition");

        if (requisition.Status == RequisitionStatus.Issued || requisition.Status == RequisitionStatus.PartiallyIssued)
            throw new InvalidOperationException("Cannot cancel a requisition that has been issued. Complete it instead.");

        await _projectReservations.ReleaseForCancelledRequisitionAsync(id, _currentUserProvider.UserId,
            reason, $"requisition-cancel:{id:N}");

        requisition.Status = RequisitionStatus.Cancelled;
        requisition.CancellationReason = reason;

        await _requisitionRepository.UpdateAsync(requisition);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Cancelled requisition {RequisitionNumber}: {Reason}", requisition.RequisitionNumber, reason);
        return true;
    }

    public async Task<InventoryRequisitionItemDto> AddItemAsync(Guid requisitionId, AddRequisitionItemDto dto)
    {
        var requisition = await _requisitionRepository.GetByIdAsync(requisitionId)
            ?? throw new ArgumentException($"Requisition {requisitionId} not found");

        if (requisition.Status != RequisitionStatus.Draft)
            throw new InvalidOperationException("Items can only be added to requisitions in Draft status");

        var item = await _itemRepository.GetByIdAsync(dto.InventoryItemId)
            ?? throw new ArgumentException($"Inventory item {dto.InventoryItemId} not found");

        var itemUnitCost = await ResolveDraftUnitCostAsync(item, requisition.WarehouseId,
            dto.LocationId ?? requisition.LocationId, dto.RequestedQuantity);

        var requisitionItem = new InventoryRequisitionItem
        {
            InventoryRequisitionId = requisitionId,
            InventoryItemId = dto.InventoryItemId,
            ItemCode = item.ItemCode,
            ItemName = item.Name,
            RequestedQuantity = dto.RequestedQuantity,
            ApprovedQuantity = 0,
            IssuedQuantity = 0,
            UnitOfMeasure = item.UnitOfMeasure,
            UnitCost = itemUnitCost,
            LocationId = dto.LocationId,
            LotNumber = dto.LotNumber,
            BatchNumber = dto.BatchNumber,
            SerialNumber = dto.SerialNumber,
            ManufactureDate = dto.ManufactureDate,
            ExpiryDate = dto.ExpiryDate,
            InventoryTrackingExceptionId = dto.InventoryTrackingExceptionId,
            Notes = dto.Notes,
            TenantId = _currentUserProvider.TenantId
        };

        await _requisitionItemRepository.AddAsync(requisitionItem);
        await UpdateRequisitionTotals(requisition, new[] { requisitionItem });
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Added item {ItemCode} to requisition {RequisitionNumber}", item.ItemCode, requisition.RequisitionNumber);

        return new InventoryRequisitionItemDto
        {
            Id = requisitionItem.Id,
            InventoryItemId = requisitionItem.InventoryItemId,
            ItemCode = item.ItemCode,
            ItemName = item.Name,
            RequestedQuantity = requisitionItem.RequestedQuantity,
            ApprovedQuantity = requisitionItem.ApprovedQuantity,
            IssuedQuantity = requisitionItem.IssuedQuantity,
            UnitOfMeasure = requisitionItem.UnitOfMeasure ?? string.Empty,
            UnitCost = requisitionItem.UnitCost,
            TotalCost = requisitionItem.RequestedQuantity * requisitionItem.UnitCost,
            LotNumber = requisitionItem.LotNumber,
            BatchNumber = requisitionItem.BatchNumber,
            SerialNumber = requisitionItem.SerialNumber,
            ManufactureDate = requisitionItem.ManufactureDate,
            ExpiryDate = requisitionItem.ExpiryDate,
            InventoryTrackingExceptionId = requisitionItem.InventoryTrackingExceptionId,
            Notes = requisitionItem.Notes
        };
    }

    public async Task<InventoryRequisitionItemDto> UpdateItemAsync(Guid requisitionId, Guid itemId, UpdateRequisitionItemDto dto)
    {
        var requisition = await _requisitionRepository.GetByIdAsync(requisitionId)
            ?? throw new ArgumentException($"Requisition {requisitionId} not found");

        if (requisition.Status != RequisitionStatus.Draft)
            throw new InvalidOperationException("Items can only be updated on requisitions in Draft status");

        var requisitionItem = await _requisitionItemRepository.GetByIdAsync(itemId)
            ?? throw new ArgumentException($"Requisition item {itemId} not found");

        if (requisitionItem.InventoryRequisitionId != requisitionId)
            throw new ArgumentException("Requisition item does not belong to this requisition");

        var item = await _itemRepository.GetByIdAsync(requisitionItem.InventoryItemId)
            ?? throw new ArgumentException($"Inventory item not found");

        requisitionItem.RequestedQuantity = dto.RequestedQuantity;
        requisitionItem.LocationId = dto.LocationId;
        requisitionItem.UnitCost = await ResolveDraftUnitCostAsync(item, requisition.WarehouseId,
            dto.LocationId ?? requisition.LocationId, dto.RequestedQuantity);
        requisitionItem.LotNumber = dto.LotNumber;
        requisitionItem.BatchNumber = dto.BatchNumber;
        requisitionItem.SerialNumber = dto.SerialNumber;
        requisitionItem.ManufactureDate = dto.ManufactureDate;
        requisitionItem.ExpiryDate = dto.ExpiryDate;
        requisitionItem.InventoryTrackingExceptionId = dto.InventoryTrackingExceptionId;
        requisitionItem.Notes = dto.Notes;

        await _requisitionItemRepository.UpdateAsync(requisitionItem);
        await UpdateRequisitionTotals(requisition, new[] { requisitionItem });
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Updated item {ItemCode} on requisition {RequisitionNumber}", item.ItemCode, requisition.RequisitionNumber);

        return new InventoryRequisitionItemDto
        {
            Id = requisitionItem.Id,
            InventoryItemId = requisitionItem.InventoryItemId,
            ItemCode = item.ItemCode,
            ItemName = item.Name,
            RequestedQuantity = requisitionItem.RequestedQuantity,
            ApprovedQuantity = requisitionItem.ApprovedQuantity,
            IssuedQuantity = requisitionItem.IssuedQuantity,
            UnitOfMeasure = requisitionItem.UnitOfMeasure ?? string.Empty,
            UnitCost = requisitionItem.UnitCost,
            TotalCost = requisitionItem.RequestedQuantity * requisitionItem.UnitCost,
            LotNumber = requisitionItem.LotNumber,
            BatchNumber = requisitionItem.BatchNumber,
            SerialNumber = requisitionItem.SerialNumber,
            ManufactureDate = requisitionItem.ManufactureDate,
            ExpiryDate = requisitionItem.ExpiryDate,
            InventoryTrackingExceptionId = requisitionItem.InventoryTrackingExceptionId,
            Notes = requisitionItem.Notes
        };
    }

    public async Task<bool> RemoveItemAsync(Guid requisitionId, Guid itemId)
    {
        var requisition = await _requisitionRepository.GetByIdAsync(requisitionId)
            ?? throw new ArgumentException($"Requisition {requisitionId} not found");

        if (requisition.Status != RequisitionStatus.Draft)
            throw new InvalidOperationException("Items can only be removed from requisitions in Draft status");

        var requisitionItem = await _requisitionItemRepository.GetByIdAsync(itemId)
            ?? throw new ArgumentException($"Requisition item {itemId} not found");

        if (requisitionItem.InventoryRequisitionId != requisitionId)
            throw new ArgumentException("Requisition item does not belong to this requisition");

        await _requisitionItemRepository.DeleteAsync(requisitionItem);
        await UpdateRequisitionTotals(requisition, removedItemId: itemId);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Removed item from requisition {RequisitionNumber}", requisition.RequisitionNumber);
        return true;
    }

    #region Private Methods

    private IQueryable<InventoryIssueVoucher> IssueVouchers =>
        _unitOfWork.Repository<InventoryIssueVoucher>().GetQueryable();

    private void EnsureIssueActor()
    {
        if (!_currentUserProvider.IsAuthenticated || _currentUserProvider.UserId == Guid.Empty ||
            _currentUserProvider.TenantId == Guid.Empty || _currentUserProvider.IsExternalUser)
            throw new InventoryIssueAuthorizationException("An authenticated internal tenant user is required.");
    }

    private async Task EnsureActiveInternalUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var active = await _unitOfWork.Repository<UserTenant>().GetQueryable().AsNoTracking()
            .AnyAsync(value => value.TenantId == _currentUserProvider.TenantId && value.UserId == userId &&
                               !value.IsDeleted && value.Status == UserTenantStatus.Active &&
                               (!value.ExpiresAt.HasValue || value.ExpiresAt > now) && value.User.IsActive &&
                               !value.User.UserRoles.Any(role => role.Role.Name == Constants.Roles.ExternalUser),
                cancellationToken);
        if (!active)
            throw new InventoryIssueControlException("INV_ISSUE_RECEIVER_INVALID",
                "The receiver is not an active internal user in the current tenant.");
    }

    private async Task<bool> CanReadIssueEvidenceAsync(InventoryRequisition requisition)
    {
        if (_currentUserProvider.UserId == requisition.RequestedById ||
            _currentUserProvider.UserId == requisition.ApprovedById ||
            _currentUserProvider.UserId == requisition.IssuedById)
            return true;
        return await CanReadRequisitionAsync(requisition);
    }

    private async Task<InventoryIssueVoucher?> LoadIssueVoucherAsync(
        Guid voucherId,
        bool tracked,
        CancellationToken cancellationToken)
    {
        IQueryable<InventoryIssueVoucher> query = IssueVouchers
            .Where(value => value.TenantId == _currentUserProvider.TenantId && value.Id == voucherId && !value.IsDeleted)
            .Include(value => value.InventoryRequisition).ThenInclude(value => value.Warehouse)
            .Include(value => value.InventoryRequisition).ThenInclude(value => value.Location)
            .Include(value => value.Warehouse)
            .Include(value => value.Location)
            .Include(value => value.RequestedBy)
            .Include(value => value.ApprovedBy)
            .Include(value => value.IssuedBy)
            .Include(value => value.ReceiverUser)
            .Include(value => value.Lines).ThenInclude(value => value.InventoryItem)
            .Include(value => value.Lines).ThenInclude(value => value.Location)
            .Include(value => value.Actions);
        if (!tracked) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<string> GenerateIssueVoucherNumberAsync(
        DateTime issuedAt,
        CancellationToken cancellationToken = default)
    {
        var prefix = $"SIV-{issuedAt:yyyyMMdd}-";
        var count = await IssueVouchers.CountAsync(value => value.TenantId == _currentUserProvider.TenantId &&
            value.VoucherNumber.StartsWith(prefix), cancellationToken);
        return $"{prefix}{count + 1:D5}";
    }

    private async Task AddVoucherActionAsync(
        InventoryIssueVoucher voucher,
        InventoryIssueVoucherActionType actionType,
        InventoryIssueVoucherStatus status,
        string comment,
        object payload,
        string correlationId)
    {
        var persistedCount = await _unitOfWork.Repository<InventoryIssueVoucherAction>().GetQueryable().AsNoTracking()
            .CountAsync(value => value.TenantId == _currentUserProvider.TenantId &&
                                 value.InventoryIssueVoucherId == voucher.Id && !value.IsDeleted);
        var pendingCount = voucher.Actions.Count(value => value.Id != Guid.Empty && value.CreatedAt == default);
        var sequence = persistedCount + pendingCount + 1;
        var previousHash = voucher.Actions.OrderByDescending(value => value.Sequence)
                               .Select(value => value.IntegrityHash).FirstOrDefault() ?? string.Empty;
        var now = DateTime.UtcNow;
        var payloadJson = JsonSerializer.Serialize(payload);
        var action = new InventoryIssueVoucherAction
        {
            TenantId = _currentUserProvider.TenantId,
            InventoryIssueVoucherId = voucher.Id,
            Sequence = sequence,
            ActionType = actionType,
            StatusAfter = status,
            ActorUserId = _currentUserProvider.UserId,
            ActorName = string.IsNullOrWhiteSpace(_currentUserProvider.FullName)
                ? _currentUserProvider.Username
                : _currentUserProvider.FullName,
            OccurredAtUtc = now,
            Comment = NormalizeRequired(comment, 1000, "Action comment"),
            PayloadJson = payloadJson,
            CorrelationId = NormalizeCorrelation(correlationId),
            CreatedById = _currentUserProvider.UserId
        };
        action.IntegrityHash = Hash(new { previousHash, action.InventoryIssueVoucherId, action.Sequence,
            action.ActionType, action.StatusAfter, action.ActorUserId, action.OccurredAtUtc, action.Comment,
            action.PayloadJson, action.CorrelationId });
        voucher.Actions.Add(action);
        await _unitOfWork.Repository<InventoryIssueVoucherAction>().AddAsync(action);
    }

    private async Task AddIssueAuditAsync(
        string action,
        InventoryIssueVoucher voucher,
        object? before,
        object? after,
        string correlationId)
    {
        await _unitOfWork.Repository<AuditLog>().AddAsync(new AuditLog
        {
            TenantId = _currentUserProvider.TenantId,
            UserId = _currentUserProvider.UserId,
            Username = string.IsNullOrWhiteSpace(_currentUserProvider.Username) ? "Unknown" : _currentUserProvider.Username,
            Action = action,
            Resource = "InventoryIssueVoucher",
            ResourceId = voucher.Id.ToString(),
            OldValues = before is null ? null : JsonSerializer.Serialize(before),
            NewValues = after is null ? null : JsonSerializer.Serialize(after),
            IpAddress = "Service",
            UserAgent = $"Correlation:{NormalizeCorrelation(correlationId)}",
            Timestamp = DateTime.UtcNow
        });
    }

    private static InventoryIssueVoucherDto MapIssueVoucher(InventoryIssueVoucher voucher) => new()
    {
        Id = voucher.Id,
        VoucherNumber = voucher.VoucherNumber,
        InventoryRequisitionId = voucher.InventoryRequisitionId,
        RequisitionNumber = voucher.InventoryRequisition?.RequisitionNumber ?? string.Empty,
        Status = voucher.Status,
        WarehouseId = voucher.WarehouseId,
        WarehouseName = voucher.Warehouse?.Name ?? string.Empty,
        LocationId = voucher.LocationId,
        LocationCode = voucher.Location?.LocationCode,
        DepartmentId = voucher.DepartmentId,
        DepartmentName = voucher.DepartmentName,
        CostCenter = voucher.CostCenter,
        ProjectId = voucher.ProjectId,
        ProjectCode = voucher.ProjectCode,
        RequestedById = voucher.RequestedById,
        RequestedByName = UserName(voucher.RequestedBy),
        ApprovedById = voucher.ApprovedById,
        ApprovedByName = UserName(voucher.ApprovedBy),
        IssuedById = voucher.IssuedById,
        IssuedByName = UserName(voucher.IssuedBy),
        ReceiverUserId = voucher.ReceiverUserId,
        ReceiverName = UserName(voucher.ReceiverUser),
        AcknowledgedById = voucher.AcknowledgedById,
        IssuedAtUtc = voucher.IssuedAtUtc,
        AcknowledgedAtUtc = voucher.AcknowledgedAtUtc,
        Notes = voucher.Notes,
        ReceiverComment = voucher.ReceiverComment,
        MovementReasonCode = voucher.MovementReasonCode,
        FinancePostingEventId = voucher.FinancePostingEventId,
        FinanceJournalEntryId = voucher.FinanceJournalEntryId,
        RowVersion = Convert.ToBase64String(voucher.RowVersion ?? Array.Empty<byte>()),
        Lines = voucher.Lines.OrderBy(value => value.InventoryItem.ItemCode).Select(value => new InventoryIssueVoucherLineDto
        {
            Id = value.Id,
            InventoryRequisitionItemId = value.InventoryRequisitionItemId,
            InventoryItemId = value.InventoryItemId,
            ItemCode = value.InventoryItem?.ItemCode ?? string.Empty,
            ItemName = value.InventoryItem?.Name ?? string.Empty,
            WarehouseId = value.WarehouseId,
            LocationId = value.LocationId,
            LocationCode = value.Location?.LocationCode,
            Quantity = value.Quantity,
            UnitCost = value.UnitCost,
            TotalValue = value.TotalValue,
            UnitOfMeasure = value.UnitOfMeasure,
            LotNumber = value.LotNumber,
            BatchNumber = value.BatchNumber,
            SerialNumber = value.SerialNumber,
            ManufactureDate = value.ManufactureDate,
            ExpiryDate = value.ExpiryDate
        }).ToList(),
        Actions = voucher.Actions.OrderBy(value => value.Sequence).Select(value => new InventoryIssueVoucherActionDto
        {
            Sequence = value.Sequence,
            ActionType = value.ActionType,
            StatusAfter = value.StatusAfter,
            ActorUserId = value.ActorUserId,
            ActorName = value.ActorName,
            OccurredAtUtc = value.OccurredAtUtc,
            Comment = value.Comment
        }).ToList()
    };

    private static string UserName(ApplicationUser? user) => user is null
        ? string.Empty
        : string.IsNullOrWhiteSpace((user.FirstName + " " + user.LastName).Trim())
            ? user.UserName ?? user.Email ?? user.Id.ToString()
            : (user.FirstName + " " + user.LastName).Trim();

    private static string VoucherIntegrity(InventoryIssueVoucher voucher) => Hash(new
    {
        voucher.TenantId,
        voucher.Id,
        voucher.VoucherNumber,
        voucher.InventoryRequisitionId,
        voucher.Status,
        voucher.WarehouseId,
        voucher.LocationId,
        voucher.DepartmentId,
        voucher.CostCenter,
        voucher.ProjectId,
        voucher.RequestedById,
        voucher.ApprovedById,
        voucher.IssuedById,
        voucher.ReceiverUserId,
        voucher.AcknowledgedById,
        voucher.IssuedAtUtc,
        voucher.AcknowledgedAtUtc,
        voucher.ReceiverComment,
        voucher.MovementReasonCode,
        voucher.FinancePostingEventId,
        voucher.FinanceJournalEntryId,
        voucher.IdempotencyKey,
        voucher.PayloadHash,
        voucher.CorrelationId,
        voucher.SourceSnapshotJson
    });

    private static string Hash(object value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));

    private static string NormalizeRequired(string? value, int maxLength, string field)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new InventoryIssueControlException("INV_ISSUE_VALUE_REQUIRED", $"{field} is required.");
        if (normalized.Length > maxLength)
            throw new InventoryIssueControlException("INV_ISSUE_VALUE_TOO_LONG", $"{field} cannot exceed {maxLength} characters.");
        return normalized;
    }

    private static string NormalizeMovementReason(string? value)
    {
        var normalized = NormalizeRequired(value, 50, "Movement reason").ToUpperInvariant();
        if (!InventoryIssueMovementReasons.Labels.ContainsKey(normalized))
            throw new InventoryIssueControlException("INV_ISSUE_MOVEMENT_REASON_INVALID",
                "Select a supported inventory movement reason.");
        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) return null;
        if (normalized.Length > maxLength)
            throw new InventoryIssueControlException("INV_ISSUE_VALUE_TOO_LONG", $"A value cannot exceed {maxLength} characters.");
        return normalized;
    }

    private static string NormalizeCorrelation(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? Guid.NewGuid().ToString("N") : normalized[..Math.Min(100, normalized.Length)];
    }

    private static DateTime? Utc(DateTime? value) => !value.HasValue
        ? null
        : value.Value.Kind == DateTimeKind.Utc ? value.Value : value.Value.ToUniversalTime();

    private static void EnsureRowVersion(byte[] current, string supplied, string resource)
    {
        if (string.IsNullOrWhiteSpace(supplied)) return;
        byte[] expected;
        try { expected = Convert.FromBase64String(supplied.Trim()); }
        catch (FormatException)
        {
            throw new InventoryIssueControlException("INV_ISSUE_ROW_VERSION_INVALID", "RowVersion must be valid base64.");
        }
        if (!current.SequenceEqual(expected))
            throw new InventoryIssueControlException("INV_ISSUE_CONCURRENCY_CONFLICT",
                $"The {resource} changed after it was loaded. Refresh and retry.");
    }

    private async Task<IReadOnlyList<InventoryRequisition>> ApplyReadScopeAsync(
        IEnumerable<InventoryRequisition> requisitions)
    {
        var candidates = requisitions.ToList();
        var readable = new List<InventoryRequisition>();
        var scopeDecisions = new Dictionary<(Guid WarehouseId, Guid? LocationId), bool>();
        foreach (var requisition in candidates)
        {
            if (requisition.RequestedById == _currentUserProvider.UserId)
            {
                readable.Add(requisition);
                continue;
            }

            // A list can contain many requisitions for the same stores scope. Resolve the
            // capability once per distinct scope instead of issuing an authorization query
            // for every row; the decision inputs are identical for that scope.
            var scope = (requisition.WarehouseId, requisition.LocationId);
            if (!scopeDecisions.TryGetValue(scope, out var allowed))
            {
                allowed = await CanReadRequisitionAsync(requisition);
                scopeDecisions.Add(scope, allowed);
            }

            if (allowed)
                readable.Add(requisition);
        }

        return readable;
    }

    private async Task<bool> CanReadRequisitionAsync(InventoryRequisition requisition)
    {
        // Requesters retain access to their own resource; every other actor must hold
        // the inventory-read capability in the exact warehouse/location scope.
        if (requisition.RequestedById == _currentUserProvider.UserId)
            return true;

        try
        {
            var decision = await _accessControl.CheckCapabilityAsync(
                new ProcurementAccessCapabilityRequest
                {
                    PermissionCode = "procurement.inventory.read",
                    WarehouseId = requisition.WarehouseId,
                    LocationId = requisition.LocationId,
                    RequireLocationScope = true,
                    SourceType = "InventoryRequisition",
                    SourceReference = requisition.RequisitionNumber
                },
                Guid.NewGuid().ToString("N"));
            return decision.Allowed;
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

    private async Task<ErpSystem.Core.Entities.Projects.Project?> NormalizeProjectReferenceAsync(Guid? projectId, string? projectCode)
    {
        if (!projectId.HasValue && string.IsNullOrWhiteSpace(projectCode))
        {
            return null;
        }

        ErpSystem.Core.Entities.Projects.Project? project = null;
        if (projectId.HasValue)
        {
            project = await _projectRepository.GetByIdAsync(projectId.Value)
                ?? throw new ArgumentException($"Project {projectId.Value} not found");

            if (!string.IsNullOrWhiteSpace(projectCode) &&
                !string.Equals(project.ProjectCode, projectCode.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Project ID and project code do not match");
            }
        }
        else
        {
            project = await _projectRepository.GetByProjectCodeAsync(projectCode!.Trim())
                ?? throw new ArgumentException($"Project code {projectCode} not found");
        }

        return project;
    }

    private async Task<decimal> ResolveDraftUnitCostAsync(
        InventoryItem item, Guid warehouseId, Guid? locationId, decimal quantity)
    {
        // This is a draft estimate, not a stock posting or a change to the item's
        // configured valuation method. Never substitute standard cost for FIFO/WAC.
        if (item.ValuationMethod == ValuationMethod.StandardCost)
            return decimal.Round(item.StandardCost, 2, MidpointRounding.AwayFromZero);

        var balances = await _unitOfWork.Repository<InventoryBalance>().GetQueryable(value =>
                value.TenantId == _currentUserProvider.TenantId && value.InventoryItemId == item.Id &&
                value.WarehouseId == warehouseId && !value.IsDeleted &&
                (!locationId.HasValue || value.LocationId == locationId))
            .AsNoTracking().ToListAsync();
        var balanceQuantity = balances.Sum(value => value.QuantityOnHand);
        var warehouse = await _warehouseQuantityRepository.GetByWarehouseAndItemAsync(warehouseId, item.Id);
        var fallback = balanceQuantity > 0 ? balances.Sum(value => value.TotalValue) / balanceQuantity
            : warehouse?.AverageCost > 0 ? warehouse.AverageCost
            : item.AverageCost > 0 ? item.AverageCost : item.LastPurchaseCost;

        if (quantity <= 0 || item.ValuationMethod is not (ValuationMethod.FIFO or ValuationMethod.LIFO))
            return decimal.Round(fallback, 2, MidpointRounding.AwayFromZero);

        var query = _unitOfWork.Repository<InventoryLayer>().GetQueryable(value =>
            value.TenantId == _currentUserProvider.TenantId && value.InventoryItemId == item.Id &&
            value.WarehouseId == warehouseId && !value.IsDeleted && !value.IsFullyConsumed &&
            value.RemainingQuantity > 0 && (!locationId.HasValue || value.LocationId == locationId));
        var layers = await (item.ValuationMethod == ValuationMethod.LIFO
                ? query.OrderByDescending(value => value.LayerDate).ThenByDescending(value => value.CreatedAt).ThenByDescending(value => value.Id)
                : query.OrderBy(value => value.LayerDate).ThenBy(value => value.CreatedAt).ThenBy(value => value.Id))
            .AsNoTracking().ToListAsync();
        decimal remaining = quantity, estimatedValue = 0;
        foreach (var layer in layers)
        {
            var taken = Math.Min(remaining, layer.RemainingQuantity);
            estimatedValue += taken * layer.UnitCost;
            remaining -= taken;
            if (remaining == 0) break;
        }
        // Draft demand may exceed stock; the issue gate still checks availability.
        return decimal.Round((estimatedValue + remaining * fallback) / quantity, 2, MidpointRounding.AwayFromZero);
    }

    private async Task UpdateRequisitionTotals(InventoryRequisition requisition,
        IEnumerable<InventoryRequisitionItem>? pendingItems = null, Guid? removedItemId = null)
    {
        // Queries do not include Added entities and may still return a pending
        // soft delete. Merge the command's lines before the single SaveChanges.
        var itemsById = (await _requisitionItemRepository.GetByRequisitionAsync(requisition.Id))
            .ToDictionary(item => item.Id);
        foreach (var item in pendingItems ?? Enumerable.Empty<InventoryRequisitionItem>())
            itemsById[item.Id] = item;
        var items = itemsById.Values.Where(item => !item.IsDeleted && item.Id != removedItemId).ToList();
        requisition.TotalItems = items.Count();
        requisition.TotalQuantity = items.Sum(i => i.RequestedQuantity);
        requisition.TotalValue = items.Sum(i => i.RequestedQuantity * i.UnitCost);
        await _requisitionRepository.UpdateAsync(requisition);
    }

    private static InventoryRequisitionDto MapToDto(InventoryRequisition requisition)
    {
        var items = requisition.Items.Where(item => !item.IsDeleted).ToList();
        return new InventoryRequisitionDto
        {
            Id = requisition.Id,
            RequisitionNumber = requisition.RequisitionNumber,
            Description = requisition.Description,
            DepartmentId = requisition.DepartmentId,
            DepartmentName = requisition.DepartmentName,
            CostCenter = requisition.CostCenter,
            WarehouseId = requisition.WarehouseId,
            WarehouseName = requisition.Warehouse?.Name ?? string.Empty,
            LocationId = requisition.LocationId,
            LocationName = requisition.Location?.LocationCode,
            ProjectId = requisition.ProjectId,
            ProjectCode = requisition.ProjectCode,
            Status = requisition.Status,
            RequisitionType = requisition.RequisitionType,
            Priority = requisition.Priority,
            RequestDate = requisition.RequestDate,
            RequestDateFormatted = requisition.RequestDate.ToString("dd MMM yyyy"),
            RequiredDate = requisition.RequiredDate,
            RequiredDateFormatted = requisition.RequiredDate?.ToString("dd MMM yyyy"),
            IssuedDate = requisition.IssuedDate,
            TotalItems = items.Count,
            TotalQuantity = items.Sum(item => item.RequestedQuantity),
            TotalValue = items.Sum(item => item.RequestedQuantity * item.UnitCost),
            RequestedByName = requisition.RequestedBy != null
                ? $"{requisition.RequestedBy.FirstName} {requisition.RequestedBy.LastName}"
                : null,
            RequestedById = requisition.RequestedById,
            ApprovedById = requisition.ApprovedById,
            ApprovedByName = requisition.ApprovedBy != null
                ? $"{requisition.ApprovedBy.FirstName} {requisition.ApprovedBy.LastName}"
                : null,
            Notes = requisition.Notes,
            Purpose = requisition.Purpose,
            CreatedAtFormatted = requisition.CreatedAt.ToString("dd MMM yyyy HH:mm"),
            RowVersion = Convert.ToBase64String(requisition.RowVersion ?? Array.Empty<byte>())
        };
    }

    private static InventoryRequisitionDetailDto MapToDetailDto(InventoryRequisition requisition)
    {
        var items = requisition.Items.Where(item => !item.IsDeleted).ToList();
        var dto = new InventoryRequisitionDetailDto
        {
            Id = requisition.Id,
            RequisitionNumber = requisition.RequisitionNumber,
            Description = requisition.Description,
            DepartmentId = requisition.DepartmentId,
            DepartmentName = requisition.DepartmentName,
            CostCenter = requisition.CostCenter,
            WarehouseId = requisition.WarehouseId,
            WarehouseName = requisition.Warehouse?.Name ?? string.Empty,
            ProjectId = requisition.ProjectId,
            ProjectCode = requisition.ProjectCode,
            Status = requisition.Status,
            RequisitionType = requisition.RequisitionType,
            Priority = requisition.Priority,
            RequestDate = requisition.RequestDate,
            RequestDateFormatted = requisition.RequestDate.ToString("dd MMM yyyy"),
            RequiredDate = requisition.RequiredDate,
            RequiredDateFormatted = requisition.RequiredDate?.ToString("dd MMM yyyy"),
            IssuedDate = requisition.IssuedDate,
            TotalItems = items.Count,
            TotalQuantity = items.Sum(item => item.RequestedQuantity),
            TotalValue = items.Sum(item => item.RequestedQuantity * item.UnitCost),
            RequestedByName = requisition.RequestedBy != null
                ? $"{requisition.RequestedBy.FirstName} {requisition.RequestedBy.LastName}"
                : null,
            RequestedById = requisition.RequestedById,
            ApprovedById = requisition.ApprovedById,
            ApprovedByName = requisition.ApprovedBy != null
                ? $"{requisition.ApprovedBy.FirstName} {requisition.ApprovedBy.LastName}"
                : null,
            Notes = requisition.Notes,
            Purpose = requisition.Purpose,
            CreatedAtFormatted = requisition.CreatedAt.ToString("dd MMM yyyy HH:mm"),
            RowVersion = Convert.ToBase64String(requisition.RowVersion ?? Array.Empty<byte>()),
            ApprovalDate = requisition.ApprovalDate,
            CompletedDate = requisition.CompletedDate,
            IssuedByName = requisition.IssuedBy != null
                ? $"{requisition.IssuedBy.FirstName} {requisition.IssuedBy.LastName}"
                : null,
            RejectionReason = requisition.RejectionReason,
            CancellationReason = requisition.CancellationReason,
            LocationId = requisition.LocationId,
            LocationName = requisition.Location?.LocationCode,
            Items = items.Select(i => new InventoryRequisitionItemDto
            {
                Id = i.Id,
                InventoryItemId = i.InventoryItemId,
                ItemCode = i.ItemCode ?? i.InventoryItem?.ItemCode ?? string.Empty,
                ItemName = i.ItemName ?? i.InventoryItem?.Name ?? string.Empty,
                RequestedQuantity = i.RequestedQuantity,
                ApprovedQuantity = i.ApprovedQuantity,
                IssuedQuantity = i.IssuedQuantity,
                UnitOfMeasure = i.UnitOfMeasure ?? string.Empty,
                UnitCost = i.UnitCost,
                TotalCost = i.RequestedQuantity * i.UnitCost,
                LotNumber = i.LotNumber,
                BatchNumber = i.BatchNumber,
                SerialNumber = i.SerialNumber,
                ManufactureDate = i.ManufactureDate,
                ExpiryDate = i.ExpiryDate,
                InventoryTrackingExceptionId = i.InventoryTrackingExceptionId,
                LocationId = i.LocationId,
                LocationName = i.Location?.LocationCode,
                Notes = i.Notes
            }).ToList()
        };

        return dto;
    }

    #endregion
}
