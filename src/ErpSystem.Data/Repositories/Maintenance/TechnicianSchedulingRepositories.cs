using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.Maintenance;

#region Technician Scheduling Repository Implementations

public class TechnicianScheduleRepository : GenericRepository<TechnicianSchedule>, ITechnicianScheduleRepository
{
    public TechnicianScheduleRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<TechnicianSchedule>> GetByTechnicianIdAsync(Guid technicianId)
    {
        if (technicianId == Guid.Empty)
        {
            throw new ArgumentException("Technician ID cannot be empty", nameof(technicianId));
        }

        return await _dbSet
            .Where(ts => ts.TechnicianId == technicianId && !ts.IsDeleted)
            .Include(ts => ts.WorkOrder)
                .ThenInclude(wo => wo!.Asset)
            .Include(ts => ts.WorkOrder)
                .ThenInclude(wo => wo!.PriorityLevel)
            .Include(ts => ts.Shift)
            .OrderBy(ts => ts.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TechnicianSchedule>> GetByWorkOrderIdAsync(Guid workOrderId)
    {
        if (workOrderId == Guid.Empty)
        {
            throw new ArgumentException("Work Order ID cannot be empty", nameof(workOrderId));
        }

        return await _dbSet
            .Where(ts => ts.WorkOrderId == workOrderId && !ts.IsDeleted)
            .Include(ts => ts.WorkOrder)
            .OrderBy(ts => ts.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TechnicianSchedule>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        if (startDate >= endDate)
        {
            throw new ArgumentException("Start date must be before end date");
        }

        return await _dbSet
            .Where(ts => !ts.IsDeleted &&
                ts.StartDate >= startDate && ts.StartDate <= endDate)
            .Include(ts => ts.WorkOrder)
            .OrderBy(ts => ts.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TechnicianSchedule>> GetByTechnicianAndDateRangeAsync(Guid technicianId, DateTime startDate, DateTime endDate)
    {
        if (technicianId == Guid.Empty)
        {
            throw new ArgumentException("Technician ID cannot be empty", nameof(technicianId));
        }

        if (startDate >= endDate)
        {
            throw new ArgumentException("Start date must be before end date");
        }

        return await _dbSet
            .Where(ts => ts.TechnicianId == technicianId && !ts.IsDeleted &&
                ts.StartDate >= startDate && ts.StartDate <= endDate)
            .Include(ts => ts.WorkOrder)
            .Include(ts => ts.Shift)
            .OrderBy(ts => ts.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TechnicianSchedule>> GetByStatusAsync(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Status cannot be null or empty", nameof(status));
        }

        return await _dbSet
            .Where(ts => ts.Status == status && !ts.IsDeleted)
            .Include(ts => ts.WorkOrder)
            .OrderBy(ts => ts.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TechnicianSchedule>> GetOverdueSchedulesAsync()
    {
        var currentDate = DateTime.UtcNow;
        return await _dbSet
            .Where(ts => !ts.IsDeleted &&
                (ts.Status == "Scheduled" || ts.Status == "InProgress") &&
                ts.EndDate < currentDate)
            .Include(ts => ts.WorkOrder)
            .OrderBy(ts => ts.EndDate)
            .ToListAsync();
    }

    public async Task<bool> HasConflictingScheduleAsync(Guid technicianId, DateTime startDate, DateTime endDate, Guid? excludeScheduleId = null)
    {
        var query = _dbSet.Where(ts => ts.TechnicianId == technicianId && !ts.IsDeleted &&
            ts.Status != "Cancelled" &&
            ((startDate >= ts.StartDate && startDate < ts.EndDate) ||
             (endDate > ts.StartDate && endDate <= ts.EndDate) ||
             (startDate <= ts.StartDate && endDate >= ts.EndDate)));

        if (excludeScheduleId.HasValue)
        {
            query = query.Where(ts => ts.Id != excludeScheduleId.Value);
        }

        return await query.AnyAsync();
    }

    public async Task<double> GetTotalScheduledHoursAsync(Guid technicianId, DateTime startDate, DateTime endDate)
    {
        return await _dbSet
            .Where(ts => ts.TechnicianId == technicianId && !ts.IsDeleted &&
                ts.StartDate >= startDate && ts.StartDate <= endDate &&
                (ts.Status == "Scheduled" || ts.Status == "InProgress"))
            .SumAsync(ts => ts.EstimatedHours);
    }

    /// <summary>
    /// Gets technician schedules with minimal navigation properties for performance
    /// </summary>
    public async Task<IEnumerable<TechnicianSchedule>> GetByTechnicianIdLightweightAsync(Guid technicianId)
    {
        return await _dbSet
            .Where(ts => ts.TechnicianId == technicianId && !ts.IsDeleted)
            .OrderBy(ts => ts.StartDate)
            .ToListAsync();
    }

    /// <summary>
    /// Gets technician schedules with full details for reporting/display
    /// </summary>
    public async Task<IEnumerable<TechnicianSchedule>> GetByTechnicianIdWithFullDetailsAsync(Guid technicianId)
    {
        return await _dbSet
            .Where(ts => ts.TechnicianId == technicianId && !ts.IsDeleted)
            .Include(ts => ts.WorkOrder)
                .ThenInclude(wo => wo!.Asset)
            .Include(ts => ts.WorkOrder)
                .ThenInclude(wo => wo!.PriorityLevel)
            .Include(ts => ts.WorkOrder)
                .ThenInclude(wo => wo!.MaintenanceType)
            .Include(ts => ts.Shift)
            .OrderBy(ts => ts.StartDate)
            .ToListAsync();
    }
}

public class TechnicianAvailabilityRepository : GenericRepository<TechnicianAvailability>, ITechnicianAvailabilityRepository
{
    public TechnicianAvailabilityRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<TechnicianAvailability>> GetByTechnicianIdAsync(Guid technicianId)
    {
        if (technicianId == Guid.Empty)
        {
            throw new ArgumentException("Technician ID cannot be empty", nameof(technicianId));
        }

        return await _dbSet
            .Where(ta => ta.TechnicianId == technicianId && !ta.IsDeleted)
            .OrderBy(ta => ta.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TechnicianAvailability>> GetByTechnicianAndDateRangeAsync(Guid technicianId, DateTime startDate, DateTime endDate)
    {
        if (technicianId == Guid.Empty)
        {
            throw new ArgumentException("Technician ID cannot be empty", nameof(technicianId));
        }

        if (startDate >= endDate)
        {
            throw new ArgumentException("Start date must be before end date");
        }

        return await _dbSet
            .Where(ta => ta.TechnicianId == technicianId && !ta.IsDeleted &&
                ta.StartDate <= endDate && ta.EndDate >= startDate)
            .OrderBy(ta => ta.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TechnicianAvailability>> GetByAvailabilityTypeAsync(string availabilityType)
    {
        if (string.IsNullOrWhiteSpace(availabilityType))
        {
            throw new ArgumentException("Availability type cannot be null or empty", nameof(availabilityType));
        }

        return await _dbSet
            .Where(ta => ta.AvailabilityType == availabilityType && !ta.IsDeleted)
            .OrderBy(ta => ta.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TechnicianAvailability>> GetByReasonAsync(string reason)
    {
        return await _dbSet
            .Where(ta => ta.Reason == reason && !ta.IsDeleted)
            .OrderBy(ta => ta.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TechnicianAvailability>> GetRecurringAvailabilityAsync()
    {
        return await _dbSet
            .Where(ta => ta.IsRecurring && !ta.IsDeleted)
            .OrderBy(ta => ta.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TechnicianAvailability>> GetUnavailablePeriodsByTechnicianAsync(Guid technicianId, DateTime startDate, DateTime endDate)
    {
        return await _dbSet
            .Where(ta => ta.TechnicianId == technicianId && !ta.IsDeleted &&
                ta.AvailabilityType == "Unavailable" &&
                ta.StartDate <= endDate && ta.EndDate >= startDate)
            .OrderBy(ta => ta.StartDate)
            .ToListAsync();
    }

    public async Task<bool> IsTechnicianAvailableAsync(Guid technicianId, DateTime startDate, DateTime endDate)
    {
        // Check for any unavailable periods that overlap with the requested time
        var unavailablePeriods = await _dbSet
            .Where(ta => ta.TechnicianId == technicianId && !ta.IsDeleted &&
                ta.AvailabilityType == "Unavailable" &&
                ta.StartDate <= endDate && ta.EndDate >= startDate)
            .AnyAsync();

        return !unavailablePeriods;
    }

    public async Task<double> GetAvailableHoursAsync(Guid technicianId, DateTime startDate, DateTime endDate)
    {
        var availabilityRecords = await _dbSet
            .Where(ta => ta.TechnicianId == technicianId && !ta.IsDeleted &&
                ta.AvailabilityType == "Available" &&
                ta.StartDate <= endDate && ta.EndDate >= startDate &&
                ta.AvailableHours.HasValue)
            .ToListAsync();

        return availabilityRecords.Sum(ta => ta.AvailableHours ?? 0.0);
    }

    /// <summary>
    /// Validates business rules for technician availability
    /// </summary>
    public async Task<(bool IsValid, string[] ValidationErrors)> ValidateAvailabilityAsync(TechnicianAvailability availability)
    {
        var errors = new List<string>();

        try
        {
            if (availability == null)
            {
                errors.Add("Availability record cannot be null");
                return (false, errors.ToArray());
            }

            // Basic validation
            if (availability.TechnicianId == Guid.Empty)
            {
                errors.Add("Valid technician ID is required");
            }

            if (availability.StartDate >= availability.EndDate)
            {
                errors.Add("Start date must be before end date");
            }

            if (string.IsNullOrWhiteSpace(availability.AvailabilityType))
            {
                errors.Add("Availability type is required");
            }

            if (string.IsNullOrWhiteSpace(availability.Reason))
            {
                errors.Add("Reason for availability change is required");
            }

            // Business rule validation
            var validAvailabilityTypes = new[] { "Available", "Unavailable", "PartiallyAvailable" };
            if (!validAvailabilityTypes.Contains(availability.AvailabilityType))
            {
                errors.Add($"Invalid availability type. Must be one of: {string.Join(", ", validAvailabilityTypes)}");
            }

            // Validate capacity percentage
            if (availability.CapacityPercentage.HasValue &&
                (availability.CapacityPercentage < 0 || availability.CapacityPercentage > 100))
            {
                errors.Add("Capacity percentage must be between 0 and 100");
            }

            // Validate available hours
            if (availability.AvailableHours.HasValue && availability.AvailableHours < 0)
            {
                errors.Add("Available hours cannot be negative");
            }

            // Check for overlapping availability records
            var overlapping = await _dbSet
                .Where(a => a.TechnicianId == availability.TechnicianId &&
                           a.Id != availability.Id &&
                           !a.IsDeleted &&
                           a.StartDate < availability.EndDate &&
                           a.EndDate > availability.StartDate)
                .AnyAsync();

            if (overlapping)
            {
                errors.Add("Availability period overlaps with existing availability records");
            }

            return (errors.Count == 0, errors.ToArray());
        }
        catch (Exception)
        {
            errors.Add("Validation failed due to system error");
            return (false, errors.ToArray());
        }
    }

    /// <summary>
    /// Overridden Add method with validation
    /// </summary>
    public override async Task<TechnicianAvailability> AddAsync(TechnicianAvailability entity)
    {
        var validation = await ValidateAvailabilityAsync(entity);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Availability validation failed: {string.Join(", ", validation.ValidationErrors)}");
        }

        return await base.AddAsync(entity);
    }

    /// <summary>
    /// Overridden Update method with validation
    /// </summary>
    public override async Task UpdateAsync(TechnicianAvailability entity)
    {
        var validation = await ValidateAvailabilityAsync(entity);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Availability validation failed: {string.Join(", ", validation.ValidationErrors)}");
        }

        await base.UpdateAsync(entity);
    }
}

public class TechnicianShiftRepository : GenericRepository<TechnicianShift>, ITechnicianShiftRepository
{
    public TechnicianShiftRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<TechnicianShift>> GetActiveShiftsAsync()
    {
        return await _dbSet
            .Where(ts => ts.IsActive && !ts.IsDeleted)
            .OrderBy(ts => ts.StartTime)
            .ToListAsync();
    }

    public async Task<TechnicianShift?> GetByNameAsync(string name)
    {
        return await _dbSet
            .Where(ts => ts.Name == name && !ts.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<TechnicianShift>> GetShiftsByDayOfWeekAsync(int dayOfWeek)
    {
        return await _dbSet
            .Where(ts => !ts.IsDeleted && ts.DaysOfWeek.Contains(dayOfWeek.ToString()))
            .OrderBy(ts => ts.StartTime)
            .ToListAsync();
    }

    public async Task<bool> IsNameUniqueAsync(string name, Guid? excludeId = null)
    {
        var query = _dbSet.Where(ts => ts.Name == name && !ts.IsDeleted);
        if (excludeId.HasValue)
        {
            query = query.Where(ts => ts.Id != excludeId.Value);
        }

        return !await query.AnyAsync();
    }

    public async Task<IEnumerable<TechnicianShift>> GetShiftsInTimeRangeAsync(TimeSpan startTime, TimeSpan endTime)
    {
        return await _dbSet
            .Where(ts => !ts.IsDeleted &&
                ((ts.StartTime >= startTime && ts.StartTime <= endTime) ||
                 (ts.EndTime >= startTime && ts.EndTime <= endTime) ||
                 (ts.StartTime <= startTime && ts.EndTime >= endTime)))
            .OrderBy(ts => ts.StartTime)
            .ToListAsync();
    }

    public async Task<double> GetTotalShiftHoursAsync(Guid shiftId)
    {
        var shift = await GetByIdAsync(shiftId);
        if (shift == null)
        {
            return 0;
        }

        // Calculate hours considering break time
        var totalMinutes = (shift.EndTime - shift.StartTime).TotalMinutes - shift.BreakMinutes;
        return totalMinutes / 60.0; // Convert to hours
    }

    public async Task<int> GetScheduleCountByShiftAsync(Guid shiftId)
    {
        return await _context.Set<TechnicianSchedule>()
            .Where(ts => ts.ShiftId == shiftId && !ts.IsDeleted)
            .CountAsync();
    }
}

public class EmployeeRepository : GenericRepository<Employee>, IEmployeeRepository
{
    public EmployeeRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<Employee>> GetByDepartmentAsync(string department)
    {
        if (string.IsNullOrWhiteSpace(department))
        {
            throw new ArgumentException("Department cannot be null or empty", nameof(department));
        }

        return await _dbSet
            .Include(e => e.Department)
            .Where(e => e.Department != null && e.Department.Name == department && !e.IsDeleted)
            .OrderBy(e => e.LastName)
            .ThenBy(e => e.FirstName)
            .ToListAsync();
    }

    public async Task<Employee?> GetByEmployeeNumberAsync(string employeeNumber)
    {
        if (string.IsNullOrWhiteSpace(employeeNumber))
        {
            throw new ArgumentException("Employee number cannot be null or empty", nameof(employeeNumber));
        }

        return await _dbSet
            .Where(e => e.EmployeeNumber == employeeNumber && !e.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> IsEmployeeNumberUniqueAsync(string employeeNumber, Guid? excludeId = null)
    {
        var query = _dbSet.Where(e => e.EmployeeNumber == employeeNumber && !e.IsDeleted);
        if (excludeId.HasValue)
        {
            query = query.Where(e => e.Id != excludeId.Value);
        }

        return !await query.AnyAsync();
    }

    public async Task<IEnumerable<Employee>> GetByStatusAsync(string status)
    {
        // Convert string to StaffStatus enum
        if (Enum.TryParse<StaffStatus>(status, true, out var staffStatus))
        {
            return await _dbSet
                .Where(e => e.StaffStatus == staffStatus && !e.IsDeleted)
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .ToListAsync();
        }
        return new List<Employee>();
    }

    public async Task<IEnumerable<Employee>> GetActiveEmployeesAsync()
    {
        return await _dbSet
            .Where(e => e.IsActive && !e.IsDeleted)
            .Include(e => e.Department)
            .Include(e => e.Position)
            .OrderBy(e => e.LastName)
            .ThenBy(e => e.FirstName)
            .ToListAsync();
    }

    /// <summary>
    /// Gets active employees with minimal data for dropdowns/selections
    /// </summary>
    public async Task<IEnumerable<Employee>> GetActiveEmployeesLightweightAsync()
    {
        return await _dbSet
            .Where(e => e.IsActive && !e.IsDeleted)
            .OrderBy(e => e.LastName)
            .ThenBy(e => e.FirstName)
            .ToListAsync();
    }

    /// <summary>
    /// Gets active employees with full hierarchy and department information
    /// </summary>
    public async Task<IEnumerable<Employee>> GetActiveEmployeesWithHierarchyAsync()
    {
        return await _dbSet
            .Where(e => e.IsActive && !e.IsDeleted)
            .Include(e => e.Department)
            .Include(e => e.Position)
            .Include(e => e.Manager)
            .Include(e => e.DirectReports)
            .OrderBy(e => e.LastName)
            .ThenBy(e => e.FirstName)
            .ToListAsync();
    }

    public async Task<IEnumerable<Employee>> GetByPositionTitleAsync(string positionTitle)
    {
        return await _dbSet
            .Include(e => e.Position)
            .Where(e => e.Position != null && e.Position.Title == positionTitle && !e.IsDeleted)
            .OrderBy(e => e.LastName)
            .ThenBy(e => e.FirstName)
            .ToListAsync();
    }

    public async Task<IEnumerable<Employee>> GetByManagerIdAsync(Guid managerId)
    {
        return await _dbSet
            .Where(e => e.ManagerId == managerId && !e.IsDeleted)
            .OrderBy(e => e.LastName)
            .ThenBy(e => e.FirstName)
            .ToListAsync();
    }

    public async Task<IEnumerable<Employee>> SearchEmployeesAsync(string searchTerm)
    {
        return await _dbSet
            .Where(e => !e.IsDeleted && (
                e.FirstName.Contains(searchTerm) ||
                e.LastName.Contains(searchTerm) ||
                e.EmailAddress.Contains(searchTerm) ||
                e.EmployeeNumber.Contains(searchTerm) ||
                (e.MobileNumber != null && e.MobileNumber.Contains(searchTerm))))
            .OrderBy(e => e.LastName)
            .ThenBy(e => e.FirstName)
            .ToListAsync();
    }

    public async Task<IEnumerable<Employee>> GetByHireDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        var startDateOnly = DateOnly.FromDateTime(startDate);
        var endDateOnly = DateOnly.FromDateTime(endDate);

        return await _dbSet
            .Where(e => !e.IsDeleted &&
                e.DateEmployed.HasValue &&
                e.DateEmployed >= startDateOnly && e.DateEmployed <= endDateOnly)
            .OrderBy(e => e.DateEmployed)
            .ToListAsync();
    }

    public async Task<int> GetEmployeeCountByDepartmentAsync(string department)
    {
        return await _dbSet
            .Include(e => e.Department)
            .Where(e => e.Department != null && e.Department.Name == department && !e.IsDeleted)
            .CountAsync();
    }
}

#endregion
