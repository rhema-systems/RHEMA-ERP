using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.Extensions.Logging;
using System.Data;

namespace ErpSystem.Core.Services.Maintenance;

/// <summary>
/// Service for scheduling work orders with automatic inventory reservation
/// </summary>
public class WorkOrderSchedulingService : IWorkOrderSchedulingService
{
    private readonly IWorkOrderRepository _workOrderRepository;
    private readonly IInventoryWorkOrderReservationService _reservations;
    private readonly IMaintenanceInventoryService _maintenanceInventoryService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly ErpSystem.Core.Interfaces.Maintenance.IEmployeeRepository _employeeRepository;
    private readonly ILogger<WorkOrderSchedulingService> _logger;

    public WorkOrderSchedulingService(
        IWorkOrderRepository workOrderRepository,
        IInventoryWorkOrderReservationService reservations,
        IMaintenanceInventoryService maintenanceInventoryService,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        ErpSystem.Core.Interfaces.Maintenance.IEmployeeRepository employeeRepository,
        ILogger<WorkOrderSchedulingService> logger)
    {
        _workOrderRepository = workOrderRepository;
        _reservations = reservations;
        _maintenanceInventoryService = maintenanceInventoryService;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _employeeRepository = employeeRepository;
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
            EnsureActor(scheduledById);
            return await ExecuteAtomicAsync(async () =>
            {
                _logger.LogInformation("Scheduling work order {WorkOrderId} by user {ScheduledById}",
                    scheduleDto.WorkOrderId, scheduledById);

                var workOrder = await _workOrderRepository.GetByIdAsync(scheduleDto.WorkOrderId)
                    ?? throw new ArgumentException($"Work order {scheduleDto.WorkOrderId} not found");
                if (workOrder.TenantId != _currentUser.TenantId)
                    throw new InventoryWorkOrderReservationAuthorizationException("The work order is outside the current tenant.");
                if (workOrder.Status != "Approved")
                    throw new InvalidOperationException($"Cannot schedule work order in {workOrder.Status} status");

                var result = new WorkOrderSchedulingResult
                {
                    WorkOrderId = scheduleDto.WorkOrderId, ScheduledById = scheduledById,
                    SchedulingDate = DateTime.UtcNow, Success = true
                };

                await ValidateResourceAvailabilityAsync(scheduleDto, result);

                if (scheduleDto.ReserveParts && workOrder.Parts.Any())
                {
                    var reservation = await _reservations.ReserveForScheduleAsync(scheduleDto.WorkOrderId,
                        scheduleDto.RequestedStartDate, $"schedule:{scheduleDto.WorkOrderId:N}:{scheduledById:N}",
                        $"work-order-schedule:{scheduleDto.WorkOrderId:N}");
                    result.PartsReserved = reservation.AffectedParts;
                }

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
                    result.SuccessMessages.Add($"Reserved {result.PartsReserved} parts for the scheduled work");

                _logger.LogInformation("Work order {WorkOrderId} scheduled successfully for {StartDate}. " +
                    "Reserved parts: {PartsReserved}, Technician: {TechnicianId}",
                    scheduleDto.WorkOrderId, scheduleDto.RequestedStartDate, result.PartsReserved,
                    scheduleDto.AssignedTechnicianId);

                return result;
            });
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
            EnsureActor(rescheduledById);
            return await ExecuteAtomicAsync(async () =>
            {
                _logger.LogInformation("Rescheduling work order {WorkOrderId} by user {RescheduledById}",
                    rescheduleDto.WorkOrderId, rescheduledById);

                var workOrder = await _workOrderRepository.GetByIdAsync(rescheduleDto.WorkOrderId)
                    ?? throw new ArgumentException($"Work order {rescheduleDto.WorkOrderId} not found");
                if (workOrder.TenantId != _currentUser.TenantId)
                    throw new InventoryWorkOrderReservationAuthorizationException("The work order is outside the current tenant.");
                if (workOrder.Status != "Assigned" && workOrder.Status != "OnHold")
                    throw new InvalidOperationException($"Cannot reschedule work order in {workOrder.Status} status");

            var result = new WorkOrderSchedulingResult
            {
                WorkOrderId = rescheduleDto.WorkOrderId,
                ScheduledById = rescheduledById,
                SchedulingDate = DateTime.UtcNow,
                PreviousStartDate = workOrder.RequestedStartDate,
                PreviousCompletionDate = workOrder.RequestedCompletionDate,
                Success = true
            };

            if (workOrder.Parts.Any() && rescheduleDto.UpdateReservations)
            {
                var reservation = await _reservations.RescheduleAsync(rescheduleDto.WorkOrderId,
                    rescheduleDto.NewStartDate, rescheduleDto.RescheduleReason,
                    $"reschedule:{rescheduleDto.WorkOrderId:N}:{rescheduledById:N}:{rescheduleDto.NewStartDate:O}",
                    $"work-order-reschedule:{rescheduleDto.WorkOrderId:N}");
                if (reservation.AffectedParts > 0)
                    result.SuccessMessages.Add($"Updated {reservation.AffectedParts} inventory reservation(s)");
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
            });
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

    private async Task<T> ExecuteAtomicAsync<T>(Func<Task<T>> action)
    {
        if (_unitOfWork.HasActiveTransaction) return await action();
        return await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var result = await action();
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

    private void EnsureActor(Guid suppliedActorId)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.IsExternalUser || _currentUser.UserId == Guid.Empty ||
            _currentUser.TenantId == Guid.Empty || suppliedActorId != _currentUser.UserId)
            throw new InventoryWorkOrderReservationAuthorizationException(
                "The authenticated tenant actor must perform the scheduling operation.");
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
