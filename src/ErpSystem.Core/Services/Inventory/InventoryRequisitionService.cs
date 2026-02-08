using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
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
    private readonly IWarehouseQuantityRepository _warehouseQuantityRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IWorkflowIntegrationService _workflowIntegrationService;
    private readonly IWorkflowStatusAdapterRegistry _workflowStatusAdapterRegistry;
    private readonly ILogger<InventoryRequisitionService> _logger;

    public InventoryRequisitionService(
        IInventoryRequisitionRepository requisitionRepository,
        IInventoryRequisitionItemRepository requisitionItemRepository,
        IInventoryItemRepository itemRepository,
        IWarehouseRepository warehouseRepository,
        IWarehouseQuantityRepository warehouseQuantityRepository,
        IStockMovementRepository stockMovementRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        IWorkflowIntegrationService workflowIntegrationService,
        IWorkflowStatusAdapterRegistry workflowStatusAdapterRegistry,
        ILogger<InventoryRequisitionService> logger)
    {
        _requisitionRepository = requisitionRepository;
        _requisitionItemRepository = requisitionItemRepository;
        _itemRepository = itemRepository;
        _warehouseRepository = warehouseRepository;
        _warehouseQuantityRepository = warehouseQuantityRepository;
        _stockMovementRepository = stockMovementRepository;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _workflowIntegrationService = workflowIntegrationService;
        _workflowStatusAdapterRegistry = workflowStatusAdapterRegistry;
        _logger = logger;
    }

    public async Task<IEnumerable<InventoryRequisitionDto>> GetAllAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var requisitions = await _requisitionRepository.GetByDateRangeAsync(
            fromDate ?? DateTime.UtcNow.AddMonths(-3),
            toDate ?? DateTime.UtcNow);
        return requisitions.Select(MapToDto);
    }

    public async Task<IEnumerable<InventoryRequisitionDto>> GetByWarehouseAsync(Guid warehouseId)
    {
        var requisitions = await _requisitionRepository.GetByWarehouseAsync(warehouseId);
        return requisitions.Select(MapToDto);
    }

    public async Task<IEnumerable<InventoryRequisitionDto>> GetByDepartmentAsync(Guid departmentId)
    {
        var requisitions = await _requisitionRepository.GetByDepartmentAsync(departmentId);
        return requisitions.Select(MapToDto);
    }

    public async Task<IEnumerable<InventoryRequisitionDto>> GetPendingApprovalAsync()
    {
        var requisitions = await _requisitionRepository.GetPendingApprovalAsync();
        return requisitions.Select(MapToDto);
    }

    public async Task<IEnumerable<InventoryRequisitionDto>> GetPendingIssueAsync()
    {
        var requisitions = await _requisitionRepository.GetPendingIssueAsync();
        return requisitions.Select(MapToDto);
    }

    public async Task<InventoryRequisitionDetailDto?> GetByIdAsync(Guid id)
    {
        var requisition = await _requisitionRepository.GetWithItemsAsync(id);
        return requisition != null ? MapToDetailDto(requisition) : null;
    }

    public async Task<InventoryRequisitionDetailDto?> GetByRequisitionNumberAsync(string requisitionNumber)
    {
        var requisition = await _requisitionRepository.GetByRequisitionNumberAsync(requisitionNumber);
        if (requisition == null) return null;
        var fullRequisition = await _requisitionRepository.GetWithItemsAsync(requisition.Id);
        return fullRequisition != null ? MapToDetailDto(fullRequisition) : null;
    }

    public async Task<InventoryRequisitionDetailDto> CreateAsync(CreateInventoryRequisitionDto dto)
    {
        var warehouse = await _warehouseRepository.GetByIdAsync(dto.WarehouseId)
            ?? throw new ArgumentException($"Warehouse {dto.WarehouseId} not found");

        var requisition = new InventoryRequisition
        {
            RequisitionNumber = await _requisitionRepository.GenerateRequisitionNumberAsync(),
            DepartmentId = dto.DepartmentId,
            DepartmentName = dto.DepartmentName,
            CostCenter = dto.CostCenter,
            WarehouseId = dto.WarehouseId,
            LocationId = dto.LocationId,
            ProjectId = dto.ProjectId,
            ProjectCode = dto.ProjectCode,
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
            foreach (var itemDto in dto.Items)
            {
                var item = await _itemRepository.GetByIdAsync(itemDto.InventoryItemId)
                    ?? throw new ArgumentException($"Inventory item {itemDto.InventoryItemId} not found");

                var itemUnitCost = item.AverageCost > 0 ? item.AverageCost
                    : (item.StandardCost > 0 ? item.StandardCost : item.LastPurchaseCost);

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
                    SerialNumber = itemDto.SerialNumber,
                    Notes = itemDto.Notes,
                    TenantId = _currentUserProvider.TenantId
                };

                await _requisitionItemRepository.AddAsync(requisitionItem);
            }

            await UpdateRequisitionTotals(requisition);
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
        if (dto.ProjectId.HasValue) requisition.ProjectId = dto.ProjectId;
        if (dto.ProjectCode != null) requisition.ProjectCode = dto.ProjectCode;
        if (dto.RequiredDate.HasValue) requisition.RequiredDate = dto.RequiredDate;
        if (dto.Purpose != null) requisition.Purpose = dto.Purpose;
        if (dto.Notes != null) requisition.Notes = dto.Notes;

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
        var requisition = await _requisitionRepository.GetWithItemsAsync(id)
            ?? throw new ArgumentException($"Requisition {id} not found");

        if (requisition.Status != RequisitionStatus.Approved &&
            requisition.Status != RequisitionStatus.InProgress &&
            requisition.Status != RequisitionStatus.PartiallyIssued)
            throw new InvalidOperationException("Only approved or in-progress requisitions can be issued");

        var warehouse = await _warehouseRepository.GetByIdAsync(requisition.WarehouseId)
            ?? throw new ArgumentException($"Warehouse not found");

        foreach (var issueItem in dto.Items)
        {
            var requisitionItem = requisition.Items.FirstOrDefault(i => i.Id == issueItem.ItemId)
                ?? throw new ArgumentException($"Requisition item {issueItem.ItemId} not found");

            var remainingToIssue = requisitionItem.ApprovedQuantity - requisitionItem.IssuedQuantity;
            if (issueItem.IssuedQuantity > remainingToIssue)
                throw new InvalidOperationException($"Cannot issue more than remaining quantity for {requisitionItem.ItemCode}");

            // Check stock availability
            var warehouseQty = await _warehouseQuantityRepository.GetByWarehouseAndItemAsync(requisition.WarehouseId, requisitionItem.InventoryItemId);
            if (warehouseQty == null || warehouseQty.AvailableStock < issueItem.IssuedQuantity)
                throw new InvalidOperationException($"Insufficient stock for {requisitionItem.ItemCode}");

            // Update issued quantity
            requisitionItem.IssuedQuantity += issueItem.IssuedQuantity;
            requisitionItem.LineValue = requisitionItem.IssuedQuantity * requisitionItem.UnitCost;
            if (issueItem.LocationId.HasValue) requisitionItem.LocationId = issueItem.LocationId;
            if (issueItem.LotNumber != null) requisitionItem.LotNumber = issueItem.LotNumber;
            if (issueItem.SerialNumber != null) requisitionItem.SerialNumber = issueItem.SerialNumber;

            await _requisitionItemRepository.UpdateAsync(requisitionItem);

            // Deduct from warehouse quantity
            warehouseQty.CurrentStock -= issueItem.IssuedQuantity;
            warehouseQty.AvailableStock -= issueItem.IssuedQuantity;
            await _warehouseQuantityRepository.UpdateAsync(warehouseQty);

            // Update inventory item quantities
            var inventoryItem = await _itemRepository.GetByIdAsync(requisitionItem.InventoryItemId);
            if (inventoryItem != null)
            {
                inventoryItem.CurrentStock -= issueItem.IssuedQuantity;
                inventoryItem.AvailableStock -= issueItem.IssuedQuantity;
                await _itemRepository.UpdateAsync(inventoryItem);
            }

            // Create stock movement
            var movement = new StockMovement
            {
                InventoryItemId = requisitionItem.InventoryItemId,
                MovementType = "Issue",
                MovementDate = DateTime.UtcNow,
                Quantity = -issueItem.IssuedQuantity,
                UnitCost = requisitionItem.UnitCost,
                TotalValue = issueItem.IssuedQuantity * requisitionItem.UnitCost,
                ReferenceType = ReferenceType.Requisition,
                ReferenceNumber = requisition.RequisitionNumber,
                ReferenceId = requisition.Id,
                LocationId = issueItem.LocationId ?? requisition.LocationId,
                Notes = $"Issued for requisition {requisition.RequisitionNumber}",
                TenantId = _currentUserProvider.TenantId
            };
            await _stockMovementRepository.AddAsync(movement);
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
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Issued items for requisition {RequisitionNumber}", requisition.RequisitionNumber);
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
        var requisition = await _requisitionRepository.GetByIdAsync(id)
            ?? throw new ArgumentException($"Requisition {id} not found");

        if (requisition.Status == RequisitionStatus.Completed || requisition.Status == RequisitionStatus.Cancelled)
            throw new InvalidOperationException("Cannot cancel a completed or already cancelled requisition");

        if (requisition.Status == RequisitionStatus.Issued || requisition.Status == RequisitionStatus.PartiallyIssued)
            throw new InvalidOperationException("Cannot cancel a requisition that has been issued. Complete it instead.");

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

        var itemUnitCost = item.AverageCost > 0 ? item.AverageCost
            : (item.StandardCost > 0 ? item.StandardCost : item.LastPurchaseCost);

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
            SerialNumber = dto.SerialNumber,
            Notes = dto.Notes,
            TenantId = _currentUserProvider.TenantId
        };

        await _requisitionItemRepository.AddAsync(requisitionItem);
        await UpdateRequisitionTotals(requisition);
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
            SerialNumber = requisitionItem.SerialNumber,
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
        requisitionItem.LotNumber = dto.LotNumber;
        requisitionItem.SerialNumber = dto.SerialNumber;
        requisitionItem.Notes = dto.Notes;

        await _requisitionItemRepository.UpdateAsync(requisitionItem);
        await UpdateRequisitionTotals(requisition);
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
            SerialNumber = requisitionItem.SerialNumber,
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
        await UpdateRequisitionTotals(requisition);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Removed item from requisition {RequisitionNumber}", requisition.RequisitionNumber);
        return true;
    }

    #region Private Methods

    private async Task UpdateRequisitionTotals(InventoryRequisition requisition)
    {
        var items = await _requisitionItemRepository.GetByRequisitionAsync(requisition.Id);
        requisition.TotalItems = items.Count();
        requisition.TotalQuantity = items.Sum(i => i.RequestedQuantity);
        requisition.TotalValue = items.Sum(i => i.RequestedQuantity * i.UnitCost);
        await _requisitionRepository.UpdateAsync(requisition);
    }

    private static InventoryRequisitionDto MapToDto(InventoryRequisition requisition)
    {
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
            TotalItems = requisition.TotalItems,
            TotalQuantity = requisition.TotalQuantity,
            TotalValue = requisition.TotalValue,
            RequestedByName = requisition.RequestedBy != null
                ? $"{requisition.RequestedBy.FirstName} {requisition.RequestedBy.LastName}"
                : null,
            ApprovedByName = requisition.ApprovedBy != null
                ? $"{requisition.ApprovedBy.FirstName} {requisition.ApprovedBy.LastName}"
                : null,
            Notes = requisition.Notes,
            Purpose = requisition.Purpose,
            CreatedAtFormatted = requisition.CreatedAt.ToString("dd MMM yyyy HH:mm")
        };
    }

    private static InventoryRequisitionDetailDto MapToDetailDto(InventoryRequisition requisition)
    {
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
            TotalItems = requisition.TotalItems,
            TotalQuantity = requisition.TotalQuantity,
            TotalValue = requisition.TotalValue,
            RequestedByName = requisition.RequestedBy != null
                ? $"{requisition.RequestedBy.FirstName} {requisition.RequestedBy.LastName}"
                : null,
            ApprovedByName = requisition.ApprovedBy != null
                ? $"{requisition.ApprovedBy.FirstName} {requisition.ApprovedBy.LastName}"
                : null,
            Notes = requisition.Notes,
            Purpose = requisition.Purpose,
            CreatedAtFormatted = requisition.CreatedAt.ToString("dd MMM yyyy HH:mm"),
            ApprovalDate = requisition.ApprovalDate,
            CompletedDate = requisition.CompletedDate,
            IssuedByName = requisition.IssuedBy != null
                ? $"{requisition.IssuedBy.FirstName} {requisition.IssuedBy.LastName}"
                : null,
            RejectionReason = requisition.RejectionReason,
            CancellationReason = requisition.CancellationReason,
            LocationId = requisition.LocationId,
            LocationName = requisition.Location?.LocationCode,
            Items = requisition.Items.Select(i => new InventoryRequisitionItemDto
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
                SerialNumber = i.SerialNumber,
                LocationId = i.LocationId,
                LocationName = i.Location?.LocationCode,
                Notes = i.Notes
            }).ToList()
        };

        return dto;
    }

    #endregion
}
