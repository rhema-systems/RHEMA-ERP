using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.Extensions.Logging;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Maintenance;

namespace ErpSystem.Core.Services.Maintenance;

/// <summary>
/// Service for managing inventory operations related to maintenance work orders
/// Handles parts allocation, consumption, and returns
/// </summary>
public class MaintenanceInventoryService : IMaintenanceInventoryService
{
    private readonly IInventoryManagementService _inventoryService;
    private readonly IWorkOrderRepository _workOrderRepository;
    private readonly IWorkOrderPartRepository _workOrderPartRepository;
    private readonly IInventoryAllocationRepository _allocationRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly ILogger<MaintenanceInventoryService> _logger;

    public MaintenanceInventoryService(
        IInventoryManagementService inventoryService,
        IWorkOrderRepository workOrderRepository,
        IWorkOrderPartRepository workOrderPartRepository,
        IInventoryAllocationRepository allocationRepository,
        IStockMovementRepository stockMovementRepository,
        ILogger<MaintenanceInventoryService> logger)
    {
        _inventoryService = inventoryService;
        _workOrderRepository = workOrderRepository;
        _workOrderPartRepository = workOrderPartRepository;
        _allocationRepository = allocationRepository;
        _stockMovementRepository = stockMovementRepository;
        _logger = logger;
    }

    #region Parts Allocation

    /// <summary>
    /// Allocates parts for all required items in a work order
    /// </summary>
    public async Task<MaintenancePartsAllocationResult> AllocateWorkOrderPartsAsync(Guid workOrderId, Guid userId)
    {
        try
        {
            _logger.LogInformation("Starting parts allocation for work order {WorkOrderId}", workOrderId);

            var workOrder = await _workOrderRepository.GetByIdAsync(workOrderId);
            if (workOrder == null)
                throw new ArgumentException($"Work order {workOrderId} not found");

            if (workOrder.Status != "Approved" && workOrder.Status != "Assigned")
                throw new InvalidOperationException($"Cannot allocate parts for work order in {workOrder.Status} status");

            var result = new MaintenancePartsAllocationResult
            {
                WorkOrderId = workOrderId,
                TotalPartsRequested = workOrder.Parts.Count,
                AllocatedParts = new List<WorkOrderPartAllocationDto>(),
                FailedAllocations = new List<PartAllocationFailure>()
            };

            foreach (var part in workOrder.Parts.Where(p => p.Status == "Required"))
            {
                try
                {
                    var allocation = await AllocateWorkOrderPartAsync(part, userId);
                    result.AllocatedParts.Add(allocation);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to allocate part {PartId} for work order {WorkOrderId}", 
                        part.Id, workOrderId);
                    
                    result.FailedAllocations.Add(new PartAllocationFailure
                    {
                        PartId = part.Id,
                        PartName = part.ItemName,
                        RequestedQuantity = part.QuantityRequired,
                        Reason = ex.Message
                    });
                }
            }

            result.SuccessfulAllocations = result.AllocatedParts.Count;
            result.FailedAllocationCount = result.FailedAllocations.Count;
            result.AllocationDate = DateTime.UtcNow;

            // Update work order status if all parts are allocated
            if (result.FailedAllocationCount == 0 && result.SuccessfulAllocations > 0)
            {
                await UpdateWorkOrderStatusAsync(workOrderId, "PartsAllocated", userId);
            }

            _logger.LogInformation("Completed parts allocation for work order {WorkOrderId}. " +
                "Success: {Success}, Failed: {Failed}", 
                workOrderId, result.SuccessfulAllocations, result.FailedAllocationCount);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error allocating parts for work order {WorkOrderId}", workOrderId);
            throw;
        }
    }

    /// <summary>
    /// Allocates a specific part for a work order
    /// </summary>
    private async Task<WorkOrderPartAllocationDto> AllocateWorkOrderPartAsync(WorkOrderPart part, Guid userId)
    {
        // Create allocation request
        var allocationRequest = new AllocateInventoryDto
        {
            InventoryItemId = part.InventoryItemId,
            Quantity = part.QuantityRequired,
            // ReferenceType = "WorkOrder", // Property doesn't exist in DTO
            ReferenceNumber = $"WO-{part.WorkOrder?.WorkOrderNumber}",
            ReferenceId = part.WorkOrderId,
            RequiredDate = part.WorkOrder?.RequestedStartDate,
            Notes = $"Allocated for work order part: {part.ItemName}",
            UserId = userId
        };

        // Allocate through inventory service
        var allocation = await _inventoryService.AllocateForWorkOrderAsync(allocationRequest);

        // Update work order part record
        part.AllocationId = allocation.Id;
        part.QuantityAllocated = allocation.AllocatedQuantity;
        part.AllocatedAt = allocation.AllocationDate;
        part.Status = "Allocated";

        await _workOrderPartRepository.UpdateAsync(part);

        return new WorkOrderPartAllocationDto
        {
            PartId = part.Id,
            ItemCode = part.ItemCode,
            ItemName = part.ItemName,
            QuantityRequired = part.QuantityRequired,
            QuantityAllocated = part.QuantityAllocated,
            AllocationId = allocation.Id,
            LocationCode = allocation.LocationCode,
            // LocationName = allocation.LocationName, // Property doesn't exist in DTO
            AllocatedAt = allocation.AllocationDate
        };
    }

    #endregion

    #region Parts Consumption

    /// <summary>
    /// Consumes parts when work order tasks are completed
    /// </summary>
    public async Task<MaintenancePartsConsumptionResult> ConsumeWorkOrderPartsAsync(
        Guid workOrderId, List<PartConsumptionDto> consumptions, Guid userId)
    {
        try
        {
            _logger.LogInformation("Starting parts consumption for work order {WorkOrderId}", workOrderId);

            var workOrder = await _workOrderRepository.GetByIdAsync(workOrderId);
            if (workOrder == null)
                throw new ArgumentException($"Work order {workOrderId} not found");

            var result = new MaintenancePartsConsumptionResult
            {
                WorkOrderId = workOrderId,
                TotalPartsProcessed = consumptions.Count,
                ConsumedParts = new List<PartConsumptionResultDto>(),
                ConsumptionFailures = new List<PartConsumptionFailure>()
            };

            foreach (var consumption in consumptions)
            {
                try
                {
                    var consumptionResult = await ConsumeWorkOrderPartAsync(
                        consumption.PartId, consumption.QuantityConsumed, userId);
                    result.ConsumedParts.Add(consumptionResult);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to consume part {PartId} for work order {WorkOrderId}", 
                        consumption.PartId, workOrderId);
                    
                    result.ConsumptionFailures.Add(new PartConsumptionFailure
                    {
                        PartId = consumption.PartId,
                        RequestedQuantity = consumption.QuantityConsumed,
                        Reason = ex.Message
                    });
                }
            }

            result.SuccessfulConsumptions = result.ConsumedParts.Count;
            result.FailedConsumptions = result.ConsumptionFailures.Count;
            result.ConsumptionDate = DateTime.UtcNow;

            _logger.LogInformation("Completed parts consumption for work order {WorkOrderId}. " +
                "Success: {Success}, Failed: {Failed}", 
                workOrderId, result.SuccessfulConsumptions, result.FailedConsumptions);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error consuming parts for work order {WorkOrderId}", workOrderId);
            throw;
        }
    }

    /// <summary>
    /// Consumes a specific part quantity
    /// </summary>
    private async Task<PartConsumptionResultDto> ConsumeWorkOrderPartAsync(
        Guid partId, decimal quantityConsumed, Guid userId)
    {
        var part = await _workOrderPartRepository.GetByIdAsync(partId);
        if (part == null)
            throw new ArgumentException($"Work order part {partId} not found");

        if (part.AllocationId == null)
            throw new InvalidOperationException($"Part {part.ItemName} is not allocated");

        if (quantityConsumed > part.QuantityAllocated)
            throw new InvalidOperationException(
                $"Cannot consume {quantityConsumed} units. Only {part.QuantityAllocated} allocated");

        // Simplified consumption without missing DTO - consuming directly via inventory service
        var consumeSuccess = await _inventoryService.ConsumeAllocatedInventoryAsync(
            part.AllocationId.Value, quantityConsumed, userId);

        if (!consumeSuccess)
            throw new InvalidOperationException("Failed to consume inventory");

        // Create a placeholder consumption result
        var consumption = new { 
            ConsumptionDate = DateTime.UtcNow, 
            UnitCost = 0m, 
            TotalCost = 0m 
        };

        // Update work order part record
        part.QuantityUsed += quantityConsumed;
        part.UsedAt = consumption.ConsumptionDate;
        
        if (part.QuantityUsed >= part.QuantityRequired)
        {
            part.Status = "Used";
        }
        else
        {
            part.Status = "PartiallyUsed";
        }

        await _workOrderPartRepository.UpdateAsync(part);

        return new PartConsumptionResultDto
        {
            PartId = part.Id,
            ItemCode = part.ItemCode,
            ItemName = part.ItemName,
            QuantityConsumed = quantityConsumed,
            TotalQuantityUsed = part.QuantityUsed,
            ConsumptionDate = consumption.ConsumptionDate,
            UnitCost = consumption.UnitCost,
            TotalCost = consumption.TotalCost
        };
    }

    #endregion

    #region Parts Returns

    /// <summary>
    /// Returns unused parts from a work order
    /// </summary>
    public async Task<MaintenancePartsReturnResult> ReturnWorkOrderPartsAsync(
        Guid workOrderId, List<PartReturnDto> returns, Guid userId)
    {
        try
        {
            _logger.LogInformation("Starting parts return for work order {WorkOrderId}", workOrderId);

            var result = new MaintenancePartsReturnResult
            {
                WorkOrderId = workOrderId,
                TotalPartsProcessed = returns.Count,
                ReturnedParts = new List<PartReturnResultDto>(),
                ReturnFailures = new List<PartReturnFailure>()
            };

            foreach (var returnRequest in returns)
            {
                try
                {
                    var returnResult = await ReturnWorkOrderPartAsync(
                        returnRequest.PartId, returnRequest.QuantityReturned, 
                        returnRequest.ReturnReason, userId);
                    result.ReturnedParts.Add(returnResult);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to return part {PartId} for work order {WorkOrderId}", 
                        returnRequest.PartId, workOrderId);
                    
                    result.ReturnFailures.Add(new PartReturnFailure
                    {
                        PartId = returnRequest.PartId,
                        RequestedQuantity = returnRequest.QuantityReturned,
                        Reason = ex.Message
                    });
                }
            }

            result.SuccessfulReturns = result.ReturnedParts.Count;
            result.FailedReturns = result.ReturnFailures.Count;
            result.ReturnDate = DateTime.UtcNow;

            _logger.LogInformation("Completed parts return for work order {WorkOrderId}. " +
                "Success: {Success}, Failed: {Failed}", 
                workOrderId, result.SuccessfulReturns, result.FailedReturns);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error returning parts for work order {WorkOrderId}", workOrderId);
            throw;
        }
    }

    /// <summary>
    /// Returns a specific part quantity
    /// </summary>
    private async Task<PartReturnResultDto> ReturnWorkOrderPartAsync(
        Guid partId, decimal quantityReturned, string returnReason, Guid userId)
    {
        var part = await _workOrderPartRepository.GetByIdAsync(partId);
        if (part == null)
            throw new ArgumentException($"Work order part {partId} not found");

        if (part.AllocationId == null)
            throw new InvalidOperationException($"Part {part.ItemName} has no allocation to return from");

        var availableToReturn = part.QuantityAllocated - part.QuantityUsed;
        if (quantityReturned > availableToReturn)
            throw new InvalidOperationException(
                $"Cannot return {quantityReturned} units. Only {availableToReturn} available to return");

        // Simplified return without missing DTO - returning directly via inventory service
        var returnSuccess = await _inventoryService.ReleaseAllocationAsync(
            part.AllocationId.Value, userId);

        if (!returnSuccess)
            throw new InvalidOperationException("Failed to return inventory");

        // Create a placeholder return result
        var returnResult = new { 
            ReturnDate = DateTime.UtcNow
        };

        // Update work order part record
        part.QuantityReturned += quantityReturned;
        part.Status = part.QuantityUsed > 0 ? "PartiallyUsed" : "Returned";

        await _workOrderPartRepository.UpdateAsync(part);

        return new PartReturnResultDto
        {
            PartId = part.Id,
            ItemCode = part.ItemCode,
            ItemName = part.ItemName,
            QuantityReturned = quantityReturned,
            TotalQuantityReturned = part.QuantityReturned,
            ReturnDate = returnResult.ReturnDate,
            ReturnReason = returnReason
        };
    }

    #endregion

    #region Work Order Integration

    /// <summary>
    /// Gets current parts status for a work order
    /// </summary>
    public async Task<WorkOrderPartsStatusDto> GetWorkOrderPartsStatusAsync(Guid workOrderId)
    {
        try
        {
            var workOrder = await _workOrderRepository.GetByIdAsync(workOrderId);
            if (workOrder == null)
                throw new ArgumentException($"Work order {workOrderId} not found");

            var partsStatus = workOrder.Parts.Select(part => new WorkOrderPartStatusDto
            {
                PartId = part.Id,
                ItemCode = part.ItemCode,
                ItemName = part.ItemName,
                Description = part.Description,
                QuantityRequired = part.QuantityRequired,
                QuantityAllocated = part.QuantityAllocated,
                QuantityUsed = part.QuantityUsed,
                QuantityReturned = part.QuantityReturned,
                Status = part.Status,
                UnitCost = part.UnitCost,
                TotalCost = part.TotalCost,
                AllocationId = part.AllocationId,
                AllocatedAt = part.AllocatedAt,
                UsedAt = part.UsedAt,
                Notes = part.Notes
            }).ToList();

            return new WorkOrderPartsStatusDto
            {
                WorkOrderId = workOrderId,
                WorkOrderNumber = workOrder.WorkOrderNumber,
                TotalParts = partsStatus.Count,
                AllocatedParts = partsStatus.Count(p => p.Status == "Allocated" || p.Status == "PartiallyUsed" || p.Status == "Used"),
                ConsumedParts = partsStatus.Count(p => p.Status == "Used" || p.Status == "PartiallyUsed"),
                ReturnedParts = partsStatus.Count(p => p.Status == "Returned"),
                TotalEstimatedCost = partsStatus.Sum(p => p.TotalCost),
                Parts = partsStatus
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting parts status for work order {WorkOrderId}", workOrderId);
            throw;
        }
    }

    /// <summary>
    /// Checks if all required parts are available for allocation
    /// </summary>
    public async Task<PartsAvailabilityCheckResult> CheckPartsAvailabilityAsync(Guid workOrderId)
    {
        try
        {
            var workOrder = await _workOrderRepository.GetByIdAsync(workOrderId);
            if (workOrder == null)
                throw new ArgumentException($"Work order {workOrderId} not found");

            var availabilityChecks = new List<PartAvailabilityDto>();

            foreach (var part in workOrder.Parts)
            {
                var itemDetail = await _inventoryService.GetInventoryItemDetailAsync(part.InventoryItemId);
                if (itemDetail == null)
                {
                    availabilityChecks.Add(new PartAvailabilityDto
                    {
                        PartId = part.Id,
                        ItemCode = part.ItemCode,
                        ItemName = part.ItemName,
                        QuantityRequired = part.QuantityRequired,
                        AvailableQuantity = 0,
                        IsAvailable = false,
                        AvailabilityIssue = "Item not found in inventory"
                    });
                    continue;
                }

                var isAvailable = itemDetail.AvailableStock >= part.QuantityRequired;
                availabilityChecks.Add(new PartAvailabilityDto
                {
                    PartId = part.Id,
                    ItemCode = part.ItemCode,
                    ItemName = part.ItemName,
                    QuantityRequired = part.QuantityRequired,
                    AvailableQuantity = itemDetail.AvailableStock,
                    IsAvailable = isAvailable,
                    AvailabilityIssue = isAvailable ? null : $"Insufficient stock (need {part.QuantityRequired}, have {itemDetail.AvailableStock})"
                });
            }

            return new PartsAvailabilityCheckResult
            {
                WorkOrderId = workOrderId,
                AllPartsAvailable = availabilityChecks.All(c => c.IsAvailable),
                TotalParts = availabilityChecks.Count,
                AvailableParts = availabilityChecks.Count(c => c.IsAvailable),
                UnavailableParts = availabilityChecks.Count(c => !c.IsAvailable),
                CheckDate = DateTime.UtcNow,
                PartAvailability = availabilityChecks
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking parts availability for work order {WorkOrderId}", workOrderId);
            throw;
        }
    }

    /// <summary>
    /// Updates work order status based on parts operations
    /// </summary>
    private async Task UpdateWorkOrderStatusAsync(Guid workOrderId, string status, Guid userId)
    {
        var workOrder = await _workOrderRepository.GetByIdAsync(workOrderId);
        if (workOrder != null)
        {
            workOrder.Status = status;
            // Add status change logic here if needed
            await _workOrderRepository.UpdateAsync(workOrder);
        }
    }

    #endregion
}

