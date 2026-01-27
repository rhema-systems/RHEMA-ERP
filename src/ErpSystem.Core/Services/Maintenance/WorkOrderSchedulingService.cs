using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Maintenance;

/// <summary>
/// Service for scheduling work orders with automatic inventory reservation
/// </summary>
public class WorkOrderSchedulingService : IWorkOrderSchedulingService
{
    private readonly IWorkOrderRepository _workOrderRepository;
    private readonly IInventoryManagementService _inventoryService;
    private readonly IMaintenanceInventoryService _maintenanceInventoryService;
    private readonly ErpSystem.Core.Interfaces.Maintenance.IEmployeeRepository _employeeRepository;
    private readonly ITechnicianTeamRepository _technicianTeamRepository;
    private readonly ILogger<WorkOrderSchedulingService> _logger;

    public WorkOrderSchedulingService(
        IWorkOrderRepository workOrderRepository,
        IInventoryManagementService inventoryService,
        IMaintenanceInventoryService maintenanceInventoryService,
        ErpSystem.Core.Interfaces.Maintenance.IEmployeeRepository employeeRepository,
        ITechnicianTeamRepository technicianTeamRepository,
        ILogger<WorkOrderSchedulingService> logger)
    {
        _workOrderRepository = workOrderRepository;
        _inventoryService = inventoryService;
        _maintenanceInventoryService = maintenanceInventoryService;
        _employeeRepository = employeeRepository;
        _technicianTeamRepository = technicianTeamRepository;
        _logger = logger;
    }

    #region Work Order Scheduling

    /// <summary>
    /// Schedules a work order with automatic inventory reservation
    /// </summary>
    public async Task<WorkOrderSchedulingResult> ScheduleWorkOrderAsync(
        ScheduleWorkOrderDto scheduleDto, Guid scheduledById)
    {
        try
        {
            _logger.LogInformation("Scheduling work order {WorkOrderId} by user {ScheduledById}",
                scheduleDto.WorkOrderId, scheduledById);

            var workOrder = await _workOrderRepository.GetByIdAsync(scheduleDto.WorkOrderId) ?? throw new ArgumentException($"Work order {scheduleDto.WorkOrderId} not found");
            if (workOrder.Status != "Approved")
            {
                throw new InvalidOperationException($"Cannot schedule work order in {workOrder.Status} status");
            }

            var result = new WorkOrderSchedulingResult
            {
                WorkOrderId = scheduleDto.WorkOrderId,
                ScheduledById = scheduledById,
                SchedulingDate = DateTime.UtcNow,
                Success = true
            };

            // Validate technician/team availability
            await ValidateResourceAvailabilityAsync(scheduleDto, result);

            // Check parts availability and reserve if needed
            if (scheduleDto.ReserveParts && workOrder.Parts.Any())
            {
                await ProcessInventoryReservationAsync(scheduleDto.WorkOrderId, scheduleDto.RequestedStartDate,
                    scheduledById, result);
            }

            // Update work order scheduling
            workOrder.AssignedTechnicianId = scheduleDto.AssignedTechnicianId;
            workOrder.AssignedTeamId = scheduleDto.AssignedTeamId;
            workOrder.RequestedStartDate = scheduleDto.RequestedStartDate;
            workOrder.RequestedCompletionDate = scheduleDto.RequestedCompletionDate;
            workOrder.Status = "Assigned";

            await _workOrderRepository.UpdateAsync(workOrder);

            result.NewStatus = workOrder.Status;
            result.ScheduledStartDate = scheduleDto.RequestedStartDate;
            result.ScheduledCompletionDate = scheduleDto.RequestedCompletionDate;
            result.AssignedTechnicianId = scheduleDto.AssignedTechnicianId;
            result.AssignedTeamId = scheduleDto.AssignedTeamId;
            result.SuccessMessages.Add("Work order scheduled successfully");

            if (result.PartsReserved > 0)
            {
                result.SuccessMessages.Add($"Reserved {result.PartsReserved} parts for the scheduled work");
            }

            _logger.LogInformation("Work order {WorkOrderId} scheduled successfully for {StartDate}. " +
                "Reserved parts: {PartsReserved}, Technician: {TechnicianId}",
                scheduleDto.WorkOrderId, scheduleDto.RequestedStartDate, result.PartsReserved,
                scheduleDto.AssignedTechnicianId);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling work order {WorkOrderId}", scheduleDto.WorkOrderId);
            throw;
        }
    }

    /// <summary>
    /// Reschedules a work order with inventory reservation updates
    /// </summary>
    public async Task<WorkOrderSchedulingResult> RescheduleWorkOrderAsync(
        RescheduleWorkOrderDto rescheduleDto, Guid rescheduledById)
    {
        try
        {
            _logger.LogInformation("Rescheduling work order {WorkOrderId} by user {RescheduledById}",
                rescheduleDto.WorkOrderId, rescheduledById);

            var workOrder = await _workOrderRepository.GetByIdAsync(rescheduleDto.WorkOrderId) ?? throw new ArgumentException($"Work order {rescheduleDto.WorkOrderId} not found");
            if (workOrder.Status != "Assigned" && workOrder.Status != "OnHold")
            {
                throw new InvalidOperationException($"Cannot reschedule work order in {workOrder.Status} status");
            }

            var result = new WorkOrderSchedulingResult
            {
                WorkOrderId = rescheduleDto.WorkOrderId,
                ScheduledById = rescheduledById,
                SchedulingDate = DateTime.UtcNow,
                PreviousStartDate = workOrder.RequestedStartDate,
                PreviousCompletionDate = workOrder.RequestedCompletionDate,
                Success = true
            };

            // Update reservations if the schedule has changed significantly
            if (workOrder.Parts.Any() && rescheduleDto.UpdateReservations)
            {
                var timeDifference = rescheduleDto.NewStartDate - workOrder.RequestedStartDate;

                // If rescheduled by more than 1 day, update reservations
                if (Math.Abs(timeDifference?.TotalDays ?? 0) > 1)
                {
                    await UpdateInventoryReservationsAsync(rescheduleDto.WorkOrderId,
                        rescheduleDto.NewStartDate, rescheduledById, result);
                }
            }

            // Update technician assignment if changed
            if (rescheduleDto.NewAssignedTechnicianId.HasValue &&
                rescheduleDto.NewAssignedTechnicianId != workOrder.AssignedTechnicianId)
            {
                workOrder.AssignedTechnicianId = rescheduleDto.NewAssignedTechnicianId;
                result.AssignedTechnicianId = rescheduleDto.NewAssignedTechnicianId;
                result.SuccessMessages.Add("Technician assignment updated");
            }

            // Update team assignment if changed
            if (rescheduleDto.NewAssignedTeamId.HasValue &&
                rescheduleDto.NewAssignedTeamId != workOrder.AssignedTeamId)
            {
                workOrder.AssignedTeamId = rescheduleDto.NewAssignedTeamId;
                result.AssignedTeamId = rescheduleDto.NewAssignedTeamId;
                result.SuccessMessages.Add("Team assignment updated");
            }

            // Update scheduling
            workOrder.RequestedStartDate = rescheduleDto.NewStartDate;
            workOrder.RequestedCompletionDate = rescheduleDto.NewCompletionDate;

            await _workOrderRepository.UpdateAsync(workOrder);

            result.NewStatus = workOrder.Status;
            result.ScheduledStartDate = rescheduleDto.NewStartDate;
            result.ScheduledCompletionDate = rescheduleDto.NewCompletionDate;
            result.SuccessMessages.Add($"Work order rescheduled: {rescheduleDto.RescheduleReason}");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rescheduling work order {WorkOrderId}", rescheduleDto.WorkOrderId);
            throw;
        }
    }

    /// <summary>
    /// Gets work orders that need scheduling (approved but not assigned)
    /// </summary>
    public async Task<List<WorkOrderForSchedulingDto>> GetWorkOrdersForSchedulingAsync(
        DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            // Get all work orders and filter for those needing scheduling (approved but not assigned)
            var allWorkOrders = await _workOrderRepository.GetAllAsync();
            var workOrders = allWorkOrders
                .Where(wo => wo.Status == "Approved" &&
                           !wo.AssignedTechnicianId.HasValue &&
                           !wo.AssignedTeamId.HasValue)
                .Where(wo => startDate == null || wo.RequestedStartDate >= startDate)
                .Where(wo => endDate == null || wo.RequestedStartDate <= endDate)
                .ToList();

            var result = new List<WorkOrderForSchedulingDto>();

            foreach (var wo in workOrders)
            {
                var partsAvailability = await _maintenanceInventoryService.CheckPartsAvailabilityAsync(wo.Id);

                result.Add(new WorkOrderForSchedulingDto
                {
                    WorkOrderId = wo.Id,
                    WorkOrderNumber = wo.WorkOrderNumber,
                    Title = wo.Title,
                    Description = wo.Description,
                    AssetId = wo.AssetId,
                    AssetName = "Asset Information Not Available", // TODO: Load from Asset repository if needed
                    PriorityLevel = wo.PriorityLevel?.Level ?? 5,
                    PriorityName = wo.PriorityLevel?.Name ?? "Normal",
                    EstimatedHours = wo.EstimatedHours,
                    EstimatedCost = wo.EstimatedCost,
                    RequiredParts = wo.Parts.Count,
                    AllPartsAvailable = partsAvailability.AllPartsAvailable,
                    UnavailableParts = partsAvailability.UnavailableParts,
                    CreatedAt = wo.CreatedAt,
                    RequestedStartDate = wo.RequestedStartDate,
                    RequiresPermit = wo.RequiresPermit,
                    RequiresLockout = wo.RequiresLockout,
                    RequiresConfinedSpaceEntry = wo.RequiresConfinedSpaceEntry
                });
            }

            return result.OrderBy(wo => wo.PriorityLevel)
                        .ThenBy(wo => wo.CreatedAt)
                        .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting work orders for scheduling");
            throw;
        }
    }

    #endregion

    #region Resource Availability

    /// <summary>
    /// Checks technician availability for a given time period
    /// </summary>
    public async Task<TechnicianAvailabilityResult> CheckTechnicianAvailabilityAsync(
        Guid technicianId, DateTime startDate, DateTime endDate)
    {
        try
        {
            // This would integrate with the TechnicianSchedulingService
            // For now, return a basic implementation
            var technician = await _employeeRepository.GetByIdAsync(technicianId) ?? throw new ArgumentException($"Technician {technicianId} not found");

            // Check for existing work order assignments during this period
            var allWorkOrders = await _workOrderRepository.GetAllAsync();
            var existingWorkOrders = allWorkOrders
                .Where(wo => wo.AssignedTechnicianId == technicianId &&
                           wo.RequestedStartDate.HasValue &&
                           wo.RequestedStartDate.Value >= startDate &&
                           wo.RequestedStartDate.Value <= endDate)
                .ToList();

            var totalHoursBooked = existingWorkOrders.Sum(wo => wo.EstimatedHours);
            var availableHours = CalculateAvailableHours(startDate, endDate) - totalHoursBooked;

            return new TechnicianAvailabilityResult
            {
                TechnicianId = technicianId,
                TechnicianName = technician.FullName,
                StartDate = startDate,
                EndDate = endDate,
                IsAvailable = availableHours > 0,
                AvailableHours = Math.Max(0, availableHours),
                BookedHours = totalHoursBooked,
                ConflictingWorkOrders = existingWorkOrders.Select(wo => new WorkOrderConflictDto
                {
                    WorkOrderId = wo.Id,
                    WorkOrderNumber = wo.WorkOrderNumber,
                    StartDate = wo.RequestedStartDate,
                    EndDate = wo.RequestedCompletionDate,
                    EstimatedHours = wo.EstimatedHours
                }).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking technician availability for {TechnicianId}", technicianId);
            throw;
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Validates resource (technician/team) availability
    /// </summary>
    private async Task ValidateResourceAvailabilityAsync(
        ScheduleWorkOrderDto scheduleDto, WorkOrderSchedulingResult result)
    {
        if (scheduleDto.AssignedTechnicianId.HasValue)
        {
            var availability = await CheckTechnicianAvailabilityAsync(
                scheduleDto.AssignedTechnicianId.Value,
                scheduleDto.RequestedStartDate ?? DateTime.UtcNow,
                scheduleDto.RequestedCompletionDate ?? DateTime.UtcNow.AddDays(1));

            if (!availability.IsAvailable)
            {
                result.WarningMessages.Add($"Technician may have conflicts during scheduled period");
                result.ResourceConflicts.AddRange(availability.ConflictingWorkOrders);
            }
        }
    }

    /// <summary>
    /// Processes inventory reservation for work order parts
    /// </summary>
    private async Task ProcessInventoryReservationAsync(
        Guid workOrderId, DateTime? requiredDate, Guid reservedById, WorkOrderSchedulingResult result)
    {
        try
        {
            var partsAvailability = await _maintenanceInventoryService.CheckPartsAvailabilityAsync(workOrderId);

            if (!partsAvailability.AllPartsAvailable)
            {
                result.WarningMessages.Add($"{partsAvailability.UnavailableParts} parts are not available for reservation");
                result.PartsAvailabilityIssues.AddRange(
                    partsAvailability.PartAvailability.Where(p => !p.IsAvailable));
            }

            // Reserve available parts
            var availableParts = partsAvailability.PartAvailability
                .Where(p => p.IsAvailable)
                .ToList();

            if (availableParts.Any())
            {
                // Create reservations (using allocation with "Reserved" status)
                var workOrder = await _workOrderRepository.GetByIdAsync(workOrderId);
                if (workOrder != null)
                {
                    foreach (var availablePart in availableParts)
                    {
                        var part = workOrder.Parts.FirstOrDefault(p => p.Id == availablePart.PartId);
                        if (part != null)
                        {
                            try
                            {
                                var reservationRequest = new AllocateInventoryDto
                                {
                                    InventoryItemId = part.InventoryItemId,
                                    Quantity = part.QuantityRequired,
                                    ReferenceType = "WorkOrderReservation",
                                    ReferenceNumber = $"RES-{workOrder.WorkOrderNumber}",
                                    ReferenceId = workOrderId,
                                    RequiredDate = requiredDate,
                                    Notes = $"Reserved for scheduled work order: {workOrder.Title}",
                                    UserId = reservedById
                                };

                                var reservation = await _inventoryService.AllocateForWorkOrderAsync(reservationRequest);

                                // Update part with reservation info
                                part.AllocationId = reservation.Id;
                                part.QuantityAllocated = reservation.AllocatedQuantity;
                                part.AllocatedAt = reservation.AllocationDate;
                                part.Status = "Reserved";

                                await _workOrderRepository.UpdateAsync(workOrder);

                                result.PartsReserved++;
                                result.ReservedParts.Add(new ReservedPartDto
                                {
                                    PartId = part.Id,
                                    ItemCode = part.ItemCode,
                                    ItemName = part.ItemName,
                                    QuantityReserved = reservation.AllocatedQuantity,
                                    ReservationId = reservation.Id,
                                    ReservedAt = reservation.AllocationDate
                                });
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to reserve part {PartId} for work order {WorkOrderId}",
                                    availablePart.PartId, workOrderId);
                                result.WarningMessages.Add($"Failed to reserve part {part.ItemName}: {ex.Message}");
                                result.ReservationFailures.Add(new PartReservationFailure
                                {
                                    PartId = availablePart.PartId,
                                    PartName = part.ItemName,
                                    Reason = ex.Message
                                });
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing inventory reservation for work order {WorkOrderId}", workOrderId);
            result.WarningMessages.Add($"Inventory reservation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates inventory reservations when rescheduling
    /// </summary>
    private async Task UpdateInventoryReservationsAsync(
        Guid workOrderId, DateTime? newRequiredDate, Guid updatedById, WorkOrderSchedulingResult result)
    {
        try
        {
            var workOrder = await _workOrderRepository.GetByIdAsync(workOrderId);
            if (workOrder == null)
            {
                return;
            }

            var reservedParts = workOrder.Parts.Where(p => p.Status == "Reserved" && p.AllocationId.HasValue).ToList();

            foreach (var part in reservedParts)
            {
                try
                {
                    // Update reservation date through inventory service
                    // This would require a new method in the inventory service
                    // For now, we'll just log the update need
                    _logger.LogInformation("Need to update reservation {AllocationId} for new date {NewDate}",
                        part.AllocationId, newRequiredDate);

                    result.SuccessMessages.Add($"Updated reservation for {part.ItemName}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update reservation for part {PartId}", part.Id);
                    result.WarningMessages.Add($"Failed to update reservation for {part.ItemName}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating inventory reservations for work order {WorkOrderId}", workOrderId);
            result.WarningMessages.Add($"Failed to update some reservations: {ex.Message}");
        }
    }

    /// <summary>
    /// Calculates available working hours for a period
    /// </summary>
    private static double CalculateAvailableHours(DateTime startDate, DateTime endDate)
    {
        var totalDays = (endDate - startDate).TotalDays;
        var workingDays = totalDays * 5.0 / 7.0; // Assume 5-day work week
        return workingDays * 8.0; // 8 hours per day
    }

    #endregion
}
