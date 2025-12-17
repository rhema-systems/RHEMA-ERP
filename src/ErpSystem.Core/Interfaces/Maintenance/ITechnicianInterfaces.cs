using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Core.Interfaces.Maintenance;

// Placeholder interfaces to resolve build issues
public interface ITechnicianSchedulingService
{
    // Technician availability methods
    Task<IEnumerable<object>> GetAvailableTechniciansAsync(DateTime startDate, DateTime endDate, string? requiredSkills = null);
    Task<bool> IsTechnicianAvailableAsync(Guid technicianId, DateTime startDate, DateTime endDate);
    Task<TechnicianAvailabilityDto> GetTechnicianAvailabilityAsync(Guid technicianId, DateTime startDate, DateTime endDate);

    // Scheduling methods
    Task<object> ScheduleWorkOrderAsync(Guid workOrderId, Guid technicianId, DateTime scheduledDate);
    Task<object> FindBestTechnicianAsync(Guid workOrderId, DateTime scheduledDate);

    // Workload management
    Task<TechnicianWorkloadDto> GetTechnicianWorkloadAsync(Guid technicianId, DateTime startDate, DateTime endDate);
    Task SetTechnicianAvailabilityAsync(Guid technicianId, DateTime startDate, DateTime endDate, string availabilityType, string? reason = null);
}

public interface ITechnicianScheduleRepository : IGenericRepository<TechnicianSchedule>
{
    Task<IEnumerable<TechnicianSchedule>> GetByTechnicianIdAsync(Guid technicianId);
    Task<IEnumerable<TechnicianSchedule>> GetByWorkOrderIdAsync(Guid workOrderId);
    Task<IEnumerable<TechnicianSchedule>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<TechnicianSchedule>> GetByTechnicianAndDateRangeAsync(Guid technicianId, DateTime startDate, DateTime endDate);
    Task<IEnumerable<TechnicianSchedule>> GetByStatusAsync(string status);
    Task<IEnumerable<TechnicianSchedule>> GetOverdueSchedulesAsync();
    Task<bool> HasConflictingScheduleAsync(Guid technicianId, DateTime startDate, DateTime endDate, Guid? excludeScheduleId = null);
    Task<double> GetTotalScheduledHoursAsync(Guid technicianId, DateTime startDate, DateTime endDate);

    // Optimized navigation property loading methods
    Task<IEnumerable<TechnicianSchedule>> GetByTechnicianIdLightweightAsync(Guid technicianId);
    Task<IEnumerable<TechnicianSchedule>> GetByTechnicianIdWithFullDetailsAsync(Guid technicianId);
}

public interface ITechnicianAvailabilityRepository : IGenericRepository<TechnicianAvailability>
{
    Task<IEnumerable<TechnicianAvailability>> GetByTechnicianIdAsync(Guid technicianId);
    Task<IEnumerable<TechnicianAvailability>> GetByTechnicianAndDateRangeAsync(Guid technicianId, DateTime startDate, DateTime endDate);
    Task<IEnumerable<TechnicianAvailability>> GetByAvailabilityTypeAsync(string availabilityType);
    Task<IEnumerable<TechnicianAvailability>> GetByReasonAsync(string reason);
    Task<IEnumerable<TechnicianAvailability>> GetRecurringAvailabilityAsync();
    Task<IEnumerable<TechnicianAvailability>> GetUnavailablePeriodsByTechnicianAsync(Guid technicianId, DateTime startDate, DateTime endDate);
    Task<bool> IsTechnicianAvailableAsync(Guid technicianId, DateTime startDate, DateTime endDate);
    Task<double> GetAvailableHoursAsync(Guid technicianId, DateTime startDate, DateTime endDate);
}

public interface ITechnicianShiftRepository : IGenericRepository<TechnicianShift>
{
    Task<IEnumerable<TechnicianShift>> GetActiveShiftsAsync();
    Task<TechnicianShift?> GetByNameAsync(string name);
    Task<IEnumerable<TechnicianShift>> GetShiftsByDayOfWeekAsync(int dayOfWeek);
    Task<bool> IsNameUniqueAsync(string name, Guid? excludeId = null);
    Task<IEnumerable<TechnicianShift>> GetShiftsInTimeRangeAsync(TimeSpan startTime, TimeSpan endTime);
    Task<double> GetTotalShiftHoursAsync(Guid shiftId);
    Task<int> GetScheduleCountByShiftAsync(Guid shiftId);
}

public interface IEmployeeRepository : IGenericRepository<Employee>
{
    Task<IEnumerable<Employee>> GetByDepartmentAsync(string department);
    Task<Employee?> GetByEmployeeNumberAsync(string employeeNumber);
    Task<bool> IsEmployeeNumberUniqueAsync(string employeeNumber, Guid? excludeId = null);
    Task<IEnumerable<Employee>> GetByStatusAsync(string status);
    Task<IEnumerable<Employee>> GetActiveEmployeesAsync();
    Task<IEnumerable<Employee>> GetByPositionTitleAsync(string positionTitle);
    Task<IEnumerable<Employee>> GetByManagerIdAsync(Guid managerId);
    Task<IEnumerable<Employee>> SearchEmployeesAsync(string searchTerm);
    Task<IEnumerable<Employee>> GetByHireDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<int> GetEmployeeCountByDepartmentAsync(string department);

    // Optimized navigation property loading methods
    Task<IEnumerable<Employee>> GetActiveEmployeesLightweightAsync();
    Task<IEnumerable<Employee>> GetActiveEmployeesWithHierarchyAsync();
}
