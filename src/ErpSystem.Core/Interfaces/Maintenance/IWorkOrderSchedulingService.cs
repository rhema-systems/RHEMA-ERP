using ErpSystem.Core.DTOs.Maintenance;

namespace ErpSystem.Core.Interfaces.Maintenance;

/// <summary>
/// Interface for work order scheduling service with inventory reservation
/// </summary>
public interface IWorkOrderSchedulingService
{
    #region Work Order Scheduling

    /// <summary>
    /// Schedules a work order with automatic inventory reservation
    /// </summary>
    Task<WorkOrderSchedulingResult> ScheduleWorkOrderAsync(
        ScheduleWorkOrderDto scheduleDto, Guid scheduledById);

    /// <summary>
    /// Reschedules a work order with inventory reservation updates
    /// </summary>
    Task<WorkOrderSchedulingResult> RescheduleWorkOrderAsync(
        RescheduleWorkOrderDto rescheduleDto, Guid rescheduledById);

    /// <summary>
    /// Gets work orders that need scheduling (approved but not assigned)
    /// </summary>
    Task<List<WorkOrderForSchedulingDto>> GetWorkOrdersForSchedulingAsync(
        DateTime? startDate = null, DateTime? endDate = null);

    #endregion

    #region Resource Availability

    /// <summary>
    /// Checks technician availability for a given time period
    /// </summary>
    Task<TechnicianAvailabilityResult> CheckTechnicianAvailabilityAsync(
        Guid technicianId, DateTime startDate, DateTime endDate);

    #endregion
}