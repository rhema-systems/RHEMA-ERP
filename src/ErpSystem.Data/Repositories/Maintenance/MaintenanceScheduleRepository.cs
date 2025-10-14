using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;

namespace ErpSystem.Data.Repositories.Maintenance;

/// <summary>
/// Repository implementation for maintenance schedule operations
/// </summary>
public class MaintenanceScheduleRepository : GenericRepository<MaintenanceSchedule>, IMaintenanceScheduleRepository
{
    public MaintenanceScheduleRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<MaintenanceSchedule>> GetActiveSchedulesAsync()
    {
        return await _context.MaintenanceSchedules
            .Where(ms => ms.IsActive && !ms.IsDeleted)
            .Include(ms => ms.Asset)
            .Include(ms => ms.MaintenanceType)
            .Include(ms => ms.DefaultTechnician)
            .Include(ms => ms.DefaultTeam)
            .Include(ms => ms.PriorityLevel)
            .OrderBy(ms => ms.NextDueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MaintenanceSchedule>> GetByAssetIdAsync(Guid assetId)
    {
        return await _context.MaintenanceSchedules
            .Where(ms => ms.AssetId == assetId && !ms.IsDeleted)
            .Include(ms => ms.MaintenanceType)
            .Include(ms => ms.DefaultTechnician)
            .Include(ms => ms.DefaultTeam)
            .Include(ms => ms.PriorityLevel)
            .OrderBy(ms => ms.NextDueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MaintenanceSchedule>> GetByMaintenanceTypeIdAsync(Guid maintenanceTypeId)
    {
        return await _context.MaintenanceSchedules
            .Where(ms => ms.MaintenanceTypeId == maintenanceTypeId && !ms.IsDeleted)
            .Include(ms => ms.Asset)
            .Include(ms => ms.DefaultTechnician)
            .Include(ms => ms.DefaultTeam)
            .Include(ms => ms.PriorityLevel)
            .OrderBy(ms => ms.NextDueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MaintenanceSchedule>> GetByScheduleTypeAsync(string scheduleType)
    {
        return await _context.MaintenanceSchedules
            .Where(ms => ms.ScheduleType == scheduleType && !ms.IsDeleted)
            .Include(ms => ms.Asset)
            .Include(ms => ms.MaintenanceType)
            .Include(ms => ms.DefaultTechnician)
            .Include(ms => ms.DefaultTeam)
            .Include(ms => ms.PriorityLevel)
            .OrderBy(ms => ms.NextDueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MaintenanceSchedule>> GetDueSchedulesAsync(DateTime dueDate)
    {
        return await _context.MaintenanceSchedules
            .Where(ms => ms.IsActive && ms.NextDueDate <= dueDate && !ms.IsDeleted)
            .Include(ms => ms.Asset)
            .Include(ms => ms.MaintenanceType)
            .Include(ms => ms.DefaultTechnician)
            .Include(ms => ms.DefaultTeam)
            .Include(ms => ms.PriorityLevel)
            .OrderBy(ms => ms.NextDueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MaintenanceSchedule>> GetOverdueSchedulesAsync()
    {
        var today = DateTime.UtcNow.Date;
        return await _context.MaintenanceSchedules
            .Where(ms => ms.IsActive && ms.NextDueDate < today && !ms.IsDeleted)
            .Include(ms => ms.Asset)
            .Include(ms => ms.MaintenanceType)
            .Include(ms => ms.DefaultTechnician)
            .Include(ms => ms.DefaultTeam)
            .Include(ms => ms.PriorityLevel)
            .OrderBy(ms => ms.NextDueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MaintenanceSchedule>> GetUpcomingSchedulesAsync(int daysAhead = 30)
    {
        var startDate = DateTime.UtcNow.Date;
        var endDate = startDate.AddDays(daysAhead);
        
        return await _context.MaintenanceSchedules
            .Where(ms => ms.IsActive && 
                        ms.NextDueDate >= startDate && 
                        ms.NextDueDate <= endDate && 
                        !ms.IsDeleted)
            .Include(ms => ms.Asset)
            .Include(ms => ms.MaintenanceType)
            .Include(ms => ms.DefaultTechnician)
            .Include(ms => ms.DefaultTeam)
            .Include(ms => ms.PriorityLevel)
            .OrderBy(ms => ms.NextDueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MaintenanceSchedule>> GetByTechnicianIdAsync(Guid technicianId)
    {
        return await _context.MaintenanceSchedules
            .Where(ms => ms.DefaultTechnicianId == technicianId && !ms.IsDeleted)
            .Include(ms => ms.Asset)
            .Include(ms => ms.MaintenanceType)
            .Include(ms => ms.DefaultTeam)
            .Include(ms => ms.PriorityLevel)
            .OrderBy(ms => ms.NextDueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MaintenanceSchedule>> GetByTeamIdAsync(Guid teamId)
    {
        return await _context.MaintenanceSchedules
            .Where(ms => ms.DefaultTeamId == teamId && !ms.IsDeleted)
            .Include(ms => ms.Asset)
            .Include(ms => ms.MaintenanceType)
            .Include(ms => ms.DefaultTechnician)
            .Include(ms => ms.PriorityLevel)
            .OrderBy(ms => ms.NextDueDate)
            .ToListAsync();
    }

    public async Task<bool> IsScheduleUniqueAsync(Guid assetId, Guid maintenanceTypeId, Guid? excludeId = null)
    {
        var query = _context.MaintenanceSchedules
            .Where(ms => ms.AssetId == assetId && 
                        ms.MaintenanceTypeId == maintenanceTypeId && 
                        !ms.IsDeleted);

        if (excludeId.HasValue)
        {
            query = query.Where(ms => ms.Id != excludeId.Value);
        }

        return !await query.AnyAsync();
    }

    public async Task<MaintenanceSchedule?> GetByCodeAsync(string code)
    {
        return await _context.MaintenanceSchedules
            .Where(ms => ms.Code == code && !ms.IsDeleted)
            .Include(ms => ms.Asset)
            .Include(ms => ms.MaintenanceType)
            .Include(ms => ms.DefaultTechnician)
            .Include(ms => ms.DefaultTeam)
            .Include(ms => ms.PriorityLevel)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> IsCodeUniqueAsync(string code, Guid? excludeId = null)
    {
        var query = _context.MaintenanceSchedules
            .Where(ms => ms.Code == code && !ms.IsDeleted);

        if (excludeId.HasValue)
        {
            query = query.Where(ms => ms.Id != excludeId.Value);
        }

        return !await query.AnyAsync();
    }

    public async Task<IEnumerable<MaintenanceSchedule>> GetSchedulesRequiringProcessingAsync()
    {
        var cutoffDate = DateTime.UtcNow.AddHours(-1); // Don't process same schedule multiple times within an hour
        
        return await _context.MaintenanceSchedules
            .Where(ms => ms.IsActive && 
                        ms.NextDueDate <= DateTime.UtcNow.AddDays(1) && // Due within next day
                        (ms.LastProcessedDate == null || ms.LastProcessedDate < cutoffDate) &&
                        !ms.IsDeleted)
            .Include(ms => ms.Asset)
            .Include(ms => ms.MaintenanceType)
            .Include(ms => ms.DefaultTechnician)
            .Include(ms => ms.DefaultTeam)
            .Include(ms => ms.PriorityLevel)
            .OrderBy(ms => ms.NextDueDate)
            .ToListAsync();
    }

    public async Task UpdateLastProcessedDateAsync(Guid scheduleId, DateTime processedDate)
    {
        var schedule = await GetByIdAsync(scheduleId);
        if (schedule != null)
        {
            schedule.LastProcessedDate = processedDate;
            await UpdateAsync(schedule);
        }
    }

    public async Task<IEnumerable<MaintenanceSchedule>> GetComplianceReportDataAsync(DateTime startDate, DateTime endDate)
    {
        return await _context.MaintenanceSchedules
            .Where(ms => ms.CreatedAt >= startDate && 
                        ms.CreatedAt <= endDate && 
                        !ms.IsDeleted)
            .Include(ms => ms.Asset)
            .Include(ms => ms.MaintenanceType)
            .Include(ms => ms.GeneratedWorkOrders.Where(wo => wo.CreatedAt >= startDate && wo.CreatedAt <= endDate))
            .OrderBy(ms => ms.CreatedAt)
            .ToListAsync();
    }

    public async Task<Dictionary<string, int>> GetScheduleTypeCountsAsync()
    {
        return await _context.MaintenanceSchedules
            .Where(ms => !ms.IsDeleted)
            .GroupBy(ms => ms.ScheduleType)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
    }

    public async Task<Dictionary<string, int>> GetScheduleStatusCountsAsync()
    {
        return await _context.MaintenanceSchedules
            .Where(ms => !ms.IsDeleted)
            .GroupBy(ms => ms.IsActive ? "Active" : "Inactive")
            .ToDictionaryAsync(g => g.Key, g => g.Count());
    }
    
    public async Task<IEnumerable<MaintenanceSchedule>> GetSchedulesDueForGenerationAsync()
    {
        var today = DateTime.UtcNow.Date;
        return await _context.MaintenanceSchedules
            .Where(ms => ms.IsActive && !ms.IsDeleted &&
                        ms.NextDueDate <= today &&
                        (ms.LastGeneratedDate == null || 
                         ms.LastGeneratedDate < ms.NextDueDate))
            .Include(ms => ms.Asset)
            .Include(ms => ms.MaintenanceType)
            .Include(ms => ms.DefaultTechnician)
            .Include(ms => ms.DefaultTeam)
            .Include(ms => ms.PriorityLevel)
            .OrderBy(ms => ms.NextDueDate)
            .ToListAsync();
    }
    
    public async Task<IEnumerable<MaintenanceSchedule>> GetByFrequencyAsync(string frequency)
    {
        return await _context.MaintenanceSchedules
            .Where(ms => ms.Frequency == frequency && !ms.IsDeleted)
            .Include(ms => ms.Asset)
            .Include(ms => ms.MaintenanceType)
            .Include(ms => ms.DefaultTechnician)
            .Include(ms => ms.DefaultTeam)
            .Include(ms => ms.PriorityLevel)
            .OrderBy(ms => ms.NextDueDate)
            .ToListAsync();
    }
    
    public async Task<IEnumerable<MaintenanceSchedule>> GetByMaintenanceTypeAsync(Guid maintenanceTypeId)
    {
        return await _context.MaintenanceSchedules
            .Where(ms => ms.MaintenanceTypeId == maintenanceTypeId && !ms.IsDeleted)
            .Include(ms => ms.Asset)
            .Include(ms => ms.MaintenanceType)
            .Include(ms => ms.DefaultTechnician)
            .Include(ms => ms.DefaultTeam)
            .Include(ms => ms.PriorityLevel)
            .OrderBy(ms => ms.NextDueDate)
            .ToListAsync();
    }
    
    public async Task<IEnumerable<MaintenanceSchedule>> GetSchedulesDueInDaysAsync(int days)
    {
        var targetDate = DateTime.UtcNow.Date.AddDays(days);
        return await _context.MaintenanceSchedules
            .Where(ms => ms.IsActive && !ms.IsDeleted &&
                        ms.NextDueDate <= targetDate)
            .Include(ms => ms.Asset)
            .Include(ms => ms.MaintenanceType)
            .Include(ms => ms.DefaultTechnician)
            .Include(ms => ms.DefaultTeam)
            .Include(ms => ms.PriorityLevel)
            .OrderBy(ms => ms.NextDueDate)
            .ToListAsync();
    }
    
    public async Task UpdateNextDueDateAsync(Guid scheduleId, DateTime nextDueDate)
    {
        var schedule = await GetByIdAsync(scheduleId);
        if (schedule != null)
        {
            schedule.NextDueDate = nextDueDate;
            schedule.UpdatedAt = DateTime.UtcNow;
            await UpdateAsync(schedule);
        }
    }
    
    public async Task UpdateLastGeneratedDateAsync(Guid scheduleId, DateTime lastGeneratedDate)
    {
        var schedule = await GetByIdAsync(scheduleId);
        if (schedule != null)
        {
            schedule.LastGeneratedDate = lastGeneratedDate;
            schedule.UpdatedAt = DateTime.UtcNow;
            await UpdateAsync(schedule);
        }
    }
}
