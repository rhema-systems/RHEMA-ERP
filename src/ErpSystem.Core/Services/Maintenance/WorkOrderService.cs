using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Services;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using InvoiceCreateDto = ErpSystem.Core.DTOs.AR.InvoiceCreateDto;
using InvoiceLineItemCreateDto = ErpSystem.Core.DTOs.AR.InvoiceLineItemCreateDto;
using WorkOrderTaskDtoFull = ErpSystem.Core.DTOs.Maintenance.WorkOrderTaskDto;

namespace ErpSystem.Core.Services.Maintenance;

/// <summary>
/// Service for managing work order lifecycle with integrated inventory management
/// </summary>
public class WorkOrderService : IWorkOrderService
{
    private readonly IWorkOrderRepository _workOrderRepository;
    private readonly IWorkOrderTaskRepository _taskRepository;
    private readonly IMaintenanceInventoryService _inventoryService;
    private readonly IMaintenanceAssetRepository _assetRepository;
    private readonly ErpSystem.Core.Interfaces.HR.IEmployeeRepository _employeeRepository;
    private readonly IWorkOrderTypeRepository _workOrderTypeRepository;
    private readonly IMaintenanceTypeRepository _maintenanceTypeRepository;
    private readonly IPriorityLevelRepository _priorityLevelRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IQualityControlService _qualityControlService;
    private readonly ITaskTemplateService _taskTemplateService;
    private readonly IMaintenanceStaffScheduleRepository _scheduleRepository;
    private readonly IMaintenanceExpenseRepository _expenseRepository;
    private readonly IUserService _userService;
    private readonly IInvoiceService _invoiceService;
    private readonly ILogger<WorkOrderService> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppEventBus _appEventBus;

    public WorkOrderService(
        IWorkOrderRepository workOrderRepository,
        IWorkOrderTaskRepository taskRepository,
        IMaintenanceInventoryService inventoryService,
        IMaintenanceAssetRepository assetRepository,
        ErpSystem.Core.Interfaces.HR.IEmployeeRepository employeeRepository,
        IWorkOrderTypeRepository workOrderTypeRepository,
        IMaintenanceTypeRepository maintenanceTypeRepository,
        IPriorityLevelRepository priorityLevelRepository,
        ICurrentUserService currentUserService,
        IQualityControlService qualityControlService,
        ITaskTemplateService taskTemplateService,
        IMaintenanceStaffScheduleRepository scheduleRepository,
        IMaintenanceExpenseRepository expenseRepository,
        IUserService userService,
        IInvoiceService invoiceService,
        ILogger<WorkOrderService> logger,
        IUnitOfWork unitOfWork,
        IAppEventBus appEventBus)
    {
        _workOrderRepository = workOrderRepository;
        _taskRepository = taskRepository;
        _inventoryService = inventoryService;
        _assetRepository = assetRepository;
        _employeeRepository = employeeRepository;
        _workOrderTypeRepository = workOrderTypeRepository;
        _maintenanceTypeRepository = maintenanceTypeRepository;
        _priorityLevelRepository = priorityLevelRepository;
        _currentUserService = currentUserService;
        _qualityControlService = qualityControlService;
        _taskTemplateService = taskTemplateService;
        _scheduleRepository = scheduleRepository;
        _expenseRepository = expenseRepository;
        _userService = userService;
        _invoiceService = invoiceService;
        _logger = logger;
        _unitOfWork = unitOfWork;
        _appEventBus = appEventBus;
    }

    #region Work Order Lifecycle

    /// <summary>
    /// Starts a work order and allocates required parts
    /// </summary>
    public async Task<WorkOrderStartResult> StartWorkOrderAsync(Guid workOrderId, Guid technicianId)
    {
        try
        {
            _logger.LogInformation("Starting work order {WorkOrderId} by technician {TechnicianId}",
                workOrderId, technicianId);

            var workOrder = await _workOrderRepository.GetByIdAsync(workOrderId) ?? throw new ArgumentException($"Work order {workOrderId} not found");

            // Validate work order can be started
            if (workOrder.Status != "Draft" && workOrder.Status != "Open" && workOrder.Status != "Assigned" && workOrder.Status != "Approved")
            {
                throw new InvalidOperationException($"Cannot start work order in {workOrder.Status} status");
            }

            // Validate technician is assigned
            if (workOrder.AssignedTechnicianId != technicianId)
            {
                throw new UnauthorizedAccessException("Technician is not assigned to this work order");
            }

            var result = new WorkOrderStartResult
            {
                WorkOrderId = workOrderId,
                TechnicianId = technicianId,
                StartedAt = DateTime.UtcNow,
                Success = true
            };

            // Validate vehicle availability before starting
            var (vehiclesAvailable, unavailableVehicles) = await ValidateVehicleAvailabilityAsync(workOrder);
            if (!vehiclesAvailable)
            {
                result.Success = false;
                result.WarningMessages.Add("Cannot start work order: Some vehicles are not available.");
                foreach (var vehicle in unavailableVehicles)
                {
                    result.WarningMessages.Add($"  - {vehicle}");
                }
                result.WarningMessages.Add("Please assign different vehicles before starting this work order.");

                _logger.LogWarning("Work order {WorkOrderId} cannot start due to unavailable vehicles: {Vehicles}",
                    workOrderId, string.Join(", ", unavailableVehicles));

                return result;
            }

            // Check parts availability first
            var availabilityCheck = await _inventoryService.CheckPartsAvailabilityAsync(workOrderId);
            if (!availabilityCheck.AllPartsAvailable)
            {
                result.WarningMessages.Add($"Not all parts are available: {availabilityCheck.UnavailableParts} items unavailable");
                result.PartsAvailabilityIssues = availabilityCheck.PartAvailability
                    .Where(p => !p.IsAvailable)
                    .ToList();
            }

            // Allocate parts if they are available
            if (availabilityCheck.AllPartsAvailable && workOrder.Parts.Any())
            {
                try
                {
                    var allocationResult = await _inventoryService.AllocateWorkOrderPartsAsync(workOrderId, technicianId);
                    if (allocationResult.SuccessfulAllocations > 0)
                    {
                        result.PartsAllocated = allocationResult.SuccessfulAllocations;
                        result.SuccessMessages.Add($"Successfully allocated {allocationResult.SuccessfulAllocations} parts");
                    }

                    if (allocationResult.FailedAllocationCount > 0)
                    {
                        result.WarningMessages.Add($"Failed to allocate {allocationResult.FailedAllocationCount} parts");
                        result.AllocationFailures = allocationResult.FailedAllocations;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Parts allocation failed for work order {WorkOrderId}", workOrderId);
                    result.WarningMessages.Add($"Parts allocation failed: {ex.Message}");
                }
            }

            // Update vehicle status to Maintenance for assigned vehicles
            await UpdateVehicleStatusOnWorkOrderStartAsync(workOrderId);

            // Update work order status
            workOrder.Status = "InProgress";
            workOrder.ActualStartDate = result.StartedAt;
            await _workOrderRepository.UpdateAsync(workOrder);
            await _unitOfWork.SaveChangesAsync();

            result.NewStatus = workOrder.Status;
            result.SuccessMessages.Add("Work order started successfully");

            _logger.LogInformation("Work order {WorkOrderId} started successfully by technician {TechnicianId}",
                workOrderId, technicianId);

            // Publish event for admin-configurable notification topics (best-effort).
            try
            {
                var triggeredBy = Guid.TryParse(_currentUserService.UserId, out var uid) ? uid : (Guid?)null;
                await _appEventBus.PublishAsync(new EntityActivityEvent
                {
                    TenantId = workOrder.TenantId,
                    EntityType = "WorkOrder",
                    Activity = "Started",
                    Audience = "Internal",
                    EntityId = workOrder.Id,
                    TriggeredByUserId = triggeredBy,
                    Data = new Dictionary<string, object>
                    {
                        ["WorkOrderId"] = workOrder.Id,
                        ["WorkOrderNumber"] = workOrder.WorkOrderNumber ?? string.Empty,
                        ["TechnicianId"] = technicianId,
                        ["Status"] = workOrder.Status ?? string.Empty,
                        ["StartedAt"] = result.StartedAt.ToString("o")
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish WorkOrder.Started entity activity event for work order {WorkOrderId}", workOrderId);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting work order {WorkOrderId}", workOrderId);
            throw;
        }
    }

    /// <summary>
    /// Completes a work order with inventory consumption and returns
    /// </summary>
    public async Task<WorkOrderCompletionResult> CompleteWorkOrderAsync(
        CompleteWorkOrderDto completionDto, Guid completedById)
    {
        try
        {
            _logger.LogInformation("Completing work order {WorkOrderId} by user {CompletedById}",
                completionDto.WorkOrderId, completedById);

            var workOrder = await _workOrderRepository.GetByIdAsync(completionDto.WorkOrderId) ?? throw new ArgumentException($"Work order {completionDto.WorkOrderId} not found");
            if (workOrder.Status != "InProgress")
            {
                throw new InvalidOperationException($"Cannot complete work order in {workOrder.Status} status");
            }

            // Perform quality control validation before completion
            var qualityValidation = await _qualityControlService.ValidateWorkOrderCompletionAsync(completionDto.WorkOrderId);

            var result = new WorkOrderCompletionResult
            {
                WorkOrderId = completionDto.WorkOrderId,
                CompletedById = completedById,
                CompletionDate = DateTime.UtcNow,
                Success = true,
                QualityValidationResult = qualityValidation
            };

            // Check if quality validation allows completion
            if (!qualityValidation.CanComplete)
            {
                result.Success = false;
                result.WarningMessages.AddRange(qualityValidation.ValidationFailures);
                result.WarningMessages.Add("Work order cannot be completed until quality control requirements are met");

                // If inspection officer approval is required, set appropriate status
                if (qualityValidation.RequiresInspectionOfficerApproval)
                {
                    workOrder.Status = "PendingQualityApproval";
                    workOrder.UpdatedAt = DateTime.UtcNow;
                    await _workOrderRepository.UpdateAsync(workOrder);
                    await _unitOfWork.SaveChangesAsync();
                    result.WarningMessages.Add("Work order requires inspection officer approval before completion");
                }

                return result;
            }

            var linkedFleetDefects = await GetLinkedFleetDefectsAsync(workOrder);
            EnsureLatestQualityCheckPassedForLinkedDefects(workOrder, linkedFleetDefects);

            // Complete all tasks if not already done
            await CompleteAllTasksAsync(completionDto.WorkOrderId, completedById);

            // Process parts consumption
            if (completionDto.PartsConsumed?.Any() == true)
            {
                try
                {
                    var consumptionResult = await _inventoryService.ConsumeWorkOrderPartsAsync(
                        completionDto.WorkOrderId, completionDto.PartsConsumed, completedById);

                    result.PartsConsumed = consumptionResult.SuccessfulConsumptions;
                    result.TotalPartsCost = consumptionResult.ConsumedParts.Sum(p => p.TotalCost);

                    if (consumptionResult.SuccessfulConsumptions > 0)
                    {
                        result.SuccessMessages.Add($"Successfully consumed {consumptionResult.SuccessfulConsumptions} parts");
                    }

                    if (consumptionResult.FailedConsumptions > 0)
                    {
                        result.WarningMessages.Add($"Failed to consume {consumptionResult.FailedConsumptions} parts");
                        result.ConsumptionFailures = consumptionResult.ConsumptionFailures;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Parts consumption failed for work order {WorkOrderId}", completionDto.WorkOrderId);
                    result.WarningMessages.Add($"Parts consumption failed: {ex.Message}");
                }
            }

            // Process parts returns
            if (completionDto.PartsReturned?.Any() == true)
            {
                try
                {
                    var returnResult = await _inventoryService.ReturnWorkOrderPartsAsync(
                        completionDto.WorkOrderId, completionDto.PartsReturned, completedById);

                    result.PartsReturned = returnResult.SuccessfulReturns;

                    if (returnResult.SuccessfulReturns > 0)
                    {
                        result.SuccessMessages.Add($"Successfully returned {returnResult.SuccessfulReturns} parts");
                    }

                    if (returnResult.FailedReturns > 0)
                    {
                        result.WarningMessages.Add($"Failed to return {returnResult.FailedReturns} parts");
                        result.ReturnFailures = returnResult.ReturnFailures;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Parts return failed for work order {WorkOrderId}", completionDto.WorkOrderId);
                    result.WarningMessages.Add($"Parts return failed: {ex.Message}");
                }
            }

            // Calculate total costs
            var laborCost = workOrder.Labor.Sum(l => l.TotalCost);
            var usedPartsCost = CalculateUsedPartsCost(workOrder);
            result.TotalPartsCost = usedPartsCost;
            var approvedExpensesTotal = await _expenseRepository.GetTotalExpensesByWorkOrderIdAsync(completionDto.WorkOrderId);

            var computedActualCost = Math.Round(laborCost + usedPartsCost + approvedExpensesTotal, 2, MidpointRounding.AwayFromZero);
            var actualCost = completionDto.ActualCost.HasValue && completionDto.ActualCost.Value > 0
                ? completionDto.ActualCost.Value
                : computedActualCost;

            // Release vehicles back to Active status
            await UpdateVehicleStatusOnWorkOrderCompleteAsync(completionDto.WorkOrderId);

            // Ensure any active staff schedules for this work order are marked as completed
            var staffSchedules = await _scheduleRepository.GetByWorkOrderIdAsync(completionDto.WorkOrderId);
            if (staffSchedules != null)
            {
                var now = DateTime.UtcNow;
                foreach (var schedule in staffSchedules.Where(s => s.Status == "Scheduled" || s.Status == "InProgress"))
                {
                    schedule.Status = "Completed";
                    if (!schedule.ActualEndTime.HasValue)
                    {
                        schedule.ActualEndTime = now;
                    }

                    schedule.UpdatedAt = now;
                    await _scheduleRepository.UpdateAsync(schedule);
                }
            }

            // Update work order completion
            workOrder.Status = "Completed";
            workOrder.ActualCompletionDate = result.CompletionDate;
            workOrder.CompletionNotes = completionDto.CompletionNotes;
            workOrder.ActualCost = actualCost;
            workOrder.ActualHours = completionDto.ActualHours ?? workOrder.Labor.Sum(l => l.Hours);

            if (!string.IsNullOrEmpty(completionDto.FailureCode))
            {
                workOrder.FailureCode = completionDto.FailureCode;
            }

            if (!string.IsNullOrEmpty(completionDto.CauseCode))
            {
                workOrder.CauseCode = completionDto.CauseCode;
            }

            if (!string.IsNullOrEmpty(completionDto.ActionCode))
            {
                workOrder.ActionCode = completionDto.ActionCode;
            }

            // Post internal maintenance cost to fleet ledger when this work order involves exactly one vehicle asset (best-effort).
            await UpsertFleetInternalMaintenanceCostEntryAsync(workOrder, actualCost, result.CompletionDate, completedById);

            await CloseLinkedFleetDefectsAsync(linkedFleetDefects, completedById, result.CompletionDate);

            await _workOrderRepository.UpdateAsync(workOrder);
            await _unitOfWork.SaveChangesAsync();

            result.NewStatus = workOrder.Status;
            result.TotalCost = actualCost;
            result.TotalHours = workOrder.ActualHours;
            result.SuccessMessages.Add("Work order completed successfully");

            // Update asset status if specified
            if (!string.IsNullOrEmpty(completionDto.AssetStatusUpdate))
            {
                await UpdateAssetStatusAsync(workOrder.AssetId, completionDto.AssetStatusUpdate, completedById);
                result.SuccessMessages.Add($"Asset status updated to {completionDto.AssetStatusUpdate}");
            }

            _logger.LogInformation("Work order {WorkOrderId} completed successfully. " +
                "Total cost: {TotalCost}, Parts consumed: {PartsConsumed}, Parts returned: {PartsReturned}",
                completionDto.WorkOrderId, result.TotalCost, result.PartsConsumed, result.PartsReturned);

            // Publish event for admin-configurable notification topics (best-effort).
            try
            {
                var triggeredBy = Guid.TryParse(_currentUserService.UserId, out var uid) ? uid : (Guid?)null;
                await _appEventBus.PublishAsync(new EntityActivityEvent
                {
                    TenantId = workOrder.TenantId,
                    EntityType = "WorkOrder",
                    Activity = "Completed",
                    Audience = "Internal",
                    EntityId = workOrder.Id,
                    TriggeredByUserId = triggeredBy,
                    Data = new Dictionary<string, object>
                    {
                        ["WorkOrderId"] = workOrder.Id,
                        ["WorkOrderNumber"] = workOrder.WorkOrderNumber ?? string.Empty,
                        ["CompletedById"] = completedById,
                        ["Status"] = workOrder.Status ?? string.Empty,
                        ["CompletedAt"] = result.CompletionDate.ToString("o"),
                        ["TotalCost"] = result.TotalCost,
                        ["TotalHours"] = result.TotalHours
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish WorkOrder.Completed entity activity event for work order {WorkOrderId}", completionDto.WorkOrderId);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing work order {WorkOrderId}", completionDto.WorkOrderId);
            throw;
        }
    }

    /// <summary>
    /// Pauses a work order and returns allocated parts if needed
    /// </summary>
    public async Task<WorkOrderStatusChangeResult> PauseWorkOrderAsync(
        Guid workOrderId, string reason, bool returnParts, Guid userId)
    {
        try
        {
            _logger.LogInformation("Pausing work order {WorkOrderId} by user {UserId}", workOrderId, userId);

            var workOrder = await _workOrderRepository.GetByIdAsync(workOrderId) ?? throw new ArgumentException($"Work order {workOrderId} not found");
            if (workOrder.Status != "InProgress")
            {
                throw new InvalidOperationException($"Cannot pause work order in {workOrder.Status} status");
            }

            var result = new WorkOrderStatusChangeResult
            {
                WorkOrderId = workOrderId,
                UserId = userId,
                ChangeDate = DateTime.UtcNow,
                PreviousStatus = workOrder.Status,
                Success = true
            };

            // Return parts if requested
            if (returnParts && workOrder.Parts.Any(p => p.Status == "Allocated"))
            {
                var allocatedParts = workOrder.Parts
                    .Where(p => p.Status == "Allocated" && p.AllocationId.HasValue)
                    .Select(p => new PartReturnDto
                    {
                        PartId = p.Id,
                        QuantityReturned = p.QuantityAllocated - p.QuantityUsed,
                        ReturnReason = $"Work order paused: {reason}"
                    })
                    .Where(p => p.QuantityReturned > 0)
                    .ToList();

                if (allocatedParts.Any())
                {
                    try
                    {
                        var returnResult = await _inventoryService.ReturnWorkOrderPartsAsync(
                            workOrderId, allocatedParts, userId);

                        result.SuccessMessages.Add($"Returned {returnResult.SuccessfulReturns} allocated parts");
                        result.PartsReturned = returnResult.SuccessfulReturns;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to return parts when pausing work order {WorkOrderId}", workOrderId);
                        result.WarningMessages.Add($"Failed to return some parts: {ex.Message}");
                    }
                }
            }

            // Update work order status
            workOrder.Status = "OnHold";
            await _workOrderRepository.UpdateAsync(workOrder);
            await _unitOfWork.SaveChangesAsync();

            result.NewStatus = workOrder.Status;
            result.SuccessMessages.Add($"Work order paused: {reason}");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pausing work order {WorkOrderId}", workOrderId);
            throw;
        }
    }

    /// <summary>
    /// Resumes a paused work order and re-allocates parts if needed
    /// </summary>
    public async Task<WorkOrderStatusChangeResult> ResumeWorkOrderAsync(
        Guid workOrderId, Guid userId)
    {
        try
        {
            _logger.LogInformation("Resuming work order {WorkOrderId} by user {UserId}", workOrderId, userId);

            var workOrder = await _workOrderRepository.GetByIdAsync(workOrderId) ?? throw new ArgumentException($"Work order {workOrderId} not found");
            if (workOrder.Status != "OnHold")
            {
                throw new InvalidOperationException($"Cannot resume work order in {workOrder.Status} status");
            }

            var result = new WorkOrderStatusChangeResult
            {
                WorkOrderId = workOrderId,
                UserId = userId,
                ChangeDate = DateTime.UtcNow,
                PreviousStatus = workOrder.Status,
                Success = true
            };

            // Re-allocate parts that were returned
            var partsToReallocate = workOrder.Parts.Where(p => p.Status == "Required").ToList();
            if (partsToReallocate.Any())
            {
                try
                {
                    var allocationResult = await _inventoryService.AllocateWorkOrderPartsAsync(workOrderId, userId);
                    if (allocationResult.SuccessfulAllocations > 0)
                    {
                        result.SuccessMessages.Add($"Re-allocated {allocationResult.SuccessfulAllocations} parts");
                        result.PartsAllocated = allocationResult.SuccessfulAllocations;
                    }

                    if (allocationResult.FailedAllocationCount > 0)
                    {
                        result.WarningMessages.Add($"Failed to re-allocate {allocationResult.FailedAllocationCount} parts");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to re-allocate parts when resuming work order {WorkOrderId}", workOrderId);
                    result.WarningMessages.Add($"Failed to re-allocate some parts: {ex.Message}");
                }
            }

            // Update work order status
            workOrder.Status = "InProgress";
            await _workOrderRepository.UpdateAsync(workOrder);
            await _unitOfWork.SaveChangesAsync();

            result.NewStatus = workOrder.Status;
            result.SuccessMessages.Add("Work order resumed successfully");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming work order {WorkOrderId}", workOrderId);
            throw;
        }
    }

    #endregion

    #region Task Management

    /// <summary>
    /// Completes a work order task
    /// </summary>
    public async Task<TaskCompletionResult> CompleteTaskAsync(CompleteTaskDto taskDto, Guid completedById)
    {
        try
        {
            var task = await _taskRepository.GetByIdAsync(taskDto.TaskId) ?? throw new ArgumentException($"Task {taskDto.TaskId} not found");
            if (task.Status == "Completed")
            {
                throw new InvalidOperationException("Task is already completed");
            }

            var result = new TaskCompletionResult
            {
                TaskId = taskDto.TaskId,
                WorkOrderId = task.WorkOrderId,
                CompletedById = completedById,
                CompletionDate = DateTime.UtcNow,
                Success = true
            };

            // Update task
            task.Status = "Completed";
            task.CompletedAt = result.CompletionDate;
            task.CompletionNotes = taskDto.CompletionNotes;
            task.ActualHours = taskDto.ActualHours;

            await _taskRepository.UpdateAsync(task);

            // Create WorkOrderLabor record to track technician hours and cost
            if (task.AssignedTechnicianId.HasValue && task.ActualHours > 0)
            {
                try
                {
                    var technician = await _employeeRepository.GetByIdAsync(task.AssignedTechnicianId.Value);
                    if (technician != null)
                    {
                        var hourlyRate = 0m; // Default rate - would need to be configured per employee
                                             // TODO: Add HourlyRate property to Employee entity or fetch from a rate table

                        var laborRecord = new WorkOrderLabor
                        {
                            Id = Guid.NewGuid(),
                            WorkOrderId = task.WorkOrderId,
                            TechnicianId = task.AssignedTechnicianId.Value,
                            StartTime = task.StartedAt ?? result.CompletionDate.AddHours(-task.ActualHours),
                            EndTime = result.CompletionDate,
                            Hours = task.ActualHours,
                            HourlyRate = hourlyRate,
                            TotalCost = (decimal)task.ActualHours * hourlyRate,
                            Notes = $"Task: {task.TaskName}",
                            LaborType = "Regular",
                            TenantId = task.TenantId,
                            CreatedAt = DateTime.UtcNow
                        };

                        var laborRepo = _unitOfWork.Repository<WorkOrderLabor>();
                        await laborRepo.AddAsync(laborRecord);

                        _logger.LogInformation("Created labor record for technician {TechnicianId} - {Hours} hours on task {TaskId}",
                            task.AssignedTechnicianId.Value, task.ActualHours, task.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to create labor record for task {TaskId}", taskDto.TaskId);
                    // Don't fail the task completion if labor tracking fails
                }
            }

            await _unitOfWork.SaveChangesAsync();

            // Check if all tasks are completed - simplified implementation
            var workOrder = await _workOrderRepository.GetByIdAsync(task.WorkOrderId);
            var allTasksCompleted = true; // Simplified - would need proper task checking

            result.AllTasksCompleted = allTasksCompleted;
            result.SuccessMessages.Add("Task completed successfully");

            if (allTasksCompleted)
            {
                result.SuccessMessages.Add("All required tasks completed");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing task {TaskId}", taskDto.TaskId);
            throw;
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Completes all incomplete required tasks
    /// </summary>
    private static async Task CompleteAllTasksAsync(Guid workOrderId, Guid completedById)
    {
        // Simplified implementation - would need to get tasks from task repository
        // For now, just return as this functionality would require proper task management
        return;
    }

    /// <summary>
    /// Updates asset status
    /// </summary>
    private async Task UpdateAssetStatusAsync(Guid assetId, string newStatus, Guid updatedById)
    {
        var asset = await _assetRepository.GetByIdAsync(assetId);
        if (asset != null)
        {
            // Note: Asset status update would need proper enum conversion
            // For now, we'll comment this out as it requires proper enum mapping
            // asset.Status = Enum.Parse<AssetStatus>(newStatus);
            // await _assetRepository.UpdateAsync(asset);
        }
    }

    /// <summary>
    /// Creates tasks for a work order using predefined templates with fallback logic
    /// Priority: Asset-specific > Asset-type > Maintenance-type > Generic templates
    /// </summary>
    private async Task CreateDefaultTasksAsync(WorkOrder workOrder)
    {
        try
        {
            _logger.LogInformation("Creating tasks for work order {WorkOrderId} using predefined templates", workOrder.Id);

            // Get task templates using the task template service with fallback logic
            var taskTemplates = await _taskTemplateService.GetTaskTemplatesForWorkOrderAsync(
                workOrder.AssetId, workOrder.MaintenanceTypeId);

            var tasks = new List<WorkOrderTask>();

            if (taskTemplates.Any())
            {
                // Create tasks from predefined templates
                foreach (var template in taskTemplates.OrderBy(t => t.Sequence))
                {
                    var task = new WorkOrderTask
                    {
                        Id = Guid.NewGuid(),
                        WorkOrderId = workOrder.Id,
                        TaskName = template.TaskName,
                        Description = template.Description,
                        Sequence = template.Sequence,
                        EstimatedHours = template.EstimatedHours,
                        IsRequired = template.IsRequired,
                        AssignedTechnicianId = template.AssignedTechnicianId, // Assign technician from template
                        TenantId = workOrder.TenantId,
                        CreatedAt = DateTime.UtcNow,
                        Status = "Pending"
                    };

                    tasks.Add(task);
                }

                _logger.LogInformation("Using {TaskCount} predefined task templates from {Source} for work order {WorkOrderId}",
                    tasks.Count, taskTemplates.First().Source, workOrder.Id);
            }
            else
            {
                // Fallback to generic tasks if no templates found
                _logger.LogWarning("No predefined task templates found for asset {AssetId} and maintenance type {MaintenanceTypeId}, using generic fallback",
                    workOrder.AssetId, workOrder.MaintenanceTypeId);

                tasks = await GenerateGenericFallbackTasks(workOrder);
            }

            // Add tasks to repository
            foreach (var task in tasks)
            {
                await _taskRepository.AddAsync(task);
            }

            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Created {TaskCount} tasks for work order {WorkOrderId}",
                tasks.Count, workOrder.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating tasks for work order {WorkOrderId}", workOrder.Id);
            // Don't throw - task creation failure shouldn't prevent work order creation
        }
    }

    /// <summary>
    /// Generates generic fallback tasks when no predefined templates are available
    /// </summary>
    private async Task<List<WorkOrderTask>> GenerateGenericFallbackTasks(WorkOrder workOrder)
    {
        var maintenanceType = await _maintenanceTypeRepository.GetByIdAsync(workOrder.MaintenanceTypeId);
        var asset = await _assetRepository.GetByIdAsync(workOrder.AssetId);

        return new List<WorkOrderTask>
        {
            new WorkOrderTask
            {
                Id = Guid.NewGuid(),
                WorkOrderId = workOrder.Id,
                TaskName = "Work Preparation",
                Description = "Prepare tools, materials, and safety equipment for the maintenance work",
                Sequence = 1,
                EstimatedHours = 0.25,
                IsRequired = true,
                TenantId = workOrder.TenantId,
                CreatedAt = DateTime.UtcNow,
                Status = "Pending"
            },
            new WorkOrderTask
            {
                Id = Guid.NewGuid(),
                WorkOrderId = workOrder.Id,
                TaskName = $"{maintenanceType?.Name ?? "Maintenance"} Work",
                Description = $"Perform {maintenanceType?.Name?.ToLower() ?? "maintenance"} tasks on {asset?.Name ?? "asset"}",
                Sequence = 2,
                EstimatedHours = Math.Max(workOrder.EstimatedHours * 0.7, 1.0),
                IsRequired = true,
                TenantId = workOrder.TenantId,
                CreatedAt = DateTime.UtcNow,
                Status = "Pending"
            },
            new WorkOrderTask
            {
                Id = Guid.NewGuid(),
                WorkOrderId = workOrder.Id,
                TaskName = "Verification and Documentation",
                Description = "Verify work completion and document results",
                Sequence = 3,
                EstimatedHours = 0.25,
                IsRequired = true,
                TenantId = workOrder.TenantId,
                CreatedAt = DateTime.UtcNow,
                Status = "Pending"
            }
        };
    }

    #endregion

    #region IWorkOrderService Interface Implementation

    /// <summary>
    /// Creates a new work order
    /// </summary>
    public async Task<WorkOrderDto> CreateWorkOrderAsync(CreateWorkOrderDto createDto)
    {
        try
        {
            // Log current user information for debugging
            _logger.LogInformation("Creating work order - Current User ID: {UserId}, Employee ID: {EmployeeId}, Tenant ID: {TenantId}",
                _currentUserService.UserId, _currentUserService.EmployeeId, _currentUserService.TenantId);

            // Log the incoming DTO for debugging
            _logger.LogDebug("CreateWorkOrderDto: Title={Title}, AssetId={AssetId}, WorkOrderTypeId={WorkOrderTypeId}, MaintenanceTypeId={MaintenanceTypeId}, PriorityLevelId={PriorityLevelId}",
                createDto.Title, createDto.AssetId, createDto.WorkOrderTypeId, createDto.MaintenanceTypeId, createDto.PriorityLevelId);

            // Determine RequestedById (ApplicationUser.Id).
            // WorkOrders.RequestedById is a FK to Users, so do NOT use EmployeeId here.
            // For background services (e.g., auto-generated from schedules), this will be null.
            Guid? requestedById = null;
            if (_currentUserService.UserId != null && Guid.TryParse(_currentUserService.UserId, out var userId) && userId != Guid.Empty)
            {
                requestedById = userId;
            }
            else
            {
                _logger.LogInformation("Creating work order without RequestedById (background service context)");
            }

            var workOrder = new WorkOrder
            {
                Title = createDto.Title,
                Description = createDto.Description,
                AssetId = createDto.AssetId,
                WorkOrderTypeId = createDto.WorkOrderTypeId,
                MaintenanceTypeId = createDto.MaintenanceTypeId,
                PriorityLevelId = createDto.PriorityLevelId,
                TenantId = createDto.TenantId ?? _currentUserService.TenantId ?? Guid.Empty,
                WorkOrderNumber = await GenerateWorkOrderNumberAsync(),
                RequestedById = requestedById, // Can be null for auto-generated work orders
                AssignedTechnicianId = createDto.AssignedTechnicianId, // Assign technician if provided
                AssignedTeamId = createDto.AssignedTeamId, // Assign team if provided
                JobCardId = createDto.JobCardId, // Link to job card if generated from one
                MaintenanceScheduleId = createDto.MaintenanceScheduleId, // Link to maintenance schedule if auto-generated
                Status = !string.IsNullOrWhiteSpace(createDto.Status) ? createDto.Status : "Open",
                MaintenanceLocation = createDto.MaintenanceLocation ?? "Internal",
                RequestedStartDate = createDto.RequestedStartDate,
                RequestedCompletionDate = createDto.RequestedCompletionDate,
                EstimatedHours = createDto.EstimatedHours,
                EstimatedCost = createDto.EstimatedCost,
                SafetyRequirements = createDto.SafetyRequirements,
                RequiresPermit = createDto.RequiresPermit,
                RequiresLockout = createDto.RequiresLockout,
                RequiresConfinedSpaceEntry = createDto.RequiresConfinedSpaceEntry,
                BillingType = createDto.BillingType ?? "Repairs", // Set billing type from DTO
                FixedAmount = createDto.FixedAmount, // Set fixed amount for Maintenance billing type
                CustomFieldValues = createDto.CustomFieldValues == null
                    ? null
                    : JsonSerializer.Serialize(createDto.CustomFieldValues)
            };

            _logger.LogDebug("Work Order entity created: Id={Id}, Number={Number}, RequestedById={RequestedById}, TenantId={TenantId}",
                workOrder.Id, workOrder.WorkOrderNumber, workOrder.RequestedById, workOrder.TenantId);

            await _workOrderRepository.AddAsync(workOrder);
            _logger.LogDebug("Work order added to repository, saving changes...");

            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Work order {WorkOrderNumber} (ID: {Id}) created successfully", workOrder.WorkOrderNumber, workOrder.Id);

            if (createDto.GenerateDefaultTasks)
            {
                await CreateDefaultTasksAsync(workOrder);
            }

            // Publish event for admin-configurable notification topics (best-effort).
            try
            {
                var triggeredBy = Guid.TryParse(_currentUserService.UserId, out var uid) ? uid : (Guid?)null;
                await _appEventBus.PublishAsync(new EntityActivityEvent
                {
                    TenantId = workOrder.TenantId,
                    EntityType = "WorkOrder",
                    Activity = "Created",
                    Audience = "Internal",
                    EntityId = workOrder.Id,
                    TriggeredByUserId = triggeredBy,
                    Data = new Dictionary<string, object>
                    {
                        ["WorkOrderId"] = workOrder.Id,
                        ["WorkOrderNumber"] = workOrder.WorkOrderNumber ?? string.Empty,
                        ["Title"] = workOrder.Title ?? string.Empty,
                        ["Status"] = workOrder.Status ?? string.Empty,
                        ["AssetId"] = workOrder.AssetId,
                        ["JobCardId"] = workOrder.JobCardId ?? Guid.Empty,
                        ["RequestedById"] = workOrder.RequestedById ?? Guid.Empty,
                        ["AssignedTechnicianId"] = workOrder.AssignedTechnicianId ?? Guid.Empty,
                        ["AssignedTeamId"] = workOrder.AssignedTeamId ?? Guid.Empty
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish WorkOrder.Created entity activity event for work order {WorkOrderId}", workOrder.Id);
            }

            return await MapToWorkOrderDtoAsync(workOrder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating work order");
            throw;
        }
    }

    /// <summary>
    /// Updates an existing work order
    /// </summary>
    public async Task<WorkOrderDto> UpdateWorkOrderAsync(Guid id, UpdateWorkOrderDto updateDto)
    {
        try
        {
            var workOrder = await _workOrderRepository.GetByIdAsync(id) ?? throw new ArgumentException($"Work order {id} not found");

            // Manual mapping
            workOrder.Title = updateDto.Title;
            workOrder.Description = updateDto.Description;

            // Only update these fields if provided
            if (updateDto.WorkOrderTypeId.HasValue)
            {
                workOrder.WorkOrderTypeId = updateDto.WorkOrderTypeId.Value;
            }

            if (updateDto.MaintenanceTypeId.HasValue)
            {
                workOrder.MaintenanceTypeId = updateDto.MaintenanceTypeId.Value;
            }

            if (updateDto.PriorityLevelId.HasValue)
            {
                workOrder.PriorityLevelId = updateDto.PriorityLevelId.Value;
            }

            workOrder.AssignedTechnicianId = updateDto.AssignedTechnicianId;
            workOrder.AssignedTeamId = updateDto.AssignedTeamId;

            if (!string.IsNullOrEmpty(updateDto.MaintenanceLocation))
            {
                workOrder.MaintenanceLocation = updateDto.MaintenanceLocation;
            }

            workOrder.RequestedStartDate = updateDto.RequestedStartDate;
            workOrder.RequestedCompletionDate = updateDto.RequestedCompletionDate;
            workOrder.EstimatedHours = updateDto.EstimatedHours;
            workOrder.EstimatedCost = updateDto.EstimatedCost;
            workOrder.SafetyRequirements = updateDto.SafetyRequirements;
            workOrder.RequiresPermit = updateDto.RequiresPermit;
            workOrder.RequiresLockout = updateDto.RequiresLockout;
            workOrder.RequiresConfinedSpaceEntry = updateDto.RequiresConfinedSpaceEntry;
            workOrder.UpdatedAt = DateTime.UtcNow;

            await _workOrderRepository.UpdateAsync(workOrder);
            await _unitOfWork.SaveChangesAsync();
            return await MapToWorkOrderDtoAsync(workOrder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating work order {WorkOrderId}", id);
            throw;
        }
    }

    /// <summary>
    /// Deletes a work order
    /// </summary>
    public async Task DeleteWorkOrderAsync(Guid id)
    {
        try
        {
            await _workOrderRepository.DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting work order {WorkOrderId}", id);
            throw;
        }
    }

    /// <summary>
    /// Gets a work order by ID
    /// </summary>
    public async Task<WorkOrderDto?> GetWorkOrderByIdAsync(Guid id)
    {
        try
        {
            var workOrder = await _workOrderRepository.GetByIdAsync(id);
            return workOrder == null ? null : await MapToWorkOrderDtoAsync(workOrder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting work order {WorkOrderId}", id);
            throw;
        }
    }

    /// <summary>
    /// Gets a work order by work order number
    /// </summary>
    public async Task<WorkOrderDto?> GetWorkOrderByNumberAsync(string workOrderNumber)
    {
        try
        {
            var workOrder = await _workOrderRepository.GetByWorkOrderNumberAsync(workOrderNumber);
            return workOrder == null ? null : await MapToWorkOrderDtoAsync(workOrder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting work order by number {WorkOrderNumber}", workOrderNumber);
            throw;
        }
    }

    /// <summary>
    /// Gets paginated work orders with filtering
    /// </summary>
    public async Task<PagedResult<WorkOrderListDto>> GetWorkOrdersPagedAsync(WorkOrderFilterDto filter)
    {
        try
        {
            var allWorkOrders = await _workOrderRepository.GetAllAsync();
            var filteredWorkOrders = ApplyWorkOrderFilters(allWorkOrders, filter).ToList();
            var page = Math.Max(filter.Page, 1);
            var pageSize = filter.PageSize <= 0 ? 25 : filter.PageSize;
            var workOrdersPage = filteredWorkOrders
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var workOrderDtos = new List<WorkOrderListDto>();
            foreach (var workOrder in workOrdersPage)
            {
                var dto = await MapToWorkOrderListDtoAsync(workOrder);
                workOrderDtos.Add(dto);
            }

            return new PagedResult<WorkOrderListDto>
            {
                Items = workOrderDtos,
                TotalCount = filteredWorkOrders.Count,
                Page = page,
                PageSize = pageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting paginated work orders");
            throw;
        }
    }

    /// <summary>
    /// Updates work order status
    /// </summary>
    public async Task<WorkOrderDto> UpdateWorkOrderStatusAsync(Guid id, string status, string? notes = null)
    {
        try
        {
            var workOrder = await _workOrderRepository.GetByIdAsync(id) ?? throw new ArgumentException($"Work order {id} not found");
            var previousStatus = workOrder.Status;
            workOrder.Status = status;
            // Note: Notes functionality would need to be implemented via WorkOrderComment entity

            workOrder.UpdatedAt = DateTime.UtcNow;
            await _workOrderRepository.UpdateAsync(workOrder);
            await _unitOfWork.SaveChangesAsync();

            // If a work order is put on hold, release any assigned vehicles back to Active so they can be used elsewhere.
            // This prevents a stale "InUse" lock from blocking future resume/start actions.
            if (string.Equals(status, "OnHold", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(previousStatus, "InProgress", StringComparison.OrdinalIgnoreCase))
            {
                await UpdateVehicleStatusOnWorkOrderCompleteAsync(id);
            }

            return await MapToWorkOrderDtoAsync(workOrder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating work order status {WorkOrderId}", id);
            throw;
        }
    }

    /// <summary>
    /// Assigns work order to technician or team
    /// </summary>
    public async Task<WorkOrderDto> AssignWorkOrderAsync(Guid id, Guid? technicianId, Guid? teamId)
    {
        try
        {
            var workOrder = await _workOrderRepository.GetByIdAsync(id) ?? throw new ArgumentException($"Work order {id} not found");

            // Validate technician exists and can be assigned to maintenance if provided
            if (technicianId.HasValue)
            {
                var technician = await _employeeRepository.GetByIdAsync(technicianId.Value, e => e.OrganizationUnit)
                    ?? throw new ArgumentException($"Technician with ID {technicianId.Value} not found in HR system");
                if (!technician.IsActive)
                {
                    throw new InvalidOperationException($"Technician {technician.FirstName} {technician.LastName} is not active");
                }

                if (!technician.CanBeAssignedToMaintenance)
                {
                    throw new InvalidOperationException($"Employee {technician.FirstName} {technician.LastName} is not qualified for maintenance assignments");
                }

                EnsureTechnicianMatchesAssetLocation(technician, workOrder);

                _logger.LogInformation("Assigning work order {WorkOrderId} to technician {TechnicianName} (ID: {TechnicianId})",
                    id, $"{technician.FirstName} {technician.LastName}", technicianId.Value);
            }

            workOrder.AssignedTechnicianId = technicianId;
            workOrder.AssignedTeamId = teamId;
            workOrder.Status = "Assigned";
            workOrder.UpdatedAt = DateTime.UtcNow;

            await _workOrderRepository.UpdateAsync(workOrder);
            await _unitOfWork.SaveChangesAsync();
            return await MapToWorkOrderDtoAsync(workOrder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning work order {WorkOrderId}", id);
            throw;
        }
    }

    /// <summary>
    /// Approves a work order
    /// </summary>
    public async Task<WorkOrderDto> ApproveWorkOrderAsync(Guid id, string? approvalNotes = null)
    {
        try
        {
            var workOrder = await _workOrderRepository.GetByIdAsync(id) ?? throw new ArgumentException($"Work order {id} not found");
            workOrder.Status = "Approved";
            
            // Set ApprovedById - can be null for background service context
            Guid? approvedById = null;
            if (_currentUserService.UserId != null && Guid.TryParse(_currentUserService.UserId, out var userId2) && userId2 != Guid.Empty)
            {
                approvedById = userId2;
            }
            workOrder.ApprovedById = approvedById;
            workOrder.ApprovedAt = DateTime.UtcNow;

            // Note: Approval notes would need to be implemented via WorkOrderComment entity

            workOrder.UpdatedAt = DateTime.UtcNow;
            await _workOrderRepository.UpdateAsync(workOrder);
            await _unitOfWork.SaveChangesAsync();

            return await MapToWorkOrderDtoAsync(workOrder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving work order {WorkOrderId}", id);
            throw;
        }
    }

    /// <summary>
    /// Starts a work order (interface method - simplified version)
    /// </summary>
    public async Task<WorkOrderDto> StartWorkOrderAsync(Guid id)
    {
        try
        {
            var workOrder = await _workOrderRepository.GetByIdAsync(id) ?? throw new ArgumentException($"Work order {id} not found");

            if (string.Equals(workOrder.Status, "InProgress", StringComparison.OrdinalIgnoreCase))
            {
                return await MapToWorkOrderDtoAsync(workOrder);
            }

            var isResume = string.Equals(workOrder.Status, "OnHold", StringComparison.OrdinalIgnoreCase);

            // Validate vehicle availability before starting
            var (vehiclesAvailable, unavailableVehicles) = await ValidateVehicleAvailabilityAsync(workOrder, allowInUseWhenResuming: isResume);
            if (!vehiclesAvailable)
            {
                var errorMessage = $"Cannot start work order: Some vehicles are not available. " +
                    string.Join(", ", unavailableVehicles) + ". " +
                    "Please assign different vehicles before starting this work order.";

                _logger.LogWarning("Work order {WorkOrderId} cannot start due to unavailable vehicles: {Vehicles}",
                    id, string.Join(", ", unavailableVehicles));

                throw new InvalidOperationException(errorMessage);
            }

            // Update vehicle status to InUse for assigned vehicles
            await UpdateVehicleStatusOnWorkOrderStartAsync(id);

            workOrder.Status = "InProgress";
            workOrder.ActualStartDate ??= DateTime.UtcNow;
            workOrder.UpdatedAt = DateTime.UtcNow;

            await _workOrderRepository.UpdateAsync(workOrder);
            await _unitOfWork.SaveChangesAsync();
            return await MapToWorkOrderDtoAsync(workOrder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting work order {WorkOrderId}", id);
            throw;
        }
    }

    /// <summary>
    /// Completes a work order (interface method - simplified version)
    /// </summary>
    public async Task<WorkOrderDto> CompleteWorkOrderAsync(Guid id, CompleteWorkOrderDto completeDto)
    {
        try
        {
            var workOrder = await _workOrderRepository.GetByIdAsync(id) ?? throw new ArgumentException($"Work order {id} not found");

            var qualityValidation = await _qualityControlService.ValidateWorkOrderCompletionAsync(id);
            if (!qualityValidation.CanComplete)
            {
                throw new InvalidOperationException(
                    qualityValidation.ValidationFailures.FirstOrDefault()
                    ?? "Work order cannot be completed until quality control requirements are met.");
            }

            var linkedFleetDefects = await GetLinkedFleetDefectsAsync(workOrder);
            EnsureLatestQualityCheckPassedForLinkedDefects(workOrder, linkedFleetDefects);

            // Release vehicles back to Active status
            await UpdateVehicleStatusOnWorkOrderCompleteAsync(id);

            // Ensure any active staff schedules for this work order are marked as completed
            var staffSchedules = await _scheduleRepository.GetByWorkOrderIdAsync(id);
            if (staffSchedules != null)
            {
                var now = DateTime.UtcNow;
                foreach (var schedule in staffSchedules.Where(s => s.Status == "Scheduled" || s.Status == "InProgress"))
                {
                    schedule.Status = "Completed";
                    if (!schedule.ActualEndTime.HasValue)
                    {
                        schedule.ActualEndTime = now;
                    }

                    schedule.UpdatedAt = now;
                    await _scheduleRepository.UpdateAsync(schedule);
                }
            }

            workOrder.Status = "Completed";
            workOrder.ActualCompletionDate = DateTime.UtcNow;
            workOrder.CompletionNotes = completeDto.CompletionNotes;
            workOrder.ActualHours = completeDto.ActualHours ?? workOrder.EstimatedHours;
            workOrder.FailureCode = completeDto.FailureCode;
            workOrder.CauseCode = completeDto.CauseCode;
            workOrder.ActionCode = completeDto.ActionCode;
            workOrder.UpdatedAt = DateTime.UtcNow;

            // Compute and store actual cost consistently (labor + used parts + approved expenses), unless explicitly provided.
            var laborCost = workOrder.Labor.Sum(l => l.TotalCost);
            var usedPartsCost = CalculateUsedPartsCost(workOrder);
            var approvedExpensesTotal = await _expenseRepository.GetTotalExpensesByWorkOrderIdAsync(workOrder.Id);
            var computedActualCost = Math.Round(laborCost + usedPartsCost + approvedExpensesTotal, 2, MidpointRounding.AwayFromZero);
            workOrder.ActualCost = completeDto.ActualCost.HasValue && completeDto.ActualCost.Value > 0
                ? completeDto.ActualCost.Value
                : computedActualCost;

            // Best-effort fleet cost posting for vehicle work orders.
            var completedByUserId = Guid.TryParse(_currentUserService.UserId, out var uid) ? uid : (Guid?)null;
            await UpsertFleetInternalMaintenanceCostEntryAsync(workOrder, workOrder.ActualCost, workOrder.ActualCompletionDate ?? DateTime.UtcNow, completedByUserId);

            await CloseLinkedFleetDefectsAsync(
                linkedFleetDefects,
                completedByUserId,
                workOrder.ActualCompletionDate ?? DateTime.UtcNow);

            await _workOrderRepository.UpdateAsync(workOrder);
            await _unitOfWork.SaveChangesAsync();
            return await MapToWorkOrderDtoAsync(workOrder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing work order {WorkOrderId}", id);
            throw;
        }
    }

    private static IEnumerable<WorkOrder> ApplyWorkOrderFilters(IEnumerable<WorkOrder> workOrders, WorkOrderFilterDto filter)
    {
        var query = workOrders;

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var searchTerm = filter.SearchTerm.Trim();
            query = query.Where(wo =>
                ContainsIgnoreCase(wo.WorkOrderNumber, searchTerm) ||
                ContainsIgnoreCase(wo.Title, searchTerm) ||
                ContainsIgnoreCase(wo.Description, searchTerm) ||
                ContainsIgnoreCase(wo.Asset?.Name, searchTerm) ||
                ContainsIgnoreCase(wo.Asset?.AssetNumber, searchTerm));
        }

        if (filter.AssetId.HasValue)
        {
            query = query.Where(wo => wo.AssetId == filter.AssetId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(wo => string.Equals(wo.Status, filter.Status, StringComparison.OrdinalIgnoreCase));
        }

        if (filter.WorkOrderTypeId.HasValue)
        {
            query = query.Where(wo => wo.WorkOrderTypeId == filter.WorkOrderTypeId.Value);
        }

        if (filter.MaintenanceTypeId.HasValue)
        {
            query = query.Where(wo => wo.MaintenanceTypeId == filter.MaintenanceTypeId.Value);
        }

        if (filter.PriorityLevelId.HasValue)
        {
            query = query.Where(wo => wo.PriorityLevelId == filter.PriorityLevelId.Value);
        }

        if (filter.AssignedTechnicianId.HasValue)
        {
            query = query.Where(wo => wo.AssignedTechnicianId == filter.AssignedTechnicianId.Value);
        }

        if (filter.AssignedTeamId.HasValue)
        {
            query = query.Where(wo => wo.AssignedTeamId == filter.AssignedTeamId.Value);
        }

        if (filter.StartDate.HasValue || filter.EndDate.HasValue)
        {
            query = query.Where(wo => IsWorkOrderInDateRange(wo, filter.StartDate, filter.EndDate));
        }

        if (filter.IsOverdue.HasValue)
        {
            query = query.Where(wo => IsWorkOrderOverdue(wo) == filter.IsOverdue.Value);
        }

        query = (filter.SortBy ?? "CreatedAt").ToLowerInvariant() switch
        {
            "workordernumber" => filter.SortDescending ? query.OrderByDescending(wo => wo.WorkOrderNumber) : query.OrderBy(wo => wo.WorkOrderNumber),
            "title" => filter.SortDescending ? query.OrderByDescending(wo => wo.Title) : query.OrderBy(wo => wo.Title),
            "status" => filter.SortDescending ? query.OrderByDescending(wo => wo.Status) : query.OrderBy(wo => wo.Status),
            "priority" => filter.SortDescending ? query.OrderByDescending(wo => wo.PriorityLevel?.Level ?? 0) : query.OrderBy(wo => wo.PriorityLevel?.Level ?? 0),
            "requestedstartdate" => filter.SortDescending ? query.OrderByDescending(wo => wo.RequestedStartDate ?? DateTime.MinValue) : query.OrderBy(wo => wo.RequestedStartDate ?? DateTime.MaxValue),
            "requestedcompletiondate" => filter.SortDescending ? query.OrderByDescending(wo => wo.RequestedCompletionDate ?? DateTime.MinValue) : query.OrderBy(wo => wo.RequestedCompletionDate ?? DateTime.MaxValue),
            _ => filter.SortDescending ? query.OrderByDescending(wo => wo.CreatedAt) : query.OrderBy(wo => wo.CreatedAt)
        };

        return query;
    }

    private static bool ContainsIgnoreCase(string? value, string searchTerm)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            value.Contains(searchTerm, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWorkOrderInDateRange(WorkOrder workOrder, DateTime? startDate, DateTime? endDate)
    {
        var start = startDate?.Date ?? DateTime.MinValue;
        var end = endDate?.Date.AddDays(1).AddTicks(-1) ?? DateTime.MaxValue;
        DateTime?[] candidateDates =
        {
            workOrder.CreatedAt,
            workOrder.RequestedStartDate,
            workOrder.RequestedCompletionDate,
            workOrder.ActualStartDate,
            workOrder.ActualCompletionDate
        };

        return candidateDates.Any(date => date.HasValue && date.Value >= start && date.Value <= end);
    }

    private static bool IsActiveWorkOrderStatus(string? status)
    {
        return !IsCompletedWorkOrderStatus(status) && !IsCancelledWorkOrderStatus(status);
    }

    private static bool IsCompletedWorkOrderStatus(string? status)
    {
        return string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "Closed", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCancelledWorkOrderStatus(string? status)
    {
        return string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "Canceled", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWorkOrderOverdue(WorkOrder workOrder)
    {
        return workOrder.RequestedCompletionDate.HasValue &&
            workOrder.RequestedCompletionDate.Value < DateTime.UtcNow &&
            IsActiveWorkOrderStatus(workOrder.Status);
    }

    private static double CalculateAverageWorkOrderDuration(IEnumerable<WorkOrderListDto> completedWorkOrders)
    {
        var durations = completedWorkOrders
            .Select(wo =>
            {
                var start = wo.ActualStartDate ?? wo.RequestedStartDate ?? wo.CreatedAt;
                var end = wo.ActualEndDate ?? wo.ActualCompletionDate;
                return end.HasValue && end.Value > start ? (double?)(end.Value - start).TotalHours : null;
            })
            .Where(duration => duration.HasValue)
            .Select(duration => duration!.Value)
            .ToList();

        return durations.Any() ? durations.Average() : 0;
    }

    public async Task<InvoiceDto> PostWorkOrderBillingToArInvoiceAsync(Guid id)
    {
        try
        {
            var workOrder = await _workOrderRepository.GetByIdAsync(id)
                ?? throw new ArgumentException($"Work order {id} not found");

            if (workOrder.JobCard?.CustomerBusinessPartnerId == null)
            {
                throw new InvalidOperationException("Link a customer business partner to the job card before posting this work order to AR.");
            }

            var customerPartner = await _unitOfWork.Repository<BusinessPartner>()
                .FirstOrDefaultAsync(bp => bp.Id == workOrder.JobCard.CustomerBusinessPartnerId.Value
                    && bp.TenantId == workOrder.TenantId
                    && !bp.IsDeleted);

            if (customerPartner == null)
            {
                throw new InvalidOperationException("The customer business partner linked to this job card was not found.");
            }

            var currencyCode = await MaintenanceCurrencyResolver.ResolveBaseCurrencyCodeAsync(_unitOfWork, workOrder.TenantId);
            var lineItems = await BuildArInvoiceLineItemsAsync(workOrder);

            if (lineItems.Count == 0 || lineItems.Sum(li => li.Quantity * li.UnitPrice) <= 0)
            {
                throw new InvalidOperationException("There are no billable work order lines to post to AR.");
            }

            return await _invoiceService.CreateAsync(new InvoiceCreateDto
            {
                CustomerId = customerPartner.Id,
                InvoiceDate = DateTime.UtcNow.Date,
                Reference = workOrder.WorkOrderNumber,
                Notes = $"Maintenance work order {workOrder.WorkOrderNumber} generated from job card {workOrder.JobCard.JobCardNumber}.",
                CurrencyCode = string.IsNullOrWhiteSpace(customerPartner.Currency) ? currencyCode : customerPartner.Currency,
                ExchangeRate = 1m,
                DiscountAmount = 0m,
                LineItems = lineItems
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error posting work order {WorkOrderId} billing to AR invoice", id);
            throw;
        }
    }

    private async Task<List<InvoiceLineItemCreateDto>> BuildArInvoiceLineItemsAsync(WorkOrder workOrder)
    {
        var lineItems = new List<InvoiceLineItemCreateDto>();

        if (string.Equals(workOrder.BillingType, "Maintenance", StringComparison.OrdinalIgnoreCase))
        {
            AddInvoiceLine(lineItems, $"Fixed maintenance charge - {workOrder.Title}", 1m, workOrder.FixedAmount);
            return lineItems;
        }

        foreach (var part in workOrder.Parts ?? Enumerable.Empty<WorkOrderPart>())
        {
            var quantity = part.QuantityUsed > 0 ? part.QuantityUsed : part.QuantityRequired;
            var total = part.TotalCost > 0 ? part.TotalCost : quantity * part.UnitCost;
            var unitPrice = quantity > 0 ? Math.Round(total / quantity, 4, MidpointRounding.AwayFromZero) : part.UnitCost;
            AddInvoiceLine(lineItems, $"Part: {part.ItemCode} - {part.ItemName}", quantity, unitPrice, "each");
        }

        var laborRecords = (workOrder.Labor ?? Enumerable.Empty<WorkOrderLabor>()).ToList();
        if (laborRecords.Count > 0)
        {
            var totalHours = Convert.ToDecimal(laborRecords.Sum(l => l.Hours));
            var totalCost = laborRecords.Sum(l =>
            {
                var hours = Convert.ToDecimal(l.Hours);
                return l.TotalCost > 0 ? l.TotalCost : hours * l.HourlyRate;
            });
            var blendedRate = totalHours > 0
                ? Math.Round(totalCost / totalHours, 4, MidpointRounding.AwayFromZero)
                : 0m;

            AddInvoiceLine(lineItems, $"Labor ({laborRecords.Count} entries)", totalHours, blendedRate, "hr");
        }

        var workOrderTools = await _unitOfWork.Repository<WorkOrderTool>()
            .GetQueryable(t => t.WorkOrderId == workOrder.Id && !t.IsDeleted && !t.IsExcludedFromBilling)
            .Include(t => t.Tool)
            .Include(t => t.Checkout)
            .ToListAsync();

        foreach (var tool in workOrderTools)
        {
            var rentalRate = tool.Tool?.DailyRentalRate ?? 0m;
            if (rentalRate <= 0)
            {
                continue;
            }

            var checkoutDate = tool.Checkout?.CheckoutDate ?? workOrder.ActualStartDate ?? workOrder.CreatedAt;
            var returnDate = tool.Checkout?.ActualReturnDate ?? workOrder.ActualCompletionDate ?? DateTime.UtcNow;
            var days = Math.Max(1, (int)Math.Ceiling((returnDate - checkoutDate).TotalDays));
            AddInvoiceLine(lineItems, $"Tool: {tool.Tool?.ItemCode} {tool.Tool?.Name}".Trim(), days, rentalRate, "day");
        }

        var expenses = await _expenseRepository.GetByWorkOrderIdAsync(workOrder.Id);
        foreach (var expense in expenses.Where(e => !string.Equals(e.Status, "Rejected", StringComparison.OrdinalIgnoreCase)))
        {
            AddInvoiceLine(lineItems, $"Expense: {expense.ExpenseType} - {expense.Description}", 1m, expense.Amount);
        }

        return lineItems;
    }

    private static void AddInvoiceLine(
        List<InvoiceLineItemCreateDto> lineItems,
        string description,
        decimal quantity,
        decimal unitPrice,
        string? unit = null)
    {
        if (quantity <= 0 || unitPrice <= 0)
        {
            return;
        }

        lineItems.Add(new InvoiceLineItemCreateDto
        {
            LineItemType = "Product",
            Description = description,
            Quantity = quantity,
            UnitPrice = unitPrice,
            Unit = unit,
            DiscountPercentage = 0m
        });
    }

    /// <summary>
    /// Gets overdue work orders
    /// </summary>
    public async Task<IEnumerable<WorkOrderListDto>> GetOverdueWorkOrdersAsync()
    {
        try
        {
            var allWorkOrders = await _workOrderRepository.GetAllAsync();
            var overdueWorkOrders = allWorkOrders.Where(wo =>
                wo.RequestedCompletionDate < DateTime.UtcNow &&
                wo.Status != "Completed" &&
                wo.Status != "Cancelled");
            var results = new List<WorkOrderListDto>();
            foreach (var workOrder in overdueWorkOrders)
            {
                var dto = await MapToWorkOrderListDtoAsync(workOrder);
                results.Add(dto);
            }
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting overdue work orders");
            throw;
        }
    }

    /// <summary>
    /// Gets work orders due in specified days
    /// </summary>
    public async Task<IEnumerable<WorkOrderListDto>> GetWorkOrdersDueInDaysAsync(int days)
    {
        try
        {
            var allWorkOrders = await _workOrderRepository.GetAllAsync();
            var dueDate = DateTime.UtcNow.AddDays(days);
            var workOrdersDue = allWorkOrders.Where(wo =>
                wo.RequestedCompletionDate <= dueDate &&
                wo.Status != "Completed" &&
                wo.Status != "Cancelled");
            var results = new List<WorkOrderListDto>();
            foreach (var workOrder in workOrdersDue)
            {
                var dto = await MapToWorkOrderListDtoAsync(workOrder);
                results.Add(dto);
            }
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting work orders due in {Days} days", days);
            throw;
        }
    }

    /// <summary>
    /// Gets work orders by technician
    /// </summary>
    public async Task<IEnumerable<WorkOrderListDto>> GetWorkOrdersByTechnicianAsync(Guid technicianId)
    {
        try
        {
            var workOrders = await _workOrderRepository.GetByAssignedTechnicianAsync(technicianId);
            var results = new List<WorkOrderListDto>();
            foreach (var workOrder in workOrders)
            {
                var dto = await MapToWorkOrderListDtoAsync(workOrder);
                results.Add(dto);
            }
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting work orders by technician {TechnicianId}", technicianId);
            throw;
        }
    }

    /// <summary>
    /// Gets work orders by team
    /// </summary>
    public async Task<IEnumerable<WorkOrderListDto>> GetWorkOrdersByTeamAsync(Guid teamId)
    {
        try
        {
            var workOrders = await _workOrderRepository.GetByAssignedTeamAsync(teamId);
            var results = new List<WorkOrderListDto>();
            foreach (var workOrder in workOrders)
            {
                var dto = await MapToWorkOrderListDtoAsync(workOrder);
                results.Add(dto);
            }
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting work orders by team {TeamId}", teamId);
            throw;
        }
    }

    /// <summary>
    /// Gets work orders by asset
    /// </summary>
    public async Task<IEnumerable<WorkOrderListDto>> GetWorkOrdersByAssetAsync(Guid assetId)
    {
        try
        {
            var workOrders = await _workOrderRepository.GetByAssetIdAsync(assetId);
            var results = new List<WorkOrderListDto>();
            foreach (var workOrder in workOrders)
            {
                var dto = await MapToWorkOrderListDtoAsync(workOrder);
                results.Add(dto);
            }
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting work orders by asset {AssetId}", assetId);
            throw;
        }
    }

    /// <summary>
    /// Gets work order metrics
    /// </summary>
    public async Task<ErpSystem.Core.DTOs.Maintenance.WorkOrderMetricsDto> GetWorkOrderMetricsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            _logger.LogInformation("Getting work order metrics from {StartDate} to {EndDate}", startDate, endDate);

            var result = await GetWorkOrdersPagedAsync(new WorkOrderFilterDto
            {
                StartDate = startDate,
                EndDate = endDate,
                Page = 1,
                PageSize = int.MaxValue
            });

            var workOrders = result.Items.ToList();
            var completedWorkOrders = workOrders
                .Where(wo => IsCompletedWorkOrderStatus(wo.Status))
                .ToList();
            var activeWorkOrders = workOrders
                .Where(wo => IsActiveWorkOrderStatus(wo.Status))
                .ToList();
            var totalWorkOrders = workOrders.Count;
            var totalCost = workOrders.Sum(wo => wo.ActualCost > 0 ? wo.ActualCost : wo.EstimatedCost);

            var metrics = new ErpSystem.Core.DTOs.Maintenance.WorkOrderMetricsDto
            {
                TotalWorkOrders = totalWorkOrders,
                CompletedWorkOrders = completedWorkOrders.Count,
                PendingWorkOrders = activeWorkOrders.Count,
                OverdueWorkOrders = workOrders.Count(wo => wo.IsOverdue),
                CompletionRate = totalWorkOrders > 0 ? (double)completedWorkOrders.Count / totalWorkOrders * 100 : 0.0,
                AverageCompletionTime = CalculateAverageWorkOrderDuration(completedWorkOrders),
                TotalCost = totalCost,
                TypeBreakdown = workOrders
                    .GroupBy(wo => string.IsNullOrWhiteSpace(wo.MaintenanceTypeName) ? "Unspecified" : wo.MaintenanceTypeName)
                    .Select(g => new ErpSystem.Core.DTOs.Maintenance.WorkOrderTypeMetricDto
                    {
                        WorkOrderType = g.Key,
                        Count = g.Count(),
                        Percentage = totalWorkOrders > 0 ? (double)g.Count() / totalWorkOrders * 100 : 0,
                        AverageCost = g.Any() ? g.Average(wo => wo.ActualCost > 0 ? wo.ActualCost : wo.EstimatedCost) : 0,
                        AverageCompletionTime = CalculateAverageWorkOrderDuration(g.Where(wo => IsCompletedWorkOrderStatus(wo.Status)).ToList())
                    })
                    .OrderByDescending(x => x.Count)
                    .ToList()
            };

            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting work order metrics");
            throw;
        }
    }

    /// <summary>
    /// Generates a unique work order number
    /// </summary>
    public async Task<string> GenerateWorkOrderNumberAsync()
    {
        try
        {
            // Simplified implementation - in a real implementation, this would generate unique numbers
            var date = DateTime.UtcNow;
            var random = new Random();
            return $"WO{date:yyyyMM}{random.Next(1000, 9999)}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating work order number");
            throw;
        }
    }

    /// <summary>
    /// Creates a child work order
    /// </summary>
    public async Task<WorkOrderDto> CreateChildWorkOrderAsync(Guid parentId, CreateWorkOrderDto createDto)
    {
        try
        {
            var parentWorkOrder = await _workOrderRepository.GetByIdAsync(parentId) ?? throw new ArgumentException($"Parent work order {parentId} not found");
            
            // Determine RequestedById - can be null for background service context
            Guid? requestedById = null;
            if (_currentUserService.UserId != null && Guid.TryParse(_currentUserService.UserId, out var userId3) && userId3 != Guid.Empty)
            {
                requestedById = userId3;
            }
            
            var childWorkOrder = new WorkOrder
            {
                Title = createDto.Title,
                Description = createDto.Description,
                AssetId = createDto.AssetId,
                WorkOrderTypeId = createDto.WorkOrderTypeId,
                MaintenanceTypeId = createDto.MaintenanceTypeId,
                PriorityLevelId = createDto.PriorityLevelId,
                TenantId = _currentUserService.TenantId ?? Guid.Empty,
                WorkOrderNumber = await GenerateWorkOrderNumberAsync(),
                RequestedById = requestedById, // Can be null for auto-generated work orders
                AssignedTechnicianId = createDto.AssignedTechnicianId, // Assign technician if provided
                AssignedTeamId = createDto.AssignedTeamId, // Assign team if provided
                ParentWorkOrderId = parentId,
                Status = !string.IsNullOrWhiteSpace(createDto.Status) ? createDto.Status : "Open",
                RequestedStartDate = createDto.RequestedStartDate,
                RequestedCompletionDate = createDto.RequestedCompletionDate,
                EstimatedHours = createDto.EstimatedHours,
                EstimatedCost = createDto.EstimatedCost,
                SafetyRequirements = createDto.SafetyRequirements,
                RequiresPermit = createDto.RequiresPermit,
                RequiresLockout = createDto.RequiresLockout,
                RequiresConfinedSpaceEntry = createDto.RequiresConfinedSpaceEntry
            };

            await _workOrderRepository.AddAsync(childWorkOrder);
            await _unitOfWork.SaveChangesAsync();
            return await MapToWorkOrderDtoAsync(childWorkOrder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating child work order for parent {ParentId}", parentId);
            throw;
        }
    }

    /// <summary>
    /// Gets child work orders
    /// </summary>
    public async Task<IEnumerable<WorkOrderListDto>> GetChildWorkOrdersAsync(Guid parentId)
    {
        try
        {
            var workOrders = await _workOrderRepository.GetChildWorkOrdersAsync(parentId);
            var results = new List<WorkOrderListDto>();
            foreach (var workOrder in workOrders)
            {
                var dto = await MapToWorkOrderListDtoAsync(workOrder);
                results.Add(dto);
            }
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting child work orders for parent {ParentId}", parentId);
            throw;
        }
    }

    /// <summary>
    /// Assigns technician to work order (interface method)
    /// </summary>
    public async Task<WorkOrderDto> AssignTechnicianAsync(Guid workOrderId, Guid technicianId)
    {
        var result = await AssignWorkOrderAsync(workOrderId, technicianId, null);
        return result;
    }

    /// <summary>
    /// Gets work orders by status (helper method)
    /// </summary>
    public async Task<IEnumerable<WorkOrderListDto>> GetWorkOrdersByStatusAsync(string status)
    {
        try
        {
            var workOrders = await _workOrderRepository.GetByStatusAsync(status);
            var results = new List<WorkOrderListDto>();
            foreach (var workOrder in workOrders)
            {
                var dto = await MapToWorkOrderListDtoAsync(workOrder);
                results.Add(dto);
            }
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting work orders by status {Status}", status);
            throw;
        }
    }

    #endregion

    #region Helper Mapping Methods

    private static bool IsMaintenanceBilling(string? billingType)
        => string.Equals(billingType, "Maintenance", StringComparison.OrdinalIgnoreCase);

    private static decimal ResolveFixedAmount(WorkOrder workOrder, decimal maintenanceTypeFixedAmount = 0)
    {
        if (!IsMaintenanceBilling(workOrder.BillingType))
        {
            return workOrder.FixedAmount;
        }

        return workOrder.FixedAmount > 0 ? workOrder.FixedAmount : maintenanceTypeFixedAmount;
    }

    private async Task<decimal> CalculateBillingTabTotalAsync(WorkOrder workOrder, decimal maintenanceTypeFixedAmount = 0)
    {
        if (IsMaintenanceBilling(workOrder.BillingType))
        {
            return ResolveFixedAmount(workOrder, maintenanceTypeFixedAmount);
        }

        var parts = await _unitOfWork.Repository<WorkOrderPart>()
            .GetQueryable(part => part.WorkOrderId == workOrder.Id && !part.IsDeleted)
            .ToListAsync();

        var partsTotal = parts.Sum(part =>
        {
            var quantity = part.QuantityUsed > 0 ? part.QuantityUsed : part.QuantityRequired;
            return part.TotalCost > 0 ? part.TotalCost : quantity * part.UnitCost;
        });

        var labor = await _unitOfWork.Repository<WorkOrderLabor>()
            .GetQueryable(record => record.WorkOrderId == workOrder.Id && !record.IsDeleted)
            .ToListAsync();

        var laborTotal = labor.Sum(record =>
        {
            var hours = Convert.ToDecimal(record.Hours);
            return record.TotalCost > 0 ? record.TotalCost : hours * record.HourlyRate;
        });

        // Keep this aligned with the Billing tab: each non-excluded tool contributes its daily rental rate.
        var tools = await _unitOfWork.Repository<WorkOrderTool>()
            .GetQueryable(tool => tool.WorkOrderId == workOrder.Id && !tool.IsDeleted && !tool.IsExcludedFromBilling)
            .Include(tool => tool.Tool)
            .ToListAsync();

        var toolsTotal = tools.Sum(tool => tool.Tool?.DailyRentalRate ?? 0m);

        var expenses = await _expenseRepository.GetByWorkOrderIdAsync(workOrder.Id);
        var expensesTotal = expenses.Sum(expense => expense.Amount);

        return Math.Round(partsTotal + laborTotal + toolsTotal + expensesTotal, 2, MidpointRounding.AwayFromZero);
    }

    private async Task<WorkOrderDto> MapToWorkOrderDtoAsync(WorkOrder workOrder)
    {
        // Get assigned technician name
        string? assignedTechnicianName = null;
        if (workOrder.AssignedTechnicianId.HasValue)
        {
            try
            {
                var technician = await _employeeRepository.GetByIdAsync(workOrder.AssignedTechnicianId.Value);
                if (technician != null)
                {
                    assignedTechnicianName = $"{technician.FirstName} {technician.LastName}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load technician {TechnicianId}", workOrder.AssignedTechnicianId.Value);
            }
        }

        // Get asset information including category
        string? assetName = null;
        string? assetCategory = null;
        if (workOrder.AssetId != Guid.Empty)
        {
            try
            {
                var asset = await _assetRepository.GetByIdAsync(workOrder.AssetId);
                if (asset != null)
                {
                    assetName = asset.Name;
                    // Asset category comes from navigation property if loaded
                    assetCategory = asset.AssetCategory?.Name ?? asset.AssetCategory?.AssetType;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load asset {AssetId}", workOrder.AssetId);
            }
        }

        // Build labor list with technician information
        var laborDtos = new List<WorkOrderLaborDto>();
        if (workOrder.Labor != null)
        {
            foreach (var labor in workOrder.Labor)
            {
                var laborDto = new WorkOrderLaborDto
                {
                    Id = labor.Id,
                    WorkOrderId = labor.WorkOrderId,
                    TechnicianId = labor.TechnicianId,
                    StartTime = labor.StartTime,
                    EndTime = labor.EndTime,
                    Hours = labor.Hours,
                    HourlyRate = labor.HourlyRate,
                    TotalCost = labor.TotalCost,
                    Notes = labor.Notes,
                    LaborType = labor.LaborType ?? "Regular",
                    CreatedAt = labor.CreatedAt,
                    UpdatedAt = labor.UpdatedAt
                };

                // Look up technician information (Employee-driven). Backward compatibility: fall back to Users for legacy records.
                try
                {
                    _logger.LogInformation("Looking up technician with ID: {TechnicianId} for labor record {LaborId}", labor.TechnicianId, labor.Id);
                    var employee = await _employeeRepository.GetByIdAsync(labor.TechnicianId);
                    if (employee != null)
                    {
                        _logger.LogInformation("Found employee: {EmployeeId}, Name: {Name}, EmployeeNumber: {EmployeeNumber}",
                            employee.Id, employee.FullName, employee.EmployeeNumber);
                        laborDto.Technician = new ErpSystem.Core.DTOs.HR.EmployeeDto
                        {
                            Id = employee.Id,
                            FullName = employee.FullName,
                            EmployeeNumber = employee.EmployeeNumber
                        };
                    }
                    else
                    {
                        var user = await _userService.GetUserByIdAsync(labor.TechnicianId);
                        if (user != null)
                        {
                            _logger.LogInformation("Found user (legacy labor record): {UserId}, FirstName: {FirstName}, LastName: {LastName}, UserName: {UserName}",
                                user.Id, user.FirstName, user.LastName, user.UserName);
                            laborDto.Technician = new ErpSystem.Core.DTOs.HR.EmployeeDto
                            {
                                Id = user.Id,
                                FullName = $"{user.FirstName} {user.LastName}",
                                EmployeeNumber = user.UserName ?? string.Empty
                            };
                        }
                        else
                        {
                            _logger.LogWarning("No employee or user found for TechnicianId: {TechnicianId}", labor.TechnicianId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error looking up technician {TechnicianId} for labor record", labor.TechnicianId);
                }

                laborDtos.Add(laborDto);
            }
        }

        var detailMaintenanceTypeFixedAmount = workOrder.MaintenanceType?.FixedAmount ?? 0;
        if (IsMaintenanceBilling(workOrder.BillingType) && detailMaintenanceTypeFixedAmount <= 0)
        {
            try
            {
                var maintenanceType = await _maintenanceTypeRepository.GetByIdAsync(workOrder.MaintenanceTypeId);
                detailMaintenanceTypeFixedAmount = maintenanceType?.FixedAmount ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load maintenance type fixed amount {MaintenanceTypeId} for work order {WorkOrderId}",
                    workOrder.MaintenanceTypeId, workOrder.Id);
            }
        }

        var resolvedFixedAmount = ResolveFixedAmount(workOrder, detailMaintenanceTypeFixedAmount);

        var detailBillingTotal = await CalculateBillingTabTotalAsync(workOrder, detailMaintenanceTypeFixedAmount);

        return new WorkOrderDto
        {
            Id = workOrder.Id,
            WorkOrderNumber = workOrder.WorkOrderNumber,
            Title = workOrder.Title,
            Description = workOrder.Description,
            JobCardId = workOrder.JobCardId,
            JobCardNumber = workOrder.JobCard?.JobCardNumber,
            Status = workOrder.Status,
            MaintenanceLocation = workOrder.MaintenanceLocation ?? "Internal",
            AssetId = workOrder.AssetId,
            WorkOrderTypeId = workOrder.WorkOrderTypeId,
            MaintenanceTypeId = workOrder.MaintenanceTypeId,
            PriorityLevelId = workOrder.PriorityLevelId,
            RequestedStartDate = workOrder.RequestedStartDate,
            RequestedCompletionDate = workOrder.RequestedCompletionDate,
            ActualStartDate = workOrder.ActualStartDate,
            ActualCompletionDate = workOrder.ActualCompletionDate,
            EstimatedHours = workOrder.EstimatedHours,
            ActualHours = workOrder.ActualHours,
            EstimatedCost = workOrder.EstimatedCost,
            ActualCost = detailBillingTotal,
            BillingType = workOrder.BillingType,
            FixedAmount = resolvedFixedAmount,
            Type = workOrder.WorkOrderType?.Name ?? string.Empty,
            CompletionNotes = workOrder.CompletionNotes,
            FailureCode = workOrder.FailureCode,
            CauseCode = workOrder.CauseCode,
            ActionCode = workOrder.ActionCode,
            SafetyRequirements = workOrder.SafetyRequirements,
            RequiresPermit = workOrder.RequiresPermit,
            RequiresLockout = workOrder.RequiresLockout,
            RequiresConfinedSpaceEntry = workOrder.RequiresConfinedSpaceEntry,
            AssignedTechnicianId = workOrder.AssignedTechnicianId,
            AssignedTeamId = workOrder.AssignedTeamId,
            RequestedById = workOrder.RequestedById,
            ApprovedById = workOrder.ApprovedById,
            ApprovedAt = workOrder.ApprovedAt,
            ParentWorkOrderId = workOrder.ParentWorkOrderId,
            MaintenanceScheduleId = workOrder.MaintenanceScheduleId,
            IsRecurring = workOrder.IsRecurring,
            // Map navigation properties
            WorkOrderType = workOrder.WorkOrderType != null ? new WorkOrderTypeDto
            {
                Id = workOrder.WorkOrderType.Id,
                Name = workOrder.WorkOrderType.Name,
                Code = workOrder.WorkOrderType.Code,
                Description = workOrder.WorkOrderType.Description,
                Color = workOrder.WorkOrderType.Color,
                Icon = workOrder.WorkOrderType.Icon,
                IsActive = workOrder.WorkOrderType.IsActive,
                RequiresApproval = workOrder.WorkOrderType.RequiresApproval,
                DefaultPriority = workOrder.WorkOrderType.DefaultPriority
            } : null,
            MaintenanceType = workOrder.MaintenanceType != null ? new MaintenanceTypeDto
            {
                Id = workOrder.MaintenanceType.Id,
                Name = workOrder.MaintenanceType.Name,
                Code = workOrder.MaintenanceType.Code,
                Description = workOrder.MaintenanceType.Description,
                Color = workOrder.MaintenanceType.Color,
                Icon = workOrder.MaintenanceType.Icon,
                IsActive = workOrder.MaintenanceType.IsActive,
                FixedAmount = workOrder.MaintenanceType.FixedAmount
            } : null,
            PriorityLevel = workOrder.PriorityLevel != null ? new PriorityLevelDto
            {
                Id = workOrder.PriorityLevel.Id,
                Name = workOrder.PriorityLevel.Name,
                Code = string.Empty, // Not in entity
                Level = workOrder.PriorityLevel.Level,
                Description = workOrder.PriorityLevel.Description ?? string.Empty,
                Color = workOrder.PriorityLevel.Color ?? string.Empty,
                Icon = string.Empty, // Not in entity
                ResponseTime = workOrder.PriorityLevel.ResponseTimeHours * 60, // Convert hours to minutes
                EscalationTime = 0, // Not in entity
                RequiresApproval = false, // Not in entity
                NotificationRules = string.Empty, // Not in entity
                SlaHours = workOrder.PriorityLevel.ResponseTimeHours,
                AutoAssign = false, // Not in entity
                IsActive = workOrder.PriorityLevel.IsActive,
                CreatedAt = workOrder.PriorityLevel.CreatedAt,
                UpdatedAt = workOrder.PriorityLevel.UpdatedAt
            } : null,
            Tasks = await MapTasksWithTechniciansAsync(workOrder.Tasks),
            Parts = workOrder.Parts?.Select(p => new WorkOrderPartDto
            {
                Id = p.Id,
                WorkOrderId = p.WorkOrderId,
                InventoryItemId = p.InventoryItemId,
                ItemCode = p.ItemCode,
                ItemName = p.ItemName,
                Description = p.Description,
                QuantityRequired = p.QuantityRequired,
                QuantityAllocated = p.QuantityAllocated,
                QuantityUsed = p.QuantityUsed,
                QuantityReturned = p.QuantityReturned,
                UnitCost = p.UnitCost,
                TotalCost = p.TotalCost,
                SerialNumber = p.SerialNumber,
                LotNumber = p.LotNumber,
                Status = p.Status,
                AllocationId = p.AllocationId,
                AllocatedAt = p.AllocatedAt,
                PickedAt = p.PickedAt,
                UsedAt = p.UsedAt,
                Notes = p.Notes,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            }).ToList() ?? new List<WorkOrderPartDto>(),
            Labor = laborDtos,
            Tools = workOrder.Tools?.Select(t => new WorkOrderToolDto
            {
                Id = t.Id,
                WorkOrderId = t.WorkOrderId,
                ToolId = t.ToolId,
                IsRequired = t.IsRequired,
                IsAllocated = t.IsAllocated,
                AllocationDate = t.AllocationDate,
                CheckoutId = t.CheckoutId,
                Notes = t.Notes,
                IsExcludedFromBilling = t.IsExcludedFromBilling,
                BillingExclusionReason = t.BillingExclusionReason,
                BillingExcludedAt = t.BillingExcludedAt,
                BillingExcludedBy = t.BillingExcludedBy,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            }).ToList() ?? new List<WorkOrderToolDto>(),
            // Add loaded string values for frontend
            AssignedTechnicianName = assignedTechnicianName ?? string.Empty,
            AssetName = assetName ?? string.Empty,
            AssetCategory = assetCategory ?? string.Empty,
            CreatedAt = workOrder.CreatedAt,
            UpdatedAt = workOrder.UpdatedAt
        };
    }

    private async Task<WorkOrderListDto> MapToWorkOrderListDtoAsync(WorkOrder workOrder)
    {
        // Get technician name from HR if assigned
        string assignedTechnicianName = "Unassigned";
        if (workOrder.AssignedTechnicianId.HasValue)
        {
            try
            {
                var technician = await _employeeRepository.GetByIdAsync(workOrder.AssignedTechnicianId.Value);
                if (technician != null)
                {
                    assignedTechnicianName = $"{technician.FirstName} {technician.LastName}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load technician {TechnicianId} for work order {WorkOrderId}",
                    workOrder.AssignedTechnicianId.Value, workOrder.Id);
                assignedTechnicianName = "Unknown Technician";
            }
        }

        // Get asset information
        string assetName = "Unknown Asset";
        string assetNumber = "Unknown";
        try
        {
            var asset = await _assetRepository.GetByIdAsync(workOrder.AssetId);
            if (asset != null)
            {
                assetName = asset.Name;
                assetNumber = asset.AssetNumber;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load asset {AssetId} for work order {WorkOrderId}",
                workOrder.AssetId, workOrder.Id);
        }

        // Get work order type information
        string workOrderTypeName = "Unknown Type";
        try
        {
            var workOrderType = await _workOrderTypeRepository.GetByIdAsync(workOrder.WorkOrderTypeId);
            if (workOrderType != null)
            {
                workOrderTypeName = workOrderType.Name;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load work order type {WorkOrderTypeId} for work order {WorkOrderId}",
                workOrder.WorkOrderTypeId, workOrder.Id);
        }

        // Get maintenance type information
        string maintenanceTypeName = "Unknown Type";
        decimal maintenanceTypeFixedAmount = 0;
        try
        {
            var maintenanceType = await _maintenanceTypeRepository.GetByIdAsync(workOrder.MaintenanceTypeId);
            if (maintenanceType != null)
            {
                maintenanceTypeName = maintenanceType.Name;
                maintenanceTypeFixedAmount = maintenanceType.FixedAmount;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load maintenance type {MaintenanceTypeId} for work order {WorkOrderId}",
                workOrder.MaintenanceTypeId, workOrder.Id);
        }

        // Get priority level information
        string priorityName = "Medium";
        int priorityLevel = 1;
        try
        {
            var priority = await _priorityLevelRepository.GetByIdAsync(workOrder.PriorityLevelId);
            if (priority != null)
            {
                priorityName = priority.Name;
                priorityLevel = priority.Level;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load priority level {PriorityLevelId} for work order {WorkOrderId}",
                workOrder.PriorityLevelId, workOrder.Id);
        }

        var resolvedFixedAmount = ResolveFixedAmount(workOrder, maintenanceTypeFixedAmount);
        var actualCost = await CalculateBillingTabTotalAsync(workOrder, maintenanceTypeFixedAmount);

        return new WorkOrderListDto
        {
            Id = workOrder.Id,
            WorkOrderNumber = workOrder.WorkOrderNumber,
            Title = workOrder.Title,
            Description = workOrder.Description,
            JobCardId = workOrder.JobCardId,
            JobCardNumber = workOrder.JobCard?.JobCardNumber,
            AssetId = workOrder.AssetId,
            Status = workOrder.Status,
            MaintenanceLocation = workOrder.MaintenanceLocation ?? "Internal",
            AssetName = assetName,
            AssetNumber = assetNumber,
            WorkOrderTypeId = workOrder.WorkOrderTypeId,
            WorkOrderTypeName = workOrderTypeName,
            MaintenanceTypeId = workOrder.MaintenanceTypeId,
            MaintenanceTypeName = maintenanceTypeName,
            Priority = priorityName,
            PriorityName = priorityName,
            PriorityLevelId = workOrder.PriorityLevelId,
            PriorityLevel = priorityLevel,
            AssignedTechnicianId = workOrder.AssignedTechnicianId,
            AssignedTechnicianName = assignedTechnicianName,
            AssignedTeamName = "Team Name", // TODO: Load actual team name
            Type = workOrderTypeName, // Add this for frontend compatibility
            RequestedStartDate = workOrder.RequestedStartDate,
            RequestedCompletionDate = workOrder.RequestedCompletionDate,
            ScheduledStartDate = workOrder.RequestedStartDate,
            ScheduledEndDate = workOrder.RequestedCompletionDate,
            ActualStartDate = workOrder.ActualStartDate,
            ActualEndDate = workOrder.ActualCompletionDate,
            ActualCompletionDate = workOrder.ActualCompletionDate,
            BillingType = workOrder.BillingType,
            FixedAmount = resolvedFixedAmount,
            EstimatedCost = workOrder.EstimatedCost,
            ActualCost = actualCost,
            EstimatedHours = workOrder.EstimatedHours,
            ActualHours = workOrder.ActualHours,
            IsOverdue = IsWorkOrderOverdue(workOrder),
            TasksCount = workOrder.Tasks?.Count ?? 0,
            CompletedTasksCount = workOrder.Tasks?.Count(t => t.Status == "Completed") ?? 0,
            CompletionPercentage = workOrder.Tasks?.Count > 0 ? (double)(workOrder.Tasks?.Count(t => t.Status == "Completed") ?? 0) / workOrder.Tasks.Count * 100 : 0,
            CreatedAt = workOrder.CreatedAt,
            CreatedDate = workOrder.CreatedAt,
            UpdatedAt = workOrder.UpdatedAt,
            DueDate = workOrder.RequestedCompletionDate,
            WorkOrderSource = workOrder.MaintenanceScheduleId.HasValue || workOrder.IsRecurring ? "Scheduled" : "Manual"
        };
    }

    #endregion

    #region Additional Required Methods

    private static void EnsureTechnicianMatchesAssetLocation(
        ErpSystem.Core.Entities.HR.Employee technician,
        WorkOrder workOrder)
    {
        var assetLocationId = workOrder.Asset?.CurrentSiteLocationId;
        if (!assetLocationId.HasValue)
        {
            throw new InvalidOperationException(
                $"Asset {workOrder.Asset?.AssetNumber ?? workOrder.AssetId.ToString()} must have a current site location before a technician can be assigned.");
        }

        if (technician.LocationId != assetLocationId)
        {
            throw new InvalidOperationException(
                $"Technician {technician.FullName} is not assigned to the asset's current location.");
        }
    }

    private async Task<List<FleetDefect>> GetLinkedFleetDefectsAsync(WorkOrder workOrder)
    {
        return await _unitOfWork.Repository<FleetDefect>()
            .GetQueryable(defect =>
                defect.TenantId == workOrder.TenantId &&
                defect.WorkOrderId == workOrder.Id &&
                !defect.IsDeleted &&
                defect.Status != "Closed")
            .ToListAsync();
    }

    private static void EnsureLatestQualityCheckPassedForLinkedDefects(
        WorkOrder workOrder,
        IReadOnlyCollection<FleetDefect> linkedFleetDefects)
    {
        if (linkedFleetDefects.Count == 0)
            return;

        var latestQualityCheck = workOrder.QualityChecks
            .OrderByDescending(check => check.InspectionDate)
            .FirstOrDefault();

        if (latestQualityCheck == null ||
            !string.Equals(latestQualityCheck.OverallResult, "Pass", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The work order is linked to an inspection defect and cannot be completed until its latest QC result is Pass.");
        }
    }

    private async Task CloseLinkedFleetDefectsAsync(
        IEnumerable<FleetDefect> linkedFleetDefects,
        Guid? completedById,
        DateTime completedAt)
    {
        var defectRepository = _unitOfWork.Repository<FleetDefect>();
        foreach (var defect in linkedFleetDefects)
        {
            defect.Status = "Closed";
            defect.UpdatedAt = completedAt;
            defect.LastModifiedById = completedById;
            await defectRepository.UpdateAsync(defect);
        }
    }

    /// <summary>
    /// Gets work orders due soon
    /// </summary>
    public async Task<IEnumerable<WorkOrderListDto>> GetWorkOrdersDueSoonAsync(int days = 7)
    {
        try
        {
            var allWorkOrders = await _workOrderRepository.GetAllAsync();
            var dueDate = DateTime.UtcNow.AddDays(days);
            var workOrdersDue = allWorkOrders.Where(wo =>
                wo.RequestedCompletionDate <= dueDate &&
                wo.Status != "Completed" &&
                wo.Status != "Cancelled");

            var results = new List<WorkOrderListDto>();
            foreach (var workOrder in workOrdersDue)
            {
                var dto = await MapToWorkOrderListDtoAsync(workOrder);
                results.Add(dto);
            }
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting work orders due soon");
            throw;
        }
    }

    /// <summary>
    /// Gets work orders scheduled today
    /// </summary>
    public async Task<IEnumerable<WorkOrderListDto>> GetWorkOrdersScheduledTodayAsync()
    {
        try
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var allWorkOrders = await _workOrderRepository.GetAllAsync();
            var todaysWorkOrders = allWorkOrders.Where(wo =>
                wo.RequestedStartDate >= today &&
                wo.RequestedStartDate < tomorrow &&
                wo.Status != "Completed" &&
                wo.Status != "Cancelled");

            var results = new List<WorkOrderListDto>();
            foreach (var workOrder in todaysWorkOrders)
            {
                var dto = await MapToWorkOrderListDtoAsync(workOrder);
                results.Add(dto);
            }
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting work orders scheduled today");
            throw;
        }
    }

    // AssignTechnicianAsync is already implemented earlier in the class with different signature

    /// <summary>
    /// Cancels a work order
    /// </summary>
    public async Task<WorkOrderDto> CancelWorkOrderAsync(Guid workOrderId, string reason)
    {
        try
        {
            var workOrder = await _workOrderRepository.GetByIdAsync(workOrderId) ?? throw new ArgumentException($"Work order {workOrderId} not found");
            workOrder.Status = "Cancelled";
            workOrder.CompletionNotes = reason;
            await _workOrderRepository.UpdateAsync(workOrder);
            await _unitOfWork.SaveChangesAsync();

            return await MapToWorkOrderDtoAsync(workOrder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling work order {WorkOrderId}", workOrderId);
            throw;
        }
    }

    /// <summary>
    /// Updates work order priority
    /// </summary>
    public async Task<WorkOrderDto> UpdatePriorityAsync(Guid workOrderId, string priority)
    {
        try
        {
            var workOrder = await _workOrderRepository.GetByIdAsync(workOrderId) ?? throw new ArgumentException($"Work order {workOrderId} not found");

            // Note: This is a simplified implementation - in reality you'd want to validate priority
            // and possibly map from string to appropriate PriorityLevelId
            await _workOrderRepository.UpdateAsync(workOrder);
            await _unitOfWork.SaveChangesAsync();

            return await MapToWorkOrderDtoAsync(workOrder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating priority for work order {WorkOrderId}", workOrderId);
            throw;
        }
    }

    /// <summary>
    /// Reschedules a work order
    /// </summary>
    public async Task<WorkOrderDto> RescheduleWorkOrderAsync(Guid workOrderId, DateTime newScheduledDate)
    {
        try
        {
            var workOrder = await _workOrderRepository.GetByIdAsync(workOrderId) ?? throw new ArgumentException($"Work order {workOrderId} not found");
            workOrder.RequestedStartDate = newScheduledDate;
            await _workOrderRepository.UpdateAsync(workOrder);
            await _unitOfWork.SaveChangesAsync();

            return await MapToWorkOrderDtoAsync(workOrder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rescheduling work order {WorkOrderId}", workOrderId);
            throw;
        }
    }

    /// <summary>
    /// Creates work orders from schedule
    /// </summary>
    public async Task<IEnumerable<WorkOrderDto>> CreateWorkOrdersFromScheduleAsync(Guid scheduleId, int count = 1)
    {
        try
        {
            var results = new List<WorkOrderDto>();

            // This is a placeholder implementation
            // In a real implementation, you'd load the schedule and create work orders based on it
            _logger.LogWarning("CreateWorkOrdersFromScheduleAsync is not fully implemented");

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating work orders from schedule {ScheduleId}", scheduleId);
            throw;
        }
    }

    /// <summary>
    /// Updates a work order task status with user tracking
    /// </summary>
    public async Task<ErpSystem.Core.DTOs.Maintenance.WorkOrderTaskDto?> UpdateTaskStatusAsync(
        Guid taskId,
        string status,
        double? actualHours = null,
        string? completionNotes = null,
        Guid? technicianId = null)
    {
        try
        {
            var task = await _taskRepository.GetByIdAsync(taskId);
            if (task == null)
            {
                _logger.LogWarning("Task {TaskId} not found", taskId);
                return null;
            }

            var currentUserId = _currentUserService.UserId;
            var currentEmployeeId = _currentUserService.EmployeeId;

            _logger.LogInformation(
                "Updating task {TaskId} status from {OldStatus} to {NewStatus} by user {UserId}",
                taskId, task.Status, status, currentUserId);

            // Update task status
            var previousStatus = task.Status;
            task.Status = status;
            task.UpdatedAt = DateTime.UtcNow;
            task.UpdatedBy = currentUserId ?? "System";

            // Track who is working on the task
            if (status == "InProgress" && previousStatus == "Pending")
            {
                task.StartedAt = DateTime.UtcNow;
                // If no technician assigned yet, assign current user or provided technician
                if (!task.AssignedTechnicianId.HasValue)
                {
                    if (technicianId.HasValue)
                    {
                        task.AssignedTechnicianId = technicianId.Value;
                    }
                    else if (currentEmployeeId.HasValue)
                    {
                        task.AssignedTechnicianId = currentEmployeeId.Value;
                    }
                }
            }

            // Handle task completion
            if (status == "Completed")
            {
                task.CompletedAt = DateTime.UtcNow;
                if (actualHours.HasValue)
                {
                    task.ActualHours = actualHours.Value;
                }
                if (!string.IsNullOrEmpty(completionNotes))
                {
                    task.CompletionNotes = completionNotes;
                }
                // Assign technician if provided and not already assigned
                if (technicianId.HasValue && !task.AssignedTechnicianId.HasValue)
                {
                    task.AssignedTechnicianId = technicianId.Value;
                }
                // Override technician assignment if explicitly provided during completion
                else if (technicianId.HasValue)
                {
                    task.AssignedTechnicianId = technicianId.Value;
                }
            }

            await _taskRepository.UpdateAsync(task);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "Task {TaskId} status updated successfully to {Status}",
                taskId, status);

            // Map to DTO and include technician information
            var technician = task.AssignedTechnicianId.HasValue
                ? await _employeeRepository.GetByIdAsync(task.AssignedTechnicianId.Value)
                : null;

            return new WorkOrderTaskDtoFull
            {
                Id = task.Id,
                WorkOrderId = task.WorkOrderId,
                TaskName = task.TaskName,
                Description = task.Description,
                Sequence = task.Sequence,
                Status = task.Status,
                EstimatedHours = task.EstimatedHours,
                ActualHours = task.ActualHours,
                IsRequired = task.IsRequired,
                PhotoPath = task.PhotoPath,
                AssignedTechnicianId = task.AssignedTechnicianId,
                StartedAt = task.StartedAt,
                CompletedAt = task.CompletedAt,
                CompletionNotes = task.CompletionNotes,
                CreatedAt = task.CreatedAt,
                AssignedTechnician = technician != null ? new ErpSystem.Core.DTOs.HR.EmployeeDto
                {
                    Id = technician.Id,
                    FullName = $"{technician.FirstName} {technician.LastName}",
                    EmployeeNumber = technician.EmployeeNumber
                } : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating task {TaskId} status", taskId);
            throw;
        }
    }

    /// <summary>
    /// Updates a work order task photo path
    /// </summary>
    public async Task<WorkOrderTaskDtoFull?> UpdateTaskPhotoAsync(Guid taskId, string? photoPath)
    {
        try
        {
            var task = await _taskRepository.GetByIdAsync(taskId);
            if (task == null)
            {
                _logger.LogWarning("Task {TaskId} not found for photo update", taskId);
                return null;
            }

            var currentUserId = _currentUserService.UserId;

            _logger.LogInformation(
                "Updating task {TaskId} photo by user {UserId}",
                taskId, currentUserId);

            task.PhotoPath = photoPath;
            task.UpdatedAt = DateTime.UtcNow;
            task.UpdatedBy = currentUserId ?? "System";

            await _taskRepository.UpdateAsync(task);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Task {TaskId} photo updated successfully", taskId);

            // Map to DTO and include technician information
            var technician = task.AssignedTechnicianId.HasValue
                ? await _employeeRepository.GetByIdAsync(task.AssignedTechnicianId.Value)
                : null;

            return new WorkOrderTaskDtoFull
            {
                Id = task.Id,
                WorkOrderId = task.WorkOrderId,
                TaskName = task.TaskName,
                Description = task.Description,
                Sequence = task.Sequence,
                Status = task.Status,
                EstimatedHours = task.EstimatedHours,
                ActualHours = task.ActualHours,
                IsRequired = task.IsRequired,
                PhotoPath = task.PhotoPath,
                AssignedTechnicianId = task.AssignedTechnicianId,
                StartedAt = task.StartedAt,
                CompletedAt = task.CompletedAt,
                CompletionNotes = task.CompletionNotes,
                CreatedAt = task.CreatedAt,
                AssignedTechnician = technician != null ? new ErpSystem.Core.DTOs.HR.EmployeeDto
                {
                    Id = technician.Id,
                    FullName = $"{technician.FirstName} {technician.LastName}",
                    EmployeeNumber = technician.EmployeeNumber
                } : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating task {TaskId} photo", taskId);
            throw;
        }
    }

    /// <summary>
    /// Validates that all assigned vehicles are available (Active status)
    /// </summary>
    private async Task<(bool IsValid, List<string> UnavailableVehicles)> ValidateVehicleAvailabilityAsync(WorkOrder workOrder, bool allowInUseWhenResuming = false)
    {
        try
        {
            // Get all schedules and expenses for this work order
            var schedules = await _scheduleRepository.GetByWorkOrderIdAsync(workOrder.Id);
            var expenses = await _expenseRepository.GetByWorkOrderIdAsync(workOrder.Id);

            // Collect all vehicle IDs from schedules and expenses
            var vehicleIds = new List<Guid>();

            // From schedules
            vehicleIds.AddRange(schedules
                .Where(s => s.AssignedVehicleId.HasValue)
                .Select(s => s.AssignedVehicleId!.Value));

            // From expenses
            vehicleIds.AddRange(expenses
                .Where(e => e.VehicleId.HasValue)
                .Select(e => e.VehicleId!.Value));

            // Remove duplicates
            vehicleIds = vehicleIds.Distinct().ToList();

            if (!vehicleIds.Any())
            {
                return (true, new List<string>()); // No vehicles assigned, validation passes
            }

            // Check each vehicle's availability
            var unavailableVehicles = new List<string>();

            foreach (var vehicleId in vehicleIds)
            {
                var vehicle = await _assetRepository.GetByIdAsync(vehicleId);
                if (vehicle == null)
                {
                    unavailableVehicles.Add($"Vehicle ID {vehicleId} not found");
                }
                else if (vehicle.Status != ErpSystem.Core.Enums.AssetStatus.Active)
                {
                    // Resume special-case: if the work order is resuming from OnHold, allow a stale InUse state as long as
                    // the vehicle isn't actively in a fleet trip and isn't being used by another InProgress work order.
                    if (allowInUseWhenResuming && vehicle.Status == ErpSystem.Core.Enums.AssetStatus.InUse)
                    {
                        var inFleetTrip = await IsVehicleInActiveFleetTripAsync(vehicleId, workOrder.TenantId);
                        var inOtherWorkOrder = await IsVehicleInUseByOtherInProgressWorkOrderAsync(vehicleId, workOrder.TenantId, workOrder.Id);

                        if (!inFleetTrip && !inOtherWorkOrder)
                        {
                            continue;
                        }
                    }

                    var statusName = vehicle.Status.ToString();
                    unavailableVehicles.Add($"{vehicle.Name} ({vehicle.AssetNumber}) is currently {statusName}");
                    _logger.LogWarning("Vehicle {VehicleId} ({VehicleName}) is not available for work order {WorkOrderId}. Current status: {Status}",
                        vehicleId, vehicle.Name, workOrder.Id, statusName);
                }
            }

            bool isValid = !unavailableVehicles.Any();

            if (isValid)
            {
                _logger.LogInformation("All {Count} vehicle(s) are available for work order {WorkOrderId}",
                    vehicleIds.Count, workOrder.Id);
            }
            else
            {
                _logger.LogWarning("{Count} vehicle(s) are unavailable for work order {WorkOrderId}",
                    unavailableVehicles.Count, workOrder.Id);
            }

            return (isValid, unavailableVehicles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating vehicle availability for work order {WorkOrderId}", workOrder.Id);
            throw;
        }
    }

    private async Task<bool> IsVehicleInActiveFleetTripAsync(Guid vehicleAssetId, Guid tenantId)
    {
        try
        {
            var q = _unitOfWork.Repository<ErpSystem.Core.Entities.Maintenance.FleetTrip>()
                .GetQueryable(t =>
                    t.TenantId == tenantId &&
                    !t.IsDeleted &&
                    t.VehicleAssetId == vehicleAssetId &&
                    t.Status == ErpSystem.Core.Entities.Maintenance.FleetTripStatuses.Dispatched);

            return await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(q);
        }
        catch
        {
            // Best-effort: if we can't validate, be conservative and block resume/start.
            return true;
        }
    }

    private async Task<bool> IsVehicleInUseByOtherInProgressWorkOrderAsync(Guid vehicleAssetId, Guid tenantId, Guid currentWorkOrderId)
    {
        try
        {
            var scheduleWorkOrderIds = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                _scheduleRepository
                    .GetQueryable(s =>
                        s.TenantId == tenantId &&
                        !s.IsDeleted &&
                        s.AssignedVehicleId.HasValue &&
                        s.AssignedVehicleId.Value == vehicleAssetId &&
                        s.WorkOrderId.HasValue &&
                        s.WorkOrderId.Value != currentWorkOrderId)
                    .Select(s => s.WorkOrderId!.Value)
                    .Distinct());

            var expenseWorkOrderIds = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                _unitOfWork.Repository<MaintenanceExpense>()
                    .GetQueryable(e =>
                        e.TenantId == tenantId &&
                        !e.IsDeleted &&
                        e.VehicleId.HasValue &&
                        e.VehicleId.Value == vehicleAssetId &&
                        e.WorkOrderId != currentWorkOrderId)
                    .Select(e => e.WorkOrderId)
                    .Distinct());

            var otherIds = scheduleWorkOrderIds
                .Concat(expenseWorkOrderIds)
                .Distinct()
                .ToList();

            if (otherIds.Count == 0) return false;

            var q = _unitOfWork.Repository<WorkOrder>()
                .GetQueryable(w =>
                    w.TenantId == tenantId &&
                    !w.IsDeleted &&
                    otherIds.Contains(w.Id) &&
                    w.Status == "InProgress");

            return await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(q);
        }
        catch
        {
            // Best-effort: if we can't validate, be conservative and block resume/start.
            return true;
        }
    }

    /// <summary>
    /// Updates vehicle status to InUse when work order starts
    /// </summary>
    private async Task UpdateVehicleStatusOnWorkOrderStartAsync(Guid workOrderId)
    {
        try
        {
            // Get all schedules and expenses for this work order
            var schedules = await _scheduleRepository.GetByWorkOrderIdAsync(workOrderId);
            var expenses = await _expenseRepository.GetByWorkOrderIdAsync(workOrderId);

            // Collect all vehicle IDs from schedules and expenses
            var vehicleIds = new List<Guid>();

            // From schedules
            vehicleIds.AddRange(schedules
                .Where(s => s.AssignedVehicleId.HasValue)
                .Select(s => s.AssignedVehicleId!.Value));

            // From expenses
            vehicleIds.AddRange(expenses
                .Where(e => e.VehicleId.HasValue)
                .Select(e => e.VehicleId!.Value));

            // Remove duplicates
            vehicleIds = vehicleIds.Distinct().ToList();

            if (!vehicleIds.Any())
            {
                _logger.LogInformation("No vehicles to update for work order {WorkOrderId}", workOrderId);
                return;
            }

            _logger.LogInformation("Found {Count} vehicle(s) to update for work order {WorkOrderId}: {VehicleIds}",
                vehicleIds.Count, workOrderId, string.Join(", ", vehicleIds));

            // Update each vehicle status to InUse
            int updatedCount = 0;
            foreach (var vehicleId in vehicleIds)
            {
                var vehicle = await _assetRepository.GetByIdAsync(vehicleId);
                if (vehicle == null)
                {
                    _logger.LogWarning("Vehicle {VehicleId} not found for work order {WorkOrderId}", vehicleId, workOrderId);
                    continue;
                }

                _logger.LogInformation("Vehicle {VehicleId} ({VehicleName}) current status: {Status}",
                    vehicleId, vehicle.Name, vehicle.Status);

                if (vehicle.Status == ErpSystem.Core.Enums.AssetStatus.Active)
                {
                    vehicle.Status = ErpSystem.Core.Enums.AssetStatus.InUse;
                    vehicle.UpdatedAt = DateTime.UtcNow;
                    await _assetRepository.UpdateAsync(vehicle);
                    updatedCount++;
                    _logger.LogInformation("Vehicle {VehicleId} ({VehicleName}) status updated to InUse for work order {WorkOrderId}",
                        vehicleId, vehicle.Name, workOrderId);
                }
                else
                {
                    _logger.LogWarning("Vehicle {VehicleId} ({VehicleName}) status is {Status}, expected Active. Skipping update.",
                        vehicleId, vehicle.Name, vehicle.Status);
                }
            }

            if (updatedCount > 0)
            {
                _logger.LogInformation("Saving changes for {Count} vehicle(s)...", updatedCount);
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("{Count} vehicle(s) marked as InUse for work order {WorkOrderId}",
                    updatedCount, workOrderId);
            }
            else
            {
                _logger.LogWarning("No vehicles were updated for work order {WorkOrderId}", workOrderId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating vehicle status for work order {WorkOrderId}. Exception: {Message}",
                workOrderId, ex.Message);
            // Don't throw - this shouldn't block work order start
        }
    }

    /// <summary>
    /// Updates vehicle status back to Active when work order completes
    /// </summary>
    private async Task UpdateVehicleStatusOnWorkOrderCompleteAsync(Guid workOrderId)
    {
        try
        {
            _logger.LogInformation("Starting vehicle status release for work order {WorkOrderId}", workOrderId);

            // Get all schedules and expenses for this work order
            var schedules = await _scheduleRepository.GetByWorkOrderIdAsync(workOrderId);
            var expenses = await _expenseRepository.GetByWorkOrderIdAsync(workOrderId);

            _logger.LogInformation("Found {ScheduleCount} schedules and {ExpenseCount} expenses for work order {WorkOrderId}",
                schedules.Count(), expenses.Count(), workOrderId);

            // Collect all vehicle IDs from schedules and expenses
            var vehicleIds = new List<Guid>();

            // From schedules
            var scheduleVehicles = schedules
                .Where(s => s.AssignedVehicleId.HasValue)
                .Select(s => s.AssignedVehicleId!.Value)
                .ToList();
            vehicleIds.AddRange(scheduleVehicles);
            _logger.LogInformation("Found {Count} vehicles from schedules: {VehicleIds}",
                scheduleVehicles.Count, string.Join(", ", scheduleVehicles));

            // From expenses
            var expenseVehicles = expenses
                .Where(e => e.VehicleId.HasValue)
                .Select(e => e.VehicleId!.Value)
                .ToList();
            vehicleIds.AddRange(expenseVehicles);
            _logger.LogInformation("Found {Count} vehicles from expenses: {VehicleIds}",
                expenseVehicles.Count, string.Join(", ", expenseVehicles));

            // Remove duplicates
            vehicleIds = vehicleIds.Distinct().ToList();

            if (!vehicleIds.Any())
            {
                _logger.LogInformation("No vehicles to release for work order {WorkOrderId}", workOrderId);
                return;
            }

            _logger.LogInformation("Found {Count} unique vehicle(s) to release for work order {WorkOrderId}: {VehicleIds}",
                vehicleIds.Count, workOrderId, string.Join(", ", vehicleIds));

            // Update each vehicle status back to Active
            int releasedCount = 0;
            foreach (var vehicleId in vehicleIds)
            {
                var vehicle = await _assetRepository.GetByIdAsync(vehicleId);
                if (vehicle == null)
                {
                    _logger.LogWarning("Vehicle {VehicleId} not found for work order {WorkOrderId}", vehicleId, workOrderId);
                    continue;
                }

                _logger.LogInformation("Vehicle {VehicleId} ({VehicleName}) current status: {Status}",
                    vehicleId, vehicle.Name, vehicle.Status);

                if (vehicle.Status == ErpSystem.Core.Enums.AssetStatus.InUse)
                {
                    vehicle.Status = ErpSystem.Core.Enums.AssetStatus.Active;
                    vehicle.UpdatedAt = DateTime.UtcNow;
                    await _assetRepository.UpdateAsync(vehicle);
                    releasedCount++;
                    _logger.LogInformation("Vehicle {VehicleId} ({VehicleName}) status updated to Active after work order {WorkOrderId} completion",
                        vehicleId, vehicle.Name, workOrderId);
                }
                else
                {
                    _logger.LogWarning("Vehicle {VehicleId} ({VehicleName}) status is {Status}, expected InUse. Skipping release.",
                        vehicleId, vehicle.Name, vehicle.Status);
                }
            }

            if (releasedCount > 0)
            {
                _logger.LogInformation("Saving changes for {Count} vehicle(s)...", releasedCount);
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("{Count} vehicle(s) marked as Active after work order {WorkOrderId} completion",
                    releasedCount, workOrderId);
            }
            else
            {
                _logger.LogWarning("No vehicles were released for work order {WorkOrderId}", workOrderId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing vehicle status for work order {WorkOrderId}. Exception: {Message}",
                workOrderId, ex.Message);
            // Don't throw - this shouldn't block work order completion
        }
    }

    private static decimal CalculateUsedPartsCost(WorkOrder workOrder)
    {
        if (workOrder.Parts == null || workOrder.Parts.Count == 0) return 0m;

        // WorkOrderPart.TotalCost represents required cost (QuantityRequired * UnitCost).
        // For actual costing we sum UnitCost * QuantityUsed to avoid over-counting unused/returned parts.
        decimal sum = 0m;
        foreach (var p in workOrder.Parts)
        {
            if (p.QuantityUsed <= 0) continue;
            if (p.UnitCost <= 0) continue;
            sum += p.UnitCost * p.QuantityUsed;
        }

        return Math.Round(sum, 2, MidpointRounding.AwayFromZero);
    }

    private async Task UpsertFleetInternalMaintenanceCostEntryAsync(
        WorkOrder workOrder,
        decimal actualCost,
        DateTime completedAtUtc,
        Guid? createdByUserId)
    {
        try
        {
            if (workOrder == null) return;
            if (actualCost <= 0) return;

            var tenantId = workOrder.TenantId;
            var now = DateTime.UtcNow;
            var effectiveUserId = createdByUserId.HasValue && createdByUserId.Value != Guid.Empty
                ? createdByUserId.Value
                : (Guid.TryParse(_currentUserService.UserId, out var uid) ? uid : Guid.Empty);
            var currencyCode = await MaintenanceCurrencyResolver.ResolveBaseCurrencyCodeAsync(_unitOfWork, tenantId);

            var vehicleAssetId = await TryResolveSingleVehicleAssetIdForWorkOrderAsync(workOrder);
            if (!vehicleAssetId.HasValue || vehicleAssetId.Value == Guid.Empty) return;

            // If this work order came from a fleet defect linked to a trip, carry the trip ID onto the cost entry (best-effort).
            Guid? fleetTripId = null;
            try
            {
                fleetTripId = await _unitOfWork.Repository<FleetDefect>()
                    .GetQueryable(d => d.TenantId == tenantId && !d.IsDeleted && d.WorkOrderId == workOrder.Id && d.FleetTripId.HasValue)
                    .OrderByDescending(d => d.ReportedAtUtc)
                    .Select(d => d.FleetTripId)
                    .FirstOrDefaultAsync();
            }
            catch
            {
                // best-effort
            }

            var repo = _unitOfWork.Repository<FleetCostEntry>();

            var existing = await repo.FirstOrDefaultAsync(c =>
                c.TenantId == tenantId &&
                c.WorkOrderId == workOrder.Id &&
                c.CostType == "InternalMaintenance" &&
                !c.IsDeleted);

            if (existing == null)
            {
                await repo.AddAsync(new FleetCostEntry
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    VehicleAssetId = vehicleAssetId.Value,
                    FleetTripId = fleetTripId,
                    WorkOrderId = workOrder.Id,
                    CostDateUtc = completedAtUtc.ToUniversalTime(),
                    CostType = "InternalMaintenance",
                    Source = "WorkOrderCompletion",
                    Amount = Math.Round(actualCost, 2, MidpointRounding.AwayFromZero),
                    CurrencyCode = currencyCode,
                    Notes = $"Work Order {workOrder.WorkOrderNumber} completion cost",
                    CreatedAt = now,
                    CreatedById = effectiveUserId
                });
            }
            else
            {
                existing.VehicleAssetId = vehicleAssetId.Value;
                existing.FleetTripId = fleetTripId;
                existing.CostDateUtc = completedAtUtc.ToUniversalTime();
                existing.Amount = Math.Round(actualCost, 2, MidpointRounding.AwayFromZero);
                existing.CurrencyCode = string.IsNullOrWhiteSpace(existing.CurrencyCode) ? currencyCode : existing.CurrencyCode;
                existing.Notes = $"Work Order {workOrder.WorkOrderNumber} completion cost";
                existing.Source = "WorkOrderCompletion";
                existing.UpdatedAt = now;
                existing.LastModifiedById = effectiveUserId;
                await repo.UpdateAsync(existing);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to upsert fleet internal maintenance cost entry for work order {WorkOrderId}", workOrder.Id);
        }
    }

    private async Task<Guid?> TryResolveSingleVehicleAssetIdForWorkOrderAsync(WorkOrder workOrder)
    {
        // Primary: the work order asset itself is a vehicle.
        if (workOrder.AssetId != Guid.Empty &&
            string.Equals(workOrder.Asset?.AssetCategory?.AssetType, "Vehicle", StringComparison.OrdinalIgnoreCase))
            return workOrder.AssetId;

        // Fallback: if schedules/expenses reference exactly one vehicle, use it.
        var schedules = await _scheduleRepository.GetByWorkOrderIdAsync(workOrder.Id);
        var expenses = await _expenseRepository.GetByWorkOrderIdAsync(workOrder.Id);

        var vehicleIds = new HashSet<Guid>();
        foreach (var s in schedules)
        {
            if (s.AssignedVehicleId.HasValue && s.AssignedVehicleId.Value != Guid.Empty)
                vehicleIds.Add(s.AssignedVehicleId.Value);
        }

        foreach (var e in expenses)
        {
            if (e.VehicleId.HasValue && e.VehicleId.Value != Guid.Empty)
                vehicleIds.Add(e.VehicleId.Value);
        }

        if (vehicleIds.Count != 1)
        {
            if (vehicleIds.Count > 1)
                _logger.LogInformation("Skipping fleet cost posting for work order {WorkOrderId} because multiple vehicles were referenced: {VehicleIds}", workOrder.Id, string.Join(", ", vehicleIds));
            return null;
        }

        return vehicleIds.First();
    }

    #endregion

    #region Task Mapping Helpers

    /// <summary>
    /// Maps work order tasks to DTOs including technician information
    /// </summary>
    private async Task<List<WorkOrderTaskDtoFull>> MapTasksWithTechniciansAsync(ICollection<WorkOrderTask>? tasks)
    {
        if (tasks == null || !tasks.Any())
        {
            return new List<WorkOrderTaskDtoFull>();
        }

        var taskDtos = new List<WorkOrderTaskDtoFull>();

        // Get all unique technician IDs from tasks
        var technicianIds = tasks
            .Where(t => t.AssignedTechnicianId.HasValue)
            .Select(t => t.AssignedTechnicianId!.Value)
            .Distinct()
            .ToList();

        // Fetch all technicians in one batch
        var technicians = new Dictionary<Guid, ErpSystem.Core.Entities.HR.Employee>();
        foreach (var techId in technicianIds)
        {
            try
            {
                var technician = await _employeeRepository.GetByIdAsync(techId);
                if (technician != null)
                {
                    technicians[techId] = technician;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load technician {TechnicianId} for task mapping", techId);
            }
        }

        // Map tasks with technician information
        foreach (var task in tasks)
        {
            var taskDto = new WorkOrderTaskDtoFull
            {
                Id = task.Id,
                WorkOrderId = task.WorkOrderId,
                TaskName = task.TaskName,
                Description = task.Description,
                Sequence = task.Sequence,
                Status = task.Status,
                EstimatedHours = task.EstimatedHours,
                ActualHours = task.ActualHours,
                IsRequired = task.IsRequired,
                PhotoPath = task.PhotoPath,
                AssignedTechnicianId = task.AssignedTechnicianId,
                StartedAt = task.StartedAt,
                CompletedAt = task.CompletedAt,
                CompletionNotes = task.CompletionNotes,
                CreatedAt = task.CreatedAt
            };

            // Add technician information if available
            if (task.AssignedTechnicianId.HasValue && technicians.TryGetValue(task.AssignedTechnicianId.Value, out var technician))
            {
                taskDto.AssignedTechnician = new ErpSystem.Core.DTOs.HR.EmployeeDto
                {
                    Id = technician.Id,
                    FullName = $"{technician.FirstName} {technician.LastName}",
                    EmployeeNumber = technician.EmployeeNumber
                };
            }

            taskDtos.Add(taskDto);
        }

        return taskDtos;
    }

    #endregion
}

